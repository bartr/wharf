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

Session 3 established a filesystem-backed `1.0` state contract. Each run
stores its effective configuration, an append-only `events.jsonl` stream, and
an atomically replaced `summary.json`. Event IDs are idempotent, sequence
numbers are contiguous, malformed or mismatched records fail explicitly, and
logical attempts must remain distinct from infrastructure retries. Keep
read-only summary access independent from the per-run writer lock.

Session 4 established Harbor task loading as a library boundary. Schema `1.3`
manifests are parsed strictly, task checksums use sorted relative paths and
content, and selection is deterministic across language, include, exclude,
shard, seeded-sample, and K-attempt expansion options. Vendored fixtures under
`tests/fixtures/harbor/` are sanitized: never add solutions, hidden tests,
reference patches, or grader-only material to the Wharf checkout.
