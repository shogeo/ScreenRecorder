# Usage

## Start and stop

Run `Recording.exe` from its complete build folder, or install using `install.ps1`.
There is no main window. The app waits in the notification area for global hotkeys.

- **Win+Shift+R** starts silently, or stops and saves an active recording.
- **Win+Shift+A** starts with system audio, or toggles that source during recording.
- **Win+Shift+M** starts with the default microphone, or toggles that source.
- **Win+Shift+B** starts with both, or enables both unless both are already on.

A shortcut pressed while the file is finishing is ignored. A stop during startup
is remembered and applied as soon as the encoder starts. A new session does not
inherit the previous session's sound mode.

## Overlay and tray

The indicator is positioned in the primary monitor's top-right working area before
it is shown. The red dot, elapsed time, speaker/microphone states, and stop button
are visible while starting, recording, and finalizing. There are no hover tooltips
or hover-driven panel movement. Small glass buttons retain their input animation,
and dismissal uses a brief compression.

The app asks Windows to exclude its HUD and menu from capture. Capture exclusion
and glass effects are platform-dependent. Topmost restoration does not elevate
the app into another desktop or guarantee exclusive-fullscreen composition.

Right-click the tray icon for **Open recordings** and **Quit**. Double-clicking it
opens the recordings folder. Quit finalizes an active recording first.

## Output files and clipboard

Output uses the system Videos known folder, including redirection:

```text
<Videos>\Recordings\Recording_yyyy-MM-dd_HH-mm-ss_fff_abcd.mp4
```

During capture, the name ends in `.recording.mp4`. After the encoder exits
successfully, the app checks that a video frame can be decoded and removes the
`.recording` suffix. The resulting file is placed on the clipboard as a file-drop
object, so Ctrl+V can attach/copy it in supporting applications. This replaces
the previous clipboard contents. There is no manual Copy item in the tray menu.

If the clipboard remains busy after retries, the MP4 stays saved and an error
notification explains that copying failed. Open the folder and copy it manually.

Normal MP4 finalization needs a graceful stop. Power loss, process termination,
or a driver crash can leave an incomplete `.recording.mp4`, which is retained
for possible recovery rather than presented as a completed video.

## Audio

Devices use the default Windows **Multimedia** endpoints resolved at recording
start. System audio uses WASAPI loopback; microphone uses shared-mode WASAPI
capture. Both are converted to 48 kHz stereo and mixed, with saturation on overload.
The app does not record separate editable audio tracks or apply noise suppression.

A source starts only when first enabled. Muting gates its output, clears buffered
audio, and supplies silence while leaving an already-started WASAPI device session
open. The session closes at recording end. A mono microphone is duplicated to both
output channels. If both signals are loud, lower device/application levels to avoid
clipping in the mix.

## Installation and autostart

`install.ps1` copies `dist\Recording` into `%LOCALAPPDATA%\Programs\Recording`.
It registers this current-user startup entry:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Recording = "<LocalAppData>\Programs\Recording\Recording.exe" --tray
```

`--tray` is the autostart invocation; ordinary startup is also background-only.
`--quit` asks an existing instance to finish and exit. Running a second normal
instance exits immediately. `install.ps1 -NoStart` updates/enables autostart without
starting the new copy. `-RemoveAutostart` disables only the Run entry.

## Uninstall

1. Disable autostart with `install.ps1 -RemoveAutostart`.
2. Use **Quit** in the tray, or run the installed EXE with `--quit`.
3. Remove `%LOCALAPPDATA%\Programs\Recording` after the process exits.
4. Optionally remove `%LOCALAPPDATA%\Recording` for local logs/settings.

Recordings in Videos are separate user files and are not removed by these scripts.

The app currently has no settings window. The primary screen and encoding profile
are configured in source. Legacy settings helpers are internal and do not provide
an external configuration contract.
