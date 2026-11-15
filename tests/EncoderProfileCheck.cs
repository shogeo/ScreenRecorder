using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
class EncoderProfileCheck {
 static int Main(string[] args) {
  object encoder=null;
  try {
   var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));
   var type=asm.GetType("Recording.VideoEncoder",true);
   var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
   var mode=Activator.CreateInstance(asm.GetType("Recording.AudioMode"));
   encoder=Activator.CreateInstance(type,flags,null,new object[]{"-re -f lavfi -i testsrc2=size=1920x1200:rate=60",Path.GetFullPath(args[1]),true},null);
   var start=(Task)type.GetMethod("StartAsync",flags).Invoke(encoder,new[]{mode});
   if(!start.Wait(20000)) throw new Exception("Encoder initialization timed out");
   Thread.Sleep(500);
   type.GetMethod("RequestStop",flags).Invoke(encoder,null);
   var done=(Task)type.GetProperty("Completion",flags).GetValue(encoder,null);
   if(!done.Wait(15000)) throw new Exception("Encoder finalization timed out");
   Console.WriteLine("Direct H.264 pipeline finalized synthetic input and stereo PCM pipe.");
   return 0;
  } catch(Exception e) { Console.WriteLine(e); return 1; }
  finally { if(encoder!=null) ((IDisposable)encoder).Dispose(); }
 }
}
