# Codex native authentication plan

## Reused research and approved decision

Reuse the [Codex research](2026-11-16-codex-cli-research.md) and the official
[authentication documentation](https://developers.openai.com/codex/auth).
The user approved replacing the extra Wharf credential copy with Codex's
native auth.json, honoring `CODEX_HOME`, and printing browser/headless login
instructions when the file is absent.

Current boundaries: `RunService.cs:1195` prepares invocations and resolves
provider credential paths; `CopilotContracts.cs:195` reads credential files
and reports missing files. Existing container execution keeps credentials
under its own `/home/wharf/.codex`, outside the workspace.

## Implementation tasks

1. [x] Resolve the host Codex source from `$CODEX_HOME/auth.json`, falling back
   to `~/.codex/auth.json`. No fallback to `~/.wharf/codex`; preserve Claude
   and Copilot behavior and private container copies.
2. [x] Add file-mode ChatGPT browser and device-auth instructions to missing
   Codex-file errors, without launching login or reading actual credentials.
   Cover default/custom paths, missing-file diagnostics, and unchanged other
   provider diagnostics with focused tests.
3. [x] Update README, run provider/execution/smoke regressions, Release build,
   formatting, and whitespace checks. Record implementation/review evidence.

No host installation changes, automatic authentication, source credential
writes, live model calls, shared writable credential mounts, or commits/pushes.
