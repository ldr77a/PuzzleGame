#!/bin/bash
# Compile-checks the Unity assemblies without the editor: Assembly-CSharp, Assembly-CSharp-Editor, and the
# Cameli EditMode tests. References and defines come from the .csproj files Unity generates (open the
# project in Unity once if they are missing). Only errors are fatal; warnings are shown for Cameli files.
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$PROJ"

for f in Assembly-CSharp.csproj Assembly-CSharp-Editor.csproj; do
    [ -f "$f" ] || { echo "$f not found: open the project in Unity to generate it." >&2; exit 1; }
done

ASMDEF_DIRS="$(find Assets -name '*.asmdef' -exec dirname {} \;)"
in_asmdef() {
    local file="$1" dir
    for dir in $ASMDEF_DIRS; do
        case "$file" in "$dir"/*) return 0 ;; esac
    done
    return 1
}

# Writes a csc response file with the references and defines of a generated .csproj.
csproj_rsp() {
    local csproj="$1" rsp="$2"
    : > "$rsp"
    grep -o '<HintPath>[^<]*</HintPath>' "$csproj" | sed -e 's/<[^>]*>//g' -e 's/^/-r:"/' -e 's/$/"/' >> "$rsp"
    echo "-define:$(sed -n 's:.*<DefineConstants>\(.*\)</DefineConstants>.*:\1:p' "$csproj" | head -1)" >> "$rsp"
}

compile() {
    local name="$1" rsp="$2"
    local log="$OUT/$name.log"
    if csc -target:library -warn:4 -nowarn:1701,1702 -out:"$OUT/$name.dll" @"$rsp" > "$log" 2>&1; then
        grep -E "Cameli.*warning" "$log" || true
        echo "$name: OK"
    else
        grep -E "error|Cameli.*warning" "$log" || cat "$log"
        echo "$name: FAILED"
        exit 1
    fi
}

build_core

# Assembly-CSharp: scripts outside Editor folders and outside assembly definitions.
RSP="$OUT/Assembly-CSharp.rsp"
csproj_rsp Assembly-CSharp.csproj "$RSP"
echo "-r:\"$OUT/Cameli.Core.dll\"" >> "$RSP"
while IFS= read -r file; do
    case "$file" in */Editor/*) continue ;; esac
    in_asmdef "$file" || echo "\"$file\"" >> "$RSP"
done < <(find Assets -name '*.cs' | sort)
compile Assembly-CSharp "$RSP"

# Assembly-CSharp-Editor: scripts inside Editor folders, outside assembly definitions.
RSP="$OUT/Assembly-CSharp-Editor.rsp"
csproj_rsp Assembly-CSharp-Editor.csproj "$RSP"
grep -v 'Library/ScriptAssemblies/Assembly-CSharp.dll' "$RSP" > "$RSP.tmp" && mv "$RSP.tmp" "$RSP"
echo "-r:\"$OUT/Cameli.Core.dll\"" >> "$RSP"
echo "-r:\"$OUT/Assembly-CSharp.dll\"" >> "$RSP"
while IFS= read -r file; do
    in_asmdef "$file" || echo "\"$file\"" >> "$RSP"
done < <(find Assets -name '*.cs' -path '*/Editor/*' | sort)
compile Assembly-CSharp-Editor "$RSP"

# Cameli EditMode tests (their own assembly definition, editor-only).
RSP="$OUT/Cameli.Tests.EditMode.rsp"
csproj_rsp Assembly-CSharp-Editor.csproj "$RSP"
grep -v 'Library/ScriptAssemblies/Assembly-CSharp' "$RSP" > "$RSP.tmp" && mv "$RSP.tmp" "$RSP"
echo "-r:\"$OUT/Cameli.Core.dll\"" >> "$RSP"
find Assets/Tests/EditMode/Cameli -name '*.cs' | sort | sed 's/.*/"&"/' >> "$RSP"
compile Cameli.Tests.EditMode "$RSP"
