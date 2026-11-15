using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Recording
{
    internal enum CaptureState { Idle, Starting, Recording, Finishing }
    internal struct AudioMode
    {
        public bool System;
        public bool Mic;
        public AudioMode(bool system, bool mic) { System = system; Mic = mic; }
        public AudioMode Toggle(Command command)
        {
            switch (command)
            {
                case Command.SystemAudio: return new AudioMode(!System, Mic);
                case Command.Microphone: return new AudioMode(System, !Mic);
                case Command.Both: bool enable = !(System && Mic); return new AudioMode(enable, enable);
                default: return this;
            }
        }
        public static AudioMode FromCommand(Command command) => new AudioMode(command == Command.SystemAudio || command == Command.Both, command == Command.Microphone || command == Command.Both);
        public override string ToString() => System && Mic ? "System + microphone" : System ? "System audio" : Mic ? "Microphone" : "No audio";
    }
    internal sealed class CaptureSession : IDisposable
    {
        private VideoEncoder encoder;
        private int generation;
        private string pendingPath;
        private readonly Stopwatch elapsed = new Stopwatch();
        private bool stopWhenReady;
        private TaskCompletionSource<string> completion;
        public CaptureState State { get; private set; }
        public AudioMode Audio { get; private set; }
        public TimeSpan Elapsed => elapsed.Elapsed;
        public string LastFile { get; private set; }
        public string Error { get; private set; }
        public Task<string> Completion => completion?.Task ?? Task.FromResult<string>(null);
        public static string OutputFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Recordings");
        public event Action Changed;
        public event Action<string> Saved;
        public event Action<string> Failed;

        public CaptureSession(Control ui) { }
        public void Handle(Command command, Settings settings)
        {
            if (State == CaptureState.Finishing) return;
            if (State == CaptureState.Idle) { Start(AudioMode.FromCommand(command), settings); return; }
            if (command == Command.Record) { Stop(); return; }
            SetAudio(Audio.Toggle(command));
        }
        public void Start(AudioMode audio, Settings settings, string outputPath = null)
        {
            if (State != CaptureState.Idle) return;
            Error = null;
            completion = new TaskCompletionSource<string>();
            stopWhenReady = false;
            elapsed.Reset();
            int session = ++generation;
            State = CaptureState.Starting;
            Audio = audio;
            Changed?.Invoke();
            try
            {
                Directory.CreateDirectory(OutputFolder);
                pendingPath = outputPath ?? Path.Combine(OutputFolder, "Recording_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 4) + ".recording.mp4");
                Directory.CreateDirectory(Path.GetDirectoryName(pendingPath));
                var screen = Array.Find(Screen.AllScreens, s => s.DeviceName == settings.Monitor) ?? Screen.PrimaryScreen;
                encoder = new VideoEncoder(DesktopInput.ForScreen(screen),pendingPath);
                Log.Write("Starting H264 NVENC P6 VBR 2.5Mbps max3Mbps; 100% "+screen.Bounds.Width+"x"+screen.Bounds.Height+"; 60fps; AAC stereo 48kHz 128kbps; "+pendingPath+"; audio="+audio);
                _ = RunEncoder(session,encoder,audio);
            }
            catch (Exception e) { Fail(e.Message); }
        }
        private async Task RunEncoder(int session,VideoEncoder active,AudioMode initialMode)
        {
            try
            {
                await active.StartAsync(initialMode);
                if (session != generation || encoder != active) return;
                active.SetMode(Audio);
                elapsed.Start();
                State = CaptureState.Recording;
                Changed?.Invoke();
                if (stopWhenReady) Stop();
                await active.Completion;
                await VideoEncoder.ValidateFileAsync(pendingPath);
                if (session == generation && encoder == active) Finish(pendingPath);
            }
            catch (Exception e) { if (session == generation && encoder == active) Fail(e.Message); }
        }
        public void SetAudio(AudioMode mode)
        {
            if (State != CaptureState.Recording && State != CaptureState.Starting) return;
            var previous = Audio;
            try
            {
                encoder?.SetMode(mode);
                Audio = mode;
                Log.Write("Audio: " + mode);
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Audio = previous;
                Error = e.Message;
                Failed?.Invoke(e.Message);
                Changed?.Invoke();
            }
        }
        public void Stop()
        {
            if (State == CaptureState.Starting) { stopWhenReady = true; return; }
            if (State != CaptureState.Recording) return;
            State = CaptureState.Finishing;
            elapsed.Stop();
            Log.Write("Stop requested");
            Changed?.Invoke();
            try { encoder.RequestStop(); }
            catch (Exception e) { Fail(e.Message); }
        }
        private void Finish(string path)
        {
            elapsed.Stop();
            ReleaseEncoder();
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new IOException("Recording finished without a video file.");
                string finalPath = path.Replace(".recording.mp4", ".mp4");
                if (path != finalPath) File.Move(path, finalPath);
                LastFile = finalPath;
                State = CaptureState.Idle;
                Audio = default;
                Log.Write("Saved " + finalPath);
                Changed?.Invoke();
                Saved?.Invoke(finalPath);
                completion.TrySetResult(finalPath);
            }
            catch (Exception e) { Fail("Could not finish saving: " + e.Message); }
        }
        private void Fail(string message)
        {
            elapsed.Stop();
            ReleaseEncoder();
            State = CaptureState.Idle;
            Audio = default;
            Error = message;
            Log.Write("Capture failed: " + message);
            // Keep incomplete files for recovery; never advertise them as a completed recording.
            Changed?.Invoke();
            Failed?.Invoke(message);
            completion?.TrySetException(new InvalidOperationException(message));
        }
        private void ReleaseEncoder()
        {
            var old = encoder;
            encoder = null;
            old?.Dispose();
        }
        public void Dispose() { generation++; ReleaseEncoder(); }
    }
}
