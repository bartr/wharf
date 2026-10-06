# Project Guidelines

## Benchmark execution

Always run long-running tests and benchmark jobs inside a named `tmux` session so
they survive SSH or VS Code disconnects and can be monitored or reattached.
Run the job in the foreground with stdout and stderr connected to the tmux pane;
do not wrap it in shell redirection, `nohup`, backgrounding, or an outer logging
pipeline. Follow [RUN_MONITORING.md](../RUN_MONITORING.md) for tmux lifecycle,
read-only monitoring, and provider-specific checks.

## Wharf implementation

Wharf is an enterprise-focused .NET replacement for Pier. The authoritative
requirements are in [wharf.md](../wharf.md), the machine-readable ledger is
[wharf.jsonl](../wharf.jsonl), and the human PRD is
[wharf-prd.md](../wharf-prd.md). Use [wharf-sessions.md](../wharf-sessions.md)
as the implementation sequence and [session-log.md](../session-log.md) plus
[RETRO.md](../RETRO.md) for session closeout.

Wharf is a .NET 10.0.401 SDK-style application pinned by `global.json`.
Use `./scripts/validate-wharf.sh` to format, analyze, test, and build all
Wharf .NET code.

Keep `src/wharf/Program.cs` as the thin process entry point. Reusable
foundations are organized by responsibility under `Command`, `Configuration`,
`Logging`, `Versioning`, and `ExitCodes`; do not add catch-all utility
namespaces or a second implementation project.

The Wharf version has one source value. Release builds override it with
`dotnet build -p:WharfVersion=<semver>`.

Global environment variables are explicit: `WHARF_LOG_FORMAT`, `WHARF_QUIET`,
and `WHARF_VERBOSE`. Flags override environment values, which override built-in
defaults. Session 2 adds the manifest layer.

Exit codes are stable: success 0, internal failure 1, invalid input 2,
unsupported host 3, blocked run 4, and partial run 5. Only the process boundary
prints command errors.

The Go `0.1.0` tag was withdrawn before the replacement release. The .NET
implementation will recreate `0.1.0` after its validation passes. Operational
commands remain explicit non-successful placeholders until their later sessions.
Session 2 starts with manifest-aware configuration and `--dry-run`.

Do not pull manifest parsing, Docker integration, host checks, Harbor loading,
Copilot execution, or run-state creation into session 1. Run the session frame,
RPI loop, fit check, tests, merge, tag, memory update, session log, and retro
close ritual defined by the sessions methodology.
