# Wharf 1.5.0

Wharf 1.5.0 builds on the MVP release of the DeepSWE/Harbor benchmark
execution platform. It provides deterministic DeepSWE task selection, isolated
Copilot, Claude Code, or Codex agent and verifier execution, durable evidence, recovery, unattended
benchmark operation, HydraFusion support, and reproducible analytics. It runs
[Harbor-format tasks](docs/Harbor.md), which bundle task instructions,
execution environments, and automated verification of the resulting code.

## Running Wharf

- Tested hosts are Ubuntu 24.04 LTS
  - Tested on bare-metal, cloud VM, and WSL2
  - Other Linux versions / distros will work
    - The setup scripts will likely need to be updated

- Clone this repo
  ```bash
  git clone https://github.com/bartr/wharf

  # start in the repo root
  cd wharf
  ```

- Save GitHub Copilot PAT to `.wharf`
  ```bash
  mkdir -p $HOME/.wharf
  printf %s "github_pat_for_copilot_..." > $HOME/.wharf/copilot

  # set access
  chmod 0700 $HOME/.wharf
  chmod 0600 $HOME/.wharf/*
  ```

- Install pre-requisites
  ```bash
  # base.sh assumes a stock Ubunutu install - adjust as needed
  sudo ./setup/base.sh

  # ensure $HOME/bin is in your path
  ./setup/wharf.sh
  ```
  `setup/wharf.sh` installs Copilot, Claude Code, Codex, and Muse, then builds
  and installs Wharf. Node.js and npm must already be available for Codex;
  also ensure `$HOME/.local/bin` is on your `PATH` for Claude Code and Muse.
- Run `wharf check` to validate setup
    ```bash
    wharf check
    ```
  Disk capacity requires at least 50 GiB free on the filesystem containing
  Docker's reported `DockerRootDir`, not necessarily `/`. The capacity report
  includes `diskPath`. Storage discovery or access failures remain blocking,
  even with `--allow-unsupported-host`.
- Run smoke test
   ```bash
   cd wharf-swe
   wharf smoke
   ```
- Prebuild the docker image cache
   ```bash
   # prebuild help
   wharf prebuild -h

   # adjust -p 12 based on your disk IOPs
   wharf prebuild -p 12
   ```

- Run a WharfSWE test
  ```bash
  # run help
  wharf run -h

  # adjust -n 16 based on the number of vCPUs - 2 * vCPUs is a good starting point
  # adjust -p 12 based on IOPs
  # adjust -k 4 based on the number of 14 test runs
  #   DeepSWE standards are 4-5 iterations of all tests
  wharf run -n 16 -p 12 -k 4 --model gpt-6-luna
  ```

## Progress and retained results

Use `wharf progress --log-file <run-directory>/run.jsonl` to inspect a run
without attaching to its tmux session. Ctrl+C in the running pane requests
cancellation so Wharf can retain evidence and clean up its owned resources.
Use `wharf resume --run-directory <run-directory>` to continue unfinished work.

From `wharf-swe`, export completed runs and rebuild the data-only index:

```bash
wharf analytics export
wharf analytics manifest
```

Export defaults to `./jobs`, discovers completed `all-N` runs recursively, and
combines completed jobs for each arm into `data/<arm>.jsonl`. Incomplete runs
and runs needing resume are skipped with a reason. Existing JSONL files are
left unchanged; use `wharf analytics export --recompute` to rebuild them.
Discovery stops at run directories and does not follow directory symlinks.
It reads retained evidence only and does not consume model quota.

`--run-directory` can select a jobs tree, one arm, or one `all-N` run.
CLI/model/version/effort are inferred; contradictory explicit labels fail.
Unrecorded subagent availability is null unless supplied with `--subagents`.
Use `--output-directory` for a different export destination, or `--output`
for a single arm. Explicit paths inside an `all-N` run still default to the
suite's sibling `data` directory. No manifest, README or chart is auto-updated.

Choose N/P explicitly from measured host
capacity; Wharf does not automatically tune concurrency.

## Operator defaults

Every Wharf invocation creates `~/.wharf/` and `~/.wharf/config.json` if missing,
including no arguments, help/version, invalid flags, dry-run and resume.
Initialization uses built-in defaults, not environment or command-line overrides,
and never overwrites existing files. New runs, dry-run, smoke and prebuild read
these defaults; edit the generated file when persistent overrides are useful.
The generated default configuration is:

```json
{
  "schemaVersion": "1.0",
  "k": 1,
  "n": 4,
  "p": 4,
  "timeouts": {},
  "retries": { "maxAttempts": 1 },
  "outputFormat": "text",
  "retainWorkspaces": false,
  "shortestFirst": false
}
```

