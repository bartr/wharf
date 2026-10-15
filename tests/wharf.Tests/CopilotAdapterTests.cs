using System.Net;
using Wharf.Copilot;

namespace Wharf.Tests;

public sealed class CopilotAdapterTests
{
    [Fact]
    public async Task RecordsExactVersionAndNormalizedEffort()
    {
        var probe = new FakeProbe("v9.9.9");
        var adapter = new CopilotAdapter(probe);
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
        Assert.Equal(0, probe.CallCount);
    }

    [Fact]
    public async Task UsesModelDefaultWithoutPassingReasoningEffort()
    {
        var adapter = new CopilotAdapter(new FakeProbe("1.2.3"));

        CopilotInvocation invocation = await adapter.PrepareAsync(Options(
            nativeEffort: "default",
            frontierEffort: "default"));

        Assert.Equal("default", invocation.NativeEffort);
        Assert.Equal("default", invocation.FrontierEffort);
        Assert.DoesNotContain("--reasoning-effort", invocation.Arguments);
    }

    [Fact]
    public async Task ExactVersionDoesNotUseProbeVersion()
    {
        var probe = new FakeProbe("1.2.4");
        var adapter = new CopilotAdapter(probe);

        CopilotInvocation invocation = await adapter.PrepareAsync(Options());

        Assert.Equal("1.2.3", invocation.EffectiveVersion);
        Assert.Equal(0, probe.CallCount);
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
        var probe = new FakeProbe("1.2.3-0");
        var adapter = new CopilotAdapter(probe);
        CopilotInvocation invocation = await adapter.PrepareAsync(
            Options(requestedVersion: "latest"));

        Assert.Equal("1.2.3-0", invocation.RequestedVersion);
        Assert.Equal("1.2.3-0", invocation.EffectiveVersion);
        Assert.Equal(1, probe.CallCount);
    }

    [Fact]
    public async Task RegistryProbeResolvesPublishedVersion()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """{"name":"@github/copilot","version":"1.0.98"}"""));
        var probe = new NpmRegistryCopilotCliProbe(client);

        CopilotCliVersion version = await probe.GetVersionAsync(CancellationToken.None);

        Assert.Equal("1.0.98", version.Value);
    }

    [Fact]
    public async Task RegistryProbeRejectsMissingVersion()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """{"name":"@github/copilot"}"""));
        var probe = new NpmRegistryCopilotCliProbe(client);

        CopilotAdapterException exception = await Assert.ThrowsAsync<CopilotAdapterException>(
            () => probe.GetVersionAsync(CancellationToken.None));

        Assert.Contains("returned no version", exception.Message);
    }

    [Fact]
    public async Task RegistryProbeReportsHttpFailure()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            "{}"));
        var probe = new NpmRegistryCopilotCliProbe(client);

        CopilotAdapterException exception = await Assert.ThrowsAsync<CopilotAdapterException>(
            () => probe.GetVersionAsync(CancellationToken.None));

        Assert.Contains("HTTP 503", exception.Message);
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
        string nativeEffort = "medium",
        string frontierEffort = "medium",
        string model = "gpt-5",
        CopilotCredentialSource? credential = null,
        IReadOnlyList<string>? subagentModels = null,
        IReadOnlyList<string>? endpoints = null) =>
        new(
            requestedVersion,
            model,
            nativeEffort,
            frontierEffort,
            subagentModels ?? [],
            credential ?? new CopilotCredentialSource(
                CopilotCredentialSourceKind.Environment,
                "GH_TOKEN"),
            endpoints ?? [],
            new HashSet<string>(["api.github.com"], StringComparer.OrdinalIgnoreCase));

    private sealed class FakeProbe(string version) : ICopilotCliProbe
    {
        public int CallCount { get; private set; }

        public Task<CopilotCliVersion> GetVersionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(GetVersion());

        private CopilotCliVersion GetVersion()
        {
            CallCount++;
            return new CopilotCliVersion(version);
        }
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }
}
