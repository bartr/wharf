# Wharf 1.1.0

Wharf 1.1.0 releases the accumulated runtime, CLI, analytics, and documentation
changes since 1.0.0. The intervening `1.0.1` branch was a development
checkpoint, not a tagged release.

## Migration: changed flags and defaults

This release includes intentional compatibility changes:

* Replace `--k`, `--n`, and `--p` with `-k`, `-n`, and `-p`.
  The former spellings are rejected, not compatibility aliases.
  Resume supports `-n` and `-p`; retained K cannot be changed.
* New runs default to `hydrafusion` rather than `gpt-5.6-luna`.
* Effort defaults to `default` rather than `max`. This omits the provider
  effort argument and leaves selection to the model.
* To reproduce earlier choices, set model and effort explicitly using flags,
  environment, or manifests. Existing precedence is unchanged.

For example:

```bash
wharf run --tasks enterprise/tasks -k 4 -n 32 -p 12 \
  --model hydrafusion --effort default
```

`-m` and `-e` are short aliases for `--model` and `--effort` in run and
analytics export commands.

## Runtime correction

Exact requested Copilot CLI versions no longer depend on probing an unrelated
host CLI installation. `latest` resolves through authoritative npm package
metadata. Prepared images still install and validate the exact resolved CLI
version. Registry and invalid-response failures remain explicit.

Debian packages now retain the complete .NET publish output rather than only
the apphost executable. Qualification and a regression test execute the
extracted package and verify its version, catching missing runtime payload
files before publication.

## Analytics and operating evidence

* Data-only manifests include model, effort, CLI/version, passing trials,
  P2P regression-error trials, cost per costed trial, and average agent time.
* The separate regression-adjusted score is
  `passes / trials - (p2p_errors * 0.0025)`. It is not PASS@1 or the
  task-equal Enterprise Score.
* Average cost per task and mean trial time are no longer manifest summaries;
  per-trial timing evidence remains in JSONL exports.
* The enterprise C# dataset now contains 24 K=4 arms, 14 tasks per arm, and
  1,344 logical trials. DeepSWE and enterprise comparisons remain separate.
* The retrospective records cold-run preparation-pressure lessons and the
  need to record failed-run evidence before deleting job directories.
* A reproducible PASS@1-versus-cost plotting script accompanies the retained
  benchmark comparisons.

Benchmark exports retain their original CLI versions and runtime provenance;
this release does not relabel historical runs as 1.1.0 executions.

## Qualification and scope

See [release qualification](release-qualification.md) and the
[release review](../.github/tracking/2026-10-28-release-1.1.0-review.md) for
fresh validation results. Package signing requires an operator-provided key;
unsigned packages must remain explicitly labeled.

Single-host operating experience is not a multi-host qualification claim.
Hosted dashboards, repair/replay, alternate agent products, and distributed
orchestration remain outside this release.
