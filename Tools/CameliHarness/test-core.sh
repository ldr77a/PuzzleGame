#!/bin/bash
# Builds Cameli.Core and runs the engine-free EditMode tests (CoreTests.cs) outside Unity.
set -euo pipefail
source "$(dirname "$0")/env.sh"

build_core
NUNIT="$(ls "$PROJ"/Library/PackageCache/com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll | head -1)"
csc -target:exe -nowarn:1701,1702 $(runtime_refs) -r:"$OUT/Cameli.Core.dll" -r:"$NUNIT" \
    -out:"$OUT/CoreTests.dll" "$PROJ/Assets/Tests/EditMode/Cameli/CoreTests.cs" "$HARNESS/TestRunner.cs"
cp "$NUNIT" "$OUT/"
write_runtimeconfig "$OUT/CoreTests.runtimeconfig.json"
"$DOTNET" exec "$OUT/CoreTests.dll"
