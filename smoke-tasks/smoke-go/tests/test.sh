#!/bin/bash
# Verifier entrypoint for the smoke-go infra-test task.
# Applies the agent patch + hidden test patch (grader.py prepare), runs the two
# Go suites through go-ctrf-json-reporter, then scores them (grader.py grade).
set -uo pipefail
trap 'if [ ! -f /logs/verifier/reward.json ] && [ ! -f /logs/verifier/reward.txt ]; then mkdir -p /logs/verifier; echo -1 > /logs/verifier/reward.txt; fi' EXIT
log() { echo "[verifier] $*"; }
cd /app || { mkdir -p /logs/verifier; exit 6; }

python3 /tests/grader.py prepare || exit $?
[ -f /logs/verifier/reward.json ] && exit 0   # model.patch didn't apply -> graded 0

export RUN_LOG=/logs/verifier/run.log
: > "$RUN_LOG" 2>/dev/null || true

export PATH="$(go env GOPATH 2>/dev/null)/bin:$PATH"
export GOCACHE="${GOCACHE:-/app/.gocache}"
command -v go >/dev/null 2>&1               || { log "ERROR: go missing; PATH=$PATH"; exit 127; }
command -v go-ctrf-json-reporter >/dev/null 2>&1 || { log "ERROR: go-ctrf-json-reporter missing; PATH=$PATH"; exit 127; }

# base suite (pass-to-pass) and new suite (fail-to-pass). Build-fail JSON events
# are filtered out (the reporter chokes on them); the reporter's own exit code is
# never gated on -- a missing/invalid CTRF grades as all-missing (= failed).
set +e
go test -json -count=1 -timeout 120s -run 'TestGreetNonEmpty' ./... 2>>"$RUN_LOG" | grep -v '"Action":"build-' | tee -a "$RUN_LOG" | go-ctrf-json-reporter -quiet -output /logs/verifier/base-ctrf.json
go test -json -count=1 -timeout 120s -run 'TestGreetHello'    ./... 2>>"$RUN_LOG" | grep -v '"Action":"build-' | tee -a "$RUN_LOG" | go-ctrf-json-reporter -quiet -output /logs/verifier/new-ctrf.json
set -e

echo "===== raw suite output ====="
[ -s "$RUN_LOG" ] && cat "$RUN_LOG"
echo "===== grade ====="
python3 /tests/grader.py grade
log "reward.json=$(cat /logs/verifier/reward.json 2>/dev/null)"
