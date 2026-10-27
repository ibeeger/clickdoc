#!/usr/bin/env bash
# Builds an unnotarized release: build/release/RecordToDocs-<version>.dmg
# containing the app, an /Applications link and 安装说明.txt.
# Accepts the same environment variables as build-app.sh.
set -euo pipefail
cd "$(dirname "$0")/.."

./scripts/build-app.sh
VERSION="${VERSION:-$(cat VERSION 2>/dev/null || echo 0.0.0)}"

OUT=build/release
STAGE=build/dmg-stage
DMG="$OUT/RecordToDocs-$VERSION.dmg"
rm -rf "$STAGE"
mkdir -p "$STAGE" "$OUT"
cp -R build/RecordToDocs.app "$STAGE/"
ln -s /Applications "$STAGE/应用程序"
cp packaging/安装说明.txt "$STAGE/"

rm -f "$DMG"
hdiutil create -volname "RecordToDocs $VERSION" -srcfolder "$STAGE" -fs HFS+ -format UDZO -ov "$DMG" >/dev/null
rm -rf "$STAGE"

echo "Release: $(pwd)/$DMG"
shasum -a 256 "$DMG"
