The Wharf 0.5.0 release can prepare Docker images but does not have bounded
image-pull coordination. Add the image pull control boundary from Wharf 0.6.0.

Implement image pull coordination so that it:

- reuses images already present locally without registry access;
- retries only explicit registry rate-limit responses with bounded jittered
  backoff;
- tracks registry pressure and decays it after successful pulls;
- coalesces duplicate image pulls;
- records per-attempt and aggregate pull telemetry;
- exposes verbosity-controlled retry and summary output.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

IMPORTANT: Commit everything when you are done.
