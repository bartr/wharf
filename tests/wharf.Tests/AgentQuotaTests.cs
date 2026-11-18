using System.Diagnostics;
using System.Text.Json;
using Wharf.Agent;
using Wharf.Copilot;
using Wharf.Execution;

namespace Wharf.Tests;

public sealed class AgentQuotaTests
{
    [Theory]
    [InlineData("codex", """{"type":"error","message":"You've hit your usage limit. secret-token"}""", false)]
    [InlineData("codex", """{"type":"error","message":"You\u2019ve hit your usage limit. secret-token"}""", false)]
    [InlineData("codex", """{"type":"turn.failed","error":{"message":"You\u2019ve hit your usage limit. secret-token"}}""", false)]
    [InlineData("codex", """{"type":"turn.failed","error":{"code":"usage_limit_reached","resets_at":1795050000}}""", false)]
    [InlineData("codex", """{"type":"error","error":{"type":"insufficient_quota","message":"secret-token"}}""", true)]
    [InlineData("codex", """{"error":{"type":"usage_limit_reached","code":null,"message":"secret-token","resets_at":1795050000}}""", false)]
    [InlineData("claude", """{"type":"assistant","error":"rate_limit","message":{"content":[{"type":"text","text":"You've hit your limit · resets 5pm"}]}}""", false)]
    [InlineData("claude", """{"type":"assistant","error":"rate_limit","message":{"content":[{"type":"text","text":"You\u2019ve hit your limit; resets 5pm"}]}}""", false)]
    [InlineData("claude", """{"type":"result","is_error":true,"errors":["Credit balance is too low. secret-token"]}""", true)]
    [InlineData("claude", """{"type":"rate_limit_event","rate_limit_info":{"status":"rejected","rateLimitType":"five_hour","resetsAt":1795050000}}""", false)]
    public void RecognizesExplicitExhaustionWithoutRetainingRawDiagnostics(
        string cli, string output, bool credits)
    {
        AgentQuotaFailure quota = Assert.IsType<AgentQuotaFailure>(AgentQuotaParser.Parse(cli, output));

        Assert.Equal(
            $"{cli} {(credits ? "credits exhausted" : "subscription usage limit reached")}",
            quota.Reason);
        Assert.DoesNotContain("secret-token", JsonSerializer.Serialize(quota));
    }

    [Theory]
    [InlineData("codex", """{"type":"error","message":"429 Too Many Requests, retrying in 1s"}""")]
    [InlineData("codex", """{"type":"turn.failed","error":{"message":"Service overloaded"}}""")]
    [InlineData("codex", """{"type":"error","message":"Failed to fetch model catalog: network timeout"}""")]
    [InlineData("codex", """{"type":"error","message":"Rate limit reached for requests per minute"}""")]
    [InlineData("codex", """{"type":"item.completed","item":{"type":"command_execution","aggregated_output":"You've hit your usage limit"}}""")]
    [InlineData("claude", """{"type":"assistant","message":{"content":[{"type":"text","text":"You've hit your limit · resets tomorrow"}]}}""")]
    [InlineData("claude", """{"type":"rate_limit_event","rate_limit_info":{"status":"allowed_warning","rateLimitType":"five_hour"}}""")]
    [InlineData("claude", """{"type":"rate_limit_event","rate_limit_info":{"status":"rejected","rateLimitType":"requests_per_minute"}}""")]
    [InlineData("claude", """{"type":"result","is_error":true,"errors":["HTTP 429 overloaded_error"]}""")]
    [InlineData("copilot", """{"type":"error","message":"You've hit your usage limit"}""")]
    [InlineData("claude", "You've hit your limit · resets tomorrow")]
    public void IgnoresTransientFailuresAndNonErrorContent(string cli, string output) =>
        Assert.Null(AgentQuotaParser.Parse(cli, output));

