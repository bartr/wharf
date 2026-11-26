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

public sealed class CodexCliTests : IDisposable
{
    internal const string Credential =
        """{"auth_mode":"chatgpt","tokens":{"access_token":"synthetic-access","id_token":"synthetic-id","refresh_token":"synthetic-refresh","account_id":"synthetic-account"}}""";
    private readonly string root = Path.Combine(Path.GetTempPath(), $"wharf-codex-{Guid.NewGuid():N}");

    public CodexCliTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Theory]
    [InlineData("flag-model", "environment-model", "flag-model")]
    [InlineData(null, "environment-model", "environment-model")]
    [InlineData(null, null, "gpt-6-luna")]
    public void CodexDefaultAndModelOverridesUseStandardPrecedence(
        string? flag, string? environment, string expected)
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(Cli: "codex", Model: flag),
            new Dictionary<string, string?>
            {
                ["WHARF_CLI"] = "copilot",
                ["WHARF_MODEL"] = environment
            });
        Assert.Equal("codex", configuration.Cli);
        Assert.Equal(expected, configuration.Model);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("smoke")]
    [InlineData("prebuild")]
    public void CommandsAcceptCodex(string name)
    {
        RootCommand command = CommandTree.Create(new WharfConfiguration());
        var selected = command.Subcommands.Single(item => item.Name == name);
        var option = Assert.IsType<Option<string>>(
            selected.Options.Single(item => item.Name == "--cli"));
        var parsed = command.Parse([name, "--cli", "codex"]);
        Assert.Empty(parsed.Errors);
        Assert.Equal("codex", parsed.GetValue(option));
    }

    [Fact]
    public void CodexDryRunRequiresNoCredentialOrOutput()
    {
        string output = $"wharf-codex-dry-{Guid.NewGuid():N}";
        string rendered = DryRunService.Execute(
            new RunConfigurationOverrides(Cli: "codex", OutputDirectory: output),
            "json", new Dictionary<string, string?>(), new UserConfiguration());
        using JsonDocument document = JsonDocument.Parse(rendered);
        Assert.Equal("codex", document.RootElement.GetProperty("cli").GetString());
        Assert.Equal("gpt-6-luna", document.RootElement.GetProperty("model").GetString());
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("default", "default", null)]
    [InlineData("high", "high", "high")]
    [InlineData("max", "xhigh", "xhigh")]
    public async Task CodexUsesNativeArgumentsAndPrivateAuthTransport(
        string effort, string native, string? configured)
    {
        string path = Path.Combine(root, "auth.json");
        File.WriteAllText(path, Credential);
        CopilotInvocation invocation = await Adapter().PrepareAsync(Options(path, effort));

        Assert.Equal("codex", invocation.Cli);
        Assert.Equal("codex", invocation.Executable);
        Assert.Equal("exec", invocation.Arguments[0]);
        Assert.Equal("0.169.0", invocation.EffectiveVersion);
        Assert.Contains("--json", invocation.Arguments);
        Assert.Contains("--dangerously-bypass-approvals-and-sandbox", invocation.Arguments);
        Assert.Contains("forced_login_method=\"chatgpt\"", invocation.Arguments);
        Assert.Contains("cli_auth_credentials_store=\"file\"", invocation.Arguments);
        Assert.Equal(native, invocation.NativeEffort);
        Assert.Equal(effort, invocation.FrontierEffort);
        Assert.Equal(configured is not null,
            invocation.Arguments.Any(argument => argument.StartsWith("model_reasoning_effort=", StringComparison.Ordinal)));
        if (configured is not null)
            Assert.Contains($"model_reasoning_effort=\"{configured}\"", invocation.Arguments);
        Assert.DoesNotContain("--reasoning-effort", invocation.Arguments);
        Assert.DoesNotContain("--effort", invocation.Arguments);
        Assert.Equal("WHARF_CODEX_AUTH_JSON",
            invocation.Environment["WHARF_COPILOT_CREDENTIAL_VARIABLE"]);
        Assert.Equal("/home/wharf/.codex", invocation.Environment["CODEX_HOME"]);
        Assert.Equal(Credential, invocation.CredentialValue);
        Assert.DoesNotContain("synthetic-access", invocation.RedactedCommand);
        Assert.DoesNotContain("synthetic-access", string.Join(' ', invocation.Environment.Values));
    }

    [Theory]
    [InlineData("synthetic-api-key")]
    [InlineData("{synthetic-secret")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"tokens":{"access_token":""}}""")]
    [InlineData("""{"auth_mode":"apikey","tokens":{"access_token":"synthetic-secret"}}""")]
    [InlineData("""{"OPENAI_API_KEY":"synthetic-secret","tokens":{"access_token":"synthetic-secret"}}""")]
    public async Task RejectsInvalidCredentialsWithoutEchoingTheirContents(string credential)
    {
        string path = Path.Combine(root, "auth.json");
        File.WriteAllText(path, credential);
        CopilotAdapterException exception = await Assert.ThrowsAsync<CopilotAdapterException>(
            () => Adapter().PrepareAsync(Options(path)));
        Assert.Contains("ChatGPT auth.json", exception.Message);
        Assert.DoesNotContain("synthetic", exception.Message);
    }

    [Fact]
    public async Task CodexCredentialFileMustExistAndBeNonempty()
    {
        string path = Path.Combine(root, "auth.json");
        CopilotAdapterException missing = await Assert.ThrowsAsync<CopilotAdapterException>(
            () => Adapter().PrepareAsync(Options(path)));
        Assert.Contains($"credential file does not exist: {path}", missing.Message);
        Assert.Contains("ChatGPT account", missing.Message);
        Assert.Contains("codex -c 'cli_auth_credentials_store=\"file\"' login\n", missing.Message);
        Assert.Contains("codex -c 'cli_auth_credentials_store=\"file\"' login --device-auth", missing.Message);
        Assert.Contains("same value", missing.Message);
        File.WriteAllText(path, "");
        Assert.Contains("empty",
            (await Assert.ThrowsAsync<CopilotAdapterException>(
                () => Adapter().PrepareAsync(Options(path)))).Message);
    }

    [Theory]
    [InlineData(null, "does not exist")]
    [InlineData("", "empty")]
    [InlineData("{synthetic-secret", "ChatGPT auth.json")]
    public async Task InvalidAuthFailsBeforeLatestVersionLookup(string? credential, string error)
    {
        string path = Path.Combine(root, "auth.json");
        if (credential is not null)
            File.WriteAllText(path, credential);
        var probe = new CountingVersionProbe();
        var adapter = new CopilotAdapter(probe, "codex", "codex");

        CopilotAdapterException exception = await Assert.ThrowsAsync<CopilotAdapterException>(
            () => adapter.PrepareAsync(Options(path) with { RequestedVersion = "latest" }));

        Assert.Contains(error, exception.Message);
        Assert.Equal(0, probe.CallCount);
    }

    [Fact]
    public void CodexUsesItsOwnCredentialPromptAndArmIdentity()
    {
        (CopilotCredentialSource source, string? value) = RunService.ResolveCredential(
            new SecretSettings(Token: "copilot-only"), "codex");
        Assert.Equal(CopilotCredentialSourceKind.File, source.Kind);
        Assert.Equal("~/.codex/auth.json", source.Reference);
        Assert.Null(value);
        Assert.Equal(["--", "- Fix this task."],
            RunService.BuildPromptArguments("- Fix this task.", "codex"));
        Assert.Equal("codex-gpt-6-luna-default-0.169.0",
            RunService.BuildJobDirectoryName("gpt-6-luna", "default", "latest", "0.169.0", "codex"));
    }

    [Fact]
    public async Task CodexUsesCustomHostHomeWithoutChangingContainerHome()
    {
        string home = Path.Combine(root, "custom codex home");
        Directory.CreateDirectory(home);
        string path = Path.Combine(home, "auth.json");
        File.WriteAllText(path, Credential);

        (CopilotCredentialSource source, string? value) = RunService.ResolveCredential(
            new SecretSettings(Token: "copilot-only"), "codex", home);
        Assert.Equal(path, source.Reference);
        Assert.Null(value);

        CopilotInvocation invocation = await Adapter().PrepareAsync(Options(source.Reference));
        Assert.Equal(Credential, invocation.CredentialValue);
        Assert.Equal("/home/wharf/.codex", invocation.Environment["CODEX_HOME"]);
        Assert.DoesNotContain(home, string.Join(' ', invocation.Environment.Values));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyCodexHomeUsesTheNativeDefault(string home)
    {
        (CopilotCredentialSource source, _) =
            RunService.ResolveCredential(new SecretSettings(), "codex", home);
        Assert.Equal("~/.codex/auth.json", source.Reference);
    }

    [Theory]
    [InlineData("copilot")]
    [InlineData("claude")]
    public async Task OtherProvidersKeepTheirCredentialPathsAndMissingFileErrors(string cli)
    {
        (CopilotCredentialSource source, _) =
            RunService.ResolveCredential(new SecretSettings(), cli, root);
        Assert.Equal($"~/.wharf/{cli}", source.Reference);

        string path = Path.Combine(root, "missing");
        var adapter = new CopilotAdapter(new ExactVersionProbe(), cli, cli);
        CopilotAdapterException exception = await Assert.ThrowsAsync<CopilotAdapterException>(
            () => adapter.PrepareAsync(Options(path)));
        Assert.Equal($"credential file does not exist: {path}", exception.Message);
    }

    [Fact]
    public async Task ResolvesLatestFromTheCodexPackage()
    {
        using var client = new HttpClient(new RegistryHandler());
        var adapter = new CopilotAdapter(
            new NpmRegistryCopilotCliProbe(client, "codex"), "codex", "codex");
        Assert.Equal("0.169.0", await adapter.ResolveVersionAsync("latest"));
    }

    [Theory]
    [InlineData("claude", "2.1.241")]
    [InlineData("codex", "0.169.0")]
    public async Task LatestProviderVersionNamesItsOwnRunDirectory(string cli, string version)
    {
        string path = Path.Combine(root, "credential");
        File.WriteAllText(path, cli == "codex" ? Credential : "synthetic-oauth");
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            null, new RunConfigurationOverrides(Cli: cli), new Dictionary<string, string?>());
        var probe = new CountingVersionProbe(version);
        var adapter = new CopilotAdapter(probe, cli, cli);
        CopilotInvocation invocation = await adapter.PrepareAsync(Options(path) with
        {
            RequestedVersion = configuration.CliVersion,
            Model = configuration.Model
        });

        Assert.Equal(version, invocation.EffectiveVersion);
        Assert.Equal(1, probe.CallCount);
        Assert.Equal($"{cli}-{configuration.Model}-default-{version}",
            RunService.BuildJobDirectoryName(configuration.Model, configuration.Effort,
                configuration.CliVersion, invocation.EffectiveVersion, cli));
    }

    [Fact]
    public void CodexImageCachesUseTheCorrectPackageAndInstallPath()
    {
        string payload = DockerAgentImagePreparer.BuildCopilotPayloadDockerfile(
            "installer:1", "0.169.0", "codex");
        string agent = DockerAgentImagePreparer.BuildAgentCacheDockerfile(
            "task:1", "cli:1", "0.169.0", "key", "codex");
        Assert.Contains("@openai/codex@0.169.0", payload);
        Assert.Contains("/opt/wharf/codex/node_modules/@openai/codex/package.json", payload);
        Assert.DoesNotContain("--omit=optional", payload);
        Assert.Contains("COPY --link --from=cli /opt/wharf/codex /opt/wharf/codex", agent);
        Assert.StartsWith("wharf-codex-cli:0.169.0-linux-",
            DockerAgentImagePreparer.BuildCopilotCacheImageName("0.169.0", "codex"));
    }

    [Fact]
    public void SumsPerTurnUsageWithoutInventingCostOrRequests()
    {
        AgentStreamResult result = Assert.IsType<AgentStreamResult>(CodexUsageParser.Parse(
            """
            {"type":"thread.started","thread_id":"thread"}
            {"type":"turn.started"}
            {"type":"item.completed","item":{"type":"agent_message","text":"Done"}}
            {"type":"turn.completed","usage":{"input_tokens":10,"cached_input_tokens":4,"cache_write_input_tokens":1,"output_tokens":3,"reasoning_output_tokens":2}}
            {"type":"turn.started"}
            {"type":"turn.completed","usage":{"input_tokens":20,"cached_input_tokens":8,"output_tokens":5}}
            """));
        Assert.NotNull(result.Usage);
        Assert.Equal(30, result.Usage.InputTokens);
        Assert.Equal(12, result.Usage.CacheReadTokens);
        Assert.Equal(1, result.Usage.CacheWriteTokens);
        Assert.Equal(8, result.Usage.OutputTokens);
        Assert.Equal(2, result.Usage.ReasoningTokens);
        Assert.Equal(2, result.Turns);
        Assert.Null(result.Usage.AiCredits);
        Assert.Null(result.Usage.CostUsd);
        Assert.Null(result.Usage.Requests);
        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData("""{"type":"turn.failed","error":{"message":"failed"}}""")]
    [InlineData("""{"type":"error","message":"failed"}""")]
    [InlineData("""
        {"type":"turn.completed","usage":{"input_tokens":1,"cached_input_tokens":0,"output_tokens":2}}
        {"type":"turn.started"}
        """)]
    public void FailedAndIncompleteTurnsCannotBecomeSuccess(string output)
    {
        Assert.True(Assert.IsType<AgentStreamResult>(CodexUsageParser.Parse(output)).IsError);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("""{"type":"turn.completed"}""")]
    [InlineData("""{"type":"turn.completed","usage":{"input_tokens":-1,"cached_input_tokens":0,"output_tokens":0}}""")]
    public void MalformedCodexEvidenceFailsExplicitly(string output)
    {
        Assert.Throws<AgentExecutionException>(() => CodexUsageParser.Parse(output));
    }

    private static CopilotAdapter Adapter() => new(new ExactVersionProbe(), "codex", "codex");

    internal static CopilotAdapterOptions Options(string path, string effort = "default") =>
        new("0.169.0", "gpt-6-luna", effort, effort, [],
            new CopilotCredentialSource(CopilotCredentialSourceKind.File, path), [], new HashSet<string>());

    private sealed class ExactVersionProbe : ICopilotCliProbe
    {
        public Task<CopilotCliVersion> GetVersionAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An exact version must not query the registry.");
    }

    private sealed class CountingVersionProbe(string version = "0.169.0") : ICopilotCliProbe
    {
        public int CallCount { get; private set; }

        public Task<CopilotCliVersion> GetVersionAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new CopilotCliVersion(version));
        }
    }

    private sealed class RegistryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://registry.npmjs.org/%40openai%2Fcodex/latest",
                request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"version":"0.169.0"}""")
            });
        }
    }
}
