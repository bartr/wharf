# Remove container CPU controls: plan

1. [x] Remove configuration/flag/environment support, allocator and policy,
   request/result CPU properties and Docker affinity arguments. Keep other
   resource/evidence/phase controls and legacy JSON readability.
2. [x] Delete CPU-only tests and adapt adjacent tests. Verify removed flag/config/
   environment rejection, no CPU restrictions in all three container paths,
   and byte-preserved legacy retained policy loading.
3. [x] Update README, current continuous-improvement decision, retrospective
   supersession and monitoring metadata. Leave historical tracking/task/evidence
   payloads untouched and clearly document resume's intentional policy change.
4. [x] Run the combined focused offline Release suite, Release build, changed-file
   formatting and whitespace/JSON checks in named tmux. Exclude paid discovery;
   retain failures and results in changes/review artifacts.

Acceptance: no runtime CPU allocator/limits/probe; explicit obsolete-input
failures; default/new/resumed containers unrestricted by Wharf CPU affinity;
N/P, memory/PID, evidence and scoring behavior preserved. Keep/revise decision
is based on bounded regression evidence, not a measured throughput improvement.

Final evidence: 221 focused Release tests and Release build passed. Changed-file
format verification found only the pre-existing IDE0008 at AgentExecution.cs:1003
(previously :1007); excluding that diagnostic passed. Migration errors, source
scope, requirements JSONL, monitoring-script syntax and whitespace were verified.
