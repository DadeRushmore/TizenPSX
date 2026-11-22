using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Tizen.TV.Accessory;

partial class PlayerApp {
 NativeProbePlan nativeProbe;
 NativeProbeGuide nativeGuide;
 int nativePacket;
 double nativeReadMs;
 string nativeProbePhase="Not started";
 readonly System.Text.StringBuilder nativeTraceBuffer=new System.Text.StringBuilder();
 double nativeTraceFlush;
 void FlushNativeTrace(){if(nativeTraceBuffer.Length==0)return;File.AppendAllText(NativeTracePath,nativeTraceBuffer.ToString());nativeTraceBuffer.Clear();nativeTraceFlush=KeyTime;}
 string NativeTracePath=>Path.Combine(data,"native-controller-trace.txt");
 void BeginNativeProbe(){
  nativeTraceBuffer.Clear();nativeTraceFlush=KeyTime;eventPad.Profile=(ControllerProfile)options.NativeProfile;eventPad.Reset();nativeGuide=new NativeProbeGuide(KeyTime);nativeProbe=new NativeProbePlan(options.Slot,KeyTime,NativeProbeGuide.Duration,60);nativePacket=0;nativeReadMs=0;
  try{File.WriteAllText(NativeTracePath,"TizenPSX 0.3.40 native controller probe\nUTC: "+DateTime.UtcNow.ToString("O")+"\nAPI assembly: "+typeof(GamePad).Assembly.FullName+"\nOne slot only: "+nativeProbe.Slot+"\nProfile movement 90-second sequence; up to 60 batches/second. Every populated raw event is logged. No native-call timeout.\n");nativeProbePhase="Ready";}
  catch(Exception e){nativeProbe.Stop();nativeProbePhase="Trace unavailable; sampling stopped: "+e.Message;}
 }
 void NativeCheckpoint(string text){
  nativeProbePhase=text;
  // Only the bounded diagnostic test writes this trace, never ordinary gameplay.
  // Buffer at most 250 ms between flushes to avoid diagnostic file I/O per event.
  // Force-quit may lose the last buffered fraction of a second.
  try{nativeTraceBuffer.AppendLine(DateTime.UtcNow.ToString("O")+" | "+text);if(KeyTime-nativeTraceFlush>=.25||text.StartsWith("ERROR")||nativeProbe.Reads==1)FlushNativeTrace();}
  catch(Exception e){nativeProbe.Stop();throw new IOException("Cannot save native controller checkpoint",e);}
 }
 GamePadState ProbeState(int index){
  NativeCheckpoint("ENTER EventPad slot "+(index+1)+" read "+nativeProbe.Reads+" | "+nativeGuide.Label(KeyTime));
  var timer=Stopwatch.StartNew();
  try{
   var state=eventPad.Read(index,true);
   timer.Stop();nativeReadMs=timer.Elapsed.TotalMilliseconds;nativePacket=state.PacketNumber;
   if(eventPad.BatchEvents.Length>0)NativeCheckpoint("RAW EVENTS slot "+(index+1)+" | "+eventPad.BatchEvents);
   NativeCheckpoint("RETURN EventPad slot "+(index+1)+" in "+nativeReadMs.ToString("F2")+" ms | connected="+state.IsConnected+" | "+eventPad.Detail+" | raw buttons="+eventPad.ButtonCounts+" | packet="+nativePacket+" | buttons="+PadSnapshot.Names(ButtonBits(state))+" | triggers="+state.Triggers.Left+","+state.Triggers.Right+" | left="+state.ThumbSticks.Left.X+","+state.ThumbSticks.Left.Y+" | right="+state.ThumbSticks.Right.X+","+state.ThumbSticks.Right.Y);
   return state;
  }catch(Exception e){nativeProbe.Stop();if(eventPad.BatchEvents.Length>0)NativeCheckpoint("RAW EVENTS before error | "+eventPad.BatchEvents);NativeCheckpoint("ERROR EventPad: "+e.GetType().Name+": "+e.Message);throw;}
 }
 string NativeProbeText(){
  if(menuLearner!=null)return MenuButtonLearningText();
  var p=Volatile.Read(ref pad);
  return "GUIDED USB PS5 INPUT TEST — TV Back: save and leave<br/><br/><font_size=30>"+Esc(nativeProbe.Stopped?"SAMPLING STOPPED — TV Back, then export":nativeGuide.Label(KeyTime))+"</font_size><br/><br/>Follow ONLY the named control. Release everything when prompted.<br/>Sticks: move fully in the named directions without clicking.<br/>Buttons/triggers: press, hold briefly, then release.<br/><br/>Slot "+nativeProbe.Slot+" • heartbeat "+testRefresh/15+" • reads "+nativeProbe.Reads+" • 90 seconds total<br/>Connected: "+p.Connected+" • last read "+nativeReadMs.ToString("F2")+" ms<br/>Interpreted buttons (mapping unverified): "+PadSnapshot.Names(padStats.Raw)+"<br/>Profile: "+eventPad.Profile+" • Left "+p.LX.ToString("F2")+", "+p.LY.ToString("F2")+" • Right "+p.RX.ToString("F2")+", "+p.RY.ToString("F2")+"<br/>L2 "+padStats.LT.ToString("F2")+" • R2 "+padStats.RT.ToString("F2")+"<br/>Last RAW button: "+Esc(eventPad.LastButtonEvent)+"<br/>Raw reader: "+Esc(eventPad.Detail)+"<br/>Button totals: "+Esc(eventPad.ButtonCounts)+"<br/>Errors: "+padStats.Errors+" • last: "+Esc(padStats.LastError)+"<br/><br/>USB PS5: sticks/L2/R2 enabled; L1/R1 pending. Legacy profile is digital only.<br/>At completion: release all, TV Back, then Export diagnostics to USB.<br/>If frozen: restart and export BEFORE another test. Native calls cannot be cancelled.";
 }
}
