using System.Net.Http.Json;
using System.Text.Json;
using Wharf.State;

namespace Wharf.Operations;

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
