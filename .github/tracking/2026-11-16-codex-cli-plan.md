# Codex CLI plan

Based on [research](2026-11-16-codex-cli-research.md).

1. [x] Extend CLI metadata, default model selection, command help, prompt
   shaping, credential-source resolution, and resume identity for Codex.
   Targets: `AgentCli.cs`, `RunConfiguration.cs`, `CommandTree.cs`,
   `RunService.cs`, `RetainedRunResumePlanner.cs`.
   Exit: run and smoke accept Codex; default is `gpt-6-luna`; Copilot/Claude
   behavior and flag/environment/manifest precedence remain unchanged.
2. [x] Add Codex-native invocation and validate the auth.json envelope.
   Materialize credentials only in the agent home at execution with restrictive
   permissions, unset secret transport before Codex, and retain no credentials.
   Targets: `CopilotContracts.cs`, `AgentExecution.cs`.
   Exit: actual Codex 0.169.0 accepts the generated argument shape in a
   no-network test; malformed/missing credentials fail explicitly.
3. [x] Parse Codex JSONL into shared structured-result evidence.
   Targets: new Codex usage parser, `AgentContracts.cs`, `ClaudeUsageParser.cs`,
   `AgentExecution.cs`. Exit: per-turn tokens sum correctly, fatal/incomplete
   results cannot become success, missing dollar and credit costs remain null.
4. [x] Add focused configuration/command/adapter/resume/usage tests, and a
   Docker lifecycle test using the real Codex package, synthetic auth JSON,
   and no model request. Preserve existing Claude/Copilot regression coverage.
5. [x] Update README, analytics docs, and implementation evidence.
   Exit: document ChatGPT auth.json format, run/smoke commands, model default,
   inherited version-pin handling, and credential refresh limitations.
6. [x] Review without source edits: run targeted compatibility tests, Release
   build, formatting, actual CLI help/dry-run, and Docker cleanup checks in a
   named tmux session. Record outcomes and residual qualification needs.

Prebuild support follows the shared selector automatically and stays consistent
with run/smoke preparation. Do not install over the operator's current `wharf`,
read actual credentials, run a paid benchmark, or commit/push without a request.
