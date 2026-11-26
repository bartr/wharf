# Zsh completions: changes

Implements the [research](2026-11-25-zsh-completions-research.md) and
[plan](2026-11-25-zsh-completions-plan.md).

## Delivered

- `wharf completions zsh` emits sourceable Zsh completion; `completion` remains
  an alias. Unsupported shells now fail explicitly instead of printing a
  success-shaped placeholder.
- The function queries System.CommandLine's existing native suggestion
  directive, so commands, nested commands, flags, and registered value hints
  follow the installed CLI. No duplicate command/option catalog was added.
- Queries use only the buffer prefix through the cursor, with Unicode converted
  to the parser's UTF-16 position convention. Query errors retain stderr and
  nonzero status; only an empty successful response falls back to `_files`.
- README documents compinit/source setup and persistence after initialization.
  Source requirements, PRD, and existing ledger records/hash are synchronized.
  Bash and inline typeahead remain explicitly unimplemented.
- Existing startup configuration behavior and historical records are preserved.
  No dependencies, benchmark payloads, raw jobs, or exports were changed.

## Validation evidence

Long-running checks ran foreground in owned tmux session
`wharf-zsh-completions`, without outer redirection or logging pipelines.

- Initial focused tests: 59/59 succeeded, with a new CA1416 warning on test
  executable permissions. A platform guard fixed the warning; an additional
  error-propagation test was added.
- Final focused Release tests: **60/60 succeeded**, zero failures/skips.
  Includes WharfApp and ZshCompletion tests; excludes the paid
  `RunCommandExecutesTaskDiscovery` path.
- Release solution and Debug executable builds: exit 0, no warnings.
- Three changed C# files: formatting and verify-no-changes exit 0.
- Generated scripts for plural/singular commands passed `zsh -n`.
- Real Zsh harnesses checked native command/nested-command/flag/value
  suggestions, supplementary Unicode, a cursor before later arguments,
  file fallback, and query failure without success-shaped fallback.
- Independent interactive PTY testing loaded the generated script through real
  compinit and pressed Tab. It completed `run`, `analytics export`,
  `--recompute`, a filesystem path, and a quoted filename containing spaces.
  Captured shell buffers matched the expected text exactly. Completed commands
  were never executed and no evaluation output was created.
- The actual user `wharf` command resolves to the refreshed Debug executable
  and emits the new script.
- Ledger: 394 unique records, matching source hash, and correct source lines
  for CON-025 and REQ-048. Whitespace/scope checks passed.
- Owned interactive-check scratch was removed, its temporary home/processes
  were cleaned, and the finished owned tmux session was closed. Unrelated
  sessions were untouched. Changes remain uncommitted.

No paid evaluation or provider call was launched. These checks establish
completion functionality, not a measured productivity benefit or package
release qualification.
