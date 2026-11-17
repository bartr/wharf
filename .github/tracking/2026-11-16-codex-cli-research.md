# Codex CLI research

## Requirements and reused evidence

Support `--cli codex` for run and smoke, with default model `gpt-6-luna`;
Copilot remains the default CLI. The user confirmed that `~/.wharf/codex`
will contain a ChatGPT-login `auth.json`, not a plain-text API key.
Credential material was not inspected.

Reuse the completed Claude CLI architecture and qualification rather than
re-researching the shared pipeline:

- CLI package/install metadata: `src/wharf/Agent/AgentCli.cs:5`.
- Configuration precedence and per-CLI defaults:
  `src/wharf/Configuration/RunConfiguration.cs:190`.
- CLI selection already flows through run, smoke, and prebuild:
  `src/wharf/Command/CommandTree.cs:139,252,920`.
- Shared exact-version installation, independent CLI caches, and workspace
  checks already parameterize CLI metadata. Codex needs a new package profile,
  not a second image pipeline.
- Invocation, credential reading, and native flags:
  `src/wharf/Copilot/CopilotContracts.cs:107`.
- Execution transports secret values only via the Docker client's environment;
  command arguments contain variable names, not values:
  `src/wharf/Agent/AgentExecution.cs:423`.
- Resume validates CLI-specific arm identity:
  `src/wharf/Recovery/RetainedRunResumePlanner.cs:32`.
- Raw stream evidence and per-provider usage parsing:
  `src/wharf/Agent/AgentExecution.cs:323`.

## Codex-specific evidence

No host Codex CLI is installed. Public npm metadata identifies
`@openai/codex` 0.169.0 and platform-specific optional binary dependencies.
Keep the normal npm install, including optional dependencies.

Official documentation:

- <https://developers.openai.com/codex/noninteractive>: `codex exec --json`
  emits JSONL; prompts are positional; `turn.completed` carries usage.
- <https://developers.openai.com/codex/auth>: ChatGPT credentials are read from
  `$CODEX_HOME/auth.json`; `cli_auth_credentials_store="file"` selects file
  storage. `forced_login_method="chatgpt"` restricts login to that method.
- <https://developers.openai.com/codex/cli/reference>: configuration overrides
  use `--config key=value`; bypassing nested sandbox/approval checks is intended
  only inside an externally isolated container.
- Pinned 0.169.0 sources:
  <https://github.com/openai/codex/blob/rust-v0.169.0/codex-rs/exec/src/cli.rs>
  confirms `exec`, `--json`, and positional prompts.
  <https://github.com/openai/codex/blob/rust-v0.169.0/codex-rs/exec/src/exec_events.rs>
  defines `turn.completed`, `turn.failed`, fatal `error`, and per-turn input,
  cached input, cache-write input, output, and reasoning token fields. These
  events provide no cost evidence, so cost and credits must stay null.
  <https://github.com/openai/codex/blob/main/codex-rs/login/src/auth/storage.rs>
  documents the saved credential envelope (`tokens`, `auth_mode`,
  `OPENAI_API_KEY`).

## Recommendation and risks

Extend the existing profile and adapter with Codex-native flags and exact
versions. Transport the auth JSON through a private environment variable,
materialize it only inside the executing container under
`/home/wharf/.codex/auth.json` with directory mode 0700 and file mode 0600,
then unset the transport variable before invoking Codex. Keep credentials
outside `/workspace`, image layers, patches, and evidence. Explicitly select
ChatGPT login rather than accidentally falling back to an API key.

Use positional prompts after `--`. Explicit Wharf `max` effort maps to Codex
`xhigh`; `default` omits the effort override. Parse completed-turn usage without
inventing requests, dollars, or Copilot credits. Reuse shared structured-result
handling rather than duplicating the runner.

Codex may refresh its isolated credential copy; Wharf must not overwrite the
operator's source credential file or retain refreshed credentials as evidence.
Operators must maintain valid source credentials for future runs. Concurrent
copies and expiring subscription tokens warrant live qualification before a
long benchmark.

Out of scope: API-key support, host installation, real model requests,
benchmark exports, release/tag/push, and unrelated provider refactoring.
RPI follows Microsoft HVE Core; the environment provides no in-place context
reset, so retain explicit phase artifacts.
