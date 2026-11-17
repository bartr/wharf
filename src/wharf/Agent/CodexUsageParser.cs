using System.Text.Json;

namespace Wharf.Agent;

internal static class CodexUsageParser
{
    public static AgentStreamResult? Parse(string output)
    {
        long input = 0, cached = 0, cacheWrite = 0, tokens = 0, reasoning = 0;
        int turns = 0;
        bool completed = false;
        bool failed = false;
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("type", out JsonElement type))
                    throw new AgentExecutionException("Codex JSONL event must be an object with a type");
                switch (type.GetString())
                {
                    case "turn.started":
                        completed = false;
                        break;
                    case "turn.completed":
                        if (!root.TryGetProperty("usage", out JsonElement usage) ||
                            usage.ValueKind != JsonValueKind.Object)
                            throw new AgentExecutionException("Codex completed turn is missing usage");
                        checked
                        {
                            input += Count(usage, "input_tokens");
                            cached += Count(usage, "cached_input_tokens");
                            cacheWrite += Count(usage, "cache_write_input_tokens", optional: true);
                            tokens += Count(usage, "output_tokens");
                            reasoning += Count(usage, "reasoning_output_tokens", optional: true);
                            turns++;
                        }
                        completed = true;
                        break;
                    case "turn.failed":
                    case "error":
                        failed = true;
                        break;
                }
            }
            catch (Exception exception) when (
                exception is JsonException or InvalidOperationException or
                FormatException or OverflowException)
            {
                throw new AgentExecutionException(
                    $"Codex returned invalid JSONL evidence: {exception.Message}");
            }
        }
        if (turns == 0 && !failed)
            return null;
        AgentUsage? totals = turns == 0 ? null : new AgentUsage(
            input, tokens, cached, cacheWrite, reasoning, null, 0, null,
            new Dictionary<string, AgentModelUsage>());
        return new AgentStreamResult(totals, turns, failed || !completed);
    }

    private static long Count(JsonElement root, string name, bool optional = false)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
            return optional ? 0 : throw new AgentExecutionException($"Codex usage is missing {name}");
        long count = value.GetInt64();
        return count >= 0
            ? count
            : throw new AgentExecutionException($"Codex returned a negative {name} count");
    }
}
