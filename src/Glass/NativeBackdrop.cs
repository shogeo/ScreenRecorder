// Native backdrop implementation from the reference loader.
using System;
using System.Runtime.InteropServices;
namespace Recording
{
    internal static class NativeBackdrop
    {
        internal static readonly System.Drawing.Color WindowTint = System.Drawing.Color.FromArgb(154, 9, 13, 21);

        internal static bool AllowsMotion() => !SystemParametersInfo(0x1042, 0, out bool enabled, 0) || enabled;

        internal static bool EnableSmoothWindowCorners(IntPtr handle)
        {
            try
            {
                int rounded = 2; // DWMWCP_ROUND: DWM owns the antialiased outer edge.
                if (DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int)) != 0) return false;
                int noBorder = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
                DwmSetWindowAttribute(handle, 34, ref noBorder, sizeof(int));
                int applied;
                return DwmGetWindowAttribute(handle, 33, out applied, sizeof(int)) == 0 && applied == rounded;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        internal static bool EnableBackdrop(IntPtr handle, out string mode, out bool alphaEnabled)
        {
            mode = "opaque fallback"; alphaEnabled = false;
            try
            {
                bool composition;
                if (DwmIsCompositionEnabled(out composition) != 0 || !composition) return false;
                int dark = 1, backdrop = 1;
                DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
                // 24H2 ignores redirection-bitmap alpha unless explicitly enabled.
                int useAlpha = 1;
                alphaEnabled = DwmSetWindowAttribute(handle, 39, ref useAlpha, sizeof(int)) == 0;
                // Let DWM blur the complete surface; the cached premultiplied tint remains translucent.
                var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                if (DwmExtendFrameIntoClientArea(handle, ref margins) != 0) return false;
                // Stock Desktop Acrylic has an opaque inactive fallback. Use the
                // existing accent path with a near-clear tint and our own dark layer.
                DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int));
                var accent = new AccentPolicy { State = 4, Flags = 0, GradientColor = unchecked((int)0x01150D09) };
                IntPtr data = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy)));
                try
                {
                    Marshal.StructureToPtr(accent, data, false);
                    var attribute = new CompositionData { Attribute = 19, Data = data, Size = Marshal.SizeOf(typeof(AccentPolicy)) };
                    if (SetWindowCompositionAttribute(handle, ref attribute) != 0) { mode = "accent acrylic"; return true; }
                    accent.State = 3;
                    Marshal.StructureToPtr(accent, data, false);
                    if (SetWindowCompositionAttribute(handle, ref attribute) != 0) { mode = "accent blur"; return true; }
                    backdrop = 3;
                    if (DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int)) == 0)
                    { mode = "system acrylic"; return true; }
                    return false;
                }
                finally { Marshal.FreeHGlobal(data); }
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        [StructLayout(LayoutKind.Sequential)] private struct Margins { internal int Left, Right, Top, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { internal int State, Flags, GradientColor, Animation; }
        [StructLayout(LayoutKind.Sequential)] private struct CompositionData { internal int Attribute; internal IntPtr Data; internal int Size; }
        [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled(out bool enabled);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
        [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
        [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr window, ref CompositionData data);
        [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint parameter, out bool value, uint flags);
    }
}
