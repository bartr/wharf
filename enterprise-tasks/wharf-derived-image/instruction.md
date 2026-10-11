The Wharf 0.6.0 release has image pulling but no derived-image coordinator.
Add the derived image build pipeline from Wharf 0.7.0.

Implement derived-image coordination so that it:

- creates deterministic SHA-256 cache keys from base image, ordered scripts,
  sorted versions, and sorted configuration;
- gates base preparation, context preparation, and derived builds together;
- coalesces identical in-flight builds;
- reuses successful builds unless `ForceBuild` is requested;
- lets caller cancellation stop only that caller's wait;
- removes failed in-flight operations so later attempts can retry.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

IMPORTANT: Commit everything when you are done.
