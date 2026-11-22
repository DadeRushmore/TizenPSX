using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections.Generic;

partial class PlayerApp {
 readonly NativePadsReader nativePads=new NativePadsReader();readonly bool twoPlayerNative=true;
 bool TwoPlayerPolling=>!paused;
 readonly Queue<Tuple<string,bool,long>> nativeWindowKeys=new Queue<Tuple<string,bool,long>>();
 string nativePlayersStatus="";
 bool DeferNativeWindowKey(string key,bool down){
  if(playing)return key!="XF86AudioPlay"&&key!="XF86AudioPause"&&key!="XF86AudioPlayPause";
  if(key!="Return"&&key!="KP_Enter"&&key!="XF86Back"&&key!="Escape"&&key!="Up"&&key!="Down"&&key!="Left"&&key!="Right"&&key!="p"&&key!="f")return false;
  if(nativeWindowKeys.Count<64)nativeWindowKeys.Enqueue(Tuple.Create(key,down,Stopwatch.GetTimestamp()));return true;
 }
 void InitializeTwoPlayers(){try{physicalMap.Load(AtomicText.Read(Path.Combine(data,"dualsense-native-map.txt")));}catch{}nativePads.Start();}
 readonly DualSenseButtonMap physicalMap=new DualSenseButtonMap(new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15});
 void SavePhysicalMap(){AtomicText.Write(Path.Combine(data,"dualsense-native-map.txt"),physicalMap.Serialize());}
 void PollTwoPlayers(){
  DisableDirectFaces();nativePads.Enabled=!paused;
  while(nativeWindowKeys.Count>0&&(Stopwatch.GetTimestamp()-nativeWindowKeys.Peek().Item3)/(double)Stopwatch.Frequency>=.08){var e=nativeWindowKeys.Dequeue();if(!nativePads.Slots.RecentActivity(e.Item3))Key(e.Item1,e.Item2,false,true);}
  if(!TwoPlayerPolling)return;
  string next=nativePads.Slots.Status;if(next!=nativePlayersStatus){nativePlayersStatus=next;Log("NATIVE PLAYERS "+next);if(!playing){status=next;Draw();}}
  var p=nativePads.Slots.Read(0,Stopwatch.GetTimestamp(),false);
  if(mappingPage){PollNativeMapping();return;}
  if(!playing){if(!audioTesting&&!controllerTest)NavigateNativeMenu(p.Connected,p.Bits,p.LX,p.LY);return;}
  Volatile.Write(ref pad,PadSnapshot.Create(p.Connected,physicalMap.Apply(p.Bits),p.LX,p.LY,p.RX,p.RY,p.Timestamp,1,!dualShock));
 }
 ulong TwoPlayerControls(int port){
  if(stop)return port==0?0x10000UL:0;
  if(paused||!twoPlayerNative)return 0;
  var p=nativePads.Slots.Read(port,Stopwatch.GetTimestamp(),true);
  var mapped=PadSnapshot.Create(p.Connected,physicalMap.Apply(p.Bits),p.LX,p.LY,p.RX,p.RY,p.Timestamp,port+1,!dualShock);
  // This explicit input mode never mixes unassigned TV keyboard/SDK events.
  ulong packet=ControllerProtocol.NativePacket(mapped,p.Connected,dualShock,port==0&&Volatile.Read(ref audioClock)!=0,.10f);
  if(port==0){
   Interlocked.Increment(ref controlReads);Interlocked.Exchange(ref lastControlPacket,unchecked((long)packet));
   if((p.Bits&12)==12){if(comboStart<0)comboStart=clock.Elapsed.TotalSeconds;if(clock.Elapsed.TotalSeconds-comboStart>=1){RequestGameStop("Player 1 Create + Options held");return 0x10000UL;}}else comboStart=-1;
  }
  return packet;
 }
}
