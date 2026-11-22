using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Stored as bounded indexes so hand-edited or older settings cannot select unsafe values.
internal sealed class PlayerOptions {
 internal int NativeProfile=1, MenuButton=-1;
 internal bool NativePolling=true;
 internal int Slot=1, InputSource=1, StickSource, DeadZone, Trigger, FaceLayout, Buffer=1, Presentation, Overlay=1;
 internal int FrameSkip,CpuClock,Interpolation;
 internal bool Reverb=true,InputTrace,AdaptiveAudio=true,Dynarec=true,EnhancedResolution=true,BufferedVideo;
 internal bool Sound=true;
 internal static readonly int[] BufferTimes={60,120,200};
 internal static readonly float[] DeadZones={.10f,.20f,.30f};
 internal static readonly float[] TriggerLevels={.25f,.40f,.65f};
 internal PlayerOptions(){Trigger=1;}
 internal int BufferMs=>BufferTimes[Buffer];
 internal float AnalogDeadZone=>DeadZones[DeadZone];
 internal float DigitalDeadZone=>Math.Max(.25f,AnalogDeadZone);
 internal float TriggerLevel=>TriggerLevels[Trigger];
 internal bool UsePad=>InputSource!=2;
 internal bool UseKeys=>InputSource!=1;
 internal string Serialize()=>string.Join("\n",new[]{"inputSchema=3","performanceSchema=1","fullSpecDefaults=1","bufferedVideo="+(BufferedVideo?1:0),"enhancedResolution="+(EnhancedResolution?1:0),"adaptiveAudio="+(AdaptiveAudio?1:0),"dynarec="+(Dynarec?1:0),"frameSkip="+FrameSkip,"cpuClock="+CpuClock,"interpolation="+Interpolation,"reverb="+(Reverb?1:0),"inputTrace="+(InputTrace?1:0),"nativePolling="+(NativePolling?1:0),"menuButton="+MenuButton,"profile="+NativeProfile,"slot="+Slot,"input="+InputSource,"stick="+StickSource,"dead="+DeadZone,"trigger="+Trigger,"face="+FaceLayout,"buffer="+Buffer,"presentation="+Presentation,"overlay="+Overlay,"sound="+(Sound?1:0)});
 internal static PlayerOptions Parse(string text){
  var o=new PlayerOptions();bool currentSchema=false,performanceSchema=false,fullSpecDefaults=false;
  foreach(var line in text.Split('\n')){var p=line.Trim().Split('=');int v;if(p.Length!=2||!int.TryParse(p[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out v)||v<0)continue;
   switch(p[0]){case "fullSpecDefaults":fullSpecDefaults=v==1;break;case "bufferedVideo":if(v<=1)o.BufferedVideo=v==1;break;case "enhancedResolution":if(v<=1)o.EnhancedResolution=v==1;break;case "adaptiveAudio":if(v<=1)o.AdaptiveAudio=v==1;break;case "dynarec":if(v<=1)o.Dynarec=v==1;break;case "performanceSchema":performanceSchema=v==1;break;case "frameSkip":if(v<=2)o.FrameSkip=v;break;case "cpuClock":if(v<=4)o.CpuClock=v;break;case "interpolation":if(v<=2)o.Interpolation=v;break;case "reverb":if(v<=1)o.Reverb=v==1;break;case "inputTrace":if(v<=1)o.InputTrace=v==1;break;case "profile":if(v<=1)o.NativeProfile=v;break;case "nativePolling":if(v<=1)o.NativePolling=v==1;break;case "menuButton":if(v<=255)o.MenuButton=v;break;case "inputSchema":currentSchema=v==3;break;case "slot":if(v<=2)o.Slot=v;break;case "input":if(v<=2)o.InputSource=v;break;case "stick":if(v<=3)o.StickSource=v;break;case "dead":if(v<=2)o.DeadZone=v;break;case "trigger":if(v<=2)o.Trigger=v;break;case "face":if(v<=1)o.FaceLayout=v;break;case "buffer":if(v<=2)o.Buffer=v;break;case "presentation":if(v<=1)o.Presentation=v;break;case "overlay":if(v<=1)o.Overlay=v;break;case "sound":if(v<=1)o.Sound=v==1;break;}
  }if(!fullSpecDefaults){o.Dynarec=true;o.EnhancedResolution=true;}if(!performanceSchema)o.Overlay=1;if(!currentSchema){o.NativeProfile=1;o.InputSource=1;o.NativePolling=true;o.MenuButton=-1;if(o.Slot==0)o.Slot=1;}return o;
 }
 internal static PlayerOptions Load(string path){try{return Parse(AtomicText.Read(path));}catch{return new PlayerOptions();}}
 internal uint MapButtons(uint bits){
  if(FaceLayout==0)return bits;
  const uint faces=(1u<<0)|(1u<<8)|(1u<<1)|(1u<<9);
  return (bits&~faces)|((bits&1)<<8)|((bits&(1u<<8))>>8)|((bits&2)<<8)|((bits&(1u<<9))>>8);
 }
 internal PadSnapshot Map(bool connected,uint raw,float lx,float ly,float rx,float ry,long time,int slot,bool digital){
  // Stick source only affects digital emulation. DualShock retains both analog axes.
  return PadSnapshot.Create(connected,MapButtons(raw),lx,ly,rx,ry,time,slot,digital,StickSource,DigitalDeadZone);
 }
}

// UI-thread-owned observations: never infer physical button presses from mapped sticks.
internal sealed class PadDiagnostics {
 internal uint Raw, Seen, Released, Mapped;
 internal float LT,RT;
 internal readonly float[] Min={0,0,0,0},Max={0,0,0,0};
 internal long Polls,SlowPolls,Errors,Changes;
 internal double WorstGap,LongestHold,GapWarningMs=50;
 internal string Slots="Not scanned",LastKey="none",LastError="none";
 internal readonly Queue<string> Events=new Queue<string>();
 double lastTime=-1,holdStart;
 uint previous;
 internal void Observe(double seconds,bool connected,uint raw,uint mapped,float lx,float ly,float rx,float ry,float lt,float rt){
  if(lastTime>=0){double gap=(seconds-lastTime)*1000;WorstGap=Math.Max(WorstGap,gap);if(gap>GapWarningMs)SlowPolls++;}lastTime=seconds;Polls++;
  Raw=connected?raw:0;Mapped=connected?mapped:0;LT=connected?lt:0;RT=connected?rt:0;
  Seen|=Raw;Released|=previous&~Raw;
  if(Raw!=previous){if(previous!=0)LongestHold=Math.Max(LongestHold,seconds-holdStart);holdStart=seconds;Changes++;Add("Buttons: "+PadSnapshot.Names(Raw));previous=Raw;}
  if(Raw!=0)LongestHold=Math.Max(LongestHold,seconds-holdStart);
  if(connected){var axes=new[]{lx,ly,rx,ry};for(int i=0;i<4;i++){if(float.IsNaN(axes[i])||float.IsInfinity(axes[i]))continue;Min[i]=Math.Min(Min[i],axes[i]);Max[i]=Math.Max(Max[i],axes[i]);}}
 }
 internal void Add(string message){Events.Enqueue(message);while(Events.Count>6)Events.Dequeue();}
 internal void Error(string message){Errors++;LastError=message;Add("Error: "+message);}
 internal string Report()=>"TizenPS controller test\nSlots: "+Slots+"\nPolls: "+Polls+"; gaps >"+GapWarningMs+"ms: "+SlowPolls+"; worst gap: "+WorstGap.ToString("F1",CultureInfo.InvariantCulture)+" ms; errors: "+Errors+"\nRaw held: "+PadSnapshot.Names(Raw)+"\nSeen: "+PadSnapshot.Names(Seen)+"\nReleased: "+PadSnapshot.Names(Released)+"\nLongest unchanged hold: "+LongestHold.ToString("F1",CultureInfo.InvariantCulture)+" s\nLast key: "+LastKey+"\nLast error: "+LastError+"\nAxes min: "+string.Join(", ",Min)+"\nAxes max: "+string.Join(", ",Max)+"\n"+string.Join("\n",Events);
}
