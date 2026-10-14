# PedalKeys

A one-page Next.js site plus a small Windows tray helper that makes a Sanguine Preon / VEC Infinity
USB foot pedal (HID `05F3:00FF`) control playback in whatever app has focus, YouTube in Chrome included.

| Pedal         | Key         | Behaviour                                 |
| ------------- | ----------- | ----------------------------------------- |
| Left          | Seek back 1 second | Fires on press, keeps stepping while held |
| Center / Play | Space              | Once per press, never repeats             |
| Right         | Seek forward 1 second | Fires on press, keeps stepping while held |

The mapping is fixed and applied by the helper (not the site) as soon as it starts. There is no
mapping screen, no account and no backend. It does not use or bundle NCH Pedable, a Chrome
extension, or WebHID.

## Repository layout

```
app/                    Next.js App Router site (single page: app/page.tsx)
public/downloads/       PedalKeys-Windows.zip, linked from the Download button
helper/
  src/                  C# source for PedalKeys.exe
  package/              files copied into the zip: setup.bat, uninstall.bat, README.txt
  tests/                key-sending / repeat-timing check (no pedal needed)
  build.ps1             compiles the helper and refreshes the zip
```

## Run the website

```powershell
npm install
npm run dev        # http://localhost:3000
npm run build      # production build (the page is fully static)
```

## Build the helper on Windows

Requirements: Windows 10/11 and PowerShell. Nothing else. The helper is compiled with `csc.exe`,
the C# compiler that ships with the .NET Framework 4.x already built into Windows, so there is no
.NET SDK, Visual Studio, or NuGet restore involved.

```powershell
npm run build:helper
# same thing without npm:
powershell -NoProfile -ExecutionPolicy Bypass -File helper\build.ps1
```

This compiles `helper/src/*.cs` into `helper/dist/PedalKeys/PedalKeys.exe`, adds the files from
`helper/package/`, and writes `public/downloads/PedalKeys-Windows.zip`. Use `-NoZip` to only
build the staged folder.

## Refresh the zip served by the site

1. Change the helper source (or `helper/package/*`).
2. `npm run build:helper`
3. Commit the updated `public/downloads/PedalKeys-Windows.zip` and deploy the site.

The button links to `/downloads/PedalKeys-Windows.zip`. Keep that file name, or update
`DOWNLOAD_URL` in `app/page.tsx`.

## Test the helper

```powershell
npm run test:helper
```

Feeds simulated pedal states into the real mapper and `SendInput` code and records what a focused
window receives. It checks:

- Arrow keys arrive with the extended-key flag
- Hold-to-repeat is about 200 ms, each step 1 second of media
- Space fires exactly once per press
- Nothing fires while paused

It needs an interactive desktop session, and it takes a few seconds because it opens a short-lived window.

## How the helper works

- `Hid.cs` enumerates HID interfaces and reads input reports with the Windows HID API
  (`hid.dll`, `cfgmgr32.dll`, `ReadFile`). This is what hidapi and node-hid wrap on Windows, called
  directly so the zip has no native DLLs or Node runtime in it.
- `PedalMonitor.cs` looks for vendor `0x05F3` / product `0x00FF` every 2 seconds for as long as the
  app is open, so plugging in late, unplugging and replugging all work. While it can't find the pedal,
  the tray icon is amber and reads "Pedal not found. Retrying...".
- `Mapper.cs` turns button state into playback commands (press, 200 ms repeat for Left/Right, once for Space).
- `MediaSeeker.cs` seeks the focused app's Windows media session by 1 second, so YouTube and local
  files move by 1 second instead of YouTube's 5-second arrow shortcut. Arrow keys are only a fallback.
- `KeySender.cs` sends Space (and fallback arrows) with `SendInput` to the focused window.
- `TrayApp.cs` is the whole UI: Pause mappings, Resume mappings, Start with Windows, Quit.
- `Startup.cs` manages `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. It is on by default,
  applied once on first run so unchecking it sticks. It is per-user and needs no admin rights.

`setup.bat` (inside the zip) stops any running copy, copies `PedalKeys.exe` to
`%LOCALAPPDATA%\PedalKeys`, adds the Run entry, clears the downloaded-from-internet mark on the
installed copy, and starts the app. `uninstall.bat` reverses all of that.

## The pedal's report format

The helper scans every HID interface for vendor `05F3` / product `00FF`, so the pedal is found
whichever port it is plugged into. On the connected unit the report is three button usages
(1 = left, 2 = center, 3 = right), which arrive as bits `0x01`, `0x02` and `0x04` in the first
data byte. If a different unit reports differently, the pedals will do the wrong thing. To check:

```powershell
# quit PedalKeys from the tray first
& "$env:LOCALAPPDATA\PedalKeys\PedalKeys.exe" --log
# press each pedal, then read:
Get-Content "$env:LOCALAPPDATA\PedalKeys\reports.log"
```

Each line is one raw report as hex. If the bits differ, change the `LEFT`/`CENTER`/`RIGHT`
constants at the top of `helper/src/Mapper.cs` and rebuild.

If the log stays empty and the tray says "Pedal not found", the pedal is likely in a keyboard-style
mode, which Windows locks to itself and which can't be read from user space. That is a device
configuration issue, not an app one.

## macOS helper

`helper-mac/` is a separate Swift menu-bar app with the same behaviour (Left / Space / Right, 200 ms
repeat on the arrows, pause/resume, start at login, "Pedal not found" retry state). It reads the
pedal with IOKit's `IOHIDManager`, which also notices plugging and unplugging on its own, and posts
keys with `CGEvent`.

**It must be built on a Mac**, and it has not been compiled or run yet, because it was written on
Windows. Expect to fix small compile errors on the first build.

```bash
xcode-select --install        # once, if you don't have the compiler
npm run build:helper:mac      # or: bash helper-mac/build.sh
```

This produces a universal (Apple silicon + Intel) `PedalKeys.app`, ad-hoc signs it, and writes
`public/downloads/PedalKeys-macOS.zip`. The site checks for that file when it builds: if it exists,
the page shows a "Download for Mac" button, otherwise it says the Mac version isn't published yet.
Commit the zip, rebuild the site and deploy.

What differs from Windows:

- macOS asks the user to allow **Accessibility** (to press keys) and **Input Monitoring** (to read the
  pedal) once.
- The app isn't notarized, so the first launch of `setup.command` needs right-click > Open. Signing
  with an Apple Developer ID and notarizing removes that step and the Gatekeeper warning.
- Buttons are read as HID Button-page usages 1, 2, 3. If a unit differs, run
  `PedalKeys --log` and check `~/Library/Logs/PedalKeys.log`.

The site also detects Mac, phone and Linux visitors and tells them when the Windows download isn't
for them.

## Known limits

- The exe is unsigned, so Windows SmartScreen may warn on first run ("More info" > "Run anyway"),
  and some antivirus tools are wary of unsigned apps that send keystrokes. Code-signing the exe
  before publishing avoids both. Sign `helper/dist/PedalKeys/PedalKeys.exe` between compiling and
  zipping.
- A normal app can't send keys into an elevated ("Run as administrator") window.
- Space and the arrows act on the focused window. For YouTube, the player must have focus, hence
  "click the video once".
