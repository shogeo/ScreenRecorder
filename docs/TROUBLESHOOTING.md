# Troubleshooting

## Hotkeys do nothing

- Confirm the app is running in the same interactive Windows session.
- Another running instance causes a new invocation to exit immediately.
- Inspect `%LOCALAPPDATA%\Recording\app.log`. Shortcut conflicts should trigger
  the dedicated fallback hook; successful conflict handling is not itself an error.
- Elevated applications, secure desktops, session transitions, or another tool's
  input hooks can affect global input. This app does not bypass Windows boundaries.

## Recording fails at startup

Keep `Recording.exe`, `ffmpeg.exe`, and all distributed DLLs in the same directory.
The build requires NVIDIA H.264 NVENC at runtime; other vendors are not an automatic
encoder fallback. Check the NVIDIA driver, active display connection, and available
disk space. Remote/disconnected or locked desktops may not support Desktop Duplication.

The log records the chosen DXGI adapter/output and codec profile. Review these
details if the wrong display is selected on a hybrid GPU machine.

## MP4 has no visible video

The current profile is H.264 High / 4:2:0 in MP4, not the earlier AV1 profile.
Older AV1 recordings may require a player with an AV1 decoder. Updating the app
does not convert existing files. A `.recording.mp4` can be incomplete and should
not be assumed playable merely because it has a file extension.

Current recording finalization checks that at least one video frame decodes.
This detects missing/invalid video, not whether a captured game rendered useful
content. Protected content or true exclusive fullscreen may still produce an
unusable image. Try the application's borderless fullscreen mode.

## File is too large or text is blurry

Confirm that the current build is running. The active profile is full resolution,
60 FPS, target 2.5 Mbit/s and maximum 3 Mbit/s video, plus 128 kbit/s stereo audio.
Budget approximately 20–24 MB/minute. Bitrate control is buffered, so short clips
can differ from the estimate. Previously recorded files keep their old encoding.

Small text benefits from native resolution. A strict size limit competes with
detail in fast-moving scenes; the app does not promise artifact-free compression.
Frame rate is a target, not a guarantee under GPU/CPU overload.

## Audio is missing, delayed, or clipped

R starts silently by design. Check the HUD's microphone/speaker state and Windows
default Multimedia devices before starting. Restart the recording after changing
default devices. Windows microphone permissions, disconnected endpoints, device
driver latency, and system load can affect capture.

Both sources mix into one stereo track. A mono microphone is duplicated to both
channels. Reduce source levels if the sum clips. Muting silences the file but does
not close a WASAPI session already opened during that recording.

## HUD is hidden or blur looks different

Use borderless fullscreen for games which cover normal desktop windows. Topmost
does not cross into the UAC Secure Desktop, another user session, lock screen, or
true exclusive-fullscreen rendering. Transparency/accessibility/power settings,
Windows version, and composition support can change Acrylic appearance.

## Clipboard paste fails

The target application must accept pasted files. ScreenRecorder places a file-drop
object on the clipboard, not a video attachment for every messaging client.
If another app keeps the clipboard locked, the recording remains in Videos\Recordings;
copy it manually from Explorer.

## Reporting a problem

Include the Windows version, GPU/driver, app build/commit, triggering shortcut,
audio mode, fullscreen/windowed mode, and relevant error text. If available, include
codec/duration/size metadata from FFprobe. Do not publish recordings, usernames,
paths, microphone content, or full logs without reviewing them first.
