# Changelog

## Unreleased

- Organized the source as the ScreenRecorder Git repository.
- Added an MIT project license, documentation, dependency notices and build CI.
- Documented the current H.264 recording profile and explicit opt-in verification.

## Initial implementation

- Background Windows recorder with Win+Shift+R/A/M/B global shortcuts.
- Independent live system/microphone gates and mixed 48 kHz stereo audio.
- Primary-display capture at full resolution and a 60 FPS target.
- H.264 NVIDIA NVENC P6, VBR target 2.5 Mbit/s / maximum 3 Mbit/s; AAC 128 kbit/s.
- MP4 fast-start finalization, decoded-frame check and automatic file clipboard data.
- Native glass HUD, custom tray menu and best-effort topmost restoration.
- Per-user installation/autostart and graceful child-process lifetime management.

Earlier experimental profiles are not release guarantees. The current H.264 path
supersedes the AV1 experiment, which had playback-compatibility and size concerns.
