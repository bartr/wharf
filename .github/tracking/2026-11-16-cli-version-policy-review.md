# CLI version policy review

Reviewed against the [plan](2026-11-16-cli-version-policy-plan.md) and
[changes](2026-11-16-cli-version-policy-changes.md), without further source edits.

| Validation | Execution | Outcome |
| --- | --- | --- |
| Targeted compatibility suite | Completed | 270 passed, 0 failed, 0 skipped |
| `dotnet build wharf.slnx -c Release --no-restore` | Completed | Passed |
| `dotnet format wharf.slnx --verify-no-changes --no-restore` | Completed | Passed |
| Actual run/dry-run/smoke/prebuild/resume with each obsolete variable | Completed | Ten cases exited 2 with migration guidance before Docker or output |
| Actual dry-run version precedence for all three providers | Completed | Nine cases confirmed flag > manifest > latest |
| Actual smoke help | Completed | Advertises `--cli-version` |
| `git diff --check` | Completed | Passed |

Build/test/format ran in named tmux `wharf-cli-version-policy`, foreground with
pane-connected output. Actual-command checks used the Release DLL, temporary
HOME/working directories, and a Docker shim that rejects and records every
invocation. No Docker invocation occurred during the obsolete-setting checks.
Dry-run performed no credential reads, registry access, or output creation.
Temporary fixtures were cleaned automatically.

Accepted: explicit selected-provider pins are never silently ignored;
manifests retain reproducibility; provider latest versions feed exact run
identities; smoke supports the same explicit version control. Existing
immutable resume configuration remains authoritative after obsolete settings
are removed.

Operators must remove obsolete exports from shell/profile configuration and
unset them before using an updated build. Host-installed CLI versions do not
select container CLI versions. No credentials were inspected, live model calls
made, installations changed, retained run evidence rewritten, commits created,
or changes pushed.
