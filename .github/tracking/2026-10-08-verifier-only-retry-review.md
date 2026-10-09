# Verifier-only retry review

## Requirement review

- Verifier timeout and external termination retry only verification: satisfied.
- Exact retained agent patch is reused: satisfied; the patch is read before the
  verifier loop and captured unchanged by every verifier attempt.
- Fresh verifier environment: satisfied; every attempt performs image
  preparation, pristine workspace creation, and patch application under P.
- Verifier commands do not hold P: satisfied; `VerifyPreparedAsync` remains
  outside `PreparationGate.ExecuteAsync`.
- Agent is not rerun or recharged: satisfied; agent preparation/execution and
  `totalCost` mutation remain outside the verifier retry helper.
- Failed verifier evidence is retained separately: satisfied under
  `verifier-retries/retry-N/`.
- Agent-side retry behavior is preserved: satisfied; OOM, agent timeout, and
  other typed agent failures remain eligible for whole-trial retry.
- Reward-zero verifier is not retried: satisfied; only
  `VerifierTimeout` and `VerifierInfrastructure` are verifier-retryable.
- K accounting is unchanged: satisfied; verifier retry is nested inside one
  logical attempt.

## Correctness review

- The verifier image is cleaned in the per-attempt `finally` before failed
  evidence is archived.
- The top-level verifier directory is recreated before retry image-build
  evidence is written.
- A failed archive cannot produce a success-shaped retry; it throws explicitly.
- Retry exhaustion rethrows the typed verifier exception and cannot fall
  through to whole-trial retry.
- `verifierRetry` records the final retry count even when the last verifier
  attempt fails.
- Progress counts `verifier-retry`, clears the superseded failure, and reports
  verifier occupancy.

No implementation defects were found in read-only review.

## Validation

| Check | Execution | Outcome |
| --- | --- | --- |
| Retry, verifier, and progress tests | Completed | 45/45 passed |
| Broad suite excluding Docker-backed smoke | Completed | 157/157 passed |
| Formatting verification | Completed | Passed |
| `git diff --check` | Completed | Passed |
| Active-run isolation | Completed | Loaded K=2 process remains untouched |
| Full Docker-backed suite | Completed | 162/162 passed |

## Review outcome

Implementation is approved. The Docker-backed full suite passed after the K=2
benchmark completed, without modifying that benchmark's retained evidence.
