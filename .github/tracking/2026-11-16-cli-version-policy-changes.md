# CLI version policy changes

Implements the [approved plan](2026-11-16-cli-version-policy-plan.md).

## Changes

- Version precedence is now explicit `--cli-version`, manifest `cliVersion`,
  then `latest` for the selected provider.
- Central configuration validation rejects both obsolete version environment
  variables, including when a flag would otherwise override them. Diagnostics
  instruct operators to unset both variables and use a flag/manifest pin.
- Run/prebuild environment collection includes both deprecated names for
  rejection, not selection. Smoke rejects them before host probes; resume
  rejects them before planning and still uses immutable retained versions.
- A shared CLI-version option is available on run, smoke, and prebuild.
  Smoke forwards the pin into the existing provider-aware configuration path.
- Provider version resolution, exact-version installation, and effective
  version propagation into caches/directories/evidence remain intact.
- Updated README/reference documentation and removed the old smoke
  environment-variable workaround.

## Verification

Before implementation, twelve selected version tests yielded **8 failed,
4 passed**: obsolete settings were accepted, and smoke lacked the option.

After implementation, the targeted provider/configuration/run/retry/baseline/
smoke/resume/analytics/execution/build selection yielded **270 passed,
0 failed, 0 skipped**. Coverage includes all providers' flag/manifest/default
precedence, explicit pins despite obsolete settings, smoke pin propagation,
and Claude/Codex directory names using their resolved provider versions.

Tests ran foreground in named tmux session `wharf-cli-version-policy`.
The validation process alone unset the obsolete environment settings; no host
shell/profile environment was changed. The pending auth fail-fast fix remains
included and tested. See the [review](2026-11-16-cli-version-policy-review.md).
