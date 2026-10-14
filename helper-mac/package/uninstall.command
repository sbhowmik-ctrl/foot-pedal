#!/bin/bash
# Removes PedalKeys: quits it, turns off start-at-login, and deletes the app.
pkill -x PedalKeys 2>/dev/null
sleep 1
rm -rf "$HOME/Applications/PedalKeys.app"
defaults delete com.pedalkeys.app 2>/dev/null
echo
echo " PedalKeys has been removed."
echo " (Optional: remove it from System Settings > Privacy & Security > Accessibility / Input Monitoring.)"
echo
read -r -p " Press Return to close." _
