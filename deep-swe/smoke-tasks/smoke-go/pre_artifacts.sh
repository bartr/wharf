#!/bin/bash
# Capture the agent's committed work as the submission artifact: the diff
# between the task's base commit and the agent's final HEAD. Only committed work
# is graded. Keep this SHA in sync with the deterministic base commit produced
# by the Dockerfiles and tests/config.json.
set -uo pipefail
cd /app || exit 0
mkdir -p /logs/artifacts
git config --global --add safe.directory /app 2>/dev/null || true
git diff --binary 62b380d84cd04ad32ff8056a59fe9c57b2601a21 HEAD > /logs/artifacts/model.patch 2>/dev/null || true
echo "[pre_artifacts] captured $(wc -c < /logs/artifacts/model.patch) bytes"
