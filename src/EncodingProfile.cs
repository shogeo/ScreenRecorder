using System;
using System.IO;
using System.Text;

namespace Recording
{
    internal static class EncodingProfile
    {
        internal const int FramesPerSecond = 60;
        internal const int SampleRate = 48000;
        internal const int Channels = 2;
        internal static string EncoderPath => Path.Combine(Path.GetDirectoryName(typeof(EncodingProfile).Assembly.Location),"ffmpeg.exe");

        internal static string BuildArguments(string videoInput,string audioPipe,string outputPath)
        {
            return "-hide_banner -loglevel warning -stats_period 0.2 -progress pipe:1 -n " + videoInput +
                " -thread_queue_size 64 -probesize 32 -analyzeduration 0 -f f32le -ar 48000 -ac 2 -i " + Quote(audioPipe) +
                " -map 0:v:0 -map 1:a:0 -vf format=yuv420p -c:v h264_nvenc -preset p6 -tune hq -profile:v high -tag:v avc1" +
                " -rc vbr -cq 26 -b:v 2500k -maxrate 3000k -bufsize 6000k" +
                " -rc-lookahead 0 -delay 0 -bf 0 -spatial-aq 1 -temporal-aq 1 -g 120 -r 60 -fps_mode cfr" +
                " -c:a aac -b:a 128k -ar 48000 -ac 2 -shortest -movflags +faststart -f mp4 " + Quote(outputPath);
        }
        // Windows CommandLineToArgvW escaping, including trailing slashes and quotes.
        internal static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char ch in value)
            {
                if (ch == '\\') { slashes++; continue; }
                if (ch == '"') { result.Append('\\',slashes*2+1); result.Append('"'); slashes = 0; continue; }
                result.Append('\\',slashes); slashes = 0; result.Append(ch);
            }
            result.Append('\\',slashes*2); return result.Append('"').ToString();
        }
    }
}
