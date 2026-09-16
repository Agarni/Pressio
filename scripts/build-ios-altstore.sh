#!/usr/bin/env bash
# Gera um .ipa SEM assinatura para instalar no iPhone via AltStore (o AltStore assina com seu Apple ID).
# Uso: ./scripts/build-ios-altstore.sh
set -euo pipefail
cd "$(dirname "$0")/.."

# O workload .NET iOS 26.5 exige Xcode 26.6. Use XCODE_DEV_DIR para apontar um Xcode
# específico; senão, procura o Xcode 26.6/26.5 em /Applications automaticamente.
if [[ -z "${DEVELOPER_DIR:-}" ]]; then
  for cand in "${XCODE_DEV_DIR:-}" /Applications/Xcode_26.6.app /Applications/Xcode_26.5.app; do
    [[ -n "$cand" && -d "$cand/Contents/Developer" ]] && export DEVELOPER_DIR="$cand/Contents/Developer" && break
  done
fi
[[ -n "${DEVELOPER_DIR:-}" ]] && echo ">> Usando Xcode: $DEVELOPER_DIR"

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
