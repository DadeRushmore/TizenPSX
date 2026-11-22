using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

internal sealed class ShellItem {
 internal readonly string Title,Description;internal readonly FriendlySetting? Setting;internal readonly Action Action;
 internal ShellItem(string title,string description,Action action){Title=title;Description=description;Action=action;}
 internal ShellItem(string title,FriendlySetting setting){Title=title;Description="";Setting=setting;}
}

partial class PlayerApp {
 LibraryGame detailGame;bool gamePage,nextLaunchSafer,shellConfirmHeld;double uiReadyAt;string activeSessionPath="";int testReturnPage=15,testReturnSelected;
 int advancedReturnPage=12,advancedReturnFocus=3;
 bool ShellActive=>!playing&&!browsing&&!controllerTest&&!diagnostics&&!artworkDiagnostics&&!mappingPage&&(gamePage||settingsPage>=10);
 bool ScopedGameOptions=>editGame.Length>0;
 PlayerOptions SavedGameOptions(string path){var saved=PlayerOptions.Load(Path.Combine(data,"player-options.txt"));if(path.Length>0)try{PerGameOptions.Load(data,path,saved);}catch(Exception e){status="Could not read saved game settings: "+e.Message;}return saved;}
 void ShowGamePage(LibraryGame selectedGame=null,int focus=0){
  if(selectedGame!=null){detailGame=selectedGame;status="";}if(detailGame==null){ShowLibrary();return;}
  RestoreGlobalPerformance();editGame="";gamePage=true;launcher=false;settingsPage=0;selected=focus;uiReadyAt=KeyTime+.3;ResetMenuInput();Draw();
 }
 void ShowOptionsPage(int page,int focus=0){if(page==13){advancedReturnPage=settingsPage==18?18:12;advancedReturnFocus=settingsPage==18?2:3;}gamePage=false;launcher=false;settingsPage=page;selected=focus;uiReadyAt=KeyTime+.3;ResetMenuInput();Draw();}
 void OpenGlobalOptions(){RestoreGlobalPerformance();editGame="";status="";ShowOptionsPage(10);}
 void OpenGameOptions(){
  if(detailGame==null||detailGame.Path.Length==0){status="Link this game's file from USB first.";Draw();return;}
  RestoreGlobalPerformance();editGame=detailGame.Path;try{PerGameOptions.Load(data,editGame,options);}catch(Exception e){status="Saved setup needs attention: "+e.Message;}ShowOptionsPage(18);
 }
 void OpenControllerPage(){RestoreGlobalPerformance();editGame="";ShowOptionsPage(15);}
 string[] GameActions()=>new[]{"Play game","Game options",detailGame!=null&&detailGame.Favourite?"Remove favourite":"Add favourite","Details & stats","Safer launch — once","Back to covers"};
 void PlayDetailGame(bool safer){
  if(detailGame==null||detailGame.Path.Length==0||!File.Exists(detailGame.Path)){status="Game file is missing. Reconnect USB and choose Refresh library in Options.";Draw();return;}
  if(!File.Exists(Path.Combine(bios,"scph101.bin"))){status="Import your BIOS in Options > Library & files first.";Draw();return;}
  nextLaunchSafer=safer;game=detailGame.Path;File.WriteAllText(Path.Combine(data,"last-game-path.txt"),game);Start(game,false);if(playing){detailGame.LastPlayed=DateTime.UtcNow.Ticks;SaveLibrary();}else Draw();
 }
 void DrawShell(){
  label.Hide();
  if(gamePage){
   selected=Math.Max(0,Math.Min(selected,GameActions().Length-1));var saved=SavedGameOptions(detailGame.Path);var history=GameHistory.Load(data,detailGame.Path);
   string warning=history.State=="Unfinished session"||history.State=="Stopped with an error"?"Previous session did not finish normally. Saved settings kept. You can try Safer launch once.":"";
   if(selected==4)warning="One run with Reliable CPU, Original picture and Compatible delivery. Your saved setup stays unchanged.";
   string stats=history.Launches==0?"No session stats recorded yet":"Launches: "+history.Launches+"  /  Last: "+history.State+(history.Seconds>0?"\n"+history.Seconds.ToString("F0")+" sec  /  "+history.Fps.ToString("F1")+" FPS  /  "+history.Speed.ToString("F0")+"% speed":"");
   launcherView.DrawGame(detailGame,saved,detailGame.Path.Length>0&&AtomicText.Exists(PerGameOptions.FileName(data,detailGame.Path)),warning,GameActions(),selected,stats,status);return;
  }
  if(settingsPage==20){DrawGameStats();return;}
  var items=ShellItems();selected=Math.Max(0,Math.Min(selected,items.Count-1));string scope=ScopedGameOptions?(detailGame?.Title??Path.GetFileNameWithoutExtension(editGame)):"Global options";
  string note=ScopedGameOptions?"THIS GAME ONLY  •  Changes save immediately  •  Other games are unchanged":"DEFAULTS  •  Used by games without a custom setup";
  if(settingsPage==15||settingsPage==16||settingsPage==17||settingsPage==21||settingsPage==22||settingsPage==23)note="TV remote or controller: choose a clear, highlighted row";
  launcherView.DrawMenu(ShellHeading(),scope,note,items,selected,options,status);
 }
 string ShellHeading(){switch(settingsPage){case 11:return "Sound";case 12:return "Picture & performance";case 13:return "Advanced settings";case 14:return "Advanced troubleshooting";case 15:return "Controllers";case 16:return "Troubleshooting";case 24:return "Advanced diagnostics";case 17:return "Library & files";case 18:return "Game options";case 19:return "Confirm reset";case 21:return "Library view & filters";case 22:return "Controller connection";case 23:return "Confirm controller reset";default:return "Options";}}
 ShellItem Go(string title,string description,int page)=>new ShellItem(title,description,()=>ShowOptionsPage(page));
 ShellItem BackItem()=>new ShellItem("Back","Return to the previous page without changing saved settings.",BackShell);
 List<ShellItem> ShellItems(){
  switch(settingsPage){
   case 10:return new List<ShellItem>{Go("Sound","Game sound and audio delay.",11),Go("Picture & performance","Resolution, frame skipping and the speed display.",12),new ShellItem("Controllers","Visual PS5 button mapper. Controllers connect automatically.",BeginVisualMapping),Go("Library & files","Refresh USB games, import BIOS or choose a file.",17),new ShellItem("Network & backups","PC transfers and memory-card backups.",()=>OpenNetwork()),Go("Troubleshooting","Export a report or check your controller and speakers.",16),new ShellItem("Back to games","Return to your collection.",()=>ShowLibrary()),new ShellItem("Library view: "+LibraryViewName,"Switch between a large carousel and an 18-game grid. Your choice is remembered.",ToggleLibraryView),new ShellItem("Exit app","Close TizenPSX.",()=>Exit())};
   case 18:return new List<ShellItem>{Go("Audio","Sound choices saved only for this game.",11),Go("Video","Picture choices saved only for this game.",12),Go("Advanced settings","CPU and compatibility choices saved only for this game.",13),Go("Use global defaults","Remove this game's custom setup. You will be asked to confirm.",19),BackItem()};
   case 11:return new List<ShellItem>{new ShellItem("Sound",FriendlySetting.Sound),new ShellItem("Smooth audio",FriendlySetting.SmoothAudio),new ShellItem("Audio delay",FriendlySetting.AudioDelay),BackItem()};
   case 12:return new List<ShellItem>{new ShellItem("Picture quality",FriendlySetting.Picture),new ShellItem("Frame skipping",FriendlySetting.FrameSkip),new ShellItem("Show speed & FPS",FriendlySetting.Overlay),Go("Advanced settings","CPU compatibility, frame delivery and sound detail.",13),BackItem()};
   case 13:return new List<ShellItem>{new ShellItem("CPU mode",FriendlySetting.CpuMode),new ShellItem("Game CPU speed",FriendlySetting.CpuClock),new ShellItem("Frame delivery",FriendlySetting.VideoPath),new ShellItem("Display updates",FriendlySetting.DisplayFrames),new ShellItem("Sound detail",FriendlySetting.SoundDetail),new ShellItem("Echo effects",FriendlySetting.Echo),BackItem()};
   case 14:return new List<ShellItem>{new ShellItem("Detailed input logging",FriendlySetting.InputTrace),BackItem()};
   case 15:return new List<ShellItem>{new ShellItem("Visual button mapper","PS5 controllers connect automatically. "+nativePads.Slots.Status,BeginVisualMapping),BackItem()};
   case 16:return new List<ShellItem>{new ShellItem("Export report to USB","Save game, controller and system details to send when something goes wrong.",()=>{exportPicking=true;scanPicking=selectBios=false;browsing=true;Browse("");Draw();}),new ShellItem("Last game report","View the most recent game report.",()=>{diagnostics=true;selected=0;lock(logs){logs.Clear();if(File.Exists(logPath))logs.AddRange(File.ReadAllLines(logPath));}Draw();}),Go("Advanced diagnostics","Extra logging, key events and artwork checks.",24),BackItem()};
   case 24:return new List<ShellItem>{new ShellItem("Detailed input logging",FriendlySetting.InputTrace),new ShellItem("Check artwork","Report missing or unreadable cover files.",()=>{artworkDiagnostics=true;Draw();}),new ShellItem("Run built-in test game","Start the bundled display test.",()=>Start(Path.Combine(resources,"homebrew.exe"),true)),BackItem()};
   case 17:return new List<ShellItem>{new ShellItem("Library view: "+LibraryViewName,"Choose carousel or grid. Only the visible covers are loaded.",ToggleLibraryView),Go("Filters & unavailable games","Favourites, recent games and records for disconnected or removed games.",21),new ShellItem("Refresh USB library","Find added games and matching cover/background JPG or PNG files inside PSX folders. Missing games keep their saved records.",()=>{ShowLibrary();BeginAutoLibraryScan(true);}),new ShellItem("Import BIOS","Choose your BIOS file using the file browser.",()=>{scanPicking=false;selectBios=true;browsing=true;Browse("");Draw();}),new ShellItem("Choose a game file manually","Use this if automatic PSX discovery does not find the game.",()=>{scanPicking=selectBios=false;browsing=true;Browse("");Draw();}),BackItem()};
   case 19:return new List<ShellItem>{new ShellItem("Keep this game's setup","Cancel and keep all saved choices.",()=>ShowOptionsPage(18,3)),new ShellItem("Use global defaults for this game","Removes this game's saved override only. Other games and global settings stay unchanged.",()=>{AtomicText.Remove(PerGameOptions.FileName(data,editGame));RestoreGlobalPerformance();status="Custom setup removed. This game now follows global defaults.";ShowOptionsPage(18,3);})};
   case 21:return new List<ShellItem>{new ShellItem("Library view: "+LibraryViewName,"Carousel shows a large selected cover. Grid shows 18 games at once.",ToggleLibraryView),new ShellItem("All games","Show games available on connected storage.",()=>SetLibraryFilter(0)),new ShellItem("Favourites","Show games you marked as favourites.",()=>SetLibraryFilter(1)),new ShellItem("Recently played","Show games in their recent-play order.",()=>SetLibraryFilter(2)),new ShellItem("Unavailable games","Saved records for disconnected or removed files. Reconnect the drive or restore the file and refresh; favourites and saves are kept.",()=>SetLibraryFilter(3)),new ShellItem("Options","Open global app options.",OpenGlobalOptions),new ShellItem("Back to covers","Return without changing the library filter.",()=>ShowLibrary())};
   default:return new List<ShellItem>{BackItem()};
  }
 }
 void LegacyControllerChange(int oldIndex){int page=settingsPage,focus=selected;settingsPage=1;selected=oldIndex;try{ActivateSettingsPage();}finally{settingsPage=page;selected=focus;}}
 void SetLibraryFilter(int filter){libraryTab=filter;libraryIndex=0;status="";ShowLibrary();}
 void BackShell(){status="";switch(settingsPage){case 11:case 12:ShowOptionsPage(ScopedGameOptions?18:10,settingsPage==11?0:1);return;case 13:ShowOptionsPage(advancedReturnPage,advancedReturnFocus);return;case 24:ShowOptionsPage(16,2);return;case 14:ShowOptionsPage(13,3);return;case 19:ShowOptionsPage(18,3);return;case 15:ShowOptionsPage(10,2);return;case 16:ShowOptionsPage(10,5);return;case 17:case 21:ShowOptionsPage(10,3);return;case 18:ShowGamePage(null,1);return;case 20:ShowGamePage(null,3);return;default:ShowLibrary();return;}}
 bool HandleShellKey(string key,bool down,bool native){
  if(!ShellActive)return false;if(!down||KeyTime<uiReadyAt)return true;if(audioTesting){if(KeyInput.Reserved(key))audioTestStop=true;return true;}if(!menuKeyFilter.Accept(key,native,KeyTime))return true;
  try{
   if(KeyInput.Reserved(key)){if(gamePage)ShowLibrary();else BackShell();return true;}
   if(settingsPage==20){if(key=="Return"||key=="KP_Enter")ShowGamePage(null,3);return true;}
   int count=gamePage?GameActions().Length:ShellItems().Count;
   if(key=="Up")selected=(selected+count-1)%count;else if(key=="Down")selected=(selected+1)%count;
   else if(key=="Return"||key=="KP_Enter"||key=="Left"||key=="Right"){
    if(gamePage){if(key=="Left"||key=="Right")return true;switch(selected){case 0:PlayDetailGame(false);break;case 1:OpenGameOptions();break;case 2:detailGame.Favourite=!detailGame.Favourite;SaveLibrary();break;case 3:ShowOptionsPage(20);break;case 4:PlayDetailGame(true);break;case 5:ShowLibrary();break;}}
    else{var item=ShellItems()[selected];if(item.Setting.HasValue){SettingsCatalog.Change(item.Setting.Value,options,key=="Left"?-1:1);SaveScopedOptions();status=ScopedGameOptions?"Saved for "+detailGame.Title+" only. Applies on the next launch.":"Global defaults saved. Games with their own setup keep it.";}else if(key=="Return"||key=="KP_Enter")item.Action?.Invoke();}
   }
  }catch(Exception e){if(SettingsCatalog.IsScopedPage(settingsPage)){RestoreGlobalPerformance();if(editGame.Length>0)try{PerGameOptions.Load(data,editGame,options);}catch{}}status="Could not complete that action: "+e.Message;}
  Draw();return true;
 }
 void DrawGameStats(){
  if(detailGame==null){ShowLibrary();return;}var history=GameHistory.Load(data,detailGame.Path);string size=File.Exists(detailGame.Path)?(new FileInfo(detailGame.Path).Length/1048576.0).ToString("F1",CultureInfo.InvariantCulture)+" MiB":"File not connected";
  string[] lines={"Game ID: "+detailGame.Id+"  /  Region: "+detailGame.Region,"Settings: "+(detailGame.Path.Length>0&&AtomicText.Exists(PerGameOptions.FileName(data,detailGame.Path))?"Custom game setup":"Global defaults"),"Launches: "+history.Launches+"  /  Last result: "+history.State,"Last session: "+history.Seconds.ToString("F0")+" sec  /  "+history.Fps.ToString("F1")+" emulation FPS  /  "+history.Speed.ToString("F0")+"% speed","Last engine: "+history.Engine+"  /  Renderer: ARM NEON software","Game file size: "+size,"Game file: "+(detailGame.Path.Length>0?detailGame.Path:"Not linked"),"Last exit: "+history.Reason};
  launcherView.DrawInfo("Technical details for this game",detailGame.Title,lines,status);
 }
}
