using System;
using System.IO;
using System.Threading;
using Tizen.TV.Accessory;

partial class PlayerApp {
 GameplayTrace gameplayTrace=new GameplayTrace();
 string GameplayTracePath=>Path.Combine(data,"gameplay-input-trace.txt");
 long tracePacket=-1;
 double tracePacketTime;
 void BeginGameplayTrace(){gameplayTrace=new GameplayTrace();tracePacket=-1;tracePacketTime=-1;try{File.WriteAllText(GameplayTracePath,"Gameplay started; detailed input trace is optional in Audio / video options.\n");}catch(Exception e){Log("Gameplay trace init: "+e.Message);}}
 void SaveGameplayTrace(){try{gameplayTrace.Add("END "+gameExitReason+" | raw totals="+eventPad.ButtonCounts);File.WriteAllText(GameplayTracePath,gameplayTrace.Snapshot());}catch(Exception e){Log("Gameplay trace save: "+e.Message);}}
 GamePadState ReadGameplayPad(int index){
  try{
   var state=eventPad.Read(index,options.InputTrace);
   if(!options.InputTrace)return state;
   gameplayTrace.Add(clock.Elapsed.TotalSeconds.ToString("F3")+" NATIVE slot="+(index+1)+" "+eventPad.Detail+" | raw="+eventPad.BatchEvents+" | mapped="+PadSnapshot.Names(ButtonBits(state))+" | left="+state.ThumbSticks.Left+" right="+state.ThumbSticks.Right+" triggers="+state.Triggers.Left+","+state.Triggers.Right);
   return state;
  }catch(Exception e){gameplayTrace.Add("NATIVE ERROR "+e.Message+" raw="+eventPad.BatchEvents);throw;}
 }
 void TraceCorePacket(ulong packet,bool fresh){
  if(!options.InputTrace)return;
  double now=clock.Elapsed.TotalSeconds;long previous=tracePacket;
  if((uint)previous!=(uint)packet||now-tracePacketTime>=.1){
   gameplayTrace.Add(now.ToString("F3")+" CORE INPUT packet=0x"+packet.ToString("X16")+" buttons="+PadSnapshot.Names((uint)packet&65535)+" fresh="+fresh);
   tracePacket=unchecked((long)packet);tracePacketTime=now;
  }
 }
}
