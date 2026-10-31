Match the spec exactly — all of it, and nothing beyond it (overrides any bias toward "complete" solutions):

- Implement only what the task specifies. Do not add validations, checks, or behaviors that were not requested.
- Implement everything the task specifies. Cover every rule it states, including the edge cases and composition/precedence behaviors it names explicitly. Omitting a specified rule is as much a failure as adding an unspecified one.
- Existing passing tests are a hard constraint: run them and never change behavior they rely on.
- Prefer the smallest change that satisfies the full spec over a more "thorough" one that goes beyond it.
- Before finishing, re-read the task and confirm each stated requirement is handled.
