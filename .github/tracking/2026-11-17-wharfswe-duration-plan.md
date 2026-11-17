# WharfSWE duration estimates

## Research

- `src/wharf/Task/HarborTaskLoader.cs:12,125` automatically loads
  `tasks/agent-duration-estimates.json` using schema 1.2.
- `src/wharf/Task/TaskSelector.cs:69` orders each K wave by descending
  scheduling duration, with stable task/attempt tie-breakers.
- The schema requires language-stride metadata. WharfSWE has only C# tasks,
  so C# at position 1 with spacing 1 preserves ordinary longest-first order.
- `wharf-swe/README.md` identifies three checksum-matched CLI 1.0.112 arms
  (HydraFusion, GPT-6.1 Sol, GPT-6 Luna), each containing 56 logical trials.
  Exports retain actual agent seconds; verifier results retain per-command
  durations. Use all 168 trials, including reward-zero results.

## Plan

1. [x] Validate task/checksum coverage and timing evidence for those three arms.
   Compute per-task median agent and verifier command seconds over 12 trials;
   scheduling duration is their sum, without artificial offsets.
2. [x] Add the schema-compatible JSON under `wharf-swe/tasks/`, document
   provenance, and extend the existing suite-loading test to check complete
   estimates and longest-first K-wave ordering.
3. [x] Run the targeted Harbor task tests in named tmux; independently
   recalculate estimates and verify checksums/payloads remain unchanged.

Do not alter pinned tasks, raw run evidence, exports, analytics manifests,
DeepSWE metadata, or the user's untracked `wharf-swe/run.sh`.
