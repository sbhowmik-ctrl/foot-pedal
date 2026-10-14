PedalKeys - foot pedal controls for any app
===========================================

Left pedal    -> Seek back 1 second               keeps stepping while held
Center pedal  -> Space (play / pause)            once per press
Right pedal   -> Seek forward 1 second            keeps stepping while held

Install
  1. Extract this zip (right-click > Extract All...).
  2. Double-click setup.bat. No administrator prompt.
  3. Leave PedalKeys running in the system tray.
  4. Click the YouTube video or the downloaded file once, then use the pedal.

Tray menu: Pause mappings, Resume mappings, Start with Windows, Quit.
The tray icon is green when the pedal is connected, amber when it is not found
(it keeps looking), and grey when paused.

Remove: run uninstall.bat.

Notes
  * Windows may show a SmartScreen warning because the app is not code-signed.
    Choose "More info" > "Run anyway".
  * Click the player or file so it is in front. Forward/back seek that playing media by
    1 second. Play/pause sends Space to the focused window. Windows does not let a normal
    app send keys to a "Run as administrator" window.
  * Troubleshooting: quit PedalKeys, run "%LOCALAPPDATA%\PedalKeys\PedalKeys.exe --log",
    press each pedal, and look at %LOCALAPPDATA%\PedalKeys\reports.log.

PedalKeys is independent software. It is not affiliated with or derived from NCH Pedable,
Sanguine, or VEC, and it does not use or control any of their software.