    [Theory]
    [InlineData("1795050000", "1795050000")]
    [InlineData("\"2026-11-18T21:00:00Z\"", "2026-11-18T21:00:00.0000000+00:00")]
    [InlineData("\"secret-token\"", null)]
    public void RetainsOnlyValidatedResetMetadata(string reset, string? expected)
    {
        AgentQuotaFailure quota = AgentQuotaParser.Parse("codex",
            """{"type":"turn.failed","error":{"code":"usage_limit_reached","resets_at":""" +
            reset + "}}")!;

        Assert.Equal(expected, quota.Reset);
    }

    [Theory]
    [InlineData("You've hit your limit · resets 5pm (America/Chicago)", "17:00 (America/Chicago)")]
    [InlineData("You've hit your usage limit. Try again at 5:32 PM.", "17:32")]
    [InlineData("You've hit your usage limit. resets secret-token", null)]
    [InlineData("You\u2019ve hit your usage limit. Try again at Nov 19th, 2026 2:53 AM.", "2026-11-19 02:53")]
    [InlineData("You've hit your usage limit. Try again at Feb 31st, 2026 2:53 AM.", null)]
    public void NormalizesExplicitResetClockWithoutRetainingSurroundingMessage(
        string message, string? expected)
    {
        string output = JsonSerializer.Serialize(new { type = "error", message });
        AgentQuotaFailure quota = AgentQuotaParser.Parse("claude", output)!;

        Assert.Equal(expected, quota.Reset);
        Assert.DoesNotContain("secret-token", JsonSerializer.Serialize(quota));
    }

    [Theory]
    [InlineData("")]
    [InlineData("partial patch")]
    public async Task QuotaFailsAndRetainsEvidenceWithoutRetryEvenWithSuccessfulExit(string patch)
    {
        using var directories = new TestDirectories();
        var quota = new AgentQuotaFailure("codex subscription usage limit reached", "1795050000");
        var runner = new FakeRunner(new AgentRunResult(
            0, "output", "Execution failed: 400 internal server error", "trajectory",
            patch, TimeSpan.FromSeconds(1), Quota: quota));
        var service = new AgentExecutionService(runner);
        int notifications = 0;
        var sink = new RetrySink();
        AgentExecutionRequest request = directories.Request with
        {
            QuotaDetected = found =>
            {
                Assert.Equal(quota, found);
                notifications++;
            }

        };

        AgentExecutionOutcome outcome = await service.ExecuteWithInfrastructureRetryAsync(
            request, directories.Evidence, "logical-1", 2,
            (_, _, _) => throw new InvalidOperationException("quota must not retry"), sink);

        Assert.Equal("failed", outcome.Summary.Status);
        Assert.Equal(AgentQuotaFailure.ErrorType, outcome.Summary.ErrorType);
        Assert.Equal(quota, outcome.Summary.Quota);
        Assert.Equal(0, outcome.InfrastructureRetries);
        Assert.Equal(1, notifications);
        Assert.Equal(1, runner.Starts);
        Assert.Equal(0, sink.Count);
        Assert.Equal(patch, File.ReadAllText(Path.Combine(directories.Evidence, "patch.diff")));
        Assert.Contains("\"errorType\":\"AgentQuota\"",
            File.ReadAllText(Path.Combine(directories.Evidence, "result.json")));
        Assert.Contains("\"reset\":\"1795050000\"",
            File.ReadAllText(Path.Combine(directories.Evidence, "result.json")));
    }

    [Fact]
    public async Task CodexSubscriptionWindowErrorDoesNotRetryOrAcceptPartialPatch()
    {
        using var directories = new TestDirectories();
        string output = JsonSerializer.Serialize(new
        {
            type = "error",
            message = "You\u2019ve hit your usage limit. Upgrade to Pro, " +
                "purchase more credits or try again at Nov 19th, 2026 2:53 AM."
        });
        var runner = new FakeRunner(new AgentRunResult(
            1, output, "", output, "partial patch", TimeSpan.FromSeconds(1)));
        var sink = new RetrySink();
        AgentExecutionOutcome outcome = await new AgentExecutionService(runner)
            .ExecuteWithInfrastructureRetryAsync(
                directories.Request with
                {
                    Invocation = directories.Request.Invocation with { Cli = "codex" }
                },
                directories.Evidence, "logical-1", 2,
                (_, _, _) => throw new InvalidOperationException("quota must not retry"), sink);

        Assert.Equal(AgentQuotaFailure.ErrorType, outcome.Summary.ErrorType);
        Assert.Equal("2026-11-19 02:53", outcome.Summary.Quota?.Reset);
        Assert.Equal("failed", outcome.Summary.Status);
        Assert.Equal(1, runner.Starts);
        Assert.Equal(0, sink.Count);
    }

