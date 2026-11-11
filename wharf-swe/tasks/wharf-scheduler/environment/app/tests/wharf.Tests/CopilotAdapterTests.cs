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
        Assert.Contains("--frontier-effort", invocation.Arguments);
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
    public async Task SupportsFileAndStdinCredentialSourcesWithoutSecretValues()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3"));
        CopilotInvocation file = await adapter.PrepareAsync(Options(
            credential: new CopilotCredentialSource(CopilotCredentialSourceKind.File, "/tmp/token")));
        CopilotInvocation stdin = await adapter.PrepareAsync(Options(
            credential: new CopilotCredentialSource(CopilotCredentialSourceKind.Stdin, "secret-value")));

        Assert.Contains("--credential-file", file.Arguments);
        Assert.False(file.ReadsCredentialFromStdin);
        Assert.True(stdin.ReadsCredentialFromStdin);
        Assert.Contains("--credential-stdin", stdin.Arguments);
        Assert.DoesNotContain("secret-value", stdin.RedactedCommand);
        Assert.DoesNotContain("secret-value", string.Join(' ', stdin.Environment.Values));
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
        string frontierEffort = "medium",
        CopilotCredentialSource? credential = null,
        IReadOnlyList<string>? subagentModels = null,
        IReadOnlyList<string>? endpoints = null) =>
        new(
            "1.2.3",
            "gpt-5",
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
