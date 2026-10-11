The Wharf 0.7.0 release has image preparation but no Copilot adapter boundary.
Add the Copilot adapter and secret boundary from Wharf 0.8.0.

Implement the Copilot adapter so that it:

- requires exact requested/effective three-part CLI version equality;
- normalizes native and frontier effort values;
- emits model, effort, subagent, and endpoint arguments;
- allows only explicitly allowlisted HTTPS endpoints;
- supports environment, file, and stdin credential sources;
- retains credential source metadata without exposing credential values.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

IMPORTANT: Commit everything when you are done.
