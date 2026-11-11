#!/bin/bash
set -uo pipefail
trap 'if [ ! -f /logs/verifier/reward.json ] && [ ! -f /logs/verifier/reward.txt ]; then mkdir -p /logs/verifier; echo -1 > /logs/verifier/reward.txt; fi' EXIT

cd /app || exit 6
python3 /tests/grader.py prepare || exit $?
[ -f /logs/verifier/reward.json ] && exit 0

mkdir -p /logs/verifier
run_test() {
  local filter="$1"
  local report_name="$2"
  local status
  if dotnet test --no-restore --configuration Release \
      --filter "FullyQualifiedName~${filter}" \
      >"/logs/verifier/${report_name}.log" 2>&1; then
    status="passed"
  else
    status="failed"
  fi
  python3 - "$report_name" "$filter" "$status" <<'PY'
import json
import sys
from pathlib import Path
report_name, test_name, status = sys.argv[1:]
Path("/logs/verifier/" + report_name + ".json").write_text(json.dumps({
    "results": {
        "tool": {"name": "dotnet-smoke-runner"},
        "tests": [{"name": test_name, "suite": ["WharfRelease"], "status": status}],
    }
}))
PY
}

run_test "RunConfigurationTests.PrecedenceIsFlagThenEnvironmentThenManifestThenDefault" base-ctrf
run_test "DeepSweWharfTests.AddsIsolatedAgentExecutionBoundary" contracts-ctrf
run_test "DeepSweWharfTests.AddsVersionedAgentEvidenceContract" operations-ctrf
run_test "DeepSweWharfTests.AgentRequestExposesHiddenPathAndWorkspaceFields" compatibility-ctrf
python3 /tests/grader.py grade
