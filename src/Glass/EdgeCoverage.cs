// Visual renderer adapted from NLsalfetka/launcher/EdgeCoverage.cs; no loader or backend code.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Recording
{
    internal static class EdgeCoverage
    {
        // Resolve the silhouette in final device pixels. Rotating a rectangular
        // image and clipping a GDI path cannot supply this coverage reliably.
        internal static void Apply(Bitmap image, byte[] pixels, GlassBackdrop backdrop, Point source,
            float scale, GlassPose pose, float radius)
        { Apply(image, pixels, backdrop, source, scale, pose, radius, false, PointF.Empty, false, true); }
        internal static void Apply(Bitmap image, byte[] pixels, GlassBackdrop backdrop, Point source,
            float scale, GlassPose pose, float radius, bool resolveGlass, PointF cursor, bool cursorVisible, bool enabled)
        {
            var bounds = new Rectangle(Point.Empty, image.Size);
            var data = image.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < image.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * image.Width * 4, image.Width * 4);
                float logicalWidth = image.Width / scale, logicalHeight = image.Height / scale;
                float[] inverse;
                using (var matrix = GlassMotion.Transform(new RectangleF(0, 0, logicalWidth, logicalHeight), pose))
                { matrix.Invert(); inverse = matrix.Elements; }
                var lighting = CursorLight.ForTilt(pose.X * Math.Max(0, Math.Min(1, pose.Hover)), pose.Y * Math.Max(0, Math.Min(1, pose.Hover)));
                double halfWidth = (logicalWidth - 6) / 2, halfHeight = (logicalHeight - 6) / 2;
                double corner = Math.Min(radius, Math.Min(halfWidth, halfHeight));
                double progress = pose.Pulse >= 0 ? Math.Min(1, pose.Pulse) : 1;
                double pulseFade = Math.Pow(1 - progress, 1.8);
                double pulseRadius = Math.Max(logicalWidth - 6, logicalHeight - 6) * (1 - Math.Pow(1 - progress, 2)) * 1.15 + 2;
                double pulseWidth = 1 + 1.8 * pulseFade * scale;
                for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                {
                    double px = (x + .5) / scale, py = (y + .5) / scale;
                    double localX = inverse[0] * px + inverse[2] * py + inverse[4] - logicalWidth / 2;
                    double localY = inverse[1] * px + inverse[3] * py + inverse[5] - logicalHeight / 2;
                    double qx = Math.Abs(localX) - halfWidth + corner, qy = Math.Abs(localY) - halfHeight + corner;
                    double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0), length = Math.Sqrt(ox * ox + oy * oy);
                    double distance = length + Math.Min(Math.Max(qx, qy), 0) - corner;
                    // Interior is untouched; only the final contour is resolved.
                    if (!resolveGlass && distance < -2 / scale) continue;
                    double nx, ny;
                    if (length > .00001) { nx = ox / length * Math.Sign(localX); ny = oy / length * Math.Sign(localY); }
                    else if (qx > qy) { nx = Math.Sign(localX); ny = 0; }
                    else { nx = 0; ny = Math.Sign(localY); }
                    double gx = (nx * inverse[0] + ny * inverse[1]) / scale;
                    double gy = (nx * inverse[2] + ny * inverse[3]) / scale;
                    double physicalDistance = distance / Math.Max(.0001, Math.Sqrt(gx * gx + gy * gy));
                    double coverage = Math.Max(0, Math.Min(1, .5 - physicalDistance / 1.45));
                    if (!resolveGlass && coverage >= 1) continue;
                    int destination = (y * image.Width + x) * 4;
                    int background = backdrop == null ? 0 : (Math.Max(0, Math.Min(backdrop.Height - 1, source.Y + y)) * backdrop.Width +
                        Math.Max(0, Math.Min(backdrop.Width - 1, source.X + x))) * 4;
                    double blue = pixels[destination] / 255.0, green = pixels[destination + 1] / 255.0;
                    double red = pixels[destination + 2] / 255.0, alpha = pixels[destination + 3] / 255.0;
                    if (resolveGlass && physicalDistance < 1)
                    {
                        CursorLight.Reflection reflection = CursorLight.Evaluate(distance, nx, ny, lighting);
                        // Resolve the rim as a continuous function of signed
                        // distance. No native-resolution clipped vector stroke.
                        double band = Math.Max(0, 1 - Math.Abs(physicalDistance + .9) / 1.35);
                        band = band * band * (3 - 2 * band);
                        double strength = enabled ? 1 : .5;
                        Over(ref blue, ref green, ref red, ref alpha, 182, 209, 231, band * .1 * strength);
                        double reflectionBand = Math.Max(0, 1 - Math.Abs(physicalDistance + 1.7) / 1.7);
                        reflectionBand = reflectionBand * reflectionBand * (3 - 2 * reflectionBand);
                        Over(ref blue, ref green, ref red, ref alpha, 235, 242, 252,
                            reflectionBand * reflection.Edge * strength);
                        double transmittedBand = Math.Max(0, 1 - Math.Abs(physicalDistance + 1.9) / 1.6);
                        transmittedBand *= transmittedBand;
                        Over(ref blue, ref green, ref red, ref alpha,
                            164, 207, 232, transmittedBand * reflection.Transmission * strength);
                        if (pose.Pulse >= 0)
                        {
                            double clickX = localX + logicalWidth / 2 - (3 + (logicalWidth - 6) * pose.OriginX);
                            double clickY = localY + logicalHeight / 2 - (3 + (logicalHeight - 6) * pose.OriginY);
                            double ringDistance = Math.Abs(Math.Sqrt(clickX * clickX + clickY * clickY) - pulseRadius) * scale;
                            double wave = Math.Max(0, 1 - ringDistance / pulseWidth);
                            Over(ref blue, ref green, ref red, ref alpha, 155, 207, 236, wave * wave * pulseFade * .38);
                            Over(ref blue, ref green, ref red, ref alpha, 141, 184, 214, pulseFade * .025);
                        }
                    }
                    double outside = (1 - coverage) / 255;
                    blue = blue * coverage + (backdrop == null ? Theme.Background.B : backdrop.Pixels[background]) * outside;
                    green = green * coverage + (backdrop == null ? Theme.Background.G : backdrop.Pixels[background + 1]) * outside;
                    red = red * coverage + (backdrop == null ? Theme.Background.R : backdrop.Pixels[background + 2]) * outside;
                    alpha = alpha * coverage + (backdrop == null ? 255 : backdrop.Pixels[background + 3]) * outside;
                    pixels[destination] = Byte(blue); pixels[destination + 1] = Byte(green);
                    pixels[destination + 2] = Byte(red); pixels[destination + 3] = Byte(alpha);
                }
                for (int y = 0; y < image.Height; y++) Marshal.Copy(pixels, y * image.Width * 4, IntPtr.Add(data.Scan0, y * data.Stride), image.Width * 4);
            }
            finally { image.UnlockBits(data); }
        }
        private static byte Byte(double value) { return (byte)Math.Max(0, Math.Min(255, Math.Round(value * 255))); }
        private static void Over(ref double blue, ref double green, ref double red, ref double alpha, int r, int g, int b, double opacity)
        {
            blue = blue * (1 - opacity) + b / 255.0 * opacity; green = green * (1 - opacity) + g / 255.0 * opacity;
            red = red * (1 - opacity) + r / 255.0 * opacity; alpha += (1 - alpha) * opacity;
        }
    }
}
