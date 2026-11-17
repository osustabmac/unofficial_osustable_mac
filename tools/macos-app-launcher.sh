#!/bin/sh
set -eu

app_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
export FONTCONFIG_FILE="$app_dir/fonts.conf"
export FONTCONFIG_PATH="$app_dir"
export PANGOCAIRO_BACKEND="coretext"
exec "$app_dir/osu-stable-bin" "$@"
