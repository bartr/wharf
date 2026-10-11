The Wharf 0.13.0 release supports recovery but lacks operator-facing run
operations. Implement the 0.14.0 operations and notification slice.

Derive read-only status snapshots from retained events, including lifecycle
counts, occupied concurrency, retry counts, last sequence, and an ETA only when
enough completed work exists. Graceful stop must cancel first, wait within a
positive bound, and clean only resource IDs explicitly supplied as ephemeral
after that bound expires. Emit durable completed and blocked notifications to
every configured sink. Webhook sinks must reject non-HTTPS endpoints.

Preserve the historical public contract in the `Wharf.Operations` namespace.
Expose `RunStatusSnapshot`, `GracefulStopService`, `NotificationService`, and
`IRunNotificationSink`, while preserving the existing
`Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Do not modify existing tests or commit changes. The evaluator captures the working-tree
patch after implementation.
