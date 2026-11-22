using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

internal interface IDualSenseHid {
    int OpenSingle(out int bus,out string detail);
    int Read(int fd,byte[] buffer,out int error);
    void Close(int fd);
}

// Every fd is owned and closed by the background worker. No writes, grabs,
// permissions changes, feature reports, or shared /dev/input event consumption.
internal sealed class LinuxDualSenseHid : IDualSenseHid
{
    [DllImport("libc.so.6",SetLastError=true)]static extern int open(string path,int flags);
    [DllImport("libc.so.6",SetLastError=true)]static extern int close(int fd);
    [DllImport("libc.so.6",SetLastError=true)]static extern int ioctl(int fd,uint request,IntPtr data);
    [DllImport("libc.so.6",SetLastError=true)]static extern IntPtr read(int fd,[Out] byte[] data,UIntPtr length);
    public int OpenSingle(out int bus,out string detail) {
        bus=0;detail="No readable DualSense";
        if(IntPtr.Size!=4||RuntimeInformation.ProcessArchitecture!=Architecture.Arm)return -1;
        int selected=-1,count=0;IntPtr info=Marshal.AllocHGlobal(8);
        try {
            foreach(var path in Directory.EnumerateFiles("/dev","hidraw*").OrderBy(x=>x,StringComparer.Ordinal).Take(64)) {
                int fd=open(path,0x800);if(fd<0)continue;
                try {
                    if(ioctl(fd,0x80084803,info)!=0)continue;
                    int transport=Marshal.ReadInt32(info);
                    if(unchecked((ushort)Marshal.ReadInt16(info,4))!=0x054c || unchecked((ushort)Marshal.ReadInt16(info,6))!=0x0ce6 || (transport!=3&&transport!=5))continue;
                    count++;
                    if(selected<0){selected=fd;fd=-1;bus=transport;detail=Path.GetFileName(path)+" Sony 054C:0CE6 bus="+bus;}
                }finally{if(fd>=0)close(fd);}
            }
            if(count==1){int result=selected;selected=-1;return result;}
            if(count>1)detail="Multiple DualSense devices; direct faces disabled";
            return -1;
        }finally{if(selected>=0)close(selected);Marshal.FreeHGlobal(info);}
    }
    public int Read(int fd,byte[] buffer,out int error) {int n=read(fd,buffer,new UIntPtr((uint)buffer.Length)).ToInt32();error=n<0?Marshal.GetLastWin32Error():0;return n;}
    public void Close(int fd){close(fd);}
}

internal sealed class DualSenseHidReader
{
    internal static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    internal struct State {internal bool Active;internal uint Faces;internal double Time;}
    readonly object sync=new object();
    readonly Queue<State> queue=new Queue<State>();
    readonly IDualSenseHid io;
    volatile bool allowed,stop,finished;
    bool active;
    uint faces;
    double lastValid=-1;
    long reports,rejected,overflows,circlePresses,trianglePresses;
    string status="Waiting for one controller",format="none",lastPacket="none";
    internal DualSenseHidReader(IDualSenseHid io=null){this.io=io??new LinuxDualSenseHid();}
    internal bool Allowed {get=>allowed;set{allowed=value;if(!value)Invalidate("Direct faces inactive");}}
    internal bool Finished=>finished;
    internal string Diagnostics {get{lock(sync)return "DUALSENSE_HID "+status+" active="+active+" format="+format+" reports="+reports+" rejected="+rejected+" queue_overflows="+overflows+" faces="+faces+" circle_presses="+circlePresses+" triangle_presses="+trianglePresses+" last_packet="+lastPacket;}}
    internal void Start()=>new Thread(Loop){IsBackground=true,Name="DualSense face buttons"}.Start();
    internal void Stop(){stop=true;Allowed=false;}
    internal void Resync(){lock(sync){queue.Clear();queue.Enqueue(new State{Active=active&&allowed,Faces=faces,Time=Now});}}
    void Invalidate(string why){lock(sync){status=why;if(active||queue.Count>0){queue.Clear();queue.Enqueue(new State{Time=Now});}active=false;faces=0;}}
    internal State[] Drain() {
        lock(sync) {
            if(!allowed || active&&Now-lastValid>.75)Invalidate("Waiting for fresh DualSense reports");
            var result=queue.ToArray();queue.Clear();return result;
        }
    }
    void Loop() {
        int fd=-1,bus=0;double nextSearch=0,lastReport=0;
        var buffer=new byte[128];
        try {
            while(!stop) {
                if(!allowed){if(fd>=0){io.Close(fd);fd=-1;}Thread.Sleep(10);continue;}
                if(fd<0) {
                    if(Now<nextSearch){Thread.Sleep(10);continue;}
                    nextSearch=Now+1;
                    string detail;fd=io.OpenSingle(out bus,out detail);lock(sync)status=detail;
                    if(fd<0)continue;lastReport=Now;
                }
                bool failed=false;double begin=Now;
                for(int i=0;i<128 && Now-begin<.006 && allowed&&!stop;i++) {
                    int error,n=io.Read(fd,buffer,out error);
                    if(n<0 && (error==11||error==4))break; // EAGAIN/EINTR, no pending packet
                    if(n<=0){failed=true;break;}
                    uint bits;string kind;
                    if(!DualSenseReports.Decode(buffer,n,bus,out bits,out kind)) {
                        lock(sync){rejected++;if(rejected<=4||(rejected&(rejected-1))==0)lastPacket="bus="+bus+" size="+n+" prefix="+BitConverter.ToString(buffer,0,Math.Min(n,12));}
                        continue;
                    }
                    double now=Now;lastReport=now;
                    lock(sync) {
                        if(!allowed||stop)break;
                        reports++;format=kind;lastValid=now;
                        if(queue.Count>=256){overflows++;queue.Clear();queue.Enqueue(new State{Time=now});active=false;}
                        if((bits&~faces&DualSenseReports.Circle)!=0)circlePresses++;
                        if((bits&~faces&DualSenseReports.Triangle)!=0)trianglePresses++;
                        if(!active||faces!=bits) {
                            lastPacket="bus="+bus+" size="+n+" prefix="+BitConverter.ToString(buffer,0,Math.Min(n,12));
                            queue.Enqueue(new State{Active=true,Faces=bits,Time=now});
                        }
                        active=true;faces=bits;
                    }
                }
                if(failed||Now-lastReport>.75) {
                    io.Close(fd);fd=-1;Invalidate(failed?"DualSense disconnected/read failed":"DualSense report stream stalled");
                }
                Thread.Sleep(4);
            }
        }catch(Exception e){Invalidate("Direct faces unavailable: "+e.Message);}
        finally{if(fd>=0)io.Close(fd);Invalidate("Direct face reader stopped");finished=true;}
    }
}
