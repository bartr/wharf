# Wharf .NET migration research

Date: 2026-10-06

## Decision under evaluation

Replace the short-lived Go Wharf implementation with one canonical .NET
implementation rather than maintaining two language implementations. The
released Go `0.1.0` tag is intentionally being withdrawn while the product is
still at its first foundation release.

## Evidence

- The current repository is clean on `main` at commit `74a86cb`, with local and
  remote `main`, `v0.1.0`, and tag `0.1.0` all pointing to the Go foundation.
- The requirements currently mandate Go and Go-specific structure and tooling:
  `docs/wharf.md:69-81`, `docs/wharf-prd.md:27-34`, `docs/wharf-prd.md:273-285`,
  `.github/copilot-instructions.md:14-42`.
- The requirements explicitly defer Java and .NET enterprise suites until the
  DeepSWE path is stable: `docs/wharf.md:41`, `docs/wharf.md:131`, `docs/wharf-prd.md:74`.
- Session 1 is the only completed implementation session. Sessions 2-20 are
  still a requirements-driven implementation sequence, so changing the
  language now avoids a large dual-maintenance cost.
- The session plan's meaningful DeepSWE validation surface spans task loading,
  adapter execution, agent/verifier isolation, scheduling, recovery, scoring,
  reporting, smoke, scale, and release qualification
  (`docs/wharf-sessions.md:210-234`, `docs/wharf-sessions.md:325-419`,
  `docs/wharf-sessions.md:423-598`, `docs/wharf-sessions.md:603-737`).
- The current environment has no `dotnet` executable installed.

## Runtime recommendation

Use .NET 10.0 LTS and the SDK-style console application template. As of this
date, .NET 10 is the supported LTS line with support through November 2028.
Install the SDK locally under `$HOME/.dotnet` so the migration does not modify
the host package manager or require root access. Add a checked-in
`global.json` to pin the selected SDK feature band once installation reports
the exact available SDK.

## Recommended project structure

```text
src/wharf/
  Program.cs                 thin process boundary
  wharf.csproj
  Command/
    RootCommand.cs           command tree and execution wiring
    OperationalCommands.cs
  Configuration/
    WharfOptions.cs          typed settings and explicit environment parsing
  Logging/
    WharfLogger.cs           console/durable sinks and redaction
  Versioning/
    WharfVersion.cs          one version source
  ExitCodes/
    WharfExitCode.cs         stable process codes
tests/wharf.Tests/
  Command/
  Configuration/
  Logging/
  Versioning/
  ExitCodes/
  TestData/
```

Keep reusable code under `src/wharf`, keep `Program.cs` thin, and avoid
premature public libraries. A single executable project plus one test project
is the smallest maintainable structure for 0.1.0. Do not introduce a separate
class library, source generator, hosted service, or dependency-injection
container until a later session requires it.

Use built-in .NET facilities first:

- `System.CommandLine` only if its stable API is available in the selected
  SDK/package version; otherwise use a small Wharf-owned parser boundary.
- `Microsoft.Extensions.Configuration` only behind explicit typed binding and
  observable precedence; do not create implicit environment behavior.
- `Microsoft.Extensions.Logging` concepts may inform the Wharf logger, but
  console rendering and durable JSON must remain separate sinks.
- `System.Text.Json` for versioned structured records and deterministic
  serialization.
- `System.Diagnostics.Process` with argument-list APIs for future CLI
  adapters, never shell-concatenated commands.

## Release and history strategy

1. Update `docs/wharf.md`, `docs/wharf-prd.md`, `docs/wharf-sessions.md`, README, and repository
   memory to make .NET the canonical implementation language.
2. Delete the remote and local `0.1.0` tag because it identifies the withdrawn
   Go implementation.
3. Preserve the Go commits in Git history; do not rewrite or force-push branch
   history.
4. Remove only Wharf Go production/test files, `go.mod`, `go.sum`, and the Go
   validation script. Preserve unrelated benchmark Go fixtures under
   `smoke-tasks/`.
5. Recreate session 1 as the .NET CLI foundation and tag the replacement
   `0.1.0` only after its own tests and close ritual pass.

## Risks and mitigations

- .NET 10 SDK availability may differ from the current date's release
  information. Verify the installed SDK and pin the exact version in
  `global.json`.
- `System.CommandLine` API compatibility can vary. Keep command construction
  behind Wharf-owned factories and test the actual compiled binary.
- Removing the tag is a destructive release-history operation. Delete only
  the exact `0.1.0` tag; never reset branches or rewrite commits.
- Existing Go fixtures are part of the benchmark corpus, not Wharf. Scope
  deletion to the root Wharf module and its `cmd/wharf`/`internal` trees.

## Not taken

- Maintaining a parallel Go Wharf implementation.
- Rewriting the entire repository or deleting unrelated Go smoke-task modules.
- Deleting Go commits or force-pushing history.
- Starting sessions 2+ functionality before the replacement .NET 0.1.0
  foundation is green.
