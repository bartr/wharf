#!/bin/zsh
set -eu

cd $HOME/wharf
git pull

# build base image
docker build -t mars-base:latest mars-base-override

# build wharf
dotnet build

# smoke test
./wharf smoke
