using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

// Read-only observation: no instruction probes, native entry points or CPU changes.
static class HardwareReport {
 public static string Capture(){
  var text=new StringBuilder("\nHardware architecture (read-only)\n");
  text.AppendLine("Process pointer width: "+(IntPtr.Size*8)+" bits");
  try{text.AppendLine("Process architecture: "+RuntimeInformation.ProcessArchitecture+" | OS architecture reported by runtime: "+RuntimeInformation.OSArchitecture);}catch(Exception e){text.AppendLine("Runtime architecture unavailable: "+e.GetType().Name);}
  text.AppendLine("Compiler baseline: ARM32 Cortex-A72, gnueabi, explicit Clang O3. A72 comparison build.");
  try{
   int count=0;bool found=false;
   using(var stream=new FileStream("/proc/cpuinfo",FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
   using(var reader=new StreamReader(stream)){
    while(count++<4096){string line=reader.ReadLine();if(line==null)break;if(line.Length>4096)line=line.Substring(0,4096);
     int colon=line.IndexOf(':');if(colon<0)continue;string key=line.Substring(0,colon).Trim();
     if(key=="processor"||key=="Processor"||key=="CPU architecture"||key=="CPU part"||key=="CPU implementer"||key=="CPU variant"||key=="CPU revision"||key=="Hardware"||key=="model name"||key=="Features"){text.AppendLine(line);found=true;}
    }
   }
   if(!found)text.AppendLine("cpuinfo readable but no recognised identity fields exposed.");
  }catch(Exception e){text.AppendLine("/proc/cpuinfo unavailable: "+e.GetType().Name+": "+e.Message);}
  // auxv words follow the current process ABI, not the kernel's bitness.
  try{
   using(var stream=new FileStream("/proc/self/auxv",FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
   using(var reader=new BinaryReader(stream)){
    bool found=false;for(int i=0;i<256;i++){
     ulong type=IntPtr.Size==4?reader.ReadUInt32():reader.ReadUInt64();
     ulong value=IntPtr.Size==4?reader.ReadUInt32():reader.ReadUInt64();if(type==0)break;
     if(type==16||type==26){text.AppendLine((type==16?"AT_HWCAP":"AT_HWCAP2")+" (process ABI): 0x"+value.ToString("x"));found=true;}
    }if(!found)text.AppendLine("No HWCAP entries exposed.");
   }
  }catch(Exception e){text.AppendLine("/proc/self/auxv unavailable: "+e.GetType().Name+": "+e.Message);}
  text.AppendLine("Identity and capabilities require review before selecting a newer compiler target. Missing information is not evidence of missing hardware.");
  return text.ToString();
 }
}
