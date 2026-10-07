# Session 17 Review

- The smoke fixture is deterministic and does not require Docker or network
  access to exercise the artifact and reporting path.
- Real host checks run before the fixture and remain visible in
  `host-check.json`.
- Docker and capacity failures remain blocking even when unsupported platform
  classification is overridden.
