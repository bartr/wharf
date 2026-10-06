# Wharf .NET migration changes

The withdrawn Go `0.1.0` tag was removed locally and remotely without
rewriting history. Only the root Wharf Go module and its validation script were
deleted; benchmark fixtures and unrelated directories remain.

The canonical implementation is now a .NET 10.0.401 SDK-style solution:

- `src/wharf` contains the executable and responsibility-based namespaces.
- `tests/wharf.Tests` contains focused CLI foundation tests.
- `global.json` pins the SDK.
- `scripts/validate-wharf.sh` is the documented format, analyze, build, and
  test command.

Validation completed:

- `dotnet build wharf.slnx --configuration Release`
- `dotnet test wharf.slnx --configuration Release`
- `./scripts/validate-wharf.sh`
- compiled CLI checks for `--version`, `version`, invalid completion, JSON
  output, operational failure, help, and `-p:WharfVersion=0.1.7`

The replacement release remains deliberately limited to the Session 1
foundation. Operational commands return explicit non-successful placeholders.
