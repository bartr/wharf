using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wharf.Agent;

internal static class AgentQuotaParser
{
    public static AgentQuotaFailure? Parse(string cli, string output)
    {
        if (cli is not ("claude" or "codex"))
            return null;

        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                string? type = Text(root, "type");
                if (cli == "claude" && type == "rate_limit_event" &&
                    root.TryGetProperty("rate_limit_info", out JsonElement info) &&
                    Text(info, "status") == "rejected" &&
                    Text(info, "rateLimitType") is
                        "five_hour" or "seven_day" or "seven_day_opus" or "seven_day_sonnet" or "overage")
                {
                    return Failure(cli, credits: false, info);
                }

                bool errorEnvelope = type is "error" or "turn.failed" ||
                    (type is null && root.ValueKind == JsonValueKind.Object &&
                        root.TryGetProperty("error", out JsonElement providerError) &&
                        providerError.ValueKind == JsonValueKind.Object) ||
                    (cli == "claude" && type == "assistant" && Text(root, "error") is not null) ||
                    (cli == "claude" && type == "result" &&
                        root.TryGetProperty("is_error", out JsonElement isError) &&
                        isError.ValueKind == JsonValueKind.True);
                if (!errorEnvelope)
                    continue;

                AgentQuotaFailure? failure = FindError(cli, root);
                if (failure is not null)
                    return failure;
            }
            catch (JsonException)
            {
                // Progress diagnostics and incomplete stream fragments are not quota evidence.
            }
        }
        return null;
    }

    private static AgentQuotaFailure? FindError(string cli, JsonElement root)
    {
        string? code = Text(root, "code") ?? Text(root, "type");
        bool credits = code is "insufficient_quota" or "credit_balance_too_low" or "credits_exhausted";
        if (credits || code is "usage_limit_reached" or "subscription_limit_reached")
            return Failure(cli, credits, root);

        foreach (string field in new[] { "message", "error", "errors", "content", "text" })
        {
            if (!root.TryGetProperty(field, out JsonElement value))
                continue;
            AgentQuotaFailure? failure = FindValue(cli, value, root);
            if (failure is not null)
                return failure;
        }
        return null;
    }

    private static AgentQuotaFailure? FindValue(string cli, JsonElement value, JsonElement metadata)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            AgentQuotaFailure? failure = FindError(cli, value);
            return failure is null ? null : failure with { Reset = failure.Reset ?? Reset(metadata) };
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in value.EnumerateArray())
            {
                AgentQuotaFailure? failure = FindValue(cli, element, metadata);
                if (failure is not null)
                    return failure;
            }
        }
        if (value.ValueKind != JsonValueKind.String)
            return null;

        string message = value.GetString()!;
        bool credits = Contains(message, "credit balance is too low") ||
            Contains(message, "insufficient credits") ||
            Contains(message, "out of credits") ||
            Contains(message, "credits exhausted");
        bool usage = Contains(message, "you've hit your usage limit") ||
            Contains(message, "you have reached your usage limit") ||
            Contains(message, "subscription usage limit") ||
            Contains(message, "weekly limit reached") ||
            (cli == "claude" && Contains(message, "you've hit your limit") &&
                Contains(message, "reset"));
        if (!credits && !usage)
            return null;
        AgentQuotaFailure quota = Failure(cli, credits, metadata);
        return quota with { Reset = quota.Reset ?? MessageReset(message) };
    }

    private static bool Contains(string value, string text) =>
        value.Contains(text, StringComparison.OrdinalIgnoreCase);

    private static AgentQuotaFailure Failure(string cli, bool credits, JsonElement metadata) =>
        new($"{cli} {(credits ? "credits exhausted" : "subscription usage limit reached")}", Reset(metadata));

    private static string? Reset(JsonElement metadata)
    {
        foreach (string name in new[] { "resets_at", "reset_at", "resetsAt", "reset" })
        {
            if (!metadata.TryGetProperty(name, out JsonElement value))
                continue;
            string? text = value.ValueKind == JsonValueKind.Number
                ? value.GetRawText()
                : value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (text is null || text.Length > 64)
                continue;
            if (Regex.IsMatch(text, @"^\d{1,13}$", RegexOptions.CultureInvariant))
                return text;
            if (DateTimeOffset.TryParseExact(text,
                    ["O", "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset timestamp))
                return timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
        return null;
    }

    private static string? MessageReset(string message)
    {
        Match match = Regex.Match(message.Length > 4096 ? message[..4096] : message,
            @"(?:resets?|try again at)\s+(?<time>(?:1[0-2]|0?[1-9])(?::[0-5]\d)?\s*[ap]m)(?:\s*\((?<zone>[A-Za-z_]+(?:/[A-Za-z_+-]+)+)\))?(?=$|[\s.·])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        if (!match.Success || !TimeOnly.TryParseExact(
                match.Groups["time"].Value,
                ["htt", "h tt", "h:mmtt", "h:mm tt"],
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out TimeOnly time))
            return null;

        string reset = time.ToString("HH:mm", CultureInfo.InvariantCulture);
        string zone = match.Groups["zone"].Value;
        return TimeZoneInfo.TryFindSystemTimeZoneById(zone, out TimeZoneInfo? timeZone)
            ? $"{reset} ({timeZone.Id})"
            : reset;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

internal sealed class AgentQuotaObserver(string cli, Action<AgentQuotaFailure>? detected)
{
    private readonly object sync = new();
    public AgentQuotaFailure? Quota { get; private set; }

    public void Observe(string line)
    {
        AgentQuotaFailure? quota = AgentQuotaParser.Parse(cli, line);
        if (quota is null)
            return;
        lock (sync)
        {
            if (Quota is not null)
                return;
            Quota = quota;
            detected?.Invoke(quota);
        }
    }
}
