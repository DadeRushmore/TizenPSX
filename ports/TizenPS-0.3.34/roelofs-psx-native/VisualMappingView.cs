using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ElmSharp;

// Retained native canvas, matching the launcher rendering path; no web view.
sealed class VisualMappingView {
 readonly Window win;readonly List<Label> labels=new List<Label>();int used;
 [DllImport("libevas.so.1")]static extern IntPtr evas_object_rectangle_add(IntPtr e);
 [DllImport("libevas.so.1")]static extern IntPtr evas_object_polygon_add(IntPtr e);
 [DllImport("libevas.so.1")]static extern void evas_object_polygon_point_add(IntPtr o,int x,int y);
 [DllImport("libevas.so.1")]static extern void evas_object_color_set(IntPtr o,int r,int g,int b,int a);
 readonly List<IntPtr> panels=new List<IntPtr>();
 internal VisualMappingView(Window w){win=w;var p=evas_object_polygon_add(Screen.evas_object_evas_get(win.Handle));int[] points={180,300,220,270,420,270,490,310,1000,310,1070,270,1270,270,1310,300,1370,700,1250,830,1130,800,1000,680,490,680,370,800,240,830,120,700};for(int i=0;i<points.Length;i+=2)evas_object_polygon_point_add(p,points[i],points[i+1]);Screen.evas_object_layer_set(p,25);evas_object_color_set(p,28,34,63,255);panels.Add(p);}
 void Panel(int x,int y,int w,int h){var p=evas_object_rectangle_add(Screen.evas_object_evas_get(win.Handle));Screen.evas_object_move(p,x,y);Screen.evas_object_resize(p,w,h);Screen.evas_object_layer_set(p,25);evas_object_color_set(p,28,34,63,255);panels.Add(p);}
 void Text(string t,int x,int y,int w,int h,int size,string color){Label l;if(used==labels.Count){l=new Label(win){LineWrapType=WrapType.Mixed};labels.Add(l);}else l=labels[used];used++;l.Geometry=new Rect(x,y,w,h);l.Layer=30;l.Text="<font_size="+size+"><color="+color+">"+t.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\n","<br/>")+"</color></font_size>";l.Show();}
 internal void Hide(){foreach(var l in labels)l.Hide();foreach(var p in panels)Screen.evas_object_hide(p);}
 internal void Draw(DualSenseButtonMap map,int selected,uint bits,float lx,float ly,float rx,float ry,string message,bool armed){
  used=0;foreach(var p in panels)Screen.evas_object_show(p);
  Text("CONTROLLER MAPPING",70,35,1300,60,42,"#ffffff");
  Text("TV remote: arrows select · OK assigns · Back cancels / leaves",70,100,1780,48,26,"#b7c7ef");
  int[] xs={1100,980,650,850,340,340,190,490,1220,1100,240,1050,240,1050,450,890};
  int[] ys={550,445,440,440,345,545,445,445,445,345,225,225,155,155,680,680};
  for(int i=0;i<16;i++)Text((selected==i?"> ":"")+KeyInput.ButtonNames[i]+((bits&(1u<<i))!=0?" ●":""),xs[i],ys[i],180,85,29,(bits&(1u<<i))!=0?"#64ffae":selected==i?"#ffe07a":"#d3dcf4");
  Text("LEFT STICK\n"+lx.ToString("F2")+", "+ly.ToString("F2"),450,770,340,85,24,"#9bb8eb");
  Text("RIGHT STICK\n"+rx.ToString("F2")+", "+ry.ToString("F2"),890,770,350,85,24,"#9bb8eb");
  Text("PS1 ACTION → PHYSICAL INPUT",1400,180,440,55,25,"#c4b4ff");
  int start=Math.Max(0,Math.Min(selected-5,6));for(int i=start;i<Math.Min(start+10,16);i++)Text((selected==i?"> ":"")+KeyInput.ButtonNames[i]+": "+map.SourceName(i),1400,250+(i-start)*49,445,48,24,selected==i?"#ffe07a":"#d3dcf4");
  string[] actions={"Restore PS5 defaults","Save and return"};
  for(int i=0;i<2;i++)Text((selected==16+i?"> ":"")+actions[i],90+i*600,890,570,65,26,selected==16+i?"#ffe07a":"#c4b4ff");
  Text((armed?"LISTENING: ":"")+message,70,970,1780,90,25,armed?"#ffe07a":"#a9c4ed");
  for(int i=used;i<labels.Count;i++)labels[i].Hide();
 }
}
