# Smoke model and effort review

Reviewed against the [plan](2026-11-16-smoke-model-effort-plan.md) and
[changes](2026-11-16-smoke-model-effort-changes.md), without source edits.

| Check | Execution | Outcome |
| --- | --- | --- |
| Focused compatibility and new smoke tests | Completed | 197 passed, 0 failed, 0 skipped |
| `dotnet build wharf.slnx -c Release --no-restore` | Completed | Passed |
| `dotnet format wharf.slnx --verify-no-changes --no-restore` | Completed | Passed |
| Current-build `smoke --help` | Completed | Advertises model and effort options |
| Current-build smoke with explicit Codex/model and invalid effort | Completed | Exit 2 with unsupported-effort diagnostic; no output directory created |
| `git diff --check` | Completed | Passed |
| Test-owned Docker container/child-image checks | Completed | None remaining |

Build/test/format commands ran foreground in named tmux session
`wharf-smoke-options`. Actual-command validation used the Release DLL from
`wharf-swe`, temporary output paths, and no model calls. Temporary validation
files were cleaned.

Accepted: both smoke flags and aliases, standard precedence/provider
normalization, actionable invalid-input errors, and preserved smoke output
and scheduling behavior. No remaining defects found in these checks.

Installed tools were not updated; changes are uncommitted and unpushed.
