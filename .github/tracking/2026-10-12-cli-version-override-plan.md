# CLI Version Override Plan

1. [x] Add CLI version to run overrides and configuration precedence.
2. [x] Wire `--cli-version` and `WHARF_CLI_VERSION` through normal and dry-run
   execution, and expose the environment variable to smoke resolution.
3. [x] Add focused precedence tests and update CLI documentation.
4. [x] Run focused configuration tests and validate dry-run output for flag and
   environment inputs.
