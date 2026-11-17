# Authentication fail-fast changes

Implements the [plan](2026-11-16-auth-preflight-plan.md).

## Root cause and changes

File credentials were read after CLI version resolution. Invocation errors
were then caught inside RunService and converted into a nullable invocation,
allowing image preparation and failed agent-stage records.

- Read/validate file credentials immediately after adapter option validation,
  before resolving `latest`. Reuse the same secret value in the final
  invocation without serializing it into arguments/environment metadata.
- Propagate invocation setup failures from RunService before creating output
  or admitting trials. Existing run/smoke/resume handlers print the error.
- Remove dependent nullable-invocation continuation and failed-stage fallback.
  Real preparation, execution, retry, and evidence-retention paths remain.
- Preserve Codex browser/device login guidance and document early failure.
- Leave prebuild/version-only operations, dry-run, baseline modes, and the
  separately updated setup installer unchanged.

## Red/green evidence

Before the source fix, all **five** new regressions failed:

- Missing, empty, and malformed auth each performed one version lookup rather
  than zero.
- Shared run/smoke setup errors each reached the image-preparer fake.

After the fix, the provider/configuration/run/retry/baseline/smoke/resume/
analytics/execution/build selection passed: **251 passed, 0 failed, 0 skipped**.

The shared execution tests deliberately use invalid invocation options to
trigger setup failure before any host credential access, and assert no output,
image preparation, agent execution, or attached run log. Adapter tests use
synthetic files and a counting version probe.

Tests ran foreground with pane-connected output in named tmux session
`wharf-auth-preflight`. See the [review](2026-11-16-auth-preflight-review.md)
for actual-command and final validation.
