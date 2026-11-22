using System;
using System.Runtime.InteropServices;

internal interface IAudioOutput : IDisposable {
 void Write(IntPtr data,uint frames);
 void Drain();
}

// The output backend is owned exclusively by Sound's caller (original mode)
// or its feeder thread (adaptive mode). Never free it under a blocked write.
internal sealed class PulseAudioOutput : IAudioOutput {
 [StructLayout(LayoutKind.Sequential)] struct Spec{public int format;public uint rate;public byte channels;}
 [StructLayout(LayoutKind.Sequential)] struct Attributes{public uint maxlength,tlength,prebuf,minreq,fragsize;}
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr New(IntPtr server,string name,int direction,IntPtr device,string stream,ref Spec spec,IntPtr map,ref Attributes attrs,out int error);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int WriteStream(IntPtr stream,IntPtr data,UIntPtr count,out int error);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Free(IntPtr stream);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DrainStream(IntPtr stream,out int error);
 IntPtr stream;readonly WriteStream write;readonly Free free;readonly DrainStream drain;
 internal PulseAudioOutput(int bufferMs){
  var h=Native.Open("libpulse-simple.so.0");var spec=new Spec{format=3,rate=44100,channels=2};uint target=(uint)(44100*Math.Max(40,Math.Min(250,bufferMs))/1000)*4;var attrs=new Attributes{maxlength=Math.Max(35280u,target*2),tlength=target,prebuf=target*2/3/4*4,minreq=2940,fragsize=uint.MaxValue};int error;
  write=Native.Function<WriteStream>(h,"pa_simple_write");free=Native.Function<Free>(h,"pa_simple_free");drain=Native.Function<DrainStream>(h,"pa_simple_drain");stream=Native.Function<New>(h,"pa_simple_new")(IntPtr.Zero,"TizenPS",1,IntPtr.Zero,"Game",ref spec,IntPtr.Zero,ref attrs,out error);if(stream==IntPtr.Zero)throw new Exception("Audio open: "+error);
 }
 public void Write(IntPtr data,uint frames){int error;if(write(stream,data,new UIntPtr(frames*4),out error)<0)throw new Exception("Audio write: "+error);}
 public void Drain(){int error;if(stream!=IntPtr.Zero&&drain(stream,out error)<0)throw new Exception("Audio drain: "+error);}
 public void Dispose(){if(stream!=IntPtr.Zero){free(stream);stream=IntPtr.Zero;}}
}
