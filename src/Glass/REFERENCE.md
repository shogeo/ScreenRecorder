The visual-only rendering code is adapted from
an earlier renderer project by Georgiy S. (`NLsalfetka/launcher`):
`AlphaSurface.cs`, `GlassButton.cs`, `LiquidGlass.cs`, `GlassMotion.cs`,
`CursorLight.cs`, `EdgeCoverage.cs`, and the backdrop functions from `Drawing.cs`.
No loader, payload or backend code is included.

The overlay and custom tray menu now use native WinForms windows. Premultiplied
32-bit DIB pixels reach the window DC through AlphaSurface.BitBlt, preserving the
same redirection-bitmap alpha and accent-acrylic path as the reference. WPF window
composition and non-client-rendering suppression are no longer involved.

Only the small buttons use the reference spring and optical renderer. The overlay
has no hover-driven position/size animation, outline, ToolTips or native drop-shadow
class flag. A brief compression remains on dismissal. Native DWM rounding is kept.
