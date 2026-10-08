# Combined duration scheduling changes

## Motivation

Agent-only longest-processing-time admission can leave tasks with moderate
agents but slow verifiers near the drain tail. During the second K=1
qualification run, `pwntools-tube-multiplexing` reached a verifier duration
over ten minutes after being admitted late.

## Implementation

- Upgraded `agent-duration-estimates.json` from schema 1.1 to 1.2.
- Imported median agent and verifier intervals from the same 452 Pier attempts
  across 113 tasks.
- Regenerated deterministic scheduling durations from the combined median
  agent-plus-verifier duration.
- Preserved K-wave ordering, Rust spacing, and stable task/attempt tie-breakers.
- Updated both production task admission and the standalone scheduler to use
  combined duration.

## Reference observation

The Pier reference median for `pwntools-tube-multiplexing` is:

- agent: 932.871 seconds;
- verifier: 681.049 seconds;
- combined: 1,613.920 seconds.

The task is therefore no longer ranked from agent duration alone.

## Validation

- Importer regeneration was byte-for-byte stable.
- Focused loader, ordering, and scheduler tests: 12 passed.
- Formatting verification passed.
- `git diff --check` passed.
