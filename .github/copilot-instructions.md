# Project Guidelines

## Benchmark execution

Always run long-running tests and benchmark jobs inside a named `tmux` session so
they survive SSH or VS Code disconnects and can be monitored or reattached.
Run the job in the foreground with stdout and stderr connected to the tmux pane;
do not wrap it in shell redirection, `nohup`, backgrounding, or an outer logging
pipeline. Follow [RUN_MONITORING.md](../RUN_MONITORING.md) for tmux lifecycle,
read-only monitoring, and provider-specific checks.

Wharf documentation lives under `docs/`. Session RPI artifacts belong under
`.github/tracking/`; keep those artifacts with the repository so decisions and
validation evidence survive context resets.

Session 2 established strict `1.0` JSON run manifests and an explicit
flag-over-environment-over-manifest-over-default precedence contract.
`wharf run --manifest <path> --dry-run` validates and renders a deterministic
plan without creating output directories, run state, Docker resources, or
network activity. Manifest and `WHARF_API_KEY`, `WHARF_TOKEN`, and
`WHARF_PASSWORD` values are redacted in rendered output. Keep configuration
validation before any output-directory creation.
