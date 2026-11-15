using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
namespace Recording
{
    // Never shown. Owns the message pump, global hotkeys and tray lifetime.
    internal sealed class MainWindow : Form
    {
        internal readonly CaptureSession Session;
        private readonly Settings options = new Settings();
        private readonly NotifyIcon tray;
        private readonly Icon idleIcon,recordingIcon;
        private readonly IndicatorWindow indicator;
        private readonly TrayMenuWindow trayMenu;
        private readonly Timer tick;
        private readonly Hotkeys hotkeys;
        private bool exiting;
        private CaptureState lastState;
        private int clipboardGeneration;
        internal string ClipboardFile { get; private set; }
        internal IntPtr IndicatorHandle => indicator.Handle;
        public MainWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Text = "Recording message host";
            CreateHandle();
            idleIcon = Theme.MakeIcon(false); recordingIcon = Theme.MakeIcon(true);
            Session = new CaptureSession(this);
            indicator = new IndicatorWindow(HandleCommand);
            indicator.Place(Screen.PrimaryScreen);
            trayMenu = new TrayMenuWindow(OpenFolder,async () => await ExitApp());
            tray = new NotifyIcon { Icon = idleIcon, Visible = true };
            tray.MouseUp += (s,e) => {
                if (e.Button == MouseButtons.Right && !exiting)
                {
                    var point = Cursor.Position;
                    BeginInvoke((Action)(() => trayMenu.ShowAt(point,Session)));
                }
            };
            tray.DoubleClick += (s,e) => OpenFolder();
            Session.Changed += RefreshState;
            Session.Saved += async path => await CopyFile(path);
            Session.Failed += ShowError;
            hotkeys = new Hotkeys(Handle,HandleCommand);
            tick = new Timer { Interval = 250 };
            tick.Tick += (s,e) => UpdateClock(); tick.Start();
            SystemEvents.SessionSwitch += SessionSwitch;
            SystemEvents.PowerModeChanged += PowerChanged;
            SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        }
        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312) hotkeys?.HandleMessage(m.WParam);
            else if (m.Msg == 0x8001) HandleCommand((Command)m.WParam.ToInt32());
            else if (m.Msg == Program.QuitMessage) BeginInvoke((Action)(async () => await ExitApp()));
            base.WndProc(ref m);
        }
        internal void HandleCommand(Command command)
        {
            if (exiting) return;
            try { Log.Write("Command "+command+"; state="+Session.State); Session.Handle(command,options); }
            catch (Exception e) { Log.Write(e.ToString()); ShowError(e.Message); }
        }
        internal static string TimeText(TimeSpan time) => time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");
        private void UpdateClock()
        {
            if (Session.State != CaptureState.Starting && Session.State != CaptureState.Recording) return;
            indicator.UpdateState(Session);
            indicator.EnsureVisibleAboveAll();
        }
        private void RefreshState()
        {
            var state = Session.State;
            bool idle = state == CaptureState.Idle;
            tray.Icon = idle ? idleIcon : recordingIcon;
            if (idle || state == CaptureState.Finishing)
            {
                if (state == CaptureState.Finishing) indicator.UpdateState(Session);
                indicator.SetRecordingPresence(false);
                _ = indicator.DismissAsync();
            }
            else
            {
                indicator.UpdateState(Session);
                if (lastState == CaptureState.Idle) indicator.Place(Screen.PrimaryScreen);
                indicator.SetRecordingPresence(true);
                indicator.ShowForRecording();
                indicator.EnsureVisibleAboveAll();
            }
            lastState = state;
        }
        internal async Task<bool> CopyFile(string path)
        {
            int generation = ++clipboardGeneration;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (generation != clipboardGeneration || IsDisposed) return false;
                try
                {
                    var data = new DataObject();
                    data.SetFileDropList(new StringCollection { path });
                    data.SetData("Preferred DropEffect",new MemoryStream(BitConverter.GetBytes(1)));
                    Clipboard.SetDataObject(data,true,1,0);
                    ClipboardFile = path; return true;
                }
                catch (System.Runtime.InteropServices.ExternalException) { await Task.Delay(150); }
            }
            ShowError("MP4 saved in Videos\\Recordings. The clipboard was busy and could not accept the file.");
            return false;
        }
        private void ShowError(string message) { Log.Write("Error: "+message); tray.ShowBalloonTip(5000,"Recording error",message,ToolTipIcon.Error); }
        private void OpenFolder()
        {
            try { Directory.CreateDirectory(CaptureSession.OutputFolder); Process.Start("explorer.exe","\""+CaptureSession.OutputFolder+"\""); }
            catch (Exception e) { ShowError(e.Message); }
        }
        private async Task ExitApp()
        {
            if (exiting) return;
            exiting = true;
            if (Session.State != CaptureState.Idle)
            {
                Session.Stop();
                try { await Session.Completion; } catch { }
                for (int i = 0; i < 15 && Session.LastFile != null && ClipboardFile != Session.LastFile; i++) await Task.Delay(150);
            }
            await indicator.DismissAsync();
            Application.ExitThread();
        }
        private void SessionSwitch(object sender,SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.SessionLogoff || e.Reason == SessionSwitchReason.RemoteDisconnect || e.Reason == SessionSwitchReason.ConsoleDisconnect) SafeDispatch(Session.Stop);
        }
        private void PowerChanged(object sender,PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Suspend) SafeDispatch(Session.Stop); }
        private void DisplaysChanged(object sender,EventArgs e) => SafeDispatch(Session.Stop);
        private void SafeDispatch(Action action) { if (!IsDisposed && IsHandleCreated) { try { BeginInvoke(action); } catch (InvalidOperationException) { } } }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.SessionSwitch -= SessionSwitch; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
                tick?.Dispose(); hotkeys?.Dispose(); indicator?.Close(); trayMenu?.Close(); Session?.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                idleIcon?.Dispose(); recordingIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
