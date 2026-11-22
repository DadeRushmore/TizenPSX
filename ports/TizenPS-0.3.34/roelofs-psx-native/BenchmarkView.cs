using System;
using System.Globalization;
using System.Threading;

partial class PlayerApp {
 BenchmarkSample benchmark;
 double benchmarkLogTime;
 void RecordBenchmark(uint ticks,uint audio,uint elapsedMs,uint targetMilliFps){
  double seconds=elapsedMs/1000.0,target=targetMilliFps/1000.0;
  try{
   var sample=new BenchmarkSample(ticks,audio,seconds,target,Volatile.Read(ref benchmark));
   Volatile.Write(ref benchmark,sample);
   if(seconds-benchmarkLogTime>=5){
    benchmarkLogTime=seconds;Log(WorkSummary());var videoPool=Volatile.Read(ref activeVideoFrames);if(videoPool!=null)Log(videoPool.Summary);var output=Volatile.Read(ref activeSound);if(output!=null)Log(output.Summary);
    Log(string.Format(CultureInfo.InvariantCulture,"BENCH {0:F1}s: emulation {1:F1}/{2:F2} FPS ({3:F1}%), average {4:F1} FPS; audio produced {5:F1}%",seconds,sample.Fps,target,sample.Speed,sample.AverageFps,sample.AudioPercent));
   }
  }catch(Exception e){Log("Benchmark callback: "+e.Message);}
 }
 string BenchmarkText(){
  var s=Volatile.Read(ref benchmark);
  if(s==null)return "Emulation FPS: waiting for first sample";
  if((System.Diagnostics.Stopwatch.GetTimestamp()-s.Received)/(double)System.Diagnostics.Stopwatch.Frequency>3)return "Emulation FPS: waiting for core (last sample is stale)";
  return System.Threading.Volatile.Read(ref engineStatus)+"<br/>Emulation "+s.Fps.ToString("F1")+" / "+s.Target.ToString("F2")+" FPS | Speed "+s.Speed.ToString("F0")+"% | Avg "+s.AverageFps.ToString("F1")+" FPS";
 }
 string[] BenchmarkChoices()=>new[]{
  "Quick preset: "+BenchmarkOptions.Name(options)+" (OK: next)",
  "Sound output: "+(soundEnabled?"ON":"OFF (timer pacing)"),
  "Audio buffer: "+options.BufferMs+" ms",
  "Core frameskip: "+(options.FrameSkip==0?"OFF":"up to "+options.FrameSkip+" skipped frame(s)"),
  "Emulated PS1 CPU: "+BenchmarkOptions.CpuNames[options.CpuClock],
  "Audio interpolation: "+BenchmarkOptions.InterpolationNames[options.Interpolation],
  "Audio reverb: "+(options.Reverb?"ON":"OFF"),
  "Display updates: "+(options.Presentation==0?"Every new frame":"Every second new frame"),
  "FPS / speed overlay: "+(options.Overlay==1?"ON":"OFF"),
  "Detailed input trace: "+(options.InputTrace?"ON (diagnostics)":"OFF (benchmarking)"),
  "Play quiet stereo test: left / right / both",
  "Adaptive audio: "+(options.AdaptiveAudio?"ON (slower/lower pitch when needed)":"OFF (original output)"),
  "CPU engine: "+(options.Dynarec?"ARM dynarec (experimental)":"Interpreter (compatible)"),
  "Internal resolution: "+(options.EnhancedResolution?"2x Enhanced (experimental)":"Native")+" (next launch)",
  "Settings scope: "+(editGame.Length==0?"Global defaults":"This game — "+System.IO.Path.GetFileNameWithoutExtension(editGame)),
  "Remove this game override",
  "Back to settings"
 };
 void ActivateBenchmarkOption(){
  switch(selected){
   case 0:BenchmarkOptions.Apply(options,(BenchmarkOptions.Preset(options)+1)%BenchmarkOptions.Presets.Length);break;
   case 1:soundEnabled=!soundEnabled;break;
   case 2:options.Buffer=(options.Buffer+1)%3;break;
   case 3:options.FrameSkip=(options.FrameSkip+1)%3;break;
   case 4:options.CpuClock=(options.CpuClock+1)%BenchmarkOptions.CpuNames.Length;break;
   case 5:options.Interpolation=(options.Interpolation+1)%BenchmarkOptions.InterpolationNames.Length;break;
   case 6:options.Reverb=!options.Reverb;break;
   case 7:options.Presentation=1-options.Presentation;break;
   case 8:options.Overlay=1-options.Overlay;break;
   case 9:options.InputTrace=!options.InputTrace;break;
   case 10:StartAudioTest();return;
   case 11:options.AdaptiveAudio=!options.AdaptiveAudio;break;
   case 12:options.Dynarec=!options.Dynarec;break;
   case 13:options.EnhancedResolution=!options.EnhancedResolution;break;
   case 14:ToggleGameScope();return;
   case 15:if(editGame.Length>0){AtomicText.Remove(PerGameOptions.FileName(data,editGame));RestoreGlobalPerformance();status="Override removed; this game follows global defaults.";}else status="Select This game scope first.";return;
   case 16:RestoreGlobalPerformance();editGame="";settingsPage=0;selected=10;return;
  }
  status="Saved for next game: "+BenchmarkOptions.Name(options)+". PS: library; Settings > Audio / video options: these options.";
  SaveOptions();
 }
 string BenchmarkHelp()=>"OK cycles settings; changes apply on the next game launch.<br/>2x Enhanced sharpens 3D using the NEON software renderer; it may reduce speed.<br/>Settings scope selects Global defaults or the highlighted game. Changes save immediately.<br/>Resolution is independent of Quick preset. Overlay shows actual output size.<br/>Compare the same fight: Speed near 100% means real-time emulation.<br/>Turbo tries the ARM dynarec; Interpreter remains the fallback.<br/>Adaptive audio reduces gaps when slow, with lower pitch. It cannot restore game speed.<br/>CPU Auto is recommended; 50% may help performance but can cause game glitches.<br/>100% adds emulation work; it does not overclock the TV. Larger buffers add audio delay.";
}
