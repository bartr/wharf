---
title: Effective job naming review
description: Review results for model-aware smoke paths and effective CLI versions
---

## Verdict

Accepted. The implementation satisfies the requested naming and smoke path
behavior.

## Requirement Review

* Successful HydraFusion runs use the configured `hydrafusion` model segment.
* Successful runs replace `latest` with the adapter's effective CLI version.
* Smoke selection follows the exact job and attempt directories returned by
  the run.
* Setup failures retain a deterministic fallback directory using the requested
  version so failure evidence is not lost.
* The generated patch remains identical in agent, artifact, and verifier
  evidence.

## Validation

* Focused .NET tests passed: 4 passed, 0 failed, 0 skipped.
* `git diff --check` completed without errors.
* All three retained patch copies have SHA-256
  `d196f28f1147427403a9f6e835de2eff66c24ca2fa03c70bdb4cca342ab0fd20`.

## Follow-up

No defect, decision, or evidence gaps remain for this change. Existing run
directories retain their original names.
