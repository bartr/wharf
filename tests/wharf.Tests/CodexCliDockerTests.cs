using Wharf.Agent;
using Wharf.Copilot;
using Wharf.Image;
using Wharf.Run;
using Wharf.Tasks;

namespace Wharf.Tests;

public sealed class CodexCliDockerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecutesCodexLifecycleWithPrivateAuthAndRetainedEvidence(bool failed)
    {
        string root = Path.Combine(Path.GetTempPath(), $"wharf-codex-docker-{Guid.NewGuid():N}");
        string workspace = Path.Combine(root, "workspace");
        string evidence = Path.Combine(root, "agent");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(evidence);
        string imageName = $"agent-codex-test-{Guid.NewGuid():N}:latest";
        var images = new DockerAgentImagePreparer(cli: "codex");
        string? image = null;
        try
        {
            string fixture = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "../../../../../wharf-swe/smoke-tasks"));
            HarborTask task = Assert.Single(new HarborTaskLoader().Load(fixture),
                task => task.TaskId == "smoke-dotnet");
            image = await images.PrepareAsync(task, evidence, imageName, copilotVersion: "0.169.0");
            string credentialPath = Path.Combine(root, "auth.json");
            File.WriteAllText(credentialPath, CodexCliTests.Credential);
            CopilotInvocation native = await new CopilotAdapter(
                    new NpmRegistryCopilotCliProbe(cli: "codex"), "codex", "codex")
                .PrepareAsync(CodexCliTests.Options(credentialPath, "max"));
            string result = failed
                ? """{"type":"turn.failed","error":{"message":"synthetic failure"}}"""
                : """{"type":"turn.completed","usage":{"input_tokens":10,"cached_input_tokens":4,"cache_write_input_tokens":1,"output_tokens":20,"reasoning_output_tokens":3}}""";
            string script =
                """
                set -euo pipefail
                test "$HOME" = /home/wharf
                test "$CODEX_HOME" = /home/wharf/.codex
                test -z "${WHARF_CODEX_AUTH_JSON+x}"
                test -z "${OPENAI_API_KEY+x}"
                test -z "${CODEX_API_KEY+x}"
                test "$(stat -c '%a' "$CODEX_HOME")" = 700
                test "$(stat -c '%a' "$CODEX_HOME/auth.json")" = 600
                node -e 'if (require(process.env.CODEX_HOME + "/auth.json").tokens.access_token !== "synthetic-access") process.exit(1)'
                test "$(codex --version)" = "codex-cli 0.169.0"
                "$@" >/dev/null
                printf 'synthetic change\n' > codex-test.txt
                """ +
                $"\nprintf '%s\\n' '{result}'";
            CopilotInvocation invocation = native with
            {
                Executable = "/bin/bash",
                Arguments =
                [
                    "-c", script, "--",
                    native.Executable, .. native.Arguments, "--help",
                    .. RunService.BuildPromptArguments("- Fix this task.", "codex")
                ]
            };
            var request = new AgentExecutionRequest(
                task.TaskId, image, task.RootPath, workspace, "none", invocation, [],
                $"agent-codex-test-{Guid.NewGuid():N}", TimeSpan.FromSeconds(60),
                RetainWorkspace: true, EvidenceDirectory: evidence);
            await new DockerAgentWorkspacePreparer().PrepareAsync(request, CancellationToken.None);
            AgentEvidenceSummary summary = await new AgentExecutionService(new DockerAgentRunner())
                .ExecuteAsync(request, evidence);

            Assert.Equal(failed ? "failed" : "completed", summary.Status);
            Assert.Equal(failed ? 1 : 0, summary.ExitCode);
            Assert.Null(summary.Cost);
            Assert.Null(summary.Usage?.CostUsd);
            Assert.Null(summary.Usage?.AiCredits);
            if (!failed)
            {
                Assert.Equal(10, summary.Usage?.InputTokens);
                Assert.Equal(4, summary.Usage?.CacheReadTokens);
                Assert.Equal(3, summary.Usage?.ReasoningTokens);
                Assert.Equal(1, summary.Steps);
            }
            Assert.Contains("codex-test.txt", File.ReadAllText(Path.Combine(evidence, "patch.diff")));
            Assert.False(Directory.Exists(Path.Combine(workspace, ".codex")));
            foreach (string file in new[] { "result.json", "stdout.log", "stderr.log", "trajectory.jsonl", "patch.diff" })
                Assert.DoesNotContain("synthetic-access", File.ReadAllText(Path.Combine(evidence, file)));
        }
        finally
        {
            if (image is not null)
                await images.RemoveImageAsync(image);
            Directory.Delete(root, recursive: true);
        }
    }
}
