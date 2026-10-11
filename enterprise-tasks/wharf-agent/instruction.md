The Wharf 0.8.0 release has Copilot preparation but no isolated agent
execution boundary. Add the agent execution slice from Wharf 0.9.0.

Implement agent execution so that it:

- validates distinct absolute source and workspace mounts;
- rejects hidden verifier paths inside either mount;
- runs the prepared Copilot agent with the configured network;
- retains stdout, stderr, trajectory, patch, timing, usage, and result evidence;
- classifies empty patches as unfinished rather than success;
- preserves evidence before reporting execution failures.

Preserve the historical public contract in the `Wharf.Agent` namespace.
Expose `AgentExecutionService`, `AgentExecutionRequest`, `IAgentRunner`,
`AgentRunResult`, `AgentEvidenceSchema`, `AgentEvidenceSummary`, and
`AgentExecutionException`. `AgentExecutionRequest` must expose public
`WorkspaceDirectory` and `HiddenPaths` properties.

Use these historical public shapes:

- `AgentExecutionRequest(string TaskId, string AgentImage,
  string SourceDirectory, string WorkspaceDirectory, string Network,
  CopilotInvocation Invocation, IReadOnlyList<string> HiddenPaths)` with a
  public `Validate()` method;
- `AgentRunResult(int ExitCode, string StandardOutput, string StandardError,
  string Trajectory, string Patch, TimeSpan Duration, int? Tokens = null,
  int? Steps = null, int? Requests = null, decimal? Cost = null)`;
- `IAgentRunner.RunAsync(AgentExecutionRequest, CancellationToken)`;
- `AgentExecutionService(IAgentRunner)` and
  `ExecuteAsync(AgentExecutionRequest, string evidenceDirectory,
  CancellationToken cancellationToken = default)`; and
- `AgentEvidenceSummary(string SchemaVersion, string TaskId, string Status,
  int ExitCode, long DurationMilliseconds, int? Tokens, int? Steps,
  int? Requests, decimal? Cost, string PatchPath,
  string StandardOutputPath, string StandardErrorPath,
  string TrajectoryPath)`; and
- `AgentEvidenceSchema.Version`, equal to `"1.0"`.

`ExecuteAsync` must write `stdout.log`, `stderr.log`, `trajectory.jsonl`,
`patch.diff`, and `result.json` under the supplied evidence directory before
reporting an unfinished or failed result.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

Do not commit changes. The evaluator captures the working-tree patch after
implementation.
