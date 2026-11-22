using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static class TransferFiles {
 internal static string Target(string root,string relative){
  if(string.IsNullOrWhiteSpace(relative)||relative.Contains('\\')||relative.Contains(':')||relative.Any(c=>char.IsControl(c)))throw new IOException("Invalid transfer path");
  var parts=relative.Split('/');if(parts.Any(p=>p.Length==0||p=="."||p==".."||p.StartsWith(".")))throw new IOException("Invalid transfer path");
  var extensions=new[]{".cue",".bin",".iso",".img",".exe",".png",".jpg",".jpeg"};
  if(!extensions.Contains(Path.GetExtension(relative).ToLowerInvariant())||parts.Any(p=>p.Equals("BIOS",StringComparison.OrdinalIgnoreCase)))throw new IOException("Transfer file excluded");
  string basePath=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  string path=Path.GetFullPath(Path.Combine(basePath,Path.Combine(parts)));
  if(!path.StartsWith(basePath,Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new IOException("Path outside selected USB folder");
  string check=basePath;foreach(var part in parts){check=Path.Combine(check,part);if((File.Exists(check)||Directory.Exists(check))&&(File.GetAttributes(check)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked transfer paths are excluded");}
  return path;
 }
 internal static string Digest(string path){using(var hash=SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
 internal static bool ValidHash(string hash)=>hash!=null&&hash.Length==64&&hash.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
 internal static void Commit(string partial,string target,long size,string digest){
  if(!File.Exists(partial)||new FileInfo(partial).Length!=size||Digest(partial)!=digest)throw new IOException("Download verification failed; partial file kept for retry");
  if(File.Exists(target))throw new IOException("Existing game file was preserved");
  File.Move(partial,target);
 }
}
