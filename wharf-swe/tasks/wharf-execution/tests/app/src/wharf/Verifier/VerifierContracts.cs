namespace Wharf.Verifier;

public sealed record VerifierCommand(string Name, string Command, TimeSpan Timeout);

public sealed record VerifierRequest(
    string TaskId,
    string SourceDirectory,
    string AgentWorkspaceDirectory,
    string Network,
    IReadOnlyList<VerifierCommand> Commands,
    IReadOnlyList<string> HiddenPaths,
    IReadOnlyList<string> CredentialEnvironmentVariables)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskId) ||
            string.IsNullOrWhiteSpace(Network) ||
            Commands.Count == 0)
        {
            throw new VerifierExecutionException("task, network, and verifier commands are required");
        }

        ValidateDirectory(SourceDirectory, nameof(SourceDirectory));
        ValidateDirectory(AgentWorkspaceDirectory, nameof(AgentWorkspaceDirectory));
        if (Path.GetFullPath(SourceDirectory).Equals(
                Path.GetFullPath(AgentWorkspaceDirectory),
                StringComparison.Ordinal))
        {
            throw new VerifierExecutionException(
                "verifier must not reuse the agent workspace");
        }

        foreach (VerifierCommand command in Commands)
        {
            if (string.IsNullOrWhiteSpace(command.Name) ||
                string.IsNullOrWhiteSpace(command.Command) ||
                command.Timeout <= TimeSpan.Zero)
            {
                throw new VerifierExecutionException("verifier commands must have names, commands, and positive timeouts");
            }
        }

        foreach (string variable in CredentialEnvironmentVariables)
        {
            if (string.IsNullOrWhiteSpace(variable) ||
                variable.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            {
                throw new VerifierExecutionException($"invalid verifier credential variable: {variable}");
            }
        }
    }

    private static void ValidateDirectory(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path) ||
            !Directory.Exists(path))
        {
            throw new VerifierExecutionException($"{name} must be an existing absolute path");
        }
    }
}

public sealed record VerifierCommandResult(
    string Name,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    long DurationMilliseconds,
    bool TimedOut);

public sealed record TrialResult(
    string SchemaVersion,
    string TaskId,
    string Status,
    string WorkspaceDirectory,
    IReadOnlyList<VerifierCommandResult> Commands,
    string PatchApplied);

public interface IPristineWorkspaceFactory
{
    Task<string> CreateAsync(string sourceDirectory, CancellationToken cancellationToken);
}

public interface IPatchApplier
{
    Task ApplyAsync(string workspaceDirectory, string patch, CancellationToken cancellationToken);
}

public interface IVerifierRunner
{
    Task<VerifierCommandResult> RunAsync(
        VerifierRequest request,
        string workspaceDirectory,
        VerifierCommand command,
        CancellationToken cancellationToken);
}

public sealed class VerifierExecutionException(string message) : Exception(message);

public static class TrialResultSchema
{
    public const string Version = "1.0";
}
