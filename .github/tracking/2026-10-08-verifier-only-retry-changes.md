# Verifier-only retry changes

## Implementation

- Added separate classification for verifier timeout and external termination.
- Excluded verifier-only failures from whole-trial retry.
- Added bounded verifier execution retry using the configured retry count and
  backoff.
- Reuses the exact retained agent patch and cumulative agent cost.
- Recreates verifier image preparation, pristine workspace creation, patch
  application, and command execution for each verifier attempt.
- Archives only failed verifier evidence under
  `verifier-retries/retry-N/`.
- Records `verifierRetry` in the final trial result.
- Added `verifier-retry` progress accounting and active verifier occupancy.

## Validation

- Focused retry, verifier, and progress suite: 45 passed.
- Broad non-Docker suite: 157 passed.
- Formatting verification and `git diff --check`: passed.
- Docker-backed full suite after the K=2 benchmark completed: 162 passed in a
  named tmux session.
