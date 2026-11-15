using System;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Recording
{
    internal sealed class Settings
    {
        public string Monitor { get; set; } = "";
        public int Framerate { get; set; } = 60;
        public bool ShowIndicator { get; set; } = true;

        public static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Recording");
        private static string FilePath => Path.Combine(DataDirectory, "settings.json");
        public static Settings Load()
        {
            try { return new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
            catch { return new Settings(); }
        }
        public void Save()
        {
            try { Directory.CreateDirectory(DataDirectory); File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this)); }
            catch (Exception e) { Log.Write("Settings: " + e); }
        }
        public static bool Autostart
        {
            get { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key?.GetValue("Recording") != null; }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (value) key.SetValue("Recording", "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --tray");
                    else key.DeleteValue("Recording", false);
                }
            }
        }
    }
    internal static class Log
    {
        private static readonly object Gate = new object();
        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Settings.DataDirectory);
                    string path = Path.Combine(Settings.DataDirectory, "app.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + "." + DateTime.Now.ToString("yyyyMMddHHmmss"));
                    File.AppendAllText(path, DateTime.Now.ToString("O") + " " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
