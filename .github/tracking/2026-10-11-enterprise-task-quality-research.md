---
description: "Evidence-based classification of the 14-task enterprise run and instruction leakage audit"
author: "Wharf"
ms.date: 2026-10-11
ms.topic: "concept"
---

## Research question

The first .NET enterprise-task run passed 13 of 14 tasks. This review asks:

* Is the failed trial primarily an infrastructure, instruction, or model failure?
* Do the 13 passing instructions disclose too much of their hidden evaluator?
* What task-design approach preserves solvability without turning the prompt into
  a test oracle?

This is a read-only analysis of the retained run. No benchmark was rerun and no
task, evaluator, solution, or run evidence was changed.

## Result

The failed `wharf-image-pull` trial is primarily an
**instruction/evaluator contract mismatch**, not an infrastructure failure and
not persuasive evidence of a model capability failure.

The other 13 tasks disclose too much relative to what their hidden tests
measure. Exact public API names can be legitimate compatibility requirements,
but almost every hidden assertion is restated in the instruction. The tests
then verify type existence, method existence, or literal strings rather than
the production behavior described by the task. A 13/14 pass rate therefore
overstates implementation capability.

## Failed-trial evidence

### Infrastructure was healthy

The agent process completed with exit code 0 after 908,667 milliseconds, with
no retained error type or error message
([agent result](../../jobs/copilot-gpt-5.6-luna-max-1.0.95/all-1/wharf-image-pull__854e3b29/agent/result.json)).
The verifier also completed normally in 15,520 milliseconds without timing out.
It applied the 41,830-byte patch, passed the regression test, and passed two of
three feature tests
([trial result](../../jobs/copilot-gpt-5.6-luna-max-1.0.95/all-1/wharf-image-pull__854e3b29/verifier/trial-result.json)).

Those facts rule out container startup, patch capture, patch application,
timeout, test-runner, and baseline-regression failures.

### The rejected implementation was reasonable under the instruction

The instruction requires invalid retry options to be rejected explicitly, but
does not state whether rejection must occur during construction or only when
`Validate()` is called
([instruction](../../enterprise-tasks/wharf-image-pull/instruction.md)).

The model implemented both constructor-time validation and a public
`Validate()` method. The constructor calls `Validate()` immediately
([model patch](../../jobs/copilot-gpt-5.6-luna-max-1.0.95/all-1/wharf-image-pull__854e3b29/agent/patch.diff)).
The reference solution instead permits construction with invalid values and
defers rejection until the caller invokes `Validate()`
([reference solution](../../enterprise-tasks/wharf-image-pull/solution/solution.patch)).

The hidden test constructs an invalid object outside its exception assertion,
then expects the later reflective `Validate()` invocation to throw
([hidden test](../../enterprise-tasks/wharf-image-pull/tests/test.patch)).
Constructor-time rejection therefore fails the test even though it satisfies
the stated requirement more eagerly.

### Model contribution

The model could not find a reachable historical implementation and explicitly
reported that it was deriving the API
([trajectory](../../jobs/copilot-gpt-5.6-luna-max-1.0.95/all-1/wharf-image-pull__854e3b29/agent/trajectory.jsonl)).
It produced a 1,323-line patch versus the 588-line reference patch, which shows
substantial overengineering. That increased compatibility risk. It still built
the solution, passed 16 available tests, and created behavior probes for local
reuse, bounded retry, pressure decay, coalescing, telemetry, verbosity, and
invalid-option rejection.

The overengineering is a model-quality concern, but it did not cause the
observed assertion failure. The decisive issue was an unstated validation
lifecycle required by the evaluator.

## Instruction leakage audit

All 14 hidden patches contain only three or four feature tests. The passing 13
fall into three groups.

