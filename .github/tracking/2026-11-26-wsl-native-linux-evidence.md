# WSL compatibility versus native Linux capacity

## Outcome and source

The project lead reported on 2026-11-26 that Wharf runs without issues in WSL,
but testing showed substantial overhead: practical concurrency was roughly
one trial slot per available vCPU, versus over two on bare-metal Linux.
This motivated local native Linux execution through VS Code Remote-SSH.
The same procedure and setup scripts were used on Azure, DigitalOcean, and
Hetzner, with small provider-specific cloud-init differences. Running dockerd
inside the Linux execution host, rather than Docker Desktop, was a separate
experience-based choice.

These are attributed operational observations, not a retained matched benchmark.

## Repository evidence

- `docs/wharf.md:294-304` explicitly distinguishes primary Ubuntu hosts from
  lower-throughput WSL2 hosts and specifies shared bootstrap across environments.
- `setup/base.sh:60,65-75,128-132` installs Docker Engine, selects local
  Docker/containerd storage, validates configuration, and restarts local services.
- `setup/disk.sh:4` scopes the optional data-disk script to cloud VMs.
- `docs/Continuous-Agent-Improvement.md:497-509` records the native 16-core
  z01 operating point N=32/P=12 and distinguishes admission from throughput.
- `.github/tracking/2026-10-09-large-host-kernel-limits-research.md:24-43`
  retains DigitalOcean host telemetry while a run drained from N=80.

The separate DeepSWE checkout was absent from `../deep-swe`; Wharf's own
`deep-swe/` directory contains suite data and jobs, not that repository.
Read-only GitHub inspection used DeepSWE HEAD
`7f23215d23352ee0cc8989257e1ea7e9aec9dac1`:

- [Shared setup](https://github.com/bartr/deep-swe/blob/7f23215d23352ee0cc8989257e1ea7e9aec9dac1/setup/base.sh#L22-L38)
  installs Docker Engine; lines 40-70 configure local Docker/containerd storage.
- [Platform requirements](https://github.com/bartr/deep-swe/blob/7f23215d23352ee0cc8989257e1ea7e9aec9dac1/wharf.md#L56-L68)
  describe lower-throughput WSL2 and shared bootstrap.
- [Cold-start findings](https://github.com/bartr/deep-swe/blob/7f23215d23352ee0cc8989257e1ea7e9aec9dac1/docs/cold-start-2026-10-04/README.md#L350-L362)
  recommend N=80-88 on a 40-vCPU OS-disk VM and N=64 on its data disk.
  Those are Pier experiments with storage/cache confounders, not WSL comparisons.

No WSL/native or Docker Desktop/host-local Docker matched results were found
in the inspected top-level narratives, operational runbook, meeting brief,
cold-start report, or listed research records. Provider-specific cloud-init
equivalence and all three deployments remain the project lead's account.

## Bounded documentation decision

Add an overview entry and adjacent infrastructure case in the existing learning
record. Preserve WSL compatibility, distinguish concurrency from throughput,
and attribute unretained measurements and deployment history. Do not generalize
the ratios, claim a Docker Desktop speedup, change setup, or launch benchmarks.

Validation criteria: each numerical claim names its evidence level; citations
resolve to inspected sources; existing scoring and runtime behavior are unchanged.
Documentation-only work needs no build or test run. Quantifying the benefit
requires a future matched comparison retaining hardware, WSL/resource settings,
filesystem and Docker placement, N/P, workload, cache state, elapsed time,
resource pressure, and failures.

## Change and review outcome

Added the case and overview entry to `docs/Continuous-Agent-Improvement.md`.
Kept the operational observations attributed, separated native cloud VMs from
bare metal, and explicitly left both matched comparisons unproven.
Source line references were checked and `git diff --check` passed.
No runtime files changed; builds, tests, and live benchmarks were not run.
Keep the documented operating decision; defer numerical benefit claims to the
matched-comparison follow-up above.
