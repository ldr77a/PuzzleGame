# Shared setup for the harness scripts: uses the C# compiler and .NET runtime bundled with the Unity
# editor version in ProjectSettings/ProjectVersion.txt, so nothing extra needs installing and the
# scripts work while the editor has the project open. Set UNITY_SCRIPTING to use another install.

HARNESS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$(cd "$HARNESS/../.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")"
S="${UNITY_SCRIPTING:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting}"
DOTNET="$S/NetCoreRuntime/dotnet"
RT="$(ls -d "$S"/NetCoreRuntime/shared/Microsoft.NETCore.App/* | head -1)"
OUT="$HARNESS/out"
mkdir -p "$OUT"

if [ ! -x "$DOTNET" ]; then
    echo "Unity $UNITY_VERSION scripting tools not found at $S (set UNITY_SCRIPTING)." >&2
    exit 1
fi

csc() {
    "$DOTNET" "$S/DotNetSdkRoslyn/csc.dll" -nologo -langversion:9.0 -noconfig -nostdlib "$@"
}

# References for code that runs on the bundled .NET runtime (tests, LevelLab).
runtime_refs() {
    ls "$RT"/*.dll | grep -v -E 'Native|mscordaccore|mscordbi|clrjit|coreclr|hostpolicy|hostfxr' | sed 's/^/-r:/'
}

write_runtimeconfig() {
    printf '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
        "$(basename "$RT")" > "$1"
}

# Compiles into a private temp folder under the final file name (csc takes the assembly name from it, so a
# temp file name would leak into the assembly identity), then moves the file into $OUT. The move is atomic,
# so harness processes already running keep their copy.
#   build_swapped <file name> <csc args...>
build_swapped() {
    local name="$1"; shift
    local tmp="$OUT/tmp.$$"
    mkdir -p "$tmp"
    if ! csc "$@" -out:"$tmp/$name"; then
        rm -rf "$tmp"
        return 1
    fi
    mv -f "$tmp/$name" "$OUT/$name"
    rm -rf "$tmp"
}

# Cameli.Core, compiled the way Unity does (netstandard 2.1, no engine references). Rebuilds only when a
# source changed.
build_core() {
    local dll="$OUT/Cameli.Core.dll"
    if [ -f "$dll" ] && [ -z "$(find "$PROJ/Assets/Scripts/Cameli/Core" -name '*.cs' -newer "$dll")" ]; then
        return 0
    fi
    build_swapped Cameli.Core.dll -target:library -warn:4 -r:"$S/NetStandard/ref/2.1.0/netstandard.dll" \
        "$PROJ"/Assets/Scripts/Cameli/Core/*.cs
}
