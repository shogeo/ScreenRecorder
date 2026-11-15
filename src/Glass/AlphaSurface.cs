// Visual surface adapted from NLsalfetka/launcher/AlphaSurface.cs; no backend code.
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Recording
{
    // Keep premultiplied alpha in a DIB until the final BitBlt to the window DC.
    internal sealed class AlphaSurface : IDisposable
    {
        private IntPtr dc, dib, previous;
        private Bitmap image;
        internal Bitmap Image { get { return image; } }
        internal IntPtr DeviceContextForCheck { get { return dc; } }
        internal void Ensure(Size size)
        {
            if (image != null && image.Size == size) return;
            Dispose();
            var info = new BitmapInfo { Size = 40, Width = size.Width, Height = -size.Height, Planes = 1, BitCount = 32 };
            dc = CreateCompatibleDC(IntPtr.Zero);
            IntPtr pixels;
            dib = CreateDIBSection(dc, ref info, 0, out pixels, IntPtr.Zero, 0);
            if (dc == IntPtr.Zero || dib == IntPtr.Zero || pixels == IntPtr.Zero)
            { Dispose(); throw new Win32Exception(); }
            previous = SelectObject(dc, dib);
            image = new Bitmap(size.Width, size.Height, size.Width * 4, PixelFormat.Format32bppPArgb, pixels);
        }
        internal void Paint(Graphics destination, Size size, Rectangle clip, Action<Graphics, Rectangle> draw)
        {
            Ensure(size);
            clip.Intersect(new Rectangle(Point.Empty, size));
            if (clip.IsEmpty) return;
            using (Graphics canvas = Graphics.FromImage(image))
            { canvas.SetClip(clip); draw(canvas, clip); canvas.Flush(System.Drawing.Drawing2D.FlushIntention.Sync); }
            IntPtr target = destination.GetHdc();
            try
            {
                if (!BitBlt(target, clip.X, clip.Y, clip.Width, clip.Height, dc, clip.X, clip.Y, 0x00CC0020))
                    throw new Win32Exception();
            }
            finally { destination.ReleaseHdc(target); }
        }
        public void Dispose()
        {
            if (image != null) { image.Dispose(); image = null; }
            if (dc != IntPtr.Zero && previous != IntPtr.Zero) SelectObject(dc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (dc != IntPtr.Zero) DeleteDC(dc);
            dc = dib = previous = IntPtr.Zero;
        }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
        { internal uint Size; internal int Width, Height; internal ushort Planes, BitCount; internal uint Compression, ImageSize; internal int XPels, YPels; internal uint Used, Important; }
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint operation);
    }
}
