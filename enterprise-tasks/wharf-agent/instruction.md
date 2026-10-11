The Wharf 0.8.0 release has Copilot preparation but no isolated agent
execution boundary. Add the agent execution slice from Wharf 0.9.0.

Implement agent execution so that it:

- validates distinct absolute source and workspace mounts;
- rejects hidden verifier paths inside either mount;
- runs the prepared Copilot agent with the configured network;
- retains stdout, stderr, trajectory, patch, timing, usage, and result evidence;
- classifies empty patches as unfinished rather than success;
- preserves evidence before reporting execution failures.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

IMPORTANT: Commit everything when you are done.
