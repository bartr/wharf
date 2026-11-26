# Zsh completions: plan

Based on [research](2026-11-25-zsh-completions-research.md). Use the existing
native suggestion directive; no new parser, dependencies, or execution API.

1. [x] Replace `CommandTree.cs:1046-1063` with canonical `completions`, singular
   alias, Zsh shell hint, explicit unsupported-shell error, and sourceable
   function using native suggestions, UTF-16 positions, and file fallback.
2. [x] Extend existing invocation tests and add focused Zsh subprocess tests.
   Check aliases, unsupported shells, commands/nested commands/options, shell
   values, Unicode and cursor prefixes, file fallback, and no run evidence.
3. [x] Document compinit/source setup in README; synchronize source CLI contract,
   PRD, and existing ledger records/hash. Distinguish delivered Zsh tab
   completion from planned Bash/typeahead. Preserve historical records.
4. [x] Run focused Release tests, Release/Debug builds, and changed-file
   formatting in named tmux. Independently load generated output in real Zsh
   and exercise interactive Tab completion with isolated HOME.
5. [x] Record changes/review, verify whitespace and scope, remove owned scratch,
   close completed owned tmux, and leave uncommitted.

Acceptance is executable completion behavior and no execution side effects.
Do not claim measured productivity or live provider effectiveness.
