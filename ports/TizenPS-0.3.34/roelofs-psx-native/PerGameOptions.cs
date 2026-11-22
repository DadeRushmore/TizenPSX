using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal static class PerGameOptions {
 internal static string FileName(string data,string game){
  string identity=AutoLibrary.RelativeGame(game)??game.Replace('\\','/');
  using(var hash=SHA256.Create())return Path.Combine(data,"game-options",BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","").ToLowerInvariant()+".txt");
 }
 internal static void Copy(PlayerOptions from,PlayerOptions to){
  to.FrameSkip=from.FrameSkip;to.CpuClock=from.CpuClock;to.Interpolation=from.Interpolation;to.Reverb=from.Reverb;
  to.AdaptiveAudio=from.AdaptiveAudio;to.Dynarec=from.Dynarec;to.EnhancedResolution=from.EnhancedResolution;
  to.BufferedVideo=from.BufferedVideo;to.Sound=from.Sound;to.Buffer=from.Buffer;to.Presentation=from.Presentation;to.Overlay=from.Overlay;to.InputTrace=from.InputTrace;
 }
 internal static bool Load(string data,string game,PlayerOptions target){string path=FileName(data,game);if(!AtomicText.Exists(path))return false;Copy(PlayerOptions.Parse(AtomicText.Read(path)),target);return true;}
 internal static void Save(string data,string game,PlayerOptions options){string path=FileName(data,game);Directory.CreateDirectory(Path.GetDirectoryName(path));AtomicText.Write(path,options.Serialize());}
}
