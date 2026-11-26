# Zsh completions: review

Reviewed against the [research](2026-11-25-zsh-completions-research.md),
[plan](2026-11-25-zsh-completions-plan.md), and
[implementation evidence](2026-11-25-zsh-completions-changes.md).

## Decision: keep

The requested command is implemented and the generated script works in real
interactive Zsh. Native parser suggestions avoid catalog drift and do not
invoke benchmark handlers. Cursor-prefix handling, UTF-16 conversion, aliases,
file fallback, and error propagation have focused regression evidence.

Final validation: 60 focused tests passed; Release/Debug builds and changed-file
formatting passed; actual interactive Tab completion passed for commands,
nested commands, a flag, paths, and quoted filenames. No evaluation output,
paid calls, or benchmark-evidence changes occurred.

The initial test-only platform warning is recorded and resolved. Existing
configuration initialization is preserved. No blocking defect remains in the
reviewed scope, and no full-repository formatting or release qualification is
claimed.

## Follow-ups and limits

- Bash completion and inline-autosuggestion/typeahead remain separate work.
- Value completion depends on hints registered in the current command tree;
  arbitrary model names and version numbers are not discovered from providers.
  Empty successful suggestions use the shell's filename fallback.
- Learning/productivity benefits are not measured by these functional checks.
- Shell startup files were not edited; operators opt in with the documented
  source command after compinit.
