# Claude Code CLI review

Reviewed against [research](2026-11-16-claude-cli-research.md) and
[plan](2026-11-16-claude-cli-plan.md), using the implementation and validation
evidence in [changes](2026-11-16-claude-cli-changes.md).

## Acceptance

| Requirement | Evidence |
| --- | --- |
| Copilot remains default; `--cli claude` selects Claude | Configuration precedence and command tests; actual dry-run |
| Claude uses `~/.wharf/claude` OAuth token | Credential resolution and file validation tests |
| No credentials in arguments, images, or retained configuration | Adapter assertions; synthetic Docker test; existing redaction/resume tests |
| Correct CLI installation and native invocation | Independent package/cache tests; real Claude 2.1.241 version check inside the container |
| Existing execution/verifier behavior preserved | Agent/build/resume regressions; unchanged verifier boundary |
| Resume restores CLI and exact version | Claude identity mismatch test; legacy metadata test; resolved-version retention |
| Correct cost units and missing evidence handling | Parser and retry-analytics tests; dollar cost separate from null credits |
| Invalid output cannot become a successful trial | Invalid JSON, missing status, negative count/cost cases and explicit runner failure path |
| Build and formatting | Final Release build and formatting verification passed |

Execution is complete and local qualification is accepted: the 171-test
compatibility suite passed, followed by 24/24 provider-specific tests after the
last parser hardening. The latter includes a real Docker lifecycle with a
synthetic credential and no model request.

## Follow-up destination

Optional operational qualification: run one paid Claude smoke task using the
operator's actual subscription token before a controlled benchmark or release.
No authenticated model call was made in this task, and synthetic cost evidence
is test-only, not an exported benchmark result.

No blocking defects or scope decisions remain. No source changes were made
during review.
