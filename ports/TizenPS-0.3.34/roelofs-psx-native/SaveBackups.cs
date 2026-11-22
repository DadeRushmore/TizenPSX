using System;
using System.IO;
using System.Linq;

internal static class SaveBackups {
 // Backups live in a dedicated app-owned tree; never prune the live cards folder.
 internal static string Snapshot(string source,string directory,bool memoryCard,int keep=10){
  if(!File.Exists(source))return null;
  long size=new FileInfo(source).Length;
  if(memoryCard?size!=131072:size>2*1024*1024)throw new IOException("Unexpected saved file size; backup skipped");
  Directory.CreateDirectory(directory);
  string extension=memoryCard?".mcr":".txt";
  string digest=TransferFiles.Digest(source);
  var existing=Directory.GetFiles(directory,"*"+extension).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
  if(existing.Any(p=>Path.GetFileName(p).Contains("-"+digest.Substring(0,16)+"-")))return existing.First(p=>Path.GetFileName(p).Contains("-"+digest.Substring(0,16)+"-"));
  string path=Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+digest.Substring(0,16)+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+extension);
  string partial=path+".tmp";File.Copy(source,partial,false);
  if(TransferFiles.Digest(partial)!=digest){File.Delete(partial);throw new IOException("Saved file changed during backup; retry later");}
  File.Move(partial,path);
  foreach(var old in Directory.GetFiles(directory,"*"+extension).Where(p=>p!=path).OrderByDescending(File.GetLastWriteTimeUtc).Skip(Math.Max(0,keep-1)))File.Delete(old);
  return path;
 }
}
