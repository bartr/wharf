# Remove container CPU controls: research

The user explicitly requests removal of the flag, implementation and current
documentation. The negative operating finding is already recorded in
`docs/Continuous-Agent-Improvement.md:600`; no useful measured affinity benefit
was established. Keep N/P and batching, memory/PID limits, ownership and evidence.

## Verified dependencies

- `src/wharf/Configuration/RunConfiguration.cs:16,61,77` carries user/effective/
  override CPU settings; the renderer emits them. CommandTree exposes the flag
  and resume display. Smoke also consumes the environment setting.
- `src/wharf/Docker/DockerCpuSetAllocator.cs:6` discovers available CPU IDs
  through a Docker probe and allocates rotating affinity sets.
- `src/wharf/Run/RunService.cs:124,377,667` propagates sets through agent and
  verifier preparation; BaselineRunService uses the same path for trusted baselines.
- AgentContracts and VerifierContracts carry CpuSet in requests/results, and
  Docker create arguments emit `--cpuset-cpus`.
- `src/wharf/State/StateContracts.cs:134` uses general JSON deserialization:
  unknown legacy fields are ignored. `Recovery/RetainedRunMetadata.cs:53`
  reads old run plans without rewriting them. Removed CPU fields can therefore
  remain in historical evidence while new execution stops applying affinity.
- CPU-only tests cover the deleted allocator/policy. Keep memory, PID, ownership,
  pristine-workspace, evidence, timeout, retry and resume checks in adjacent tests.
- `scripts/monitor-wharf-run.py:176` includes the field in new monitoring metadata.

## Recommended approach and boundaries

Remove CPU properties, allocator/policy/probe, argument injection, new-result
fields and supported-input docs. Explicitly reject the obsolete environment
variable and strict user-config key rather than silently accepting them.
Preserve raw evidence and pinned benchmark snapshots. Test old retained CPU
metadata remains readable/unchanged, removed inputs fail before run output,
and agent/verifier/baseline create arguments contain no CPU restriction.

The intentional behavior change applies to resumed execution too: old affinity
settings are historical evidence, not a supported runtime policy. Do not remove
Docker memory or PID limits, alter N/P, change scoring, or launch paid runs.
Full formatting already has an unrelated IDE0008 in AgentExecution.cs:1007;
if shifted by these edits, preserve it and separate baseline formatting evidence.
