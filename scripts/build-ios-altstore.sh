#!/usr/bin/env bash
# Gera um .ipa SEM assinatura para instalar no iPhone via AltStore (o AltStore assina com seu Apple ID).
# Uso: ./scripts/build-ios-altstore.sh
set -euo pipefail
cd "$(dirname "$0")/.."

CONFIG="${CONFIG:-Release}"
RID=ios-arm64

echo ">> Publicando ($CONFIG, $RID, sem codesign) para o AltStore..."
dotnet publish Pressio.iOS/Pressio.iOS.csproj -c "$CONFIG" -f net10.0-ios -r "$RID" \
  -p:CodesignKey="" -p:CodesignProvision="" -p:EnableCodeSigning=false

IPA="Pressio.iOS/bin/$CONFIG/net10.0-ios/$RID/publish/Pressio.iOS.ipa"
if [[ ! -f "$IPA" ]]; then
  IPA="$(find Pressio.iOS/bin -name 'Pressio.iOS.ipa' -path "*/$RID/*" | head -1)"
fi

echo ">> IPA gerado (sem assinatura):"
echo "   $IPA"
echo ">> Envie/arraste este .ipa para o AltStore (AltStore assina e instala no seu iPhone)."
