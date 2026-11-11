The Wharf 0.13.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.13.0 to 0.14.0 change for Implement run operations and notifications.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement run operations and notifications. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

Preserve the historical public contract in the `Wharf.Operations` namespace.
Expose `RunStatusSnapshot`, `GracefulStopService`, `NotificationService`, and
`IRunNotificationSink`, while preserving the existing
`Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Use these historical public shapes:

- `RunNotification` exposes `SchemaVersion`, `RunId`, and `Status`, with
  schema version `"1.0"`;
- `IRunNotificationSink.NotifyAsync(RunNotification, CancellationToken)`;
- `NotificationService(params IRunNotificationSink[] sinks)` and
  `CompletedAsync(string runId, CancellationToken cancellationToken =
  default)`;
- `WebhookRunNotificationSink(HttpClient, Uri endpoint)` rejects non-HTTPS
  endpoints;
- `IGracefulRun.CancelAsync(CancellationToken)` and
  `WaitForCompletionAsync(CancellationToken)`;
- `IEphemeralResourceCleaner.RemoveAsync(IReadOnlySet<string> resourceIds,
  CancellationToken)`; and
- `GracefulStopService(IGracefulRun, IEphemeralResourceCleaner)` with
  `StopAsync(IReadOnlySet<string> ephemeralResourceIds, TimeSpan timeout,
  CancellationToken cancellationToken = default)`.

Completion notifications are sent to every configured sink. Graceful stop
cancels the run first, waits only within the supplied bound, and removes only
the explicitly supplied ephemeral resource IDs. If the completion wait times
out or returns a canceled task while the caller's cancellation token remains
active, treat the run as unfinished and invoke the cleaner. Propagate
caller-requested cancellation.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
