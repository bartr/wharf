# N=32 qualification review

## Requirement review

- OOM containment: future runs stop on the first Docker OOM event and retain a
  typed failure.
- Dataset memory: future agent and verifier containers use 8 GiB with no
  additional swap.
- Timeout enforcement: independent agent-build, agent, verifier-build, and
  verifier limits are loaded from the correct manifest tables and enforced.
- Scoring: verifier reward, not command exit code, determines pass/fail.
- Failed-agent patches: every nonempty complete patch reaches verification.
- Infrastructure retry: the observed Copilot internal HTTP 400 receives one
  fresh-workspace retry with separate evidence.
- Prompt safety: leading-dash instructions are attached to the prompt option.
- Evidence: committed, uncommitted, binary, and new-file changes are retained;
  Wharf runtime files remain excluded.

## Validation review

| Check | Execution | Outcome |
| --- | --- | --- |
| Focused agent, verifier, and task-loader tests | Completed | 26/26 passed |
| Focused build | Completed | Passed |
| `git diff --check` | Completed | Passed |
| Manual Koota verifier | Completed | reward=1, F2P 43/43, P2P 172/172 |
| Full Docker-backed suite | Deferred | Active N=32 qualification still running |

## Evidence interpretation

- The active run uses the pre-fix binary and 4 GiB agent limits. Its OOM
  failures and displayed pass count are not release evidence for the new code.
- Pier wall-clock phase timestamps were collected at N=115 on a different
  40-vCPU host. Cost and matched result comparisons remain useful; raw phase
  duration comparisons require normalized execution boundaries.
- Koota proves that treating agent exit status as task score underreports
  successful patches. Its stopped agent exited 137, while the preserved patch
  received reward 1.

## Review outcome

The focused implementation and manual evidence satisfy the discovered
correctness requirements. Acceptance remains provisional until the active run
finishes and the same workload is repeated with the pushed fixes.

## Next session starter

Recompute the completed K=1 run from retained rewards and typed failures, then
rerun N=32/B=12 with the 8 GiB, timeout, OOM, retry, complete-patch, and
failed-agent verification fixes.
