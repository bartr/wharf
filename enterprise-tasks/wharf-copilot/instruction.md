The Wharf 0.7.0 release has image preparation but no Copilot adapter boundary.
Add the Copilot adapter and secret boundary from Wharf 0.8.0.

Implement the Copilot adapter so that it:

- requires exact requested/effective three-part CLI version equality;
- normalizes native and frontier effort values;
- emits model, effort, subagent, and endpoint arguments;
- allows only explicitly allowlisted HTTPS endpoints;
- supports environment, file, and stdin credential sources;
- retains credential source metadata without exposing credential values.

Preserve the historical public contract in the `Wharf.Copilot` namespace.
Expose `CopilotAdapter`, `CopilotInvocation`, `ICopilotCliProbe`, and
`CopilotAdapterOptions`. Also expose `CopilotCredentialSourceKind` with exactly
three enum values, `CopilotCredentialSource` with a public `Validate` method,
and `CopilotAdapterException`. `CopilotInvocation` must expose public
`EffectiveVersion`, `RedactedCommand`, and `SubagentModels` properties.

Use these historical public shapes:

- `CopilotCredentialSource(CopilotCredentialSourceKind Kind, string Reference)`;
- `CopilotAdapterOptions(string RequestedVersion, string Model,
  string NativeEffort, string FrontierEffort,
  IReadOnlyList<string> SubagentModels, CopilotCredentialSource Credential,
  IReadOnlyList<string> Endpoints,
  IReadOnlySet<string> AllowedEndpointHosts)`;
- `CopilotCliVersion(string Value)`;
- `ICopilotCliProbe.GetVersionAsync(CancellationToken)` returning
  `Task<CopilotCliVersion>`;
- `CopilotAdapter(ICopilotCliProbe probe, string executable = "copilot")`; and
- `CopilotAdapter.PrepareAsync(CopilotAdapterOptions,
  CancellationToken cancellationToken = default)`.

`CopilotInvocation` must also expose `Arguments`, `Environment`,
`ReadsCredentialFromStdin`, `NativeEffort`, `FrontierEffort`, and `Endpoints`.
Normalize a leading `v` from CLI versions and map frontier effort `xhigh` to
`high`.

Build the invocation arguments with `--model` followed by the model, one
`--subagent-model` and value pair for every subagent model, and one
`--endpoint` and value pair for every endpoint.

For an environment credential source, add only source metadata to the
invocation environment: set `WHARF_COPILOT_CREDENTIAL_SOURCE` to
`environment` and `WHARF_COPILOT_CREDENTIAL_VARIABLE` to the credential
reference. Never resolve or include the credential value.

`CopilotCredentialSource.Validate()` must require environment references to
match `^[A-Za-z_][A-Za-z0-9_]*$` and throw `ArgumentException` for malformed
names such as `BAD-NAME`.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

Do not commit changes. The evaluator captures the working-tree patch after
implementation.
