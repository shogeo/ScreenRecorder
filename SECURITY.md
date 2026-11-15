# Security

ScreenRecorder is a local desktop capture application. It intentionally accesses
screen content, optional system audio/microphone input, output files and clipboard
file data. It has no application upload service or account backend.

The fallback keyboard hook watches modifier/key combinations for the configured
shortcuts. It does not save general keystroke history. An already-started WASAPI
session remains open while its recording output is muted; see [Usage](docs/USAGE.md).

Installation optionally writes a current-user Run entry and needs no administrator
rights. Do not enable UIAccess, bypass Secure Desktop, or disable platform protections
to make the HUD appear over restricted system surfaces.

Keep the complete installed folder in a location writable only by trusted users:
the app loads adjacent libraries and invokes the adjacent `ffmpeg.exe`. Dependency
downloads should be checked against their pinned hashes/lockfile before packaging.
The FFmpeg process is assigned to a kill-on-close Windows Job.

Recordings can contain private data. Logs can reveal file paths and device names;
clipboard data can be pasted into other apps. Review reports before sharing them.

For a suspected vulnerability, use the repository's private vulnerability reporting
feature if enabled. If private reporting is unavailable, contact the repository
owner through a contact method they publicly provide. Do not post exploit details,
credentials, private logs, or recordings in a public issue while arranging contact.
No response time or security-support window is promised.
