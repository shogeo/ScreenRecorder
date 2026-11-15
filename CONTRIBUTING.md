# Contributing

Build instructions are in [docs/BUILDING.md](docs/BUILDING.md), and the component
map is in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Development workflow

1. Start from a clean branch and keep changes scoped to one problem.
2. Preserve global hotkeys, independent source toggles, background startup, MP4
   finalization, stereo audio, clipboard behavior, and the minimal tray/HUD flow.
3. Keep UI text in English. Do not add setup dialogs for routine capture actions.
4. Keep codec defaults in `EncodingProfile.cs` and update profile documentation
   when behavior changes. Never label an untested codec/profile as validated.
5. Compile the project. Run only checks appropriate to the change; no live recording
   is necessary for documentation edits.
6. Describe the problem, final behavior, validation actually performed, and material
   limitations in the pull request. Use generated/synthetic media when possible.

Live integration checks use the real display, speakers and microphone. Run them
only explicitly and in an intended local interactive session. Do not commit their
outputs, personal recordings, `.tools/`, builds, logs, or credentials.

The codebase targets .NET Framework 4.8 and x64. Avoid dependencies which require
an unrelated runtime migration. Native handles, audio callbacks, pipes and child
processes must have explicit lifetime/cleanup handling.

The glass renderer includes visual code adapted from an earlier project by the
same author. Preserve provenance comments and third-party notices when reusing code.
New project contributions are accepted under the repository's MIT license;
separately licensed dependencies keep their original terms.
