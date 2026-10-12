# HydraFusion Default Label Review

## Outcome

Accepted. HydraFusion now has one effective effort identity, `default`, matching
the adapter behavior that omits `--reasoning-effort`.

## Checks

- Resolved configuration records HydraFusion effort as `default`.
- Job directories defensively normalize HydraFusion to `default`.
- Non-HydraFusion configured effort remains unchanged.
- Invalid HydraFusion effort remains invalid and is rejected by the existing
  validator rather than silently normalized.
- Both README rows use only HydraFusion, version, and the applicable run suffix.
- Focused test suite passed all 33 tests.

## Follow-ups

Historical job archives and analytics filenames retain `max` for evidence
stability, as planned.
