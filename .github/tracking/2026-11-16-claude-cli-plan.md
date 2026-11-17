# Claude Code CLI implementation plan

Based on [research](2026-11-16-claude-cli-research.md).

1. [x] Add validated CLI selection to configuration, manifest, rendering, run,
   prebuild, and smoke commands. Preserve flag/environment/manifest/default
   precedence and Copilot defaults. Exit: dry-run selects Claude without
   credentials, directories, Docker, or network activity; invalid CLI fails.
2. [x] Share package/install metadata and adapt credentials, invocation flags,
   prompt arguments, image caches, and workspace version validation.
   Targets: `CopilotContracts.cs`, `DockerAgentImagePreparer.cs`,
   `AgentExecution.cs`, `RunService.cs`, `BuildService.cs`.
   Exit: exact versions, isolated images, unchanged preparation gates, no
   credentials in persisted configuration or command arguments.
3. [x] Restore retained CLI on resume, including legacy Copilot metadata.
   Target: `RetainedRunResumePlanner.cs`. Exit: arm/metadata mismatch rejected.
4. [x] Parse Claude stream-json usage and dollar costs without inventing
   Copilot credits. Targets: agent evidence and `RunEvidenceReader.cs`.
   Exit: result and retry evidence retains tokens, turns, costs, and raw output;
   missing cost stays null.
5. [x] Add focused configuration, adapter, cache, execution, resume, and
   analytics regression tests. Update README and analytics documentation.
   Exit: tests exercise provider differences and legacy defaults.
6. [x] Validate in a named tmux session: focused tests, Release build,
   formatting, and a Docker-backed synthetic Claude CLI lifecycle with no
   external model calls. Record actual outcomes in changes/review artifacts.

## Parking lot

Authenticated paid benchmark qualification and any release/tag/push are outside
this request. Existing pinned task payloads and retained benchmarks stay intact.
