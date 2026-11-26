# Remove unused commands: research

## Outcome and baseline

User request: remove code/docs for status, stop, report and profile, then inspect
analytics. This is a CLI simplification, not a measured performance hypothesis.
Prior direct command inspection is reused rather than repeated.

- CommandTree.cs:76 wires status/stop to a placeholder returning failure
  (:1179). Report (:760) only renders prepared ReportInput; profile (:1100)
  recommends limits from two hard-coded profiles and reported CPU/RAM.
- CommandTree.cs:500 resumes real retained work; :615 provides read-only
  progress; :817 registers analytics export and manifest. Preserve these.
- ReportRenderer.cs:7 and ReportContracts.cs:5 have no production consumers
  outside the removed command; ReportRendererTests covers only that renderer.
- Qualification/HostProfiles.cs:50 is used only by the profile command and
  HostProfileTests. Its qualification DTOs share that exclusive file.
- Operations/RunOperations.cs:7 and :44 implement unused status/stop services.
  OperationsContracts.cs:6/:57/:62 holds their exclusive contracts. Notification
  services/sinks share these files but are separate behavior; keep them and their
  durable notification test. Production Ctrl+C cancellation/resource cleanup is
  separate and must remain unchanged.
- docs/wharf.md:428 and wharf-prd.md:162 advertise the retired commands;
  wharf.jsonl:214-216 repeats them. Current report/automatic-profile promises
  must be narrowed to retained analytics and operator-controlled limits.
- docs/wharf-sessions.md:3 and release-qualification.md:41 are historical plans/
  qualification evidence. Mark superseded contracts rather than rewrite past
  successful tests, pinned tasks, jobs, exports or archived tracking records.

## Recommendation and boundaries

Remove four registrations/handlers, the placeholder factory, exclusive report/
profile modules/tests, and only status/stop portions of Operations. Add actual
process rejection/help coverage. Preserve notifications, progress, resume,
audit/scoring, smoke output, cancellation and ownership-based cleanup.

Inspect analytics evidence reading and aggregation separately after removal.
Do not change scoring or launch paid runs; missing live evidence must stay
explicit. No quantified benefit, release approval or historical rewrite.
