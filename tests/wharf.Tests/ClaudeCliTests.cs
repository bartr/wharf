using System.CommandLine;
using System.Net;
using System.Text.Json;
using Wharf.Agent;
using Wharf.Command;
using Wharf.Configuration;
using Wharf.Copilot;
using Wharf.Image;
using Wharf.Run;

namespace Wharf.Tests;

public sealed class ClaudeCliTests
{
    [Theory]
    [InlineData("copilot", "claude", "claude", "copilot")]
    [InlineData(null, "claude", "copilot", "claude")]
    [InlineData(null, null, "claude", "claude")]
    [InlineData(null, null, null, "copilot")]
    public void ResolvesCliWithStandardPrecedence(
        string? flag, string? environment, string? manifest, string expected)
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new RunManifest(Cli: manifest),
            new RunConfigurationOverrides(Cli: flag),
            new Dictionary<string, string?> { ["WHARF_CLI"] = environment });

        Assert.Equal(expected, configuration.Cli);
        Assert.Equal(expected == "claude" ? "claude-sonnet-5-5" : "hydrafusion", configuration.Model);
        using JsonDocument document = JsonDocument.Parse(
            RunConfigurationRenderer.Render(configuration, "json"));
        Assert.Equal(expected, document.RootElement.GetProperty("cli").GetString());
    }

    [Fact]
    public void ClaudeKeepsExplicitModelAndEffortAndIgnoresUnrelatedCliEnvironment()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new RunManifest(Cli: "claude", Model: "claude-opus-4-6", Effort: "high"),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["CLI"] = "copilot" });

        Assert.Equal("claude", configuration.Cli);
        Assert.Equal("claude-opus-4-6", configuration.Model);
        Assert.Equal("high", configuration.Effort);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("prebuild")]
    [InlineData("smoke")]
    public void CommandsAcceptCliSelection(string commandName)
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        var command = root.Subcommands.Single(command => command.Name == commandName);
        var option = Assert.IsType<Option<string>>(
            command.Options.Single(option => option.Name == "--cli"));
        var result = root.Parse([commandName, "--cli", "claude"]);

        Assert.Empty(result.Errors);
        Assert.Equal("claude", result.GetValue(option));
    }

    [Fact]
    public async Task InvalidCliFailsBeforeCreatingOutput()
    {
        string output = $"wharf-invalid-cli-{Guid.NewGuid():N}";
        Assert.Equal(2, await WharfApp.RunAsync([
            "run", "--cli", "unknown", "--output-directory", output]));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void ClaudeDryRunRequiresNoCredentialsAndCreatesNoOutput()
    {
        string output = $"wharf-claude-dry-{Guid.NewGuid():N}";
        string rendered = DryRunService.Execute(
            null, new RunConfigurationOverrides(Cli: "claude", OutputDirectory: output),
            "json", new Dictionary<string, string?>());

        using JsonDocument document = JsonDocument.Parse(rendered);
        Assert.Equal("claude", document.RootElement.GetProperty("cli").GetString());
        Assert.Equal("claude-sonnet-5-5", document.RootElement.GetProperty("model").GetString());
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("high")]
    public async Task ClaudeInvocationUsesOAuthAndNativeFlagsWithoutLeakingToken(string effort)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "synthetic-oauth-token\n");
            var adapter = new CopilotAdapter(new FixedProbe(), "claude", "claude");
            CopilotInvocation invocation = await adapter.PrepareAsync(new CopilotAdapterOptions(
                "2.1.241", "opus", effort, effort, [],
                new CopilotCredentialSource(CopilotCredentialSourceKind.File, path),
                [], new HashSet<string>()));

            Assert.Equal("claude", invocation.Executable);
            Assert.Equal("claude", invocation.Cli);
            Assert.Equal("2.1.241", invocation.EffectiveVersion);
            Assert.Contains("--print", invocation.Arguments);
            Assert.Contains("stream-json", invocation.Arguments);
            Assert.Contains("--dangerously-skip-permissions", invocation.Arguments);
            Assert.Equal(effort != "default", invocation.Arguments.Contains("--effort"));
            Assert.DoesNotContain("--reasoning-effort", invocation.Arguments);
            Assert.DoesNotContain("--no-auto-update", invocation.Arguments);
            Assert.Equal("1", invocation.Environment["DISABLE_AUTOUPDATER"]);
            Assert.Equal("CLAUDE_CODE_OAUTH_TOKEN",
                invocation.Environment["WHARF_COPILOT_CREDENTIAL_VARIABLE"]);
            Assert.Equal("synthetic-oauth-token", invocation.CredentialValue);
            Assert.DoesNotContain("synthetic-oauth-token", invocation.RedactedCommand);
            Assert.DoesNotContain("synthetic-oauth-token", string.Join(' ', invocation.Environment.Values));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ClaudeCredentialFileMustExistAndBeNonempty()
    {
        string path = Path.GetTempFileName();
        try
        {
            var adapter = new CopilotAdapter(new FixedProbe(), "claude", "claude");
            var options = new CopilotAdapterOptions(
                "2.1.241", "opus", "default", "default", [],
                new CopilotCredentialSource(CopilotCredentialSourceKind.File, path),
                [], new HashSet<string>());
            Assert.Contains("empty",
                (await Assert.ThrowsAsync<CopilotAdapterException>(() => adapter.PrepareAsync(options))).Message);
            File.Delete(path);
            Assert.Contains("does not exist",
                (await Assert.ThrowsAsync<CopilotAdapterException>(() => adapter.PrepareAsync(options))).Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ClaudeNeverUsesCopilotCredentialsOrPromptFlags()
    {
        (CopilotCredentialSource source, string? value) = RunService.ResolveCredential(
            new SecretSettings(Token: "copilot-only"), "claude");
        Assert.Equal("~/.wharf/claude", source.Reference);
        Assert.Null(value);
        Assert.Equal(["--", "- Fix this task."],
            RunService.BuildPromptArguments("- Fix this task.", "claude"));
        Assert.Equal("claude-opus-high-2.1.241",
            RunService.BuildJobDirectoryName("opus", "high", "latest", "2.1.241", "claude"));
    }

    [Fact]
    public async Task ClaudeLatestVersionUsesItsOwnRegistryPackage()
    {
        using var client = new HttpClient(new RegistryHandler());
        var adapter = new CopilotAdapter(
            new NpmRegistryCopilotCliProbe(client, "claude"), "claude", "claude");
        Assert.Equal("2.1.241", await adapter.ResolveVersionAsync("latest"));
    }

    [Fact]
    public void ClaudeImagesAndWorkspaceChecksUseTheirOwnPackageAndCache()
    {
        string payload = DockerAgentImagePreparer.BuildCopilotPayloadDockerfile(
            "installer:1", "2.1.241", "claude");
        string agent = DockerAgentImagePreparer.BuildAgentCacheDockerfile(
            "task:1", "cli:1", "2.1.241", "key", "claude");
        Assert.Contains("@anthropic-ai/claude-code@2.1.241", payload);
        Assert.Contains("/opt/wharf/claude/node_modules/@anthropic-ai/claude-code/package.json", payload);
        Assert.Contains("COPY --link --from=cli /opt/wharf/claude /opt/wharf/claude", agent);
        Assert.DoesNotContain("@github/copilot", payload);
        Assert.StartsWith("wharf-claude-cli:2.1.241-linux-",
            DockerAgentImagePreparer.BuildCopilotCacheImageName("2.1.241", "claude"));
    }

    [Fact]
    public void ParsesOnlyFinalCumulativeUsageWithoutConvertingDollarsToCredits()
    {
        ClaudeResult result = Assert.IsType<ClaudeResult>(ClaudeUsageParser.Parse(
            """
            {"type":"assistant","message":{"usage":{"input_tokens":999}}}
            {"type":"result","is_error":false,"num_turns":3,"total_cost_usd":0.45,"usage":{"input_tokens":10,"output_tokens":20,"cache_read_input_tokens":30,"cache_creation_input_tokens":40},"modelUsage":{"claude-opus":{"inputTokens":10,"outputTokens":20,"cacheReadInputTokens":30,"cacheCreationInputTokens":40,"costUSD":0.45}}}
            """));
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage.InputTokens);
        Assert.Equal(20, result.Usage.OutputTokens);
        Assert.Equal(30, result.Usage.CacheReadTokens);
        Assert.Equal(40, result.Usage.CacheWriteTokens);
        Assert.Equal(0.45m, result.Usage.CostUsd);
        Assert.Null(result.Usage.AiCredits);
        Assert.Null(result.Usage.Requests);
        Assert.Equal(3, result.Turns);
        Assert.False(result.IsError);
        Assert.Single(result.Usage.Models);
    }

    [Fact]
    public void PreservesUnknownCostAndErrorResults()
    {
        ClaudeResult result = Assert.IsType<ClaudeResult>(ClaudeUsageParser.Parse(
            """{"type":"result","is_error":true,"usage":{"input_tokens":10}}"""));
        Assert.True(result.IsError);
        Assert.Null(result.Usage?.CostUsd);
        Assert.Null(ClaudeUsageParser.Parse("""{"type":"system","subtype":"init"}"""));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("""{"type":"result","total_cost_usd":-1}""")]
    [InlineData("""{"type":"result","usage":{"input_tokens":-1}}""")]
    [InlineData("""{"type":"result"}""")]
    [InlineData("""{"type":"result","is_error":false,"num_turns":-1}""")]
    public void RejectsMalformedOrInvalidClaudeEvidence(string output)
    {
        Assert.Throws<AgentExecutionException>(() => ClaudeUsageParser.Parse(output));
    }

    private sealed class FixedProbe : ICopilotCliProbe
    {
        public Task<CopilotCliVersion> GetVersionAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An exact version must not query the registry.");
    }

    private sealed class RegistryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(
                "https://registry.npmjs.org/%40anthropic-ai%2Fclaude-code/latest",
                request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"version":"2.1.241"}""")
            });
        }
    }
}
