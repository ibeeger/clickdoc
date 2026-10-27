#!/usr/bin/env bash
# Builds build/RecordToDocs.app as a universal binary (Apple silicon + Intel).
# Environment:
#   SIGN_IDENTITY  keychain code-signing identity; keeps the same signature across
#                  rebuilds (avoids re-granting permissions). Defaults to ad-hoc.
#   BUNDLE_ID      bundle identifier. Defaults to com.local.recordtodocs.
#                  Never change it after release: macOS treats it as another app.
#   VERSION        version string. Defaults to the contents of ./VERSION.
set -euo pipefail
cd "$(dirname "$0")/.."

BUNDLE_ID="${BUNDLE_ID:-com.local.recordtodocs}"
VERSION="${VERSION:-$(cat VERSION 2>/dev/null || echo 0.0.0)}"
BUILD_NUMBER="$(git rev-list --count HEAD 2>/dev/null || echo 1)"

BINS=()
for ARCH in arm64 x86_64; do
    TRIPLE="$ARCH-apple-macosx14.0"
    swift build -c release --product RecordToDocs --triple "$TRIPLE"
    BINS+=("$(swift build -c release --triple "$TRIPLE" --show-bin-path)/RecordToDocs")
done
mkdir -p build
BIN=build/RecordToDocs-universal
lipo -create "${BINS[@]}" -output "$BIN"

APP=build/RecordToDocs.app
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/RecordToDocs"

# App icon from ./icon.png, regenerated only when the source changes.
ICON_PLIST=""
if [ -f icon.png ]; then
    ICNS=build/AppIcon.icns
    if [ ! -f "$ICNS" ] || [ icon.png -nt "$ICNS" ] || [ scripts/make-icon.swift -nt "$ICNS" ]; then
        swiftc -O scripts/make-icon.swift -o build/make-icon
        build/make-icon icon.png build/AppIcon.iconset
        iconutil -c icns build/AppIcon.iconset -o "$ICNS"
        rm -rf build/AppIcon.iconset
    fi
    cp "$ICNS" "$APP/Contents/Resources/AppIcon.icns"
    ICON_PLIST="<key>CFBundleIconFile</key><string>AppIcon</string>"
fi

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
    <key>CFBundleName</key><string>RecordToDocs</string>
    <key>CFBundleExecutable</key><string>RecordToDocs</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>$BUILD_NUMBER</string>
    <key>LSMinimumSystemVersion</key><string>14.0</string>
    <key>LSUIElement</key><true/>
    <key>NSHighResolutionCapable</key><true/>
    $ICON_PLIST
</dict>
</plist>
PLIST

IDENTITY="${SIGN_IDENTITY:--}"
codesign --force --deep --sign "$IDENTITY" --identifier "$BUNDLE_ID" "$APP"

echo "Built: $(pwd)/$APP ($VERSION, $(lipo -archs "$APP/Contents/MacOS/RecordToDocs"))"
if [ "$IDENTITY" = "-" ]; then
    echo "提示：当前为 ad-hoc 签名，每次重新构建后可能需要重新授权。"
    echo "      可在钥匙串中创建自签名代码签名证书，然后用 SIGN_IDENTITY=\"证书名\" ./scripts/build-app.sh 构建。"
fi
