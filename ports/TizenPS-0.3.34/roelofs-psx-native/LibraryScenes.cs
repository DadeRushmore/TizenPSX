using System;

using System.Collections.Generic;

using System.IO;

using System.Runtime.InteropServices;



sealed partial class LauncherView {

 void BeginScene(string breadcrumb,LibraryGame game=null,bool carousel=false){
  if(!carousel)ResetCarousel();

  li=ii=0;Picture(Path.Combine(res,"ui","background.png"),0,0,1920,1080);

  if(game!=null){

   if(!string.IsNullOrEmpty(game.BackgroundPath)){Picture(game.BackgroundPath,0,0,1920,1080,false,960,540);ToneLast(72,72,82,255);}

   else {string cover=Cover(game);if(Path.GetFileName(cover)!="missing.png"){Picture(cover,0,-420,1920,1920,false,carousel?640:48,carousel?640:48);ToneLast(carousel?85:35,carousel?82:35,carousel?99:52,carousel?195:115);}}

  }

  Picture(Path.Combine(res,"ui","tizenps-emblem.png"),65,35,85,80,true);

  Picture(Path.Combine(res,"ui","tizenps-wordmark.png"),170,41,260,76,true);

  Text(breadcrumb,465,61,1190,52,27,"#b7accd");Text("0.3.40",1725,64,130,40,22,"#a59bb9");

 }

 void EndScene(){

  var notes=new List<string>();var seen=new HashSet<string>();int failures=0;

  for(int i=0;i<ii;i++)if(seen.Add(paths[i])){if(artworkFailed[paths[i]])failures++;notes.Add(artworkNotes[paths[i]]);}

  report="Visible artwork failures: "+failures+"\n"+string.Join("\n",notes);

  for(int i=li;i<labels.Count;i++)labels[i].Hide();for(int i=ii;i<images.Count;i++)ReleaseImage(i);

  // Keep diagnostics proportional to the visible page, not the whole drive.

  var stale=new List<string>();foreach(var key in artworkNotes.Keys)if(!seen.Contains(key))stale.Add(key);

  foreach(var key in stale){artworkNotes.Remove(key);artworkFailed.Remove(key);}

 }

 public void DrawLibrary(List<LibraryGame> games,int index,int tab,string status,bool scanning){

  index=Math.Max(0,Math.Min(index,Math.Max(0,games.Count-1)));

  BeginScene("LIBRARY  /  "+new[]{"All games","Favourites","Recently played","Unavailable"}[Math.Max(0,Math.Min(tab,3))]+"  /  "+(GridView?"Grid":"Carousel"),games.Count>0?games[index]:null,!GridView&&games.Count>0);

  if(games.Count==0){Text(tab==1?"No favourites available":tab==2?"No recent games available":tab==3?"All linked games are available":"Your collection starts here",190,330,1500,110,62);Text(tab==1?"Open a game page and choose Add favourite. Back opens library options.":tab==2?"Games appear here after you launch them. Back opens library options.":tab==3?"Removed or disconnected games keep their favourites, history and saves.":"Connect your USB drive with games in a PSX folder. Back opens library options.",195,490,1500,100,29,"#c4bad4");}

  else if(GridView){

   int first=LibraryNavigation.PageStart(index),last=Math.Min(first+LibraryNavigation.PageSize,games.Count);

   Text("PAGE "+(first/LibraryNavigation.PageSize+1)+" / "+((games.Count+LibraryNavigation.PageSize-1)/LibraryNavigation.PageSize)+"     "+games.Count+" GAMES",90,157,1600,42,22,"#c0a4ef");

   for(int n=first;n<last;n++){

    int col=(n-first)%LibraryNavigation.Columns,row=(n-first)/LibraryNavigation.Columns,x=95+col*293,y=220+row*240;

    if(n==index)Picture(Path.Combine(res,"ui","cover-focus.png"),x-9,y-9,218,218);

    Picture(Cover(games[n]),x,y,200,200,true);

    Text(games[n].Title.Length>25?games[n].Title.Substring(0,24)+"…":games[n].Title,x-20,y+204,265,32,18,n==index?"#ffffff":"#c8c3d6");

   }

   Text(games[index].Title,90,950,1660,48,31);

   Text((index+1)+" / "+games.Count,1730,956,130,38,22,"#c0a4ef");

  }

  else{

   DrawCarousel(games,index,tab,status,scanning);
   EndScene();
   return;
  }
  Text(scanning?"Refreshing USB library...":status.Length>0?status:GridView?"D-pad: browse games     OK: open game     Back: view, filters & options":"Left / Right: browse covers     OK: open game     Down: grid     Up: filters     Back: options",70,1015,1780,52,22,"#c1b6d0");EndScene();

 }

