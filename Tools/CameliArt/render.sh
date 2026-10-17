#!/bin/bash
# Renders Cameli's layers (cameli.html) to transparent 512 x 512 PNGs in Assets/Resources/CameliArt/, which
# CameliFigure loads at runtime. Needs Google Chrome. Run after editing cameli.html, then focus the Unity Editor.
#
#   Tools/CameliArt/render.sh
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../Assets/Resources/CameliArt"
chrome="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
size=512

mkdir -p "$out"
for layer in body_line tail_fill tail_line body_fill face_normal face_happy face_oops; do
  "$chrome" --headless=new --disable-gpu --hide-scrollbars --default-background-color=00000000 \
    --window-size=$size,$size --force-device-scale-factor=1 \
    --screenshot="$out/$layer.png" "file://$here/cameli.html?layer=$layer" >/dev/null 2>&1
  echo "$layer.png"
done
