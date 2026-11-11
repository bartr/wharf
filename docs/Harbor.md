# Harbor test process

Harbor packages an engineering task as an **instruction, an execution
environment, and a verifier**. An agent attempts the task; the verifier checks
the resulting work and produces a machine-readable reward.

The Harbor **task format** and the Harbor **runner** are different things.
Tasks can be executed by any compatible runner. Wharf reads Harbor task
definitions and provides its own preparation, scheduling, agent execution,
verification, retry, and evidence-retention pipeline; it does not invoke the
Harbor CLI to run each trial.

This document describes the general format and the separate-verifier process
used for DeepSWE testing in Wharf. It is not a claim that Wharf implements
every Harbor feature or environment backend.

## 1. What a task contains

A typical single-step Harbor task has this layout:

```text
example-task/
|-- instruction.md
|-- task.toml
|-- environment/
|   \-- Dockerfile
|-- tests/
|   |-- Dockerfile       (optional separate verifier image)
|   \-- test.sh
\-- solution/
    \-- solve.sh         (optional trusted reference solution)
```

| Component | Purpose |
|---|---|
| `instruction.md` | Describes the requested change and requirements shown to the agent. |
| `task.toml` | Identifies the task and declares environment configuration, timeouts, resources, artifacts, and verifier settings. |
| `environment/` | Defines the starting environment, including the repository and dependencies needed to attempt the task. An image reference can also supply the environment. |
| `tests/` | Contains the benchmark verifier and its supporting files. These are evaluation material, not instructions for solving the task. |
| `solution/` | Provides an optional reference implementation for trusted validation. It is not input to the evaluated AI agent. |

Wharf's current loader requires task schema `1.3`, including task metadata,
agent/environment settings, and verifier/environment settings. Its production
benchmark path uses Docker. Broader Harbor documentation may describe other
schemas, shared verification, multi-step tasks, or other environment backends.

The sanitized [parser fixtures](../tests/fixtures/harbor/README.md) intentionally
omit hidden tests, graders, and reference solutions. They test task loading,
not whether an agent can solve the real benchmark.

## 2. A trial, from instruction to reward

A **task** is the problem definition. A **trial** is one logical attempt to
solve that task with a particular model, harness, and configuration.

```text
Select task and retain its identity/configuration
                    |
Prepare isolated agent environment
                    |
Give agent the instruction and starting repository
                    |
Agent edits code and may run available project tests
                    |
Capture the complete patch and agent evidence
                    |
Prepare a fresh verifier environment from the original source
                    |
Apply the retained patch and run the benchmark verifier
                    |
Retain reward, test results, logs, timing, and terminal outcome
                    |
Aggregate logical trials into benchmark metrics
```

### Select and prepare

Wharf loads the task definitions, applies the requested selection, and expands
the selected tasks into `K` attempts each. It retains the selected task IDs,
checksums, and effective run configuration so the evaluated inputs can be
identified later.

The task environment establishes the starting repository state and available
tools. Preparation may involve pulling a base image, building derived images,
and installing the exact requested agent CLI version. `wharf prebuild` warms
these caches; it does not ask an agent to solve tasks or run the graders.

### Let the agent attempt the task

The agent receives the instruction and a writable repository workspace. It
can inspect the source, make changes, and run tests available in that project.
Those development-time test runs are useful feedback, but they are **not the
final benchmark judgment**.

Benchmark-only hidden tests, reference patches, and reference solutions must
remain outside the agent's accessible workspace and environment. Ordinary
tests already present in the source repository are a different matter: they
can legitimately be available to the agent.

Wharf captures a complete patch, including committed changes, subsequent
uncommitted edits, and new non-runtime files. A clean agent exit with no patch
is `unfinished`, not proof of success.

### Verify the retained work independently

The separate verifier starts from the original source, not the agent's dirty
workspace. The retained patch is applied to that clean starting point, and the
benchmark's test entrypoint runs under an explicit timeout.

