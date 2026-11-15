using System;
using System.Windows.Forms;
using SharpDX.DXGI;

namespace Recording
{
    internal static class DesktopInput
    {
        internal static string ForScreen(Screen screen)
        {
            using (var factory = new Factory1())
            {
                var adapters = factory.Adapters1;
                try
                {
                    for (int adapterIndex = 0; adapterIndex < adapters.Length; adapterIndex++)
                    {
                        var outputs = adapters[adapterIndex].Outputs;
                        try
                        {
                            for (int outputIndex = 0; outputIndex < outputs.Length; outputIndex++)
                            {
                                if (!string.Equals(outputs[outputIndex].Description.DeviceName,screen.DeviceName,StringComparison.OrdinalIgnoreCase)) continue;
                                Log.Write("Desktop duplication: adapter="+adapterIndex+" output="+outputIndex+" display="+screen.DeviceName);
                                return "-init_hw_device d3d11va=desktop:"+adapterIndex+" -filter_hw_device desktop -thread_queue_size 16 -f lavfi -i "+
                                    EncodingProfile.Quote("ddagrab=output_idx="+outputIndex+":framerate=60:draw_mouse=1:dup_frames=1,hwdownload,format=bgra");
                            }
                        }
                        finally { foreach (var output in outputs) output.Dispose(); }
                    }
                }
                finally { foreach (var adapter in adapters) adapter.Dispose(); }
            }
            throw new InvalidOperationException("The selected display is not available to Windows Desktop Duplication.");
        }
    }
}
