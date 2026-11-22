using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

sealed class Sound : IDisposable {
 static int adaptiveOwner;
 readonly IAudioOutput outputBackend;long samples,writes,writeTicks,maxWriteTicks,slowWrites;
 readonly bool adaptive;
 readonly object gate=new object();
 readonly AudioResampler resampler;
 readonly short[] inputBuffer;
 readonly Stopwatch audioClock=Stopwatch.StartNew();
 System.Threading.Thread worker;
 volatile bool stopping,disposed;
 string failure="";
 internal bool UsesAudioClock=>!adaptive;
 internal string Failure=>System.Threading.Volatile.Read(ref failure);
 internal string LiveSummary {get{if(!adaptive)return "Original audio output";lock(gate)return "Audio "+(resampler.Speed*100).ToString("F0")+"% | queue "+resampler.BufferedMs.ToString("F0")+" ms | underruns "+resampler.Underruns+" | dropped "+resampler.DroppedFrames+(Failure.Length>0?" | "+Failure:"");}}
 internal string Summary=>"Audio: "+System.Threading.Interlocked.Read(ref samples)+" stereo frames / "+System.Threading.Interlocked.Read(ref writes)+" writes; output wait "+(System.Threading.Interlocked.Read(ref writeTicks)/(double)Stopwatch.Frequency).ToString("F1")+" s; worst write "+(System.Threading.Interlocked.Read(ref maxWriteTicks)*1000.0/Stopwatch.Frequency).ToString("F1")+" ms; writes over 50 ms: "+System.Threading.Interlocked.Read(ref slowWrites)+" | "+LiveSummary;
 internal Sound(int bufferMs=120,bool adaptive=false):this(()=>new PulseAudioOutput(bufferMs),adaptive){}
 internal Sound(Func<IAudioOutput> createOutput,bool adaptive){
  this.adaptive=adaptive;
  if(adaptive&&System.Threading.Interlocked.CompareExchange(ref adaptiveOwner,1,0)!=0)throw new Exception("Previous adaptive audio output is still stopping; restart the app before another audio session.");
  try{
   outputBackend=createOutput();if(outputBackend==null)throw new ArgumentException("Audio backend is required.");
   if(adaptive){resampler=new AudioResampler();inputBuffer=new short[4096];worker=new System.Threading.Thread(Feed){IsBackground=true,Name="TizenPS audio"};worker.Start();}
  }catch{try{outputBackend?.Dispose();}finally{if(adaptive)System.Threading.Interlocked.Exchange(ref adaptiveOwner,0);}throw;}
 }
 void WriteAudio(IntPtr data,uint frames){
  long start=Stopwatch.GetTimestamp();outputBackend.Write(data,frames);long elapsed=Stopwatch.GetTimestamp()-start;
  System.Threading.Interlocked.Add(ref writeTicks,elapsed);if(elapsed>System.Threading.Interlocked.Read(ref maxWriteTicks))System.Threading.Interlocked.Exchange(ref maxWriteTicks,elapsed);if(elapsed>Stopwatch.Frequency/20)System.Threading.Interlocked.Increment(ref slowWrites);
  System.Threading.Interlocked.Increment(ref writes);
 }
 void Feed(){
  var output=new short[882];var pin=GCHandle.Alloc(output,GCHandleType.Pinned);
  try{while(!stopping){lock(gate)resampler.Render(output,441,audioClock.Elapsed.TotalSeconds);WriteAudio(pin.AddrOfPinnedObject(),441);}}
  catch(Exception e){System.Threading.Volatile.Write(ref failure,"Adaptive output: "+e.Message);}
  finally{pin.Free();try{outputBackend.Dispose();}catch(Exception e){System.Threading.Volatile.Write(ref failure,"Audio close: "+e.Message);}finally{System.Threading.Interlocked.Exchange(ref adaptiveOwner,0);}}
 }
 internal void Push(IntPtr data,uint frames){
  if(disposed)return;
  if(Failure.Length>0)throw new Exception(Failure);
  if(frames>1048576)throw new ArgumentOutOfRangeException(nameof(frames));
  if(!adaptive){WriteAudio(data,frames);}
  else{
   int left=(int)frames,offset=0;
   while(left>0){int n=Math.Min(left,inputBuffer.Length/2);Marshal.Copy(IntPtr.Add(data,offset*4),inputBuffer,0,n*2);lock(gate)resampler.Push(inputBuffer,0,n,audioClock.Elapsed.TotalSeconds);left-=n;offset+=n;}
  }
  System.Threading.Interlocked.Add(ref samples,frames);
 }
 internal void Drain(){
  if(adaptive){var wait=Stopwatch.StartNew();while(!disposed&&Failure.Length==0&&wait.Elapsed.TotalSeconds<3){lock(gate){if(resampler.BufferedFrames<2)return;}System.Threading.Thread.Sleep(10);}if(Failure.Length>0)throw new Exception(Failure);return;}
  outputBackend.Drain();
 }
 public void Dispose(){
  if(disposed)return;disposed=true;
  if(adaptive){stopping=true;if(worker!=null&&!worker.Join(1000))System.Threading.Volatile.Write(ref failure,"Audio output has not stopped yet; stream stays owned by its worker.");}
  else outputBackend.Dispose();
 }
}

