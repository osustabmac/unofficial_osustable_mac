#!/bin/sh
set -eu

repo_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repo_dir"

tools/fetch-macos-native-deps.sh
tools/fetch-macos-drawing-runtime.sh

mkdir -p work/mac-decompiled
find work/mac-decompiled -depth -delete
cp -R work/normalized-decompiled/. work/mac-decompiled/

tools/dotnet10/dotnet run --project tools/MacRecoveredSourceFixups -- \
  work/mac-decompiled

tools/dotnet10/dotnet build tools/OriginalEngineMacCompile -c Release --no-incremental
