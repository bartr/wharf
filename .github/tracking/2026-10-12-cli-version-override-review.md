# CLI Version Override Review

## Requirement review

- `--cli-version` is available on `wharf run`.
- `WHARF_CLI_VERSION` is available to normal run, dry-run, and smoke
  configuration resolution.
- Precedence is flag, environment, manifest, then `latest`.
- Existing manifest support remains as a lower-precedence source and is no
  longer required to select a CLI version.
- Existing exact requested/effective version validation is unchanged.

## Validation

- Focused configuration tests: 20 passed, 0 failed.
- Project and test assemblies built successfully.
- Help output includes `--cli-version`.
- Dry-run rendered the environment value when no flag was supplied.
- Dry-run rendered the flag value over the environment value.
- `git diff --check` passed for all task files.

## Findings

No remaining defects found. The initial review identified that the new optional
record parameter could disrupt positional callers; it was moved to the end of
the record constructor and the validation was rerun successfully.
