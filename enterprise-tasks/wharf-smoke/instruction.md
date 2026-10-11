The Wharf 0.16.0 release can render reports but lacks a local end-to-end smoke
workflow. Implement the 0.17.0 smoke slice.

Use only local fixtures and the supplied host-check result. Create distinct
source, agent-workspace, and verifier-workspace directories; retain trial,
patch, report, and host-check evidence; and render Markdown, JSON, and JSON
Lines reports. Successful host qualification returns completed status and exit
code zero. A blocking host result must retain the same evidence while returning
qualification-blocked status and the host exit code. Reject an empty output
path explicitly.

Preserve the historical public contract in the `Wharf.Smoke` namespace.
Expose `SmokeService`, `SmokeResult`, and `SmokeException`, and preserve the
existing `Wharf.Command.CommandTree`,
`Wharf.Configuration.RunConfiguration`, and
`Wharf.Configuration.WharfConfiguration` types.

Do not modify existing tests or commit changes. The evaluator captures the working-tree
patch after implementation.
