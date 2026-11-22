using System;

using System.Collections.Generic;

using System.Diagnostics;

using System.IO;

using System.Linq;

using System.Runtime.InteropServices;

using System.Threading;

using ElmSharp;

using Tizen.Applications;

using Tizen.TV.Accessory;



partial class PlayerApp : CoreUIApplication {

 Window win;Label label;IntPtr image;string data,resources,game="",bios="",folder="",status="",logPath;bool browsing,selectBios,diagnostics,artworkDiagnostics,controllerTest,dualShock;int audioClock;IntPtr padTimer;PadSnapshot pad=new PadSnapshot();bool paused;long padHoldStart;uint previousPad;int padPolls,padSlot,scanCounter;long lastPadTick;double worstPadGapMs;string lastPadError="";

 readonly List<string> entries=new List<string>(),logs=new List<string>();int selected;volatile bool playing,stop;int keys;

 readonly object frameLock=new object();int[] frame;int fw,fh,pw,ph;bool pending;long frames,presented;Stopwatch clock=new Stopwatch();double comboStart=-1;bool padReported;

 static void Main(string[] args){Elementary.Initialize();Elementary.ThemeOverlay();new PlayerApp().Run(args);}

 protected override void OnCreate(){base.OnCreate();if(!EcoreMainloop.IsMainThread)throw new Exception("UI must start on the main thread");data=Application.Current.DirectoryInfo.Data;resources=Application.Current.DirectoryInfo.Resource;logPath=Path.Combine(data,"last-game.txt");

  options=PlayerOptions.Load(Path.Combine(data,"player-options.txt"));RecoverExperimentalSession();eventPad.ButtonEvent=OnNativeButton;LoadControllerMapping();LoadKeyBindings();InitializeDirectFaces();InitializeTwoPlayers();bios=Path.Combine(data,"bios");Directory.CreateDirectory(bios);try{dualShock=AtomicText.Read(Path.Combine(data,"pad-mode.txt")).Trim()=="dualshock";}catch{}Directory.CreateDirectory(Path.Combine(data,"cards"));InitializeNetwork();

  try{game=File.ReadAllText(Path.Combine(data,"last-game-path.txt"));}catch{}

  win=new Window("TizenPSX");win.Show();win.Active();var bg=new Background(win){Color=Color.FromRgb(8,10,22)};bg.Show();win.AddResizeObject(bg);Screen.evas_object_layer_set(bg.Handle,-100);

  label=new Label(win){LineWrapType=WrapType.Mixed};label.Geometry=new Rect(65,40,1780,990);label.Show();label.Layer=LauncherView.TextLayer;

  padTimer=EcoreMainloop.AddTimer(1.0/60,()=>{if(ShouldPollController)PollController();else {DisableDirectFaces();nativePads.Enabled=false;}if(!paused&&controllerTest&&++testRefresh%15==0)Draw();return true;});

  EcoreMainloop.AddTimer(1,()=>{UpdatePerformance();return true;});

  InitializeLauncher();

  win.KeyDown+=(s,e)=>Key(e.KeyName,true);win.KeyUp+=(s,e)=>Key(e.KeyName,false);Draw();

 }

 protected override void OnResume(){paused=false;base.OnResume();}

 protected override void OnPause(){shellConfirmHeld=false;if(mappingPage){mappingPage=false;mappingArmed=false;mappingView.Hide();controllerTest=false;nativeTest=false;}paused=true;nativePads.Enabled=false;DisableDirectFaces();ResetMenuInput();menuExitHeld=false;menuLearner=null;eventPad.Reset();captureRelease="";learningBit=-1;keyInput.ClearHeld();audioTestStop=true;pad=new PadSnapshot();if(playing)RequestGameStop("Application OnPause");else stop=true;Interlocked.Exchange(ref keys,0);base.OnPause();}

