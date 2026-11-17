using Wharf.Configuration;

namespace Wharf.Agent;

internal sealed record AgentCli(string Name, string Package, string CredentialVariable)
{
    public string InstallPath => $"/opt/wharf/{Name}";

    public static AgentCli For(string name) => name switch
    {
        "copilot" => new(name, "@github/copilot", "GH_TOKEN"),
        "claude" => new(name, "@anthropic-ai/claude-code", "CLAUDE_CODE_OAUTH_TOKEN"),
        _ => throw new RunConfigurationException(
            $"unsupported CLI: {name}; allowed values: claude, copilot")
    };
}
