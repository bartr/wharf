# Agent resource guards review

## Requirements review

- Root cause addressed: yes. pnpm automatic CLI management is disabled in the
  agent environment while the writable retained home remains under the
  workspace.
- Host containment added: yes. Every agent container has the approved 4 GiB
  memory, no-additional-swap, and 512-PID limits.
- Existing security boundaries preserved: yes. The agent remains UID/GID
  `1000:1000`, source remains read-only, hidden verifier material is not
  mounted, and credentials retain the existing environment-variable path.
- Cancellation cleanup ordering hardened: yes. Cleanup executes even if
  cancellation notification throws.
- Preparation and verifier resource behavior unchanged: yes.

## Validation review

| Check | Execution | Outcome |
| --- | --- | --- |
| Focused agent tests | Completed | 8/8 passed |
| Full test suite | Completed | 95/95 passed |
| Build | Completed | Passed |
| Bounded pnpm reproduction | Completed | Exit 0, no recursive installer |

## Residual work

- Pier comparison remains an evidence gap: no local Pier job directory or
  public result for this exact trial was available.
- N=32 should be rerun before attempting N=64. Docker will now terminate an
  individual agent that exceeds either resource limit instead of allowing it
  to exhaust the host.