This separation matters: an agent's cached build outputs, installed tools, or
other unrecorded workspace changes should not make an otherwise incomplete
patch pass. The deliverable being evaluated is the retained patch.

In the Docker-backed Wharf path, artifacts are copied into the verifier
container under `/logs/artifacts`, and the task's `/tests/test.sh` performs
grading. Verifier evidence is copied out of `/logs/verifier` before container
cleanup. The agent and verifier do not share a writable workspace.

Harbor also supports shared verification, but that is distinct from the
separate-verifier process described here.

## 3. Functional tests and regression tests

Harbor does not prescribe one universal grading formula: the task's verifier
defines the checks and reward. DeepSWE uses functional and regression checks:

| Check | Question | Common label |
|---|---|---|
| Functional | Did the patch implement the requested behavior or fix the bug? | F2P: fail-to-pass |
| Regression | Does previously working behavior still work after the patch? | P2P: pass-to-pass |

For these binary DeepSWE tasks, a passing patch must satisfy the required
functional checks **and** preserve the required regression checks. Passing the
new tests while breaking existing behavior is not a successful solution.

Illustrative results, not results from an actual benchmark task:

| Functional checks | Regression checks | Outcome |
|---:|---:|---|
| 4/4 | 20/20 | Pass |
| 3/4 | 20/20 | Fail: requested behavior is incomplete |
| 4/4 | 19/20 | Fail: the patch introduced a regression |

Retain both sets of counts. A single pass/fail number cannot explain whether
a patch failed to solve the task, broke existing code, or did both.
Unavailable test counts are unknown, not evidence that no regressions occurred.

## 4. Reward is different from process exit status

The Harbor verifier contract writes a numerical reward to:

- `/logs/verifier/reward.txt` for a single numeric result; or
- `/logs/verifier/reward.json` for labeled numeric results.

Harbor prefers `reward.json` when both files exist. The DeepSWE/Wharf analytics
path reads the retained JSON reward and, when available, functional and
regression test counts. Do not assume Wharf supports every reward structure
accepted by the broader Harbor framework.

For the binary tasks discussed here, reward `1` means pass and reward `0`
means fail. Our analytics count a retained trial as passing when its reward
is greater than zero.

**A successful verifier process is not necessarily a passing patch.** A grader
can run successfully, write `reward: 0`, and exit normally. That is a measured
test failure, not an infrastructure failure.

Conversely, an agent's nonzero exit does not by itself establish that its
patch fails. Wharf can still verify a retained nonempty patch after an agent
error, timeout, or OOM event. Keep the agent failure evidence alongside the
verifier result rather than conflating the two.

Missing or malformed grading evidence needs investigation. A lifecycle status
alone is not evidence that every functional and regression check passed.

## 5. Attempts, retries, and scoring

`K` is the number of logical attempts per selected task. It is not the number
of infrastructure retries, and it is not the number of individual test cases.

For example, the full 113-task DeepSWE suite at `K=4` has **452 planned logical
trials**. A container or verifier retry does not increase that number.

Wharf distinguishes:

- **A completed reward-zero verification:** a measured failure; do not rerun
  the grader merely to seek a passing result.
- **A verifier timeout or external termination:** eligible for bounded
  verifier-only retry using the exact same agent patch, a fresh verifier
  environment, and separately archived failed verifier evidence.
- **Eligible agent-side infrastructure failures:** may use bounded whole-trial
  retries. These remain part of the same logical attempt; retain all execution
  evidence and aggregate the relevant agent cost and command time.
- **A clean agent exit with no patch or an agent execution timeout:** does not
  trigger another agent attempt. A retained nonempty patch can still be graded.

Use the [analytics contract](ANALYTICS.md) for reporting:

```text
PASS@1 = passing logical trials / planned logical trials
```

Failed, errored, timed-out, infrastructure-failed, cancelled, and missing
scheduled outcomes do not reduce that denominator. Repeating each task `K`
times estimates single-attempt success; it does not turn the result into
PASS@K.

