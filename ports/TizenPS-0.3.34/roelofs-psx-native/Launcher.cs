using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using ElmSharp;

partial class PlayerApp {
 GameLibrary library;LauncherView launcherView;bool launcher=true,scanPicking,scanning;int libraryIndex,libraryTab;
 string LibraryViewName=>launcherView!=null&&launcherView.GridView?"Grid":"Carousel";
 void ToggleLibraryView(){
  launcherView.GridView=!launcherView.GridView;
  try{AtomicText.Write(Path.Combine(data,"library-view.txt"),launcherView.GridView?"grid":"carousel");}catch(Exception e){status="Cannot save library view: "+e.Message;}
  ShowLibrary();
 }
 List<LibraryGame> VisibleGames(){return library.Games.Where(g=>!g.Suppressed&&(libraryTab==3?g.Path.Length>0&&!g.Available:g.Available&&(libraryTab==1?g.Favourite:libraryTab==2?g.LastPlayed>0:true))).OrderBy(g=>libraryTab==2?-g.LastPlayed:0).ThenBy(g=>g.Title,StringComparer.OrdinalIgnoreCase).ToList();}
 void InitializeLauncher(){
  library=new GameLibrary(Path.Combine(resources,"catalog.tsv"),data);
  if(game.Length>0&&File.Exists(game)&&!library.Games.Any(g=>g.Path==game)){library.Add(game);SaveLibrary();}
  if(library.CleanupKlaymen(File.Exists))SaveLibrary();
  launcherView=new LauncherView(win,resources);
  try{launcherView.GridView=AtomicText.Exists(Path.Combine(data,"library-view.txt"))&&AtomicText.Read(Path.Combine(data,"library-view.txt")).Trim()=="grid";}catch(Exception e){status="Library view reset: "+e.Message;}
  int last=VisibleGames().FindIndex(g=>g.Path==game);if(last>=0)libraryIndex=last;
  EcoreMainloop.AddTimer(1,()=>{BeginAutoLibraryScan();return false;});
  EcoreMainloop.AddTimer(60,()=>{BeginAutoLibraryScan();return true;});
  EcoreMainloop.AddTimer(30,()=>{if(launcher&&!playing&&!browsing&&!controllerTest&&!diagnostics)Draw();return true;});
 }
 void SaveLibrary(){try{library.Save();}catch(Exception e){status="Cannot save library: "+e.Message;}}
 void ShowLibrary(){RestoreGlobalPerformance();editGame="";if(libraryTab>3)libraryTab=0;if(detailGame!=null){int focus=VisibleGames().FindIndex(g=>g.Id==detailGame.Id);if(focus>=0)libraryIndex=focus;}detailGame=null;gamePage=false;launcher=true;settingsPage=0;selected=0;uiReadyAt=KeyTime+.3;ResetMenuInput();label.Hide();Draw();}
 void LauncherKey(string k){
  if(KeyTime<uiReadyAt)return;
  var games=VisibleGames();if(games.Count>0)libraryIndex=Math.Max(0,Math.Min(libraryIndex,games.Count-1));
  if(k=="Left"||k=="Right"||(launcherView.GridView&&(k=="Up"||k=="Down"))){libraryIndex=LibraryNavigation.Move(libraryIndex,games.Count,k,launcherView.GridView);status="";}
  else if(k=="Down"){ToggleLibraryView();return;}
  else if(k=="Up"){RestoreGlobalPerformance();editGame="";ShowOptionsPage(21);return;}
  else if(KeyInput.Reserved(k)){OpenGlobalOptions();return;}
  else if(k=="Return"||k=="KP_Enter"||k=="p"||k=="P"){if(games.Count>0)ShowGamePage(games[libraryIndex]);return;}
  Draw();
 }
 void ScanFolder(string root){
  if(scanning)return;
  browsing=false;scanPicking=false;scanning=true;launcher=true;status="Scanning games and artwork…";Draw();
  var known=library.Games.Where(g=>g.Path.Length>0).Select(g=>g.Path).ToArray();
  new Thread(()=>{
   LibraryScanResult result=null;Dictionary<string,LibraryFileState> states=null;string error="";
   try{
    result=GameLibrary.ScanDetailed(root,()=>playing||paused);
    states=GameLibrary.InspectFiles(known.Concat(result.Files),()=>playing||paused);
    foreach(var pair in states)if(result.SuppressedFiles.Contains(Path.GetFullPath(pair.Key)))pair.Value.Suppressed=true;
   }catch(Exception e){error=e.Message;}
   EcoreMainloop.PostAndWakeUp(()=>{
    scanning=false;if(playing||paused)return;
    if(result!=null&&states!=null){
     var previous=VisibleGames();string focus=previous.Count>0?previous[Math.Max(0,Math.Min(libraryIndex,previous.Count-1))].Id:null;
     int failed=0;var relativeCounts=result.Files.Select(AutoLibrary.RelativeGame).Where(p=>p!=null).GroupBy(p=>p).ToDictionary(g=>g.Key,g=>g.Count());
     foreach(var file in result.Files)try{LibraryFileState state;if(!states.TryGetValue(file,out state)||!state.Available)continue;string relative=AutoLibrary.RelativeGame(file);AutoLibrary.Link(library,file,p=>!states.ContainsKey(p)||states[p].Available,PreserveMovedCard,relative!=null&&relativeCounts[relative]==1);}catch(Exception){failed++;}
     if(library.ApplyFileStates(states))launcherView.InvalidateArtwork();library.CleanupKlaymen(File.Exists);SaveLibrary();
     status="Found "+result.Files.Count+" games."+(failed>0?" Could not link "+failed+" games.":"")+(result.Skipped>0?" "+result.Skipped+" unsupported or unreadable items skipped.":"")+(!result.Complete?" Scan incomplete; use a smaller folder or refresh again.":"");
     var visible=VisibleGames();int next=visible.FindIndex(g=>g.Id==focus);libraryIndex=next>=0?next:Math.Max(0,Math.Min(libraryIndex,visible.Count-1));
    }else status="Scan failed: "+error;
    Draw();
   });
  }){IsBackground=true}.Start();
 }
}
