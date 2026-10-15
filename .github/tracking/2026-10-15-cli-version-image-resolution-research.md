# CLI Version Image Resolution Research

## Requirements

- An exact CLI version selected by `--cli-version`, `WHARF_CLI_VERSION`, or the
  manifest must be authoritative for image preparation.
- Exact version selection must not depend on the host-installed `copilot`
  executable.
- `latest` must resolve to the current published `@github/copilot` package
  version, not the host-installed version.
- The derived image build must continue validating that the installed package
  exactly matches the resolved version.

## Findings

- Smoke forwards `WHARF_CLI_VERSION` into shared configuration resolution
  (`src/wharf/Smoke/SmokeService.cs:66-80`).
- Configuration already applies
  flag-over-environment-over-manifest-over-default precedence
  (`src/wharf/Configuration/RunConfiguration.cs:171-178`).
- Run setup currently calls `ProcessCopilotCliProbe` before any image
  preparation (`src/wharf/Run/RunService.cs:105-125`,
  `src/wharf/Run/RunService.cs:1157-1171`).
- `CopilotAdapter.PrepareAsync` always probes the host CLI and rejects an exact
  requested version when it differs from that host version
  (`src/wharf/Copilot/CopilotContracts.cs:90-106`).
- The CLI payload cache is already keyed by exact version and builds a missing
  image automatically (`src/wharf/Image/DockerAgentImagePreparer.cs:198-224`,
  `src/wharf/Image/DockerAgentImagePreparer.cs:269-294`).
- The payload Dockerfile installs `@github/copilot@<version>` and verifies the
  installed package version exactly, so image creation is the authoritative
  runtime validation point (`src/wharf/Image/DockerAgentImagePreparer.cs:320-346`).

## Alternatives

1. Require the host CLI to match.
   - Rejected: it prevents the version-specific image cache from building and
     couples container execution to unrelated host state.
2. Treat an exact requested version as effective without probing; retain the
   host probe for `latest`.
   - Rejected: `latest` would still mean "host installed", not "currently
     published".
3. Treat exact versions as effective directly and resolve `latest` from the npm
   registry.
   - Recommended: this matches user-facing configuration semantics and keeps
     exact package validation in the image build.

## Risks

- Registry resolution adds network activity for `latest`; failures must be
  explicit rather than silently falling back to the host version.
- Registry responses must be parsed strictly and normalized through the
  existing three-part version validation.
- Tests must not access the network; latest resolution remains behind the
  existing probe interface.

## Recommendation

Change the adapter to call its version probe only for `latest`. Replace the
run-time host CLI probe with a registry-backed implementation that resolves the
published `@github/copilot` latest version. Keep exact image naming, package
installation, and build-time version verification unchanged.

## Out of Scope

- Changing configuration precedence.
- Changing cache naming or invalidating existing correctly versioned images.
- Adding prerelease selection or semver ranges.
