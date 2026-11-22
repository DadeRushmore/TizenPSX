using System;

internal static class BenchmarkOptions {
 internal static readonly string[] Presets={"Baseline","Audio cushion","Speed test","Silent comparison","Turbo (experimental)"};
 internal static readonly string[] CpuNames={"Auto (recommended)","50% (underclock test)","57%","75%","100% (more CPU work)"};
 internal static readonly string[] InterpolationNames={"Gaussian (normal)","Simple (lighter)","Off (lowest quality)"};
 // ABI v2: skip [1:0], CPU index [6:4], interpolation [9:8], disable reverb [10], display skip [11], dynarec [12], enhanced resolution [13].
 internal static uint CoreFlags(PlayerOptions o)=>(uint)(o.FrameSkip|(o.CpuClock<<4)|(o.Interpolation<<8)|(o.Reverb?0:1024)|(o.Presentation==1?2048:0)|(o.Dynarec?4096:0)|(o.EnhancedResolution?8192:0));
 internal static void Apply(PlayerOptions o,int preset){
  if(preset<0||preset>=Presets.Length)throw new ArgumentOutOfRangeException(nameof(preset));
  o.AdaptiveAudio=preset!=0&&preset!=3;o.Dynarec=preset==4;
  o.Sound=preset!=3;o.Buffer=preset==1||preset==2||preset==4?2:1;
  o.FrameSkip=preset==2?1:0;o.CpuClock=0;o.Interpolation=preset==2||preset==4?1:0;
  o.Reverb=preset!=2&&preset!=4;o.Presentation=0;o.Overlay=1;o.InputTrace=false;
 }
 internal static int Preset(PlayerOptions o){
  for(int i=0;i<Presets.Length;i++){
   var p=new PlayerOptions();Apply(p,i);
   if(o.AdaptiveAudio==p.AdaptiveAudio&&o.Dynarec==p.Dynarec&&o.Sound==p.Sound&&o.Buffer==p.Buffer&&o.FrameSkip==p.FrameSkip&&o.CpuClock==p.CpuClock&&o.Interpolation==p.Interpolation&&o.Reverb==p.Reverb&&o.Presentation==p.Presentation)return i;
  }return -1;
 }
 internal static string Name(PlayerOptions o){int i=Preset(o);return i<0?"Custom":Presets[i];}
}

// Emulator ticks are independent of rendered frames and display submissions.
internal sealed class BenchmarkSample {
 internal readonly uint Ticks,AudioFrames;
 internal readonly double Seconds,Target,Fps,Speed,AverageFps,AudioPercent;
 internal readonly long Received=System.Diagnostics.Stopwatch.GetTimestamp();
 internal BenchmarkSample(uint ticks,uint audio,double seconds,double target,BenchmarkSample previous){
  Ticks=ticks;AudioFrames=audio;Seconds=seconds;Target=target;
  double elapsed=seconds-(previous?.Seconds??0);
  if(elapsed>0&&ticks>=(previous?.Ticks??0)&&audio>=(previous?.AudioFrames??0)){
   Fps=(ticks-(previous?.Ticks??0))/elapsed;
   AudioPercent=100.0*(audio-(previous?.AudioFrames??0))/elapsed/44100;
  }
  Speed=target>0?100*Fps/target:0;AverageFps=seconds>0?ticks/seconds:0;
 }
}