 static string Esc(string s){return s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");}

 void Log(string s){lock(logs){logs.Add(s);if(logs.Count>100)logs.RemoveAt(0);try{File.AppendAllText(logPath,s+Environment.NewLine);}catch{}}}

 void Draw(){if(playing)return;if(mappingPage){DrawVisualMapping();return;}if(launcher&&!browsing&&!diagnostics&&!artworkDiagnostics&&!controllerTest){label.Hide();launcherView.DrawLibrary(VisibleGames(),libraryIndex,libraryTab,status,scanning);return;}if(!browsing&&!controllerTest&&!diagnostics&&!artworkDiagnostics&&!gamePage&&settingsPage==0){settingsPage=10;selected=0;}if(ShellActive){DrawShell();return;}launcherView?.Hide();label.Show();string t="<font_size=42><color=#b9a2ff>TIZENPSX  /  PLAYER SETTINGS</color></font_size><br/><font_size=26><color=#ffffff>0.3.39 • Controller / audio / video test build • Return: Library<br/><br/>";

  if(learningBit>=0){t+="LEARN BUTTON: "+KeyInput.ButtonNames[learningBit]+"<br/><br/>Release the menu OK button, then press the desired controller button.<br/>TV Back cancels. Back/Escape are reserved for exiting.<br/><br/>The next new key-down event will be saved for this PS1 button.";}

  else if(controllerTest){t+=ControllerTestText();}

  else if(artworkDiagnostics){t+="ARTWORK DIAGNOSTICS — Return to settings<br/><br/>"+Esc(launcherView.ArtworkDiagnostics).Replace("\n","<br/>");}

  else if(diagnostics){t+="LAST GAME — Return to menu<br/><br/>";lock(logs){foreach(var line in logs.Skip(Math.Max(0,logs.Count-20)))t+=Esc(line)+"<br/>";}}

  else if(settingsPage==4&&networkEdit){t+=NetworkEditText();}

  else if(browsing){t+=(exportPicking?"CHOOSE A USB FOLDER FOR DIAGNOSTICS":selectBios?"CHOOSE BIOS":"CHOOSE GAME (CUE preferred)")+"<br/>"+Esc(folder.Length==0?"Storage devices":folder)+"<br/><br/>";int start=Math.Max(0,selected-6);for(int i=start;i<Math.Min(entries.Count,start+13);i++)t+=(i==selected?"&gt; ":"  ")+Esc(i==0?"[..] Back":entries[i]=="@export"?"[ Save diagnostic reports here ]":entries[i]=="@scan"?"[ Use this folder — scan games ]":Path.GetFileName(entries[i].TrimEnd('/')))+"<br/>";}

  else{var choices=SettingsChoices();int first=Math.Max(0,selected-7);t+=(settingsPage==1?"CONTROLLER OPTIONS":settingsPage==2?(editGame.Length>0?"GAME SETTINGS: "+Esc(Path.GetFileNameWithoutExtension(editGame)):"GLOBAL AUDIO / VIDEO OPTIONS"):settingsPage==4?"NETWORK TRANSFERS / PC BACKUPS":settingsPage==3?"LEARN KEY-EVENT BUTTONS":"SETTINGS")+"<br/>";for(int i=first;i<Math.Min(choices.Length,first+9);i++)t+=(selected==i?"&gt; ":"  ")+Esc(choices[i])+"<br/>";

   t+="<br/>"+SettingsHelp();

  }label.Text=t+"<br/><color=#a8c8ef>"+Esc(status)+"</color></color></font_size>";

 }

 string gameExitReason="";

 int gameKeyLogs;

 void RequestGameStop(string reason){

  if(Interlocked.CompareExchange(ref gameExitReason,reason,"")=="")Log("STOP REQUEST: "+reason);

  stop=true;

 }

 void Key(string k,bool down,bool nativeMenu=false,bool nativeChecked=false){
  if(TwoPlayerPolling&&!nativeMenu&&!nativeChecked&&!controllerTest&&!mappingPage&&DeferNativeWindowKey(k,down))return;

  if(!nativeMenu&&!down&&(k=="Return"||k=="KP_Enter"))shellConfirmHeld=false;

  if(!playing&&!mappingPage&&!controllerTest&&!browsing&&learningBit<0&&!nativeMenu&&(k=="Return"||k=="KP_Enter")){if(!down)shellConfirmHeld=false;else{if(shellConfirmHeld)return;shellConfirmHeld=true;}}

  if(mappingPage){if(down&&!nativeMenu)QueueRemoteMappingKey(k);return;}

  if(settingsPage==4&&!playing){if(networkEdit){NetworkEditKey(k,down);return;}if(networkBusy){if(down&&KeyInput.Reserved(k))networkCancel?.Cancel();return;}if(down&&selected==5&&(k=="Left"||k=="Right")){CycleTransferGroup(k=="Left"?-1:1);return;}}

  if(ShellActive&&HandleShellKey(k,down,nativeMenu))return;

  if(playing&&gameKeyLogs++<64)Log("Gameplay key: "+(down?"DOWN ":"UP ")+k+" | mapped="+keyInput.Bit(k)+" | reserved="+KeyInput.Reserved(k)+" | inputSource="+options.InputSource);



  if(audioTesting){if(down&&KeyInput.Reserved(k)){audioTestStop=true;status="Stopping stereo test…";Draw();}return;}

  if(captureRelease.Length>0){if(k==captureRelease){if(!down)captureRelease="";return;}if(down&&KeyInput.Reserved(k))captureRelease="";else return;}

  if(learningBit>=0){

   if(!down){if(k=="Return"||k=="KP_Enter")learnReady=true;return;}

   if(KeyInput.Reserved(k)){learningBit=-1;status="Mapping cancelled.";Draw();return;}

   if(!learnReady&&(k=="Return"||k=="KP_Enter"))return;

   try{string old=keyInput.Bind(k,learningBit);status=k+" → "+KeyInput.ButtonNames[learningBit]+" (previously "+old+")";SaveKeyBindings();captureRelease=k;learningBit=-1;}catch(Exception e){status=e.Message;}

   Draw();return;

  }

  if(controllerTest&&down&&KeyInput.Reserved(k)){EndControllerTest();Draw();return;}

  if(controllerTest||playing){keyInput.Observe(k,down,KeyTime);Interlocked.Exchange(ref keys,unchecked((int)keyInput.Bits));}

  // All ordinary buttons belong to the tester, including arrows and OK.

  if(controllerTest)return;

  if(playing){

   bool nativeInput=twoPlayerNative||(nativePollingArmed&&options.UsePad);

   if(GameplayExit.Requested(k,down,nativeInput))RequestGameStop("Gameplay exit key: "+k);

   else if(down&&k=="XF86Back"&&nativeInput)gameplayTrace.Add(clock.Elapsed.TotalSeconds.ToString("F3")+" BACK ignored during native gameplay (remote Play/Pause exits)");

   return;

  }

  if(!down)return;if(!menuKeyFilter.Accept(k,nativeMenu,KeyTime))return;if(k=="Return"||k=="KP_Enter"||KeyInput.Reserved(k))menuNavigation.Reset();if(launcher&&!browsing&&!diagnostics&&!artworkDiagnostics&&!controllerTest){try{LauncherKey(k);}catch(Exception e){status=e.Message;Draw();}return;}if(k=="XF86Back"||k=="Escape"){if(controllerTest){EndControllerTest();}else if(artworkDiagnostics){artworkDiagnostics=false;settingsPage=24;selected=3;}else if(diagnostics){diagnostics=false;settingsPage=16;selected=1;}else if(browsing)Back();else if(settingsPage==3){settingsPage=24;selected=2;}else if(settingsPage!=0){selected=settingsPage==4?4:0;settingsPage=settingsPage==4?10:15;}else{ShowLibrary();return;}Draw();return;}

  if(controllerTest||artworkDiagnostics)return;int count=browsing?entries.Count:SettingsChoices().Length;if(k=="Up")selected=(selected+count-1)%count;else if(k=="Down")selected=(selected+1)%count;else if(k=="Return"||k=="KP_Enter"){try{Activate();}catch(Exception e){status=e.Message;}}Draw();

 }

 void Activate(){if(diagnostics||artworkDiagnostics||controllerTest)return;if(settingsPage!=0&&!browsing){ActivateSettingsPage();return;}if(browsing){if(exportPicking&&folder.Length>0&&selected==1){ExportDiagnostics();return;}if(scanPicking&&folder.Length>0&&selected==1){ScanFolder(folder);return;}if(selected==0){Back();return;}var p=entries[selected];if(Directory.Exists(p)){Browse(p);return;}if(selectBios){if(new FileInfo(p).Length!=524288){status="Expected a 512 KB PS1 BIOS file.";return;}File.Copy(p,Path.Combine(bios,"scph101.bin"),true);status="BIOS imported.";}else{game=p;var chosen=library.Add(p);SaveLibrary();File.WriteAllText(Path.Combine(data,"last-game-path.txt"),game);browsing=false;scanPicking=false;ShowGamePage(chosen);return;}browsing=false;scanPicking=false;selected=0;return;}

  if(selected==0){if(!File.Exists(game)){status="Choose an uncompressed game first.";return;}if(!File.Exists(Path.Combine(bios,"scph101.bin"))){status="Import your PS1 BIOS first.";return;}Start(game,false);}

  else if(selected==1||selected==2){scanPicking=false;selectBios=selected==2;browsing=true;Browse("");}

  else if(selected==3)Start(Path.Combine(resources,"homebrew.exe"),true);

  else if(selected==4){soundEnabled=!soundEnabled;SaveOptions();}

  else if(selected==5)TogglePadMode();

  else if(selected==6){diagnostics=true;if(logs.Count==0&&File.Exists(logPath))logs.AddRange(File.ReadAllLines(logPath));}

  else if(selected==7)BeginControllerTest();

  else if(selected==8){artworkDiagnostics=true;}

  else if(selected==9||selected==10){settingsPage=selected==9?1:2;selected=0;status="";}

  else if(selected==11){exportPicking=true;scanPicking=selectBios=false;browsing=true;Browse("");}

  else if(selected==12){OpenNetwork();}

  else Exit();

 }

 void Browse(string path){try{var list=new List<string>{""};if(path.Length==0){foreach(var storage in Tizen.System.StorageManager.Storages){var root=storage.RootDirectory;if(Directory.Exists(root)&&!list.Contains(root))list.Add(root);}if(list.Count==1)status="No accessible storage found. Reconnect USB and try again.";}

   else{if(exportPicking)list.Add("@export");else if(scanPicking)list.Add("@scan");list.AddRange(Directory.GetDirectories(path).OrderBy(x=>x));var extensions=selectBios?new[]{".bin",".rom"}:new[]{".cue",".bin",".img",".iso",".exe"};if(!exportPicking)list.AddRange(Directory.GetFiles(path).Where(x=>extensions.Contains(Path.GetExtension(x).ToLowerInvariant())).OrderBy(x=>x));}

   folder=path;entries.Clear();entries.AddRange(list);selected=0;

  }catch(Exception e){status="Cannot browse: "+e.Message;if(entries.Count==0)entries.Add("");}}

 void Back(){if(folder.Length==0){browsing=false;scanPicking=exportPicking=false;selected=0;}else{var parent=Directory.GetParent(folder);Browse(parent==null?"":parent.FullName);}}

 void Start(string path,bool demo){bool safer=nextLaunchSafer;nextLaunchSafer=false;bool nativeCompleted=false;CancelNetworkForGameplay();if(!demo)BackupCard(path);try{RestoreGlobalPerformance();editGame="";if(!demo)PerGameOptions.Load(data,path,options);if(safer)options=SettingsCatalog.SaferCopy(options);activeSessionPath=path;if(image==IntPtr.Zero)image=Screen.evas_object_image_filled_add(Screen.evas_object_evas_get(win.Handle));if(image==IntPtr.Zero)throw new Exception("Video surface unavailable");}catch(Exception e){status=e.Message;return;}

  VideoFramePool sessionFrames=null;if(options.BufferedVideo)try{sessionFrames=new VideoFramePool();}catch(Exception e){Log("Buffered allocation failed; using compatible path: "+e.Message);}

  Volatile.Write(ref activeVideoFrames,sessionFrames);Action bufferedPresent=()=>PresentBuffered(sessionFrames);

  launcherView.Hide();label.Layer=110;label.Show();pw=ph=0;Screen.evas_object_move(image,240,0);Screen.evas_object_resize(image,1440,1080);Screen.evas_object_layer_set(image,100);stop=false;gameExitReason="";gameKeyLogs=0;playing=true;ResetMenuInput();eventPad.Reset();BeginGameplayTrace();benchmark=null;benchmarkLogTime=0;engineStatus="CPU: initializing";ResetWorkMetrics();keyInput.ClearHeld();keys=0;frames=0;presented=0;displaySkipped=displayReplaced=statFrames=statPresented=controlReads=staleReads=lastControlPacket=0;statTime=0;worstPadGapMs=0;lastPadTick=0;comboStart=-1;padReported=false;padHoldStart=Stopwatch.GetTimestamp();previousPad=0;pad=new PadSnapshot();audioClock=0;clock.Restart();lock(logs){logs.Clear();File.WriteAllText(logPath,"");}label.Text="<font_size=36><color=#ffffff>Loading game...<br/>Remote Play/Pause stops and saves. Back is ignored with native input.</color></font_size>";Log("TizenPSX 0.3.39 | "+Path.GetFileName(path)+" | "+(options.Dynarec?"ARM dynarec requested":"interpreter")+" | "+(dualShock?"DualShock":"Digital"));Log("VIDEO PIPELINE: "+VideoPipeline);Log("Settings: "+options.Serialize().Replace("\n"," | "));PollController();

  new Thread(()=>{Sound audio=null;try{if(soundEnabled)try{audio=new Sound(options.BufferMs,options.AdaptiveAudio);Volatile.Write(ref activeSound,audio);Volatile.Write(ref audioClock,audio.UsesAudioClock?1:0);Log("Audio: 44100 Hz stereo; "+(options.AdaptiveAudio?"adaptive output with timer pacing":"original audio-led timing")+"; requested "+options.BufferMs+" ms buffer");}catch(Exception e){Log(e.Message);}

   Native.Report report=p=>{try{ReceiveNativeReport(Marshal.PtrToStringAnsi(p).Trim().Replace("\n"," | "));}catch{}};

   Native.AcquireVideo acquire=sessionFrames==null?(Native.AcquireVideo)null:(w,h)=>{try{return stop?IntPtr.Zero:sessionFrames.Acquire((int)w,(int)h);}catch(Exception e){RequestGameStop("Video acquire: "+e.Message);return IntPtr.Zero;}};

   Native.Video video=(p,w,h)=>{try{if(sessionFrames!=null)PublishBuffered(sessionFrames,p,(int)w,(int)h,bufferedPresent);else Frame(p,(int)w,(int)h);}catch(Exception e){RequestGameStop("Video callback: "+e.Message);}};

   Native.Audio sound=(p,n)=>{try{audio?.Push(p,n);}catch(Exception e){Log(e.Message);Volatile.Write(ref audioClock,0);audio?.Dispose();audio=null;Volatile.Write(ref activeSound,null);}};

   Native.Control input=()=>{try{return Controls();}catch(Exception e){RequestGameStop("Input callback: "+e.Message);return 0x10000UL;}};

   string card=Path.Combine(data,"cards",Native.Hash(System.Text.Encoding.UTF8.GetBytes(path)).Substring(0,20)+".mcr");

   MarkExperimentalSession();

   Native.Run(resources,path,demo?"":bios,card,report,video,sound,input,dualShock,BenchmarkOptions.CoreFlags(options),RecordBenchmark,acquire,twoPlayerNative?(Native.Control)(()=>{try{return TwoPlayerControls(1);}catch(Exception e){RequestGameStop("Player 2 input: "+e.Message);return 0;}}):null);

   nativeCompleted=true;

  }catch(Exception e){RequestGameStop("Game/core failure: "+e.GetType().Name+": "+e.Message);}finally{Log("GAME END: "+(gameExitReason.Length==0?"Native player returned without managed stop request":gameExitReason));Log("Final core input: 0x"+Interlocked.Read(ref lastControlPacket).ToString("X16")+" | "+PadSnapshot.Names((uint)Interlocked.Read(ref lastControlPacket)&65535));Volatile.Write(ref audioClock,0);if(audio!=null)Log(audio.Summary);audio?.Dispose();Volatile.Write(ref activeSound,null);Log(WorkSummary());clock.Stop();Log("Video callbacks: "+frames+" | elapsed: "+clock.Elapsed.TotalSeconds.ToString("F1")+" s");EcoreMainloop.PostAndWakeUp(()=>{Log("Frames submitted: "+presented+" | skipped: "+displaySkipped+" | replaced pending: "+displayReplaced);Log("Input reads: "+controlReads+" | stale connected samples: "+staleReads+" | worst poll gap: "+worstPadGapMs.ToString("F1")+" ms");Log(DirectDiagnostics);playing=false;FinishBufferedVideo(sessionFrames);RecordSessionEnd(nativeCompleted,safer);RestoreGlobalPerformance();if(!demo)BackupAfterGame(path);ResetMenuInput();SaveGameplayTrace();keyInput.ClearHeld();Interlocked.Exchange(ref keys,0);lock(frameLock){pending=false;frame=null;}Screen.evas_object_hide(image);label.Layer=LauncherView.TextLayer;label.Show();status="Game stopped: "+(gameExitReason.Length==0?"native player returned":gameExitReason)+". See Details & stats on this game page.";selected=0;if(!demo&&detailGame!=null)ShowGamePage();else ShowLibrary();});}}){IsBackground=true}.Start();

 }

 ulong Controls(){
  if(twoPlayerNative)return TwoPlayerControls(0);

  if(stop)return 0x10000UL;

  var p=Volatile.Read(ref pad);

  Interlocked.Increment(ref controlReads);

  bool fresh=nativePollingArmed&&p.Connected&&(Stopwatch.GetTimestamp()-p.Timestamp)/(double)Stopwatch.Frequency<1.25;

  if(p.Connected&&!fresh)Interlocked.Increment(ref staleReads);

  ulong packet=ControllerProtocol.Packet(p,(uint)Volatile.Read(ref keys),fresh,dualShock,Volatile.Read(ref audioClock)!=0,options);

  uint directTaps=directFaces.TakePresses();if(fresh&&options.UsePad)packet|=directTaps;

  Interlocked.Exchange(ref lastControlPacket,unchecked((long)packet));TraceCorePacket(packet,fresh);

  if((packet&12)==12){if(comboStart<0)comboStart=clock.Elapsed.TotalSeconds;if(clock.Elapsed.TotalSeconds-comboStart>=1){RequestGameStop("Select+Start held: native="+PadSnapshot.Names(p.Bits)+" key="+PadSnapshot.Names((uint)Volatile.Read(ref keys))+" fresh="+fresh);return 0x10000UL;}}else comboStart=-1;

  return packet;

 }

 readonly EventPadReader eventPad=new EventPadReader();

 readonly GamePadState[] pollStates=new GamePadState[2];

 readonly bool[] pollRead=new bool[2];

 GamePadState ReadPad(int index){

  eventPad.Profile=(ControllerProfile)options.NativeProfile;

  if(!pollRead[index]){pollRead[index]=true;pollStates[index]=default(GamePadState);long readStart=Stopwatch.GetTimestamp();try{pollStates[index]=controllerTest&&nativeTest&&!mappingPage?ProbeState(index):playing?ReadGameplayPad(index):eventPad.Read(index);}catch(Exception e){if(lastPadError!=e.Message)Log("Controller read error: "+e.Message);lastPadError=e.Message;padStats.Error(e.Message);}finally{Interlocked.Add(ref pollWork,Stopwatch.GetTimestamp()-readStart);Interlocked.Increment(ref pollWorkCount);}}

  return pollStates[index];

 }

 static readonly PlayerIndex[] Slots={PlayerIndex.One,PlayerIndex.Two};

 uint ButtonBits(GamePadState s){

  uint bits=0;var b=s.Buttons;var d=s.DPad;

  if(b.A==ButtonState.Pressed)bits|=1u<<0;if(b.B==ButtonState.Pressed)bits|=1u<<8;

  if(b.X==ButtonState.Pressed)bits|=1u<<1;if(b.Y==ButtonState.Pressed)bits|=1u<<9;

  if(b.Back==ButtonState.Pressed)bits|=1u<<2;if(b.Start==ButtonState.Pressed)bits|=1u<<3;

  if(d.Up==ButtonState.Pressed)bits|=1u<<4;if(d.Down==ButtonState.Pressed)bits|=1u<<5;

  if(d.Left==ButtonState.Pressed)bits|=1u<<6;if(d.Right==ButtonState.Pressed)bits|=1u<<7;

  if(b.LeftShoulder==ButtonState.Pressed)bits|=1u<<10;if(b.RightShoulder==ButtonState.Pressed)bits|=1u<<11;

  if(s.Triggers.Left>options.TriggerLevel)bits|=1u<<12;if(s.Triggers.Right>options.TriggerLevel)bits|=1u<<13;

  if(b.LeftStick==ButtonState.Pressed)bits|=1u<<14;if(b.RightStick==ButtonState.Pressed)bits|=1u<<15;

  return bits;

 }

 bool Active(GamePadState s){

  return ButtonBits(s)!=0||Math.Abs(s.ThumbSticks.Left.X)>.35||Math.Abs(s.ThumbSticks.Left.Y)>.35||Math.Abs(s.ThumbSticks.Right.X)>.35||Math.Abs(s.ThumbSticks.Right.Y)>.35;

 }

 void PollController(){
  if(TwoPlayerPolling){PollTwoPlayers();return;}nativePads.Enabled=false;

  if(!ShouldPollController){menuNavigation.Reset();Volatile.Write(ref pad,new PadSnapshot());return;}

  if(controllerTest&&nativeTest&&!mappingPage&&!nativeProbe.Take(KeyTime)){if(nativeProbe.Stopped)try{FlushNativeTrace();}catch(Exception e){padStats.Error(e.Message);}return;}

  long now=Stopwatch.GetTimestamp();

  if(lastPadTick!=0){double gap=(now-lastPadTick)*1000.0/Stopwatch.Frequency;if(gap>worstPadGapMs)worstPadGapMs=gap;}

  lastPadTick=now;

  try{

   int chosen=0;GamePadState state=default(GamePadState);

   // Cache each read within a poll: diagnostics must not consume a second driver sample.

   Array.Clear(pollRead,0,pollRead.Length);

   bool scan=++scanCounter%30==1;

   if(controllerTest&&nativeTest){state=ReadPad(nativeProbe.Slot-1);if(state.IsConnected)chosen=nativeProbe.Slot;padStats.Slots="Only slot "+nativeProbe.Slot+" sampled";}

   else if(options.Slot>0){state=ReadPad(options.Slot-1);if(state.IsConnected)chosen=options.Slot;}

   else{

    if(padSlot>0){state=ReadPad(padSlot-1);if(state.IsConnected)chosen=padSlot;}

    if(chosen==0||scan){for(int i=0;i<Slots.Length;i++){var candidate=ReadPad(i);if(!candidate.IsConnected)continue;if(chosen==0||(Active(candidate)&&!Active(state))){chosen=i+1;state=candidate;}}}

   }

   if(chosen!=padSlot){padSlot=chosen;Log(chosen==0?"Controller disconnected":"Controller on Tizen player slot "+chosen);padReported=chosen!=0;}

   else if(chosen>0&&!padReported){Log("Controller active on slot "+chosen);padReported=true;}

   var l=state.ThumbSticks.Left;var r=state.ThumbSticks.Right;

   uint directPresses=PumpDirectFaces(chosen);

   uint raw=directFaces.Merge(chosen!=0?ButtonBits(state):0,customMap);

   if(NativeMenuPolling){NavigateNativeMenu(chosen!=0,raw|directPresses|directFaces.Merge(chosen!=0?ButtonBits(eventPad.Pressed(chosen-1)):0,customMap),l.X,l.Y);return;}

   var current=options.Map(chosen!=0,raw,l.X,l.Y,r.X,r.Y,now,chosen,!dualShock);

   if(controllerTest)padStats.Observe(now/(double)Stopwatch.Frequency,chosen!=0,raw,current.Bits,l.X,l.Y,r.X,r.Y,state.Triggers.Left,state.Triggers.Right);

   Volatile.Write(ref pad,current);

   if(current.Bits!=previousPad){padHoldStart=now;previousPad=current.Bits;}



  }catch(Exception e){

   // A transient firmware exception must not disable controller polling permanently.

   DisableDirectFaces();Volatile.Write(ref pad,new PadSnapshot());padSlot=0;padStats.Error(e.Message);

   if(e.Message!=lastPadError||++padPolls%120==0){lastPadError=e.Message;Log("Controller poll error (will retry): "+e.Message);}



  }

 }

 void Frame(IntPtr pixels,int w,int h){

  if(stop||w<1||h<1||w>1024||h>512)return;

  Interlocked.Increment(ref frames);long started=Stopwatch.GetTimestamp();

  try{lock(frameLock){Interlocked.Add(ref copyLockWork,Stopwatch.GetTimestamp()-started);

   if(frame==null||frame.Length!=w*h)frame=new int[w*h];Marshal.Copy(pixels,frame,0,frame.Length);fw=w;fh=h;

   if(pending){Interlocked.Increment(ref displayReplaced);return;}pending=true;

  }EcoreMainloop.PostAndWakeUp(Present);}finally{Interlocked.Add(ref copyWork,Stopwatch.GetTimestamp()-started);}

 }

 void Present(){long started=Stopwatch.GetTimestamp();try{if(!EcoreMainloop.IsMainThread)throw new Exception("Presentation called off UI thread");lock(frameLock){pending=false;if(!playing||frame==null)return;if(pw!=fw||ph!=fh){Screen.evas_object_image_size_set(image,fw,fh);pw=fw;ph=fh;}var pin=GCHandle.Alloc(frame,GCHandleType.Pinned);try{Screen.evas_object_image_data_copy_set(image,pin.AddrOfPinnedObject());}finally{pin.Free();}Screen.evas_object_image_data_update_add(image,0,0,fw,fh);Screen.evas_object_show(image);if(options.Overlay==0)label.Hide();Interlocked.Increment(ref presented);if(presented==1)Log("First frame submitted to display: "+fw+" x "+fh);}}catch(Exception e){RequestGameStop("Presentation failed: "+e.Message);}finally{Interlocked.Add(ref presentWork,Stopwatch.GetTimestamp()-started);}}

}

