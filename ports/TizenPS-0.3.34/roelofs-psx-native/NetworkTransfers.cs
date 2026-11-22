using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElmSharp;

partial class PlayerApp {
 string pcAddress="0.0.0.0",pcCode="000000",pcToken="",networkProgress="Start the PC companion, then choose Find PC.";
 int pcPort=8731,transferGroupIndex,usbTargetIndex,networkDigit;bool automaticPcBackups=true,networkBusy,networkEdit,networkEditAddress;
 string networkDigits="";CancellationTokenSource networkCancel;TransferManifest pcLibrary;
 readonly List<string> usbTargets=new List<string>();
 string NetworkPrefs=>Path.Combine(data,"network-settings.txt");
 string BackupPending=>Path.Combine(data,"backup-pending.flag");
 void InitializeNetwork(){
  try{foreach(var line in AtomicText.Read(NetworkPrefs).Split('\n')){var pair=line.Split('=');if(pair.Length!=2)continue;switch(pair[0]){case "address":if(IPAddress.TryParse(pair[1],out var ip)&&ip.AddressFamily==AddressFamily.InterNetwork)pcAddress=ip.ToString();break;case "port":if(int.TryParse(pair[1],out int port)&&port>0&&port<65536)pcPort=port;break;case "token":if(TransferFiles.ValidHash(pair[1]))pcToken=pair[1];break;case "automatic":automaticPcBackups=pair[1]=="1";break;}}}catch{}
  try{SnapshotAllCards();}catch(Exception e){Log("Backup startup: "+e.Message);}
  EcoreMainloop.AddTimer(6,()=>{if(!playing&&!networkBusy&&automaticPcBackups&&pcToken.Length>0&&File.Exists(BackupPending))SendBackups(true);return false;});
 }
 void SaveNetworkPrefs(){AtomicText.Write(NetworkPrefs,"address="+pcAddress+"\nport="+pcPort+"\ntoken="+pcToken+"\nautomatic="+(automaticPcBackups?1:0));}
 void OpenNetwork(){launcher=false;settingsPage=4;selected=0;RefreshUsbTargets();Draw();}
 void RefreshUsbTargets(){
  usbTargets.Clear();try{usbTargets.AddRange(AutoLibrary.FindFolders(Tizen.System.StorageManager.Storages.Select(s=>s.RootDirectory),()=>false).Where(p=>p.StartsWith("/opt/media/",StringComparison.Ordinal)));}catch(Exception e){networkProgress="USB discovery: "+e.Message;}
  usbTargetIndex=Math.Min(usbTargetIndex,Math.Max(0,usbTargets.Count-1));
 }
 string SelectedUsb=>usbTargets.Count>0?usbTargets[usbTargetIndex]:"";
 TransferGroup SelectedTransferGroup=>pcLibrary?.Groups?.Count>0?pcLibrary.Groups[transferGroupIndex%pcLibrary.Groups.Count]:null;
 string[] NetworkChoices()=>new[]{
  "PC address: "+pcAddress+":"+pcPort+" (OK edits IPv4)",
  "Pairing code: "+pcCode+" (OK edits; shown on PC)",
  "Find PC automatically",
  pcToken.Length>0?"Connect / refresh PC library":"Pair with PC and load library",
  "USB destination: "+(SelectedUsb.Length>0?SelectedUsb:"No PSX folder found — reconnect USB")+" (OK cycles)",
  "PC game folder: "+(SelectedTransferGroup?.Name??"Connect to PC first")+" (Left/Right or OK cycles)",
  "Import selected folder to USB",
  "Send diagnostics to PC",
  "Automatic PC save backups: "+(automaticPcBackups?"ON":"OFF")+" (local backups always ON)",
  "Back up saves/settings to PC now",
  networkBusy?"Cancel transfer":"Back to settings"
 };
 string NetworkHelp()=>"PC companion: run Start TizenPS Companion.cmd, then open http://localhost:8731 on the PC.<br/>Both devices must be on the same home network. Pair with the six-digit PC code once.<br/>Downloads resume after interruption and verify checksums. Existing game files are preserved.<br/>USB must already contain a PSX folder; the app checks write access before import.<br/>TV Back cancels a transfer; partial downloads are kept for retry.<br/>"+Esc(networkProgress)+(networkBusy?"<br/>WORKING — TV Back cancels":"");
 void ActivateNetworkOption(){
  if(networkBusy){networkCancel?.Cancel();return;}
  switch(selected){
   case 0:networkEdit=networkEditAddress=true;networkDigit=0;networkDigits=string.Concat(pcAddress.Split('.').Select(p=>int.Parse(p).ToString("D3")));return;
   case 1:networkEdit=true;networkEditAddress=false;networkDigit=0;networkDigits=pcCode;return;
   case 2:RunNetwork("Looking for the PC companion…",DiscoverPc);return;
   case 3:RunNetwork("Connecting to PC…",ConnectPc);return;
   case 4:RefreshUsbTargets();if(usbTargets.Count>1)usbTargetIndex=(usbTargetIndex+1)%usbTargets.Count;return;
   case 5:CycleTransferGroup(1);return;
   case 6:ImportSelectedFolder();return;
   case 7:SendDiagnostics();return;
   case 8:automaticPcBackups=!automaticPcBackups;SaveNetworkPrefs();return;
   case 9:SendBackups(false);return;
   case 10:ShowOptionsPage(10,4);return;
  }
 }
 void CycleTransferGroup(int step){if(pcLibrary?.Groups?.Count>0)transferGroupIndex=(transferGroupIndex+step+pcLibrary.Groups.Count)%pcLibrary.Groups.Count;Draw();}
 string NetworkEditText(){
  var parts=new List<string>();for(int i=0;i<networkDigits.Length;i++){if(networkEditAddress&&i>0&&i%3==0)parts.Add(".");parts.Add(i==networkDigit?"["+networkDigits[i]+"]":networkDigits[i].ToString());}
  return (networkEditAddress?"ENTER PC IPv4 ADDRESS":"ENTER SIX-DIGIT PAIRING CODE")+"<br/><br/><font_size=48>"+string.Join(" ",parts)+"</font_size><br/><br/>Remote Left/Right chooses a digit. Up/Down changes it.<br/>Number keys also work. OK saves; Back cancels.<br/><br/>"+Esc(networkProgress);
 }
 void NetworkEditKey(string key,bool down){
  if(!down)return;
  if(KeyInput.Reserved(key)){networkEdit=false;Draw();return;}
  if(key=="Left")networkDigit=(networkDigit+networkDigits.Length-1)%networkDigits.Length;
  else if(key=="Right")networkDigit=(networkDigit+1)%networkDigits.Length;
  else if(key=="Up"||key=="Down"||key.Length==1&&char.IsDigit(key[0])){
   char[] digits=networkDigits.ToCharArray();digits[networkDigit]=key.Length==1?key[0]:(char)('0'+(digits[networkDigit]-'0'+(key=="Up"?1:9))%10);networkDigits=new string(digits);if(key.Length==1)networkDigit=(networkDigit+1)%networkDigits.Length;
  }else if(key=="Return"||key=="KP_Enter"){
   if(networkEditAddress){int[] octets=Enumerable.Range(0,4).Select(i=>int.Parse(networkDigits.Substring(i*3,3))).ToArray();if(octets.Any(v=>v>255)){networkProgress="Each IPv4 group must be 0–255.";Draw();return;}string address=string.Join(".",octets);if(address!=pcAddress)pcToken="";pcAddress=address;}
   else{pcCode=networkDigits;pcToken="";}
   networkEdit=false;SaveNetworkPrefs();networkProgress="Saved. Choose Pair / Connect next.";
  }Draw();
 }
 void ProgressNetwork(string text){EcoreMainloop.PostAndWakeUp(()=>{networkProgress=text;if(settingsPage==4&&!playing)Draw();});}
 async void RunNetwork(string initial,Func<CancellationToken,Task> operation,bool automatic=false){
  if(networkBusy)return;networkBusy=true;networkProgress=initial;var cancel=new CancellationTokenSource();networkCancel=cancel;if(settingsPage==4)Draw();
  string result="",failure="";
  try{await Task.Run(async()=>{await operation(cancel.Token).ConfigureAwait(false);}).ConfigureAwait(false);}
  catch(OperationCanceledException){result="Transfer cancelled. Partial downloads and local backups are preserved.";}
  catch(Exception e){failure=e.Message;result=(automatic?"Local saves are backed up; PC sync pending. ":"Network: ")+e.Message;}
  finally{EcoreMainloop.PostAndWakeUp(()=>{networkBusy=false;networkCancel=null;cancel.Dispose();if(result.Length>0)networkProgress=result;if(!automatic&&failure.Length>0)status=result;if(settingsPage==4&&!playing)Draw();});}
 }
 async Task DiscoverPc(CancellationToken cancel){
  using(var udp=new UdpClient()){udp.EnableBroadcast=true;byte[] request=Encoding.ASCII.GetBytes("TIZENPS_DISCOVER_V1");await udp.SendAsync(request,request.Length,new IPEndPoint(IPAddress.Broadcast,8732)).ConfigureAwait(false);
   var receive=udp.ReceiveAsync();var completed=await Task.WhenAny(receive,Task.Delay(3500,cancel)).ConfigureAwait(false);cancel.ThrowIfCancellationRequested();if(completed!=receive)throw new IOException("PC not found. Start the companion and allow it on the private network, or enter its IP manually.");
   var reply=await receive.ConfigureAwait(false);int port=CompanionClient.DiscoveryPort(reply.Buffer);
   string address=reply.RemoteEndPoint.Address.ToString();if(address!=pcAddress||port!=pcPort)pcToken="";pcAddress=address;pcPort=port;SaveNetworkPrefs();ProgressNetwork("PC found: "+address+". Enter the pairing code from its dashboard, then Pair / Connect.");
  }
 }
 async Task ConnectPc(CancellationToken cancel){
  if(pcToken.Length==0){using(var client=new CompanionClient(pcAddress,pcPort,""))pcToken=await client.Pair(pcCode,cancel).ConfigureAwait(false);SaveNetworkPrefs();}
  using(var client=new CompanionClient(pcAddress,pcPort,pcToken))pcLibrary=await client.Library(cancel).ConfigureAwait(false);
  transferGroupIndex=0;ProgressNetwork("Connected. "+pcLibrary.Groups.Count+" game folders available. Choose a folder, then Import.");
 }
 void ImportSelectedFolder(){
  var group=SelectedTransferGroup;string root=SelectedUsb;if(group==null||root.Length==0){networkProgress="Connect to PC and select an existing USB PSX folder first.";return;}
  RunNetwork("Checking USB write access…",async cancel=>{
   string check=Path.Combine(root,".tizenps-writecheck-"+Guid.NewGuid().ToString("N"));try{File.WriteAllText(check,"TizenPS");}finally{if(File.Exists(check))File.Delete(check);}
   using(var client=new CompanionClient(pcAddress,pcPort,pcToken))await client.Download(root,group,ProgressNetwork,cancel).ConfigureAwait(false);
   ProgressNetwork("Imported "+group.Name+". Return to the library to link it automatically.");
   EcoreMainloop.PostAndWakeUp(()=>{if(!playing){launcher=true;settingsPage=0;ScanFolder(root);}});
  });
 }
 string CardPath(string path)=>Path.Combine(data,"cards",Native.Hash(Encoding.UTF8.GetBytes(path)).Substring(0,20)+".mcr");
 void BackupCard(string path){try{string card=CardPath(path);if(SaveBackups.Snapshot(card,Path.Combine(data,"backups","cards",Path.GetFileNameWithoutExtension(card)),true)!=null)File.WriteAllText(BackupPending,"pending");}catch(Exception e){Log("Local save backup: "+e.Message);}}
 void SnapshotAllCards(){string cards=Path.Combine(data,"cards");if(!Directory.Exists(cards))return;foreach(string card in Directory.GetFiles(cards,"*.mcr")){try{if(SaveBackups.Snapshot(card,Path.Combine(data,"backups","cards",Path.GetFileNameWithoutExtension(card)),true)!=null)File.WriteAllText(BackupPending,"pending");}catch(Exception e){Log("Backup: "+e.Message);}}}
 void BackupAfterGame(string path){BackupCard(path);if(automaticPcBackups&&pcToken.Length>0)SendBackups(true);}
 void CancelNetworkForGameplay(){networkCancel?.Cancel();}
 string BuildBackupArchive(){
  SnapshotAllCards();string file=Path.Combine(data,"backup-upload-"+Guid.NewGuid().ToString("N")+".zip");
  using(var archive=ZipFile.Open(file,ZipArchiveMode.Create)){
   string backups=Path.Combine(data,"backups");if(Directory.Exists(backups))foreach(var path in Directory.GetFiles(backups,"*.mcr",SearchOption.AllDirectories))archive.CreateEntryFromFile(path,"backups/"+path.Substring(backups.Length+1).Replace('\\','/'));
   string cards=Path.Combine(data,"cards");if(Directory.Exists(cards))foreach(var path in Directory.GetFiles(cards,"*.mcr"))if(new FileInfo(path).Length==131072)archive.CreateEntryFromFile(path,"cards/"+Path.GetFileName(path));
   foreach(var name in new[]{"player-options.txt","controller-map.txt","pad-mode.txt","key-bindings.txt","library.tsv"}){string path=Path.Combine(data,name);if(File.Exists(path))archive.CreateEntryFromFile(path,"settings/"+name);if(File.Exists(path+".previous"))archive.CreateEntryFromFile(path+".previous","settings/"+name+".previous");}
   foreach(string group in new[]{"game-options","game-history"}){string location=Path.Combine(data,group);if(Directory.Exists(location))foreach(var path in Directory.GetFiles(location).Where(p=>p.EndsWith(".txt",StringComparison.Ordinal)||p.EndsWith(".txt.previous",StringComparison.Ordinal)))archive.CreateEntryFromFile(path,"settings/"+group+"/"+Path.GetFileName(path));}
  }return file;
 }
 void SendBackups(bool automatic){
  if(networkBusy||playing)return;
  if(pcToken.Length==0){if(!automatic)networkProgress="Pair with the PC first. Local memory-card backups are already enabled.";return;}
  RunNetwork("Sending save backups to PC…",async cancel=>{string file=null;try{file=BuildBackupArchive();using(var client=new CompanionClient(pcAddress,pcPort,pcToken)){var receipt=await client.Upload("backups",file,cancel).ConfigureAwait(false);if(File.Exists(BackupPending))File.Delete(BackupPending);ProgressNetwork("PC backup verified: "+receipt.Saved);}}finally{if(file!=null&&File.Exists(file))File.Delete(file);}},automatic);
 }
 void SendDiagnostics(){
  if(pcToken.Length==0){networkProgress="Pair with the PC first.";return;}
  string report="TizenPSX 0.3.39 network diagnostics\n"+DateTime.UtcNow.ToString("O")+"\nOptions:\n"+options.Serialize()+"\nCustom mapping:\n"+customMap.Serialize()+"\nController:\n"+ControllerReport();
  RunNetwork("Sending diagnostics to PC…",async cancel=>{string file=Path.Combine(data,"diagnostic-upload-"+Guid.NewGuid().ToString("N")+".txt");try{
   var text=new StringBuilder(report);text.AppendLine(HardwareReport.Capture());foreach(var path in new[]{logPath,GameplayTracePath,NativeTracePath})if(File.Exists(path)){text.AppendLine("\n"+Path.GetFileName(path));string content=File.ReadAllText(path);text.Append(content.Length>2000000?content.Substring(content.Length-2000000):content);}
   File.WriteAllText(file,text.ToString());using(var client=new CompanionClient(pcAddress,pcPort,pcToken)){var receipt=await client.Upload("diagnostics",file,cancel).ConfigureAwait(false);ProgressNetwork("Diagnostics received on PC: "+receipt.Saved);}
  }finally{if(File.Exists(file))File.Delete(file);}});
 }
 void OpenSelectedGameSettings(){if(detailGame==null){var games=VisibleGames();if(games.Count>0)detailGame=games[Math.Max(0,Math.Min(libraryIndex,games.Count-1))];}OpenGameOptions();}
}
