using System;
using System.IO;
using System.Linq;

partial class PlayerApp {
 KeyInput keyInput=new KeyInput();
 bool nativeTest,learnReady;
 bool nativePollingArmed {get=>options.NativePolling;set=>options.NativePolling=value;}
 int learningBit=-1;
 string captureRelease="";
 double KeyTime=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
 void LoadKeyBindings(){try{keyInput=KeyInput.Parse(AtomicText.Read(Path.Combine(data,"key-bindings.txt")));}catch{keyInput=new KeyInput();}}
 void SaveKeyBindings(){AtomicText.Write(Path.Combine(data,"key-bindings.txt"),keyInput.Serialize());}
 string ControllerReport()=>DirectDiagnostics+"\n\n"+keyInput.Report(KeyTime)+"\n\n"+padStats.Report()+"\nLearned key bindings (button index / base64 key name):\n"+keyInput.Serialize();
 string KeyTestText(){
  string t="KEY-EVENT TEST — TV Back: save and leave<br/>Native gamepad polling: OFF • Screen heartbeat: "+testRefresh/15+"<br/><br/>";
  t+="Last DOWN: "+Esc(keyInput.LastDown)+" • Last UP: "+Esc(keyInput.LastUp)+"<br/>DOWN events: "+keyInput.Downs+" • UP events: "+keyInput.Ups+" • repeated DOWN: "+keyInput.Repeats+"<br/>Held raw names: "+Esc(keyInput.Held)+"<br/>Held mapped buttons: "+PadSnapshot.Names(keyInput.Bits)+"<br/>Current hold: "+keyInput.HoldSeconds(KeyTime).ToString("F1")+" s • longest: "+Math.Max(keyInput.LongestHold,keyInput.HoldSeconds(KeyTime)).ToString("F1")+" s<br/>Seen mapped: "+PadSnapshot.Names(keyInput.Seen)+"<br/>Released mapped: "+PadSnapshot.Names(keyInput.Released)+"<br/><br/>";
  t+=string.Join("<br/>",keyInput.Events.Select(Esc));
  return t+"<br/><br/>Hold Up for 10 seconds, then release. Repeat with each face button.<br/>Quick DOWN/UP pulses stay in this history even if Held returns to none.<br/>Unknown names can be assigned under Controller options → Learn key-event buttons.<br/>D-pad and face buttons do not switch pages or reset this test.";
 }
}
