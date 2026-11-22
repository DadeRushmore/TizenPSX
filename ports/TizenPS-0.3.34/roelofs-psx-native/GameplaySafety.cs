using System;
using System.Collections.Generic;

internal static class GameplayExit {
 internal static bool MenuButtonPressed(int assigned,int code,int value)=>assigned>=0&&assigned==code&&value==1;
 internal static bool Requested(string key,bool down,bool nativeInput){
  if(!down)return false;
  return key=="Escape"||key=="XF86AudioPlay"||key=="XF86AudioPause"||key=="XF86AudioPlayPause"||(!nativeInput&&key=="XF86Back");
 }
}

internal sealed class MenuButtonLearner {
 readonly double readyAt;
 internal int Candidate=-1,Learned=-1;
 internal MenuButtonLearner(double now){readyAt=now+1;}
 internal bool Observe(int code,int value,double now){
  if(now<readyAt||Learned>=0||code<0||code>255)return false;
  if(Candidate<0&&value==1)Candidate=code;
  else if(code==Candidate&&value==0){Learned=code;return true;}
  return false;
 }
}

// UI input and emulator callbacks share a bounded tail; never write per event.
internal sealed class GameplayTrace {
 readonly object gate=new object();
 readonly Queue<string> lines=new Queue<string>();
 readonly int maxChars;
 int chars,dropped;
 internal GameplayTrace(int limit=2000000){if(limit<128)throw new ArgumentOutOfRangeException(nameof(limit));maxChars=limit;}
 internal void Add(string line){lock(gate){if(line.Length>maxChars){dropped++;return;}while(chars+line.Length>maxChars||lines.Count>=4096){chars-=lines.Dequeue().Length;dropped++;}lines.Enqueue(line);chars+=line.Length;}}
 internal string Snapshot(){lock(gate)return "Gameplay trace (bounded tail); omitted records: "+dropped+"\n"+string.Join("\n",lines);}
}
