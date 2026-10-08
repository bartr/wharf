# Run artifact cleanup changes

## Storage evidence

- Completed Wharf K=1 run before cleanup: 137 GiB and 2,159,741 files.
- Matching Pier K=4 run: 20 GiB and 18,709 files.
- Wharf workspace directories: 136 GiB.
- Wharf agent, artifact, and verifier evidence: approximately 379 MiB.
- Aggregate Wharf JSONL log: 314,862 bytes.

The excess was disposable writable workspace state, primarily package
dependencies, language build output, tool homes, and caches. The largest trial
retained an 8.8 GiB Rust `target` directory.

## Product changes

- Added `retainWorkspaces`, default false, with manifest, environment, and
  `--retain-workspaces` support.
- Default run logging now buffers the initial record until `all-N` exists, then
  persists to `all-N/run.jsonl`. Explicit `--log-file` still wins.
- The completion line prints the resolved log path, and `wharf progress`
  defaults to `run.jsonl` in the current directory.
- Terminal trials delete primary and Copilot infrastructure-retry workspaces
  after durable evidence capture.
- Each verifier image tag is removed immediately after its verifier finishes or
  fails, so retained image storage is bounded by active verifier concurrency
  rather than total K attempts. Derived agent images and Docker build cache are
  preserved.
- A root cleanup container restores UID/GID `1000:1000` and owner write/traverse
  permissions before host deletion. This handles root-owned files and
  read-only Go module caches.
- `result.json` records workspace retention and cleanup errors without retaining
  a stale workspace path.
- Whole-trial retry archives rewrite a workspace path only when that workspace
  was intentionally retained.

## Completed-run cleanup

- Moved repository-level `wharf-run.jsonl` to the completed run as `run.jsonl`.
- Normalized ownership and modes for exactly 113 validated depth-two workspace
  paths.
- Deleted all 113 workspaces.
- Atomically updated the 113 generated trial results.
- Final run size: 380 MiB.
- Filesystem usage moved from 572 GiB used to 436 GiB used.
- Preserved 113 agent results and 113 verifier rewards.
- Removed 113 historical verifier images plus smoke verifier images created by
  validation; Docker build cache was not pruned.

## Validation

- Focused configuration/logger/cleanup/retry/progress tests: 35/35 passed.
- Full suite including Docker smoke integration: 130/130 passed.
- Root ownership cleanup integration: 31/31 passed.
- Final read-only-cache Docker cleanup proof: 22/22 passed.
- Run-scoped verifier image cleanup integration: 15/15 passed.
- Final source-focused tests: 52/52 passed.
- Solution formatting and `git diff --check`: passed.
