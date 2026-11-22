using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ElmSharp;

partial class PlayerApp {
 PlayerOptions options=new PlayerOptions();
 PadDiagnostics padStats=new PadDiagnostics();
 int settingsPage,testRefresh;bool exportPicking;
 volatile bool audioTesting,audioTestStop;
 bool soundEnabled {get=>options.Sound;set=>options.Sound=value;}
 long displaySkipped,displayReplaced,statFrames,statPresented,controlReads,staleReads,lastControlPacket;
 double statTime;string performance="Waiting for video";
 static readonly string[] InputNames={"Native pad + key events","Native gamepad only","Key events only (safe)"};
 static readonly string[] StickNames={"Either stick","Left stick only","Right stick only","Physical D-pad only"};
 string[] SettingsChoices(){
  if(settingsPage==4)return NetworkChoices();
  if(settingsPage==3)return KeyInput.ButtonNames.Select((n,i)=>n+": "+keyInput.BindingFor(i)).Concat(new[]{"Reset learned bindings","Back to controller options"}).ToArray();
  if(settingsPage==1)return new[]{"Player slot: "+(options.Slot==0?"Auto":options.Slot.ToString()),"Input source: "+InputNames[options.InputSource],"Digital movement: "+StickNames[options.StickSource],"Stick dead zone: "+(int)(options.AnalogDeadZone*100)+"%","Trigger threshold: "+(int)(options.TriggerLevel*100)+"%","Native face buttons: "+(options.FaceLayout==0?"Standard":"Swap Cross/Circle + Square/Triangle"),"PS1 controller: "+(dualShock?"DualShock ANALOG":"Standard DIGITAL"),"Open key-event test (safe)","Reset controller options","Learn key-event buttons","Native polling: "+(nativePollingArmed?"ON (saved)":"OFF (saved)"),"Controller movement test","Controller profile: "+(options.NativeProfile==1?"USB PS5 (experimental)":"Legacy digital"),"Learn controller menu button"+(options.MenuButton>=0?" (assigned)":""),"Clear controller menu button","Visual controller mapping","Back to settings"};
  if(settingsPage==2)return BenchmarkChoices();
  return new[]{"Play selected game","Choose game from USB","Import PS1 BIOS","Built-in red-screen demo","Sound: "+(soundEnabled?"ON":"OFF"),"PS1 controller: "+(dualShock?"DualShock ANALOG":"Standard DIGITAL"),"Last game diagnostics","Controller key-event test (safe)","Artwork diagnostics","Controller options","Audio / video options","Export diagnostics to USB","Network transfers / PC backups","Exit"};
 }
 void SaveOptions(){try{SaveScopedOptions();}catch(Exception e){status="Could not save settings: "+e.Message;}}
 void TogglePadMode(){dualShock=!dualShock;AtomicText.Write(Path.Combine(data,"pad-mode.txt"),dualShock?"dualshock":"digital");status="Controller mode applies to the next game.";}
 void ActivateSettingsPage(){
  if(settingsPage==4){ActivateNetworkOption();return;}
  if(settingsPage==3){if(selected<16){learningBit=selected;learnReady=false;status="";keyInput.ClearHeld();}else if(selected==16){keyInput.ResetBindings();SaveKeyBindings();status="Learned bindings cleared; keyboard defaults restored.";}else{settingsPage=24;selected=2;}return;}

  if(settingsPage==1){switch(selected){case 0:options.Slot=(options.Slot+1)%3;padSlot=0;break;case 1:options.InputSource=(options.InputSource+1)%3;break;case 2:options.StickSource=(options.StickSource+1)%4;break;case 3:options.DeadZone=(options.DeadZone+1)%3;break;case 4:options.Trigger=(options.Trigger+1)%3;break;case 5:if(customMap.Enabled)status="Custom mapping controls face buttons; disable it before using face swap.";else options.FaceLayout=1-options.FaceLayout;break;case 6:TogglePadMode();break;case 7:BeginControllerTest();return;case 8:customMap.Enabled=false;SaveControllerMapping();options.NativeProfile=1;eventPad.Profile=ControllerProfile.UsbPs5;options.InputSource=1;nativePollingArmed=true;options.MenuButton=-1;options.Slot=1;options.StickSource=options.DeadZone=options.FaceLayout=0;options.Trigger=1;padSlot=0;dualShock=false;AtomicText.Write(Path.Combine(data,"pad-mode.txt"),"digital");status="Controller defaults restored.";break;case 9:settingsPage=3;selected=0;return;case 10:nativePollingArmed=!nativePollingArmed;status=nativePollingArmed?"Native polling enabled and saved for future launches.":"Native polling disabled.";break;case 11:BeginControllerTest(true);return;case 12:options.NativeProfile=1-options.NativeProfile;eventPad.Profile=(ControllerProfile)options.NativeProfile;padSlot=0;status="Profile applies to tests and native gameplay. USB PS5: shoulder mapping pending.";break;case 13:BeginMenuButtonLearning();return;case 14:options.MenuButton=-1;status="Menu button cleared. Hold Create + Options, or use remote Play/Pause, to exit games.";break;case 15:BeginVisualMapping();return;case 16:settingsPage=0;selected=9;return;}}
  else if(settingsPage==2){ActivateBenchmarkOption();return;}
  SaveOptions();
 }
 string SettingsHelp(){
  if(settingsPage==4)return NetworkHelp();
  if(settingsPage==3)return "Select a PS1 button, press OK, then press its controller button.<br/>A key can map to only one PS1 button. Two physical buttons with the same reported name cannot be distinguished.<br/>Back/Escape always exits; analog axes are not available through key events.";
  if(settingsPage==1)return "Default: USB PS5, native gamepad only, polling ON. Changes are saved.<br/>Learn button names reported by this TV. Native tests may freeze on affected firmware.<br/>OK cycles a setting. Changes apply to the next game.<br/>Try a fixed slot if Auto picks the wrong device. Gamepad only isolates duplicate key events.<br/>USB PS5 profile enables sticks and L2/R2. L1/R1 are not mapped yet.<br/>Bluetooth: pair in TV settings first; unplug USB and turn other pads off.<br/>If no input, try slot 1 then 2 in Controller movement test; export diagnostics.<br/>USB PS5 mapping is unverified over Bluetooth. Use TV remote if buttons differ.";
  if(settingsPage==2)return BenchmarkHelp();
  return "Game: "+Esc(game.Length==0?"none selected":Path.GetFileName(game))+"<br/>BIOS: "+(File.Exists(Path.Combine(bios,"scph101.bin"))?"imported":"not imported")+"<br/>Use the TV remote to browse. Create + Options (1 second) or remote Play/Pause: save and leave game.<br/>DualShock: L3+R3 toggles analog. Keyboard: arrows, Z/X/A/S, Q/W, Enter/Shift.<br/>Controller and audio/video options contain the new testing controls.";
 }
 void BeginControllerTest(bool native=false){testReturnPage=settingsPage;testReturnSelected=selected;menuLearner=null;controllerTest=true;nativeTest=native;testRefresh=0;keyInput.ResetDiagnostics();padStats=new PadDiagnostics{GapWarningMs=native?250:50};padReported=false;worstPadGapMs=0;lastPadTick=0;lastPadError="";scanCounter=0;Volatile.Write(ref pad,new PadSnapshot());if(native)BeginNativeProbe();status=native?"90-second controller movement test. Auto selects slot 1. TV Back exits if the driver returns.":"Key events only — no native gamepad API calls. TV Back saves and exits.";}

