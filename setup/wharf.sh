#!/bin/zsh

cd $HOME/wharf || { echo "Failed to change directory to $HOME/wharf"; exit 1; }

curl -fsSL https://gh.io/copilot-install | sudo bash

if [ -d "$HOME/.oh-my-zsh/completions" ]; then
    docker completion zsh > $HOME/.oh-my-zsh/completions/docker
fi

# build wharf
dotnet build

# copy wharf to the path - make sure $HOME/bin is on your PATH
mkdir -p $HOME/bin
cp -R $HOME/wharf/src/wharf/bin/Debug/net10.0/* $HOME/bin
