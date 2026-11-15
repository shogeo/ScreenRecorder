using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Recording
{
    internal sealed class TrayMenuWindow : NativeGlassWindow
    {
        private string status = "Hotkeys active";
        internal TrayMenuWindow(Action open,Action quit) : base(false,232,152)
        {
            Text = "Recording menu";
            KeyPreview = true;
            var folder = new TrayActionButton("Open recordings",false) { AccessibleName = "Open recordings" };
            var exit = new TrayActionButton("Quit",true) { AccessibleName = "Quit Recording" };
            folder.Click += (s,e) => HideAnimated(open);
            exit.Click += (s,e) => HideAnimated(quit);
            AddGlassControl(folder,10,56,212,38);
            AddGlassControl(exit,10,98,212,38);
            Deactivate += (s,e) => HideAnimated();
            KeyDown += (s,e) => { if (e.KeyCode == Keys.Escape) HideAnimated(); };
        }
        internal void ShowAt(Point cursor,CaptureSession session)
        {
            if (Visible) { HideAnimated(); return; }
            CancelDismiss();
            status = session.State == CaptureState.Idle ? "Hotkeys active" : "Recording · "+MainWindow.TimeText(session.Elapsed);
            var area = Screen.FromPoint(cursor).WorkingArea;
            ClientSize = new Size(S(232),S(152));
            Location = new Point(Math.Max(area.Left+8,Math.Min(cursor.X-Width,area.Right-Width-8)),Math.Max(area.Top+8,Math.Min(cursor.Y-Height-8,area.Bottom-Height-8)));
            Show(); Activate(); SetForegroundWindow(Handle); Invalidate();
        }
        protected override void PaintSurface(Graphics g)
        {
            Theme.DrawText(g,"Recording",17,12,14,Theme.Text,true);
            Theme.DrawText(g,status,17,32,11,Theme.Muted);
        }
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    }
    internal sealed class TrayActionButton : GlassButton
    {
        private readonly bool quit;
        internal TrayActionButton(string text,bool quit) { Text = text; this.quit = quit; TabStop = true; }
        protected override float Radius => 8;
        protected override bool DeferActivation => true;
        protected override Color Tint => quit && PointerInside ? Color.FromArgb(90,38,53) : Color.FromArgb(38,49,67);
        protected override int ContentRevision => PointerInside ? 1 : 0;
        protected override void PaintContent(Graphics g,float width,float height)
        {
            Color color = quit ? Color.FromArgb(246,134,149) : Theme.Text;
            Theme.DrawText(g,Text,38,(height-14)/2-1,13,color);
            using (var pen = new Pen(color,1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float x = 21,y = height/2;
                if (quit) { g.DrawArc(pen,x-6,y-5,12,12,-50,280); g.DrawLine(pen,x,y-8,x,y-1); }
                else
                {
                    g.DrawLines(pen,new[] { new PointF(x-7,y+5),new PointF(x-7,y-5),new PointF(x-1,y-5),new PointF(x+1,y-2),new PointF(x+7,y-2),new PointF(x+7,y+5),new PointF(x-7,y+5) });
                }
            }
        }
    }
}
