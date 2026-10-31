# Optional agent packages for Wharf

**Status:** Draft implementation spec.

## Goal

Run the same benchmark with no added guidance, locally prepared HVE or FE
artifacts, a simple nudge, or language-scoped nudges. Treat these as different
packages using one mechanism, not separate agent implementations.

**Copy packages into the isolated agent CLI configuration home, never into
the task repository.** Package files must not enter the generated patch,
task checksum, verifier environment, or grading input.

## Configuration

Add `--agent-package <directory>`, manifest field `agentPackage`, and environment
variable `WHARF_AGENT_PACKAGE`. Follow existing precedence:
flag > environment > manifest > default. Default is no package and must
preserve current behavior. Resolve relative paths against the invocation's
working directory and retain the absolute path in the effective configuration.

The directory is already local and prepared for Copilot CLI. Wharf does not
download packages, run installers, convert HVE collections, or infer artifacts
from an arbitrary repository checkout. Select one package per run; users can
prepare a combined package locally if needed.

Example:

```bash
wharf run --manifest benchmark.json --agent-package ./agent-packages/fe
```

## Source and destination layout

Each package contains any nonempty subset of these native artifact surfaces:

```text
agent-packages/
  hve/                         # prepared HVE artifacts
    copilot-instructions.md    # optional always-on guidance
    skills/
      <skill-name>/
        SKILL.md
        ...                    # supporting files, preserved recursively
    agents/
      <agent-name>.agent.md
    instructions/
      <name>.instructions.md
  fe/                          # same layout, prepared FE artifacts
  nudge/
    copilot-instructions.md    # the nudge text only
  nudge-by-language/
    instructions/
      go.instructions.md
      python.instructions.md
      typescript.instructions.md
```

Copy the selected package's contents into each trial container:

```text
/home/wharf/.copilot/
  copilot-instructions.md
  skills/<skill-name>/SKILL.md
  skills/<skill-name>/...
  agents/<agent-name>.agent.md
  instructions/<name>.instructions.md
```

Explicitly select this directory with `COPILOT_HOME=/home/wharf/.copilot`
for Copilot execution and any configuration discovery/probes. Ensure the
trial user (`1000:1000`) can read the artifacts. Do not modify host user
configuration or `/workspace`, `/app`, `/tests`, or verifier inputs.

For a simple nudge, use the reference text in [nudge.md](../nudge.md) as
`copilot-instructions.md`. This is native always-on guidance, not the old
Pier task-preface mechanism; benchmark it as a distinct intervention.

For a Go-only nudge, `go.instructions.md` starts with:

```yaml
---
applyTo: "**/*.go"
---
```

The nudge follows the frontmatter. Other examples: `**/*.py` and
`**/*.ts,**/*.tsx`. Preserve frontmatter and content verbatim. Let Copilot
apply native file-path matching; do not select instructions merely from the
benchmark task's language label. File scoping is not an isolation guarantee
for subsequent model reasoning in mixed-language work.

## Preparation, persistence, and failures

* Validate before creating run output or Docker resources: require a readable,
  nonempty package with valid artifact layout. Reject symlinks, unsupported
  root entries, and missing `SKILL.md` files. Do not accept settings,
  credentials, plugins, or hooks as package surfaces.
* Dry-run validates and reports package identity without copying files,
  creating output, or contacting services.
* On run creation, retain an immutable package snapshot outside task
  workspaces. Record sorted relative paths, file hashes, and a package content
  hash in secret-free run evidence and the retained run plan.
* Copy from that snapshot after agent-home initialization and before agent
  execution, under the existing preparation gate. Do not bake the package
  into a shared image or share writable CLI homes between trials.
* Fresh whole-trial retries receive the identical package snapshot.
  Verifier-only retries receive no package. Resume restores and validates the
  retained snapshot rather than reading potentially changed source files.
* Missing/corrupt snapshots, invalid packages, copy failures, and unsupported
  CLI loading behavior fail explicitly with retained preparation diagnostics.
  Never silently run a requested package as the baseline.
* Preserve methodology credits and license/provenance files within skill
  directories. Do not synthesize extra activation prompts or force RPI/skill
  invocation beyond the selected package's own instructions.

## Acceptance tests

1. No-package runs retain existing invocation and patch behavior.
2. Each package form is copied to the selected CLI home with exact contents;
   task files/checksums remain unchanged and injected files never enter patches.
3. The supported Copilot version demonstrably discovers the copied artifacts.
   Verify language-scoped instruction inclusion for matching/nonmatching
   files using harness evidence, not just the existence of copied files.
4. Parallel trials have separate homes; agent retries and resume use the
   original snapshot even if the source changes or disappears.
5. Verifiers and verifier retries never receive the agent package.
6. Invalid inputs/copy failures fail explicitly; dry-run has no write or
   network side effects.

Run focused configuration, agent-preparation, resume, and patch-capture tests
plus a small Docker-backed loading smoke test. Do not launch a full benchmark
as part of implementing this feature. Subsequent comparisons must identify
the model, CLI version, and package hash and retain failures in PASS@1.
