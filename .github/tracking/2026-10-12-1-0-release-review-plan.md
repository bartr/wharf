# 1.0 Release Review Plan

1. [x] Fix cancellation and Docker cleanup safety.
2. [x] Apply effective credentials and phase timeout overrides.
3. [x] Atomically persist terminal results and validate them during recovery.
4. [x] Persist exact selected tasks and secret-free effective configuration for
   faithful resume.
5. [x] Delete owned verifier workspaces and terminate cancelled host verifiers.
6. [x] Add focused regression tests for every fix.
7. [x] Run focused suites, then the full unit test suite and release build.
