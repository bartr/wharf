# Claude Code CLI changes

## Implementation

- Added `cli` configuration with `--cli` > `WHARF_CLI` > manifest > `copilot`
  precedence. Dry-run renders the selector without credential or network access.
- Added `--cli` to run, prebuild, and smoke. Default models remain `hydrafusion`
  for Copilot and become `claude-sonnet-5-5` for Claude per the user's follow-up;
  explicit models override either.
- Shared package/install metadata and the existing adapter transport rather than
  renaming public Copilot records. Claude installs the exact
  `@anthropic-ai/claude-code` version into an independent cache.
- Claude reads only `~/.wharf/claude`, forwarding the OAuth credential as
  `CLAUDE_CODE_OAUTH_TOKEN` through the Docker execution environment. Neither
  image recipes nor persisted configuration receives its value.
- Selected native Claude headless, stream-json, permission, and effort flags;
  positional prompts are protected by `--`. Disabled Claude auto-updates.
- Preserved preparation gating, isolated child images, UID 1000, patch capture,
  evidence retention, and verifier boundaries. Copilot service retries are
  restricted to Copilot invocations.
- Added CLI-specific run names and resume identity checks. Old metadata without
  `cli` still restores Copilot. Newly retained plans pin the resolved version
  rather than retaining `latest`.
- Claude result parsing retains cumulative tokens, turns, per-model usage, and
  reported dollar cost. Invalid result evidence explicitly fails while keeping
  logs and patches. Analytics sums dollar cost across retries without credits.
- Updated README and analytics documentation. Documented that a version pinned
  in `WHARF_CLI_VERSION` applies to the selected CLI, so switching providers may
  require `--cli-version latest`.

## Validation and corrections

All long-running validation used the named `wharf-claude-validation` tmux
session with foreground commands and connected stdout/stderr.

- Initial build found a cache-evidence identifier collision; corrected.
- Initial focused suite: 142 passed, one Docker test failed because task loading
  expects the dataset parent, not an individual task directory. The sanitized
  fixture then correctly rejected Docker preparation because it lacks app
  source. Switched the test to the existing runnable WharfSWE .NET smoke task;
  no sanitized fixture or pinned task payload was modified.
- Corrected Docker integration: passed with real Claude Code 2.1.241 installed,
  a synthetic credential, no network in the agent container, and no model call.
  Verified package version, UID-1000 execution, credential forwarding, agent
  home, patch capture, raw output, cost evidence, and child-image cleanup.
- Expanded compatibility suite: **171 passed**, including configuration,
  adapters, agent execution, resume, analytics, build service, smoke service,
  image-recipe compatibility, and CLI command tests. Excluded the existing
  command test that starts a real paid benchmark and unrelated full-suite work.
- After tightening malformed-result handling, the final provider-specific
  suite passed **24/24**, including the Docker integration and two new edge
  cases for missing error status and negative turn counts.
- Final `dotnet build wharf.slnx -c Release --no-restore`: passed.
- Final `dotnet format wharf.slnx --verify-no-changes --no-restore`: passed.
- `git diff --check`: passed.
- Actual CLI dry-run and prebuild help expose Claude selection correctly.
- No temporary Docker test containers, child-image tags, or scratch directories
  remain. Reusable image caches are intentionally retained.

No real credentials were inspected and no authenticated model run, benchmark
export, release, commit, tag, or push was performed.

## Follow-up: Sonnet 5.5 default

Changed Claude's default to the exact `claude-sonnet-5-5` model ID while
preserving explicit overrides and Copilot defaults. Updated README and default
assertions. In `wharf-claude-default-validation` tmux, all 64 targeted Claude
and configuration tests passed, including a Release compilation; formatting
verification and `git diff --check` passed. Actual CLI dry-run rendered
`"model":"claude-sonnet-5-5"`.
