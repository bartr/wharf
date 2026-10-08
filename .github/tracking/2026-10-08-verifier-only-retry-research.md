# Verifier-only retry research

## Requirement

When a verifier times out or is externally terminated, retry verification
against the exact retained agent patch in a new pristine verifier workspace.
Do not rerun the agent. Agent OOMs, agent timeouts, image failures, and other
retryable agent-side failures continue to use the existing whole-trial retry.

## Findings

1. Whole-trial retry currently owns every retryable phase failure.
   `RunService` declares `totalCost` outside an outer `trialRetry` loop, archives
   the complete trial before each retry, and recreates the agent workspace,
   agent execution, verifier, and cleanup
   (`src/wharf/Run/RunService.cs:143-155`,
   `src/wharf/Run/RunService.cs:634-654`).

2. The retry predicate currently accepts all typed failures except invocation
   failures. Consequently `VerifierTimeout` and `VerifierInfrastructure` enter
   the same full-trial loop as `OutOfMemory` and `AgentTimeout`
   (`src/wharf/Run/RunService.cs:665-675`,
   `tests/wharf.Tests/RunServiceRetryTests.cs:6-16`).

3. The verifier already has the required phase boundary. `PrepareAsync`
   validates the request, creates a pristine workspace, and applies the
   retained patch. `VerifyPreparedAsync` runs commands and writes raw command,
   patch, metric, and atomic trial evidence
   (`src/wharf/Verifier/VerifierExecution.cs:31-66`,
   `src/wharf/Verifier/VerifierExecution.cs:68-128`).

4. Verifier timeout and external termination are raised only after evidence is
   persisted. Timeouts become `VerifierTimeout`; exits 137/143 become
   `VerifierInfrastructure`
   (`src/wharf/Verifier/VerifierExecution.cs:96-126`,
   `tests/wharf.Tests/VerifierExecutionTests.cs:151-179`).

5. `RunService` reads the agent patch once before verifier execution and holds
   verifier image creation, pristine copy, and patch application under P. It
   releases P before commands execute and cleans the verifier image in a
   surrounding `finally`
   (`src/wharf/Run/RunService.cs:430-505`,
   `src/wharf/Run/RunService.cs:522-544`).

6. Existing whole-trial archival moves every trial entry, including agent
   evidence, artifacts, verifier evidence, result, and any retained workspace,
   below `trial-retries/retry-N`
   (`src/wharf/Run/RunService.cs:677-710`). Verifier-only retry needs a narrower
   archive that moves only `verifier/` below
   `verifier-retries/retry-N/`.

7. Progress currently counts `trial-retry` and
   `agent-infrastructure-retry`, and only `trial-retry` clears the prior error
   while the replacement execution is active
   (`src/wharf/Run/ProgressService.cs:113-137`,
   `src/wharf/Run/ProgressService.cs:255-273`). A new `verifier-retry`
   transition must count as a retry, clear the superseded verifier error, and
   remain verifier occupancy.

8. The live K=2 run demonstrated the cost and scoring impact. Two verifier
   timeouts caused full agent reruns; both retries passed. The Koota cumulative
   agent cost rose from 24.18 to 111.12 AI Credits and Skrub from 31.94 to
   61.47. This is retained evidence, not a change target for the active run.

## Recommended approach

Add a bounded inner verifier retry loop in `RunService`:

- execute verifier image preparation, pristine workspace creation, patch
  application, command execution, and image cleanup once per verifier attempt;
- catch only `VerifierTimeout` and `VerifierInfrastructure`;
- archive the failed `verifier/` directory under
  `verifier-retries/retry-N/`;
- emit `status=verifier-retry retry=N reason=<type>`;
- apply the configured retry backoff;
- recreate verifier image/evidence/workspace and apply the same in-memory
  retained patch;
- record the final verifier retry count in `result.json`; and
- never add agent cost or invoke agent preparation/execution in this loop.

Remove verifier failure types from the whole-trial retry predicate. Keep
whole-trial retry unchanged for agent-side typed failures. Use the same
configured maximum retry count independently at the verifier boundary so a
verifier retry cannot consume or manufacture a logical K attempt.

## Validation requirements

- A helper-level test proves timeout then success invokes verification twice,
  archives the first evidence, and returns success.
- Retry classification tests prove verifier failures are verifier-only and no
  longer whole-trial retryable.
- Progress tests prove `verifier-retry` increments retries, clears the
  superseded error, and remains active verifier work.
- Existing evidence-before-error, P-release, whole-trial archive, agent retry,
  timeout, and full-suite tests remain green.
- Documentation states that verifier retry reuses the retained patch in a new
  pristine verifier workspace.

## Not taking

- Do not change the active K=2 process or its already loaded binary.
- Do not retry ordinary reward-zero or deterministic verifier failures.
- Do not reuse a failed verifier workspace or verifier image.
- Do not alter K accounting.
- Do not change retry defaults or add a new public retry setting.
- Do not convert agent-side failures to verifier-only retries.
