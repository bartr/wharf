# Project Guidelines

## Benchmark execution

Always run long-running tests and benchmark jobs inside a named `tmux` session so
they survive SSH or VS Code disconnects and can be monitored or reattached.
Run the job in the foreground with stdout and stderr connected to the tmux pane;
do not wrap it in shell redirection, `nohup`, backgrounding, or an outer logging
pipeline. Follow [RUN_MONITORING.md](../RUN_MONITORING.md) for tmux lifecycle,
read-only monitoring, and provider-specific checks.
