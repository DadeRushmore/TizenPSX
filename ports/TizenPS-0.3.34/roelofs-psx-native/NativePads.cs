using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

// Full reports keep every button and axis on the same physical controller.
internal static class NativePadReport {
 internal static bool Decode(byte[] data,int length,int bus,out PhysicalPadState state){
  state=new PhysicalPadState();uint faces;string format;
  if(!DualSenseReports.Decode(data,length,bus,out faces,out format))return false;
  int o=bus==3?8:data[0]==0x31?9:5,a=data[0]==0x31?2:1,hat=data[o]&15;
  uint bits=faces;
  if((data[o]&32)!=0)bits|=1;if((data[o]&16)!=0)bits|=2;
  if(hat==7||hat<=1)bits|=16;if(hat>=3&&hat<=5)bits|=32;
  if(hat>=5&&hat<=7)bits|=64;if(hat>=1&&hat<=3)bits|=128;
  int b=data[o+1];if((b&1)!=0)bits|=1024;if((b&2)!=0)bits|=2048;
  if((b&4)!=0)bits|=4096;if((b&8)!=0)bits|=8192;
  if((b&16)!=0)bits|=4;if((b&32)!=0)bits|=8;
  if((b&64)!=0)bits|=16384;if((b&128)!=0)bits|=32768;if((data[o+2]&1)!=0)bits|=1u<<20;
  state=PhysicalPadState.Create(true,bits,Axis(data[a]),-Axis(data[a+1]),Axis(data[a+2]),-Axis(data[a+3]),Stopwatch.GetTimestamp(),0,false);
  return true;
 }
 static float Axis(byte v)=>v>=128?(v-128)/127f:(v-128)/128f;
}

// Slots are claimed explicitly, never reassigned because the other player moved.
// All public access is serialized across HID, UI and emulation threads.
internal sealed class NativePadSlots {
 sealed class Slot {internal string Id;internal PhysicalPadState State=new PhysicalPadState();internal uint Pending;internal bool Blocked=true;}
 readonly Slot[] slots;readonly object gate=new object();
 internal NativePadSlots(int capacity=2){slots=Enumerable.Range(0,capacity).Select(i=>new Slot()).ToArray();}
 readonly Dictionary<string,uint> previous=new Dictionary<string,uint>();long activity;
 internal bool RecentActivity(long time){lock(gate)return activity!=0&&Math.Abs(time-activity)/(double)Stopwatch.Frequency<.25;}
 internal void Observe(string id,PhysicalPadState state){lock(gate){
  uint before;bool known=previous.TryGetValue(id,out before);previous[id]=state.Bits;
  if(state.Bits!=0||known&&before!=state.Bits||Math.Abs(state.LX)>.5||Math.Abs(state.LY)>.5)activity=state.Timestamp;
  var slot=Array.Find(slots,s=>s.Id==id);
  if(slot==null){slot=Array.Find(slots,s=>s.Id==null)??Array.Find(slots,s=>!s.State.Connected&&state.Bits!=0);if(slot==null)return;slot.Id=id;}
  uint old=slot.State.Bits;slot.State=state;
  if(slot.Blocked){slot.Pending=0;if(state.Bits==0)slot.Blocked=false;}
  else slot.Pending|=state.Bits&~old;
 }}
 internal PhysicalPadState Read(int port,long now,bool consume){lock(gate){
  if(port<0||port>=slots.Length)return new PhysicalPadState();var s=slots[port];var p=s.State;
  if(!p.Connected||(now-p.Timestamp)/(double)Stopwatch.Frequency>.75){s.Pending=0;s.Blocked=true;return new PhysicalPadState();}
  if(s.Blocked)return PhysicalPadState.Create(true,0,0,0,0,0,p.Timestamp,port+1,false);
  uint bits=p.Bits|(consume?s.Pending:0);if(consume)s.Pending=0;
  return PhysicalPadState.Create(true,bits,p.LX,p.LY,p.RX,p.RY,p.Timestamp,port+1,false);
 }}
 internal void Disconnect(string id){lock(gate){previous.Remove(id);foreach(var s in slots)if(s.Id==id){s.State=new PhysicalPadState();s.Pending=0;s.Blocked=true;}}}
 internal void Suspend(){lock(gate){previous.Clear();foreach(var s in slots){s.State=new PhysicalPadState();s.Pending=0;s.Blocked=true;}}}
 internal void BlockHeld(){lock(gate){foreach(var s in slots){s.Pending=0;s.Blocked=true;}}}
 internal void Reset(){lock(gate){previous.Clear();foreach(var s in slots){s.Id=null;s.State=new PhysicalPadState();s.Pending=0;s.Blocked=true;}}}
 internal string Status {get{lock(gate){long now=Stopwatch.GetTimestamp();return string.Join(" | ",Enumerable.Range(0,slots.Length).Select(i=>"P"+(i+1)+": "+(slots[i].Id==null?"not connected":Read(i,now,false).Connected?"connected":"disconnected (slot reserved)")));}}}
}

