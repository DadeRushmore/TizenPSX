using System;

internal enum FriendlySetting {Sound,SmoothAudio,AudioDelay,SoundDetail,Echo,Picture,DisplayFrames,Overlay,FrameSkip,Preset,CpuMode,CpuClock,InputTrace,VideoPath}
internal static class SettingsCatalog {
 static int Cycle(int value,int count,int step)=>(value+count+(step<0?-1:1))%count;
 internal static string Value(FriendlySetting id,PlayerOptions o){switch(id){
  case FriendlySetting.VideoPath:return o.BufferedVideo?"Buffered — experimental":"Compatible";
  case FriendlySetting.Sound:return o.Sound?"On":"Off";
  case FriendlySetting.SmoothAudio:return o.AdaptiveAudio?"On":"Off";
  case FriendlySetting.AudioDelay:return new[]{"Small — 60 ms","Normal — 120 ms","Large — 200 ms"}[o.Buffer];
  case FriendlySetting.SoundDetail:return new[]{"Normal","Simple — less work","Minimal"}[o.Interpolation];
  case FriendlySetting.Echo:return o.Reverb?"On":"Off";
  case FriendlySetting.Picture:return o.EnhancedResolution?"Sharper 2×":"Original";
  case FriendlySetting.DisplayFrames:return o.Presentation==0?"Every frame":"Every second frame";
  case FriendlySetting.Overlay:return o.Overlay==1?"On":"Off";
  case FriendlySetting.FrameSkip:return new[]{"Off","Light","More"}[o.FrameSkip];
  case FriendlySetting.Preset:{int p=BenchmarkOptions.Preset(o);return p<0?"Custom":new[]{"Compatible","Smoother sound","Lighter graphics","Silent comparison","Fast CPU — experimental"}[p];}
  case FriendlySetting.CpuMode:return o.Dynarec?"Fast":"Compatibility";
  case FriendlySetting.CpuClock:return new[]{"Auto — recommended","Low — 50%","Medium — 57%","High — 75%","Full — 100%"}[o.CpuClock];
  case FriendlySetting.InputTrace:return o.InputTrace?"On":"Off";
  default:return "";
 }}
 internal static string Explanation(FriendlySetting id){switch(id){
  case FriendlySetting.VideoPath:return "Compatible uses the existing display path. Buffered moves pictures to the TV with less copying and waiting. It may help busy scenes. Compare the same scene in both modes; this experimental option still needs TV testing.";
  case FriendlySetting.Sound:return "Turns game sound on or off. Turning it off can help compare performance, but it is not a universal speed fix.";
  case FriendlySetting.SmoothAudio:return "Reduces broken sound when the game runs slowly. Sound may become slower and lower in pitch. It cannot make slow gameplay run at full speed.";
  case FriendlySetting.AudioDelay:return "A larger sound buffer can absorb short delays, but adds delay between an action and its sound. Try Normal first.";
  case FriendlySetting.SoundDetail:return "Normal sounds closest to the console. Simple and Minimal reduce the work needed to produce sound, at the cost of detail.";
  case FriendlySetting.Echo:return "Keeps the game's room and echo effects. Turning these off can save some processing time.";
  case FriendlySetting.Picture:return "Original uses the game's normal resolution. Sharper 2× redraws supported 3D scenes at twice the resolution. It can look cleaner, but may slow down or fail in some games.";
  case FriendlySetting.DisplayFrames:return "Every frame gives smoother movement. Every second frame reduces display work and can help a busy TV, but movement looks less smooth.";
  case FriendlySetting.Overlay:return "Shows emulation speed, FPS, sound and the actual output size during play. Around 100% speed means the game is running in real time.";
  case FriendlySetting.FrameSkip:return "Allows the emulator to skip drawing frames to reduce graphics work. Gameplay may look choppier or show flicker. Try Off first.";
  case FriendlySetting.Preset:return "A starting combination of CPU and sound settings. Fast CPU uses ARM acceleration and can fail in some games. Picture quality and frame delivery stay separate choices. Fine-tune the other pages afterwards.";
  case FriendlySetting.CpuMode:return "Fast is the normal choice. Try Compatibility if a game crashes or behaves incorrectly; it runs more slowly.";
  case FriendlySetting.CpuClock:return "Changes the speed of the emulated PlayStation CPU. Lower values reduce its work but can alter game behaviour. Auto is recommended. This does not overclock the TV.";
  case FriendlySetting.InputTrace:return "Records detailed controller events for troubleshooting. Leave it off during normal play because logging adds work.";
  default:return "";
 }}
 internal static void Change(FriendlySetting id,PlayerOptions o,int step){switch(id){
  case FriendlySetting.VideoPath:o.BufferedVideo=!o.BufferedVideo;break;
  case FriendlySetting.Sound:o.Sound=!o.Sound;break;case FriendlySetting.SmoothAudio:o.AdaptiveAudio=!o.AdaptiveAudio;break;
  case FriendlySetting.AudioDelay:o.Buffer=Cycle(o.Buffer,3,step);break;case FriendlySetting.SoundDetail:o.Interpolation=Cycle(o.Interpolation,3,step);break;
  case FriendlySetting.Echo:o.Reverb=!o.Reverb;break;case FriendlySetting.Picture:o.EnhancedResolution=!o.EnhancedResolution;break;
  case FriendlySetting.DisplayFrames:o.Presentation=1-o.Presentation;break;case FriendlySetting.Overlay:o.Overlay=1-o.Overlay;break;
  case FriendlySetting.FrameSkip:o.FrameSkip=Cycle(o.FrameSkip,3,step);break;case FriendlySetting.Preset:BenchmarkOptions.Apply(o,Cycle(BenchmarkOptions.Preset(o),5,step));break;
  case FriendlySetting.CpuMode:o.Dynarec=!o.Dynarec;break;case FriendlySetting.CpuClock:o.CpuClock=Cycle(o.CpuClock,5,step);break;
  case FriendlySetting.InputTrace:o.InputTrace=!o.InputTrace;break;
 }}
 internal static bool IsScopedPage(int page)=>page==2||page==11||page==12||page==13||page==14||page==18;
 internal static PlayerOptions SaferCopy(PlayerOptions saved){var copy=PlayerOptions.Parse(saved.Serialize());copy.BufferedVideo=false;copy.Dynarec=false;copy.EnhancedResolution=false;copy.FrameSkip=0;copy.CpuClock=0;return copy;}
}
