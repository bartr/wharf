# Duration-aware scheduling review

## Acceptance review

- Pier agent time predicts scheduling: satisfied with median
  `agent_execution.started_at` to `finished_at` intervals.
- Estimates live in the task directory: satisfied by the versioned
  `tasks/agent-duration-estimates.json` dataset companion.
- Long tasks enter first: satisfied within each attempt wave.
- K ordering: every attempt 1 admission precedes every attempt 2 admission,
  continuing through configured K.
- Determinism: attempt number, estimate, task ID, and attempt ID form a complete
  stable order.
- K=1 compatibility: existing trial directory naming remains unchanged.
- K>1 isolation: evidence paths and Docker identities include attempt number.

## Validation review

| Check | Execution | Outcome |
| --- | --- | --- |
| Pier import | Completed | 452 intervals, 113 task medians |
| Deterministic regeneration | Completed | Output checksum unchanged |
| Loader/scheduler tests | Completed | 11/11 passed |
| Production build | Completed | Passed |
| Full Docker-backed suite | Deferred | Active qualification run still owned Docker capacity |

## Residual considerations

- Estimates are specific to the named Copilot/Pier reference run. A future
  importer can retain separate profiles by agent, model, effort, and host.
- Median duration is deliberately robust to four-attempt variance, but should
  be refreshed after a clean Wharf qualification run.
- Ordering minimizes the long-task drain tail; it does not reserve resources
  by predicted memory or CPU consumption.

## Outcome

The implementation meets the requested scheduling policy and corrects the
production K-expansion gap discovered while wiring it.
