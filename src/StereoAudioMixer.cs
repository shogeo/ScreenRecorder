using System;
using System.IO;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Recording
{
    // WASAPI sources are gated before mixing. The output is always 48 kHz stereo,
    // including silence; toggling audio never changes or restarts the MP4 timeline.
    internal sealed class StereoAudioMixer : IDisposable
    {
        private readonly AudioInput system, mic;
        private readonly float[] scratch = new float[960];
        private readonly object mixerGate = new object();
        private bool disposed;
        private Exception failure;
        internal Exception Failure => Volatile.Read(ref failure);

        internal StereoAudioMixer()
        {
            // Construct on a worker thread, outside the UI synchronization context.
            using (var devices = new MMDeviceEnumerator())
            {
                try { system = AudioInput.Create(devices,DataFlow.Render,ReportFailure); }
                catch (Exception e) { Log.Write("Default system audio unavailable: "+e.Message); }
                try { mic = AudioInput.Create(devices,DataFlow.Capture,ReportFailure); }
                catch (Exception e) { Log.Write("Default microphone unavailable: "+e.Message); }
            }
        }
        private void ReportFailure(Exception exception) => Interlocked.CompareExchange(ref failure,exception,null);
        internal void SetMode(AudioMode mode)
        {
            lock (mixerGate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(StereoAudioMixer));
                if (mode.System && system == null) throw new InvalidOperationException("No default audio output device is available in Windows.");
                if (mode.Mic && mic == null) throw new InvalidOperationException("No default microphone is available in Windows.");
                if (Failure != null) throw new IOException("Audio capture failed: "+Failure.Message,Failure);
                // Start requested devices before committing a new gate state.
                if (mode.System) system.EnsureStarted();
                if (mode.Mic) mic.EnsureStarted();
                system?.SetEnabled(mode.System);
                mic?.SetEnabled(mode.Mic);
            }
        }
        internal void Read(float[] output)
        {
            lock (mixerGate)
            {
                Array.Clear(output,0,output.Length);
                if (disposed) return;
                Add(system,output); Add(mic,output);
                // Saturate the mixed signal rather than wrapping on overload.
                for (int i = 0; i < output.Length; i++) output[i] = Math.Max(-1,Math.Min(1,output[i]));
            }
        }
        private void Add(AudioInput input,float[] output)
        {
            if (input == null) return;
            input.Read(scratch);
            for (int i = 0; i < output.Length; i++) output[i] += scratch[i];
        }
        public void Dispose()
        {
            lock (mixerGate) { if (disposed) return; disposed = true; }
            system?.Dispose(); mic?.Dispose();
        }

        private sealed class AudioInput : IDisposable
        {
            private readonly MMDevice device;
            private readonly WasapiCapture capture;
            private readonly BufferedWaveProvider buffer;
            private readonly Action<Exception> reportFailure;
            private readonly object gate = new object();
            private ISampleProvider samples;
            private bool started, enabled, disposed;
            internal static AudioInput Create(MMDeviceEnumerator devices,DataFlow flow,Action<Exception> report)
            {
                if (!devices.HasDefaultAudioEndpoint(flow,Role.Multimedia)) return null;
                var device = devices.GetDefaultAudioEndpoint(flow,Role.Multimedia);
                try { return new AudioInput(device,flow == DataFlow.Render,report); }
                catch { device.Dispose(); throw; }
            }
            private AudioInput(MMDevice device,bool loopback,Action<Exception> report)
            {
                this.device = device; reportFailure = report;
                capture = loopback ? (WasapiCapture)new LowLatencyLoopback(device) : new WasapiCapture(device,false,20);
                buffer = new BufferedWaveProvider(capture.WaveFormat) { BufferDuration = TimeSpan.FromMilliseconds(250), DiscardOnBufferOverflow = true, ReadFully = true };
                ResetSamples();
                capture.DataAvailable += (s,e) => { lock (gate) { if (enabled && !disposed) buffer.AddSamples(e.Buffer,0,e.BytesRecorded); } };
                capture.RecordingStopped += (s,e) => { if (e.Exception != null && !disposed) reportFailure(e.Exception); };
            }
            private void ResetSamples()
            {
                samples = buffer.ToSampleProvider();
                if (samples.WaveFormat.SampleRate != 48000) samples = new WdlResamplingSampleProvider(samples,48000);
                if (samples.WaveFormat.Channels == 1) samples = new MonoToStereoSampleProvider(samples);
                else if (samples.WaveFormat.Channels != 2)
                {
                    var channels = new MultiplexingSampleProvider(new[] { samples },2);
                    channels.ConnectInputToOutput(0,0); channels.ConnectInputToOutput(1,1);
                    samples = channels;
                }
            }
            internal void EnsureStarted()
            {
                if (started) return;
                capture.StartRecording(); started = true;
            }
            internal void SetEnabled(bool value)
            {
                lock (gate)
                {
                    if (enabled == value) return;
                    enabled = value; buffer.ClearBuffer(); ResetSamples();
                }
            }
            internal void Read(float[] output)
            {
                lock (gate)
                {
                    Array.Clear(output,0,output.Length);
                    if (enabled && !disposed) samples.Read(output,0,output.Length);
                }
            }
            public void Dispose()
            {
                lock (gate) { disposed = true; enabled = false; }
                capture.Dispose(); device.Dispose();
            }
            private sealed class LowLatencyLoopback : WasapiCapture
            {
                internal LowLatencyLoopback(MMDevice device) : base(device,false,20) { }
                protected override AudioClientStreamFlags GetAudioClientStreamFlags() => AudioClientStreamFlags.Loopback|base.GetAudioClientStreamFlags();
            }
        }
    }
}