    [Fact]
    public async Task ServiceRecognizesStreamEvidenceOnSyntheticRunnerAndDeduplicatesNotification()
    {
        using var directories = new TestDirectories();
        var quota = new AgentQuotaFailure("codex subscription usage limit reached");
        var runner = new FakeRunner(new AgentRunResult(
            0, """{"type":"error","message":"You've hit your usage limit."}""",
            "", "", "partial", TimeSpan.Zero))
        {
            OnRun = request =>
            {
                request.QuotaDetected?.Invoke(quota);
                request.QuotaDetected?.Invoke(quota);
            }
        };
        int notifications = 0;
        AgentEvidenceSummary summary = await new AgentExecutionService(runner).ExecuteAsync(
            directories.Request with
            {
                Invocation = directories.Request.Invocation with { Cli = "codex" },
                QuotaDetected = _ => notifications++
            }, directories.Evidence);

        Assert.Equal(1, notifications);
        Assert.Equal(AgentQuotaFailure.ErrorType, summary.ErrorType);
    }

    [Fact]
    public async Task QuotaPausedStartPreventsFakeExecutionAndDoesNotReadMissingResult()
    {
        using var directories = new TestDirectories();
        var runner = new FakeRunner(new AgentRunResult(0, "", "", "", "patch", TimeSpan.Zero));
        var request = directories.Request with
        {
            AgentStarting = () => throw new AgentExecutionException("quota paused", "QuotaPaused")
        };

        AgentExecutionException exception = await Assert.ThrowsAsync<AgentExecutionException>(
            () => new AgentExecutionService(runner).ExecuteWithInfrastructureRetryAsync(
                request, directories.Evidence, "logical-1", 2,
                (_, _, _) => throw new InvalidOperationException("must not retry"), new RetrySink()));

        Assert.Equal("QuotaPaused", exception.ErrorType);
        Assert.Equal(0, runner.Starts);
        Assert.False(File.Exists(Path.Combine(directories.Evidence, "result.json")));
    }

