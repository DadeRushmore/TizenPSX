using System;
using System.Linq;

// Stored in PS1 button order. Bindings operate on raw native events, before SDK conversion.
internal sealed class PadMapping {
 internal sealed class Binding {
  internal int Type,Code,Direction;
  internal string Name=>Type==1?"Button "+Code:Type==3?"Axis "+Code+(Direction<0?" −":" +"):"Unassigned";
 }
 internal readonly Binding[] Bindings=new Binding[16];
 readonly bool[] held=new bool[16];
 internal bool Enabled;
 internal PadMapping(){for(int i=0;i<16;i++)Bindings[i]=new Binding();}
 internal uint Bits {get{uint b=0;for(int i=0;i<16;i++)if(held[i])b|=1u<<i;return b;}}
 internal void Reset(){Array.Clear(held,0,held.Length);}
 internal static PadMapping Defaults(){
  var m=new PadMapping();int[] codes={1,0,8,9,17,17,16,16,2,3,4,5,3,4,12,13};
  for(int i=0;i<16;i++)m.Bindings[i]=new Binding{Type=(i>=4&&i<=7)||i==12||i==13?3:1,Code=codes[i],Direction=i==4||i==6?-1:1};
  return m;
 }
 internal static bool Active(int type,int code,int direction,int value){
  if(type==1)return value==1||value==2;
  if(code==16||code==17)return direction<0?value==-1:value==1;
  return direction<0?value<=48:value>=208;
 }
 internal void Observe(int type,int code,int value){
  for(int i=0;i<16;i++){var b=Bindings[i];if(b.Type==type&&b.Code==code)held[i]=Active(type,code,b.Direction,value);}
 }
 internal void Assign(int target,int type,int code,int direction){
  if(target<0||target>=16||!Valid(type,code,direction))throw new ArgumentException("Invalid control binding");
  // A physical input has one action. The displaced target is visibly unassigned.
  for(int i=0;i<16;i++){var b=Bindings[i];if(b.Type==type&&b.Code==code&&b.Direction==direction)Bindings[i]=new Binding();}
  Bindings[target]=new Binding{Type=type,Code=code,Direction=direction};Reset();
 }
 static bool Valid(int t,int c,int d)=>t==1&&c>=0&&c<256&&d==1||t==3&&((c>=0&&c<=5)||c==16||c==17)&&(d==1||d==-1);
 internal string Serialize()=>"enabled="+(Enabled?1:0)+"\n"+string.Join("\n",Bindings.Select((b,i)=>i+","+b.Type+","+b.Code+","+b.Direction));
 internal static PadMapping Parse(string text){var m=new PadMapping();foreach(var line in text.Split('\n')){
  if(line.Trim()=="enabled=1"){m.Enabled=true;continue;}var p=line.Trim().Split(',');
  if(p.Length!=4||!int.TryParse(p[0],out int i)||!int.TryParse(p[1],out int t)||!int.TryParse(p[2],out int c)||!int.TryParse(p[3],out int d)||i<0||i>=16||!Valid(t,c,d))continue;
  m.Assign(i,t,c,d);
 }return m;}
 // Convert PS1-order bits to Tizen's SDK layout so all existing input paths agree.
 internal uint SdkBits {get{int[] sdk={4096,16384,32,16,1,2,4,8,8192,32768,256,512,0,0,64,128};uint result=0;for(int i=0;i<16;i++)if((Bits&(1u<<i))!=0)result|=(uint)sdk[i];return result;}}
}
