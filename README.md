# ScreenRecorder

A small Windows screen recorder controlled by global keyboard shortcuts.
No main window, accounts, or recording setup dialogs: press a shortcut, record,
then paste the saved MP4 into an app that accepts files.

- Full native resolution at a target of **60 FPS**.
- **H.264 / MP4** with NVIDIA NVENC hardware encoding and bounded bitrate.
- Independently toggle system audio and the default microphone during recording.
- **AAC stereo, 48 kHz, 128 kbit/s**; mono microphones play in both channels.
- Compact glass indicator in the top-right corner and a custom tray menu.
- Save to **Videos\Recordings** and automatically copy the completed file to the clipboard.
- Optional Windows startup installation for the current user.

## Requirements

**Windows 10/11 x64**, an NVIDIA GPU with H.264 NVENC support, and a compatible NVIDIA
driver. Windows Desktop Duplication must be available in the interactive session.
The app targets .NET Framework 4.8 and uses Windows Media Foundation.

Install [Visual C++ Redistributable x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)
if the capture-exclusion library cannot load. Windows N/KN editions may require
the Media Feature Pack. Acrylic and rounded corners vary by Windows version and
system transparency settings.

## Keyboard shortcuts

All shortcuts use **Win + Shift + letter**:

| Letter | When idle | While recording |
| --- | --- | --- |
| **R** | Start without audio | Stop and save |
| **A** | Start with system audio | Toggle system audio |
| **M** | Start with the default microphone | Toggle microphone |
| **B** | Start with both sources | Enable both; if both are enabled, disable both |

Each recording starts with the audio mode selected by its starting shortcut.
It does not inherit the previous recording's mode. The indicator's speaker,
microphone, and stop buttons provide the same controls.

The tray menu contains **Open recordings** and **Quit**. Quit finishes an active
recording before exiting. Only one app instance can run at a time.

## Build

Install the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer, then run
from the repository directory in PowerShell:

```powershell
.\scripts\setup-ffmpeg.ps1
.\build.ps1
```

The setup script downloads a pinned FFmpeg 9.0.2 archive and verifies its SHA-256.
The build restores NuGet dependencies and creates `dist\Recording`. Run
`dist\Recording\Recording.exe` with its adjacent DLLs and `ffmpeg.exe`.
The repository is named ScreenRecorder; the executable remains `Recording.exe`.

For SDK discovery, packaging, and verification details, see [Building](docs/BUILDING.md).

## Install and enable autostart

```powershell
.\install.ps1
```

This copies the build to `%LOCALAPPDATA%\Programs\Recording`, enables a current-user
Windows Run entry, and starts the background app. Administrator privileges are
not required. Repeating the command updates the installed copy after gracefully
stopping it.

```powershell
# Disable autostart; does not stop or uninstall the current app.
.\install.ps1 -RemoveAutostart

# Stop a running installed instance.
& "$env:LOCALAPPDATA\Programs\Recording\Recording.exe" --quit
```

See [Usage](docs/USAGE.md) for file locations, audio behavior, and uninstall steps.

## Recording profile

| Setting | Value |
| --- | --- |
| Capture | Primary display, original resolution, cursor included |
| Frame rate | 60 FPS target, constant output cadence |
| Video | H.264 NVENC, High profile, 4:2:0, MP4 `avc1` |
| Encoder preset | P6 / HQ |
| Rate control | VBR, target 2.5 Mbit/s, maximum 3 Mbit/s, CQ 26 |
| Audio | AAC-LC, 48 kHz, 128 kbit/s, stereo |
| Output | MP4 with fast-start metadata |

Budget approximately **20–24 MB/minute**, or **30–35 MB for 90 seconds**, including
audio. This is an estimate: content, rate-control buffering, and container overhead
affect actual size. Low-motion scenes may be smaller. Compression is lossy, and a
tight bitrate limit can soften small text in busy scenes. Original resolution
preserves more text detail than downscaling.

## Limitations and privacy

The HUD reasserts its topmost position without taking keyboard focus. A normal
desktop overlay cannot guarantee visibility over true exclusive fullscreen,
Secure Desktop/UAC, another Windows session, or the lock screen. Use borderless
fullscreen when an application hides desktop overlays.

Audio sources mix into one stereo track. Muted sections contain silence. Once an
audio device has first been enabled, its WASAPI session remains open until the
recording ends; muting gates its data before mixing. Default devices are selected
at recording start, so restart the recording after changing devices.

The app has no network/upload/account functionality. Recordings stay local;
clipboard paste can share a video when the user chooses to paste it elsewhere.
Error logs can contain local filenames and device details. Review logs before
posting them in an issue. The app stops recording when the session locks,
disconnects, suspends, or the display configuration changes.

See [Troubleshooting](docs/TROUBLESHOOTING.md), [Architecture](docs/ARCHITECTURE.md),
and [Security](SECURITY.md).

## Development

See [Contributing](CONTRIBUTING.md). CI builds the Windows application only;
it does not capture a desktop, open a microphone, or run GPU recording checks.
Live capture tests are explicitly opt-in. Historical test artifacts are not
included and are not evidence of behavior on another machine.

## License

ScreenRecorder's project source is licensed under the **MIT License**.
Copyright (c) 2026 **Georgiy S.** See [LICENSE](LICENSE).

Dependencies keep their own licenses. The packaged FFmpeg executable is a
separate **GPL v3** program, not MIT. NAudio, SharpDX, and ScreenRecorderLib use
MIT licenses. See [Third-party notices](THIRD_PARTY_NOTICES.md) and `third_party/`
before redistributing a binary package.
