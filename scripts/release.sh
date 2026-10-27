#!/usr/bin/env bash
# Builds an unnotarized release: build/release/ClickDoc-<version>.dmg
# containing the app, an /Applications link and 安装说明.txt.
# Accepts the same environment variables as build-app.sh.
set -euo pipefail
cd "$(dirname "$0")/.."

./scripts/build-app.sh
VERSION="${VERSION:-$(cat VERSION 2>/dev/null || echo 0.0.0)}"

OUT=build/release
STAGE=build/dmg-stage
DMG="$OUT/ClickDoc-$VERSION.dmg"
rm -rf "$STAGE"
mkdir -p "$STAGE" "$OUT"
cp -R build/ClickDoc.app "$STAGE/"
ln -s /Applications "$STAGE/应用程序"
cp packaging/安装说明.txt "packaging/Install Guide.txt" "$STAGE/"

rm -f "$DMG"
hdiutil create -volname "ClickDoc $VERSION" -srcfolder "$STAGE" -fs HFS+ -format UDZO -ov "$DMG" >/dev/null
rm -rf "$STAGE"

# Stable name for https://github.com/ibeeger/clickdoc/releases/latest/download/ClickDoc.dmg
cp "$DMG" "$OUT/ClickDoc.dmg"

echo "Release: $(pwd)/$DMG"
echo "Upload:  $(pwd)/$OUT/ClickDoc.dmg (stable name used by the website)"
shasum -a 256 "$DMG"
