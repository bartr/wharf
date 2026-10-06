#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet format wharf.slnx --verify-no-changes
dotnet build wharf.slnx --configuration Release --no-restore
dotnet test wharf.slnx --configuration Release --no-build