 LibraryGame previousSelection;
 int previousIndex,previousCount,previousTab;
 partial void AnimateSlide(int step,Action<double> draw);
 partial void StopSlide();
 partial void AlignLastLabel();
 void ResetCarousel(){StopSlide();previousSelection=null;}
 void Center(string title,int y,int height,int size,string color){
  Text(title,180,y,1560,height,size,color);
  AlignLastLabel();
 }
 void Band(int x,int y,int w,int h,int r,int g,int b,int a){
  Picture(Path.Combine(res,"ui","surface.png"),x,y,w,h,false,8,8);
  ToneLast(r,g,b,a);
 }
 void DrawCarousel(List<LibraryGame> games,int index,int tab,string status,bool scanning){
  var selected=games[index];
  int step=0;
  bool sameList=previousCount==games.Count&&previousTab==tab&&previousSelection!=null
      &&object.ReferenceEquals(previousSelection,games[previousIndex]);
  if(sameList){
   if(index==(previousIndex+1)%games.Count&&index!=previousIndex)step=1;
   else if(index==(previousIndex+games.Count-1)%games.Count&&index!=previousIndex)step=-1;
  }
  if(!sameList||Math.Abs(index-previousIndex)>1&&games.Count<7||step==0&&index!=previousIndex)StopSlide();
  if(games.Count<7)step=sameList&&Math.Abs(index-previousIndex)==1?index-previousIndex:0;
  previousSelection=selected;previousIndex=index;previousTab=tab;previousCount=games.Count;

  Band(0,154,1920,194,12,13,25,194);
  Center(selected.Title,186,133,selected.Title.Length>52?42:56,"#f5f2ff");
  Band(0,881,1920,199,10,12,22,222);
  Center((index+1).ToString("00")+"   /   "+games.Count.ToString("00")+"     •     OK opens game details",899,52,25,"#ddd6ec");
  Center("Left / Right   Browse       •       Down   Grid       •       Up   Filters       •       Back   Options",966,48,24,"#cdc3df");
  Center(scanning?"Refreshing USB library…":tab==3?"Reconnect the drive or check the game file":status,1021,50,19,"#b5afc5");

  // Decode each visible case at a fixed size. Animation only changes its geometry.
  int first=games.Count>=7?-3:Math.Max(-3,-index);
  int last=games.Count>=7?3:Math.Min(3,games.Count-1-index);
  string[] covers=new string[last-first+1];
  for(int d=first;d<=last;d++)covers[d-first]=Cover(games[(index+d+games.Count)%games.Count]);
  int startImage=ii;
  Action<double> draw=offset=>{
   ii=startImage;
   for(int d=first;d<=last;d++){
    double distance=d+offset,focus=Math.Max(0,1-Math.Abs(distance));
    int size=(int)(236+156*focus),x=(int)(960+distance*348-size/2),y=600-size/2;
    int border=d==0?5:2;
    if(d==0)Band(x-border,y-border,size+2*border,size+2*border,190,162,251,255);
    else Band(x-border,y-border,size+2*border,size+2*border,54,52,71,255);
    Picture(covers[d-first],x,y,size,size,true,512,512);
    int shade=(int)(183+72*focus);ToneLast(shade,shade,shade,255);
   }
  };
  draw(0);
  AnimateSlide(step,draw);
 }
 public void DrawGame(LibraryGame g,PlayerOptions saved,bool custom,string warning,string[] actions,int selected,string stats,string status){

  BeginScene("LIBRARY  /  GAME",g);

  Picture(Path.Combine(res,"ui","hero-frame.png"),70,200,550,550);Picture(Cover(g),91,218,508,508,true);

  Text(g.Title,700,160,1110,125,g.Title.Length>40?40:49);

  Text(g.Genre+"  /  "+g.Region,705,287,1000,44,24,"#bcb1cc");

  Text(custom?"CUSTOM SETUP — SAVED FOR THIS GAME":"USING GLOBAL DEFAULTS",705,350,650,65,25,custom?"#8fe1bd":"#c0a6ee");

  string summary="CPU: "+SettingsCatalog.Value(FriendlySetting.CpuMode,saved)+"\nPicture: "+SettingsCatalog.Value(FriendlySetting.Picture,saved)+"\nSound: "+(saved.Sound?"On":"Off")+"  /  "+saved.BufferMs+" ms\nFrame skipping: "+SettingsCatalog.Value(FriendlySetting.FrameSkip,saved)+"\nSpeed display: "+SettingsCatalog.Value(FriendlySetting.Overlay,saved)+"\nFrame delivery: "+(saved.BufferedVideo?"Buffered":"Compatible");

  Text(summary,705,435,610,240,27,"#e2daed");Text(stats,705,704,620,100,23,"#b2c7d4");

  for(int i=0;i<actions.Length;i++){int y=370+i*70;if(i==selected)Picture(Path.Combine(res,"ui","button-focus.png"),1340,y-8,490,65);Text((i==selected?">  ":"   ")+actions[i],1360,y,460,55,27,i==selected?"#ffffff":"#c2b6d0");}

  Text("GAME FILE",85,838,1600,34,19,"#a99abd");Text(g.Path.Length>0?g.Path:"No game file linked — reconnect USB and refresh the library.",85,880,1730,80,24,"#c8bdd7");

  Text(status.Length>0?status:warning.Length>0?warning:"Up / Down: choose action     OK: select     Back: covers",80,1000,1760,65,22,warning.Length>0?"#ffcc8e":"#bfb2cf");EndScene();

 }

