using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

static class Native {
 [DllImport("libdl.so.2")] internal static extern IntPtr dlopen(string name,int flags);
 [DllImport("libdl.so.2")] internal static extern IntPtr dlsym(IntPtr h,string name);
 [DllImport("libc.so.6",SetLastError=true)] static extern IntPtr mmap(IntPtr a,UIntPtr s,int p,int f,int fd,IntPtr o);
 [DllImport("libc.so.6",SetLastError=true)] static extern int mprotect(IntPtr a,UIntPtr s,int p);
 [DllImport("libc.so.6")] static extern int munmap(IntPtr a,UIntPtr s);
 [DllImport("libc.so.6")] static extern int getpagesize();
 [DllImport("libc.so.6",EntryPoint="syscall")] static extern int Flush(int n,IntPtr a,IntPtr b,int flags);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Report(IntPtr text);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Video(IntPtr data,uint w,uint h);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Audio(IntPtr data,uint frames);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate ulong Control();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Metrics(uint ticks,uint audio,uint elapsedMs,uint targetMilliFps);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr AcquireVideo(uint width,uint height);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int PlayerV4(Report log,Video video,Audio audio,Control control,string fixture,string game,string bios,string save,int dualShock,uint options,Metrics metrics,AcquireVideo acquire,Control control2);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int PlayerV3(Report log,Video video,Audio audio,Control control,string fixture,string game,string bios,string save,int dualShock,uint options,Metrics metrics,AcquireVideo acquire);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Player(Report log,Video video,Audio audio,Control control,string fixture,string game,string bios,string save,int dualShock,uint options,Metrics metrics);
 internal static T Function<T>(IntPtr h,string name) where T:Delegate {var p=dlsym(h,name);if(p==IntPtr.Zero)throw new Exception("Missing system function: "+name);return Marshal.GetDelegateForFunctionPointer<T>(p);}
 internal static IntPtr Open(string name){var p=dlopen(name,2);if(p==IntPtr.Zero)throw new Exception("Cannot open "+name);return p;}
 internal static string Hash(byte[] data){using(var s=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(s.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
 internal static void Run(string resources,string game,string bios,string save,Report report,Video video,Audio audio,Control control,bool dualShock,uint options,Metrics metrics,AcquireVideo acquire=null,Control control2=null){
  if(IntPtr.Size!=4||RuntimeInformation.ProcessArchitecture!=Architecture.Arm)throw new Exception("ARM32 TV required");
  var bytes=File.ReadAllBytes(Path.Combine(resources,"libpcsx_tizen.so"));
  if(Hash(bytes)!=File.ReadAllText(Path.Combine(resources,"core.sha256")).Trim())throw new Exception("Core checksum mismatch");
  int page=getpagesize();var plan=DynamicElf.Parse(bytes,page,control2!=null?"retro_player_run_v4":acquire!=null?"retro_player_run_v3":"retro_player_run_v2");
  var handles=new[]{Open("libc.so.6"),Open("libdl.so.2")};var imports=new Dictionary<string,uint>();
  foreach(var s in plan.Symbols){if(s.Section!=0||s.Name.Length==0||imports.ContainsKey(s.Name))continue;IntPtr p=IntPtr.Zero;foreach(var h in handles){p=dlsym(h,s.Name);if(p!=IntPtr.Zero)break;}if(p==IntPtr.Zero)throw new Exception("Unresolved import: "+s.Name);imports[s.Name]=unchecked((uint)p.ToInt32());}
  var size=new UIntPtr((uint)plan.Image.Length);var mem=mmap(IntPtr.Zero,size,3,0x22,-1,IntPtr.Zero);if(mem==new IntPtr(-1))throw new Exception("Native allocation failed");
  try{var image=plan.Relocate(unchecked((uint)mem.ToInt32()),n=>imports[n]);Marshal.Copy(image,0,mem,image.Length);
   if(Flush(0x0f0002,mem,IntPtr.Add(mem,image.Length),0)!=0)throw new Exception("Instruction cache flush failed");
   for(int i=0;i<plan.Protections.Length;i++)if(mprotect(IntPtr.Add(mem,i*page),new UIntPtr((uint)page),plan.Protections[i])!=0)throw new Exception("Segment protection failed");
   int result=control2!=null?Marshal.GetDelegateForFunctionPointer<PlayerV4>(IntPtr.Add(mem,plan.Entry))(report,video,audio,control,Path.Combine(resources,"libovprobe.so"),game,bios,save,dualShock?1:0,options,metrics,acquire,control2):acquire!=null?Marshal.GetDelegateForFunctionPointer<PlayerV3>(IntPtr.Add(mem,plan.Entry))(report,video,audio,control,Path.Combine(resources,"libovprobe.so"),game,bios,save,dualShock?1:0,options,metrics,acquire):Marshal.GetDelegateForFunctionPointer<Player>(IntPtr.Add(mem,plan.Entry))(report,video,audio,control,Path.Combine(resources,"libovprobe.so"),game,bios,save,dualShock?1:0,options,metrics);
   if(result!=0)throw new Exception("Core returned "+result);
  }finally{GC.KeepAlive(report);GC.KeepAlive(video);GC.KeepAlive(audio);GC.KeepAlive(control);GC.KeepAlive(control2);GC.KeepAlive(metrics);GC.KeepAlive(acquire);munmap(mem,size);}
 }
}


static class Screen {
 const string E="libevas.so.1";
 [DllImport(E)] internal static extern IntPtr evas_object_evas_get(IntPtr obj);
 [DllImport(E)] internal static extern IntPtr evas_object_image_filled_add(IntPtr canvas);
 [DllImport(E)] internal static extern void evas_object_image_size_set(IntPtr obj,int w,int h);
 [DllImport(E)] internal static extern void evas_object_image_data_copy_set(IntPtr obj,IntPtr data);
 [DllImport(E)] internal static extern void evas_object_image_data_update_add(IntPtr obj,int x,int y,int w,int h);
 [DllImport(E)] internal static extern void evas_object_move(IntPtr obj,int x,int y);
 [DllImport(E)] internal static extern void evas_object_resize(IntPtr obj,int w,int h);
 [DllImport(E)] internal static extern void evas_object_layer_set(IntPtr obj,short layer);
 [DllImport(E)] internal static extern void evas_object_show(IntPtr obj);
 [DllImport(E)] internal static extern void evas_object_hide(IntPtr obj);
}
