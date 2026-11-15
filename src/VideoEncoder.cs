using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Recording
{
    internal sealed class VideoEncoder : IDisposable
    {
        private readonly Process process;
        private readonly NamedPipeServerStream audioPipe;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Queue<string> errors = new Queue<string>();
        private readonly bool synthetic;
        private readonly object modeGate = new object();
        private StereoAudioMixer mixer;
        private EncoderJob job;
        private Task audioTask;
        private AudioMode mode;
        private bool audioConnected, started, disposed;
        private volatile bool stopRequested;
        private Exception audioFailure;
        internal Task Completion { get; private set; } = Task.CompletedTask;
        internal VideoEncoder(string videoInput,string outputPath,bool synthetic = false)
        {
            this.synthetic = synthetic;
            if (!File.Exists(EncodingProfile.EncoderPath)) throw new FileNotFoundException("ffmpeg.exe is missing. Reinstall Recording's complete folder.",EncodingProfile.EncoderPath);
            string pipeName = "Recording-audio-"+Guid.NewGuid().ToString("N");
            audioPipe = new NamedPipeServerStream(pipeName,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,0,65536);
            process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = EncodingProfile.EncoderPath,
                    Arguments = EncodingProfile.BuildArguments(videoInput,@"\\.\pipe\"+pipeName,outputPath),
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(EncodingProfile.EncoderPath)
                }, EnableRaisingEvents = true
            };
            process.Exited += (s,e) => exited.TrySetResult(true);
            process.OutputDataReceived += (s,e) => {
                if (e.Data != null && e.Data.StartsWith("frame=",StringComparison.Ordinal) && long.TryParse(e.Data.Substring(6),NumberStyles.Integer,CultureInfo.InvariantCulture,out long frames) && frames > 0) ready.TrySetResult(true);
            };
            process.ErrorDataReceived += (s,e) => {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                lock (errors) { errors.Enqueue(e.Data); while (errors.Count > 20) errors.Dequeue(); }
            };
        }
        internal async Task StartAsync(AudioMode initialMode)
        {
            if (!synthetic) mixer = await Task.Run(() => new StereoAudioMixer()).ConfigureAwait(false);
            lock (modeGate) mode = initialMode;
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (!process.Start()) throw new IOException("Could not start the Video encoder.");
                started = true;
                job = new EncoderJob(process);
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                audioTask = Task.Run(AudioPump);
                Completion = MonitorExit();
                Task first = await Task.WhenAny(ready.Task,Completion,Task.Delay(15000,cancellation.Token)).ConfigureAwait(false);
                if (first == Completion) { await Completion.ConfigureAwait(false); throw new IOException("Video recording ended before the first video frame."); }
                if (first != ready.Task) throw new TimeoutException("Video capture did not start within 15 seconds. Check the NVIDIA driver and display availability.");
                await ready.Task.ConfigureAwait(false);
            }
            catch { Abort(); throw; }
        }
        internal void SetMode(AudioMode value)
        {
            lock (modeGate)
            {
                if (audioConnected) mixer?.SetMode(value);
                mode = value;
            }
        }
        private async Task AudioPump()
        {
            try
            {
                await audioPipe.WaitForConnectionAsync(cancellation.Token).ConfigureAwait(false);
                lock (modeGate) { mixer?.SetMode(mode); audioConnected = true; }
                var samples = new float[960]; // 480 frames × two channels = 10 ms.
                var bytes = new byte[samples.Length*sizeof(float)];
                var clock = Stopwatch.StartNew();
                long blocks = 0;
                while (!cancellation.IsCancellationRequested && !stopRequested)
                {
                    if (mixer?.Failure != null) throw new IOException("WASAPI capture failed.",mixer.Failure);
                    double wait = (blocks+1)*10-clock.Elapsed.TotalMilliseconds;
                    if (wait > 1) await Task.Delay((int)wait,cancellation.Token).ConfigureAwait(false);
                    if (mixer != null) mixer.Read(samples); // Synthetic runs send stereo silence.
                    Buffer.BlockCopy(samples,0,bytes,0,bytes.Length);
                    await audioPipe.WriteAsync(bytes,0,bytes.Length,cancellation.Token).ConfigureAwait(false);
                    blocks++;
                }
            }
            catch (Exception e) when (cancellation.IsCancellationRequested && (e is OperationCanceledException || e is IOException || e is ObjectDisposedException)) { }
            catch (Exception e)
            {
                // Pipe closure is normal once the encoder has exited or was asked to stop.
                if (!stopRequested && !exited.Task.IsCompleted) { audioFailure = e; RequestStop(); audioPipe.Dispose(); }
            }
            finally { audioPipe.Dispose(); } // EOF + -shortest finishes both streams on the audio timeline.
        }
        private async Task MonitorExit()
        {
            await exited.Task.ConfigureAwait(false);
            // Ensure asynchronous output callbacks have drained before inspecting diagnostics.
            await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
            cancellation.Cancel(); audioPipe.Dispose();
            if (audioTask != null) await audioTask.ConfigureAwait(false);
            if (audioFailure != null) throw new IOException("Recording audio failed: "+audioFailure.Message,audioFailure);
            if (process.ExitCode != 0) throw new IOException("Video encoder failed ("+process.ExitCode+"). "+Diagnostics());
            if (!stopRequested) throw new IOException("The Video encoder stopped unexpectedly. "+Diagnostics());
        }
        private string Diagnostics() { lock (errors) { string detail = string.Join(Environment.NewLine,errors); Log.Write("Video encoder: "+detail); return detail; } }
        internal static async Task ValidateFileAsync(string path)
        {
            // Do not announce success for an audio-only or undecodable recording.
            using (var check = new Process { StartInfo = new ProcessStartInfo {
                FileName = EncodingProfile.EncoderPath,
                Arguments = "-hide_banner -v error -xerror -i "+EncodingProfile.Quote(path)+" -map 0:v:0 -an -frames:v 1 -f null -",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            }})
            {
                check.Start();
                Task<string> errors = check.StandardError.ReadToEndAsync();
                Task<string> output = check.StandardOutput.ReadToEndAsync();
                bool ended = await Task.Run(() => check.WaitForExit(10000)).ConfigureAwait(false);
                if (!ended) { try { check.Kill(); } catch (InvalidOperationException) { } throw new IOException("Video verification timed out. The incomplete file has been kept for recovery."); }
                string diagnostic = await errors.ConfigureAwait(false);
                await output.ConfigureAwait(false);
                if (check.ExitCode != 0) throw new IOException("The recording has no decodable video frame. "+diagnostic);
            }
        }
        internal void RequestStop()
        {
            stopRequested = true;
            if (!started) return;
            // Do not send FFmpeg "q": it flushes queued video past the last audio packet.
            // Ending the paced stereo input lets -shortest finalize both streams instead.
            // Avoid an orphaned encoder if a driver or device stalls during finalization.
            _ = AbortIfStalled();
        }
        private async Task AbortIfStalled()
        {
            if (await Task.WhenAny(exited.Task,Task.Delay(30000)).ConfigureAwait(false) != exited.Task) Abort();
        }
        private void Abort()
        {
            cancellation.Cancel();
            audioPipe.Dispose();
            if (started) { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            Abort();
            if (audioTask != null) { try { audioTask.GetAwaiter().GetResult(); } catch { } }
            if (started) { try { Completion.GetAwaiter().GetResult(); } catch { } }
            mixer?.Dispose(); job?.Dispose(); process.Dispose(); cancellation.Dispose();
        }
    }
}
