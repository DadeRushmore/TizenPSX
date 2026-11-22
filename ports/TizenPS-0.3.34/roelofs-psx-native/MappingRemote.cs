using System;
using ElmSharp;
partial class PlayerApp {
 int mappingPageGeneration;
 bool MappingInputNeutral(){var p=System.Threading.Volatile.Read(ref pad);return nativeMappingBits==0&&Math.Abs(p.LX)<.25&&Math.Abs(p.LY)<.25&&Math.Abs(p.RX)<.25&&Math.Abs(p.RY)<.25;}
 void QueueRemoteMappingKey(string key){
  if(!MappingRemotePolicy.IsNavigation(key))return;
  double pressed=KeyTime;int generation=mappingPageGeneration;
  EcoreMainloop.AddTimer(.18,()=>{
   if(mappingPage&&generation==mappingPageGeneration&&!nativePads.Slots.RecentActivity((long)(pressed*System.Diagnostics.Stopwatch.Frequency))&&MappingRemotePolicy.Accept(pressed,lastMappingInput,MappingInputNeutral()))MappingKey(key,true,false);
   return false;
  });
 }
 void PrepareMappingRemote(){mappingPageGeneration++;lastMappingInput=KeyTime;mappingMessage="Use TV remote arrows to choose an action, then OK to assign. Release the controller before navigating. No Play/Pause setup needed.";}
}
