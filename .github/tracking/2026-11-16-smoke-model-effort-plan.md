# Smoke model and effort plan

## Research

- `CommandTree.cs:241` defines run model/effort options and aliases;
  `CommandTree.cs:892` creates smoke without those options.
- `SmokeService.cs:67` already uses `RunConfigurationResolver` with
  environment model/effort values, but provides no explicit overrides.
- `RunConfiguration.cs:193` implements provider defaults and flag precedence.
  Its validator checks effort centrally, but full configuration validation
  requires relative output paths; smoke intentionally uses absolute output
  paths. Reuse effort validation without changing smoke's output contract.

## Tasks

1. [x] Share run/smoke model and effort option definitions, including `-m`/`-e`,
   and forward smoke values into its existing configuration resolver.
2. [x] Reuse effort validation and test parsing, explicit-over-environment
   precedence, inherited/default behavior, and invalid effort without model
   calls or host credential access.
3. [x] Update README, run focused regression tests, Release build and
   formatting in named tmux, and record results.

Preserve CLI defaults, single-task smoke scheduling, output paths, provider
normalization, environment fallbacks, and unrelated worktree changes.
