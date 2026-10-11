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

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

Do not commit changes. The evaluator captures the working-tree patch after
implementation.
