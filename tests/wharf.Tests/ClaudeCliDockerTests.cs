using System.Text.Json;
using Wharf.Agent;
using Wharf.Copilot;
using Wharf.Image;
using Wharf.Tasks;

namespace Wharf.Tests;

public sealed class ClaudeCliDockerTests
{
    [Fact]
    public async Task ExecutesClaudeContainerLifecycleWithoutAModelRequest()
    {
        string root = Path.Combine(Path.GetTempPath(), $"wharf-claude-docker-{Guid.NewGuid():N}");
        string workspace = Path.Combine(root, "workspace");
        string evidence = Path.Combine(root, "agent");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(evidence);
        string imageName = $"agent-claude-test-{Guid.NewGuid():N}:latest";
        var images = new DockerAgentImagePreparer(cli: "claude");
        string? image = null;
        try
        {
            string fixture = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "../../../../../wharf-swe/smoke-tasks"));
            HarborTask task = Assert.Single(new HarborTaskLoader().Load(fixture),
                task => task.TaskId == "smoke-dotnet");
            image = await images.PrepareAsync(task, evidence, imageName, copilotVersion: "2.1.241");
            var invocation = new CopilotInvocation(
                "/bin/bash",
                [
                    "-c",
                    """
                    set -euo pipefail
                    test -n "$CLAUDE_CODE_OAUTH_TOKEN"
                    test "$HOME" = /home/wharf
                    test "$DISABLE_AUTOUPDATER" = 1
                    test "$(claude --version)" = "2.1.241 (Claude Code)"
                    printf 'synthetic change\n' > claude-test.txt
                    printf '%s\n' '{"type":"result","is_error":false,"num_turns":2,"total_cost_usd":0.125,"usage":{"input_tokens":10,"output_tokens":20,"cache_read_input_tokens":30,"cache_creation_input_tokens":40}}'
                    """
                ],
                new Dictionary<string, string>
                {
                    ["WHARF_COPILOT_CREDENTIAL_VARIABLE"] = "CLAUDE_CODE_OAUTH_TOKEN",
                    ["DISABLE_AUTOUPDATER"] = "1"
                },
                false, "2.1.241", "2.1.241", "opus", "default", "default", [], [],
                CredentialValue: "synthetic-token", Cli: "claude");
            var request = new AgentExecutionRequest(
                task.TaskId, image, task.RootPath, workspace, "none", invocation, [],
                $"agent-claude-test-{Guid.NewGuid():N}", TimeSpan.FromSeconds(60),
                EvidenceDirectory: evidence);
            await new DockerAgentWorkspacePreparer().PrepareAsync(request, CancellationToken.None);
            AgentEvidenceSummary summary = await new AgentExecutionService(new DockerAgentRunner())
                .ExecuteAsync(request, evidence);

            Assert.Equal("completed", summary.Status);
            Assert.Null(summary.Cost);
            Assert.Equal(0.125m, summary.Usage?.CostUsd);
            Assert.Equal(10, summary.Usage?.InputTokens);
            Assert.Equal(2, summary.Steps);
            Assert.Contains("claude-test.txt", File.ReadAllText(Path.Combine(evidence, "patch.diff")));
            string retained = File.ReadAllText(Path.Combine(evidence, "result.json"));
            Assert.DoesNotContain("synthetic-token", retained);
            using JsonDocument cache = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(evidence, "agent-image-cache.json")));
            Assert.Equal("claude", cache.RootElement.GetProperty("cli").GetString());
        }
        finally
        {
            if (image is not null)
                await images.RemoveImageAsync(image);
            Directory.Delete(root, recursive: true);
        }
    }
}
