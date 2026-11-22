partial class PlayerApp {
 readonly MenuNavigation menuNavigation=new MenuNavigation();
 readonly MenuKeyFilter menuKeyFilter=new MenuKeyFilter();
 bool menuExitHeld;
 bool NativeMenuPolling=>!playing&&!controllerTest&&!audioTesting&&learningBit<0&&captureRelease.Length==0&&eventPad.Initialized;
 bool ShouldPollController=>TwoPlayerPolling||InputPolling.Allowed(paused,playing,controllerTest,nativeTest,nativePollingArmed,options.UsePad,NativeMenuPolling);
 void ResetMenuInput(){ResetDirectFaces();nativePads.Slots.BlockHeld();menuNavigation.Reset();menuKeyFilter.Reset();captureRelease="";}
 void NavigateNativeMenu(bool connected,uint buttons,float x,float y){
  if(!connected)menuExitHeld=false;
  string key=menuNavigation.Update(connected,buttons,x,y,menuExitHeld&&options.MenuButton>=0,KeyTime);
  if(key!=null)Key(key,true,true);
 }
}
