#!/usr/bin/env bash
# Compila, instala e abre o Pressio.Android num emulador/dispositivo conectado,
# com as assemblies embutidas no APK (funciona só com `adb install`; sem Fast Deployment).
# Uso: ./scripts/run-android.sh [serial]   (ex.: emulator-5554)
set -euo pipefail
cd "$(dirname "$0")/.."

SDK="$HOME/Library/Developer/Xamarin/android-sdk-macosx"
export PATH="$PATH:$SDK/platform-tools:$SDK/emulator"

SERIAL="${1:-$(adb devices | awk 'NR>1 && $2=="device"{print $1; exit}')}"
if [[ -z "$SERIAL" ]]; then
  echo "Nenhum dispositivo/emulador conectado. Inicie um AVD: emulator -avd <nome>" >&2
  exit 1
fi

echo ">> Build (assemblies embutidas por padrão no csproj)..."
dotnet build Pressio.Android/Pressio.Android.csproj -c Debug

APK="Pressio.Android/bin/Debug/net10.0-android/com.CompanyName.Pressio-Signed.apk"

echo ">> Install em $SERIAL ..."
adb -s "$SERIAL" install -r "$APK"

echo ">> Launch..."
adb -s "$SERIAL" shell am start -n com.CompanyName.Pressio/crc64d810e507820c5593.MainActivity

echo "Pronto: abrindo em $SERIAL"
