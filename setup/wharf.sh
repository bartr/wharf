#!/bin/zsh
set -euo pipefail

cd $HOME/wharf || { echo "Failed to change directory to $HOME/wharf"; exit 1; }

if ! command -v npm >/dev/null 2>&1; then
    echo "Node.js and npm are required to install Codex." >&2
    exit 1
fi

curl -fsSL https://gh.io/copilot-install | sudo bash
curl -fsSL https://claude.ai/install.sh | bash
curl -fsSL https://chatgpt.com/codex/install.sh | sh
curl -fsSL https://dev.meta.ai/install.sh | bash

if [ -d "$HOME/.oh-my-zsh/completions" ]; then
    docker completion zsh > $HOME/.oh-my-zsh/completions/docker
fi

# build wharf
dotnet build

# copy wharf to the path - make sure $HOME/bin is on your PATH
mkdir -p $HOME/bin
cp -R $HOME/wharf/src/wharf/bin/Debug/net10.0/* $HOME/bin
