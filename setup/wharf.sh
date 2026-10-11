#!/bin/zsh

curl -fsSL https://gh.io/copilot-install | sudo env VERSION=1.0.95 bash

docker completion zsh > $HOME/.oh-my-zsh/completions/docker

# build wharf
cd $HOME/wharf
dotnet build
