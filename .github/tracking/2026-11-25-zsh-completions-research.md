# Zsh completions: research

## Outcome and baseline

Implement `wharf completions zsh` as usable shell completion, not a
success-shaped placeholder. Existing `CommandTree.cs:1046-1063` registers
singular `completion` and returns a comment for both Bash and Zsh.
`WharfApp.cs:16-36` initializes user configuration and invokes the command tree;
preserve that startup behavior and keep completion queries away from execution.

## Evidence and recommendation

- `src/wharf/wharf.csproj:19` already references System.CommandLine 2.0.0.
  Its installed XML API documentation at `System.CommandLine.xml:467-479`
  documents the native `[suggest]` directive: normal command handling is
  short-circuited and suggestions are newline-delimited.
- Offline probes against the existing CLI confirmed
  `[suggest:8] "wharf ru"` returns `run`, and
  `[suggest:12] "wharf run --"` returns current run options. Supplying a cursor
  beyond the text loses prefix filtering; use the actual cursor position.
- Zsh's cursor counts characters, while the parser expects UTF-16 positions.
  A builtin Zsh character-code loop produced four UTF-16 units for a three-
  character string containing one supplementary character. No dependency is
  needed for conversion.
- `/usr/bin/zsh` is available. Existing subprocess tests in
  `tests/wharf.Tests/WharfAppTests.cs:126-160` isolate HOME, capture streams, and
  verify no run evidence is created.

Emit a sourceable Zsh function that queries the native directive using the
buffer prefix up to the cursor and its UTF-16 length, offers parser suggestions
through `_describe`, and falls back to `_files` when no suggestions exist.
Propagate query errors without suppressing stderr. Use canonical `completions`
with the existing singular spelling retained as an alias. Reject unsupported
shells explicitly; Bash is not implemented in this change.

## Criteria and exclusions

Verify generated script syntax and execute the function in an isolated Zsh
harness against the real CLI: commands, nested commands, option prefixes,
shell values, cursor-in-middle behavior, Unicode, and file fallback.
Check no evaluations/Docker activity or run-output creation occurs. Preserve
existing invocation initialization and singular Zsh compatibility.

Do not install into the user's shell, edit shell startup files, implement Bash
or an inline-autosuggestion plugin, run paid evaluations, or change benchmark
evidence. This establishes completion behavior, not measured productivity.
