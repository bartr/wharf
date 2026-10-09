# Build this override under its distinct local-only name:
#
#   docker build -t mars-base:latest mars-base-override
#
# Add --pull only when intentionally refreshing the upstream base. DeepSWE's
# Pier compatibility layer maps the upstream reference to this local image in
# temporary build contexts; checked-in task Dockerfiles remain unchanged.
#
# DO NOT DELETE: `docker image prune -a` / `docker system prune -a` removes this
# image and every task build then fails. Rebuild it before any benchmark run.
FROM public.ecr.aws/x8v8d7g8/mars-base:latest

# Override: the upstream mars-base ships a NodeSource apt repo
# (deb.nodesource.com/node_24.x) that is currently returning 403 Forbidden,
# which makes any `apt-get update` fail (exit 100) and breaks pier's agent
# install layer. Node.js itself is already installed in the base image, so we
# only need to drop the broken apt source + its version pins, then confirm
# `apt-get update` succeeds without it.
RUN set -eux; \
    rm -f /etc/apt/sources.list.d/nodesource.list \
          /etc/apt/preferences.d/nodejs \
          /etc/apt/preferences.d/nsolid; \
    grep -rl nodesource /etc/apt/ 2>/dev/null | xargs -r rm -f; \
    apt-get update; \
    node --version || true

# Preinstall benchmark CLIs once in the patched base image. Agent install layers
# still verify/install as a fallback, but custom agents skip their network-heavy
# install path when the binary is already available.
RUN set -eux; \
            apt-get update; \
            apt-get install -y curl ca-certificates; \
            if ! command -v npm >/dev/null 2>&1; then \
                  echo "npm is required to preinstall Copilot and Claude Code" >&2; \
                  exit 1; \
            fi; \
            if ! command -v copilot >/dev/null 2>&1; then \
                  npm install -g @github/copilot; \
            fi; \
            if ! command -v claude >/dev/null 2>&1; then \
                  npm install -g @anthropic-ai/claude-code; \
            fi; \
            if ! command -v muse >/dev/null 2>&1; then \
                  curl -fsSL https://dev.meta.ai/install.sh -o /tmp/muse-install.sh; \
                  bash /tmp/muse-install.sh; \
                  if [ -x /root/.local/bin/muse ]; then \
                        ln -sf /root/.local/bin/muse /usr/local/bin/muse; \
                  fi; \
                  rm -f /tmp/muse-install.sh; \
            fi; \
            copilot --version; \
            claude --version; \
            muse --version
