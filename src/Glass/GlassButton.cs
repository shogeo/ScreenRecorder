// Visual surface adapted from NLsalfetka/launcher/GlassButton.cs; no backend code.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Recording
{
    internal abstract class GlassButton : Button
    {
        private readonly AlphaSurface surface = new AlphaSurface();
        private readonly LiquidGlass glass = new LiquidGlass();
        private readonly GlassMotion motion = new GlassMotion();
        private readonly Timer animationTimer = new Timer { Interval = 16 };
        private readonly Timer activationTimer = new Timer { Interval = 150 };
        private readonly Stopwatch elapsed = new Stopwatch();
        private Bitmap supersampled;
        private Bitmap resolved;
        private byte[] resolvedPixels;
        private GlassBackdrop materialBackdrop;
        private string materialText;
        private int materialTint, materialRevision;
        private bool materialEnabled, materialValid;
        private float materialScale;
        private Point materialLocation;
        internal int MaterialBuildsForCheck { get; private set; }
        internal const int EdgeSampling = 3;
        private bool pointerClickArmed, activationPending, pointerInside;
        private bool frameActive;
        private long lastPaint;
        internal int MotionTickCountForCheck { get; private set; }
        internal float PaintScale = 1;
        internal bool AllowMotion;
        protected GlassPose Pose { get { return motion.Pose; } }
        protected bool PointerInside { get { return Enabled && pointerInside; } }
        protected virtual bool DeferActivation { get { return false; } }
        protected abstract float Radius { get; }
        protected abstract Color Tint { get; }
        protected abstract void PaintContent(Graphics g, float width, float height);
        protected virtual void PaintAnimatedContent(Graphics g, float width, float height) { }
        protected virtual int ContentRevision { get { return 0; } }

        protected GlassButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            Cursor = Cursors.Hand;
            animationTimer.Tick += delegate
            {
                float seconds = (float)elapsed.Elapsed.TotalSeconds; elapsed.Restart();
                if (!AdvanceClock(seconds)) { animationTimer.Stop(); elapsed.Stop(); }
            };
            activationTimer.Tick += delegate
            {
                activationTimer.Stop(); activationPending = false;
                if (!IsDisposed && Enabled) base.OnClick(EventArgs.Empty);
            };
        }
        private void Animate()
        {
            if (!AllowMotion)
            {
                animationTimer.Stop(); elapsed.Stop(); frameActive = false; motion.Reset();
                motion.Pose.Hover = pointerInside && Enabled ? 1 : 0; Invalidate(); return;
            }
            if (Visible && IsHandleCreated)
            {
                frameActive = true;
                var form = Parent as NativeGlassWindow;
                if (form != null && form.RequestButtonFrames()) { animationTimer.Stop(); elapsed.Stop(); }
                else if (!animationTimer.Enabled) { elapsed.Restart(); animationTimer.Start(); }
            }
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e) { pointerInside = true; motion.Hover(true); Animate(); base.OnMouseEnter(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var owner = Parent as NativeGlassWindow;
            if (owner != null) owner.SetCursorLight(new Point(Left + e.X, Top + e.Y));
            motion.Point(e.X * 2f / Math.Max(1, Width) - 1, e.Y * 2f / Math.Max(1, Height) - 1); Animate();
            if (Visible && IsHandleCreated && AllowMotion && Stopwatch.GetTimestamp() - lastPaint >= Stopwatch.Frequency / 125) Update();
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            pointerInside = false; motion.Hover(false); Animate();
            var owner = Parent as NativeGlassWindow; if (owner != null) owner.CheckCursorLightLeave();
            if (Visible && IsHandleCreated) Update();
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                pointerClickArmed = true;
                motion.Press(e.X / (float)Math.Max(1, Width), e.Y / (float)Math.Max(1, Height)); Animate();
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        { motion.Release(); Animate(); base.OnMouseUp(e); pointerClickArmed = false; }
        protected override void OnMouseCaptureChanged(EventArgs e)
        { if (!Capture) { motion.Release(); Animate(); } base.OnMouseCaptureChanged(e); }
        protected override void OnLostFocus(EventArgs e)
        { motion.Release(); Animate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && Enabled)
            { motion.Press(.5f, .5f); pointerClickArmed = true; Animate(); }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        { motion.Release(); Animate(); base.OnKeyUp(e); pointerClickArmed = false; }
        protected override void OnClick(EventArgs e)
        {
            if (!Enabled || activationPending) return;
            if (!pointerClickArmed) { motion.Press(.5f, .5f); motion.Release(); }
            Animate();
            if (DeferActivation && AllowMotion && Visible)
            { activationPending = true; activationTimer.Start(); }
            else base.OnClick(e);
        }
        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!Enabled) { motion.Hover(false); motion.Release(); }
            Cursor = Enabled ? Cursors.Hand : Cursors.Default; Animate(); base.OnEnabledChanged(e);
        }
        protected override void OnVisibleChanged(EventArgs e)
        { if (!Visible) SuspendMotion(true); base.OnVisibleChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        { surface.Paint(e.Graphics, ClientSize, e.ClipRectangle, delegate(Graphics graphics, Rectangle clip) { PaintControl(graphics); }); }
        private void PaintControl(Graphics g)
        {
            var size = new Size(Width * EdgeSampling, Height * EdgeSampling);
            if (supersampled == null || supersampled.Size != size)
            {
                if (supersampled != null) supersampled.Dispose();
                supersampled = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                if (resolved != null) resolved.Dispose();
                resolved = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                resolvedPixels = new byte[Width * Height * 4];
                materialValid = false;
            }
            var form = Parent as NativeGlassWindow;
            GlassBackdrop backdrop = form == null ? null : form.ButtonBackdrop;
            int tint = Tint.ToArgb(), revision = ContentRevision;
            if (!materialValid || materialBackdrop != backdrop || materialText != Text || materialTint != tint ||
                materialRevision != revision || materialEnabled != Enabled || materialScale != PaintScale || materialLocation != Location)
            {
                using (Graphics highResolution = Graphics.FromImage(supersampled))
                { highResolution.ScaleTransform(EdgeSampling, EdgeSampling); PaintBaseScene(highResolution); }
                materialBackdrop = backdrop; materialText = Text; materialTint = tint; materialRevision = revision;
                materialEnabled = Enabled; materialScale = PaintScale; materialLocation = Location; materialValid = true;
                MaterialBuildsForCheck++;
            }
            using (Graphics resolve = Graphics.FromImage(resolved))
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                Theme.Quality(resolve);
                if (form == null || !form.PaintChildBackground(resolve, this, ClientRectangle)) resolve.Clear(Theme.Background);
                resolve.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                resolve.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                resolve.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                resolve.ScaleTransform(PaintScale, PaintScale);
                float width = Width / PaintScale, height = Height / PaintScale;
                using (var transform = GlassMotion.Transform(new RectangleF(0, 0, width, height), motion.Pose)) resolve.MultiplyTransform(transform);
                attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                resolve.DrawImage(supersampled, new[] { new PointF(0, 0), new PointF(width, 0), new PointF(0, height) },
                    new RectangleF(0, 0, supersampled.Width, supersampled.Height), GraphicsUnit.Pixel, attributes);
                resolve.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                PaintAnimatedContent(resolve, width, height);
            }
            PointF cursor = form == null ? new PointF(Width * (.5f + motion.Pose.LightX * .5f), Height * (.5f + motion.Pose.LightY * .5f)) :
                new PointF(form.CursorLightPoint.X - Left, form.CursorLightPoint.Y - Top);
            bool lightVisible = PointerInside && (form == null || form.CursorLightVisible);
            EdgeCoverage.Apply(resolved, resolvedPixels, backdrop, Location, PaintScale, motion.Pose, Radius, true, cursor, lightVisible, Enabled);
            var saved = g.Save();
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.DrawImageUnscaled(resolved, Point.Empty);
            g.Restore(saved);
            lastPaint = Stopwatch.GetTimestamp();
        }
        private void PaintBaseScene(Graphics g)
        {
            Theme.Quality(g);
            // The 3x resolve supplies coverage; avoid the much slower GDI+
            // gamma-correct compositor on every high-resolution light pass.
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            var form = Parent as NativeGlassWindow;
            if (form == null || !form.PaintChildBackground(g, this, ClientRectangle)) g.Clear(Theme.Background);
            var saved = g.Save();
            g.ScaleTransform(PaintScale, PaintScale);
            float width = Width / PaintScale, height = Height / PaintScale;
            // Inset reserves room for tilt/rebound inside the unchanged hit target.
            var rect = new RectangleF(3, 3, width - 6, height - 6);
            glass.Draw(g, rect, Radius, Tint, 0, Enabled, false, form == null ? null : form.ButtonBackdrop,
                new Rectangle(Left + (int)(3 * PaintScale), Top + (int)(3 * PaintScale),
                    (int)Math.Round(rect.Width * PaintScale), (int)Math.Round(rect.Height * PaintScale)), PaintScale * EdgeSampling, true);
            PaintContent(g, width, height);
            g.Restore(saved);
        }
        internal void SuspendMotion(bool value)
        {
            animationTimer.Stop(); elapsed.Stop(); frameActive = false;
            if (value) { motion.Reset(); activationTimer.Stop(); activationPending = pointerClickArmed = pointerInside = false; Invalidate(); }
        }
        internal void SetHoverForCheck(float progress)
        { SetPoseForCheck(new GlassPose { Hover = Math.Max(0, Math.Min(1, progress)), Pulse = -1, OriginX = .5f, OriginY = .5f }); }
        internal void SetPoseForCheck(GlassPose pose)
        { animationTimer.Stop(); elapsed.Stop(); frameActive = false; pointerInside = pose.Hover > .001f; motion.SetForCheck(pose); Invalidate(); }
        internal void LeaveForCheck() { OnMouseLeave(EventArgs.Empty); }
        internal Color TintForCheck { get { return Tint; } }
        internal bool WantsFrames { get { return frameActive; } }
        internal bool AdvanceClock(float seconds)
        {
            if (!frameActive || !Visible || IsDisposed) { frameActive = false; return false; }
            MotionTickCountForCheck++; frameActive = motion.Advance(seconds); Invalidate(); return frameActive;
        }
        internal void PaintForCheck(Graphics graphics) { PaintControl(graphics); }
        internal void MoveForCheck(Point point) { OnMouseEnter(EventArgs.Empty); OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0)); }
        internal void DownForCheck(Point point) { OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)); }
        internal void ReleaseForCheck() { motion.Release(); pointerClickArmed = false; }
        internal bool AdvanceForCheck(float seconds) { return motion.Advance(seconds); }
        internal GlassPose PoseForCheck { get { return motion.Pose; } }
        internal bool AnimationTimerForCheck { get { return animationTimer.Enabled; } }
        internal void InvokeForCheck() { OnClick(EventArgs.Empty); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Dispose(); activationTimer.Dispose(); surface.Dispose(); glass.Dispose();
                if (supersampled != null) { supersampled.Dispose(); supersampled = null; }
                if (resolved != null) { resolved.Dispose(); resolved = null; }
                resolvedPixels = null;
            }
            base.Dispose(disposing);
        }
    }
}
