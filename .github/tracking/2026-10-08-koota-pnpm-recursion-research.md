# Koota pnpm recursion research

## Scope

Determine whether Wharf's image preparation or agent-container runtime caused
the `koota-entity-snapshot-rollback` agent to recursively launch
`pnpm add pnpm@10.28.1`, exhaust its PID limit, and stop making progress.

## Findings

### The prebuilt image was not rebuilt

- The task selects the prebuilt image
  `public.ecr.aws/d3j8x8q7/swe-bench-202605:kh71b98xenay7p45357036xq5x82tcdf-v1.1`
  (`tasks/koota-entity-snapshot-rollback/task.toml:28`).
- Wharf checks and returns a configured prebuilt image without rewriting its
  Dockerfile or deriving another image
  (`src/wharf/Image/DockerAgentImagePreparer.cs:35-52`).
- The active container used that exact image. Its image defaults are
  `WORKDIR /app`, the image-default root user, and pnpm 10.26.1. The image
  already contains pnpm 10.28.1 below
  `/root/.local/share/pnpm/.tools/pnpm/10.28.1`.

The Wharf image-build path is therefore not the cause of this failure.

### Wharf changes the runtime layout that pnpm observes

- Wharf copies `/app` into a host-backed `/workspace`, recursively assigns it
  to UID/GID `1000:1000`, and runs the agent as that identity
  (`src/wharf/Agent/AgentExecution.cs:559-576`,
  `src/wharf/Agent/AgentExecution.cs:313-323`).
- Wharf sets `HOME=/workspace/.home`, placing pnpm's managed-tool staging
  directory below the project being operated on
  (`src/wharf/Agent/AgentExecution.cs:321`).
- Koota explicitly sets `manage-package-manager-versions: true`
  (`jobs/copilot-gpt-5.6-luna-max-latest/all-2/koota-entity-snapshot-rollback__4b5db92a/workspace/pnpm-workspace.yaml:23`)
  and requests pnpm 10.28.1
  (`jobs/copilot-gpt-5.6-luna-max-latest/all-2/koota-entity-snapshot-rollback__4b5db92a/workspace/package.json:24`).
- Wharf sets
  `NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false`
  (`src/wharf/Agent/AgentExecution.cs:323`), but an in-container probe showed
  that `pnpm config get manage-package-manager-versions` returns `true` from
  `/workspace`. The project-level `pnpm-workspace.yaml` setting wins for this
  project. Both `NPM_CONFIG_...` and `PNPM_CONFIG_...` probes remained `true`
  in the workspace, while the same environment setting returned `false` from
  `/tmp`.
- The retained process tree started with the agent's
  `pnpm -F core test run` and recursively nested
  `pnpm add pnpm@10.28.1`. It reached Wharf's 1,024-process limit, after which
  Vitest failed with `EAGAIN`.

This is the same mechanism previously observed with Arktype, except that
Koota's project-level setting defeats the environment-only mitigation.

### Harbor preserves the image defaults

At Harbor main revision
`4b94505a91c5ddcb70b5740ac95718ddee13e5a0`:

- The prebuilt Docker Compose service specifies only the image and an idle
  command; it does not replace `HOME`, `USER`, or `WORKDIR`
  ([docker-compose-prebuilt.yaml](https://github.com/harbor-framework/harbor/blob/4b94505a91c5ddcb70b5740ac95718ddee13e5a0/src/harbor/environments/docker/docker-compose-prebuilt.yaml)).
- An unspecified agent user means the image's default user, such as root
  ([config.py](https://github.com/harbor-framework/harbor/blob/4b94505a91c5ddcb70b5740ac95718ddee13e5a0/src/harbor/models/task/config.py#L338-L346)).
- An unspecified task work directory falls back to the Dockerfile work
  directory
  ([definition.py](https://github.com/harbor-framework/harbor/blob/4b94505a91c5ddcb70b5740ac95718ddee13e5a0/src/harbor/environments/definition.py#L166-L173)).

For this image that means root in `/app` with `HOME=/root`. pnpm can use the
already populated `/root/.local/share/pnpm` cache, and any new managed-version
staging occurs outside the project tree. Wharf's security and evidence
retention choices intentionally differ, but the in-project home introduces
this pnpm failure mode.

### All-1 succeeding does not disprove the runtime defect

- All-1 completed this task in about 17 minutes and passed verification.
- All-1 invoked pnpm repeatedly, including the full workspace tests.
- All-2 used the same task image reference and runtime layout but entered the
  recursive managed-version installer.

The retained evidence does not identify the exact pnpm internal condition that
made All-2 enter managed-version installation while All-1 did not. The
run-to-run trigger is therefore unresolved. The causal prerequisites for the
All-2 hang are nevertheless observed directly: Koota enabled version
management, Wharf put `HOME` below the project, pnpm started self-install
staging there, and the resulting children rediscovered the same project.

## Recommendation

Keep the unprivileged UID/GID and host-backed workspace, but move the agent
home outside the project inside the container:

1. Create a separate host-backed per-trial home directory.
2. Mount it at a path such as `/home/wharf`.
3. Assign it to `1000:1000` during workspace initialization.
4. Set `HOME=/home/wharf` and place `XDG_CACHE_HOME` below that home.
5. Retain the existing PID and memory limits as containment, not as the pnpm
   recursion fix.
6. Add a bounded reproduction using Koota's
   `manage-package-manager-versions: true` configuration and assert that no
   nested `pnpm add pnpm@...` process appears.

This preserves Wharf's non-root execution and retained home state while
removing the project-ancestor relationship that makes pnpm's self-install
recursive. Do not rely solely on an environment variable that a project-level
pnpm setting can override.

## Alternatives not recommended

- Running the agent as root would match this image's Harbor behavior but would
  reintroduce host ownership failures and weaken the execution boundary.
- Editing the task's pnpm configuration would mutate benchmark inputs and
  would not protect other projects with equivalent settings.
- Raising or removing the PID limit would turn a contained trial failure into
  host-wide process exhaustion.
- Treating the prebuilt image as faulty would miss that it runs with a
  different `HOME`, user, and working tree layout under Wharf.
