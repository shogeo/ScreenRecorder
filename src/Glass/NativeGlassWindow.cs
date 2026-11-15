using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Recording
{
    internal abstract class NativeGlassWindow : Form
    {
        private readonly AlphaSurface surface = new AlphaSurface();
        private readonly Dictionary<GlassButton,Rectangle> controlLayout = new Dictionary<GlassButton,Rectangle>();
        private readonly Timer dismissTimer = new Timer { Interval = 16 };
        private readonly Stopwatch dismissClock = new Stopwatch();
        private readonly bool noActivation;
        private readonly int logicalWidth, logicalHeight;
        private Bitmap background;
        private GlassBackdrop backdrop;
        private bool nativeBlur;
        private Rectangle dismissBounds;
        private Action afterDismiss;
        protected float ScaleX => ClientSize.Width/(float)logicalWidth;
        protected float ScaleY => ClientSize.Height/(float)logicalHeight;
        protected int S(float value) => (int)Math.Round(value*GetDpiForSystem()/96f);
        internal Point CursorLightPoint { get; private set; }
        internal bool CursorLightVisible => ClientRectangle.Contains(PointToClient(Cursor.Position));
        internal GlassBackdrop ButtonBackdrop { get { EnsureBackground(); return backdrop; } }
        protected NativeGlassWindow(bool noActivation,int width,int height)
        {
            this.noActivation = noActivation;
            logicalWidth = width; logicalHeight = height;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Background;
            ClientSize = new Size(S(width),S(height));
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer,false);
            dismissTimer.Tick += DismissFrame;
        }
        protected override bool ShowWithoutActivation => noActivation;
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style &= ~(0x00C00000|0x00040000);
                cp.ExStyle |= 0x80;
                if (noActivation) cp.ExStyle |= 0x08000000;
                cp.ClassStyle &= ~0x00020000; // No CS_DROPSHADOW.
                return cp;
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ConfigureBackdrop();
            ScreenRecorderLib.Recorder.SetExcludeFromCapture(Handle,true);
        }
        private void ConfigureBackdrop()
        {
            nativeBlur = NativeBackdrop.EnableBackdrop(Handle,out string mode,out bool alpha);
            Region = null;
            UpdateWindowCorners();
            Log.Write("Native glass surface: "+mode+"; premultiplied alpha="+alpha);
            ClearBackground();
        }
        private void UpdateWindowCorners()
        {
            if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            if (NativeBackdrop.EnableSmoothWindowCorners(Handle))
            {
                if (Region != null) Region = null;
                return;
            }
            using (var path = Theme.Rounded(ClientRectangle, S(12))) Region = new Region(path);
        }
        protected override void WndProc(ref Message m)
        {
            int message = m.Msg;
            base.WndProc(ref m);
            if ((message == 0x031E || message == 0x031A) && IsHandleCreated && !IsDisposed) ConfigureBackdrop();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (logicalWidth == 0 || logicalHeight == 0) return;
            if (Region != null) UpdateWindowCorners();
            ClearBackground();
            foreach (var entry in controlLayout)
            {
                var r = entry.Value;
                entry.Key.PaintScale = ScaleX;
                entry.Key.Bounds = new Rectangle((int)Math.Round(r.X*ScaleX),(int)Math.Round(r.Y*ScaleY),Math.Max(1,(int)Math.Round(r.Width*ScaleX)),Math.Max(1,(int)Math.Round(r.Height*ScaleY)));
            }
        }
        protected void AddGlassControl(GlassButton control,int x,int y,int width,int height)
        {
            controlLayout.Add(control,new Rectangle(x,y,width,height));
            control.PaintScale = ScaleX;
            control.AllowMotion = true;
            control.Bounds = new Rectangle((int)Math.Round(x*ScaleX),(int)Math.Round(y*ScaleY),(int)Math.Round(width*ScaleX),(int)Math.Round(height*ScaleY));
            Controls.Add(control);
        }
        private void ClearBackground()
        {
            background?.Dispose(); background = null; backdrop = null;
            Invalidate(true);
        }
        private void EnsureBackground()
        {
            if (background != null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            background = new Bitmap(ClientSize.Width,ClientSize.Height,PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(background))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.Clear(nativeBlur ? NativeBackdrop.WindowTint : Theme.Background);
            }
            backdrop = new GlassBackdrop(background);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            EnsureBackground();
            surface.Paint(e.Graphics,ClientSize,e.ClipRectangle,(g,clip) => {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(background,clip,clip,GraphicsUnit.Pixel);
                g.CompositingMode = CompositingMode.SourceOver;
                Theme.Quality(g);
                g.ScaleTransform(ScaleX,ScaleY);
                PaintSurface(g);
            });
        }
        protected void PaintGlassNow()
        {
            // Paint the real redirected client and its child buttons before revealing the HWND.
            const uint repaintClientAndChildren = 0x0001 | 0x0020 | 0x0080 | 0x0100;
            RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, repaintClientAndChildren);
        }
        protected abstract void PaintSurface(Graphics g);
        internal bool PaintChildBackground(Graphics g,Control child,Rectangle clip)
        {
            EnsureBackground();
            if (background == null) return false;
            var saved = g.Save();
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(background,new Rectangle(0,0,child.Width,child.Height),new Rectangle(child.Left,child.Top,child.Width,child.Height),GraphicsUnit.Pixel);
            g.Restore(saved); return true;
        }
        internal bool RequestButtonFrames() => false;
        internal void SetCursorLight(Point point) { CursorLightPoint = point; }
        internal void CheckCursorLightLeave() { }
        internal void CancelDismiss()
        {
            if (dismissTimer.Enabled) { dismissTimer.Stop(); Bounds = dismissBounds; }
            afterDismiss = null;
        }
        internal void HideAnimated(Action completion = null)
        {
            if (!Visible) { completion?.Invoke(); return; }
            if (dismissTimer.Enabled) { if (completion != null) afterDismiss = completion; return; }
            if (!SystemInformation.IsMenuAnimationEnabled) { Hide(); completion?.Invoke(); return; }
            dismissBounds = Bounds; afterDismiss = completion;
            dismissClock.Restart(); dismissTimer.Start();
        }
        private void DismissFrame(object sender,EventArgs e)
        {
            double progress = Math.Min(1,dismissClock.Elapsed.TotalMilliseconds/150);
            double eased = 1-Math.Pow(1-progress,3);
            int width = Math.Max(1,(int)Math.Round(dismissBounds.Width*(1-.045*eased)));
            int height = Math.Max(1,(int)Math.Round(dismissBounds.Height*(1-.14*eased)));
            Bounds = new Rectangle(dismissBounds.X+(dismissBounds.Width-width)/2,dismissBounds.Y+(dismissBounds.Height-height)/2,width,height);
            if (progress < 1) return;
            dismissTimer.Stop(); Hide(); Bounds = dismissBounds;
            var callback = afterDismiss; afterDismiss = null; callback?.Invoke();
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) foreach (var button in controlLayout.Keys) button.SuspendMotion(true);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { dismissTimer.Dispose(); surface.Dispose(); background?.Dispose(); background = null; }
            base.Dispose(disposing);
        }
        [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr handle, IntPtr rect, IntPtr region, uint flags);
    }
}
