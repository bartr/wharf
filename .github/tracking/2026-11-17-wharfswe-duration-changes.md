# WharfSWE duration estimate changes

Implements the [plan](2026-11-17-wharfswe-duration-plan.md).

Added `wharf-swe/tasks/agent-duration-estimates.json`, automatically loaded by
the existing schema 1.2 loader. All 14 tasks have 12 observations from the
HydraFusion, GPT-6.1 Sol, and GPT-6 Luna CLI 1.0.112 arms (168 logical trials).
No reward-zero trials were excluded. All source rows match current task
checksums, and these arms have no whole-trial or verifier retries.

Agent estimates use exported actual agent-command seconds. Verifier estimates
use summed command milliseconds from retained `verifier/trial-result.json`.
Per-task medians are rounded to milliseconds with decimal half-even rounding;
their sum is the scheduling estimate. No fabricated offsets are applied.
The existing required C# language stride of 1 introduces no reordering.

An independent Decimal-based recalculation caught floating-point half-millisecond
rounding differences in the first draft. Corrected those values and reran
complete independent validation successfully.

README documents sources, aggregation, automatic loading, K-wave ordering,
and shortest-first reversal. A focused test loads the real suite and checks
complete positive estimates, combined durations, and longest-first ordering
across two K waves. No task payload or scheduler implementation changed.
