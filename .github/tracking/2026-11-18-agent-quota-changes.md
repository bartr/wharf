# Agent quota implementation

- Added public `AgentQuotaFailure(Reason, Reset = null)` with `ErrorType = "AgentQuota"`.
- Appended optional `AgentStarting` and `QuotaDetected` callbacks to
  `AgentExecutionRequest`, and optional `Quota` to `AgentRunResult` and
  `AgentEvidenceSummary`.
- Added provider-specific structured error recognition, explicit credit/usage
  exhaustion messages, rejected Claude subscription windows, validated timestamp
  resets, and normalized reset clocks. Raw messages never enter normalized quota
  metadata. Generic 429, network, catalog, overload, ordinary assistant, and tool
  content do not trigger quota.
- The service fails quota results regardless of patch/exit code, retains typed
  result evidence and reset metadata, notifies synthetic runners' quota results,
  deduplicates notifications, and never performs a quota infrastructure retry.
- `QuotaPaused` start exceptions propagate without attempting to read result.json.
- Start callbacks run at service admission (including synthetic runners/retries)
  and immediately before real Docker exec. Callbacks must be idempotent.
- Provider commands mirror both streams through tee into retained container logs;
  transport callbacks receive completed lines live, retaining exact output.
- Added parser/service/live local-process/provider-shell tests and extended the
  existing infrastructure-retry test to verify start callbacks on both starts.

Validation: test-project build succeeded, zero warnings/errors; focused suite
passed 114/114 in tmux `wharf-agent-quota-20261118-b`; scoped whitespace
verification and `git diff --check` passed.
