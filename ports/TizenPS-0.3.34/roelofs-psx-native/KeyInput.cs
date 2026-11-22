using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// Window key events are independent of the firmware's GamePad.GetState API.
// Track physical key names, so releasing one alias cannot release another held alias.
internal sealed class KeyInput {
 internal static readonly string[] ButtonNames={"Cross","Square","Select","Start","Up","Down","Left","Right","Circle","Triangle","L1","R1","L2","R2","L3","R3"};
 readonly Dictionary<string,int> bindings=new Dictionary<string,int>(StringComparer.Ordinal);
 readonly Dictionary<string,double> held=new Dictionary<string,double>(StringComparer.Ordinal);
 readonly Queue<string> events=new Queue<string>();
 internal long Downs,Ups,Repeats;
 internal uint Seen,Released;
 internal string LastDown="none",LastUp="none";
 internal double LongestHold;
 internal IEnumerable<string> Events=>events;
 internal static bool Reserved(string key)=>key=="XF86Back"||key=="Escape";
 internal static int DefaultBit(string key){switch(key){
  case "Up":return 4;case "Down":return 5;case "Left":return 6;case "Right":return 7;
  case "z":case "Z":return 0;case "x":case "X":return 8;case "a":case "A":return 1;case "s":case "S":return 9;
  case "q":case "Q":return 10;case "w":case "W":return 11;case "Return":case "KP_Enter":return 3;case "Shift_L":case "Shift_R":return 2;default:return -1;
 }}
 internal int Bit(string key)=>bindings.TryGetValue(key,out int bit)?bit:DefaultBit(key);
 internal uint Bits {get{uint bits=0;foreach(var key in held.Keys){int bit=Bit(key);if(bit>=0)bits|=1u<<bit;}return bits;}}
 internal string Held=>held.Count==0?"none":string.Join(" + ",held.Keys.OrderBy(k=>k));
 internal double HoldSeconds(double now)=>held.Count==0?0:Math.Max(0,now-held.Values.Min());
 internal void Observe(string key,bool down,double now){
  if(string.IsNullOrEmpty(key))return;
  uint before=Bits;
  if(down){Downs++;LastDown=key;if(held.ContainsKey(key))Repeats++;else held[key]=now;}
  else{Ups++;LastUp=key;if(held.TryGetValue(key,out double start))LongestHold=Math.Max(LongestHold,now-start);held.Remove(key);}
  Seen|=Bits;Released|=before&~Bits;
  events.Enqueue(now.ToString("F2",CultureInfo.InvariantCulture)+" "+(down?"DOWN ":"UP   ")+key);while(events.Count>8)events.Dequeue();
 }
 internal void ClearHeld(){held.Clear();}
 internal void ResetDiagnostics(){ClearHeld();events.Clear();Downs=Ups=Repeats=0;Seen=Released=0;LastDown=LastUp="none";LongestHold=0;}
 internal string Bind(string key,int bit){
  if(string.IsNullOrWhiteSpace(key)||key.Length>128||Reserved(key)||bit<0||bit>=16)throw new ArgumentException("This key cannot be mapped.");
  string previous=bindings.TryGetValue(key,out int old)?ButtonNames[old]:DefaultBit(key)>=0?ButtonNames[DefaultBit(key)]:"unmapped";
  // One learned key per PS1 button; a key itself always has exactly one meaning.
  foreach(var entry in bindings.Where(p=>p.Value==bit).Select(p=>p.Key).ToArray())bindings.Remove(entry);
  bindings[key]=bit;ClearHeld();return previous;
 }
 internal string BindingFor(int bit){var match=bindings.FirstOrDefault(p=>p.Value==bit);return match.Key??"default / not learned";}
 internal void ResetBindings(){bindings.Clear();ClearHeld();}
 internal string Serialize()=>string.Join("\n",bindings.OrderBy(p=>p.Value).Select(p=>p.Value+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Key))));
 internal static KeyInput Parse(string text){var result=new KeyInput();foreach(var line in text.Split('\n')){var p=line.Trim().Split('\t');if(p.Length!=2||!int.TryParse(p[0],out int bit))continue;try{result.Bind(Encoding.UTF8.GetString(Convert.FromBase64String(p[1])),bit);}catch(ArgumentException){}catch(FormatException){}}return result;}
 internal string Report(double now)=>"Window key-event observations\nDOWN: "+Downs+"; UP: "+Ups+"; repeat DOWN: "+Repeats+"\nLast DOWN: "+LastDown+"\nLast UP: "+LastUp+"\nHeld raw keys: "+Held+"\nHeld mapped buttons: "+PadSnapshot.Names(Bits)+"\nSeen: "+PadSnapshot.Names(Seen)+"\nReleased: "+PadSnapshot.Names(Released)+"\nLongest hold: "+Math.Max(LongestHold,HoldSeconds(now)).ToString("F1",CultureInfo.InvariantCulture)+" s\n"+string.Join("\n",events);
}

internal static class InputPolling {
 // A key-only test must never call into the native driver, even if gameplay polling is armed.
 internal static bool Allowed(bool paused,bool playing,bool testing,bool nativeTest,bool armed,bool usePad,bool menu=false)=>!paused&&(((playing||(!testing&&menu))&&armed&&usePad)||(testing&&nativeTest));
}
