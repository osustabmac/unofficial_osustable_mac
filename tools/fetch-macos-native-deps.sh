#!/bin/sh
set -eu

repo_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
target_dir="$repo_dir/work/macos-native"
mkdir -p "$target_dir"

fetch_library() {
  url=$1
  archive=$2
  member=$3
  output=$4

  if [ -f "$target_dir/$output" ]; then
    return
  fi

  curl -L --fail --silent --show-error -o "$target_dir/$archive" "$url"
  unzip -p "$target_dir/$archive" "$member" > "$target_dir/$output"
  chmod 755 "$target_dir/$output"
}

fetch_library "https://www.un4seen.com/files/bass24-osx.zip" "bass24-osx.zip" "libbass.dylib" "libbass.dylib"
fetch_library "https://www.un4seen.com/files/z/0/bass_fx24-osx.zip" "bass_fx24-osx.zip" "libbass_fx.dylib" "libbass_fx.dylib"