    [Fact]
    public async Task AgentStartingRunsBeforeSyntheticRunner()
    {
        using var directories = new TestDirectories();
        bool started = false;
        var runner = new FakeRunner(new AgentRunResult(0, "", "", "", "patch", TimeSpan.Zero))
        {
            OnRun = _ => Assert.True(started)
        };

        await new AgentExecutionService(runner).ExecuteAsync(
            directories.Request with { AgentStarting = () => started = true },
            directories.Evidence);

        Assert.Equal(1, runner.Starts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessNotifiesQuotaBeforeExitAndRetainsExactStreams(bool standardError)
    {
        using var directories = new TestDirectories();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string release = Path.Combine(directories.Root, "release");
        const string quotaLine =
            """{"type":"turn.failed","error":{"message":"You\u2019ve hit your usage limit. Try again at Nov 19th, 2026 2:53 AM."}}""";
        var notified = new TaskCompletionSource<AgentQuotaFailure>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int notifications = 0;
        var observer = new AgentQuotaObserver("codex", quota =>
        {
            Interlocked.Increment(ref notifications);
            notified.TrySetResult(quota);
        });
        var startInfo = new ProcessStartInfo("/bin/bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(
            "printf '%s\\n' \"$2\"" + (standardError ? " >&2" : "") +
            "; while [ ! -f \"$1\" ]; do sleep 0.02; done; printf 'tail-without-newline'");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(release);
        startInfo.ArgumentList.Add(quotaLine);
        Task<DockerProcessResult> execution = DockerAgentProcess.RunProcessAsync(
            startInfo, cancellation.Token, observer.Observe, observer.Observe);
        try
        {
            AgentQuotaFailure quota = await notified.Task.WaitAsync(cancellation.Token);
            Assert.False(execution.IsCompleted);
            Assert.Equal("2026-11-19 02:53", quota.Reset);
        }
        finally
        {
            File.WriteAllText(release, "");
        }
        DockerProcessResult result = await execution;
        observer.Observe(quotaLine);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, notifications);
        Assert.Equal(standardError ? "tail-without-newline" : quotaLine + "\ntail-without-newline",
            result.StandardOutput);
        Assert.Equal(standardError ? quotaLine + "\n" : "", result.StandardError);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ProviderCommandMirrorsStreamsKeepsEvidenceAndPreservesExitCode(string cli)
    {
        using var directories = new TestDirectories();
        string logs = Directory.CreateDirectory(Path.Combine(directories.Root, "logs")).FullName;
        AgentExecutionRequest request = directories.Request with
        {
            Invocation = directories.Request.Invocation with
            {
                Cli = cli,
                Executable = "/bin/bash",
                Arguments = ["-c", "printf 'stdout'; printf 'stderr' >&2; exit 17"]
            }
        };
        IReadOnlyList<string> arguments = DockerAgentRunner.BuildAgentArguments(request, "agent-test");
        int commandIndex = arguments.ToList().IndexOf("-lc") + 1;
        var startInfo = new ProcessStartInfo("/bin/bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["CODEX_HOME"] = Path.Combine(directories.Root, "codex-home");
        startInfo.Environment["WHARF_CODEX_AUTH_JSON"] = "{}";
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(arguments[commandIndex]
            .Replace("cd /workspace/;", $"cd '{request.WorkspaceDirectory}';", StringComparison.Ordinal)
            .Replace("/logs/agent/", logs + "/", StringComparison.Ordinal));
        foreach (string argument in arguments.Skip(commandIndex + 1))
            startInfo.ArgumentList.Add(argument);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        DockerProcessResult result = await DockerAgentProcess.RunProcessAsync(startInfo, cancellation.Token);

        Assert.Equal(17, result.ExitCode);
        Assert.Equal("stdout", result.StandardOutput);
        Assert.Equal("stderr", result.StandardError);
        Assert.Equal("stdout", File.ReadAllText(Path.Combine(logs, "stdout.log")));
        Assert.Equal("stderr", File.ReadAllText(Path.Combine(logs, "stderr.log")));
        Assert.Equal("stdout", File.ReadAllText(Path.Combine(logs, "trajectory.jsonl")));
    }

    private sealed class FakeRunner(AgentRunResult result) : IAgentRunner
    {
        public int Starts { get; private set; }
        public Action<AgentExecutionRequest>? OnRun { get; init; }

        public Task<AgentRunResult> RunAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
        {
            Starts++;
            OnRun?.Invoke(request);
            return Task.FromResult(result);
        }
    }

    private sealed class RetrySink : IInfrastructureRetrySink
    {
        public int Count { get; private set; }

        public Task RecordAsync(InfrastructureRetryEvidence evidence, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestDirectories : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Directory.GetCurrentDirectory(), $".agent-quota-test-{Guid.NewGuid():N}");
        public string Evidence => Path.Combine(Root, "evidence");
        public AgentExecutionRequest Request { get; }

        public TestDirectories()
        {
            string source = Directory.CreateDirectory(Path.Combine(Root, "source")).FullName;
            string workspace = Directory.CreateDirectory(Path.Combine(Root, "workspace")).FullName;
            Request = new AgentExecutionRequest("quota-task", "agent:1", source, workspace,
                "wharf-agent", new CopilotInvocation(
                    "copilot", [], new Dictionary<string, string>(), false,
                    "1.2.3", "1.2.3", "test", "medium", "medium", [], []), []);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
