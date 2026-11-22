using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

internal static class AutoLibrary {
 internal static List<string> FindFolders(IEnumerable<string> roots,Func<bool> cancel){
  var found=new HashSet<string>(StringComparer.Ordinal);
  var visited=new HashSet<string>(StringComparer.Ordinal);
  var queue=new Queue<Tuple<string,int>>();
  foreach(var root in roots.Where(r=>!string.IsNullOrEmpty(r)).Distinct())queue.Enqueue(Tuple.Create(root,0));
  int count=0;
  while(queue.Count>0&&count++<512&&!cancel()){
   var item=queue.Dequeue();string path=item.Item1;
   if(!visited.Add(path))continue;
   try{
    if(!Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;
    if(string.Equals(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),"PSX",StringComparison.OrdinalIgnoreCase)){found.Add(path);continue;}
    if(item.Item2>=4)continue;
    foreach(var sub in Directory.EnumerateDirectories(path)){string name=Path.GetFileName(sub);if(name.Equals("PSX",StringComparison.OrdinalIgnoreCase)){if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)==0)found.Add(sub);continue;}if(queue.Count+count<512&&!name.StartsWith(".")&&!new[]{"PSP","SNES","BIOS","covers","backgrounds"}.Any(n=>n.Equals(name,StringComparison.OrdinalIgnoreCase)))queue.Enqueue(Tuple.Create(sub,item.Item2+1));}
   }catch(IOException){}catch(UnauthorizedAccessException){}
  }
  return found.OrderBy(p=>p,StringComparer.Ordinal).ToList();
 }
 internal static string RelativeGame(string path){
  var parts=path.Replace('\\','/').Split('/');
  for(int i=0;i<parts.Length-1;i++)if(parts[i].Equals("PSX",StringComparison.OrdinalIgnoreCase))return string.Join("/",parts.Skip(i+1));
  return null;
 }
 internal static bool Link(GameLibrary library,string path,Func<string,bool> exists,Action<string,string> preserveSave,bool allowReconnect=true){
  if(library.Games.Any(g=>g.Path==path))return false;
  string relative=RelativeGame(path);
  // Only reconnect an identical path below PSX, not a different disc or region.
  var matches=library.Games.Where(g=>g.Path.Length>0&&!exists(g.Path)&&relative!=null&&RelativeGame(g.Path)==relative).ToList();
  if(allowReconnect&&matches.Count==1){var g=matches[0];preserveSave(g.Path,path);g.Path=path;g.Available=true;return true;}
  library.Add(path);return true;
 }
}
