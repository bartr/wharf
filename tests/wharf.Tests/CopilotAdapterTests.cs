using Wharf.Copilot;

namespace Wharf.Tests;

public sealed class CopilotAdapterTests
{
    [Fact]
    public async Task RecordsExactVersionAndNormalizedEffort()
    {
        var adapter = new CopilotAdapter(new FakeProbe("v1.2.3"));
        CopilotInvocation invocation = await adapter.PrepareAsync(Options(
            frontierEffort: "xhigh",
            credential: new CopilotCredentialSource(
                CopilotCredentialSourceKind.Environment,
                "GH_TOKEN")));

        Assert.Equal("1.2.3", invocation.RequestedVersion);
        Assert.Equal("1.2.3", invocation.EffectiveVersion);
        Assert.Equal("high", invocation.FrontierEffort);
        Assert.Contains("--reasoning-effort", invocation.Arguments);
        Assert.DoesNotContain("--frontier-effort", invocation.Arguments);
        Assert.Equal("GH_TOKEN", invocation.Environment["WHARF_COPILOT_CREDENTIAL_VARIABLE"]);
    }

    [Fact]
    public async Task RejectsVersionDrift()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.4"));

        CopilotAdapterException exception = await Assert.ThrowsAsync<CopilotAdapterException>(
            () => adapter.PrepareAsync(Options()));

        Assert.Contains("does not match", exception.Message);
    }

    [Fact]
    public async Task AcceptsPrereleaseVersion()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3-0"));
        CopilotInvocation invocation = await adapter.PrepareAsync(
            Options(requestedVersion: "1.2.3-0"));

        Assert.Equal("1.2.3-0", invocation.EffectiveVersion);
    }

    [Fact]
    public async Task ResolvesLatestToEffectiveVersion()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3-0"));
        CopilotInvocation invocation = await adapter.PrepareAsync(
            Options(requestedVersion: "latest"));

        Assert.Equal("1.2.3-0", invocation.RequestedVersion);
        Assert.Equal("1.2.3-0", invocation.EffectiveVersion);
    }

    [Fact]
    public async Task SupportsFileAndStdinCredentialSourcesWithoutSecretValues()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3"));
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "file-secret\n");
            CopilotInvocation file = await adapter.PrepareAsync(Options(
                credential: new CopilotCredentialSource(CopilotCredentialSourceKind.File, path)));
            CopilotInvocation stdin = await adapter.PrepareAsync(Options(
                credential: new CopilotCredentialSource(CopilotCredentialSourceKind.Stdin, "secret-value")));

            Assert.DoesNotContain("--credential-file", file.Arguments);
            Assert.Equal("file-secret", file.CredentialValue);
            Assert.Equal("GH_TOKEN", file.Environment["WHARF_COPILOT_CREDENTIAL_VARIABLE"]);
            Assert.False(file.ReadsCredentialFromStdin);
            Assert.True(stdin.ReadsCredentialFromStdin);
            Assert.Contains("--credential-stdin", stdin.Arguments);
            Assert.DoesNotContain("secret-value", stdin.RedactedCommand);
            Assert.DoesNotContain("secret-value", string.Join(' ', stdin.Environment.Values));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RejectsEndpointsOutsideAllowlist()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3"));

        CopilotAdapterException exception = await Assert.ThrowsAsync<CopilotAdapterException>(
            () => adapter.PrepareAsync(Options(
                endpoints: ["https://evil.example.test/api"])));

        Assert.Contains("not allowed", exception.Message);
    }

    [Fact]
    public async Task AcceptsAllowedEndpointAndSubagentModels()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3"));
        CopilotInvocation invocation = await adapter.PrepareAsync(Options(
            subagentModels: ["solver", "reviewer"],
            endpoints: ["https://api.github.com/copilot"]));

        Assert.Equal(["solver", "reviewer"], invocation.SubagentModels);
        Assert.Contains("https://api.github.com/copilot", invocation.Arguments);
    }

    [Fact]
    public async Task SupportsHydraFusionModel()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3"));
        CopilotInvocation invocation = await adapter.PrepareAsync(Options(model: "hydrafusion"));

        Assert.Equal("hydrafusion", invocation.Model);
        Assert.Equal(
            "hydrafusion",
            invocation.Arguments[Array.IndexOf(invocation.Arguments.ToArray(), "--model") + 1]);
        Assert.DoesNotContain("--reasoning-effort", invocation.Arguments);
        Assert.Contains("--experimental", invocation.Arguments);
        Assert.Contains("--log-level", invocation.Arguments);
        Assert.Contains("--log-dir", invocation.Arguments);
    }

    [Theory]
    [InlineData(CopilotCredentialSourceKind.Environment, "bad-name")]
    [InlineData(CopilotCredentialSourceKind.Environment, "")]
    public async Task RejectsInvalidCredentialSource(
        CopilotCredentialSourceKind kind,
        string reference)
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.PrepareAsync(Options(
                credential: new CopilotCredentialSource(kind, reference))));
    }

    private static CopilotAdapterOptions Options(
        string requestedVersion = "1.2.3",
        string frontierEffort = "medium",
        string model = "gpt-5",
        CopilotCredentialSource? credential = null,
        IReadOnlyList<string>? subagentModels = null,
        IReadOnlyList<string>? endpoints = null) =>
        new(
            requestedVersion,
            model,
            "medium",
            frontierEffort,
            subagentModels ?? [],
            credential ?? new CopilotCredentialSource(
                CopilotCredentialSourceKind.Environment,
                "GH_TOKEN"),
            endpoints ?? [],
            new HashSet<string>(["api.github.com"], StringComparer.OrdinalIgnoreCase));

    private sealed class FakeProbe(string version) : ICopilotCliProbe
    {
        public Task<CopilotCliVersion> GetVersionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotCliVersion(version));
    }
}
