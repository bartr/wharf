Implement reproducible run configuration and side-effect-free dry-run support
for the .NET 10 `wharf` CLI.

The repository already contains the `wharf` executable, its test project, and
the Session 1 command, logging, version, and exit-code foundation. Preserve
existing behavior while adding this feature. Do not introduce another
implementation language or another application project.

## Configuration

Add a versioned JSON run manifest and a typed effective-configuration model.
The manifest must support:

- schema version
- `K`: attempts per task
- `N`: task concurrency
- `B`: preparation/build concurrency
- per-phase timeouts
- retry policy
- output format and output directory
- model
- effort
- Copilot CLI version
- mounts
- artifacts
- retention
- resource policy

Use explicit, documented names and types. Reject malformed JSON, missing
required values, unknown keys, unsupported enum values, non-positive counts,
invalid timeout or retry values, unsafe paths, and contradictory settings.
Validation errors must be actionable and must use the existing invalid-input
exit behavior.

The effective value for every setting must follow this precedence, highest to
lowest:

1. command-line flag
2. environment variable
3. manifest value
4. built-in default

Environment names and command-line options must be explicit and deterministic.
Do not use an opaque configuration binder that hides precedence or silently
accepts unknown values.

## CLI behavior

Extend `wharf run` with:

- `--manifest <path>` to load a JSON manifest
- `--dry-run` and `-d`
- flags for the global settings needed to prove precedence

`wharf run --dry-run` must:

1. load the manifest;
2. merge all configuration layers;
3. validate the complete effective configuration;
4. redact secret values;
5. print a deterministic execution-plan shell containing the effective
   non-secret configuration; and
6. exit successfully without creating run state, output directories, Docker
   resources, network connections, model requests, or other persistent
   side effects.

The plan must be stable across repeated invocations with identical inputs. It
must not contain timestamps, random identifiers, absolute host-specific paths,
or secret values. JSON output must be valid deterministic JSON; text output
must use the existing Wharf text-log conventions.

Invalid configuration must fail before creating an output directory or any
other run artifact. A valid non-dry-run `wharf run` may remain an explicit
not-implemented failure in this task; it must not report false success.

## Secret handling

Treat credentials, tokens, passwords, API keys, and secret-bearing environment
values as sensitive. Redact them from:

- effective configuration output;
- dry-run plans;
- text and JSON console output;
- validation errors;
- serialized or persisted configuration;
- test golden files.

Redaction must not mutate the source values used internally for validation.
Do not place real or synthetic secrets in process arguments, logs, fixtures,
or committed files.

## Tests

Add focused automated tests for:

- valid manifest loading;
- malformed manifests;
- schema-version handling;
- flag-over-environment-over-manifest-over-default precedence;
- every supported configuration field;
- invalid counts, timeouts, retries, paths, and policies;
- unknown keys;
- unsupported enum values;
- secret redaction in text output;
- secret redaction in JSON output;
- secret redaction in dry-run output;
- deterministic effective-configuration serialization;
- deterministic repeated dry-run plans;
- validation failure before output-directory creation;
- dry-run without Docker, network, credentials, or persistent state;
- stable invalid-input exit codes;
- preservation of Session 1 behavior for version, help, unknown input,
  logging, and operational placeholders.

Tests must be offline and must use only repository build outputs and temporary
directories. They must not depend on wall-clock time, machine-specific paths,
Docker, Copilot credentials, or external services.

## Validation and completion

Run the existing repository validation workflow, plus the focused test suite.
Prove the compiled CLI boundary for:

- a valid dry-run;
- an invalid manifest;
- an unknown key;
- a secret-bearing configuration;
- identical output from two identical dry-runs; and
- no output-directory creation after validation failure.

Leave the completed implementation and tests as an uncommitted working-tree
patch for evaluation. Do not create a commit or tag. Do not implement Docker
integration, host checks, task discovery, scheduling, durable run state,
agent execution, verifier execution, or reporting.
