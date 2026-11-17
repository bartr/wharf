# Claude Code CLI research

## Requirements and evidence

- Keep Copilot as the default; support `--cli claude`. The user confirmed
  `~/.wharf/claude` contains a Claude Code setup-token OAuth credential.
- Configuration currently resolves flags over environment over manifest over
  defaults (`src/wharf/Configuration/RunConfiguration.cs:152`), but has no CLI
  selector. Dry-run rendering is secret-free and side-effect-free.
- The adapter already separates credential source metadata from the in-memory
  credential value (`src/wharf/Copilot/CopilotContracts.cs:47`). Docker forwards
  credentials by environment variable name, never argument value
  (`src/wharf/Agent/AgentExecution.cs:397`).
- CLI packages and workspace version checks are hardcoded to Copilot
  (`src/wharf/Image/DockerAgentImagePreparer.cs:331`,
  `src/wharf/Agent/AgentExecution.cs:806`). Preparation and runtime must select
  the same CLI and exact version.
- Run names and resume validation assume Copilot
  (`src/wharf/Run/RunService.cs:1173`,
  `src/wharf/Recovery/RetainedRunResumePlanner.cs:32`). Retained configuration
  must include the CLI, defaulting missing legacy fields to Copilot.
- Analytics converts Copilot credits to dollars
  (`src/wharf/Reporting/RunEvidenceReader.cs:50`). Claude dollar costs must not
  be interpreted as credits.
- Installed Claude Code 2.1.241 `--help` confirms `--print`, `--model`,
  `--effort`, `--output-format stream-json`, `--verbose`, and
  `--dangerously-skip-permissions`. Unlike Copilot it accepts the prompt as a
  positional argument, not `--prompt`.
- Official reference: <https://code.claude.com/docs/en/headless> documents
  print mode and structured output. OAuth cannot use `--bare`.
  <https://code.claude.com/docs/en/env-vars> documents authentication and
  auto-update controls. Use `CLAUDE_CODE_OAUTH_TOKEN` for setup tokens.
  <https://code.claude.com/docs/en/github-actions#manual-setup> explicitly
  documents generating subscription OAuth tokens with `claude setup-token`
  and using `CLAUDE_CODE_OAUTH_TOKEN`.

## Recommendation

Share CLI metadata and existing adapter/execution transport, retaining existing
public Copilot type names for compatibility. Select package, install path,
credential variable, invocation flags, prompt shape, cache identity, and result
parser by a validated `copilot`/`claude` selector. Default Claude's model to
`claude-sonnet-5-5` per the user's follow-up (replacing the initial `opus`
recommendation); preserve explicit model and effort choices. The model ID is
documented at <https://platform.claude.com/docs/en/models/sonnet-5-5/overview>.
Extend run, prebuild, and
smoke; resume restores the retained CLI.

## Risks and exclusions

- Never read or print real credentials during investigation or tests.
- Preserve Copilot flags, caches, retries, cost units, and legacy resume.
- Do not add providers beyond these two, API-key autodetection, host login
  copying, model pricing estimates, release changes, or benchmark payload edits.
- Validation must cover container execution without external model calls.
  An authenticated paid benchmark is not required for this change.
- RPI is the Microsoft HVE Core workflow. Phase artifacts are retained here;
  this execution environment does not provide an in-place context reset.
