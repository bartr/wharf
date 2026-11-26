# Remove container CPU controls: review

## Decision: keep the removal

The implementation satisfies the approved breaking simplification. No runtime
CPU allocator/probe/affinity setting remains. Old flag/environment/config input
fails explicitly, while retained CPU evidence stays readable and byte-preserved.
New and resumed execution no longer applies Wharf CPU restrictions; other
resource, preparation, evidence and scoring contracts remain intact.

[Validation evidence](2026-11-25-remove-container-cpus-changes.md) records
221 focused Release tests, Release build, compiled migration/help checks,
legacy-policy readability, monitoring syntax, requirements validity and
whitespace. These are bounded regression checks, not live throughput evidence.

Current documentation records completed removal rather than a future direction.
The negative finding supports retiring the surface; no quantified speed or
productivity improvement is claimed.

## Remaining boundaries

- Pre-existing IDE0008 in AgentExecution.cs:1003 blocks an unqualified format
  pass. All other diagnostics for modified files pass; route the unrelated
  `var buffer` maintenance to a separate task.
- Paid discovery was excluded; no live provider or release qualification claim.
- Archived tracking records and pinned task payloads describe historical CPU
  experiments intentionally. They are evidence, not active runtime support.
