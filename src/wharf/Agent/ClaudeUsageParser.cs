using System.Text.Json;

namespace Wharf.Agent;

internal static class ClaudeUsageParser
{
    public static AgentStreamResult? Parse(string output)
    {
        AgentStreamResult? result = null;
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new AgentExecutionException("Claude Code stream event must be a JSON object");
                if (!root.TryGetProperty("type", out JsonElement type) ||
                    type.GetString() != "result")
                    continue;

                decimal? cost = Number(root, "total_cost_usd");
                if (cost < 0)
                    throw new AgentExecutionException("Claude Code returned a negative dollar cost");
                var models = new Dictionary<string, AgentModelUsage>(StringComparer.Ordinal);
                if (root.TryGetProperty("modelUsage", out JsonElement modelUsage))
                {
                    foreach (JsonProperty model in modelUsage.EnumerateObject())
                    {
                        models.Add(model.Name, new AgentModelUsage(
                            Count(model.Value, "inputTokens"),
                            Count(model.Value, "outputTokens"),
                            Count(model.Value, "cacheReadInputTokens"),
                            Count(model.Value, "cacheCreationInputTokens"),
                            0,
                            null,
                            Number(model.Value, "costUSD") ?? 0));
                    }
                }

                AgentUsage? usage = null;
                if (root.TryGetProperty("usage", out JsonElement totals))
                {
                    usage = new AgentUsage(
                        Count(totals, "input_tokens"),
                        Count(totals, "output_tokens"),
                        Count(totals, "cache_read_input_tokens"),
                        Count(totals, "cache_creation_input_tokens"),
                        0,
                        null,
                        cost ?? 0,
                        null,
                        models,
                        cost);
                }
                else if (cost is not null)
                {
                    usage = new AgentUsage(0, 0, 0, 0, 0, null, cost.Value, null, models, cost);
                }
                int? turns = root.TryGetProperty("num_turns", out JsonElement turnsValue)
                    ? turnsValue.GetInt32()
                    : null;
                if (turns < 0)
                    throw new AgentExecutionException("Claude Code returned a negative turn count");
                bool isError = root.TryGetProperty("is_error", out JsonElement error)
                    ? error.GetBoolean()
                    : throw new AgentExecutionException("Claude Code result is missing is_error");
                result = new AgentStreamResult(usage, turns, isError);
            }
            catch (Exception exception) when (
                exception is JsonException or InvalidOperationException or
                FormatException or OverflowException)
            {
                throw new AgentExecutionException(
                    $"Claude Code returned invalid stream-json evidence: {exception.Message}");
            }
        }
        return result;
    }

    private static decimal? Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDecimal()
            : null;

    private static long Count(JsonElement root, string name)
    {
        long count = root.TryGetProperty(name, out JsonElement value) ? value.GetInt64() : 0;
        return count >= 0
            ? count
            : throw new AgentExecutionException($"Claude Code returned a negative {name} count");
    }
}
