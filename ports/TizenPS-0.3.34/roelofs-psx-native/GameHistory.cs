using System;
using System.Globalization;
using System.IO;
using System.Text;

internal sealed class GameRunRecord {
 internal int Launches;internal string State="Not played yet",Engine="",Reason="";internal double Seconds,Fps,Speed;
 internal string Serialize()=>"launches="+Launches+"\nstate="+Convert.ToBase64String(Encoding.UTF8.GetBytes(State))+"\nengine="+Convert.ToBase64String(Encoding.UTF8.GetBytes(Engine))+"\nreason="+Convert.ToBase64String(Encoding.UTF8.GetBytes(Reason))+"\nseconds="+Seconds.ToString(CultureInfo.InvariantCulture)+"\nfps="+Fps.ToString(CultureInfo.InvariantCulture)+"\nspeed="+Speed.ToString(CultureInfo.InvariantCulture);
 internal static GameRunRecord Parse(string text){var r=new GameRunRecord();foreach(var line in text.Split('\n')){var p=line.Split('=');if(p.Length<2)continue;string value=line.Substring(line.IndexOf('=')+1);try{switch(p[0]){
  case "launches":int.TryParse(value,out r.Launches);break;case "state":r.State=Encoding.UTF8.GetString(Convert.FromBase64String(value));break;case "engine":r.Engine=Encoding.UTF8.GetString(Convert.FromBase64String(value));break;case "reason":r.Reason=Encoding.UTF8.GetString(Convert.FromBase64String(value));break;
  case "seconds":double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out r.Seconds);break;case "fps":double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out r.Fps);break;case "speed":double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out r.Speed);break;
 } }catch(FormatException){} }return r;}
}
internal static class GameHistory {
 internal static string PendingPath(string data)=>Path.Combine(data,"experimental-core.pending");
 internal static void StartSession(string data,string game){Begin(data,game);AtomicText.Write(PendingPath(data),"game="+Convert.ToBase64String(Encoding.UTF8.GetBytes(game)));}
 internal static bool RecoverPending(string data){
  if(!AtomicText.Exists(PendingPath(data)))return false;
  string pending=AtomicText.Read(PendingPath(data));if(pending.StartsWith("game=",StringComparison.Ordinal)){string game=Encoding.UTF8.GetString(Convert.FromBase64String(pending.Substring(5)));Interrupted(data,game);}
  AtomicText.Remove(PendingPath(data));return true;
 }
 internal static string RecordPath(string data,string game)=>Path.Combine(data,"game-history",Path.GetFileName(PerGameOptions.FileName(data,game)));
 internal static GameRunRecord Load(string data,string game){try{return GameRunRecord.Parse(AtomicText.Read(RecordPath(data,game)));}catch{return new GameRunRecord();}}
 internal static void Begin(string data,string game){var record=Load(data,game);record.Launches++;record.State="Running";record.Reason="";record.Seconds=record.Fps=record.Speed=0;record.Engine="";AtomicText.Write(RecordPath(data,game),record.Serialize());}
 internal static void Interrupted(string data,string game){var record=Load(data,game);record.Launches=Math.Max(1,record.Launches);record.State="Unfinished session";record.Reason="The app closed before the session finished. Saved settings were kept.";AtomicText.Write(RecordPath(data,game),record.Serialize());}
 internal static void Finish(string data,string game,bool success,double seconds,double fps,double speed,string engine,string reason){var record=Load(data,game);record.State=success?"Finished":"Stopped with an error";record.Seconds=seconds;record.Fps=fps;record.Speed=speed;record.Engine=engine;record.Reason=reason;AtomicText.Write(RecordPath(data,game),record.Serialize());}
}
