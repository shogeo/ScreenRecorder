using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Recording
{
    internal sealed class IndicatorWindow : NativeGlassWindow
    {
        private readonly RecorderButton system, mic, stop;
        private string timer = "00:00";
        private bool recordingPresence, raiseQueued;
        private readonly WinEventCallback visibilityCallback;
        private IntPtr foregroundHook, shownHook, restoredHook;
        internal IndicatorWindow(Action<Command> command) : base(true,264,44)
        {
            Text = "Recording indicator";
            visibilityCallback = VisibilityChanged;
            system = new RecorderButton(0) { AccessibleName = "Toggle system audio" };
            mic = new RecorderButton(1) { AccessibleName = "Toggle microphone" };
            stop = new RecorderButton(2) { AccessibleName = "Stop and save recording" };
            system.Click += (s,e) => command(Command.SystemAudio);
            mic.Click += (s,e) => command(Command.Microphone);
            stop.Click += (s,e) => command(Command.Record);
            AddGlassControl(system,130,6,36,32);
            AddGlassControl(mic,170,6,36,32);
            AddGlassControl(stop,210,6,36,32);
        }
        internal void Place(Screen screen)
        {
            CancelDismiss();
            var area = screen.WorkingArea;
            ClientSize = new Size(S(264),S(44));
            Location = new Point(area.Right-16-Width,area.Top+16);
            var handle = Handle;
        }
        internal void UpdateState(CaptureSession session)
        {
            string next = session.State == CaptureState.Starting ? "Starting…" : session.State == CaptureState.Finishing ? "Saving…" : MainWindow.TimeText(session.Elapsed);
            if (next != timer) { timer = next; Invalidate(); }
            system.Active = session.Audio.System;
            mic.Active = session.Audio.Mic;
            system.Enabled = mic.Enabled = stop.Enabled = session.State != CaptureState.Finishing;
        }
        internal void SetRecordingPresence(bool required)
        {
            if (recordingPresence == required) return;
            recordingPresence = required;
            if (required)
            {
                CancelDismiss();
                int enabled = 1;
                DwmSetWindowAttribute(Handle,11,ref enabled,4); // No taskbar Peek preview.
                DwmSetWindowAttribute(Handle,12,ref enabled,4); // Keep visible during Peek.
                // Out-of-context notifications; no code is loaded into another application.
                foregroundHook = SetWinEventHook(3,3,IntPtr.Zero,visibilityCallback,0,0,2);
                shownHook = SetWinEventHook(0x8002,0x8002,IntPtr.Zero,visibilityCallback,0,0,2);
                restoredHook = SetWinEventHook(0x17,0x17,IntPtr.Zero,visibilityCallback,0,0,2);
            }
            else ReleaseVisibilityHooks();
        }
        internal void EnsureVisibleAboveAll()
        {
            if (!recordingPresence || IsDisposed || Disposing || !IsHandleCreated) return;
            if (!Visible) Show();
            if (IsIconic(Handle)) ShowWindow(Handle,4); // Restore without taking focus.
            // Reassert the top of the topmost band, including above borderless fullscreen.
            // No activation, no geometry change, no foreground-window changes.
            SetWindowPos(Handle,(IntPtr)(-1),0,0,0,0,0x0001|0x0002|0x0010|0x0040|0x0200);
        }
        private void VisibilityChanged(IntPtr hook,uint eventType,IntPtr window,int objectId,int childId,uint thread,uint time)
        {
            if (!recordingPresence || (eventType == 0x8002 && (objectId != 0 || childId != 0))) return;
            QueueVisibilityRestore();
        }
        private void QueueVisibilityRestore()
        {
            if (raiseQueued || !recordingPresence || IsDisposed || Disposing || !IsHandleCreated) return;
            raiseQueued = true;
            try
            {
                BeginInvoke((Action)(() => { raiseQueued = false; EnsureVisibleAboveAll(); }));
            }
            catch (InvalidOperationException) { raiseQueued = false; }
        }
        protected override void WndProc(ref Message m)
        {
            if (recordingPresence && !Disposing)
            {
                if (m.Msg == 0x10 || (m.Msg == 0x112 && ((m.WParam.ToInt64()&0xFFF0) == 0xF020 || (m.WParam.ToInt64()&0xFFF0) == 0xF060)))
                    return; // Recording is stopped through the hotkey/button/tray, never by minimizing the HUD.
                if (m.Msg == 0x46 && m.LParam != IntPtr.Zero)
                {
                    var position = (WindowPosition)Marshal.PtrToStructure(m.LParam,typeof(WindowPosition));
                    if ((position.Flags&0x0080) != 0)
                        position.Flags = (position.Flags&~0x0080u)|0x0040|0x0010;
                    if ((position.Flags&0x0004) == 0)
                    {
                        position.InsertAfter = (IntPtr)(-1);
                        position.Flags |= 0x0010;
                    }
                    Marshal.StructureToPtr(position,m.LParam,false);
                }
            }
            int message = m.Msg;
            base.WndProc(ref m);
            if (recordingPresence && ((message == 0x18 && m.WParam == IntPtr.Zero) || (message == 5 && m.WParam == (IntPtr)1))) QueueVisibilityRestore();
        }
        private void ReleaseVisibilityHooks()
        {
            if (foregroundHook != IntPtr.Zero) UnhookWinEvent(foregroundHook);
            if (shownHook != IntPtr.Zero) UnhookWinEvent(shownHook);
            if (restoredHook != IntPtr.Zero) UnhookWinEvent(restoredHook);
            foregroundHook = shownHook = restoredHook = IntPtr.Zero;
        }
        protected override void Dispose(bool disposing)
        {
            recordingPresence = false;
            if (disposing) ReleaseVisibilityHooks();
            base.Dispose(disposing);
        }
        protected override void PaintSurface(Graphics g)
        {
            using (var brush = new SolidBrush(Color.FromArgb(246,134,149))) g.FillEllipse(brush,16,19.5f,5,5);
            Theme.DrawText(g,timer,31,timer.Contains(":") ? 12 : 14,timer.Contains(":") ? 14 : 12,Theme.Text,true);
            using (var pen = new Pen(Color.FromArgb(38,46,59))) g.DrawLine(pen,119,14,119,30);
        }
        private delegate void WinEventCallback(IntPtr hook,uint eventType,IntPtr window,int objectId,int childId,uint thread,uint time);
        [StructLayout(LayoutKind.Sequential)] private struct WindowPosition { public IntPtr Window,InsertAfter; public int X,Y,Width,Height; public uint Flags; }
        [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    }
    internal sealed class RecorderButton : GlassButton
    {
        private readonly int kind;
        private bool active;
        internal bool Active { get => active; set { if (active != value) { active = value; Invalidate(); } } }
        internal RecorderButton(int kind) { this.kind = kind; TabStop = false; }
        protected override float Radius => 8;
        protected override bool DeferActivation => kind == 2;
        protected override int ContentRevision => active ? 1 : 0;
        protected override Color Tint => active ? Color.FromArgb(57,128,230) : Color.FromArgb(38,49,67);
        protected override void PaintContent(Graphics g,float width,float height)
        {
            var saved = g.Save();
            g.TranslateTransform(width/2,height/2);
            Color color = Enabled ? active ? Theme.Text : Theme.Muted : Color.FromArgb(75,90,109);
            using (var pen = new Pen(color,1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (kind == 0)
                {
                    g.DrawPolygon(pen,new[] { new PointF(-7,-3),new PointF(-4,-3),new PointF(1,-7),new PointF(1,7),new PointF(-4,3),new PointF(-7,3) });
                    if (active) g.DrawArc(pen,-3,-6,12,12,-50,100);
                    else g.DrawLine(pen,-7,-7,8,8);
                }
                else if (kind == 1)
                {
                    using (var path = Theme.Rounded(new RectangleF(-3,-8,6,11),3)) g.DrawPath(pen,path);
                    g.DrawArc(pen,-6,-6,12,13,0,180);
                    g.DrawLine(pen,0,7,0,10); g.DrawLine(pen,-3,10,3,10);
                    if (!active) g.DrawLine(pen,-7,-7,8,8);
                }
                else using (var brush = new SolidBrush(Color.FromArgb(246,134,149)))
                using (var path = Theme.Rounded(new RectangleF(-4,-4,8,8),1.5f)) g.FillPath(brush,path);
            }
            g.Restore(saved);
        }
    }
}
