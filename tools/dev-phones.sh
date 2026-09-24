#!/bin/sh
# Dev-цикл: собрать «LovePandas Dev» (сервер на ПК), поставить на все телефоны по adb и запустить.
# Сервер должен работать: cd server && npm start
# Запуск из корня репозитория: sh tools/dev-phones.sh
set -e
UNITY="/c/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe"
ADB="/c/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
LOG="Builds/dev-build.log"
mkdir -p Builds

curl -s -m 3 http://127.0.0.1:8787/health >/dev/null || echo "!! Локальный сервер не отвечает — запусти: cd server && npm start"

"$UNITY" -batchmode -quit -projectPath "$(pwd -W 2>/dev/null || pwd)" -buildTarget Android \
  -executeMethod LovePandas.Editor.BuildTools.BuildAndroidDev -logFile "$LOG" || true
grep -E "error CS|\[LovePandas\] Build" "$LOG" | sort -u
grep -q "Build DEV Succeeded" "$LOG" || { echo "Сборка не удалась, лог: $LOG"; exit 1; }

for d in $("$ADB" devices | awk 'NR>1 && $2=="device" {print $1}'); do
  echo "== $d: $("$ADB" -s "$d" shell getprop ro.product.model | tr -d '\r')"
  "$ADB" -s "$d" install -r Builds/LovePandas-dev.apk | tail -1
  "$ADB" -s "$d" shell am force-stop com.redpandaart.lovepandas.dev
  "$ADB" -s "$d" shell monkey -p com.redpandaart.lovepandas.dev -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
done
