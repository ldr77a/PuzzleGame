#!/bin/bash
# Renders Cameli's layers (cameli.html) to transparent 512 x 512 PNGs in Assets/Resources/CameliArt/, which
# CameliFigure loads at runtime, and the app icon (icon.html) to 1024 x 1024 PNGs in Assets/CameliIcon/, which the
# player settings point at ("Cameli → 모바일 프로젝트 설정 적용"). Needs Google Chrome. Run after editing cameli.js,
# cameli.html or icon.html, then focus the Unity Editor.
#
#   Tools/CameliArt/render.sh
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
art="$here/../../Assets/Resources/CameliArt"
icons="$here/../../Assets/CameliIcon"
chrome="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"

shot() { # page, size, background (RRGGBBAA), output
  "$chrome" --headless=new --disable-gpu --hide-scrollbars --default-background-color="$3" \
    --window-size="$2,$2" --force-device-scale-factor=1 \
    --screenshot="$4" "file://$here/$1" >/dev/null 2>&1
  echo "${4##*/}"
}

mkdir -p "$art" "$icons"
for layer in body_line tail_fill tail_line body_fill face_normal face_happy face_oops; do
  shot "cameli.html?layer=$layer" 512 00000000 "$art/$layer.png"
done
for part in ios round android_bg android_fg; do
  shot "icon.html?part=$part" 1024 00000000 "$icons/icon_$part.png"
done
