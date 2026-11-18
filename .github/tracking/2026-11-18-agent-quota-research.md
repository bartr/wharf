# Agent quota boundary research

The supplied requirements define the public contract and scope. `AgentExecution.cs:28`
accepts runner results and writes typed evidence, but only OOM/timeouts are typed.
`AgentExecution.cs:260` starts the actual agent exec after reading container HEAD.
`AgentExecution.cs:459` redirects output entirely into container files, preventing
live notification. `DockerAgentProcess.RunAsync` reads whole streams at completion.
Provider usage parsers consume JSONL and do not recognize subscription exhaustion.

Recommended approach: recognize only provider error/rate-limit envelope fields and
explicit exhaustion messages; normalize reason and strictly validate reset metadata.
Mirror provider output through tee while retaining container logs, and add optional
line callbacks to the process transport. Notify at the exec start boundary and
deduplicate live/result notifications in the execution service.

Do not treat generic 429, transient rate limiting, overload, tools' output, or
ordinary assistant messages as subscription quota. Do not change batch orchestration.
