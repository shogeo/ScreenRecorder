// Visual renderer adapted from NLsalfetka/launcher/LiquidGlass.cs; no loader or backend code.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Recording
{
    internal sealed class GlassBackdrop
    {
        internal readonly int Width, Height;
        internal readonly byte[] Pixels;
        internal GlassBackdrop(Bitmap bitmap)
        {
            Width = bitmap.Width; Height = bitmap.Height; Pixels = new byte[Width * Height * 4];
            var data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), Pixels, y * Width * 4, Width * 4); }
            finally { bitmap.UnlockBits(data); }
        }
    }

    internal sealed class LiquidGlass : IDisposable
    {
        private Bitmap material;
        private GlassBackdrop previousBackdrop;
        private Rectangle previousBounds;
        private int previousTint, previousHover;
        private bool previousPressed, previousEnabled;
        private bool previousAnalytic;
        internal int Builds { get; private set; }

        internal void Draw(Graphics graphics, RectangleF rect, float radius, Color tint, float hover, bool enabled,
            bool pressed, GlassBackdrop backdrop, Rectangle sourceBounds, float scale, bool analyticEdges = false)
        {
            int width = Math.Max(1, (int)Math.Round(rect.Width * scale)), height = Math.Max(1, (int)Math.Round(rect.Height * scale));
            int light = (int)(Math.Max(0, Math.Min(1, hover)) * 255);
            if (material == null || material.Width != width || material.Height != height || previousBackdrop != backdrop ||
                previousBounds != sourceBounds || previousTint != tint.ToArgb() || previousHover != light || previousEnabled != enabled || previousPressed != pressed || previousAnalytic != analyticEdges)
            {
                if (material != null) material.Dispose();
                material = Build(width, height, radius * scale, tint, light / 255f, enabled, pressed, backdrop, sourceBounds, analyticEdges);
                previousBackdrop = backdrop; previousBounds = sourceBounds; previousTint = tint.ToArgb(); previousHover = light;
                previousPressed = pressed; previousEnabled = enabled; Builds++;
                previousAnalytic = analyticEdges;
            }
            var saved = graphics.Save();
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.DrawImage(material, rect);
            graphics.Restore(saved);
        }

        internal static Bitmap Build(int width, int height, float radius, Color tint, float hover, bool enabled,
            bool pressed, GlassBackdrop backdrop, Rectangle sourceBounds, bool analyticEdges = false)
        {
            var result = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            var pixels = new byte[width * height * 4];
            double sourceScaleX = sourceBounds.Width > 0 ? sourceBounds.Width / (double)width : 1;
            double sourceScaleY = sourceBounds.Height > 0 ? sourceBounds.Height / (double)height : 1;
            double halfW = width / 2.0, halfH = height / 2.0;
            radius = Math.Min(radius, (float)Math.Min(halfW, halfH));
            double bevelWidth = Math.Max(3, radius * .65), strength = enabled ? 1 : .42;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                double dx = x + .5 - halfW, dy = y + .5 - halfH;
                double qx = Math.Abs(dx) - halfW + radius, qy = Math.Abs(dy) - halfH + radius;
                double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0), length = Math.Sqrt(ox * ox + oy * oy);
                double distance = -(length + Math.Min(Math.Max(qx, qy), 0) - radius);
                double coverage = Math.Max(0, Math.Min(1, distance + .5));
                int index = (y * width + x) * 4;
                int original = 0;
                if (backdrop != null)
                {
                    int originalX = Math.Max(0, Math.Min(backdrop.Width - 1, sourceBounds.X + (int)(x * sourceScaleX)));
                    int originalY = Math.Max(0, Math.Min(backdrop.Height - 1, sourceBounds.Y + (int)(y * sourceScaleY)));
                    original = (originalY * backdrop.Width + originalX) * 4;
                    if (coverage == 0) { Array.Copy(backdrop.Pixels, original, pixels, index, 4); continue; }
                }
                else if (coverage == 0) continue;
                double nx, ny;
                if (length > .001) { nx = ox / length * Math.Sign(dx); ny = oy / length * Math.Sign(dy); }
                else if (qx > qy) { nx = Math.Sign(dx); ny = 0; }
                else { nx = 0; ny = Math.Sign(dy); }
                double bevel = Math.Max(0, 1 - Math.Max(0, distance) / bevelWidth);
                // Snell-inspired edge displacement plus a small convex magnification.
                double bend = bevel * bevel * radius * (pressed ? .17 : .42);
                int sx = (int)Math.Round(sourceBounds.X + (halfW + dx * .985 + nx * bend) * sourceScaleX);
                int sy = (int)Math.Round(sourceBounds.Y + (halfH + dy * .985 + ny * bend) * sourceScaleY);
                double b = 0, g = 0, r = 0, a = 0;
                if (backdrop != null)
                {
                    sx = Math.Max(0, Math.Min(backdrop.Width - 1, sx)); sy = Math.Max(0, Math.Min(backdrop.Height - 1, sy));
                    int sample = (sy * backdrop.Width + sx) * 4;
                    b = backdrop.Pixels[sample] / 255.0; g = backdrop.Pixels[sample + 1] / 255.0;
                    r = backdrop.Pixels[sample + 2] / 255.0; a = backdrop.Pixels[sample + 3] / 255.0;
                }
                double tintOpacity = (pressed ? .29 : .16 + hover * .065) * strength;
                Blend(ref b, ref g, ref r, ref a, tint.B, tint.G, tint.R, tintOpacity);
                double light = Math.Max(0, -nx * .55 - ny * .83);
                double rim = analyticEdges ? 0 : Math.Pow(bevel, 3.2) * (.11 + light * .54 + hover * .14) * strength;
                double reflection = Math.Pow(1 - (y + .5) / height, 3) * (pressed ? .025 : .065 + hover * .035) * strength;
                Blend(ref b, ref g, ref r, ref a, 255, 249, 237, rim + reflection);
                // A soft caustic on the opposite edge gives the lens thickness.
                double caustic = analyticEdges ? 0 : Math.Pow(bevel, 2) * Math.Max(0, nx * .3 + ny * .7) * .21 * strength;
                Blend(ref b, ref g, ref r, ref a, 255, 191, 133, caustic);
                pixels[index] = Byte(b * coverage); pixels[index + 1] = Byte(g * coverage);
                pixels[index + 2] = Byte(r * coverage); pixels[index + 3] = Byte(a * coverage);
                if (backdrop != null && coverage < 1)
                    for (int channel = 0; channel < 4; channel++) pixels[index + channel] = (byte)Math.Min(255,
                        pixels[index + channel] + Math.Round(backdrop.Pixels[original + channel] * (1 - coverage)));
            }
            var data = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < height; y++) Marshal.Copy(pixels, y * width * 4, IntPtr.Add(data.Scan0, y * data.Stride), width * 4); }
            finally { result.UnlockBits(data); }
            return result;
        }
        private static byte Byte(double value) { return (byte)Math.Max(0, Math.Min(255, Math.Round(value * 255))); }
        private static void Blend(ref double b, ref double g, ref double r, ref double a, int blue, int green, int red, double opacity)
        { opacity = Math.Max(0, Math.Min(1, opacity)); b = b * (1 - opacity) + blue / 255.0 * opacity;
          g = g * (1 - opacity) + green / 255.0 * opacity; r = r * (1 - opacity) + red / 255.0 * opacity; a += (1 - a) * opacity; }
        public void Dispose() { if (material != null) { material.Dispose(); material = null; } }
    }
}
