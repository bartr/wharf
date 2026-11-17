# Authentication fail-fast review

Reviewed against the [plan](2026-11-16-auth-preflight-plan.md) and
[changes](2026-11-16-auth-preflight-changes.md), without further source edits.

| Check | Execution | Outcome |
| --- | --- | --- |
| Five new regressions before fix | Completed | All five failed for the expected version-lookup/continued-image-preparation behavior |
| Targeted compatibility tests after fix | Completed | 251 passed, 0 failed, 0 skipped |
| `dotnet build wharf.slnx -c Release --no-restore` | Completed | Passed |
| `dotnet format wharf.slnx --verify-no-changes --no-restore` | Completed | Passed |
| Actual run and smoke with missing auth | Completed | Both exited 1 in 0.12/0.14 seconds, printing browser/device login guidance |
| Actual run and smoke with empty auth | Completed | Both exited 1 in 0.12/0.15 seconds |
| Actual run and smoke with malformed auth | Completed | Both exited 1 in 0.11/0.14 seconds without exposing synthetic credential contents |
| `git diff --check` | Completed | Passed |

Build/test/format commands ran foreground in named tmux session
`wharf-auth-preflight`. Actual-command checks used the Release DLL, temporary
HOME/CODEX_HOME/output paths, and `WHARF_CLI_VERSION=latest`. A Docker shim
recorded commands, allowed only read-only info/version/system-df probes, and
blocked every mutation. No image/container operations were attempted, no
agent stage ran, and no jobs directory or run-log pointer was created.
Temporary fixtures were cleaned automatically.

Accepted: missing/invalid local auth aborts before version lookup or trial
side effects. No live credential-validity request was added; an existing
well-formed but expired/revoked credential can still fail during CLI use.

No host credentials were inspected, model requests made, installed binaries
updated, commits created, or changes pushed.
