# Codex native authentication changes

Implements the [approved plan](2026-11-16-codex-native-auth-plan.md),
superseding the earlier Codex-specific `~/.wharf/codex` source-path decision.

- Invocation preparation passes the host `CODEX_HOME` into credential
  resolution. An unset, empty, or whitespace-only value uses
  `~/.codex/auth.json`; otherwise the source is `<CODEX_HOME>/auth.json`.
- No legacy Wharf credential fallback or automatic login is performed.
- Missing Codex-file errors show the resolved path, a file-storage ChatGPT
  browser login command, the `--device-auth` alternative, and guidance to
  preserve the same `CODEX_HOME` when signing in.
- Claude/Copilot credential resolution and missing-file diagnostics are
  unchanged. Container `CODEX_HOME` remains `/home/wharf/.codex`; host
  credentials are neither mounted writable nor overwritten.
- README now documents native login/storage rather than copying auth.json.

## Implementation verification

Ran the same provider/configuration/execution/resume/analytics/smoke filter
recorded in the [Codex implementation](2026-11-16-codex-cli-changes.md):
**185 passed, 0 failed, 0 skipped**.

New coverage includes reading synthetic auth.json from a custom home containing
spaces while preserving private container paths, empty home settings,
Codex-only missing-file login guidance, and unchanged other-provider paths and
diagnostics.

Tests ran in the named tmux session `wharf-codex-native-auth`, foreground with
pane-connected output. No actual credentials were inspected or modified.
See the [review](2026-11-16-codex-native-auth-review.md) for final checks.
