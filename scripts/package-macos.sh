#!/usr/bin/env bash
# Empacota o Pressio.Desktop como um app macOS (.app) com ícone e assinatura ad-hoc.
# Uso: ./scripts/package-macos.sh [runtimes]   (ex.: osx-arm64, osx-x64)
set -euo pipefail
cd "$(dirname "$0")/.."

RID="${1:-osx-arm64}"
CONFIG="${CONFIG:-Release}"
NAME="Pressio.app"
ROOT="Pressio.Desktop"

echo ">> Publicando ($CONFIG, $RID)..."
dotnet publish "$ROOT/$ROOT.csproj" -c "$CONFIG" -r "$RID" --self-contained false -o "$ROOT/bin/publish-macos"

echo ">> Montando $NAME ..."
APP="$ROOT/bin/$NAME"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$ROOT/bin/publish-macos/." "$APP/Contents/MacOS/"
cp "$ROOT/macos/Info.plist" "$APP/Contents/Info.plist"
cp Pressio/Assets/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
chmod +x "$APP/Contents/MacOS/Pressio.Desktop"

echo ">> Assinando (ad-hoc)..."
codesign --force --deep --sign - "$APP"

echo ">> Abrindo..."
open "$APP"

echo "Pronto: $APP"
