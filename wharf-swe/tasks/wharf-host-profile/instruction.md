The Wharf 0.18.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.18.0 to 0.19.0 change for Implement host profile qualification.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement host profile qualification. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

Preserve the historical public contract in the `Wharf.Qualification`
namespace. Expose `HostProfiles`, `HostProfileService`,
`QualificationReport`, and `QualificationEvidence`, while preserving the
existing `Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Use these historical public shapes:

- `HostCapacity(long CpuCount, long MemoryBytes)`;
- `HostProfile(string Id, string Description, int TargetConcurrency,
  long MinimumCpuCount, long MinimumMemoryBytes, TimeSpan OvernightWindow)`;
- `HostProfiles.Get(string id)` returns a case-insensitive known profile or
  throws `QualificationException`;
- `ProfileRecommendation(string ProfileId, int OperatorLimit,
  int RecommendedConcurrency, int RecommendedBuildConcurrency,
  bool RaisedOperatorLimit, IReadOnlyList<string> Reasons)`;
- `HostProfileService.Recommend(HostProfile, HostCapacity,
  int operatorLimit, int operatorBuildLimit)`;
- `QualificationEvidence(string ProfileId, string HardwareDescription,
  DateTimeOffset StartedAt, DateTimeOffset FinishedAt, int ExpectedTasks,
  int CompletedTasks, int LostCompletedTasks, TimeSpan Deadline,
  double? Cost, bool ScoreComplete, string EvidencePath)` is a positional
  record with init-accessible properties so non-destructive `with` updates are
  supported, and exposes the derived `IsQualified` property; and
- `HostProfileService.CreateReport(IEnumerable<QualificationEvidence>,
  IEnumerable<ProfileRecommendation>)` returns a `QualificationReport` with
  schema version `"1.0"` and recommendations ordered by profile ID.

Publish the `on-prem-32` profile with target concurrency 32, minimum 16 CPUs,
minimum 64 GiB memory, and a 12-hour window. Qualification requires a positive
expected-task count, all expected tasks completed, no lost completed tasks,
elapsed time no greater than the deadline, and complete scoring evidence.
Recommendations never raise the operator's limits. Cap trial concurrency by
the profile target, twice the available CPUs, one trial per 2 GiB, and the
operator limit. Cap build concurrency by available CPUs and the operator build
limit.

Do not commit changes. The evaluator captures the working-tree patch after
implementation.
