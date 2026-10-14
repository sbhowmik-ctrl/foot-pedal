#!/bin/bash
# Builds PedalKeys.app (universal: Apple silicon + Intel) and refreshes the macOS zip.
# Must be run on a Mac with Xcode or the Command Line Tools (xcode-select --install).
#
# Output:
#   helper-mac/dist/PedalKeys-macOS/   staged files
#   public/downloads/PedalKeys-macOS.zip
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$HERE")"
STAGE="$HERE/dist/PedalKeys-macOS"
APP="$STAGE/PedalKeys.app"
ZIP="$ROOT/public/downloads/PedalKeys-macOS.zip"
MIN_OS="13.0"   # SMAppService (start at login) needs macOS 13

rm -rf "$HERE/dist"
mkdir -p "$APP/Contents/MacOS" "$HERE/dist/obj"

for ARCH in arm64 x86_64; do
  echo "Compiling $ARCH"
  swiftc -O -swift-version 5 \
    -target "$ARCH-apple-macos$MIN_OS" \
    -framework Cocoa -framework IOKit -framework ServiceManagement \
    -o "$HERE/dist/obj/PedalKeys-$ARCH" \
    "$HERE/PedalKeys/main.swift"
done
lipo -create -output "$APP/Contents/MacOS/PedalKeys" \
  "$HERE/dist/obj/PedalKeys-arm64" "$HERE/dist/obj/PedalKeys-x86_64"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>com.pedalkeys.app</string>
  <key>CFBundleName</key><string>PedalKeys</string>
  <key>CFBundleExecutable</key><string>PedalKeys</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>$MIN_OS</string>
  <key>LSUIElement</key><true/>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

# Ad-hoc signature (no developer account). Required for the app to launch on Apple silicon.
# For a smooth install without the right-click step, sign with a Developer ID and notarize instead.
codesign --force --deep --sign - "$APP"

cp "$HERE"/package/* "$STAGE/"
chmod +x "$STAGE/setup.command" "$STAGE/uninstall.command"
rm -rf "$HERE/dist/obj"

mkdir -p "$(dirname "$ZIP")"
rm -f "$ZIP"
# ditto keeps the executable bits and the app bundle intact (plain zip can break them).
ditto -c -k "$STAGE" "$ZIP"
echo "Wrote $ZIP ($(du -k "$ZIP" | cut -f1) KB)"
