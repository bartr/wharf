#!/bin/zsh
set -eu

cd $HOME/wharf
git pull

# build wharf
dotnet build
cp -R $HOME/wharf/src/wharf/bin/Debug/net10.0/* /tmp

# smoke test
/tmp/wharf smoke
