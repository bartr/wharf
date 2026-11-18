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

## Choosing the agent CLI

### Subscription-aware batches

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

`--cli` overrides `WHARF_CLI`, then manifest `"cli"`, then `copilot`.
On `run`, `smoke`, and `prebuild`, `--cli-version` pins the selected CLI or
resolves its own `latest` version once before preparation. Run manifests may
also pin `cliVersion`; precedence is flag, manifest, then `latest`. Wharf uses
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
