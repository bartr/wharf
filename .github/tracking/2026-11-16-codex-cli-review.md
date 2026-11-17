# Codex CLI review

Reviewed against the [research](2026-11-16-codex-cli-research.md),
[plan](2026-11-16-codex-cli-plan.md), and
[implementation evidence](2026-11-16-codex-cli-changes.md).
Review made no source changes.

## Execution and outcome

| Check | Execution | Outcome |
| --- | --- | --- |
| Targeted provider/configuration/execution/resume/analytics/smoke tests | Completed | 180 passed, 0 failed, 0 skipped |
| Real Codex 0.169.0 Docker lifecycle checks | Completed within targeted suite | Both successful-turn and failed-turn cases passed without network/model calls |
| `dotnet build wharf.slnx -c Release --no-restore` | Completed in named tmux session | Passed |
| `dotnet format wharf.slnx --verify-no-changes --no-restore` | Completed in named tmux session | Passed |
| Current-build `run --cli codex --cli-version latest --dry-run --output-format json` | Completed | Rendered `cli: codex`, `model: gpt-6-luna`, and `cliVersion: latest` |
| Current-build `smoke --help` | Completed | Advertises `--cli` with Copilot as default and Claude/Codex alternatives |
| `git diff --check` | Completed | Passed |
| Test-owned container/image and `/tmp/wharf-codex-*` checks | Completed | No remaining test containers, child-image tags, or scratch directories |

The current-build commands used
`dotnet src/wharf/bin/Release/net10.0/wharf.dll`, not the operator's older
installed binary. The existing `WHARF_CLI_VERSION` remains untouched;
`--cli-version latest` explicitly overrides that Copilot-specific pin.
Reusable provider image caches remain available.

## Acceptance

- Run and smoke accept Codex; Copilot remains the default.
- Codex defaults to `gpt-6-luna`, with existing override precedence preserved.
- Credential source is the user-confirmed ChatGPT auth.json file; secret
  contents are not serialized into configuration, commands, or evidence.
- Raw trajectory, patch, and execution evidence remain retained. Structured
  failures cannot become success; missing billing evidence remains null.
- Existing Claude and Copilot compatibility tests pass.

Implementation qualification is accepted. No code defects or plan gaps remain
from the performed checks.

## Residual qualification

A new operational task should qualify a real authenticated Codex invocation
and long/concurrent ChatGPT credential-refresh behavior before a large
benchmark. No paid/live model request was performed here. Wharf intentionally
does not copy refreshed private credentials back to the operator's source
file; operators must keep that source valid.

The installed `wharf` remains unchanged, and the implementation is not
committed or pushed.