internal sealed class NativePadDevice {internal int Fd,Bus;internal string Id,Path;internal double Last;}
internal interface INativePadsIo {NativePadDevice[] OpenAvailable(string[] paths);int Read(int fd,byte[] data,out int error);void Close(int fd);}
internal sealed class LinuxNativePadsIo:INativePadsIo {
 static int connection;
 [DllImport("libc.so.6",SetLastError=true)]static extern int open(string path,int flags);
 [DllImport("libc.so.6",SetLastError=true)]static extern int close(int fd);
 [DllImport("libc.so.6",SetLastError=true)]static extern int ioctl(int fd,uint request,IntPtr data);
 [DllImport("libc.so.6",SetLastError=true)]static extern IntPtr read(int fd,[Out]byte[] data,UIntPtr length);
 public NativePadDevice[] OpenAvailable(string[] paths){
  var result=new List<NativePadDevice>();if(IntPtr.Size!=4||RuntimeInformation.ProcessArchitecture!=Architecture.Arm)return result.ToArray();
  IntPtr info=Marshal.AllocHGlobal(256);
  try{foreach(var path in Directory.EnumerateFiles("/dev","hidraw*").OrderBy(p=>p,StringComparer.Ordinal).Take(64)){
   if(paths.Contains(path))continue;int fd=open(path,0x800);if(fd<0)continue;
   try{if(ioctl(fd,0x80084803,info)!=0)continue;int bus=Marshal.ReadInt32(info);
    if((ushort)Marshal.ReadInt16(info,4)!=0x054c||(ushort)Marshal.ReadInt16(info,6)!=0x0ce6||(bus!=3&&bus!=5))continue;
    for(int i=0;i<256;i++)Marshal.WriteByte(info,i,0);
    // HIDIOCGRAWUNIQ: Bluetooth identity survives changing hidraw node numbers.
    int uniqueLength=ioctl(fd,0x81004808,info);Marshal.WriteByte(info,255,0);
    string unique=uniqueLength>0?Marshal.PtrToStringAnsi(info):null;
    result.Add(new NativePadDevice{Fd=fd,Bus=bus,Path=path,Id=string.IsNullOrWhiteSpace(unique)?"connection:"+Interlocked.Increment(ref connection):"sony:"+unique});fd=-1;
    if(result.Count+paths.Length>=4)break;
   }finally{if(fd>=0)close(fd);}
  }}catch{foreach(var d in result)close(d.Fd);throw;}finally{Marshal.FreeHGlobal(info);}return result.ToArray();
 }
 public int Read(int fd,byte[] data,out int error){int n=read(fd,data,new UIntPtr((uint)data.Length)).ToInt32();error=n<0?Marshal.GetLastWin32Error():0;return n;}
 public void Close(int fd){close(fd);}
}
internal sealed class NativePadsReader {
 internal static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
 internal readonly NativePadSlots Slots;readonly INativePadsIo io;volatile bool enabled,stop;internal volatile bool Finished;internal string Error="";
 long decoded,rejected;
 internal string Diagnostics=>"native_two enabled="+enabled+" decoded="+Interlocked.Read(ref decoded)+" rejected="+Interlocked.Read(ref rejected)+" "+Slots.Status+" error="+Error;
 internal NativePadsReader(INativePadsIo io=null,int capacity=2){this.io=io??new LinuxNativePadsIo();Slots=new NativePadSlots(capacity);}
 internal bool Enabled{get=>enabled;set{enabled=value;if(!value)Slots.Suspend();}}
 internal void Start()=>new Thread(Loop){IsBackground=true,Name="PSX two native controllers"}.Start();
 internal void Stop(){stop=true;Enabled=false;}
 void Loop(){var devices=new List<NativePadDevice>();var buffer=new byte[128];double search=0;
  try{while(!stop){
   if(!enabled){foreach(var d in devices)io.Close(d.Fd);devices.Clear();Slots.Suspend();Thread.Sleep(10);continue;}
   double now=NativePadsReader.Now;
   if(now>=search&&devices.Count<4){search=now+1;try{foreach(var d in io.OpenAvailable(devices.Select(x=>x.Path).ToArray())){if(devices.Any(x=>x.Id==d.Id)){io.Close(d.Fd);continue;}d.Last=now;devices.Add(d);}Error="";}catch(Exception e){Error=e.Message;}}
   for(int i=0;i<devices.Count;i++){var d=devices[i];bool failed=false;double begin=NativePadsReader.Now;
    for(int n=0;n<128&&NativePadsReader.Now-begin<.003&&enabled&&!stop;n++){
     int error,count=io.Read(d.Fd,buffer,out error);if(count<0&&(error==11||error==4))break;
     if(count<=0){failed=true;break;}PhysicalPadState state;
     if(NativePadReport.Decode(buffer,count,d.Bus,out state)){Interlocked.Increment(ref decoded);d.Last=NativePadsReader.Now;if(enabled&&!stop)Slots.Observe(d.Id,state);}else Interlocked.Increment(ref rejected);
    }
    if(failed||NativePadsReader.Now-d.Last>.75){io.Close(d.Fd);devices.RemoveAt(i--);Slots.Disconnect(d.Id);}
   }
   Thread.Sleep(4);
  }}catch(Exception e){Error=e.Message;}finally{foreach(var d in devices)io.Close(d.Fd);Slots.Suspend();Finished=true;}
 }
}