 public void DrawMenu(string heading,string scope,string scopeNote,List<ShellItem> items,int selected,PlayerOptions options,string status){

  BeginScene("OPTIONS  /  "+heading.ToUpperInvariant());

  Text(scope,80,155,1730,90,scope.Length>50?34:41);

  Text(scopeNote,84,252,1730,50,24,"#8edabb");

  Text((selected+1)+" / "+items.Count,1710,255,140,38,22,"#b8a5cf");

  int first=Math.Max(0,Math.Min(selected-3,items.Count-7));

  for(int i=first;i<Math.Min(first+7,items.Count);i++){int y=340+(i-first)*78;var item=items[i];string value=item.Setting.HasValue?SettingsCatalog.Value(item.Setting.Value,options):"";if(i==selected)Picture(Path.Combine(res,"ui","button-focus.png"),75,y-12,1070,73);Text((i==selected?">  ":"   ")+item.Title+(value.Length>0?": "+value:""),95,y,1030,65,28,i==selected?"#ffffff":"#bdb0d0");}

  var active=items[selected];Text("ABOUT THIS OPTION",1230,333,580,50,22,"#c4a8f4");Text(active.Setting.HasValue?SettingsCatalog.Explanation(active.Setting.Value):active.Description,1230,411,590,400,28,"#dcd3e7");

  Text(status,85,910,1750,75,24,"#a6ddc8");Text("Up / Down: choose     Left / Right: change value     OK: select / change     Back: previous page",80,1005,1770,55,22,"#bdb0cc");EndScene();

 }

 public void DrawInfo(string title,string scope,string[] lines,string status){

  BeginScene("GAME  /  DETAILS & STATS");Text(scope,80,160,1720,100,40);Text(title,85,273,1710,55,28,"#baa7db");

  for(int i=0;i<lines.Length;i++)Text(lines[i],90,355+i*75,1720,70,25,i%2==0?"#ded5e7":"#b8c9d6");

  Text(status.Length>0?status:"Back or OK: return to this game",80,1005,1760,55,23,"#c0b1d0");EndScene();

 }

}

