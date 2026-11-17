# CLI version policy plan

## Research and approved policy

`RunConfiguration.cs:204` applies the ambient version pin to every provider.
`CommandTree.cs:137,245` exposes explicit pins for prebuild/run, but smoke
does not expose the option. Exact resolved versions already flow into
`RunService.cs:119` configuration and job identities, and provider-specific
image recipes install the selected package at that exact version.

The user approved:

- Remove environment-based version selection for every provider.
- Keep manifest `cliVersion` below explicit `--cli-version`.
- Reject obsolete `WHARF_CLI_VERSION` and `CLI_VERSION` settings with migration
  instructions, rather than silently ignoring a specified pin.
- Support explicit pins on smoke as well as run/prebuild.
- Resolve an unpinned provider's own latest version and retain that exact
  version in directory names, caches, and evidence.

## Tasks

1. [x] Add regression coverage for obsolete settings, flag/manifest/default
   precedence, and version-option parsing on all three commands.
2. [x] Centralize obsolete-setting rejection, remove environment precedence,
   add the shared CLI-version option to smoke and its configuration flow,
   and preserve exact retained versions during resume.
3. [x] Cover smoke pin propagation and provider-resolved directory identities.
   Update README/reference docs and verify actual migration errors before
   output, network lookup, or Docker mutation.
4. [x] Run targeted provider/configuration/command/retry/baseline compatibility,
   Release build and formatting in named tmux; retain review evidence.

Preserve the pending authentication fail-fast fix. No host credential reads,
model requests, installation changes, rewrites of retained runs, or commits.
