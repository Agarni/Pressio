#!/usr/bin/env bash
# Compila, instala e abre o Pressio.iOS no iPhone conectado (via Xcode/devicectl).
# Requer: Apple ID no Xcode (assinatura de desenvolvimento) + perfil de provisionamento
# para o bundle id (copiar ~/Library/Developer/Xcode/UserData/Provisioning\ Profiles/*.mobileprovision
# para ~/Library/MobileDevice/Provisioning\ Profiles/).
# Uso: ./scripts/deploy-ios-device.sh [udid]
set -euo pipefail
cd "$(dirname "$0")/.."

CONFIG="${CONFIG:-Debug}"
RID=ios-arm64

UDID="${1:-$(xcrun devicectl list devices 2>/dev/null | rg -o 'D0ACBB72-[0-9A-F-]+' | head -1 || true)}"
if [[ -z "$UDID" ]]; then
  echo "Nenhum iPhone detectado. Conecte e desbloqueie o aparelho." >&2
  exit 1
fi

# Garante que existe ao menos um perfil de provisionamento para o bundle id.
mkdir -p ~/Library/MobileDevice/Provisioning\ Profiles
for p in ~/Library/Developer/Xcode/UserData/Provisioning\ Profiles/*.mobileprovision; do
  [[ -e "$p" ]] || continue
  if security cms -D -i "$p" 2>/dev/null | grep -q 'agarnidev.Pressio'; then
    cp "$p" ~/Library/MobileDevice/Provisioning\ Profiles/ || true
  fi
done

echo ">> Build ($CONFIG/$RID)..."
dotnet build Pressio.iOS/Pressio.iOS.csproj -c "$CONFIG" -f net10.0-ios -p:RuntimeIdentifier="$RID"

APP="Pressio.iOS/bin/$CONFIG/net10.0-ios/$RID/Pressio.iOS.app"
echo ">> Install no aparelho ($UDID)..."
xcrun devicectl device install app --device "$UDID" "$APP"

echo ">> Launch..."
xcrun devicectl device process launch --device "$UDID" agarnidev.Pressio

echo "Pronto: app aberto no iPhone ($UDID)."
