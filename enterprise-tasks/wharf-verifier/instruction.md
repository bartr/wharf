The Wharf 0.9.0 release has agent execution but no verifier boundary. Add the
pristine verifier execution vertical slice from the next release.

Use the historical public contract below. These names and signatures are part
of the task contract; do not replace them with alternate names such as
`VerifierExecutionRequest`.

Required types in the `Wharf.Verifier` namespace:

- `VerifierCommand(string Name, string Command, TimeSpan Timeout)`
- `VerifierRequest(string TaskId, string SourceDirectory,
  string AgentWorkspaceDirectory, string Network,
  IReadOnlyList<VerifierCommand> Commands,
  IReadOnlyList<string> HiddenPaths,
  IReadOnlyList<string> CredentialEnvironmentVariables)`
- `VerifierCommandResult` with name, exit code, stdout, stderr, duration in
  milliseconds, and timeout status, using
  `VerifierCommandResult(string Name, int ExitCode, string StandardOutput,
  string StandardError, long DurationMilliseconds, bool TimedOut)`
- `TrialResult(string SchemaVersion, string TaskId, string Status,
  string WorkspaceDirectory, IReadOnlyList<VerifierCommandResult> Commands,
  string PatchApplied)`
- `VerifierExecutionException`
- `TrialResultSchema.Version`, equal to `"1.0"`
- `IPristineWorkspaceFactory`, `IPatchApplier`, and `IVerifierRunner`
- `PristineVerifierService.VerifyAsync(...)`. At least one public overload must
  accept the verifier request, retained patch, evidence directory, and
  cancellation token. Additional convenience overloads are allowed.

Use these exact boundary methods:

- `IPristineWorkspaceFactory.CreateAsync(string sourceDirectory,
  CancellationToken)`;
- `IPatchApplier.ApplyAsync(string workspaceDirectory, string patch,
  CancellationToken)`;
- `IVerifierRunner.RunAsync(VerifierRequest, string workspaceDirectory,
  VerifierCommand, CancellationToken)`;
- `PristineVerifierService(IPristineWorkspaceFactory, IPatchApplier,
  IVerifierRunner)`; and
- `VerifyAsync(VerifierRequest, string patch, string evidenceDirectory,
  CancellationToken cancellationToken = default)`.

`VerifierRequest.Validate()` must reject malformed credential environment
variable names as well as invalid paths, commands, and workspace reuse.

Implement the verifier boundary so that it:

- validates task, source, agent workspace, network, command, and hidden-path
  inputs;
- copies the task source into a fresh workspace before applying the retained
  agent patch;
- runs verifier commands sequentially with explicit timeouts;
- retains each command's stdout, stderr, exit code, duration, and timeout
  status;
- stops after the first failed command;
- atomically persists a versioned `trial-result.json`;
- rejects empty patches before creating trial evidence.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
