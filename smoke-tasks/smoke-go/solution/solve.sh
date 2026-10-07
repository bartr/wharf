#!/bin/bash
# Reference solution for smoke-go. Applies the one-line fix and commits it
# the way a normal submission would (only committed work is graded).
cd /app

git apply --whitespace=nowarn /solution/solution.patch

git checkout -b feature/solution 2>/dev/null || true
git add -A
git -c user.name="oracle" -c user.email="oracle@local" commit -q --no-verify -m "Apply reference solution" || true
