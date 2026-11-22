using System;
using System.Linq;
internal sealed class PhysicalPadState {
 internal readonly bool Connected;internal readonly uint Bits;internal readonly float LX,LY,RX,RY;internal readonly long Timestamp;internal readonly int Slot;
 internal PhysicalPadState(){}
 PhysicalPadState(bool connected,uint bits,float lx,float ly,float rx,float ry,long timestamp,int slot){Connected=connected;Bits=bits;LX=lx;LY=ly;RX=rx;RY=ry;Timestamp=timestamp;Slot=slot;}
 internal static PhysicalPadState Create(bool c,uint b,float lx,float ly,float rx,float ry,long t,int slot=0,bool unused=false)=>new PhysicalPadState(c,b,lx,ly,rx,ry,t,slot);
}
// Physical HID identities, shared by the visual mapper and gameplay in both apps.
internal sealed class DualSenseButtonMap {
 internal static readonly string[] Names={"Cross","Square","Create","Options","Up","Down","Left","Right","Circle","Triangle","L1","R1","L2","R2","L3","R3","","","","","PS button"};
 readonly int[] targets,defaults;internal readonly int[] Sources;
 internal DualSenseButtonMap(int[] targets){this.targets=(int[])targets.Clone();defaults=(int[])targets.Clone();Sources=(int[])targets.Clone();}
 internal uint Apply(uint physical){uint result=0;for(int i=0;i<Sources.Length;i++)if((physical&(1u<<Sources[i]))!=0)result|=1u<<targets[i];return result;}
 internal string SourceName(int target)=>Names[Sources[target]];
 internal static string TokenName(string token){int i;return token!=null&&token.StartsWith("button:")&&int.TryParse(token.Substring(7),out i)&&i>=0&&i<Names.Length?Names[i]:"Unassigned";}
 internal void Reset()=>Array.Copy(defaults,Sources,Sources.Length);
 internal void Assign(int target,int source){if(target<0||target>=Sources.Length||source<0||source>=Names.Length||Names[source]=="")throw new ArgumentException("Invalid physical button");int other=Array.IndexOf(Sources,source);if(other>=0)Sources[other]=Sources[target];Sources[target]=source;}
 internal string Serialize()=>"DualSense-physical-v1\n"+string.Join(",",Sources);
 internal void Load(string text){var lines=text.Trim().Split('\n');if(lines.Length!=2||lines[0].Trim()!="DualSense-physical-v1")throw new FormatException("Unknown native mapping");var values=lines[1].Split(',').Select(int.Parse).ToArray();if(values.Length!=Sources.Length||values.Distinct().Count()!=values.Length||values.Any(i=>i<0||i>=Names.Length||Names[i]==""))throw new FormatException("Invalid native mapping");Array.Copy(values,Sources,values.Length);}
 internal static int SinglePress(uint mask){if(mask==0||(mask&(mask-1))!=0)return -1;for(int i=0;i<Names.Length;i++)if(mask==(1u<<i)&&Names[i]!="")return i;return -1;}
}
