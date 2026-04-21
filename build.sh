#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TOOL_PROJECT="$SCRIPT_DIR/src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj"
PACK_PROJECT="$SCRIPT_DIR/src/NuSpec.AI/NuSpec.AI.csproj"
PUBLISH_DIR="$SCRIPT_DIR/src/NuSpec.AI/tools/net8.0"
OUTPUT_DIR="$SCRIPT_DIR/artifacts"

mkdir -p "$OUTPUT_DIR"

echo "==> Publishing NuSpec.AI.Tool..."
dotnet publish "$TOOL_PROJECT" -c Release -o "$PUBLISH_DIR" --no-self-contained

echo "==> Packing NuSpec.AI..."
dotnet pack "$PACK_PROJECT" -c Release -o "$OUTPUT_DIR" --no-build

echo "==> Done! Package at: $OUTPUT_DIR"
ls -la "$OUTPUT_DIR"/*.nupkg
