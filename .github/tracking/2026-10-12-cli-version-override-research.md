# CLI Version Override Research

## Findings

- `RunManifest` supports `cliVersion`, but `RunConfigurationOverrides` has no
  CLI-version field (`src/wharf/Configuration/RunConfiguration.cs:67-76`).
- Configuration resolution currently takes CLI version directly from the
  manifest or the `latest` default, bypassing flag and environment precedence
  (`src/wharf/Configuration/RunConfiguration.cs:190-199`).
- The run command exposes model and effort options but no CLI-version option;
  normal and dry-run paths construct overrides separately
  (`src/wharf/Command/CommandTree.cs:177-252`,
  `src/wharf/Command/CommandTree.cs:332-347`).
- Normal run environment collection is explicit, while dry-run reads the full
  process environment (`src/wharf/Command/CommandTree.cs:253-276`,
  `src/wharf/Configuration/DryRunService.cs:13-23`).
- Smoke resolution also uses the shared resolver with an explicit environment
  dictionary (`src/wharf/Smoke/SmokeService.cs:66-80`).
- Existing configuration tests establish
  flag-over-environment-over-manifest-over-default precedence
  (`tests/wharf.Tests/RunConfigurationTests.cs:8-27`).

## Recommendation

Add `--cli-version` and `WHARF_CLI_VERSION`, route both through a new
`RunConfigurationOverrides.CliVersion`, and resolve them with the same
flag-over-environment-over-manifest-over-default contract as model and effort.
Keep manifest support as a lower-precedence compatibility source.

## Out of scope

- Changing exact requested/effective version equality.
- Adding a separate CLI-version option to commands other than `run`.
- Changing Copilot package installation or Docker cache behavior.
