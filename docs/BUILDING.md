# Building and verification

## Prerequisites

- Windows 10/11 x64 and PowerShell.
- .NET SDK 8 or newer; .NET Framework 4.8 reference assemblies restore via NuGet.
- Internet access for initial NuGet restore and FFmpeg download.
- An NVIDIA NVENC-capable GPU and driver only when running recording/encoding checks.

The build script checks `.tools\dotnet\dotnet.exe` first, then `dotnet` on PATH.
NuGet dependencies are versioned in `Recording.csproj` and `packages.lock.json`.

## Fresh checkout

```powershell
.\scripts\setup-ffmpeg.ps1
.\build.ps1
```

The FFmpeg setup uses a versioned archive URL and pinned SHA-256. It keeps its
download and extracted build inside ignored `.tools/`. Do not replace it with a
rolling "latest" build without deliberately updating the URL, hash, notice, and
recording validation together.

`build.ps1` compiles Release/x64, then packages the EXE, adjacent runtime DLLs,
FFmpeg, installation script, project license, and third-party notices into
`dist\Recording`. Source archives and build tools are not included in Git.

To compile without packaging/downloading FFmpeg:

```powershell
dotnet build Recording.csproj -c Release -p:Platform=x64 -p:RestoreLockedMode=true
```

Compilation does not start the app, register autostart, capture the screen, or use
the microphone. `install.ps1` performs the separate installation/startup action.

## Synthetic encoder check (optional)

This checks the real FFmpeg process, NVIDIA H.264 encoder, paced stereo PCM pipe,
and MP4 finalization using a generated video pattern and silence. It does **not**
capture the desktop or microphone. It still requires a working NVIDIA encoder.

```powershell
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force tests\results | Out-Null
& $compiler /nologo /out:tests\results\EncoderProfileCheck.exe tests\EncoderProfileCheck.cs
& .\tests\results\EncoderProfileCheck.exe `
    "$PWD\dist\Recording\Recording.exe" `
    "$PWD\tests\results\encoder-check.mp4"
```

Use a new output name if that MP4 already exists; the encoder refuses to overwrite
recordings. A successful check is not evidence of live capture/audio synchronization,
native player compatibility on every system, or HUD visibility over a particular game.

## Live integration check (explicit opt-in)

```powershell
.\build.ps1 -Test
```

This launches the `--self-test` harness. It displays an animated test card, plays a
test tone, sends actual global shortcuts, and enables the real default microphone.
The active desktop and surrounding microphone audio can be recorded. Run only
when this is intended, in a local unlocked session, with no other app instance.

The harness records in Videos\Recordings and writes copies/reports under ignored
`tests/results`. It checks hotkey modes, startup stop handling, final filenames,
clipboard file data, and indicator position/capture affinity. The media verifier
also checks codecs, frame rate, stereo audio, decoding, and selected signal levels:

```powershell
python tests\verify_media.py
```

The verifier requires Python 3 and FFmpeg/FFprobe on PATH or in `.tools/ffmpeg`.
Its timed assertions are tied to the test choreography; inspect failures rather
than treating them as proof of live failure in arbitrary recordings.

No recording checks run automatically during a normal build or in GitHub CI.

## Packaging and distribution

Distribute the complete `dist\Recording` folder. An EXE alone is insufficient.
Keep `LICENSE`, all `*.LICENSE.txt`, and `FFmpeg.NOTICE.txt` with binaries.
See [Third-party notices](../THIRD_PARTY_NOTICES.md) for FFmpeg source/build links
and the distinction between the MIT project and its separately licensed tools.