All fields are optional; omitted `schemaVersion` means `1.0`.
Precedence is **flags > supported environment variables > user config > defaults**.
Run concurrency still caps P at N; prebuild uses configured P without an N cap;
smoke always uses K=N=P=1. Timeout values must be positive seconds; omitted
phase values use task timeouts, then built-in defaults. `setupSeconds` applies
to agent and verifier preparation. `maxAttempts` is the maximum infrastructure
retry count, not total logical attempts; zero disables retries. Retry values
must be non-negative. `reportSeconds` is unsupported. Explicit Boolean flags
such as `--retain-workspaces false` or `--shortest-first false` override defaults.

Supported run environment settings remain `K`, `N`, `P` (preferred over
`WHARF_K`, `WHARF_N`, `WHARF_P`), `MODEL`/`WHARF_MODEL`, `WHARF_EFFORT`,
`WHARF_CLI`, `WHARF_OUTPUT_FORMAT`, `WHARF_OUTPUT_DIRECTORY`,
and `WHARF_RETAIN_WORKSPACES`. Timeouts and retries
are configured in the user file, not through new flags or environment aliases.

Missing configuration is atomically initialized without overwriting a concurrent
creator's file. Initialization errors fail explicitly, including on help/version.
Malformed JSON, unknown/duplicate keys, unsupported schemas and invalid settings
fail explicitly when loading defaults, even if a flag would override them.
Existing files are not validated by startup alone, so help and retained-run
commands still work with malformed current config. After initialization,
configuration is read-only during command execution;
keep credentials in their separate credential sources, never in this file.
Provider, model, effort, CLI version, tasks, output location, batch size and
no-op/oracle modes are not user-config fields; choose them through existing
flags and supported environment variables.

`--manifest` is removed, with no compatibility alias or alternate config-path
flag. Migrate operational defaults into the supported fields above and keep
run-specific choices explicit. Old mount/artifact/retention-day/resource-policy
input fields had no runtime implementation; they are not user-config controls.
Their persisted effective-configuration fields remain readable for old runs.
Every new run retains its secret-free resolved configuration in `run-plan.json`.
Resume and batch continuation restore that exact policy without reading today's
user config; dry-run prints effective settings without Docker or run-output creation.
User-config initialization is the sole startup write for otherwise read-only
commands, including dry-run and retained-run previews.
Harbor task manifests and `wharf analytics manifest` are unchanged.

`--container-cpus` is also removed. Unset `WHARF_CONTAINER_CPUS` and remove
`containerCpus` from user config; obsolete input fails explicitly. Wharf no longer
probes Docker CPU availability or restricts agent, verifier or baseline CPU
affinity. Existing run evidence is unchanged and remains readable, but resumed
execution no longer applies historical CPU restrictions. N/P concurrency,
8 GiB container memory limits and agent/baseline PID limits are unchanged.

## Choosing the agent CLI

### Subscription-aware batches

**The benchmark adapts to your workflow—not the other way around.**

Run what your quota and schedule allow, then continue later without losing
completed results or manually cleaning up quota-interrupted attempts.

Start a fixed K-attempt experiment in batches of at most B pending logical trials:

```bash
# From the repository root
cd wharf-swe
wharf run --cli codex --model gpt-6.1-sol -k 4 -n 32 -p 12 -b 25
```

After checking the reported run directory, continue from that exact `all-N`
directory with `wharf run -b 1`, or from anywhere:

```bash
wharf run --run-directory /absolute/path/to/all-1 -b 20
wharf run --run-directory /absolute/path/to/all-1 --batch-size 2 --dry-run
```

Continuation restores the original task subset/checksums, K, N, P, provider,
model, effort, exact resolved CLI version, timeouts, retries, and other policy.
Do not repeat or override those flags; only the batch size, diagnostic controls,
and refreshed credentials may change. It never resolves `latest` again.
Ordinary failures finish their slots; batching does not rerun them to seek passes.
B counts admitted logical trials, including failures, not successes or retries.
Copilot also supports bounded batches.

Explicit Claude/Codex subscription exhaustion stops new agent starts and agent
retries, even without `-b`. Already running agents and verifiers drain. Each
quota-interrupted execution, including a partial passing patch, is rejected:
Wharf first writes a secret-free audit under `.github/tracking/`, then permanently
deletes that execution's raw payload, leaving its logical slot pending.
Known rejected usage/cost/time remains in the audit, separate from accepted
analytics. Provider reset dates/times are retained without assuming a timezone
when none is supplied. Generic HTTP 429 or service overload alone does not trigger this.

