using System;

internal enum ControllerProfile { LegacyDigital=0, UsbPs5=1 }

// Own persistent state, in the SDK's Buttons bit layout. An unrelated event
// must never release a held button. Analog values remain raw until calibrated.
internal sealed class ControllerEvents {
 internal uint Buttons;
 internal readonly int[] Axes=new int[6];
 internal int Events;
 internal readonly bool[] AxisSeen=new bool[6];
 internal float Stick(int axis,bool invert=false){
  if(!AxisSeen[axis])return 0;
  int raw=Axes[axis];if(raw<0||raw>255)return 0;
  // Byte endpoint conversion used by established DualSense implementations.
  int signed=raw*257-32768;float result=signed<0?signed/32768f:signed/32767f;
  return invert?-result:result;
 }
 internal float Trigger(int axis)=>AxisSeen[axis]&&Axes[axis]>=0&&Axes[axis]<=255?Axes[axis]/255f:0;

 internal void Clear(){Buttons=0;Array.Clear(Axes,0,Axes.Length);Array.Clear(AxisSeen,0,AxisSeen.Length);Events=0;}
 internal bool Apply(int type,int code,int value,bool ps5=false){
  if(type==1){
   uint mask;
   if(ps5){if(code==1)code=0;else if(code==0)code=2;else if(code==2)code=1;else if(code==4||code==5)return false;else if(code==8)code=12;else if(code==9)code=13;else if(code==12)code=10;else if(code==13)code=11;else if(code==10||code==11)return false;}
   switch(code){case 0:mask=4096;break;case 1:mask=8192;break;case 2:mask=16384;break;case 3:mask=32768;break;case 4:mask=256;break;case 5:mask=512;break;case 10:mask=64;break;case 11:mask=128;break;case 12:mask=32;break;case 13:mask=16;break;case 14:mask=2048;break;default:return false;}
   if(value<0||value>2)return false;
   Buttons=value==0?Buttons&~mask:Buttons|mask;
  }else if(type==3){
   if(code==16||code==17){
    if(value < -1||value>1)return false;
    uint negative=code==16?4u:1u,positive=code==16?8u:2u;
    Buttons=(Buttons&~(negative|positive))|(value<0?negative:value>0?positive:0);
   }else if(code>=0&&code<6){Axes[code]=value;AxisSeen[code]=true;}
   else return false;
  }else return false;
  Events++;return true;
 }
}
