using System.CommandLine;
using System.Text.Json;
using Wharf.Agent;
using Wharf.Command;
using Wharf.Configuration;
using Wharf.Copilot;
using Wharf.Logging;
using Wharf.Image;
using Wharf.Recovery;
using Wharf.Reporting;
using Wharf.Run;
using Wharf.State;
using Wharf.Tasks;
using Wharf.Verifier;

namespace Wharf.Tests;

public sealed class BatchRunTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"wharf-batch-{Guid.NewGuid():N}");

    public BatchRunTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void BudgetsCompleteOneFixedKUniverseWithoutRerunningFailures()
    {
        string tasks = CreateTasks(14);
        RunConfiguration configuration = Configuration(k: 4, n: 56);
        var runner = new FakeAgent();
        RunService service = Service(runner);
        string? run = null;
        int[] budgets = [25, 20, 1, 2, 25];
        int[] expected = [25, 20, 1, 2, 8];
        for (int i = 0; i < budgets.Length; i++)
        {
            RetainedResumePlan? plan = run is null ? null : BatchRunPlanning.Plan(run, budgets[i]);
            using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
            int before = runner.Calls;
            RunResult result = service.Execute(
                tasks, Path.Combine(root, "jobs"), configuration, logger,
                resume: plan is null ? null : new RunResumeTarget(plan.RunDirectory, plan.Attempts),
                batchSize: budgets[i]);
            run = Path.Combine(result.JobDirectory, result.AttemptDirectory);
            Assert.Equal(expected[i], runner.Calls - before);
            Assert.Equal(i == 4 ? "completed" : "batch-limit", result.Status);
            Assert.Equal("1.0.95", BatchRunPlanning.Plan(run, 1).CliVersion);
        }
        Assert.NotNull(run);
        Assert.Empty(BatchRunPlanning.Plan(run, 1).Attempts);
        Assert.Equal(56, Directory.GetFiles(run, "result.json", SearchOption.AllDirectories)
            .Count(path => Path.GetDirectoryName(Path.GetDirectoryName(path)) == run));
        Assert.Contains(Directory.GetFiles(run, "result.json", SearchOption.AllDirectories),
            path => File.ReadAllText(path).Contains("\"status\": \"failed\"", StringComparison.Ordinal));
        BatchState state = RunBatch.Read(run);
        Assert.Equal(expected, state.Invocations.Select(batch => batch.Admitted.Count));
        Assert.Equal(TaskSelector.OrderAttemptsForAdmission(
                TaskSelector.ExpandAttempts(new HarborTaskLoader().Load(tasks), 4))
                .Select(attempt => attempt.AttemptId),
            state.Invocations.SelectMany(batch => batch.Selected));
        Assert.Equal(56, state.Invocations.SelectMany(batch => batch.Accepted).Distinct().Count());
        Assert.Equal(0, CommandTree.Create(new WharfConfiguration(Quiet: true))
            .Parse(["run", "--run-directory", run, "-b", "1"]).Invoke());
        Assert.Equal(56, runner.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidBatchSizeDoesNotCreateOutput(int size)
    {
        string tasks = CreateTasks(1);
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        Assert.Throws<RunException>(() => Service(new FakeAgent()).Execute(
            tasks, Path.Combine(root, "jobs"), Configuration(), logger, batchSize: size));
        Assert.False(Directory.Exists(Path.Combine(root, "jobs")));
    }

    [Fact]
    public void ExternalCancellationPersistsCancelledRatherThanCompleted()
    {
        string tasks = CreateTasks(2);
        using var cancellation = new CancellationTokenSource();
        var runner = new FakeAgent
        {
            Execute = (_, _) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<AgentRunResult>(cancellation.Token);
            }
        };
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            Service(runner).Execute(tasks, Path.Combine(root, "jobs"), Configuration(n: 1),
                logger, cancellationToken: cancellation.Token, batchSize: 1));
        string run = Assert.Single(Directory.GetDirectories(
            Path.Combine(root, "jobs", "copilot-test-model-default-1.0.95")));
        BatchInvocation invocation = Assert.Single(RunBatch.Read(run).Invocations);
        Assert.Equal("cancelled", invocation.Status);
        Assert.Empty(invocation.Accepted);
        Assert.Equal(2, invocation.Pending);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public void ContinuationPreservesOriginalTaskSubset()
    {
        string tasks = CreateTasks(3);
        RunConfiguration configuration = Configuration(k: 2);
        RunService service = Service(new FakeAgent());
        RunResult initial;
        using (WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true)))
            initial = service.Execute(tasks, Path.Combine(root, "jobs"), configuration,
                logger, selectedTasks: new HarborTaskLoader().Load(tasks).Take(2).ToArray(), batchSize: 1);
        string run = Path.Combine(initial.JobDirectory, initial.AttemptDirectory);
        RetainedResumePlan plan = BatchRunPlanning.Plan(run, 10);
        Assert.Equal(3, plan.Attempts.Count);
        using WharfLogger resumedLogger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        RunResult resumed = service.Execute(tasks, Path.Combine(root, "jobs"), configuration,
            resumedLogger, resume: new RunResumeTarget(run, plan.Attempts), batchSize: 10);
        Assert.Equal(2, resumed.TaskCount);
        Assert.Empty(BatchRunPlanning.Plan(run, 1).Attempts);
    }

    [Fact]
    public void QuotaStopsNewStartsDeletesPayloadAndNextBatchRetriesPristine()
    {
        string tasks = CreateTasks(3);
        var runner = new FakeAgent { ExhaustFirst = true };
        RunService service = Service(runner);
        RunConfiguration configuration = Configuration(n: 1) with { RetainWorkspaces = true };
        RunResult result;
        using (WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true)))
            result = service.Execute(tasks, Path.Combine(root, "jobs"), configuration,
                logger, batchSize: 3);
        string run = Path.Combine(result.JobDirectory, result.AttemptDirectory);
        Assert.Equal("quota-paused", result.Status);
        Assert.Equal(1, runner.Calls);
        RetainedResumePlan plan = BatchRunPlanning.Plan(run, 1);
        Assert.Equal(3, plan.Attempts.Count);
        BatchState state = RunBatch.Read(run);
        QuotaRejection rejection = Assert.Single(state.Rejections);
        Assert.True(rejection.Deleted);
        Assert.True(File.Exists(rejection.AuditPath));
        Assert.False(File.Exists(Path.Combine(run, rejection.TrialDirectory, "agent", "patch.diff")));
        Assert.False(File.Exists(Path.Combine(run, rejection.TrialDirectory, "result.json")));
        Assert.Equal(0, plan.Attempts[0].TrialRetry);
        Assert.Equal(0, ProgressService.Read(Path.Combine(run, "run.jsonl")).TestsCompleted);
        Assert.Equal(0, ProgressService.Read(Path.Combine(run, "run.jsonl")).TestsRunning);
        runner.ExhaustFirst = false;
        using (WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true)))
            result = service.Execute(tasks, Path.Combine(root, "jobs"), configuration, logger,
                resume: new RunResumeTarget(run, plan.Attempts), batchSize: 1);
        Assert.Equal("batch-limit", result.Status);
        Assert.Equal(2, runner.Calls);
        Assert.Equal(2, BatchRunPlanning.Plan(run, 1).Attempts.Count);
        Assert.Equal(1, ProgressService.Read(Path.Combine(run, "run.jsonl")).TestsCompleted);
        AnalyticsTrial row = Assert.Single(new RunEvidenceReader().Read(run,
            new AnalyticsExportOptions("arm", "copilot", "1.0.95", "test-model", "default", false)));
        Assert.NotEqual("AgentQuota", row.ExceptionType);
        Assert.Equal(1, row.AgentSeconds);
    }

    [Fact]
    public void StopGateDrainsStartedWorkAndBlocksRetriesAndPreparedWork()
    {
        string run = Path.Combine(root, "run");
        Directory.CreateDirectory(run);
        HarborTask task = new HarborTaskLoader().Load(CreateTasks(1)).Single();
        TaskAttempt[] attempts = TaskSelector.ExpandAttempts([task], 3).ToArray();
        using var batch = new RunBatch(run);
        batch.Begin(3, attempts);
        Assert.True(batch.Admit(attempts[0].AttemptId));
        Assert.True(batch.Admit(attempts[1].AttemptId));
        batch.AgentStarting(attempts[0].AttemptId);
        batch.Stop(new AgentQuotaFailure("subscription usage exhausted"));
        Assert.False(batch.Admit(attempts[2].AttemptId));
        Assert.Throws<AgentExecutionException>(() => batch.AgentStarting(attempts[0].AttemptId));
        Assert.Throws<AgentExecutionException>(() => batch.AgentStarting(attempts[1].AttemptId));
        batch.Accept(attempts[0].AttemptId);
        Assert.Single(batch.Summary.Accepted);
        Assert.Equal("quota-paused", batch.Finish(2));
    }

    [Fact]
    public void WriterLockDoesNotBlockReadOnlyPlanning()
    {
        string tasks = CreateTasks(1);
        string run = CreateRetainedRun(tasks);
        using var writer = new RunBatch(run);
        Assert.Throws<RunException>(() => new RunBatch(run));
        Assert.Single(BatchRunPlanning.Plan(run, 1).Attempts);
        string[] before = Directory.GetFiles(run, "*", SearchOption.AllDirectories);
        Assert.Equal(0, CommandTree.Create(new WharfConfiguration(Quiet: true))
            .Parse(["run", "--run-directory", run, "-b", "1", "--dry-run"]).Invoke());
        Assert.Equal(before, Directory.GetFiles(run, "*", SearchOption.AllDirectories));
        Assert.Equal(2, CommandTree.Create(new WharfConfiguration(Quiet: true))
            .Parse(["run", "--run-directory", run, "-b", "1", "--cli-version", "9.9.9"]).Invoke());
    }

    [Fact]
    public void CorruptBatchLogicalIdentityFailsReadOnly()
    {
        string run = CreateRetainedRun(CreateTasks(1));
        var state = new BatchState("1.0",
            [new BatchInvocation("batch", 1, DateTimeOffset.UtcNow, "interrupted",
                ["unknown#attempt-1"], [], [], [])], []);
        string path = Path.Combine(run, RunBatch.FileName);
        string json = JsonSerializer.Serialize(state, StateJson.Options);
        File.WriteAllText(path, json);
        Assert.Throws<RunException>(() => BatchRunPlanning.Plan(run, 1));
        Assert.Equal(json, File.ReadAllText(path));
        Assert.False(File.Exists(Path.Combine(run, ".writer.lock")));
    }

    [Fact]
    public void PendingDeletionIsNotTerminalAndRecoveryFinishesIdempotently()
    {
        string tasks = CreateTasks(1);
        string run = CreateRetainedRun(tasks);
        RetainedResumeAttempt attempt = Assert.Single(BatchRunPlanning.Plan(run, 1).Attempts);
        Directory.CreateDirectory(attempt.TrialDirectory);
        File.WriteAllText(Path.Combine(attempt.TrialDirectory, "result.json"),
            JsonSerializer.Serialize(new
            {
                status = "passed",
                taskId = attempt.Attempt.Task.TaskId,
                attempt = 1
            }));
        File.WriteAllText(Path.Combine(attempt.TrialDirectory, "patch.diff"), "must be deleted");
        string unrelatedRetry = Path.Combine(attempt.TrialDirectory, "trial-retries", "retry-1");
        Directory.CreateDirectory(unrelatedRetry);
        File.WriteAllText(Path.Combine(unrelatedRetry, "result.json"), """{"totalCost":7}""");
        string audit = Path.Combine(run, ".github", "tracking", "quota-rejection.json");
        Directory.CreateDirectory(Path.GetDirectoryName(audit)!);
        File.WriteAllText(audit, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            rejectionId = "rejection",
            runDirectory = run,
            trialDirectory = attempt.TrialDirectory,
            taskId = attempt.Attempt.Task.TaskId,
            attempt = 1
        }));
        var state = new BatchState("1.0", [],
            [new QuotaRejection("rejection", Path.GetFileName(attempt.TrialDirectory),
                attempt.Attempt.Task.TaskId, 1, DateTimeOffset.UtcNow, audit, false, null, 123)]);
        File.WriteAllText(Path.Combine(run, RunBatch.FileName),
            JsonSerializer.Serialize(state, StateJson.Options));
        RetainedResumeAttempt pending = Assert.Single(BatchRunPlanning.Plan(run, 1).Attempts);
        Assert.Equal(1, pending.TrialRetry);
        Assert.Equal(7, pending.TotalCost);
        Assert.True(File.Exists(Path.Combine(attempt.TrialDirectory, "result.json")));
        using (var recovered = new RunBatch(run))
            Assert.True(Assert.Single(RunBatch.Read(run).Rejections).Deleted);
        Assert.Empty(Directory.GetFiles(attempt.TrialDirectory));
        Assert.True(File.Exists(Path.Combine(unrelatedRetry, "result.json")));
        using (var recoveredAgain = new RunBatch(run))
            Assert.Single(BatchRunPlanning.Plan(run, 1).Attempts);
    }

    [Fact]
    public void LiveQuotaSignalPersistsRejectionBeforeAgentReturns()
    {
        string tasks = CreateTasks(1);
        string run = CreateRetainedRun(tasks);
        RetainedResumeAttempt target = Assert.Single(BatchRunPlanning.Plan(run, 1).Attempts);
        Directory.CreateDirectory(target.TrialDirectory);
        File.WriteAllText(Path.Combine(target.TrialDirectory, "partial.patch"), "partial");
        using (var interrupted = new RunBatch(run))
        {
            interrupted.Begin(1, [target.Attempt]);
            interrupted.Admit(target.Attempt.AttemptId);
            interrupted.AgentStarting(target.Attempt.AttemptId);
            interrupted.Stop(new AgentQuotaFailure("subscription usage exhausted"),
                target.TrialDirectory, target.Attempt, Configuration());
            Assert.False(Assert.Single(RunBatch.Read(run).Rejections).Deleted);
            Assert.True(File.Exists(Path.Combine(target.TrialDirectory, "partial.patch")));
        }
        using (var recovery = new RunBatch(run))
        {
            Assert.Empty(Directory.GetFiles(target.TrialDirectory));
            Assert.True(Assert.Single(RunBatch.Read(run).Rejections).Deleted);
        }
    }

    [Fact]
    public void RejectionAuditPreservesDollarCostsAndBlocksUnauditedDeletion()
    {
        string tasks = CreateTasks(1);
        string run = CreateRetainedRun(tasks);
        RetainedResumeAttempt target = Assert.Single(BatchRunPlanning.Plan(run, 1).Attempts);
        string agent = Path.Combine(target.TrialDirectory, "agent");
        Directory.CreateDirectory(agent);
        var usage = new AgentUsage(10, 20, 0, 0, 0, null, 0.4m, null,
            new Dictionary<string, AgentModelUsage>(), CostUsd: 0.4m);
        var summary = new AgentEvidenceSummary("1.0", target.Attempt.Task.TaskId,
            "failed", 1, 1500, 30, 1, null, null, "patch.diff", "stdout.log",
            "stderr.log", "trajectory.jsonl", usage);
        File.WriteAllText(Path.Combine(agent, "result.json"),
            JsonSerializer.Serialize(summary, StateJson.Options));
        using (var batch = new RunBatch(run))
        {
            batch.Begin(1, [target.Attempt]);
            batch.Admit(target.Attempt.AttemptId);
            batch.Stop(new AgentQuotaFailure("subscription usage exhausted"),
                target.TrialDirectory, target.Attempt,
                Configuration() with { Credentials = new SecretSettings(Token: "must-not-persist") });
            QuotaRejection rejection = Assert.Single(RunBatch.Read(run).Rejections);
            Assert.Equal(0.4m, rejection.KnownCostUsd);
            Assert.Null(rejection.KnownCredits);
            Assert.Equal(1500, rejection.KnownAgentMilliseconds);
            Assert.DoesNotContain("must-not-persist", File.ReadAllText(rejection.AuditPath));
            File.Delete(rejection.AuditPath);
        }
        Assert.Throws<RunException>(() => new RunBatch(run));
        Assert.True(File.Exists(Path.Combine(agent, "result.json")));
        Assert.Single(BatchRunPlanning.Plan(run, 1).Attempts);
    }

    [Fact]
    public void QuotaDrainsConcurrentSuccessWithoutAdmittingReplacement()
    {
        string tasks = CreateTasks(3);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeAgent
        {
            Execute = async (request, call) =>
            {
                if (call == 1)
                {
                    await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    var quota = new AgentQuotaFailure("subscription usage exhausted");
                    request.QuotaDetected!(quota);
                    stopped.SetResult();
                    return new AgentRunResult(1, "", "", "", "partial", TimeSpan.FromSeconds(1), Quota: quota);
                }
                started.SetResult();
                await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
                return new AgentRunResult(0, "", "", "", "valid", TimeSpan.FromSeconds(1));
            }
        };
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        RunResult result = Service(runner).Execute(tasks, Path.Combine(root, "jobs"),
            Configuration(n: 2), logger, batchSize: 3);
        string run = Path.Combine(result.JobDirectory, result.AttemptDirectory);
        Assert.Equal("quota-paused", result.Status);
        Assert.Equal(2, runner.Calls);
        Assert.Single(RunBatch.Read(run).Invocations.Single().Accepted);
        Assert.Equal(2, BatchRunPlanning.Plan(run, 1).Attempts.Count);
    }

    private RunService Service(FakeAgent runner) => new(
        imagePreparer: new FakeImages(),
        workspacePreparer: new FakeWorkspaces(),
        agentExecution: new AgentExecutionService(runner),
        verifier: new PristineVerifierService(
            new FakeVerifierWorkspaces(root), new FakePatch(), new FakeVerifier()),
        invocationFactory: configuration => new CopilotInvocation(
            "copilot", [], new Dictionary<string, string>(), false,
            configuration.CliVersion, configuration.CliVersion, configuration.Model,
            configuration.Effort, configuration.Effort, [], []));

    private static RunConfiguration Configuration(int k = 1, int n = 4) =>
        RunConfigurationResolver.Resolve(null,
            new RunConfigurationOverrides(K: k, N: n, P: 2, Model: "test-model",
                Effort: "default", CliVersion: "1.0.95"),
            new Dictionary<string, string?>());

    private string CreateRetainedRun(string tasks)
    {
        string run = Path.Combine(root, "copilot-test-model-default-1.0.95", "all-1");
        Directory.CreateDirectory(run);
        RetainedRunMetadataStore.Write(run, tasks, Configuration(),
            new HarborTaskLoader().Load(tasks));
        File.WriteAllText(Path.Combine(run, "run.jsonl"), JsonSerializer.Serialize(new
        {
            message = "plan tasks=1 attempts=1 runs=1 task-concurrency=4 preparation-concurrency=2 admission-order=longest-first"
        }) + "\n");
        return run;
    }

    private string CreateTasks(int count)
    {
        string tasks = Path.Combine(root, "tasks");
        for (int i = 0; i < count; i++)
        {
            string task = Path.Combine(tasks, $"task-{i:00}");
            Directory.CreateDirectory(task);
            File.WriteAllText(Path.Combine(task, "instruction.md"), "Synthetic task.");
            File.WriteAllText(Path.Combine(task, "task.toml"), $$"""
                schema_version = "1.3"
                [task]
                name = "task-{{i:00}}"
                description = "Synthetic batch fixture"
                [metadata]
                task_id = "task-{{i:00}}"
                language = "csharp"
                display_title = "Synthetic"
                category = "test"
                [environment]
                [agent]
                [verifier]
                [verifier.environment]
                """);
        }
        return tasks;
    }

    private sealed class FakeAgent : IAgentRunner
    {
        private int calls;
        public int Calls => calls;
        public bool ExhaustFirst { get; set; }
        public Func<AgentExecutionRequest, int, Task<AgentRunResult>>? Execute { get; init; }
        public Task<AgentRunResult> RunAsync(AgentExecutionRequest request, CancellationToken token)
        {
            int call = Interlocked.Increment(ref calls);
            if (Execute is not null)
                return Execute(request, call);
            return Task.FromResult(new AgentRunResult(
                ExhaustFirst && call == 1 ? 1 : 0, "", "", "", "synthetic patch",
                TimeSpan.FromSeconds(1),
                Quota: ExhaustFirst && call == 1 ? new AgentQuotaFailure("subscription usage exhausted") : null));
        }
    }

    private sealed class FakeImages : IAgentImagePreparer
    {
        public Task<string> PrepareAsync(HarborTask task, string evidenceDirectory, string imageName,
            Action<string>? progress = null, CancellationToken cancellationToken = default,
            string? copilotVersion = null) => Task.FromResult(imageName);
        public Task RemoveImageAsync(string image, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeWorkspaces : IAgentWorkspacePreparer
    {
        public Task PrepareAsync(AgentExecutionRequest request, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task PrepareForHostCleanupAsync(AgentExecutionRequest request, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeVerifierWorkspaces(string root) : IPristineWorkspaceFactory
    {
        public Task<string> CreateAsync(string sourceDirectory, CancellationToken cancellationToken)
        {
            string path = Path.Combine(root, "verifier-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return Task.FromResult(path);
        }
    }

    private sealed class FakePatch : IPatchApplier
    {
        public Task ApplyAsync(string workspaceDirectory, string patch, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeVerifier : IVerifierRunner
    {
        public Task<VerifierCommandResult> RunAsync(VerifierRequest request, string workspaceDirectory,
            VerifierCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(new VerifierCommandResult(command.Name,
                request.TaskId.EndsWith("0", StringComparison.Ordinal) ? 1 : 0, "", "", 1, false));
    }
}
