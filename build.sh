#!/usr/bin/env sh
set -eu
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
for project in src/Emby.AudioMemory/Emby.AudioMemory.csproj tests/Unit/Unit.csproj tests/Behavior/Behavior.csproj; do
  dotnet restore "$project"
done
dotnet build src/Emby.AudioMemory/Emby.AudioMemory.csproj -c Release --no-restore
dotnet run --project tests/Unit/Unit.csproj -c Release --no-restore
dotnet run --project tests/Behavior/Behavior.csproj -c Release --no-restore
mkdir -p artifacts
cp src/Emby.AudioMemory/bin/Release/netstandard2.0/Emby.AudioMemory.dll artifacts/Emby.AudioMemory.dll
(
  cd artifacts
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum Emby.AudioMemory.dll > SHA256SUMS
  else
    shasum -a 256 Emby.AudioMemory.dll > SHA256SUMS
  fi
)
