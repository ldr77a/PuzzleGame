#!/bin/bash
# Stage design helper built on Cameli.Core (same rules as the game).
#   ./lab.sh check [dir]          analyze stage JSON files (default: Assets/Resources/CameliStages)
#   ./lab.sh search key=value…    random-fill a template and print the best candidates
# search keys: template="? ? ?|? S ?|? ? ?" colors=RYB target=RRB [gates=RY,YB] [randwalls=N]
#              [opt=MIN-MAX] [amb=ΔE] [n=tries] [seed=N] [top=N] [nohub] [anycolors]
set -euo pipefail
source "$(dirname "$0")/env.sh"

build_core
if [ ! -f "$OUT/LevelLab.dll" ] || [ "$HARNESS/LevelLab.cs" -nt "$OUT/LevelLab.dll" ] || [ "$OUT/Cameli.Core.dll" -nt "$OUT/LevelLab.dll" ]; then
    csc -target:exe -optimize+ -nowarn:1701,1702 $(runtime_refs) -r:"$OUT/Cameli.Core.dll" \
        -out:"$OUT/LevelLab.$$.dll" "$HARNESS/LevelLab.cs"
    mv -f "$OUT/LevelLab.$$.dll" "$OUT/LevelLab.dll"   # atomic swap: running searches keep their copy
    write_runtimeconfig "$OUT/LevelLab.runtimeconfig.json"
fi

if [ "${1:-}" = "check" ] && [ $# -lt 2 ]; then
    set -- check "$PROJ/Assets/Resources/CameliStages"
fi
"$DOTNET" exec "$OUT/LevelLab.dll" "$@"
