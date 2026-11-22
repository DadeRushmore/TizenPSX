using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using ElmSharp;

partial class PlayerApp {
 bool autoScanBusy,autoScanReported;
 void BeginAutoLibraryScan(bool requested=false){
  if(autoScanBusy||scanning||playing||paused||controllerTest||audioTesting||browsing||!launcher)return;
  autoScanBusy=true;scanning=true;
  if(requested||!autoScanReported){status="Looking for PSX on connected storage…";Draw();}
  var known=library.Games.Where(g=>g.Path.Length>0).Select(g=>g.Path).ToArray();
  new Thread(()=>{
   var files=new List<string>();var suppressed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int skipped=0;bool complete=true;string error="";
   var states=new Dictionary<string,LibraryFileState>(StringComparer.Ordinal);
   try{
    var roots=Tizen.System.StorageManager.Storages.Select(s=>s.RootDirectory).ToList();
    // Saved USB locations also help when firmware only exposes a broad media root.
    foreach(var path in known){var dir=Path.GetDirectoryName(path);while(!string.IsNullOrEmpty(dir)){if(Path.GetFileName(dir).Equals("PSX",StringComparison.OrdinalIgnoreCase)){roots.Add(dir);break;}dir=Path.GetDirectoryName(dir);}}
    foreach(var root in AutoLibrary.FindFolders(roots,()=>playing||paused)){
     if(playing||paused)break;
     var scan=GameLibrary.ScanDetailed(root,()=>playing||paused);files.AddRange(scan.Files);suppressed.UnionWith(scan.SuppressedFiles);skipped+=scan.Skipped;complete&=scan.Complete;
    }
    states=GameLibrary.InspectFiles(files.Concat(known),()=>playing||paused);
    foreach(var pair in states)pair.Value.Suppressed=suppressed.Contains(Path.GetFullPath(pair.Key));
   }catch(Exception e){error=e.Message;complete=false;}
   EcoreMainloop.PostAndWakeUp(()=>{
    autoScanBusy=false;scanning=false;
    if(playing||paused)return;
    var focused=VisibleGames().ElementAtOrDefault(libraryIndex);
    int linked=0,failed=0;
    Func<string,bool> available=p=>states.ContainsKey(p)&&states[p].Available;
    var relativeCounts=files.Distinct().Select(AutoLibrary.RelativeGame).Where(p=>p!=null).GroupBy(p=>p).ToDictionary(g=>g.Key,g=>g.Count());
    foreach(var file in files.Distinct())try{if(available(file)&&AutoLibrary.Link(library,file,p=>!states.ContainsKey(p)||states[p].Available,PreserveMovedCard,AutoLibrary.RelativeGame(file)==null||relativeCounts[AutoLibrary.RelativeGame(file)]==1))linked++;}catch(Exception e){failed++;Log("Automatic linking: "+e.Message);}
    bool refreshed=library.ApplyFileStates(states);
    if((requested||refreshed)&&launcherView!=null)launcherView.InvalidateArtwork();
    if(library.CleanupKlaymen(available))linked++;
    if(linked>0)SaveLibrary();
    var visible=VisibleGames();int focus=focused==null?-1:visible.IndexOf(focused);libraryIndex=focus>=0?focus:Math.Max(0,Math.Min(libraryIndex,visible.Count-1));
    if(requested||!autoScanReported||linked>0||failed>0||refreshed){
     status=failed>0?"Could not reconnect "+failed+" game(s). See diagnostics.":error.Length>0?"USB scan: "+error:files.Count==0?"Connect a USB drive with your games inside a PSX folder. Detection is automatic.":"USB library ready: "+files.Distinct().Count()+" games found."+(skipped>0?" "+skipped+" unsupported or unreadable items skipped.":"");
     if(!complete)status+=" Scan stopped early; existing library kept. Try a smaller game folder.";
     int offline=library.Games.Count(g=>g.Path.Length>0&&!g.Available);if(offline>0)status+=" "+offline+" offline entries retained.";
    }
    autoScanReported=true;if(launcher&&!browsing)Draw();
   });
  }){IsBackground=true}.Start();
 }
 void PreserveMovedCard(string oldPath,string newPath){
  string oldCard=Path.Combine(data,"cards",Native.Hash(System.Text.Encoding.UTF8.GetBytes(oldPath)).Substring(0,20)+".mcr");
  string newCard=Path.Combine(data,"cards",Native.Hash(System.Text.Encoding.UTF8.GetBytes(newPath)).Substring(0,20)+".mcr");
  if(File.Exists(oldCard)&&!File.Exists(newCard)){Directory.CreateDirectory(Path.GetDirectoryName(newCard));File.Copy(oldCard,newCard,false);}
  // PerGameOptions and GameHistory already key identical relative paths below PSX.
  // Never remove the old card: reconnecting an original drive must remain safe.
 }
}
