#!/usr/bin/env bash
# Publishes a self-contained win-x64 build to windows/out/ClickDoc, to copy into a Windows VM.
set -euo pipefail
cd "$(dirname "$0")/../.."
dotnet publish windows/ClickDoc -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o windows/out/ClickDoc
