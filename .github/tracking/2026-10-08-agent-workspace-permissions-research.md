# Agent Workspace Permission Failures: Research

## Scope

Investigate the four agent failures reported in the supplied `wharf-run.jsonl`,
identify the shared cause, and recommend a single remediation. No implementation
is included in this phase.

## Evidence

The run log reports exactly four early agent failures:

1. `fastapi-implicit-head-options` at attachment line 163.
2. `boa-hierarchical-evaluation-canc` at attachment line 172.
3. `fd-deterministic-multi-key-sorti` at attachment line 181.
4. `fastapi-deprecation-response-hea` at attachment line 190.

The run-level messages only say `agent execution failed`. Retained trial stderr
provides the underlying errors:

1. `fastapi-implicit-head-options__c77d5db5/agent/stderr.log:1` cannot access
   `/app/./.pdm-build`: permission denied.
2. `boa-hierarchical-evaluation-cancellation__92727a5e/agent/stderr.log:1-14`
   cannot read root-owned Rust incremental lock files under
   `/app/./target/debug/incremental`: permission denied.
3. `fd-deterministic-multi-key-sorting__14fdb4c4/agent/stderr.log:1-2` cannot
   read root-owned Rust incremental lock files under
   `/app/./target/debug/incremental`: permission denied.
4. `fastapi-deprecation-response-headers__9798401c/agent/stderr.log:1` cannot
   access `/app/./.pdm-build`: permission denied.

All four errors occur before Copilot starts. `DockerAgentRunner` starts the
container as UID/GID `1000:1000` and then executes `cp -a /app/. /workspace/`
inside that same container (`src/wharf/Agent/AgentExecution.cs:117-145`).
Consequently, source files in the prepared image that are unreadable by UID
1000 cannot be copied.

The workspace is a host bind mount
(`src/wharf/Agent/AgentExecution.cs:118-119`), so files copied as root would
remain root-owned on the host unless ownership is explicitly normalized.
Wharf later runs Git against that host workspace to capture the patch
(`src/wharf/Agent/AgentExecution.cs:176-178`, `260-321`) and expects the agent
container to write there as UID/GID 1000.

## Requirements

- Copy the complete `/app` tree even when image content is unreadable by UID
  1000.
- Leave all copied workspace content owned by UID/GID `1000:1000`.
- Continue running Copilot and task commands as UID/GID `1000:1000`.
- Preserve the existing source/workspace mount separation, credential handling,
  managed-container labeling, evidence capture, and patch capture.
- Fail explicitly if workspace initialization fails; do not start the agent on
  a partial copy.

## Alternatives Considered

### Run the whole agent container as root

Rejected. This violates the established agent execution contract and would
create root-owned runtime output on the host bind mount.

### Relax permissions in each prepared image

Rejected. It mutates image construction semantics, depends on task-specific
paths, and does not reliably cover future images with unreadable content.

### Initialize the bind-mounted workspace in a separate root container

Recommended. Run a short-lived, managed container from the prepared task image
as `0:0`, copy `/app/.` to `/workspace/`, then recursively change ownership of
the workspace to `1000:1000`. Only after successful initialization should the
existing agent container run as `1000:1000`. Remove the duplicate in-agent copy.
This directly addresses both halves of the recommendation: privileged read/copy
followed by ownership normalization.

## Risks

- The initialization container adds one Docker invocation per logical attempt.
- A failed copy may leave partial files in the workspace. Wharf must surface the
  initialization failure and must not launch the agent; retained stderr remains
  the diagnostic source.
- Container naming must avoid colliding with the subsequent agent container
  while retaining the managed label for cleanup.
- Tests need a command-construction seam because the current runner directly
  launches `docker`.

## Not Taken

- No verifier behavior is changed: the observed failures are all in agent image
  materialization before verifier admission.
- No permissions are broadened in prepared images.
- No change is made to the UID/GID contract.
- No retry is added because deterministic permission failures are not
  infrastructure retries.