After a manual quota reset, run another batch; Wharf does not sleep, sign in,
or switch models automatically. Exit 0 means the invocation finished normally
(possibly only its batch); exit 6 means quota-paused. Console output and atomic
schema `1.0` `batches.json` distinguish batch-limit, completed, quota-paused,
cancelled, and failed states. Interrupted deletion is recovered before admission;
one writer owns a run, while dry-run/progress remain read-only.

### Provider selection

Copilot remains the default. Select Claude Code with `--cli claude` or Codex
with `--cli codex` on `run`, `prebuild`, or `smoke`. Claude defaults to Sonnet
5.5 (`claude-sonnet-5-5`); Codex defaults to `gpt-6-luna`. Override the model
with `--model` (`-m`) on `run` or `smoke`, or with `WHARF_MODEL`. Both commands
also accept `--effort` (`-e`), overriding `WHARF_EFFORT`. Explicit effort uses each CLI's
native setting; Wharf's `max` maps to Codex's `xhigh`, while `default` leaves
effort selection to the model.

Save a Claude Code OAuth token obtained with `claude setup-token` as plain text
in `~/.wharf/claude` and restrict the file to your user with
`chmod 0600 ~/.wharf/claude`. Wharf forwards it only to the agent execution process as
`CLAUDE_CODE_OAUTH_TOKEN`; it is not included in images, command arguments, or
retained configuration. Copilot continues to use `~/.wharf/copilot`.

For Codex, sign in with your ChatGPT account using file-based storage:

```bash
codex -c 'cli_auth_credentials_store="file"' login
```

For SSH/headless machines, add `--device-auth`. Wharf reads Codex's native
`~/.codex/auth.json`, or `$CODEX_HOME/auth.json` when `CODEX_HOME` is set;
use the same `CODEX_HOME` setting when signing in and running Wharf.
No separate Wharf credential copy or plain-text API key is needed. If the file
is missing, Wharf reports the path and both login commands without launching
authentication automatically. Run and smoke validate the auth file before
CLI version lookup, image pulls/builds, or agent execution; setup failures
abort without creating run output. It does not fall back to `~/.wharf/codex`.

Wharf creates a private copy at `/home/wharf/.codex/auth.json` only during
agent execution, with directory mode 0700 and file mode 0600. It removes the
secret transport variable before invoking Codex. The credential is never
placed in the workspace, images, patches, or retained evidence. Codex may
refresh its isolated copy; Wharf does not overwrite the source file with
refreshed credentials. Keep that source authentication current and qualify
subscription-token refresh behavior before long or concurrent benchmark runs.

From the repository root:

```bash
cd wharf-swe
wharf prebuild --cli claude -p 12
wharf run --cli claude -n 16 -p 12 -k 4

wharf prebuild --cli codex --cli-version latest -p 12
wharf run --cli codex --cli-version latest -n 16 -p 12 -k 4
wharf smoke --cli codex --cli-version latest --model gpt-6-luna --effort high
```

`--cli` overrides `WHARF_CLI`, then `copilot`.
On `run`, `smoke`, and `prebuild`, `--cli-version` pins the selected CLI or
resolves its own `latest` version once before preparation. Version precedence
is flag, then provider `latest`; user config cannot pin versions. Wharf uses
the resolved exact version in image caches, run directories, and retained
evidence, independently of any host-installed CLI.

`WHARF_CLI_VERSION` and `CLI_VERSION` are no longer supported. If either is
set, Wharf stops with migration instructions, even with an explicit version
flag. Remove old shell/profile exports and run:

```bash
unset WHARF_CLI_VERSION CLI_VERSION
```

Caches and run directories are CLI-specific. Resume
restores the original CLI and exact version from retained evidence.
Use `wharf run --cli codex --dry-run` to preview without reading credentials,
creating output, contacting a registry, or starting Docker.

## Running DeepSWE with Wharf

```bash
# make sure you're in the repoRoot/deep-swe directory
cd ../deep-swe
```
```bash
# run smoke test
wharf smoke
```

```bash
# fill the docker cache (113 * 2 images to cache - it takes about 8 min)
wharf prebuild -p 12
```

```bash
# run the test - this takes 4-10 hours depending on -n, -k and the model
# luna is the least expensive model
wharf run -n 16 -k 4 -p 12 --model gpt-6-luna
```

## References

- [Harbor test process](docs/Harbor.md),
- [DeepSWE results](docs/RESULTS.md), [analytics contract](docs/ANALYTICS.md), and
- [WharfSWE summary](wharf-swe/README.md). The datasets remain separate.
