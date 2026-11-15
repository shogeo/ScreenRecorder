using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
namespace Recording
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(14,17,23), Text = Color.FromArgb(234,240,250), Accent = Color.FromArgb(84,154,246);
        public static readonly Color Muted = Color.FromArgb(133,151,175), White = Text;
        public static Font Font(float size = 10, FontStyle style = FontStyle.Regular) => new Font("Segoe UI",size,style,GraphicsUnit.Pixel);
        internal static void Quality(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }
        internal static GraphicsPath Rounded(RectangleF rect,float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius*2,Math.Min(rect.Width,rect.Height));
            path.AddArc(rect.X,rect.Y,diameter,diameter,180,90);
            path.AddArc(rect.Right-diameter,rect.Y,diameter,diameter,270,90);
            path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
            path.AddArc(rect.X,rect.Bottom-diameter,diameter,diameter,90,90);
            path.CloseFigure(); return path;
        }
        internal static void DrawText(Graphics g,string text,float x,float y,float size,Color color,bool bold = false)
        {
            using (var font = Font(size,bold ? FontStyle.Bold : FontStyle.Regular))
            using (var brush = new SolidBrush(color)) g.DrawString(text,font,brush,x,y,StringFormat.GenericTypographic);
        }
        public static Icon MakeIcon(bool active)
        {
            using (var bitmap = new Bitmap(32,32))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(active ? Color.FromArgb(255,92,115) : Accent)) g.FillEllipse(brush,2,2,28,28);
                if (active) { using (var brush = new SolidBrush(Background)) g.FillRectangle(brush,11,11,10,10); }
                else { using (var pen = new Pen(Background,3)) g.DrawEllipse(pen,10,10,12,12); }
                IntPtr handle = bitmap.GetHicon();
                try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    }
}
