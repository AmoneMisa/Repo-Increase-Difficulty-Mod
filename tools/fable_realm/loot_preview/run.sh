#!/usr/bin/env bash
# Renders the Fable Realm valuables in the Unity validation project and prints their sizes.
# Usage (Git Bash, from the repository root):  tools/fable_realm/loot_preview/run.sh
# Output: .fable-validation/LootPreview/fable-loot-sheet.png and "FABLE LOOT" lines in loot.log.
set -euo pipefail
repo="$(cd "$(dirname "$0")/../../.." && pwd)"
project="$repo/.fable-validation"
unity="${UNITY_EDITOR:-/c/Program Files/Unity/Hub/Editor/6000.2.9f1/Editor/Unity.exe}"
dest="$project/Assets/FableLootPreview/Editor"
rm -rf "$project/Assets/FableLootPreview" "$project/Assets/FableLootPreview.meta"
mkdir -p "$dest"
cp "$repo/tools/fable_realm/loot_preview/FableLootPreview.cs" "$dest/"
# Unity compiles C# 9: turn the file-scoped namespace of each production source into a block.
for source in src/Valuables/FableLoot.cs src/Valuables/CollectionCatalog.cs src/Visuals/ProcMesh.cs; do
  python - "$repo/$source" "$dest/$(basename "$source")" <<'PY'
import io, re, sys
text = io.open(sys.argv[1], encoding='utf-8-sig').read().replace('\r\n', '\n')
text, count = re.subn(r'^namespace ([\w.]+);\s*$', r'namespace \1 {', text, count=1, flags=re.M)
io.open(sys.argv[2], 'w', encoding='utf-8', newline='\n').write(text + ('\n}\n' if count else ''))
PY
done
win() { cygpath -w "$1" 2>/dev/null || echo "$1"; }
"$unity" -batchmode -quit -projectPath "$(win "$project")" -executeMethod FableLootPreview.Run -logFile "$(win "$project/loot.log")" || true
grep -E "FABLE LOOT|error CS|Exception" "$project/loot.log" || true
# The preview sources must not stay in the project the room bundle is built from.
rm -rf "$project/Assets/FableLootPreview" "$project/Assets/FableLootPreview.meta"
