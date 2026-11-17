# Codex CLI implementation

Implements the [plan](2026-11-16-codex-cli-plan.md) using the existing
Claude/Copilot provider boundary.

## Changes

- Added the `@openai/codex` profile and `gpt-6-luna` default. Copilot remains
  the default CLI; existing CLI/model/version precedence is unchanged.
- Enabled Codex through the shared run, smoke, and prebuild selector, positional
  prompt shaping, versioned image caches, and retained resume identity.
- Read ChatGPT-login auth.json from `~/.wharf/codex`, validate its envelope,
  and materialize it only under the isolated container's
  `/home/wharf/.codex/auth.json`. Directory/file permissions are 0700/0600.
  Remove credential transport and API-key variables before the agent command.
- Generate native `codex exec --json` arguments with explicit ChatGPT/file
  authentication. Map Wharf `max` effort to native `xhigh`.
- Share structured-result handling with Claude. Codex usage sums completed
  turns, rejects failed/incomplete output as success, and retains null cost,
  credits, and request counts when no evidence exists.
- Added provider, resume, analytics, and Docker tests; documented credentials,
  commands, defaults, inherited version pins, and refresh limitations.

## Implementation validation

All long-running validation ran in the named tmux session
`wharf-codex-validation`, with commands in the foreground and output connected
to the pane.

The initial build and test attempt failed at compilation because the new
credential helper was accidentally nested in a validation loop. Moved the
helper to class scope; subsequent compilation and tests passed.

```bash
dotnet test tests/wharf.Tests/wharf.Tests.csproj -c Release --no-restore \
  --filter "FullyQualifiedName~CodexCli|FullyQualifiedName~ClaudeCli|FullyQualifiedName~CopilotAdapterTests|FullyQualifiedName~RunConfigurationTests|FullyQualifiedName~RunServiceTests|FullyQualifiedName~RetainedRunResumePlannerTests|FullyQualifiedName~AnalyticsExporterTests|FullyQualifiedName~AgentExecutionTests|FullyQualifiedName~BuildServiceTests|FullyQualifiedName~SmokeServiceTests" \
  --logger "console;verbosity=minimal"
```

Result: **180 passed, 0 failed, 0 skipped**. This includes two real-package
Docker lifecycle cases using Codex 0.169.0, synthetic credentials, no agent
network, and no model request. The cases exercise successful and failed
turns, native argument parsing, credential permissions and environment
removal, patch/log retention, and absence of credentials from retained
workspace/evidence.

`dotnet format wharf.slnx --verify-no-changes --no-restore` passed.

See the [review](2026-11-16-codex-cli-review.md) for final build, command, and
cleanup checks. No real credentials were inspected, host installation changed,
model requests made, or commits/pushes performed.
