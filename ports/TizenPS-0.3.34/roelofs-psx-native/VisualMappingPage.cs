using System;
using System.IO;
using System.Threading;

partial class PlayerApp {
 bool mappingPage,mappingArmed;int mappingSelected;double mappingReady,lastMappingInput=-100;PadMapping customMap;VisualMappingView mappingView;
 string mappingMessage="Choose a PS1 action with the TV remote, then press OK.";
 string MapPath=>Path.Combine(data,"controller-map.txt");
 void LoadControllerMapping(){try{customMap=PadMapping.Parse(AtomicText.Read(MapPath));}catch{customMap=PadMapping.Defaults();}if(customMap.Enabled)options.FaceLayout=0;eventPad.Mapping=customMap;eventPad.RawEventReceived=ObserveMapping;}
 void SaveControllerMapping(){AtomicText.Write(MapPath,customMap.Serialize());if(customMap.Enabled)options.FaceLayout=0;eventPad.Mapping=customMap;}
 uint nativeMappingBits;bool mappingNeutral;readonly uint[] mappingPrevious=new uint[2];
 void BeginVisualMapping(){mappingPage=true;controllerTest=false;mappingArmed=false;mappingSelected=0;if(mappingView==null)mappingView=new VisualMappingView(win);ResetMenuInput();PrepareMappingRemote();Draw();}
 void EndVisualMapping(){SavePhysicalMap();mappingPage=false;mappingArmed=false;mappingView.Hide();settingsPage=10;selected=2;ResetMenuInput();Draw();}
 void MappingKey(string key,bool down,bool native){
  if(native||!down)return;
  if(KeyInput.Reserved(key)){if(mappingArmed){mappingArmed=false;mappingMessage="Assignment cancelled.";}else{EndVisualMapping();return;}}
  else if(!mappingArmed&&(key=="Right"||key=="Down"))mappingSelected=(mappingSelected+1)%18;
  else if(!mappingArmed&&(key=="Left"||key=="Up"))mappingSelected=(mappingSelected+17)%18;
  else if(key=="Return"||key=="KP_Enter"){
   if(mappingSelected<16){mappingArmed=true;mappingNeutral=false;mappingReady=KeyTime+.45;mappingMessage="Release all buttons, then press the PS5 button for "+KeyInput.ButtonNames[mappingSelected]+".";}
   else if(mappingSelected==16){physicalMap.Reset();SavePhysicalMap();mappingMessage="Standard PS5 layout restored.";}
   else {EndVisualMapping();return;}
  }Draw();
 }
 void ObserveMapping(int slot,int type,int code,int value){}
 void PollNativeMapping(){
  uint held=0,pressed=0;PhysicalPadState first=null;
  for(int i=0;i<2;i++){var p=nativePads.Slots.Read(i,System.Diagnostics.Stopwatch.GetTimestamp(),false);if(first==null&&p.Connected)first=p;held|=p.Bits;pressed|=p.Bits&~mappingPrevious[i];if(p.Bits!=mappingPrevious[i])lastMappingInput=KeyTime;mappingPrevious[i]=p.Bits;}
  nativeMappingBits=held;if(held!=0)lastMappingInput=KeyTime;
  if(mappingArmed&&KeyTime>=mappingReady){if(held==0)mappingNeutral=true;int source=DualSenseButtonMap.SinglePress(pressed);if(mappingNeutral&&source>=0){physicalMap.Assign(mappingSelected,source);SavePhysicalMap();mappingArmed=false;mappingMessage="Saved "+KeyInput.ButtonNames[mappingSelected]+" from "+physicalMap.SourceName(mappingSelected)+". Press again to verify.";}}
  if(first!=null)System.Threading.Volatile.Write(ref pad,PadSnapshot.Create(true,physicalMap.Apply(held),first.LX,first.LY,first.RX,first.RY,first.Timestamp,1,false));
  else System.Threading.Volatile.Write(ref pad,new PadSnapshot());
  DrawVisualMapping();
 }
 void DrawVisualMapping(){launcherView.Hide();label.Hide();var p=Volatile.Read(ref pad);mappingView.Draw(physicalMap,mappingSelected,physicalMap.Apply(nativeMappingBits),p.LX,p.LY,p.RX,p.RY,mappingMessage+" | "+nativePads.Slots.Status,mappingArmed);}
 string editGame="";
 string SelectedGamePath(){var games=VisibleGames();return games.Count>0?games[Math.Max(0,Math.Min(libraryIndex,games.Count-1))].Path:game;}
 void RestoreGlobalPerformance(){PerGameOptions.Copy(PlayerOptions.Load(Path.Combine(data,"player-options.txt")),options);}
 void ToggleGameScope(){
  RestoreGlobalPerformance();if(editGame.Length>0){editGame="";status="Editing global audio/video defaults.";}
  else{editGame=SelectedGamePath();if(editGame.Length==0){status="Select a linked game first.";return;}PerGameOptions.Load(data,editGame,options);status="Editing "+Path.GetFileNameWithoutExtension(editGame)+". Changes save for this game.";}
 }
 void SaveScopedOptions(){if(SettingsCatalog.IsScopedPage(settingsPage)&&editGame.Length>0)PerGameOptions.Save(data,editGame,options);else AtomicText.Write(Path.Combine(data,"player-options.txt"),options.Serialize());}
}
