#!/bin/zsh

curl -fsSL https://gh.io/copilot-install | sudo bash

docker completion zsh > $HOME/.oh-my-zsh/completions/docker

# build wharf
cd $HOME/wharf
dotnet build
