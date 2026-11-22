using System;

static class BenchmarkChecks {
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Run(){
  var fresh=new PlayerOptions();Need(fresh.Overlay==1&&!fresh.InputTrace&&BenchmarkOptions.CoreFlags(fresh)==12288,"baseline FPS on, tracing off, v0.3.33 full-spec defaults");
  var migrated=PlayerOptions.Parse("inputSchema=3\noverlay=0\nmenuButton=11\nprofile=1\ninput=1\nbuffer=2\nsound=0");
  Need(migrated.Overlay==1&&migrated.MenuButton==11&&migrated.Buffer==2&&!migrated.Sound,"one-time FPS migration preserves controller binding and audio choices");
  migrated.Overlay=0;migrated.InputTrace=true;
  var saved=PlayerOptions.Parse(migrated.Serialize());Need(saved.Overlay==0&&saved.InputTrace,"future overlay and trace choices persist");
  for(int i=0;i<4;i++){
   var o=new PlayerOptions{MenuButton=11,Slot=2};BenchmarkOptions.Apply(o,i);
   Need(BenchmarkOptions.Preset(o)==i&&o.MenuButton==11&&o.Slot==2,"preset applies without changing controller");
   Need(PlayerOptions.Parse(o.Serialize()).Serialize()==o.Serialize(),"preset settings round trip");
  }
  var speed=new PlayerOptions();BenchmarkOptions.Apply(speed,2);
  Need(BenchmarkOptions.CoreFlags(speed)==9473&&speed.Sound&&speed.BufferMs==200&&speed.Presentation==0,"speed preset maps fixed frameskip/simple/no reverb without double display skip");
  speed.CpuClock=1;Need(BenchmarkOptions.CoreFlags(speed)==9489&&BenchmarkOptions.Preset(speed)==-1,"explicit underclock maps to native index");
  var invalid=PlayerOptions.Parse("frameSkip=3\ncpuClock=5\ninterpolation=3\nreverb=2\ninputTrace=2");
  Need(BenchmarkOptions.CoreFlags(invalid)==12288&&!invalid.InputTrace,"out-of-range performance settings rejected");
  var first=new BenchmarkSample(30,22050,1,60,null);
  Need(first.Fps==30&&first.Speed==50&&first.AudioPercent==50,"half-speed emulation measured independently of video");
  var next=new BenchmarkSample(90,66150,2,60,first);
  Need(next.Fps==60&&next.Speed==100&&next.AverageFps==45&&next.AudioPercent==100,"interval speed and session average distinct");
  var pal=new BenchmarkSample(50,44100,1,50,null);Need(pal.Speed==100,"PAL target is 50 rather than assumed 60");
  var same=new BenchmarkSample(50,44100,1,50,pal);Need(!double.IsNaN(same.Fps)&&!double.IsInfinity(same.Speed),"zero-time final sample remains finite");
  Console.WriteLine("PASS: performance migration, presets, native flags, saved overrides, bounded options, FPS/speed/audio measurements.");
 }
}
