PedalKeys for Mac - foot pedal controls for any app
===================================================

Left pedal    -> Left arrow   (back 1 second)     keeps stepping while held
Center pedal  -> Space        (play / pause)      once per press
Right pedal   -> Right arrow  (forward 1 second)  keeps stepping while held

Install
  1. Unzip the download.
  2. Right-click setup.command and choose Open, then Open again.
     (macOS blocks a plain double-click on files that aren't from the App Store.)
  3. When asked, allow PedalKeys under System Settings > Privacy & Security:
       Accessibility and Input Monitoring.
  4. Look for "Pedal" in the menu bar. Green dot = pedal connected, orange = not found
     (it keeps looking), grey = paused.
  5. On YouTube, click the video once, then use the pedal.

Menu: Pause mappings, Resume mappings, Start at login, Quit.
Remove: right-click uninstall.command > Open.

Notes
  * This app is not notarized by Apple, which is why step 2 is needed.
  * Keys go to whichever window is focused.
  * If the pedal does the wrong thing, quit PedalKeys, run
      ~/Applications/PedalKeys.app/Contents/MacOS/PedalKeys --log
    press each pedal, and read ~/Library/Logs/PedalKeys.log.
  * Close any other pedal software (such as NCH Pedable) first, or each press may be sent twice.

PedalKeys is independent software. It is not affiliated with NCH Pedable, Sanguine, or VEC.
