using System;

partial class PlayerApp {
 MenuButtonLearner menuLearner;
 void BeginMenuButtonLearning(){
  BeginControllerTest(true);
  menuLearner=new MenuButtonLearner(KeyTime);
  status="Release menu OK, then press and release the PS button (or your chosen button).";
 }
 void OnNativeButton(int slot,int code,int value){if(twoPlayerNative&&!controllerTest)return;
  if(mappingPage)return;
  if(directFaces.Active&&code==2)return;
  if(options.Slot>0&&slot+1!=options.Slot)return;
  if(code==options.MenuButton){if(value==1)menuExitHeld=true;else if(value==0)menuExitHeld=false;}
  if(menuLearner!=null&&controllerTest&&nativeTest){
   if(menuLearner.Observe(code,value,KeyTime)){options.MenuButton=menuLearner.Learned;SaveOptions();}
   return;
  }
  if(playing&&!stop&&nativePollingArmed&&options.UsePad&&GameplayExit.MenuButtonPressed(options.MenuButton,code,value))
   RequestGameStop("Learned controller menu button: "+code);
 }
 string MenuButtonLearningText(){
  return "LEARN CONTROLLER MENU BUTTON — TV Back: leave<br/><br/>"+
   (menuLearner.Learned>=0?"Assigned raw button "+menuLearner.Learned+". Press TV Back. In a game, this button stops and returns to TizenPS.":
    menuLearner.Candidate>=0?"Detected button "+menuLearner.Candidate+". Release it to finish.":"Wait a moment, then press and release PS once.<br/>Or press another spare controller button you prefer.")+
   "<br/><br/>If PS opens the TV Home screen, the TV intercepts it.<br/>Return to TizenPS and learn a different button instead.<br/>Fallback: hold Create + Options for one second, or remote Play/Pause.<br/>Learning changes only the exit shortcut, not the game controls.<br/>Reader: "+Esc(eventPad.Detail)+"<br/>"+(nativeProbe.Stopped?"Sampling stopped; leave and reopen this screen to retry.":"Listening for native button press + release.");
 }
}
