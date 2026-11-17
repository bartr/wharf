# WharfSWE duration estimate review

Reviewed against the [plan](2026-11-17-wharfswe-duration-plan.md) and
[changes](2026-11-17-wharfswe-duration-changes.md), without further code changes.

| Validation | Execution | Outcome |
| --- | --- | --- |
| `dotnet test tests/wharf.Tests/wharf.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~HarborTaskTests" --logger "console;verbosity=minimal"` | Completed | 9 passed, 0 failed, 0 skipped |
| `dotnet format wharf.slnx --verify-no-changes --no-restore` | Completed | Passed |
| Independent Decimal median/sum recalculation | Completed after rounding correction | All 14 entries match 168 unique source trials, 12 observations/task |
| Current task checksum recalculation | Completed | All match the source export rows |
| `git diff --check` | Completed | Passed |

Build/test/format ran foreground with pane-connected output in named tmux
`wharf-swe-durations`. The targeted test compiles Release and uses the actual
WharfSWE task loader and admission scheduler.

Accepted: new jobs automatically admit longest combined-duration tasks first
within each K wave. First is `wharf-verifier` (326.196 seconds), then
`wharf-image-pull` (275.905 seconds); last is `wharf-reporting` (154.428 seconds).
This is admission order, not a guarantee of execution/completion order under
concurrency. Existing resumed runs retain their durable policy.

No pinned payloads, raw runs, exports, analytics manifests, or DeepSWE files
were modified. The user's untracked `wharf-swe/run.sh` remains untouched.
No benchmark/model calls, commits, or pushes were performed.
