# 1.0 Release Review Changes

## Runtime fixes

- Configured run credentials now replace the machine-specific credential-file
  fallback and cross the Docker boundary only through the transient process
  environment.
- Run-level setup, agent, and verifier timeout overrides now take precedence
  over task values and defaults. Unsupported `reportSeconds` fails validation
  instead of being silently ignored.
- Terminal trial results use atomic replacement.
- New runs retain an atomic, secret-free `run-plan.json` with exact selected
  task identities and effective configuration.
- Resume restores selected tasks and immutable policy, validates terminal
  result identity, keeps dry-run planning read-only, and archives malformed
  evidence only when execution resumes.
- Ctrl+C requests cancellation without racing container deletion against
  evidence capture. Explicit global cleanup no longer matches containers by
  generic names.
- Owned verifier workspaces are deleted after verification and when preparation
  fails; host verifier process trees are killed and reaped on external
  cancellation.

## Tests

- Added credential-source and timeout-precedence tests.
- Added filtered-run, policy-preservation, secret-redaction, and malformed
  terminal-result recovery tests.
- Added verifier workspace cleanup and external-cancellation tests.
