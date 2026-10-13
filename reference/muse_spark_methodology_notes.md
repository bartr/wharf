# Methodology note

These reported results should be interpreted as model-plus-agent outcomes under a common isolated evaluation framework, not as pure model-only rankings or strict replications of official leaderboard harnesses. The setup aligns closely on dataset, verifier semantics, sandbox isolation, and per-model maximum reasoning settings, but it is intentionally not harness-identical in some cases (for example, model-selected agent products versus mini-swe-agent on official leaderboards), and third-party evaluations are described as best-effort rather than vendor-tuned. As a result, cross-system deltas can reflect a combination of model capability, agent behavior, and harness/tooling fit.

## Sources

- Meta Research post: https://research.meta.ai/blog/introducing-muse-code-and-muse-spark-1-2
- Meta methodology PDF: https://research.meta.ai/static/muse-spark-1-2-methodology
- DeepSWE v1.1 blog: https://deepswe.datacurve.ai/blog/deepswe-v1-1
- Terminal-Bench 2.1 benchmark page: https://www.tbench.ai/benchmarks/terminal-bench-2-1

## PDF excerpt: Software engineering DeepSWE v1.1

Software engineering DeepSWE v1.1: Datacurve's DeepSWE paper defines 113 tasks across 91 repositories and five languages: TypeScript, Go, Python, JavaScript, and Rust. Each task has a handwritten functional verifier and regression checks. We use the Harbor-format dataset and run tasks in isolated Daytona cloud sandboxes. We attempt to follow the benchmark's then-official Pier v0.3.0 runner as closely as possible. The agent's final patch is applied to a pristine checkout in a fresh verifier container. External internet access is blocked during rollout and grading, and only the model endpoint remains reachable. A task passes only when its functional verifier and regression checks both succeed. The official leaderboard uses mini-swe-agent for every model. Our evaluation instead uses each model's selected agent product and therefore is not harness-identical to the leaderboard. We report the average task success rate (pass@1) across five attempts.

## PDF excerpt: Overall Methodology

Overall Methodology

- Models and agents. Where comparable results are available, we compare Muse Spark 1.2 with Muse Spark 1.1, Grok 4.5, Claude Opus 5, GPT-5.6 Terra, Gemini 3.6 Flash, and Kimi K3. For Terminal-Bench 2.1 and DeepSWE 1.1, each model is evaluated with its selected agent product: Muse Code for Muse Spark 1.2, mini-swe-agent for Muse Spark 1.1, Grok Build for Grok, Claude Code for Opus, Codex for GPT, Antigravity for Gemini, and Kimi Code for Kimi. We use the maximum available reasoning strength for each model: xhigh reasoning effort for Muse Spark 1.2 and Muse Spark 1.1, high for Grok and Gemini, and max for Opus, GPT, and Kimi. Muse Spark 1.2 is served through the Meta Model API. GDPVal-AA v2 and MCP Atlas use the benchmark providers' agent harnesses and therefore are not CLI agent comparisons.
- Evaluation framework. Our Terminal-Bench 2.1 and DeepSWE 1.1 runs use an internal agent evaluation framework. Each task attempt runs the selected agent and model in an isolated Daytona cloud sandbox. We use the official benchmark datasets or faithfully convert them to Harbor format. Each benchmark's verifier grades the final result. GDPVal-AA v2 results come from Artificial Analysis, and MCP Atlas results come from Scale AI. Meta Internal Coding Bench uses a separate internal agentic harness and dedicated grading containers.
- Agentic evaluations for third-party models. The results represent our best-effort evaluations of third-party models. Where applicable, we use a common evaluation framework and align benchmark settings as closely as is practical. We note that our evaluation setup (e.g., agent tools and system prompts) may not be specifically tuned for proprietary third-party models. Therefore, the results may not reflect these models' best performance when used in environments tailored to their specific strengths.
