# Independent N and B Gates: Review

## Outcome

Accepted for N=32 qualification.

## Contract Review

- The outer asynchronous trial loop is bounded by N.
- Image preparation, root workspace copy, ownership handoff, and pinned Copilot
  installation share a cancellation-aware B gate.
- The B permit is released before `status=running-agent` and before
  `AgentExecutionService.ExecuteAsync`.
- The agent runner no longer copies `/app` or invokes npm.
- Root initialization has no network and receives no credentials.
- CLI installation and agent execution run as UID/GID `1000:1000`.
- Preparation failures release the B permit and retain agent error evidence.
- Cancellation applies to gate waits and Docker processes.

## Validation

| Check | Execution | Outcome |
| --- | --- | --- |
| Focused agent/preparation tests | Completed | 10 passed, 0 failed |
| Full test project | Completed | 95 passed, 0 failed |
| Docker smoke path | Completed | Agent completed; verifier passed |
| Build | Completed | Succeeded |

## Qualification Guidance

The code is ready for a measured `N=32 B=12` run. This review does not claim
that the host is qualified for N=64; raise N only after observing memory and I/O
pressure through a complete agent/verifier lifecycle while keeping B fixed.

## Follow-ups

- Operational evidence: run `N=32 B=12` on native wired Ethernet with Wi-Fi
  disabled and the faulty dock disconnected.
- If stable, repeat at `N=64 B=12`; do not raise B at the same time.
