# User configuration plan

1. [x] Replace the run-input record/loader with strict optional user defaults in
   `src/wharf/Configuration/RunConfiguration.cs`. Preserve effective configuration
   serialization. Test missing, invalid and unsupported files and precedence.
2. [x] Remove the run option and wire new-run, dry-run, smoke and prebuild to the
   loader. Keep continuation/resume independent. Update dependent tests; prove
   removed-option rejection without output and retained-policy compatibility.
3. [x] Document supported fields, precedence, migration and historical supersession
   in README, current specs/ledger and continuous-improvement narrative.
4. [x] Run focused offline tests, Release build and formatting in named tmux;
   inspect compiled help and dry-run. Exclude the paid discovery test and retain
   truthful validation results in changes/review artifacts.

Acceptance: no production RunManifest/loader/--manifest option; user defaults
resolve consistently without secrets or side effects; explicit flags/env win;
resume preserves original settings; unrelated dataset/analytics manifests and
pinned tasks remain unchanged. Runtime tests establish bounded behavior, not
provider effectiveness or economics.

Validation completed: 212 focused Release tests, Release build and changed-file
format verification passed. Full-repository formatting found a pre-existing
IDE0008 at `src/wharf/Agent/AgentExecution.cs:1007`; left unrelated code unchanged.

## Approved startup initialization follow-up

1. [x] Add no-overwrite atomic initialization in UserConfigurationLoader and
   invoke it before CLI parsing in WharfApp; explicit failures return invalid input.
2. [x] Test no-argument/help/version/invalid-flag/dry-run startup, existing-directory
   and existing-file preservation, concurrent creators and initialization errors.
3. [x] Update current docs and contract claims for the initialization exception;
   validate focused startup/config/resume tests, Release build and changed-file format.

Follow-up validation: 226/226 focused Release tests passed, Release build and
four-file formatting verification exited 0. User directory/config are present.
