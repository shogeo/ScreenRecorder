using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Recording
{
    internal static class Program
    {
        internal static readonly int QuitMessage = RegisterWindowMessage("Recording.Quit.65061E9E-4086-45F1-A590-F7E837B6AC4F");
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => { Log.Write(e.Exception.ToString()); MessageBox.Show(e.Exception.Message, "Recording", MessageBoxButtons.OK, MessageBoxIcon.Error); };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write(e.ExceptionObject.ToString());
            Directory.CreateDirectory(Settings.DataDirectory);
            bool test = args.Contains("--self-test");
            using (var mutex = new Mutex(true, @"Local\Recording.65061E9E-4086-45F1-A590-F7E837B6AC4F", out bool first))
            {
                if (args.Contains("--quit"))
                {
                    if (!first)
                    {
                        IntPtr host = FindWindow(null, "Recording message host");
                        if (host != IntPtr.Zero) Hotkeys.PostMessage(host, QuitMessage, IntPtr.Zero, IntPtr.Zero);
                    }
                    return;
                }
                if (!first) return;
                try
                {
                    using (var window = new MainWindow())
                    {
                        if (test) window.BeginInvoke((Action)(async () => await SelfTest.Run(window)));
                        Application.Run();
                    }
                }
                catch (Exception e)
                {
                    Log.Write(e.ToString());
                    MessageBox.Show("Could not start Recording.\n\n" + e.Message + "\n\nCapture requires Media Foundation and Visual C++ Redistributable x64.", "Recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.ExitCode = 1;
                }
            }
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string message);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string windowName);
    }
}
