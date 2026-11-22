using System;

// The protocol's Circle/Triangle bits match PS1 libretro bits 8/9.
// UI writes state; native input reads only the small locked press latch.
internal sealed class DirectFaceState {
 readonly object gate=new object();
 uint pending;bool blocked=true;
 internal bool Active {get;private set;}
 internal uint Held {get;private set;}
 internal uint Delivered=>Active&&!blocked?Held:0;
 internal bool Apply(bool active,uint bits,bool playing){
  bool changed=Active!=active;if(changed)Reset();
  uint before=Delivered;Active=active;Held=active?bits&DualSenseReports.Faces:0;
  if(active&&Held==0)blocked=false;
  if(playing)lock(gate)pending|=Delivered&~before;
  return changed;
 }
 internal void Reset(){blocked=true;lock(gate)pending=0;}
 internal uint TakePresses(){lock(gate){uint result=pending;pending=0;return result;}}
 internal static bool Compatible(PadMapping map,int profile,int layout){
  if(profile!=1||layout!=0)return false;
  if(map==null||!map.Enabled)return true;
  // Preserve intentional face remaps. Allow the old R3-for-Circle workaround.
  for(int i=0;i<16;i++){
   var b=map.Bindings[i];
   if(b.Type==1&&b.Code==2&&i!=8&&i!=9)return false;
   if(i==8&&b.Type!=0&&!(b.Type==1&&(b.Code==2||b.Code==13)))return false;
   if(i==9&&b.Type!=0&&!(b.Type==1&&(b.Code==2||b.Code==3)))return false;
  }
  return true;
 }
 internal static uint CollisionMask(PadMapping map){
  if(map==null||!map.Enabled)return 1u<<8; // USB PS5 profile maps raw 2 to SDK B.
  uint result=0;for(int i=0;i<16;i++)if(map.Bindings[i].Type==1&&map.Bindings[i].Code==2)result|=1u<<i;
  return result;
 }
 internal uint Merge(uint raw,PadMapping map)=>Active?(raw&~CollisionMask(map))|Delivered:raw;
}
