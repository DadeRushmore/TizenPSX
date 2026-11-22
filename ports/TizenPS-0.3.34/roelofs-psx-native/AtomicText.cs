using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// Inline checksum and a previous complete copy protect preferences from process termination.
internal static class AtomicText {
 const string Prefix="# TizenPS checksum=";
 static string Hash(string text){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
 static string Decode(string text){if(text.Length==0||text.Length<Prefix.Length&&Prefix.StartsWith(text,StringComparison.Ordinal)||text.StartsWith("# TizenPS",StringComparison.Ordinal)&&!text.StartsWith(Prefix,StringComparison.Ordinal))throw new IOException("Incomplete preferences header");if(!text.StartsWith(Prefix,StringComparison.Ordinal))return text;int end=text.IndexOf('\n');if(end<0)throw new IOException("Incomplete preferences");string body=text.Substring(end+1);if(text.Substring(Prefix.Length,end-Prefix.Length).TrimEnd('\r')!=Hash(body))throw new IOException("Preferences checksum mismatch");return body;}
 internal static string Read(string path){try{return Decode(File.ReadAllText(path));}catch(Exception original)when(original is IOException||original is UnauthorizedAccessException){if(File.Exists(path+".previous"))return Decode(File.ReadAllText(path+".previous"));throw;}}
 internal static bool Exists(string path)=>File.Exists(path)||File.Exists(path+".previous");
 internal static void Write(string path,string text){
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temp=path+".writing";
  byte[] bytes=Encoding.UTF8.GetBytes(Prefix+Hash(text)+"\n"+text);
  using(var output=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
  if(!File.Exists(path)){File.Move(temp,path);return;}
  // Do not replace the known-good previous copy with a corrupt current file.
  bool currentValid=true;try{Decode(File.ReadAllText(path));}catch(IOException){currentValid=false;}
  if(currentValid){try{File.Replace(temp,path,path+".previous",true);return;}catch(PlatformNotSupportedException){}catch(IOException){}
   using(var source=File.OpenRead(path))using(var backup=new FileStream(path+".previous",FileMode.Create,FileAccess.Write,FileShare.None)){source.CopyTo(backup);backup.Flush(true);}
  }
  // Fallback runtimes can recover the previous complete copy if killed in this small gap.
  File.Delete(path);File.Move(temp,path);
 }
 internal static void Remove(string path){if(File.Exists(path))File.Delete(path);if(File.Exists(path+".previous"))File.Delete(path+".previous");}
}
