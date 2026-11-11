# HydraFusion Default Label Research

## Findings

- Run configuration defaults effort to `max` for every model, including
  HydraFusion (`src/wharf/Configuration/RunConfiguration.cs:164-165`).
- Run directory identity directly embeds the configuration effort
  (`src/wharf/Run/RunService.cs:1122-1128`).
- The Copilot adapter never sends `--reasoning-effort` for HydraFusion,
  regardless of the configured effort
  (`src/wharf/Copilot/CopilotContracts.cs:108-123`).
- The adapter test explicitly requires the HydraFusion invocation to omit
  `--reasoning-effort`
  (`tests/wharf.Tests/CopilotAdapterTests.cs:124-134`).
- Existing HydraFusion configuration and job-name tests still expect `max`
  (`tests/wharf.Tests/RunConfigurationTests.cs:59-68`,
  `tests/wharf.Tests/RunServiceTests.cs:7-29`).
- The README labels both retained results as “HydraFusion Max”
  (`README.md:40`, `README.md:43`).

## Recommendation

Normalize HydraFusion's effective effort to `default` during configuration
resolution and reuse the same normalization when constructing job directory
names. This makes rendered configuration, run identity, invocation metadata,
and future analytics agree with actual provider-selected behavior. Keep
historical export filenames unchanged for evidence stability, but present the
table labels as only “HydraFusion” plus version and run suffix.

## Out of scope

- Renaming historical job archives or JSONL files.
- Rewriting historical JSONL row metadata.
- Changing HydraFusion invocation behavior.
