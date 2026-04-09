#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TOOL_PROJECT="$SCRIPT_DIR/src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj"
FREE_PACK_PROJECT="$SCRIPT_DIR/src/NuSpec.AI/NuSpec.AI.csproj"
PRO_PACK_PROJECT="$SCRIPT_DIR/src/NuSpec.AI.Pro/NuSpec.AI.Pro.csproj"
ATTR_PACK_PROJECT="$SCRIPT_DIR/src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj"
FREE_PUBLISH_DIR="$SCRIPT_DIR/src/NuSpec.AI/tools/net8.0"
PRO_PUBLISH_DIR="$SCRIPT_DIR/src/NuSpec.AI.Pro/tools/net8.0"
OUTPUT_DIR="$SCRIPT_DIR/artifacts"

mkdir -p "$OUTPUT_DIR"

echo "==> Publishing NuSpec.AI.Tool (shared binary)..."
dotnet publish "$TOOL_PROJECT" -c Release -o "$FREE_PUBLISH_DIR" --no-self-contained

echo "==> Copying tool binary to NuSpec.AI.Pro..."
cp -r "$FREE_PUBLISH_DIR/." "$PRO_PUBLISH_DIR/"

echo "==> Packing NuSpec.AI..."
dotnet pack "$FREE_PACK_PROJECT" -c Release -o "$OUTPUT_DIR" --no-build

echo "==> Packing NuSpec.AI.Pro..."
dotnet pack "$PRO_PACK_PROJECT" -c Release -o "$OUTPUT_DIR" --no-build

echo "==> Packing NuSpec.AI.Attributes..."
dotnet pack "$ATTR_PACK_PROJECT" -c Release -o "$OUTPUT_DIR"

echo "==> Done! Packages at: $OUTPUT_DIR"
ls -la "$OUTPUT_DIR"/*.nupkg
