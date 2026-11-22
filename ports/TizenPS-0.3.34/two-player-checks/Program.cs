using System;using System.Diagnostics;using System.Threading;using System.Collections.Concurrent;using System.Linq;
class Check {
 static void Need(bool v,string why){if(!v)throw new Exception(why);}
 static byte[] Packet(int kind,uint bits=0){int len=kind==0?10:kind==1?64:78,o=kind==0||kind==2?5:kind==1?8:9,a=kind==3?2:1;var b=new byte[len];b[0]=(byte)(kind==3?49:1);for(int i=0;i<4;i++)b[a+i]=128;b[o]=8;
  uint[] masks={2,1,256,512,1024,2048,4096,8192,4,8,16384,32768};
  for(int i=0;i<12;i++)if((bits&masks[i])!=0){if(i<4)b[o]|=(byte)(16<<i);else b[o+1]|=(byte)(1<<(i-4));}
  Seal(b);return b;
 }
 static void Seal(byte[] b){if(b[0]!=49)return;uint crc=DualSenseReports.InputCrc(b,74);for(int i=0;i<4;i++)b[74+i]=(byte)(crc>>(8*i));}
 static PhysicalPadState Decode(byte[] b,int bus=5){PhysicalPadState s;Need(NativePadReport.Decode(b,b.Length,bus,out s),"valid packet");return s;}
 static PhysicalPadState State(uint bits)=>PhysicalPadState.Create(true,bits,0,0,0,0,Stopwatch.GetTimestamp(),0,false);
 static void Join(NativePadSlots s,string id){s.Observe(id,State(0));}
 static PhysicalPadState Read(NativePadSlots s,int port,bool consume=true)=>s.Read(port,Stopwatch.GetTimestamp(),consume);
 static void Until(Func<bool> f,string why){var sw=Stopwatch.StartNew();while(!f()&&sw.Elapsed.TotalSeconds<3)Thread.Sleep(5);Need(f(),why);}
 sealed class FakeIo:INativePadsIo {
  internal readonly ConcurrentQueue<byte[]>[] Packets={new ConcurrentQueue<byte[]>(),new ConcurrentQueue<byte[]>()};internal int Opens,Closes;internal volatile bool LoseFirst;
  public NativePadDevice[] OpenAvailable(string[] paths){return Enumerable.Range(0,2).Where(i=>!paths.Contains("dev"+i)&&!(i==0&&LoseFirst)).Select(i=>{Interlocked.Increment(ref Opens);return new NativePadDevice{Fd=i,Bus=5,Id="id"+i,Path="dev"+i};}).ToArray();}
  public int Read(int fd,byte[] buffer,out int error){if(fd==0&&LoseFirst){error=19;return -1;}byte[] b;if(Packets[fd].TryDequeue(out b)){Array.Copy(b,buffer,b.Length);error=0;return b.Length;}error=11;return -1;}
  public void Close(int fd){Interlocked.Increment(ref Closes);}
  internal void Join(int port){Packets[port].Enqueue(Packet(0,0));}
 }
 static void Main(){
  for(int k=0;k<4;k++){
   int bus=k==1?3:5;foreach(uint bit in new uint[]{1,2,4,8,256,512,1024,2048,4096,8192,16384,32768})Need(Decode(Packet(k,bit),bus).Bits==bit,"independent button "+bit+" layout "+k);
   var b=Packet(k,256|512);Need(Decode(b,bus).Bits==768,"simultaneous faces");int a=k==3?2:1;b[a]=0;b[a+1]=255;b[a+2]=255;b[a+3]=0;Seal(b);var p=Decode(b,bus);Need(p.LX==-1&&p.LY==-1&&p.RX==1&&p.RY==1,"axes");
   int o=k==1?8:k==3?9:5;uint[] hats={16,144,128,160,32,96,64,80,0};for(int h=0;h<9;h++){b=Packet(k);b[o]=(byte)h;Seal(b);Need(Decode(b,bus).Bits==hats[h],"hat");}
  }
  PhysicalPadState ignored;var bad=Packet(3,256);bad[74]^=1;Need(!NativePadReport.Decode(bad,78,5,out ignored),"CRC rejection");Need(!NativePadReport.Decode(new byte[3],3,5,out ignored),"short packet");
  var slots=new NativePadSlots();slots.Observe("a",State(8));Need(Read(slots,0).Connected&&Read(slots,0).Bits==0,"automatic assignment blocks held input");Join(slots,"a");Join(slots,"b");Need(Read(slots,0).Bits==0&&Read(slots,1).Bits==0,"join consumed");
  slots.Observe("a",State(256));slots.Observe("b",State(512));Need(Read(slots,0).Bits==256&&Read(slots,1).Bits==512,"port isolation");
  slots.Observe("a",State(0));slots.Observe("a",State(512));slots.Observe("a",State(0));Need(Read(slots,0).Bits==512&&Read(slots,0).Bits==0,"tap latch exactly once");Need(Read(slots,1).Bits==512,"P1 consumption leaves P2 held");
  slots.Disconnect("a");Need(!Read(slots,0).Connected&&Read(slots,1).Bits==512,"disconnect independent");slots.Observe("third",State(0));Need(!Read(slots,0).Connected,"idle third pad cannot steal reserved P1");slots.Observe("a",State(256));Need(Read(slots,0).Bits==0,"reconnect blocks held");slots.Observe("a",State(0));slots.Observe("a",State(256));Need(Read(slots,0).Bits==256,"same identity reconnect");
  slots.BlockHeld();Need(Read(slots,0).Bits==0&&Read(slots,1).Bits==0,"game/menu transition blocks both");slots.Suspend();Need(!Read(slots,0).Connected&&!Read(slots,1).Connected,"pause clears both");
  slots.Reset();Join(slots,"solo");Need(Read(slots,0).Connected&&!Read(slots,1).Connected,"single native player");Need(!slots.Read(0,Stopwatch.GetTimestamp()+Stopwatch.Frequency,false).Connected,"stale neutral");
  var state=PadSnapshot.Create(true,256,-1,1,.5f,-.5f,1,1,false);var opts=new PlayerOptions();Need(ControllerProtocol.NativePacket(state,true,true,true,.1f)==ControllerProtocol.Packet(state,0,true,true,true,opts),"single protocol unchanged");Need(ControllerProtocol.NativePacket(state,false,true,false,.1f)==0,"stale axes cleared");
  var io=new FakeIo();var reader=new NativePadsReader(io);reader.Enabled=true;reader.Start();Until(()=>io.Opens==2,"two handles");io.Join(0);Until(()=>Read(reader.Slots,0).Connected,"first join");io.Join(1);Until(()=>Read(reader.Slots,1).Connected,"second join");io.Packets[0].Enqueue(Packet(0,256));io.Packets[1].Enqueue(Packet(0,512));Until(()=>Read(reader.Slots,0,false).Bits==256&&Read(reader.Slots,1,false).Bits==512,"worker isolation");io.LoseFirst=true;Until(()=>!Read(reader.Slots,0).Connected,"worker disconnect neutral");Need(Read(reader.Slots,1).Bits==512,"worker second survives");reader.Enabled=false;Until(()=>io.Closes==io.Opens,"disabled closes owned descriptors");reader.Stop();Until(()=>reader.Finished,"worker stopped");
  var map=new DualSenseButtonMap(Enumerable.Range(0,16).ToArray());Need(map.Apply(256)==256&&map.Apply(512)==512,"native defaults separate faces");map.Assign(8,9);Need(map.Apply(512)==256&&map.Apply(256)==512,"mapper swap matches gameplay on both players");var saved=map.Serialize();map.Reset();map.Load(saved);Need(map.SourceName(8)=="Triangle"&&map.Apply(512)==256,"mapping persistence");var psp=new DualSenseButtonMap(new[]{0,8,1,9,10,11,3,2,4,5,6,7,20});psp.Assign(1,9);Need(psp.Apply(512)==256&&psp.Apply(256)==512,"PSP same physical map");var three=new NativePadSlots(3);Join(three,"1");Join(three,"2");Join(three,"3");Need(Read(three,2).Connected,"three controllers tracked");
  Console.WriteLine("PASS full Bluetooth/USB decoding, all buttons/axes/hats, CRC; automatic assignment without Options, two-port isolation, tap latches, reconnect, stable slots with an idle third controller, pause/stale release, single-player packet equivalence and worker fd ownership.");
 }
}
