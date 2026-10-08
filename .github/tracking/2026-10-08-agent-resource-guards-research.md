# Agent resource guards research

## Scope

Determine why the `arktype-json-schema-refs-dependencies` agent created an
unbounded process chain, whether Wharf contributed to the failure, and which
agent-container safeguards should be added before another N=32 run.

## Evidence

- The retained process tree showed `pnpm --filter @ark/json-schema test`
  followed by an indefinitely nested chain of
  `pnpm add pnpm@10.19.0` processes. This was a TypeScript trial, not a Rust
  compile.
- The task declares `"packageManager": "pnpm@10.19.0"` and has no lifecycle
  script that invokes `pnpm add pnpm` (`jobs/copilot-gpt-5.6-luna-max-latest/all-1/arktype-json-schema-refs-dependencies__ba8e700d/workspace/package.json:13`,
  `jobs/copilot-gpt-5.6-luna-max-latest/all-1/arktype-json-schema-refs-dependencies__ba8e700d/workspace/package.json:115`).
- The task image already contains pnpm 10.19.0. With its normal root home,
  `pnpm config get manage-package-manager-versions` returns `false`.
- Wharf changes `HOME` to `/workspace/.home` for the agent
  (`src/wharf/Agent/AgentExecution.cs:156`). That hides the image's root-user
  pnpm configuration. pnpm 10.19.0 then uses its default
  `manage-package-manager-versions=true`.
- pnpm stages managed CLI versions below `$HOME/.local/share/pnpm/.tools` by
  invoking `pnpm add pnpm@<version>`. Because Wharf places `$HOME` inside the
  repository, the staging process rediscovers the ancestor project's
  `packageManager` declaration and recursively starts the same installation.
- A bounded diagnostic container reproduced the recursion by changing only
  `HOME` to a directory beneath the project. Setting
  `NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false` made pnpm report `false`
  with the same home layout.
- Agent containers currently have no Docker memory or PID controls
  (`src/wharf/Agent/AgentExecution.cs:148`). The live container exceeded
  16 GiB before the run was stopped.
- Cancellation cleanup calls `cancellation.Cancel()` before Docker cleanup
  (`src/wharf/WharfApp.cs:17`). A cancellation callback exception can prevent
  cleanup from running; the stopped N=32 process left all 32 managed containers
  active.
- No local Pier job directory or public result for this exact trial was
  available. The evidence does not establish that Pier experienced the same
  failure. Pier would avoid this specific recursion if its effective home were
  outside the project or retained the image's pnpm configuration.

## Decision

1. Keep Wharf's writable in-workspace home, but set
   `NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false` in agent containers.
2. Apply the operator-approved defaults to agent containers:
   4 GiB memory, 4 GiB memory-plus-swap (no swap allowance), and 512 PIDs.
3. Ensure Docker cleanup executes even if cancellation notification throws.
4. Do not alter preparation or verifier limits in this change.

## Alternatives not taken

- Moving `HOME` outside the workspace would lose the retained per-trial home
  and would require another writable mount.
- Relying only on the pnpm mitigation would leave other pathological processes
  able to exhaust the host.
- Applying an 8 GiB limit would allow one trial to consume too much of a
  60 GiB N=32 host before containment.

