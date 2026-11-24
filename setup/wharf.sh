#!/bin/zsh
set -euo pipefail

cd $HOME/wharf || { echo "Failed to change directory to $HOME/wharf"; exit 1; }

if [ -d "$HOME/.oh-my-zsh/completions" ]; then
    docker completion zsh > $HOME/.oh-my-zsh/completions/docker
fi

# build wharf
dotnet build
docker pull public.ecr.aws/x8v8d7g8/mars-base:latest

curl -fsSL https://gh.io/copilot-install | sudo bash
curl -fsSL https://claude.ai/install.sh | bash
curl -fsSL https://chatgpt.com/codex/install.sh | sh
curl -fsSL https://dev.meta.ai/install.sh | bash
