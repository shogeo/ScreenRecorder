# Third-party notices

The root [MIT license](LICENSE), copyright (c) 2026 Georgiy S., applies to this
project's original source and documentation. It does not relicense dependencies,
Windows components, GPU drivers or separately distributed tools.

| Component | Version | Use | License / notice |
| --- | --- | --- | --- |
| FFmpeg essentials build | 9.0.2 | Separate capture/encoding process | GPL v3; [license](third_party/FFmpeg.LICENSE.txt), [build/source notice](third_party/FFmpeg.NOTICE.txt) |
| NAudio | 2.2.1 | WASAPI capture, resampling and channel conversion | MIT; [license](third_party/NAudio.LICENSE.txt) |
| SharpDX / SharpDX.DXGI | 4.2.0 | Display/adapter enumeration | MIT; [license](third_party/SharpDX.LICENSE.txt) |
| ScreenRecorderLib | 7.0.1 | Capture-exclusion helper | MIT; [license](third_party/ScreenRecorderLib.LICENSE.txt) |
| Microsoft runtime support packages | See `packages.lock.json` | Transitive registry/access-control support | MIT; [notice](third_party/Microsoft.LICENSE.txt) |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 | Build-only reference assemblies | Microsoft package terms; not a runtime component in the app bundle |

FFmpeg is invoked as an independent process, with a named PCM pipe connecting the
app's mixer. Binary packages must retain its license and corresponding source/build
information. Do not describe the complete bundle as exclusively MIT. The repository
does not track FFmpeg binaries or third-party source archives; the setup script
downloads the pinned build into ignored `.tools/`.

The glass surface/material/motion code was adapted from an earlier project by
Georgiy S. Only visual rendering code was reused. No game loader, payload, injection,
or remote-control backend is part of ScreenRecorder. See [renderer notes](src/Glass/REFERENCE.md).
