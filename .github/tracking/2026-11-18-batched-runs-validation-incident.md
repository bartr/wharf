# Validation incident: legacy command test invokes a real agent

The initial 519-test regression run inherited obsolete `WHARF_CLI_VERSION`.
It completed with five failures: one pre-existing environment-dependent command
test and four directly related progress regressions. The progress defects were
returned to implementation.

A targeted rerun with the obsolete environment variables removed included
`WharfAppTests.RunCommandExecutesTaskDiscovery`. Despite its discovery-oriented
name, this existing test invokes a real model when local credentials exist.
It launched one Copilot HydraFusion smoke agent before this was identified.

Known execution facts:

* Source: commit `7d4494f` plus uncommitted batching implementation.
* K=1, N=4, P=4; Copilot CLI 1.0.114; model `hydrafusion`, effort `default`.
* Test output: `tests/wharf.Tests/bin/Debug/net10.0/run-test/`.
* Container: `agent-smoke-go__f2ab6b8f`.
* Agent ran approximately 60 seconds before its exact container was stopped.
  Agent stage elapsed approximately 62 seconds; final usage/cost was unknown.
* The retained partial patch passed verification. The enclosing existing test
  failed its patch-discovery assertion, then its own `finally` removed the
  temporary output. This cleanup had completed before an audit could be added;
  no missing telemetry is reconstructed or imputed.
* The agent and verifier images were removed by the tested lifecycle. No Opus
  or Sol recovery-fixture execution was started.

The final offline validation excludes this one live-integration test by its
fully-qualified name. No further model executions are permitted for this task.
The test's live-service design is a pre-existing follow-up, not a batching change.
