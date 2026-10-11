The Wharf 0.18.0 release has packaging but no host qualification profiles.
Implement the 0.19.0 qualification slice.

Publish the historical 32-concurrent and 88-concurrent overnight profiles.
Recommendations must respect both operator limits and detected CPU and memory
capacity; they must never silently raise an operator limit. Qualification
requires all expected tasks, no lost completed tasks, completion within the
deadline, and a complete score. Produce deterministic profile-ordered evidence
and recommendation reports using schema version `1.0`.

Preserve the historical public contract in the `Wharf.Qualification`
namespace. Expose `HostProfiles`, `HostProfileService`,
`QualificationReport`, and `QualificationEvidence`, while preserving the
existing `Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Do not modify existing tests or commit changes. The evaluator captures the working-tree patch after
implementation.
