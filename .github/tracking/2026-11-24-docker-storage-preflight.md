# Docker storage preflight

## Research

- Reuse the preceding diagnosis: `CapacityProbe` reads free space from `/`
  (`src/wharf/Host/HostChecks.cs:191`), ignoring Docker's configured data root.
- Docker preflight already reads `docker info --format "{{json .}}"`
  (`src/wharf/Host/HostChecks.cs:117`). Its `DockerRootDir` identifies the
  daemon's actual storage directory.
- Both check and smoke construct separate Docker and capacity probes
  (`src/wharf/Command/CommandTree.cs:100`, `src/wharf/Command/CommandTree.cs:1042`).
- Azure setup places Docker and containerd storage under `/mnt/data` when
  mounted (`setup/base.sh:65`). Checking that filesystem fixes this case.
- Existing host tests support injected probes (`tests/wharf.Tests/HostCheckTests.cs:7`).
- Retain read-only behavior, all resource thresholds, platform-only override,
  and explicit errors. No setup mutation, root-volume fallback, threshold
  reduction, or broader storage-policy changes.

## Plan

1. [x] Share one retained Docker info response between Docker and capacity
   probes in check and smoke. Resolve an absolute, nonempty DockerRootDir;
   read free space at that path and include it in capacity diagnostics.
2. [x] Add focused host tests for a separate data disk, exact disk threshold,
   missing/invalid roots, discovery errors, and shared read-only inspection.
   Document the storage check in the root README.
3. [x] Run targeted host/smoke/command tests, formatting verification, and a
   read-only real Docker check in a named tmux validation session. Record
   execution results and review against the requested behavior.

## Changes and review

- Docker and capacity probes share a lazy read-only daemon info snapshot
  within each check/smoke invocation. Capacity resolves DockerRootDir and
  reads that directory's filesystem free space, including diskPath in output.
- Invalid/missing roots, malformed info, discovery failures, and inaccessible
  storage return explicit blocking failures, with no fallback to `/`.
- Tests cover a healthy separate disk despite a small root filesystem, the
  exact 50 GiB threshold and one byte below it, discovery/access failures,
  and one shared Docker inspection. All other thresholds remain unchanged.
- Targeted validation in tmux session `wharf-storage-preflight-20261124`:
  `dotnet test tests/wharf.Tests/wharf.Tests.csproj --no-restore --filter
  "FullyQualifiedName~HostCheckTests|FullyQualifiedName~Smoke|FullyQualifiedName~CommandTree"`
  passed 58/58 tests and built successfully.
- `dotnet format wharf.slnx --verify-no-changes --no-restore --include
  src/wharf/Host/HostChecks.cs src/wharf/Command/CommandTree.cs
  tests/wharf.Tests/HostCheckTests.cs` exited 0.
- The rebuilt CLI's real read-only host check passed and reported
  `diskPath=/var/lib/docker`.
- An end-to-end read-only check with a temporary Docker info fixture reporting
  `/dev/shm` matched independently measured filesystem capacity:
  32,535,187,456 bytes instead of the root filesystem's 1,073,236,955,136
  bytes. It correctly exited 4 for less than 50 GiB free, proving the default
  filesystem reader honors a non-root location.
- `git diff --check` passed. Temporary fixture and completed validation tmux
  session were cleaned up. No Docker resources or host configuration changed.
- Review: requested behavior verified, no threshold changes or root-volume
  fallback, no follow-ups. Root README documents the check and diagnostics.
