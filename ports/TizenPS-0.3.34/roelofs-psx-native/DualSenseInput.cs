using System;
using System.IO;
using Tizen.TV.Accessory;

partial class PlayerApp {
 readonly DualSenseHidReader directReader=new DualSenseHidReader();
 readonly DirectFaceState directFaces=new DirectFaceState();
 bool directEnabled=true;
 string DirectStatus=>!directEnabled?"Off":directFaces.Active?"Connected — Circle / Triangle fixed":!DirectFaceState.Compatible(customMap,options.NativeProfile,options.FaceLayout)?"Custom profile — original input":"Waiting for one PS5 controller in slot 1";
 string DirectDiagnostics=>nativePads.Diagnostics+" | "+directReader.Diagnostics+" enabled="+directEnabled+" routed="+directFaces.Active+" delivered="+directFaces.Delivered;
 void InitializeDirectFaces(){
  try{directEnabled=AtomicText.Read(Path.Combine(data,"dualsense-faces.txt")).Trim()!="off";}catch{}
  directReader.Start();
 }
 void ToggleDirectFaces(){directEnabled=!directEnabled;AtomicText.Write(Path.Combine(data,"dualsense-faces.txt"),directEnabled?"auto":"off");DisableDirectFaces();status="PS5 button fix: "+(directEnabled?"Auto":"Off");}
 void ResetDirectFaces(){directFaces.Reset();directReader.Resync();}
 void DisableDirectFaces(){directReader.Allowed=false;directFaces.Apply(false,0,false);directFaces.Reset();}
 uint PumpDirectFaces(int chosen){
  bool allowed=directEnabled&&!paused&&ShouldPollController&&!controllerTest&&chosen==1&&
   DirectFaceState.Compatible(customMap,options.NativeProfile,options.FaceLayout);
  if(allowed)try{allowed=!GamePad.GetCapabilities(PlayerIndex.Two).IsConnected;}catch{allowed=false;}
  directReader.Allowed=allowed;
  uint menuPresses=0;
  foreach(var sample in directReader.Drain()){
   uint before=directFaces.Delivered;
   if(directFaces.Apply(sample.Active,sample.Faces,playing)){
    menuNavigation.Reset();Log("DIRECT_FACE_ROUTE "+DirectDiagnostics);
   }
   menuPresses|=directFaces.Delivered&~before;
  }
  return menuPresses;
 }
 protected override void OnTerminate(){nativePads.Stop();directReader.Stop();directFaces.Reset();base.OnTerminate();}
}
