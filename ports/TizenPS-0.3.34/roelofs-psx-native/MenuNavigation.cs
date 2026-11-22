using System;

// Menu input uses the working native reader after it has been initialized.
// Require neutral input on every return, so held gameplay controls cannot launch again.
internal sealed class MenuNavigation {
 bool armed;
 double neutralSince=-1,nextRepeat;
 uint previous;
 internal void Reset(){armed=false;neutralSince=-1;previous=0;nextRepeat=0;}
 internal string Update(bool connected,uint buttons,float x,float y,bool exitHeld,double now){
  if(!connected){Reset();return null;}
  uint directions=buttons&240;
  if(directions==0){if(x<-.5f)directions=64;else if(x>.5f)directions=128;else if(y>.5f)directions=16;else if(y<-.5f)directions=32;}
  uint current=(buttons&~240u)|directions;
  if(!armed){
   if(current!=0||exitHeld)neutralSince=-1;
   else if(neutralSince<0)neutralSince=now;
   else if(now-neutralSince>=.15)armed=true;
   previous=current;return null;
  }
  if(exitHeld){Reset();return null;}
  uint pressed=current&~previous;previous=current;
  // Confirm/back never auto-repeat. Reset before a new page can receive input.
  if((pressed&256)!=0){Reset();return "XF86Back";}
  if((pressed&9)!=0){Reset();return "Return";}
  if((pressed&2)!=0){Reset();return "p";}
  if((pressed&512)!=0){Reset();return "f";}
  if(directions==0){nextRepeat=0;return null;}
  if((pressed&240)!=0)nextRepeat=now+.4;
  else if(now>=nextRepeat)nextRepeat=now+.12;
  else return null;
  return (directions&16)!=0?"Up":(directions&32)!=0?"Down":(directions&64)!=0?"Left":"Right";
 }
}

// Some firmware emits both native input and a window key. Only suppress a
// matching action from the other path; keep the remote and ordinary key repeats.
internal sealed class MenuKeyFilter {
 string lastKey="";
 bool lastNative;
 double lastTime=-1;
 internal void Reset(){lastKey="";lastTime=-1;}
 internal bool Accept(string key,bool native,double now){
  string canonical=key=="KP_Enter"?"Return":key=="Escape"?"XF86Back":key;
  if(lastKey==canonical&&lastNative!=native&&now-lastTime<.08)return false;
  lastKey=canonical;lastNative=native;lastTime=now;return true;
 }
}
