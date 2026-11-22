using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ElmSharp;

// A small retained Evas scene: only the visible covers are decoded. No web runtime.
sealed partial class LauncherView {
 const string E="libevas.so.1";
 [DllImport(E)] static extern void evas_object_image_file_set(IntPtr o,string file,string key);
 [DllImport(E)] static extern void evas_object_image_load_size_set(IntPtr o,int w,int h);
 [DllImport(E)] static extern void evas_object_image_size_get(IntPtr o,out int w,out int h);
 [DllImport(E)] static extern void evas_object_color_set(IntPtr o,int r,int g,int b,int a);
 [DllImport(E)] static extern void evas_object_raise(IntPtr o);
 [DllImport(E)] static extern int evas_object_image_load_error_get(IntPtr o);
 [DllImport(E)] static extern IntPtr evas_load_error_str(int error);
 // Raw canvas images must sit above the Elementary window, with labels above images.
 // A negative image layer can be obscured by the window's own themed surface.
 internal const short ArtworkLayer=10,TextLayer=20;
 readonly Dictionary<string,string> artworkNotes=new Dictionary<string,string>();
 readonly Dictionary<string,bool> artworkFailed=new Dictionary<string,bool>();
 string report="Artwork has not been drawn yet.";
 public string ArtworkDiagnostics { get { return report; } }
 readonly Window win;readonly string res;readonly List<Label> labels=new List<Label>();readonly List<IntPtr> images=new List<IntPtr>();readonly List<string> paths=new List<string>();readonly List<string> decodeSizes=new List<string>();int li,ii;
 public LauncherView(Window w,string r){win=w;res=r;}
 public bool GridView {get;set;}
 public void InvalidateArtwork(){for(int n=0;n<paths.Count;n++)paths[n]="";artworkNotes.Clear();artworkFailed.Clear();}
 public void Hide(){ResetCarousel();foreach(var l in labels)l.Hide();for(int n=0;n<images.Count;n++){Screen.evas_object_hide(images[n]);evas_object_image_file_set(images[n],null,null);paths[n]="";}artworkNotes.Clear();artworkFailed.Clear();}
 IntPtr slideTimer;
 double slideStart, slideOffset;
 static double SlideClock => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
 double RemainingSlide(){
  double t=Math.Max(0,1-(SlideClock-slideStart)/.22);
  return slideOffset*t*t*t;
 }
 internal void FinishSlide(){if(slideTimer!=IntPtr.Zero)finishSlide?.Invoke();ResetCarousel();}
 Action finishSlide;
 partial void StopSlide(){
  finishSlide=null;
  if(slideTimer!=IntPtr.Zero){EcoreMainloop.RemoveTimer(slideTimer);slideTimer=IntPtr.Zero;}
  slideOffset=0;
 }
 partial void AnimateSlide(int step,Action<double> draw){
  double start=step+RemainingSlide();
  StopSlide();
  slideOffset=Math.Max(-1.5,Math.Min(1.5,start));slideStart=SlideClock;
  draw(slideOffset);
  if(Math.Abs(slideOffset)<.002)return;
  finishSlide=()=>draw(0);
  slideTimer=EcoreMainloop.AddTimer(1.0/60,()=>{
   double remaining=RemainingSlide();
   if(Math.Abs(remaining)<.002){draw(0);slideTimer=IntPtr.Zero;slideOffset=0;finishSlide=null;return false;}
   draw(remaining);return true;
  });
 }
 partial void AlignLastLabel(){
  var label=labels[li-1];label.Text="<align=center>"+label.Text+"</align>";
 }
 void ReleaseImage(int n){Screen.evas_object_hide(images[n]);evas_object_image_file_set(images[n],null,null);paths[n]="";}
 void ToneLast(int r,int g,int b,int a){if(ii>0)evas_object_color_set(images[ii-1],r,g,b,a);}
 static string Safe(string s){return (s??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");}
 void Text(string text,int x,int y,int w,int h,int size,string color="#ffffff"){
  Label l;if(li==labels.Count){l=new Label(win){LineWrapType=WrapType.Mixed};labels.Add(l);}else l=labels[li];li++;
  l.Geometry=new Rect(x,y,w,h);l.Text="<font_size="+size+"><color="+color+">"+Safe(text).Replace("\n","<br/>")+"</color></font_size>";l.Show();l.Layer=TextLayer;
 }
 void Picture(string path,int x,int y,int w,int h,bool contain=false,int decodeWidth=0,int decodeHeight=0){
  IntPtr o;if(ii==images.Count){o=Screen.evas_object_image_filled_add(Screen.evas_object_evas_get(win.Handle));images.Add(o);paths.Add("");decodeSizes.Add("");}else o=images[ii];
  if(o==IntPtr.Zero)throw new InvalidOperationException("Cannot create an artwork surface.");
  int loadW=decodeWidth>0?decodeWidth:Math.Min(1920,Math.Max(48,w)),loadH=decodeHeight>0?decodeHeight:Math.Min(1080,Math.Max(48,h));string decodeKey=loadW+"x"+loadH;
  if(paths[ii]!=path||decodeSizes[ii]!=decodeKey||!artworkNotes.ContainsKey(path)){
   evas_object_image_load_size_set(o,loadW,loadH);evas_object_image_file_set(o,path,null);paths[ii]=path;decodeSizes[ii]=decodeKey;
   int iw,ih;evas_object_image_size_get(o,out iw,out ih);int error=evas_object_image_load_error_get(o);
   bool failed=!File.Exists(path)||error!=0||iw<=0||ih<=0;artworkFailed[path]=failed;
   artworkNotes[path]=Path.GetFileName(path)+": "+(!File.Exists(path)?"FILE MISSING":error!=0?"LOAD ERROR "+error+" - "+Marshal.PtrToStringAnsi(evas_load_error_str(error)):iw+" x "+ih+(failed?" - INVALID SIZE":" - OK"));
   if(failed){string fallback=Path.Combine(res,"ui",w>=1920?"background.png":"missing.png");if(path!=fallback)evas_object_image_file_set(o,fallback,null);}
  }ii++;
  if(contain){int iw,ih;evas_object_image_size_get(o,out iw,out ih);if(iw>0&&ih>0){double scale=Math.Min(w/(double)iw,h/(double)ih);int nw=(int)(iw*scale),nh=(int)(ih*scale);x+=(w-nw)/2;y+=(h-nh)/2;w=nw;h=nh;}}
  Screen.evas_object_layer_set(o,ArtworkLayer);Screen.evas_object_move(o,x,y);Screen.evas_object_resize(o,w,h);evas_object_color_set(o,255,255,255,255);Screen.evas_object_show(o);
  // Re-establish painter order on every draw, including when pooled slots change roles.
  evas_object_raise(o);
 }
 string Cover(LibraryGame g){
  if(!string.IsNullOrEmpty(g.CoverPath))return g.CoverPath;
  var bundled=Path.Combine(res,"covers",g.Id+".png");if(File.Exists(bundled))return bundled;var demo=Path.Combine(res,"demo-covers",g.Id+".png");return File.Exists(demo)?demo:Path.Combine(res,"ui","missing.png");
 }
}
