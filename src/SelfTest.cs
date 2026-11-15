using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Recording
{
    // Opt-in integration harness. Presents an animated test card before recording the real display.
    internal static class SelfTest
    {
        internal static async Task Run(MainWindow window)
        {
            var root = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (root.Parent != null && !File.Exists(Path.Combine(root.FullName, "Recording.csproj"))) root = root.Parent;
            string directory = Path.Combine(root.FullName, "tests", "results");
            Directory.CreateDirectory(directory);
            var checks = new List<string>();
            var files = new List<string>();
            string error = null;
            using (var card = new TestCard())
            using (var toneStream = Tone())
            using (var tone = new SoundPlayer(toneStream))
            {
                try
                {
                    card.Show();
                    card.Activate();
                    await Task.Delay(700);
                    tone.PlayLooping();
                    card.Message = "01 · Silent / Win+Shift+R";
                    await Press(0x52);
                    await Until(() => window.Session.State == CaptureState.Recording, "start via global Win+Shift+R");
                    Assert(!window.Session.Audio.System && !window.Session.Audio.Mic, "R starts silent", checks);
                    Assert(!window.Visible, "no main window", checks);
                    GetWindowRect(window.IndicatorHandle, out RectangleNative indicatorBounds);
                    var screenBounds = Screen.PrimaryScreen.WorkingArea;
                    Assert(Math.Abs(screenBounds.Right - indicatorBounds.Right - 16) < 3 && Math.Abs(indicatorBounds.Top - screenBounds.Top - 16) < 3, "indicator at top right", checks);
                    Assert(GetWindowDisplayAffinity(window.IndicatorHandle, out uint affinity) && affinity == 0x11, "indicator excluded from capture", checks);
                    await Task.Delay(2000);
                    card.Message = "02 · System audio / Win+Shift+A";
                    await Press(0x41);
                    await Until(() => window.Session.Audio.System, "system hotkey");
                    Assert(!window.Session.Audio.Mic, "A enables only system", checks);
                    await Task.Delay(2000);
                    card.Message = "03 · Muted / A again";
                    await Press(0x41);
                    await Until(() => !window.Session.Audio.System, "system off");
                    await Task.Delay(2000);
                    card.Message = "04 · Microphone / Win+Shift+M";
                    await Press(0x4D);
                    await Until(() => window.Session.Audio.Mic, "mic hotkey");
                    await Task.Delay(1500);
                    await Press(0x4D);
                    await Until(() => !window.Session.Audio.Mic, "mic off");
                    Assert(!window.Session.Audio.System, "M toggles independently", checks);
                    card.Message = "05 · Both sources / Win+Shift+B";
                    await Press(0x42);
                    await Until(() => window.Session.Audio.System && window.Session.Audio.Mic, "both on");
                    await Task.Delay(2000);
                    card.Message = "06 · Both muted / B again";
                    await Press(0x42);
                    await Until(() => !window.Session.Audio.System && !window.Session.Audio.Mic, "both off");
                    await Task.Delay(1500);
                    await Press(0x52);
                    await Until(() => window.Session.State == CaptureState.Finishing || window.Session.State == CaptureState.Idle, "stop via global Win+Shift+R");
                    string first = await WithTimeout(window.Session.Completion, 20000);
                    await Until(() => window.ClipboardFile == first, "clipboard persistence");
                    Assert(File.Exists(first) && first.EndsWith(".mp4") && !first.EndsWith(".recording.mp4"), "finalized MP4", checks);
                    Assert(Path.GetDirectoryName(first) == CaptureSession.OutputFolder, "Windows Videos/Recordings", checks);
                    Assert(Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Contains(first), "clipboard contains actual file", checks);
                    string testCopy = Path.Combine(directory, "hotkeys-toggle.mp4");
                    File.Copy(first, testCopy, true);
                    files.Add(testCopy);

                    foreach (Command command in new[] { Command.SystemAudio, Command.Microphone, Command.Both })
                    {
                        card.Message = "Start with audio · " + command;
                        await Press(command == Command.SystemAudio ? (byte)0x41 : command == Command.Microphone ? (byte)0x4D : (byte)0x42);
                        await Until(() => window.Session.State == CaptureState.Recording, "start " + command);
                        AudioMode expected = AudioMode.FromCommand(command);
                        Assert(window.Session.Audio.System == expected.System && window.Session.Audio.Mic == expected.Mic, "idle " + command + " starts selected audio", checks);
                        await Task.Delay(1800);
                        await Press(0x52);
                        string file = await WithTimeout(window.Session.Completion, 20000);
                        await Until(() => window.ClipboardFile == file, "clipboard " + command);
                        testCopy = Path.Combine(directory, "start-" + command + ".mp4");
                        File.Copy(file, testCopy, true);
                        files.Add(testCopy);
                    }
                    // Startup commands issued back-to-back must finish the first session cleanly.
                    card.Message = "Quick start and stop";
                    window.HandleCommand(Command.Record);
                    window.HandleCommand(Command.Record);
                    string quick = await WithTimeout(window.Session.Completion, 20000);
                    Assert(File.Exists(quick), "stop during startup finalizes", checks);
                    await Until(() => window.ClipboardFile == quick, "quick clipboard");
                    Assert(window.Session.State == CaptureState.Idle, "ready after quick stop", checks);

                    // Partial B state converges to both enabled, then both disabled.
                    var partial = new AudioMode(true, false).Toggle(Command.Both);
                    Assert(partial.System && partial.Mic, "B from partial enables both", checks);
                    partial = partial.Toggle(Command.Both);
                    Assert(!partial.System && !partial.Mic, "B from both disables both", checks);
                }
                catch (Exception e) { error = e.ToString(); Log.Write("Self-test: " + error); }
                finally { tone.Stop(); }
            }
            File.WriteAllText(Path.Combine(directory, "integration.json"), new JavaScriptSerializer().Serialize(new { success = error == null, checks, files, error, capturedAt = DateTime.Now.ToString("O") }));
            if (error != null) Environment.ExitCode = 1;
            Application.Exit();
        }
        private static async Task Until(Func<bool> condition, string label)
        {
            DateTime end = DateTime.UtcNow.AddSeconds(15);
            while (!condition()) { if (DateTime.UtcNow > end) throw new Exception("Timeout: " + label); await Task.Delay(50); }
        }
        private static async Task<T> WithTimeout<T>(Task<T> task, int milliseconds)
        {
            if (await Task.WhenAny(task, Task.Delay(milliseconds)) != task) throw new Exception("Recording completion timeout");
            return await task;
        }
        private static void Assert(bool condition, string label, List<string> checks)
        {
            if (!condition) throw new Exception("Failed: " + label);
            checks.Add(label);
        }
        private static async Task Press(byte key)
        {
            keybd_event(0x5B, 0, 0, UIntPtr.Zero);
            keybd_event(0xA0, 0, 0, UIntPtr.Zero);
            await Task.Delay(40);
            keybd_event(key, 0, 0, UIntPtr.Zero);
            await Task.Delay(40);
            keybd_event(key, 0, 2, UIntPtr.Zero);
            keybd_event(0xA0, 0, 2, UIntPtr.Zero);
            keybd_event(0x5B, 0, 2, UIntPtr.Zero);
            await Task.Delay(150);
        }
        private static MemoryStream Tone()
        {
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
            {
                int samples = 48000;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(48000); writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (int i = 0; i < samples; i++) writer.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / 48000) * 4500));
            }
            stream.Position = 0;
            return stream;
        }
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        [StructLayout(LayoutKind.Sequential)] private struct RectangleNative { public int Left,Top,Right,Bottom; }
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out RectangleNative rectangle);
        [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(IntPtr handle, out uint affinity);
        private sealed class TestCard : Form
        {
            private readonly Timer animation;
            private int frame;
            public string Message = "Recording · Integration test";
            public TestCard()
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                Bounds = Screen.PrimaryScreen.Bounds;
                BackColor = Theme.Background;
                DoubleBuffered = true;
                TopMost = true;
                animation = new Timer { Interval = 33 };
                animation.Tick += (s, e) => { frame++; Invalidate(); };
                animation.Start();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var title = Theme.Font(36, FontStyle.Bold))
                using (var subtitle = Theme.Font(20))
                using (var brush = new SolidBrush(Theme.Text))
                using (var accent = new SolidBrush(Theme.Accent))
                {
                    e.Graphics.DrawString("Recording / capture test", title, brush, 80, 100);
                    e.Graphics.DrawString(Message, subtitle, brush, 80, 210);
                    e.Graphics.DrawString("Animation frame: " + frame, subtitle, brush, 80, 270);
                    e.Graphics.FillEllipse(accent, 80 + frame * 8 % Math.Max(1, Width - 240), Height / 2, 100, 100);
                }
            }
            protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
        }
    }
}
