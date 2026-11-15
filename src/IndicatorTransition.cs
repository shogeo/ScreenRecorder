using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Recording
{
    internal sealed partial class IndicatorWindow
    {
        private const int SlideDurationMs = 230;
        private readonly Timer slideTimer = new Timer { Interval = 15 };
        private readonly Stopwatch slideClock = new Stopwatch();
        private Point rest;
        private int outsideY, slideStartY, slideEndY;
        private TaskCompletionSource<bool> dismissal;

        internal void ShowForRecording()
        {
            if (Visible && dismissal == null) return;

            slideTimer.Stop();
            dismissal?.TrySetResult(true);
            dismissal = null;
            bool animate = NativeBackdrop.AllowsMotion();
            if (!Visible)
            {
                int enabled = 1;
                DwmSetWindowAttribute(Handle, 3, ref enabled, sizeof(int)); // Use only our slide.
                bool cloaked = DwmSetWindowAttribute(Handle, 13, ref enabled, sizeof(int)) == 0;
                try
                {
                    // Warm up the on-screen redirect surface while DWM keeps it hidden.
                    Location = cloaked || !animate ? rest : new Point(rest.X, outsideY);
                    Show();
                    PaintGlassNow();
                    if (cloaked) DwmFlush();
                    if (animate) Location = new Point(rest.X, outsideY);
                }
                finally
                {
                    if (cloaked)
                    {
                        int disabled = 0;
                        DwmSetWindowAttribute(Handle, 13, ref disabled, sizeof(int));
                    }
                }
            }
            if (animate && Top != rest.Y) StartSlide(rest.Y);
            else Location = rest;
        }

        internal Task DismissAsync()
        {
            if (dismissal != null) return dismissal.Task;
            if (!Visible) return Task.CompletedTask;
            // Submit the updated label before moving the window on the next frame.
            Update();
            if (!NativeBackdrop.AllowsMotion())
            {
                slideTimer.Stop();
                Hide();
                Location = rest;
                return Task.CompletedTask;
            }

            dismissal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            StartSlide(outsideY);
            return dismissal.Task;
        }

        private void StartSlide(int destinationY)
        {
            slideStartY = Top;
            slideEndY = destinationY;
            slideClock.Restart();
            slideTimer.Start();
        }

        private void SlideFrame(object sender, EventArgs e)
        {
            double progress = Math.Min(1, slideClock.Elapsed.TotalMilliseconds / SlideDurationMs);
            double eased = dismissal == null ? 1-Math.Pow(1-progress, 3) : progress*(0.55+0.45*progress);
            int y = slideStartY+(int)Math.Round((slideEndY-slideStartY)*eased);
            if (progress < 1 || dismissal == null) Location = new Point(rest.X, y);
            if (progress < 1) return;

            slideTimer.Stop();
            if (dismissal == null) return;
            Hide();
            Location = rest;
            var finished = dismissal;
            dismissal = null;
            finished.TrySetResult(true);
        }

        [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    }
}
