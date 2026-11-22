using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Frontend-only model. Paths are kept intact because the existing core keys saves by path.
sealed class LibraryGame {
 public string Id,Title,Genre,Publisher,Year,Region,Description,Path="";
 public string[] Aliases;
 public bool Favourite,Available,Suppressed;
 public string CoverPath="",BackgroundPath="",ArtworkStamp="";
 public long LastPlayed;
}
sealed class GameLibrary {
 public readonly List<LibraryGame> Games=new List<LibraryGame>();
 readonly string statePath;
 public GameLibrary(string catalog,string data){
  statePath=System.IO.Path.Combine(data,"library.tsv");
  foreach(var line in File.ReadAllLines(catalog)){
   var f=line.Split('\t');if(f.Length<8)continue;
   Games.Add(new LibraryGame{Id=f[0],Title=f[1],Genre=f[2],Publisher=f[3],Year=f[4],Region=f[5],Description=f[6],Aliases=f[7].Split('|')});
  }
  if(AtomicText.Exists(statePath))foreach(var line in ReadState(statePath).Split('\n'))try{
   var f=line.Split('\t');if(f.Length<5)continue;
   var path=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(f[1]));
   var g=Games.FirstOrDefault(x=>x.Id==f[0]);if(g==null)g=Games.FirstOrDefault(x=>x.Path.Length==0&&x.Aliases.Any(a=>Normalize(a+".cue")==Normalize(path)));if(g==null){g=Unknown(path);Games.Add(g);}
   if(g.Id.StartsWith("local-"))g.Id=f[0];
   g.Path=path;g.Available=File.Exists(path);g.Favourite=f[2]=="1";long v;g.LastPlayed=long.TryParse(f[3],out v)?v:0;
  }catch{}
 }
 static string ReadState(string path){
  // The previous TSV writer legitimately saved an empty file for an empty library.
  if(File.Exists(path)&&new FileInfo(path).Length==0&&!File.Exists(path+".previous"))return "";
  return AtomicText.Read(path);
 }
 public bool CleanupKlaymen(Func<string,bool> exists){
  // Only consolidate the user's removed Neverhood entry after the requested
  // replacement folder is actually present. Never delete or move game files.
  var live=Games.FirstOrDefault(g=>g.Path.Length>0&&exists(g.Path)&&
   (AutoLibrary.RelativeGame(g.Path)??"").Replace('\\','/').StartsWith("Klaymen Klaymen/",StringComparison.OrdinalIgnoreCase));
  if(live==null)return false;
  var canonical=Games.FirstOrDefault(g=>g.Id=="klaymen")??live;
  bool changed=false;
  if(canonical!=live){canonical.Path=live.Path;canonical.Available=live.Available;canonical.CoverPath=live.CoverPath;canonical.BackgroundPath=live.BackgroundPath;canonical.ArtworkStamp=live.ArtworkStamp;canonical.Favourite|=live.Favourite;canonical.LastPlayed=Math.Max(canonical.LastPlayed,live.LastPlayed);Games.Remove(live);changed=true;}
  foreach(var old in Games.Where(g=>g!=canonical).ToArray()){
   string name=Normalize(old.Title)+" "+Normalize(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(old.Path.Replace('\\','/'))??""));
   bool same=name.Contains("neverhood")||name.Contains("klaymen");
   if(same&&(old.Path.Length==0||old.Path==canonical.Path||!exists(old.Path))){canonical.Favourite|=old.Favourite;canonical.LastPlayed=Math.Max(canonical.LastPlayed,old.LastPlayed);Games.Remove(old);changed=true;}
  }
  return changed;
 }
 public static string Normalize(string value){
  value=System.IO.Path.GetFileNameWithoutExtension(value).ToLowerInvariant();
  value=Regex.Replace(value,@"\([^)]*\)|\[[^\]]*\]", " ");
  value=Regex.Replace(value,@"\b(v\d+(\.\d+)*|disc\s*\d+|track\s*\d+)\b", " ");
  return Regex.Replace(Regex.Replace(value,@"[^a-z0-9]+"," "),@"\s+"," ").Trim();
 }
 LibraryGame Unknown(string path){var title=System.IO.Path.GetFileNameWithoutExtension(path);return new LibraryGame{Id="local-"+Native.Hash(System.Text.Encoding.UTF8.GetBytes(path)).Substring(0,12),Title=title,Genre="PlayStation",Publisher="Local game",Year="",Region="Unknown",Description="Your local PlayStation game. Add a matching cover beside the game as cover.png or cover.jpg.",Aliases=new[]{Normalize(path)}};}
 public LibraryGame Add(string path){
  var name=Normalize(path);var g=Games.FirstOrDefault(x=>x.Path==path)??Games.FirstOrDefault(x=>x.Aliases.Any(a=>Normalize(a+".cue")==name));
  // A second disc or region is a separate entry, never overwrite another bound path.
  if(g!=null&&g.Path.Length>0&&g.Path!=path){g=Unknown(path);Games.Add(g);}
  if(g==null){g=Unknown(path);Games.Add(g);}g.Path=path;g.Available=File.Exists(path);return g;
 }
 public void Save(){
  var text=string.Join("\n",Games.Where(g=>g.Path.Length>0||g.Favourite||g.LastPlayed>0).Select(g=>g.Id+"\t"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(g.Path))+"\t"+(g.Favourite?"1":"0")+"\t"+g.LastPlayed+"\t1"));
  AtomicText.Write(statePath,text);
 }
 public static List<string> Scan(string root,out int skipped){var result=ScanDetailed(root,()=>false);skipped=result.Skipped;return result.Files;}
 public static LibraryScanResult ScanDetailed(string root,Func<bool> cancel){
  var result=new LibraryScanResult();var stack=new Stack<Tuple<string,int>>();stack.Push(Tuple.Create(root,0));
  var candidates=new HashSet<string>(StringComparer.Ordinal);var tracks=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  int directories=0,items=0;
  while(stack.Count>0){
   if(cancel()||directories>=4096||items>=50000){result.Complete=false;break;}
   var next=stack.Pop();directories++;
   try{
    if((File.GetAttributes(next.Item1)&FileAttributes.ReparsePoint)!=0)continue;
    foreach(var entry in Directory.EnumerateFileSystemEntries(next.Item1)){
     if(cancel()||++items>50000){result.Complete=false;break;}
     var attr=File.GetAttributes(entry);if((attr&FileAttributes.ReparsePoint)!=0)continue;
     if((attr&FileAttributes.Directory)!=0){
      string name=System.IO.Path.GetFileName(entry);
      if(name.StartsWith(".")||new[]{"BIOS","covers","backgrounds","artwork","saves"}.Any(n=>n.Equals(name,StringComparison.OrdinalIgnoreCase)))continue;
      if(next.Item2>=32){result.Complete=false;continue;}stack.Push(Tuple.Create(entry,next.Item2+1));continue;
     }
     string ext=System.IO.Path.GetExtension(entry).ToLowerInvariant();
     if(ext==".cue"){
      try{
       if(new FileInfo(entry).Length>262144){result.Skipped++;continue;}
       string cueText=File.ReadAllText(entry);candidates.Add(entry);
       foreach(Match m in Regex.Matches(cueText,@"(?im)^\s*FILE\s+(?:""([^""]+)""|(\S+))\s+")){
        string file=m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value;
        tracks.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(entry),file.Replace('\\',System.IO.Path.DirectorySeparatorChar))));
       }
      }catch(IOException){result.Skipped++;}catch(UnauthorizedAccessException){result.Skipped++;}catch(ArgumentException){result.Skipped++;}
     }else if(new[]{".bin",".img",".iso",".exe"}.Contains(ext)){
      if(ext==".bin"&&(new FileInfo(entry).Length==524288||Regex.IsMatch(System.IO.Path.GetFileName(entry),@"(?i)track\s*0*[2-9]\d*")))continue;
      candidates.Add(entry);
     }else if(new[]{".7z",".zip",".rar",".chd",".m3u",".pbp"}.Contains(ext))result.Skipped++;
    }
   }catch(IOException){result.Skipped++;result.Complete=false;}catch(UnauthorizedAccessException){result.Skipped++;result.Complete=false;}
  }
  // Resolve cue references globally, including tracks stored in another directory.
  result.SuppressedFiles.UnionWith(tracks);
  result.Files.AddRange(candidates.Where(f=>!tracks.Contains(System.IO.Path.GetFullPath(f))).OrderBy(f=>f,StringComparer.OrdinalIgnoreCase));
  return result;
 }
 // Run on a worker: one directory listing per folder, not one listing per game.
 public static Dictionary<string,LibraryFileState> InspectFiles(IEnumerable<string> paths,Func<bool> cancel){
  var result=new Dictionary<string,LibraryFileState>(StringComparer.Ordinal);var folders=new Dictionary<string,Dictionary<string,string>>(StringComparer.Ordinal);
  foreach(string path in paths.Where(p=>!string.IsNullOrEmpty(p)).Distinct()){
   if(cancel())break;var state=new LibraryFileState{Available=File.Exists(path)};result[path]=state;if(!state.Available)continue;
   string dir=System.IO.Path.GetDirectoryName(path);Dictionary<string,string> files;
   if(!folders.TryGetValue(dir,out files)){
    files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);folders[dir]=files;
    try{int count=0;foreach(string entry in Directory.EnumerateFiles(dir)){if(cancel()||++count>50000)break;string ext=System.IO.Path.GetExtension(entry);if(new[]{".jpg",".jpeg",".png"}.Any(e=>e.Equals(ext,StringComparison.OrdinalIgnoreCase))){var info=new FileInfo(entry);if(info.Length>0&&info.Length<=16777216)files[System.IO.Path.GetFileName(entry)]=entry;}}}catch(IOException){}catch(UnauthorizedAccessException){}
   }
   string stem=System.IO.Path.GetFileNameWithoutExtension(path);
   state.CoverPath=Artwork(files,new[]{stem,"cover"});state.BackgroundPath=Artwork(files,new[]{stem+".background","background","fanart"});state.ArtworkStamp=ArtworkStamp(state.CoverPath)+"|"+ArtworkStamp(state.BackgroundPath);
  }
  return result;
 }
 static string ArtworkStamp(string path){if(path.Length==0)return "";try{var info=new FileInfo(path);return path+":"+info.Length+":"+info.LastWriteTimeUtc.Ticks;}catch(IOException){return path;}catch(UnauthorizedAccessException){return path;}}
 static string Artwork(Dictionary<string,string> files,string[] stems){foreach(string stem in stems)foreach(string ext in new[]{".jpg",".jpeg",".png"}){string path;if(files.TryGetValue(stem+ext,out path))return path;}return "";}
 public bool ApplyFileStates(Dictionary<string,LibraryFileState> states){
  bool changed=false;foreach(var game in Games){LibraryFileState state;if(!states.TryGetValue(game.Path,out state))continue;
   if(game.Available!=state.Available||game.Suppressed!=state.Suppressed||game.CoverPath!=state.CoverPath||game.BackgroundPath!=state.BackgroundPath||game.ArtworkStamp!=state.ArtworkStamp)changed=true;
   game.Available=state.Available;game.Suppressed=state.Suppressed;game.CoverPath=state.CoverPath;game.BackgroundPath=state.BackgroundPath;game.ArtworkStamp=state.ArtworkStamp;
  }return changed;
 }
}
sealed class LibraryScanResult {public readonly List<string> Files=new List<string>();public readonly HashSet<string> SuppressedFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);public int Skipped;public bool Complete=true;}
sealed class LibraryFileState {public bool Available,Suppressed;public string CoverPath="",BackgroundPath="",ArtworkStamp="";}
