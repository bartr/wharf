# Wharf .NET migration plan

Research:
`.github/tracking/2026-10-06-wharf-dotnet-migration-research.md`

## 1. Withdraw the Go release safely

Targets:

- Git tag `0.1.0`
- `go.mod`, `go.sum`
- `cmd/wharf/**/*.go`
- `internal/**/*.go`
- `scripts/validate-wharf.sh`

Tasks:

- [x] Verify the worktree is clean and `0.1.0` points to the withdrawn Go
      commit.
- [x] Delete only local and remote tag `0.1.0`.
- [x] Preserve all Go commits and unrelated benchmark Go fixtures.
- [x] Remove only the root Wharf Go implementation and validation files.

Exit criteria:

- No `0.1.0` tag exists locally or on origin.
- `git log` still contains the withdrawn Go commits.
- No unrelated repository directories are deleted.

## 2. Update product requirements and repository memory

Targets:

- `docs/wharf.md`
- `docs/wharf-prd.md`
- `docs/wharf-sessions.md`
- `README.md`
- `.github/copilot-instructions.md`
- `docs/session-log.md`
- `docs/RETRO.md`

Tasks:

- [x] Make .NET 10 LTS the canonical Wharf implementation language.
- [x] Replace Go-specific command/module/linker requirements with .NET
      executable, SDK, version, packaging, and validation requirements.
- [x] Move Java/.NET-after-stability language from the active non-goal into the
      historical decision record.
- [x] Preserve the session goals and acceptance behavior while updating
      implementation-specific instructions.
- [x] Record the withdrawn Go tag and migration rationale.

Exit criteria:

- No active Wharf requirement claims Go is canonical.
- Session 1 names the .NET project layout and validation command.
- The future session sequence remains behaviorally intact.

## 3. Install and pin the .NET toolchain

Targets:

- `$HOME/.dotnet`
- `global.json`

Tasks:

- [x] Install .NET 10 SDK locally without modifying unrelated system packages.
- [x] Record the exact installed SDK feature band in `global.json`.
- [x] Verify `dotnet --info`, restore, and a clean SDK console build.

Exit criteria:

- `dotnet --version` resolves the pinned SDK through `global.json`.
- SDK installation is reproducible from documented commands.

## 4. Scaffold the .NET 0.1.0 CLI foundation

Targets:

- `src/wharf/`
- `tests/wharf.Tests/`
- `Wharf.sln` or solution equivalent
- `Directory.Build.props`
- `.editorconfig`

Tasks:

- [x] Create one SDK-style executable project and one test project.
- [x] Keep `Program.cs` as a thin process boundary.
- [x] Add typed configuration, explicit environment precedence, stable exit
      codes, one version source, structured text/JSON logging, redaction,
      quiet mode, and verbosity.
- [x] Register the complete v1 command tree and explicit non-successful
      operational placeholders.
- [x] Implement version output, linker/property release override equivalent,
      help, Bash/Zsh completion, and unknown-input rejection.
- [x] Add unit tests and compiled CLI proofs.

Exit criteria:

- Replacement .NET 0.1.0 behavior matches the approved Go session-1 contract.
- No Go runtime or project dependency remains.
- Tests exercise the actual compiled executable.

## 5. Validate and review

Targets:

- `scripts/validate-wharf.sh` or a .NET-native validation command
- `.github/tracking/2026-10-06-wharf-dotnet-migration-changes.md`
- `.github/tracking/2026-10-06-wharf-dotnet-migration-review.md`

Tasks:

- [x] Provide one documented format/analyze/test/build command.
- [x] Run restore, format check, analyzers, unit tests, and compiled CLI proofs.
- [ ] Run the relevant .NET test suite in a named tmux session if long-running.
- [ ] Record migration proof and all residual follow-ups.

Exit criteria:

- Validation is green.
- Review confirms only the Wharf surface changed.
- The replacement 0.1.0 is ready to commit and release.

## 6. Release replacement 0.1.0

Tasks:

- [ ] Commit migration and replacement foundation with the required trailer.
- [ ] Push the implementation branch.
- [ ] Fast-forward `main` and push it.
- [ ] Create and push the replacement annotated `0.1.0` tag.
- [ ] Complete session log, retro, and repository memory.

Parking lot:

- Sessions 2-20 behavior is not implemented in this migration session.
- Do not add a second Go implementation.
- Do not delete benchmark fixtures or unrelated historical documentation.
