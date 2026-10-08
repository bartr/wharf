# N=32 rerun error analysis

## Corrected terminal result

- Tasks: 113
- Passed: 74
- Failed tests: 34
- Timeouts: 3
- Infrastructure errors: 2
- Pass@1: 65.49%

The console reported 42 errors because it treated reward-zero test outcomes as
errors and retained provisional
agent OOM failures even when the retained patch later passed verification.
Three OOM-affected tasks passed. The terminal partition is 74 passes, 34 failed
tests, 3 timeouts, and 2 infrastructure errors.

## Final failure classification

| Type | Count | Meaning |
| --- | ---: | --- |
| Failed test | 34 | Verifier executed successfully, but authoritative reward was zero |
| Timeout | 3 | Agent hit the 90-minute limit and its retained patch received reward zero |
| OutOfMemory | 2 | Agent hit its 8 GiB cgroup limit and its retained patch received reward zero |

Five agents encountered cgroup OOM:

- optique-conditional-option-dependencies: reward 1
- scc-bounded-memory-spilling: reward 1
- wazero-multi-module-snapshots: reward 1
- opa-rego-rule-profiling: reward 0
- pebble-durability-wait-apis: reward 0

All OOMs were container-memory-cgroup events rather than host-global OOMs.
Automatic OOM termination, complete patch retention, and failed-agent
verification worked as designed.

Three agents reached the 90-minute timeout:

- koota-composite-trait-aspects: F2P 96.08%, P2P 100%
- koota-pair-relation-tracking: F2P 97.37%, P2P 100%
- skrub-duration-encoding: F2P 100%, P2P 99.96%

Both Koota agents were still actively testing at the deadline. Skrub repeatedly
failed to fork with `Resource temporarily unavailable`, consistent with the
512-PID limit, so extending only its timeout would not resolve the stall. Wharf
retains a PID guard because timeout and memory limits do not contain
low-memory fork storms, but raises it to 1024. At N=32 this permits at most
32,768 agent processes, matching the existing host preflight floor and
remaining far below z01's PID capacity.

## Reward-zero shape

- 28 failed new/feature tests while preserving all prior tests.
- 9 passed all new tests but regressed at least one prior test.
- 2 failed both new and prior tests.

## Reporting defects corrected

- Reward zero after successful verifier execution is a failed test, not an
  error.
- A terminal verifier pass now clears provisional agent OOM/timeout failure
  state in progress reporting.
- A final failure retains its earlier OOM/timeout classification instead of
  being overwritten by the later generic verifier failure.
- Trial `result.json` now records the actual verifier error, or null on a pass,
  rather than retaining the stale no-patch message after verification ran.

## Retry gap corrected for the next run

The completed N=32 run did not perform whole-trial retries. Production only
retried the exact Copilot HTTP 400 internal-service failure; the configured
trial retry policy was not connected to `RunService`.

Wharf now defaults to one additional whole-trial execution for an OOM, timeout,
or other agent/verifier execution error whose retained patch does not pass.
It does not retry ordinary reward-zero test failures or exceptional agents
whose retained patch passes. The first execution is archived under
`trial-retries/retry-1`, the retry uses fresh workspace/container/verifier
identities, K accounting is unchanged, and reported cost remains cumulative.
Each retained `result.json` records its retry number and cumulative cost.

## Validation

- Focused retry, progress, configuration, and agent tests: 43/43 passed.
- Full test suite, including the Docker smoke test: 124/124 passed.
- Project build and changed-file formatting checks: passed.
- Reprocessing the completed run with the corrected code reports:
  - failed tests: 34
  - timeouts: 3
  - errors: 2
  - OutOfMemory: 2
  - pass@1: 74/113, 65.49%
