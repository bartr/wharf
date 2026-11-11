using System.Net.Http.Json;
using System.Text.Json;
using Wharf.State;

namespace Wharf.Operations;

public static class RunStatusService
{
    public static RunStatusSnapshot ReadOnly(
        RunId runId,
        IReadOnlyList<StateEvent> events,
        int occupiedConcurrency,
        DateTimeOffset startedAt,
        DateTimeOffset now)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (StateEvent stateEvent in events)
        {
            counts[stateEvent.Status] = counts.GetValueOrDefault(stateEvent.Status) + 1;
        }

        int completed = counts.GetValueOrDefault(StateStatuses.Completed);
        int total = events
            .Where(stateEvent => stateEvent.TrialId is not null)
            .Select(stateEvent => stateEvent.TrialId!.Value.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();
        TimeSpan? eta = completed > 0 && total > completed
            ? TimeSpan.FromTicks(
                (now - startedAt).Ticks * (total - completed) / completed)
            : null;
        string status = events.LastOrDefault()?.Status ?? StateStatuses.Created;
        return new RunStatusSnapshot(
            runId.Value,
            status,
            counts,
            occupiedConcurrency,
            events.Count(stateEvent => stateEvent.InfrastructureRetryId is not null),
            eta,
            events.Count == 0 ? 0 : events.Max(stateEvent => stateEvent.Sequence));
    }
}

public sealed class GracefulStopService
{
    private readonly IGracefulRun run;
    private readonly IEphemeralResourceCleaner cleaner;

    public GracefulStopService(IGracefulRun run, IEphemeralResourceCleaner cleaner)
    {
        this.run = run;
        this.cleaner = cleaner;
    }

    public async Task StopAsync(
        IReadOnlySet<string> ephemeralResourceIds,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        if (gracefulTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(gracefulTimeout));
        }

        await run.CancelAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(gracefulTimeout);
        try
        {
            await run.WaitForCompletionAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await cleaner.RemoveAsync(ephemeralResourceIds, cancellationToken);
        }
    }
}

public sealed class NotificationService
{
    private readonly IReadOnlyList<IRunNotificationSink> sinks;

    public NotificationService(params IRunNotificationSink[] sinks) =>
        this.sinks = sinks;

    public Task CompletedAsync(string runId, CancellationToken cancellationToken = default) =>
        NotifyAsync(runId, "completed", "run completed", cancellationToken);

    public Task BlockedAsync(string runId, string reason, CancellationToken cancellationToken = default) =>
        NotifyAsync(runId, "blocked", reason, cancellationToken);

    private Task NotifyAsync(
        string runId,
        string status,
        string message,
        CancellationToken cancellationToken) =>
        Task.WhenAll(sinks.Select(sink => sink.NotifyAsync(
            new RunNotification("1.0", runId, status, message, DateTimeOffset.UtcNow),
            cancellationToken)));
}

public sealed class WebhookRunNotificationSink : IRunNotificationSink
{
    private readonly HttpClient client;
    private readonly Uri endpoint;

    public WebhookRunNotificationSink(HttpClient client, Uri endpoint)
    {
        if (endpoint.Scheme is not "https")
        {
            throw new ArgumentException("notification webhook must use HTTPS", nameof(endpoint));
        }

        this.client = client;
        this.endpoint = endpoint;
    }

    public async Task NotifyAsync(
        RunNotification notification,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            endpoint,
            notification,
            StateJson.Options,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