**Task accuracy is separate:** a task is solved if any retained trial for it
has `reward > 0`. "Passed at least once" must not be labeled PASS@1.
The [WharfSWE regression-adjusted score](published-benchmarks.md) is another
separately labeled metric, not Harbor's universal scoring rule.

## 6. Running the process with Wharf

Run these examples from the repository root. Start a named tmux session before
long-running preparation or benchmark work so it survives a disconnect:

```bash
tmux new -s wharf-deepswe
```

Inside that session:

```bash
wharf check
wharf prebuild --tasks deep-swe/tasks -p 12 --cli-version 1.0.109
wharf run --tasks deep-swe/tasks --output-directory deep-swe/jobs \
  -n 32 -p 12 -k 4 --cli-version 1.0.109
```

The version is an explicit example pin, not a requirement to use that release.
Use the same requested CLI version for preparation and execution.

Here, `N` limits admitted concurrent trials, `P` limits shared agent/verifier
preparation, and `K` controls logical attempts per task. Preparation permits
are released before agent or verifier commands execute. Concurrency changes
how work is scheduled, not what constitutes a passing test.

For a read-only configuration plan, add `--dry-run` to `wharf run`. This does
not execute agents or verifiers and is not an end-to-end test.

Keep the suites separate:

| Suite | Tasks | Retained jobs | Exported analytics |
|---|---|---|---|
| DeepSWE | `deep-swe/tasks` | `deep-swe/jobs` | `deep-swe/data` |
| WharfSWE | `wharf-swe/tasks` | `wharf-swe/jobs` | `wharf-swe/data` |

See the [Wharf run guide](wharf.md) for configuration, cancellation, recovery,
resource policies, and retained-workspace options.

## 7. Validating the test infrastructure

Before interpreting model results, distinguish infrastructure validation from
model evaluation. The `--no-op` and `--oracle` options below are Wharf modes
for Harbor-compatible tasks, not requirements of the Harbor task format:

- **Parser/unit fixtures** check manifest loading and selection. Sanitized
  fixtures intentionally cannot exercise a real hidden verifier.
- **Smoke tasks** exercise a small end-to-end path before a large campaign.
- **No-op runs (`--no-op`)** exercise preparation and container lifecycle without an AI
  agent or grading. They are explicitly ungraded, not passing model trials.
- **Oracle runs (`--oracle`)** execute an existing trusted reference solution and grade its
  patch in a separate environment. They can reveal task, dependency, patch, or
  verifier problems without measuring model capability.

Oracle solutions must already be supplied through the authorized task source.
Do not add solutions or hidden grader material to the sanitized fixtures.
No-op and oracle results are excluded from model-score analytics exports.
See [agent-free baseline runs](wharf.md#agent-free-baseline-runs).

For any unexpected result, inspect the task identity/checksum, effective
configuration, agent logs and retained patch, verifier stdout/stderr and reward,
test counts, retry history, and terminal result. Preserve evidence before
cleanup or investigation; a failed preparation is not the same diagnosis as
a completed grader reporting a bad patch.

## Sources and implementation references

- [Harbor task overview](https://docs.harborframework.com/tasks/overview):
  format, components, trials, and framework independence.
- [Harbor verifier documentation](https://docs.harborframework.com/tasks/verifier):
  test entrypoint, reward files, and shared/separate verification.
- [Wharf task loader](../src/wharf/Task/HarborTaskLoader.cs) and
  [selector](../src/wharf/Task/TaskSelector.cs): supported task inputs and
  deterministic logical-attempt expansion.
- [Run orchestration](../src/wharf/Run/RunService.cs) and
  [verifier execution](../src/wharf/Verifier/VerifierExecution.cs): preparation,
  pristine verification, evidence retention, and retries.
- [Analytics evidence reader](../src/wharf/Reporting/RunEvidenceReader.cs):
  retained rewards and functional/regression test counts.
