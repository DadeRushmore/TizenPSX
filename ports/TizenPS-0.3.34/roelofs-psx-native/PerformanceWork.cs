using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

partial class PlayerApp {
 Sound activeSound;
 string engineStatus="CPU: initializing";
 void ReceiveNativeReport(string text){if(text.StartsWith("CPU ACTIVE: "))System.Threading.Volatile.Write(ref engineStatus,text);Log(text);}
 long pollWork,pollWorkCount,copyWork,copyLockWork,presentWork;
 void ResetWorkMetrics(){pollWork=pollWorkCount=copyWork=copyLockWork=presentWork=0;}
 string WorkSummary(){
  double scale=1000.0/Stopwatch.Frequency;
  return "WORK cumulative: native poll "+(Interlocked.Read(ref pollWork)*scale).ToString("F1")+" ms / "+Interlocked.Read(ref pollWorkCount)+" reads; managed video "+(Interlocked.Read(ref copyWork)*scale).ToString("F1")+" ms (lock wait "+(Interlocked.Read(ref copyLockWork)*scale).ToString("F1")+"); display submission "+(Interlocked.Read(ref presentWork)*scale).ToString("F1")+" ms";
 }
 string AudioLiveText(){var audio=Volatile.Read(ref activeSound);return audio==null?"Sound output off":audio.LiveSummary;}
 string ExperimentalMarker=>Path.Combine(data,"experimental-core.pending");
 void RecoverExperimentalSession(){
  try{if(GameHistory.RecoverPending(data))status="Previous session did not finish. All saved game settings were kept.";}
  catch(Exception e){status="Could not read the previous session notice: "+e.Message;}
 }
 void MarkExperimentalSession(){try{GameHistory.StartSession(data,activeSessionPath);}catch(Exception e){Log("Session tracking unavailable: "+e.Message);}}
 void ClearExperimentalSession(){AtomicText.Remove(ExperimentalMarker);}
 void RecordSessionEnd(bool completed,bool safer){
  try{var sample=System.Threading.Volatile.Read(ref benchmark);double fps=sample?.AverageFps??0,speed=sample!=null&&sample.Target>0?100*fps/sample.Target:0;bool failure=gameExitReason.IndexOf("callback",StringComparison.OrdinalIgnoreCase)>=0||gameExitReason.IndexOf("failed",StringComparison.OrdinalIgnoreCase)>=0||gameExitReason.IndexOf("failure",StringComparison.OrdinalIgnoreCase)>=0;
   GameHistory.Finish(data,activeSessionPath,completed&&!failure,clock.Elapsed.TotalSeconds,fps,speed,engineStatus,(safer?"Safer launch once. ":"")+(gameExitReason.Length>0?gameExitReason:"Emulator returned"));ClearExperimentalSession();
  }catch(Exception e){Log("Session history: "+e.Message);}
 }
}