 void EndControllerTest(){ResetMenuInput();
  if(nativeTest)try{FlushNativeTrace();}catch(Exception e){padStats.Error("Trace flush: "+e.Message);}
  try{File.WriteAllText(Path.Combine(data,"controller-test.txt"),ControllerReport()+"\nSettings:\n"+options.Serialize()+"\nMode: "+(dualShock?"DualShock":"Digital"));status="Controller test report saved.";}catch(Exception e){status="Could not save test report: "+e.Message;}
  if(nativeTest&&nativeProbe!=null)nativeProbe.Stop();
  menuLearner=null;controllerTest=false;nativeTest=false;settingsPage=testReturnPage;selected=testReturnSelected;eventPad.Reset();keyInput.ClearHeld();Interlocked.Exchange(ref keys,0);
 }
 void ExportDiagnostics(){
  bool captured=padStats.Polls>0||keyInput.Downs>0||keyInput.Ups>0;
  string controllerReport=captured?ControllerReport():"No controller test captured.";
  string savedTest=Path.Combine(data,"controller-test.txt");
  if(File.Exists(savedTest))controllerReport=File.ReadAllText(savedTest);
  string gameReport=File.Exists(logPath)?File.ReadAllText(logPath):"No game report captured.";
  string readerInspection=ReaderInspection.Capture(typeof(Tizen.TV.Accessory.GamePad));
  string file=Path.Combine(folder,"TizenPS-diagnostics-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".txt");
  using(var output=new StreamWriter(new FileStream(file,FileMode.CreateNew,FileAccess.Write))){output.WriteLine("TizenPSX 0.3.39 diagnostics — "+DateTime.UtcNow.ToString("O"));output.WriteLine(HardwareReport.Capture());output.WriteLine(DirectDiagnostics);output.WriteLine("Current options:\n"+options.Serialize()+"\nMode: "+(dualShock?"DualShock":"Digital"));output.WriteLine("Custom controller map:\n"+customMap.Serialize());output.WriteLine(controllerReport);output.WriteLine("\nLast game:\n"+gameReport);output.WriteLine("\nGameplay input trace:\n"+(File.Exists(GameplayTracePath)?File.ReadAllText(GameplayTracePath):"No gameplay trace recorded."));output.WriteLine("\nNative controller checkpoints:\n"+(File.Exists(NativeTracePath)?File.ReadAllText(NativeTracePath):"No native test recorded."));output.WriteLine("\nSDK controller reader:\n"+readerInspection);}
  browsing=exportPicking=false;settingsPage=16;selected=0;status="Saved "+Path.GetFileName(file)+" to the selected folder.";
 }
 string ControllerTestText()=>nativeTest?NativeProbeText():KeyTestText();
 void StartAudioTest(){
  audioTesting=true;audioTestStop=false;status="Quiet stereo test: LEFT, then RIGHT, then BOTH. Return cancels.";
  new Thread(()=>{string result="Stereo test complete: left, right, both.";try{using(var audio=new Sound(options.BufferMs)){
   var samples=new short[882*2];for(int section=0;section<3&&!audioTestStop;section++)for(int block=0;block<40&&!audioTestStop;block++){
    for(int i=0;i<882;i++){int n=block*882+i;double envelope=Math.Min(1,Math.Min(n/441.0,(35279-n)/441.0));short v=(short)(Math.Sin(2*Math.PI*440*n/44100)*2000*Math.Max(0,envelope));samples[i*2]=section==1?(short)0:v;samples[i*2+1]=section==0?(short)0:v;}
    var pin=GCHandle.Alloc(samples,GCHandleType.Pinned);try{audio.Push(pin.AddrOfPinnedObject(),882);}finally{pin.Free();}
   }
   if(!audioTestStop)audio.Drain();
   if(audioTestStop)result="Stereo test cancelled.";
  }}catch(Exception e){result="Stereo test failed: "+e.Message;}
   EcoreMainloop.PostAndWakeUp(()=>{audioTesting=false;status=result;Draw();});
  }){IsBackground=true}.Start();
 }
 void UpdatePerformance(){
  if(!playing)return;double now=clock.Elapsed.TotalSeconds,elapsed=now-statTime;if(elapsed<1)return;
  long f=Interlocked.Read(ref frames),p=Interlocked.Read(ref presented);var sample=Volatile.Read(ref pad);
  ulong packet=unchecked((ulong)Interlocked.Read(ref lastControlPacket));
  performance=BenchmarkText()+"<br/>Frame delivery: "+VideoPipeline+"<br/>NEON renderer | "+(options.EnhancedResolution?"2x requested":"Native")+" | Output "+pw+" x "+ph+"<br/>"+"<br/>"+Esc(AudioLiveText())+"<br/>New video "+((f-statFrames)/elapsed).ToString("F1")+" fps | submitted "+((p-statPresented)/elapsed).ToString("F1")+" fps<br/>Display skipped "+Interlocked.Read(ref displaySkipped)+" | replaced "+Interlocked.Read(ref displayReplaced)+" | input: "+(!nativePollingArmed||!options.UsePad?"key events":sample.Connected?((Stopwatch.GetTimestamp()-sample.Timestamp)*1000.0/Stopwatch.Frequency).ToString("F0")+" ms":"disconnected")+"<br/>Poll gap max "+worstPadGapMs.ToString("F0")+" ms | "+BenchmarkOptions.Name(options)+" | skip "+options.FrameSkip+" | buffer "+options.BufferMs+" ms | "+(Volatile.Read(ref audioClock)==1?"audio pacing":"timer pacing")+"<br/>Core input: "+PadSnapshot.Names((uint)packet&65535)+" | "+(nativePollingArmed&&options.UsePad?"native slot "+sample.Slot:"key events")+" | "+(dualShock?"DualShock":"Digital");
  statTime=now;statFrames=f;statPresented=p;
  if(options.Overlay==1){label.Text="<font_size=24><color=#ffffff>"+performance+"</color></font_size>";label.Show();}
 }
}
