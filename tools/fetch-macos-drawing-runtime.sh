#!/bin/sh
set -eu

repo_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
runtime_dir="$repo_dir/work/macos-native/drawing"
if [ -f "$runtime_dir/.complete" ]; then
  exit 0
fi

download_dir="$repo_dir/work/macports-packages"
root_dir="$repo_dir/work/macports-root"
queue_file="$repo_dir/work/macports-queue.txt"
seen_file="$repo_dir/work/macports-seen.txt"
mkdir -p "$download_dir" "$root_dir" "$runtime_dir"
: > "$seen_file"
printf '%s\n' 'libgdiplus-6.0.5_3+x11' > "$queue_file"

while [ -s "$queue_file" ]; do
  spec=$(sed -n '1p' "$queue_file")
  sed '1d' "$queue_file" > "$queue_file.next"
  mv "$queue_file.next" "$queue_file"
  if grep -Fqx "$spec" "$seen_file"; then
    continue
  fi
  printf '%s\n' "$spec" >> "$seen_file"

  port_name=$(printf '%s' "$spec" | sed -E 's/-[0-9].*$//')
  index_url="https://packages-origin.macports.org/$port_name/"
  package_name=$(curl -L --fail --silent --show-error "$index_url" |
    sed -n 's/.*href="\([^"]*\.darwin_27\.arm64\.tbz2\)".*/\1/p' |
    grep -F "$spec" | head -n 1)
  if [ -z "$package_name" ]; then
    echo "Skipping non-runtime MacPorts dependency $spec" >&2
    continue
  fi

  archive="$download_dir/$package_name"
  if [ ! -f "$archive" ]; then
    curl -L --fail --silent --show-error -o "$archive" "$index_url$package_name"
  fi
  tar -xjf "$archive" -C "$root_dir"
  tar -xOf "$archive" ./+CONTENTS 2>/dev/null |
    sed -n 's/^@pkgdep //p' >> "$queue_file"
done

find "$root_dir/opt/local/lib" -name '*.dylib' -exec cp -Lf {} "$runtime_dir" \;
find "$runtime_dir" -type f -name '*.dylib' | while IFS= read -r library; do
  install_name_tool -id "@loader_path/$(basename "$library")" "$library" 2>/dev/null || true
  otool -L "$library" | sed -n 's/^[[:space:]]*\(\/opt\/local\/lib\/[^ ]*\).*/\1/p' |
    while IFS= read -r dependency; do
      install_name_tool -change "$dependency" "@loader_path/$(basename "$dependency")" "$library"
    done
  codesign --force --sign - "$library" >/dev/null 2>&1
done

if [ -f "$runtime_dir/libgdiplus.0.dylib" ]; then
  cp -f "$runtime_dir/libgdiplus.0.dylib" "$runtime_dir/libgdiplus.dylib"
fi
touch "$runtime_dir/.complete"