| Tasks | Leakage | Evidence |
|---|---|---|
| `wharf-audit`, `wharf-execution`, `wharf-host-profile`, `wharf-operations`, `wharf-recovery`, `wharf-reporting`, `wharf-scheduler`, `wharf-smoke` | Critical | Each instruction lists the exact namespace and production type names. The hidden tests primarily assert those same names exist, that one named type has any public member, and that two existing configuration types remain. The prompt supplies nearly the complete answer while giving too little behavioral detail to reconstruct the historical release. |
| `wharf-packaging` | Critical | The instruction gives all three exact script paths and the exact literals `dpkg-deb --build`, `sha256sum`, `SBOM`, `exec sudo`, and `previous.deb`. The hidden tests search for those paths and literals directly. |
| `wharf-verifier` | Critical | The instruction enumerates required records, constructor signatures, interfaces, exception names, schema value, validation cases, and the required service overload. The four hidden tests mirror those declarations and two stated validation examples. |
| `wharf-agent`, `wharf-copilot`, `wharf-derived-image` | High | The instructions include meaningful behavior, but they also enumerate nearly every type, property, method, enum count, and schema value asserted by the hidden tests. The evaluators do not test most of the described runtime behavior. |
| `wharf-image-pull` | High and inconsistent | The instruction enumerates almost every reflected type and schema assertion, but omits the validation lifecycle that the only failing behavior test requires. |

The test patches demonstrate the mirroring directly:

* The agent evaluator reflects the exact agent types, schema version, validation
  method, and properties named in the instruction
  ([test patch](../../enterprise-tasks/wharf-agent/tests/test.patch)).
* The Copilot evaluator reflects the exact adapter types, three-value enum,
  validation method, exception, and invocation properties named in the
  instruction
  ([test patch](../../enterprise-tasks/wharf-copilot/tests/test.patch)).
* The packaging evaluator searches for the exact paths and command fragments
  supplied by the prompt
  ([test patch](../../enterprise-tasks/wharf-packaging/tests/test.patch)).
* The verifier evaluator checks the exact API and validation examples supplied
  by the prompt
  ([test patch](../../enterprise-tasks/wharf-verifier/tests/test.patch)).
* The eight generic release-boundary evaluators repeat the same reflection-only
  pattern. For example, the scheduler evaluator checks the four disclosed
  scheduler names and preserved configuration types
  ([test patch](../../enterprise-tasks/wharf-scheduler/tests/test.patch)).

## Recommended approach

Redesign each instruction and hidden evaluator as a pair.

1. Keep user-observable requirements in the instruction: release context,
   invariants, failure behavior, evidence formats, determinism, and integration
   boundaries.
2. Include exact names and signatures only when they are genuinely part of a
   compatibility contract. Do not treat those disclosed facts as meaningful
   hidden coverage.
3. Replace reflection-only hidden tests with behavioral tests that instantiate
   the disclosed API and verify state transitions, ordering, cancellation,
   retries, persistence, redaction, determinism, and explicit failures.
4. Add negative and adversarial cases not enumerated verbatim in the prompt,
   but inferable from the stated invariants.
5. Make hidden tests accept behaviorally equivalent implementations unless an
   exact historical observable is explicitly required.
6. For `wharf-image-pull`, accept invalid-option rejection from either the
   constructor or `Validate()`. If deferred validation is itself the historical
   contract, state the exact constructor and validation lifecycle in the public
   API requirements.

This keeps tasks solvable because the required behavior remains explicit. It
removes the current shortcut where copying identifiers and literals is enough
to pass.

## Suggested priority

Start with the eight generic release-boundary tasks. Their instructions are
simultaneously under-specified for real implementation and over-specified for
their current reflection tests. They provide the weakest benchmark signal.

Next revise packaging and verifier, where the instruction-to-assertion mapping
is nearly one-to-one. Finish with agent, Copilot, derived image, and image pull,
which already have stronger behavioral narratives and need mainly evaluator
depth plus removal of incidental lifecycle assumptions.

## Alternatives not recommended

* Do not add the exact `wharf-image-pull` hidden-test sequence to the prompt
  without changing the test. That would turn the final undisclosed assertion
  into another disclosed recipe.
* Do not remove all public API names while retaining reflection tests. The task
  would become guesswork because the historical source is unavailable in the
  task environment.
* Do not interpret 13/14 as model-level PASS@1 evidence until the evaluators
  exercise the behavior claimed by the instructions.

## Evidence limits

This review did not execute the reference solutions, mutate task fixtures, or
run a second model. It classifies the observed failure and benchmark leakage
from retained agent, patch, and verifier evidence. A revised benchmark still
needs a clean qualification run before model comparisons are reported.
