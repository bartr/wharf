#!/bin/zsh
set -eu

cd $HOME/wharf
git pull

# build wharf
dotnet build

# smoke test
./wharf smoke --include smoke-dotnet
