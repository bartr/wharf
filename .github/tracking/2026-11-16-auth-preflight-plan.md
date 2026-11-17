# Authentication fail-fast plan

## Research

The reported missing Codex-login failure crosses two existing boundaries:

- `CopilotContracts.cs:109` resolves `latest` before reading and validating
  the credential file at line 195.
- `RunService.cs:119` catches invocation setup errors, records a nullable
  invocation, and continues. Image preparation at line 329 runs before the
  null check at line 351; the normal agent stage is then entered.
- Run, smoke, and resume already have command-level adapter-error reporting.
  Baseline modes bypass invocation preparation; dry-run and prebuild use
  separate paths and must remain credential-free.

## Tasks

1. [x] Add failing regressions for missing/empty/malformed Codex auth before
   version lookup, and shared run/smoke setup failures before output, image
   preparation, or agent execution. Use synthetic files/fakes, not host auth.
2. [x] Read and validate file credentials before registry lookup. Let shared
   invocation setup failures propagate immediately; remove the dependent
   nullable-invocation continuation without changing real execution retries.
3. [x] Document early failure; run targeted regressions, baseline compatibility,
   Release build, formatting, and actual missing-auth run/smoke checks.
   Record results, preserving unrelated setup-script changes.

No live authentication request, token refresh, model call, host installation,
setup-installer change, commit, or push.
