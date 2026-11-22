using System;
using System.Collections.Generic;

// Immutable state is published on the UI thread, read without consuming it by the core.
internal sealed class PadSnapshot {
 internal readonly bool Connected;
 internal readonly uint Bits;
 internal readonly float LX,LY,RX,RY;
 internal readonly long Timestamp;
 internal readonly int Slot;
 internal PadSnapshot(){}
 PadSnapshot(bool connected,uint bits,float lx,float ly,float rx,float ry,long timestamp,int slot){Connected=connected;Bits=bits;LX=lx;LY=ly;RX=rx;RY=ry;Timestamp=timestamp;Slot=slot;}
 internal static PadSnapshot Create(bool connected,uint buttons,float lx,float ly,float rx,float ry,long timestamp,int slot=1,bool digitalSticks=true,int stickSource=0,float threshold=.25f){
  if(!connected)return new PadSnapshot(false,0,0,0,0,0,timestamp,0);
  // Either thumbstick supplies digital movement, including games predating DualShock.
  uint directions=0;if(digitalSticks&&(buttons&240)==0){if(stickSource==0||stickSource==1)directions=Direction(lx,ly,threshold);if(directions==0&&(stickSource==0||stickSource==2))directions=Direction(rx,ry,threshold);}
  return new PadSnapshot(true,buttons|directions,lx,ly,rx,ry,timestamp,slot);
 }
 static uint Direction(float x,float y,float threshold){uint b=0;if(x< -threshold)b|=1<<6;if(x>threshold)b|=1<<7;if(y>threshold)b|=1<<4;if(y< -threshold)b|=1<<5;return b;}
 internal static string Names(uint bits){var names=new[]{"Cross","Square","Select","Start","Up","Down","Left","Right","Circle","Triangle","L1","R1","L2","R2","L3","R3"};var held=new List<string>();for(int i=0;i<16;i++)if((bits&(1u<<i))!=0)held.Add(names[i]);return held.Count==0?"none":string.Join(" + ",held);}
}

// One byte per axis matches the precision of the original PS1 DualShock protocol.
// Tizen Y points up; the frontend reverses it when encoding the libretro axes.
internal static class ControllerProtocol {
 internal static ulong NativePacket(PadSnapshot pad,bool fresh,bool analog,bool audio,float deadZone){
  ulong value=(fresh?pad.Bits:0)|(audio?0x20000UL:0UL);
  if(analog&&fresh)value|=((ulong)Axis(pad.LX,deadZone)<<32)|((ulong)Axis(-pad.LY,deadZone)<<40)|((ulong)Axis(pad.RX,deadZone)<<48)|((ulong)Axis(-pad.RY,deadZone)<<56);
  return value;
 }
 internal static ulong Packet(PadSnapshot pad,uint keys,bool fresh,bool analog,bool audio,PlayerOptions options){
  uint bits=(options.UseKeys?keys:0)|(fresh&&options.UsePad?pad.Bits:0);
  ulong value=bits|(audio?0x20000UL:0UL);
  if(analog&&fresh&&options.UsePad)value|=((ulong)Axis(pad.LX,options.AnalogDeadZone)<<32)|((ulong)Axis(-pad.LY,options.AnalogDeadZone)<<40)|((ulong)Axis(pad.RX,options.AnalogDeadZone)<<48)|((ulong)Axis(-pad.RY,options.AnalogDeadZone)<<56);
  return value;
 }
 internal static byte Axis(float position,float deadZone=.10f){
  if(float.IsNaN(position)||float.IsInfinity(position))return 0;
  position=Math.Max(-1f,Math.Min(1f,position));
  // A mild dead zone prevents resting-stick drift without losing full deflection.
  deadZone=Math.Max(0,Math.Min(.9f,deadZone));
  float distance=Math.Abs(position);
  if(distance<=deadZone)return 0;
  float scaled=(distance-deadZone)/(1f-deadZone);
  return unchecked((byte)(sbyte)Math.Round(Math.Sign(position)*scaled*127f));
 }
}
