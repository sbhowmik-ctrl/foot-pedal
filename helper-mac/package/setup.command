#!/bin/bash
# PedalKeys one-click setup for macOS. Per-user install, no administrator password.
#   1. copies PedalKeys.app to ~/Applications
#   2. clears the "downloaded from the internet" mark on that copy
#   3. starts it (menu bar). It starts at login by default.
cd "$(dirname "$0")" || exit 1

DEST="$HOME/Applications"

echo
echo " PedalKeys setup"
echo " ---------------"

if [ ! -d "PedalKeys.app" ]; then
  echo " PedalKeys.app was not found next to setup.command."
  echo " Unzip the download first, then run setup.command again."
  read -r -p " Press Return to close." _
  exit 1
fi

pkill -x PedalKeys 2>/dev/null
sleep 1

mkdir -p "$DEST"
rm -rf "$DEST/PedalKeys.app"
cp -R "PedalKeys.app" "$DEST/"
xattr -dr com.apple.quarantine "$DEST/PedalKeys.app" 2>/dev/null

open "$DEST/PedalKeys.app"

echo
echo " Installed to $DEST/PedalKeys.app"
echo " Look for 'Pedal' in the menu bar at the top right."
echo
echo " macOS will ask for two permissions. Switch both on for PedalKeys:"
echo "   - Accessibility    (lets it press keys for you)"
echo "   - Input Monitoring (lets it read the pedal)"
echo " System Settings > Privacy & Security."
echo
echo " Then open YouTube, click the video once, and use the pedal."
echo
read -r -p " Press Return to close." _
