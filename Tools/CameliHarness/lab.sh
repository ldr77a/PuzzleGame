#!/bin/bash
# Stage design helper built on Cameli.Core (same rules as the game).
#   ./lab.sh check [dir]          analyze stage JSON files (default: Assets/Resources/CameliStages)
#   ./lab.sh search key=value…    random-fill a template and print the best candidates
#   ./lab.sh solve key=value…     print a shortest solution step by step: rows="…|…" target=RRB [gates=RY]
# search keys: template="? ? ?|? S ?|? ? ?" colors=RYB target=RRB [gates=RY,YB] [randwalls=N]
#              [opt=MIN-MAX] [amb=ΔE (default 10, also applied to gates)] [n=tries] [seed=N] [top=N]
#              [sort=walk] [nohub] [anycolors] [wallsmatter]
#   colors= is the tile palette (letters from ROYGBNP); every color must appear unless anycolors.
#   wallsmatter keeps only boards where turning any wall into a tile color shortens the optimal clear or adds an optimal eaten-cell set.
# check columns: ΔE = nearest other recipe with the target's drop count from the board's colors (CIELAB
#   ΔE76, FAIL below 10, design doc 3.2); gateΔE = same per gate recipe; tileΔE = closest two tile colors;
#   walk% = chance a random walk (uniform over legal moves) clears within optimal + 2 moves.
set -euo pipefail
source "$(dirname "$0")/env.sh"

build_core
if [ ! -f "$OUT/LevelLab.dll" ] || [ "$HARNESS/LevelLab.cs" -nt "$OUT/LevelLab.dll" ] || [ "$OUT/Cameli.Core.dll" -nt "$OUT/LevelLab.dll" ]; then
    build_swapped LevelLab.dll -target:exe -optimize+ -nowarn:1701,1702 $(runtime_refs) -r:"$OUT/Cameli.Core.dll" \
        "$HARNESS/LevelLab.cs"
    write_runtimeconfig "$OUT/LevelLab.runtimeconfig.json"
fi

if [ "${1:-}" = "check" ] && [ $# -lt 2 ]; then
    set -- check "$PROJ/Assets/Resources/CameliStages"
fi
"$DOTNET" exec "$OUT/LevelLab.dll" "$@"
