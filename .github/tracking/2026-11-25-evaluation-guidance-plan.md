# Reusable evaluation guidance

## Research reused

The user approved the six-part contract discussed in this session: outcome,
evidence, hypothesis, evaluation, decision, and learning. Existing skills
already separate assumptions from findings and implementation from review:

- Discovery confidence and handoff: `.github/skills/design-thinking/SKILL.md:73`,
  `:144`.
- PRD claim classification and readiness gate:
  `.github/skills/prd/SKILL.md:112`, `:162`.
- Existing PRD testing and acceptance sections:
  `.github/skills/prd/assets/prd-template.md:109`, `:147`.
- RPI phase ownership and review: `.github/skills/rpi/SKILL.md:58`, `:91`.
- Session close and replayable log: `.github/skills/sessions/SKILL.md:128`,
  `.github/skills/sessions/assets/session-log-template.md:33`.
- Installation provenance: `.github/skills/installation-manifest.json:13`.
  Read-only inspection found stale hashes for four files being customized;
  retain recorded source provenance while updating the installed-file hashes.

Reuse this adequate research; do not repeat discovery or revise settled batch
requirements. The change adds a shared contract to existing artifacts rather
than a new skill or mandatory document workflow.

## Plan

1. [x] Add one shared evaluation contract covering software systems and domain
   learning, evidence levels, non-regression criteria, and proportional use.
2. [x] Reference it from discovery, PRD, RPI, and sessions with phase-specific
   obligations; update the existing PRD/readiness/ledger/session templates.
3. [x] Update installation provenance for the changed and added files.
4. [x] Broaden the TLT document's framing beyond agents and add skill reuse as
   the second case study, distinguishing implementation from proven benefit.
5. [x] Validate local links, manifest hashes, required template sections,
   unchanged ledger record types, and representative batch/bug-fix walkthroughs.

## Exit criteria and exclusions

The same six-part contract flows from discovery through PRD, implementation,
review, and session close. READY means ready to build, not proven effective.
Unknown baselines stay explicit; no invented numerical targets, automatic
benchmarks, release actions, or extra artifacts for trivial work.

Do not rewrite historical PRDs or results, catalog further domain findings,
modify runtime code, or change the upstream skills repository in this task.

## Changes and review

- Added `.github/skills/evaluation-contract.md` and linked it from the four
  existing skills. PRD/readiness/ledger/session-log templates carry it through
  existing artifacts. No new skill or mandatory evaluation document was added.
- The contract separates user outcome, source evidence, improvement hypothesis,
  evaluation/non-regression criteria, disposition, and domain learning.
  It distinguishes synthetic/integration/live evidence, specification readiness,
  demonstrated benefit, and release status.
- Updated installed-file hashes and local-customization metadata; preserved the
  pinned source repository/commit, recorded source hashes, and skill list.
  The previously stale hashes were for touched files, not unrelated skills.
- Renamed the TLT document's framing to continuous system improvement, preserving
  the slide at the top. Added skills reuse as Case 2 and described the
  bidirectional relationship between domain knowledge and evaluations.
- Incorporated the user's clarification: this Claude session stopped at 99%
  usage; earlier quota-failure exercises are separate reported evidence to
  attach, not proof supplied by this snapshot.

### Representative walkthroughs

These are manual guidance walkthroughs, not claims of executing a new workflow
or proving better productivity.

| Scenario | Contract application | Review result |
|---|---|---|
| Batching | Frame completing one experiment across sessions; use retained batch evidence; hypothesize continuity without duplicate work; distinguish admission checks from live quota and post-reset checks; preserve failed trials and scoring. | The PRD can be READY with defined evaluation criteria while the review accepts only evidenced claims. Earlier quota-failure records have an explicit evidence-attachment path. |
| Docker-storage bug fix | Reuse the observed false root-volume capacity failure; hypothesize checking Docker's actual filesystem; carry the targeted regression test and unchanged threshold into the existing handoff. | No new discovery cycle, numerical SLA, paid benchmark, or extra document is required. |
| Model comparison/domain learning | Record model, harness, skills, configuration, task set, and environment; compare available measurements and disclose differences. | A configuration-specific observation can revise an assumption without becoming a universal ranking or an invented accuracy/cost improvement. |

### Validation

- All four skill frontmatter blocks and existing skill/template headings remain
  present. All local links in changed guidance and the TLT document resolve.
- All 11 installation-manifest file hashes match their installed content.
  Source identity and skill list remain unchanged; new shared guidance is
  recorded as locally added.
- Existing requirements-ledger schema/record-type guidance is preserved, with
  evaluation claims mapped to existing types. BRD files are byte-for-byte
  unchanged.
- Guardrail checks confirm explicit unknowns, source-backed thresholds,
  proportional bounded-fix handling, separate evidence/readiness/release
  claims, normal paid-run authorization, and the corrected quota-history text.
- `git diff --check` passed. Documentation/guidance-only change; no runtime
  build or tests were needed, and no skills-specific test runner exists here.
- Follow-up: evaluate these local customizations on subsequent work before
  claiming improved outcomes or proposing upstream adoption.
