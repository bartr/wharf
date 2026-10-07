# Session retrospectives

Append one bullet at the close of every Wharf implementation session. Record
what was surprising and what should be done differently in the next session.

- Session 1 (`0.1.0`): Cobra's automatic version flag unexpectedly allowed
  trailing unknown input to succeed, and parsing lower-precedence environment
  values before changed flags weakened the precedence contract. Next session,
  test invalid lower-precedence values and mixed global-flag/argument cases as
  soon as each configuration layer is introduced.
- Release close (`0.1.0`): The independent code review caught integration
  defects that package-level tests missed, especially logger wiring and Cobra
  help error propagation. Keep compiled CLI boundary probes in every release
  proof, not only unit and race tests.
- Migration restart (`0.1.0` withdrawn): Replacing the short-lived Go
  implementation before the first supported release avoids maintaining two
  languages. Keep the .NET process boundary and compiled CLI probes as the
  first proof of the restarted release.
- Session 2 (`0.2.0`): A standalone task prompt produced a clearer enterprise
  boundary than directing the agent through the repository session workflow.
  Keep future test prompts self-contained, while retaining implementation
  evidence separately in `.github/tracking/`.
- Session 3 (`0.3.0`): Custom identifier structs initially compiled but did
  not round-trip through JSON deserialization, which surfaced only when replay
  materialized a summary. Add serialization round-trip tests immediately when
  introducing durable contracts, and prefer explicit converters over relying on
  constructor inference.
- Session 4 (`0.4.0`): Harbor task manifests vary in whether the task-level
  description is populated, while metadata consistently carries the display
  description. Preserve the source shape but use the metadata description as
  the explicit fallback, and keep reference material out of vendored fixtures.
- Session 5 (`0.5.0`): A real host probe exposed that a machine can have a
  healthy Docker daemon while still failing workload capacity, such as a low
  inotify limit. The first implementation also conflated inotify instances
  and watches; validate related kernel settings independently and keep
  platform, daemon, and capacity outcomes separate so an unsupported-host
  override cannot hide a safety failure.
- Session 15 (`0.15.0`): Scoring is safest when every measurement carries an
  explicit state. Keep missing and invalid evidence distinct from measured
  zero, and preserve the documented task-equal weighting in fixtures.
- Session 5 follow-up: WSL2 can run with a custom kernel that removes the
  standard Microsoft markers. Support an explicit, clearly labeled WSL
  classification without weakening any safety probe.
- Session 6 (`0.6.0`): Rate-limit handling needs two distinct forms of
  evidence: append each 429 immediately so interruption cannot erase it, then
  atomically refresh the per-image aggregate with the eventual outcome.
  Continue injecting delay and randomness at the boundary, and keep registry
  pressure inside the preparation service so Session 7 can place the whole
  pipeline behind one `B` gate without inventing a second semaphore.
- Session 7 (`0.7.0`): A shared build task must outlive an individual caller's
  cancellation; otherwise one cancelled waiter can poison every duplicate
  request. Keep cancellation at the wait boundary and remove failed
  operations from the in-flight map so recovery is explicit and testable.
- Session 8 (`0.8.0`): Secret safety is easiest to preserve when credential
  values never enter the adapter's data model at all. Pass source metadata,
  validate it at the boundary, and make redacted command output structurally
  incapable of containing the credential.
- Session 9 (`0.9.0`): Isolation is clearest when mount validation happens
  before any runner is invoked. Keep fake-agent tests at that boundary, and
  persist an unfinished result before rejecting an empty patch so debugging
  evidence survives failed execution.
- Session 10 (`0.10.0`): Keeping workspace creation, patch application, and
  verifier execution injectable makes the single-task proof testable without
  pretending that a local unit test is a full Docker integration. Preserve
  the manual release gate until the real environment is exercised.
- Session 11 (`0.11.0`): Logical-attempt banking belongs at admission/result
  boundaries, not in a later aggregate pass. Stable ordering and immediate
  banking make duplicate suppression and cancellation behavior observable.
- Session 12 (`0.12.0`): Resource release must happen before retry evidence is
  written; otherwise a slow telemetry sink can falsely hold a phase slot and
  undermine work-conserving behavior. Keep infrastructure retry identity
  separate from logical-attempt identity.
- Session 13 (`0.13.0`): Recovery is safest as a pure plan over validated
  events plus an explicit live-resource snapshot. Report orphan resources
  separately so resume never hides cleanup or mutates retained evidence.
- Session 14 (`0.14.0`): Cleanup safety is easiest to prove by making
  ephemeral-resource IDs an explicit input and never deriving them from the
  retained evidence tree. Keep status as a pure projection so operators can
  inspect runs without changing recovery state.
- Session 16 (`0.16.0`): Keeping report rendering pure over a versioned
  data-only input makes deterministic regeneration straightforward. Keep CLI
  file handling thin and test the exact JSONL line shape separately.
- Session 17 (`0.17.0`, unreleased): A local fixture can prove artifact and
  report wiring without pretending that host qualification passed. Keep
  platform overrides separate from Docker and capacity gates.
- Session 18 (`0.18.0`): Packaging needs explicit unsigned labeling and a
  version override that is shared by the published executable and Debian
  metadata. Keep host mutation isolated behind small sudo wrappers.
