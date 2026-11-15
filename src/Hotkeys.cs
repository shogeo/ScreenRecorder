using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Recording
{
    internal enum Command { Record, SystemAudio, Microphone, Both }
    internal sealed class Hotkeys : IDisposable
    {
        private readonly IntPtr window;
        private readonly HashSet<int> registered = new HashSet<int>();
        private readonly Dictionary<int, Command> fallback = new Dictionary<int, Command>();
        private readonly HashSet<int> down = new HashSet<int>();
        private readonly HashSet<int> modifiersDown = new HashSet<int>();
        private readonly HookProc callback;
        private readonly Action<Command> invoke;
        private IntPtr hook;
        private Thread hookThread;
        private uint hookThreadId;
        public bool UsesFallback => fallback.Count != 0;
        private static readonly int[] KeysToBind = { 0x52, 0x41, 0x4D, 0x42 };
        public Hotkeys(IntPtr hwnd, Action<Command> onCommand)
        {
            window = hwnd;
            invoke = onCommand;
            for (int i = 0; i < KeysToBind.Length; i++)
            {
                if (RegisterHotKey(hwnd, 100 + i, 0x0008 | 0x0004 | 0x4000, (uint)KeysToBind[i])) registered.Add(100 + i);
                else { fallback[KeysToBind[i]] = (Command)i; Log.Write("Hotkey fallback: " + (Command)i + ", error=" + Marshal.GetLastWin32Error()); }
            }
            callback = KeyboardHook;
            if (fallback.Count != 0)
            {
                // Windows silently removes a slow low-level hook. Its message pump must stay
                // independent of capture/encoder initialization and of UI repainting.
                using (var ready = new ManualResetEventSlim())
                {
                    hookThread = new Thread(() => {
                        hookThreadId = GetCurrentThreadId();
                        using (var process = Process.GetCurrentProcess())
                        using (var module = process.MainModule)
                            hook = SetWindowsHookEx(13, callback, GetModuleHandle(module.ModuleName), 0);
                        ready.Set();
                        if (hook != IntPtr.Zero)
                        {
                            Application.Run();
                            UnhookWindowsHookEx(hook);
                            hook = IntPtr.Zero;
                        }
                    }) { IsBackground = true, Name = "Recording hotkeys" };
                    hookThread.SetApartmentState(ApartmentState.STA);
                    hookThread.Start();
                    ready.Wait();
                }
                if (hook == IntPtr.Zero)
                {
                    Dispose();
                    throw new InvalidOperationException("Could not connect global hotkeys. Windows error: " + Marshal.GetLastWin32Error());
                }
            }
        }
        public void HandleMessage(IntPtr id)
        {
            int number = id.ToInt32();
            if (registered.Contains(number)) invoke((Command)(number - 100));
        }
        private IntPtr KeyboardHook(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                int key = Marshal.ReadInt32(data);
                int msg = message.ToInt32();
                bool up = msg == 0x101 || msg == 0x105;
                bool modifier = key == 0x5B || key == 0x5C || key == 0xA0 || key == 0xA1 || key == 0xA2 || key == 0xA3 || key == 0xA4 || key == 0xA5;
                if (modifier) { if (up) modifiersDown.Remove(key); else modifiersDown.Add(key); }
                if (up)
                {
                    // Consume the matching key-up even if modifiers were released first.
                    if (down.Remove(key)) return (IntPtr)1;
                }
                else if (msg == 0x100 || msg == 0x104)
                {
                    if (down.Contains(key)) return (IntPtr)1;
                    if (fallback.TryGetValue(key, out Command command) &&
                        (modifiersDown.Contains(0x5B) || modifiersDown.Contains(0x5C)) &&
                        (modifiersDown.Contains(0xA0) || modifiersDown.Contains(0xA1)) &&
                        !(modifiersDown.Contains(0xA2) || modifiersDown.Contains(0xA3) || modifiersDown.Contains(0xA4) || modifiersDown.Contains(0xA5)))
                    {
                        down.Add(key);
                        // Dispatch after returning from the hook; capture initialization must never block it.
                        PostMessage(window, 0x8001, (IntPtr)(int)command, IntPtr.Zero);
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(hook, code, message, data);
        }
        public void Dispose()
        {
            foreach (int id in registered) UnregisterHotKey(window, id);
            registered.Clear();
            if (hookThread != null && hookThread.IsAlive)
            {
                PostThreadMessage(hookThreadId, 0x12, IntPtr.Zero, IntPtr.Zero);
                hookThread.Join(2000);
            }
        }
        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int type, HookProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, int message, IntPtr wparam, IntPtr lparam);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    }
}
