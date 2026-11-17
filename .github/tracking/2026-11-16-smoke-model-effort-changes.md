# Smoke model and effort changes

Implements the [plan](2026-11-16-smoke-model-effort-plan.md).

- Shared model/effort option factories keep run and smoke names, descriptions,
  and `-m`/`-e` aliases consistent.
- Smoke forwards explicit model and effort through its service to the existing
  run configuration resolver. Flags override environment settings; omitted
  flags retain environment values and provider defaults.
- Extracted smoke configuration resolution for focused, environment-isolated
  tests. Preserved absolute output paths and K/N/P=1.
- Shared the existing effort validator between run and smoke without imposing
  run's relative-output requirement on smoke.
- Documented flags, precedence, aliases, and a Codex smoke example.

## Validation

The provider/configuration/execution/resume/analytics/build/smoke regression
selection passed: **197 passed, 0 failed, 0 skipped**. Twelve new test cases
cover long/short option parsing on both commands, overrides for all providers,
environment/default preservation, absolute smoke output, and invalid effort.

Tests ran foreground with pane-connected output in named tmux session
`wharf-smoke-options`. New tests do not invoke models or access credential
files. See the [review](2026-11-16-smoke-model-effort-review.md) for final
build, formatting, and actual-command checks.
