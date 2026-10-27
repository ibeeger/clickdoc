#!/usr/bin/env bash
# Builds build/RecordToDocs.app. Set SIGN_IDENTITY to a keychain code-signing
# identity to keep the same signature across rebuilds (avoids re-granting
# Screen Recording / Accessibility permissions). Defaults to ad-hoc signing.
set -euo pipefail
cd "$(dirname "$0")/.."

swift build -c release --product RecordToDocs
BIN="$(swift build -c release --show-bin-path)/RecordToDocs"

APP=build/RecordToDocs.app
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/RecordToDocs"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key><string>com.local.recordtodocs</string>
    <key>CFBundleName</key><string>RecordToDocs</string>
    <key>CFBundleExecutable</key><string>RecordToDocs</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>0.1.0</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>LSMinimumSystemVersion</key><string>14.0</string>
    <key>LSUIElement</key><true/>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

IDENTITY="${SIGN_IDENTITY:--}"
codesign --force --deep --sign "$IDENTITY" --identifier com.local.recordtodocs "$APP"

echo "Built: $(pwd)/$APP"
if [ "$IDENTITY" = "-" ]; then
    echo "提示：当前为 ad-hoc 签名，每次重新构建后可能需要重新授权。"
    echo "      可在钥匙串中创建自签名代码签名证书，然后用 SIGN_IDENTITY=\"证书名\" ./scripts/build-app.sh 构建。"
fi
