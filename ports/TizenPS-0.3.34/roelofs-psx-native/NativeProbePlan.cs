using System;

// Limits scheduling only. It cannot interrupt a native function that does not return.
internal sealed class NativeProbePlan {
 internal readonly int Slot;
 internal int Reads {get;private set;}
 internal bool Stopped {get;private set;}
 readonly double started;
 readonly int duration,hz;
 double next;
 internal NativeProbePlan(int selectedSlot,double now,int seconds=30,int frequency=10){if(frequency<1||frequency>60)throw new ArgumentOutOfRangeException(nameof(frequency));hz=frequency;if(seconds<1||seconds>120)throw new ArgumentOutOfRangeException(nameof(seconds));duration=seconds;Slot=selectedSlot>=1&&selectedSlot<=2?selectedSlot:1;started=next=now;}
 internal bool Take(double now){
  if(Stopped)return false;
  if(now-started>=duration||Reads>=duration*hz){Stopped=true;return false;}
  if(now<next)return false;
  Reads++;next=now+1.0/hz;return true;
 }
 internal void Stop(){Stopped=true;}
}

// Longer phases compare event delivery with and without intentional stick motion.
internal sealed class NativeProbeGuide {
 internal const int Duration=90;
 readonly double started;
 internal NativeProbeGuide(double now){started=now;}
 internal string Label(double now){
  double t=Math.Max(0,now-started);
  if(t>=Duration)return "COMPLETE — release all, TV Back, export diagnostics";
  string task=t<5?"Leave all controls released":t<20?"Move LEFT stick fully left/right/up/down, then release":t<35?"Move RIGHT stick fully left/right/up/down, then release":t<50?"Squeeze/release L2, then R2, then both":t<60?"Move both sticks while holding Cross; release all":t<75?"Press/release Square, Circle, Triangle, L1, R1 separately":"Release all controls; check sticks center and triggers reach zero";
  return task+" • "+(int)Math.Ceiling(Duration-t)+"s left";
 }
}
