# Agent quota boundary plan

1. [x] Append optional quota/start callbacks and result metadata in AgentContracts.
   Add conservative Claude/Codex structured error recognition with normalized metadata.
2. [x] Fail quota results with retained AgentQuota evidence even for exit zero/patch.
   Preserve existing failure behavior; never infrastructure-retry quota or swallow
   QuotaPaused as a missing result.
3. [x] Invoke start callback immediately before Docker exec, mirror provider logs,
   and add optional process line callbacks. Notify live and retain detected quota.
4. [x] Add focused parser, service, and live transport tests. Validate build and
   focused agent/provider tests in a uniquely named tmux session, without paid runs.

Exit criteria: explicit provider exhaustion notifies before process exit, secrets
cannot leak into normalized metadata, quota cannot pass partial patches or retry,
and generic Copilot/agent/provider tests remain passing.
