using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// Native writes one owned slot, the UI copies another; only ownership changes lock.
internal sealed class VideoFramePool : IDisposable {
 sealed class Buffer : SafeHandleZeroOrMinusOneIsInvalid {
  internal Buffer(int bytes):base(true){SetHandle(Marshal.AllocHGlobal(bytes));}
  protected override bool ReleaseHandle(){Marshal.FreeHGlobal(handle);return true;}
 }
 enum State {Free,Writing,Pending,Presenting}
 sealed class Slot {internal Buffer Buffer;internal State State;internal int Width,Height;}
 readonly object gate=new object();readonly Slot[] slots=new Slot[3];readonly int maxWidth,maxHeight,bytes;
 bool closing,freed,scheduled;int pending=-1;
 internal long Acquired,Published,Replaced,Unavailable,Presented;
 internal VideoFramePool(int width=1024,int height=512){
  if(width<1||height<1||width>1024||height>512)throw new ArgumentOutOfRangeException();maxWidth=width;maxHeight=height;bytes=checked(width*height*4);
  try{for(int i=0;i<slots.Length;i++)slots[i]=new Slot{Buffer=new Buffer(bytes)};}catch{foreach(var slot in slots)slot?.Buffer?.Dispose();throw;}
 }
 int Find(IntPtr pointer){for(int i=0;i<slots.Length;i++)if(!slots[i].Buffer.IsClosed&&slots[i].Buffer.DangerousGetHandle()==pointer)return i;return -1;}
 internal IntPtr Acquire(int width,int height){lock(gate){
  if(closing||width<1||height<1||width>maxWidth||height>maxHeight)return IntPtr.Zero;
  int index=Array.FindIndex(slots,s=>s.State==State.Free);
  if(index<0&&pending>=0){index=pending;pending=-1;Replaced++;slots[index].State=State.Free;}
  if(index<0){Unavailable++;return IntPtr.Zero;}
  slots[index].State=State.Writing;slots[index].Width=width;slots[index].Height=height;Acquired++;return slots[index].Buffer.DangerousGetHandle();
 }}
 internal bool Publish(IntPtr pointer,int width,int height){lock(gate){
  int index=Find(pointer);if(index<0||slots[index].State!=State.Writing||slots[index].Width!=width||slots[index].Height!=height)throw new InvalidOperationException("Invalid video buffer handoff");
  if(closing){slots[index].State=State.Free;TryFree();return false;}
  if(pending>=0){slots[pending].State=State.Free;Replaced++;}
  pending=index;slots[index].State=State.Pending;Published++;
  if(scheduled)return false;scheduled=true;return true;
 }}
 internal Lease Take(){lock(gate){scheduled=false;if(closing||pending<0)return null;int index=pending;pending=-1;slots[index].State=State.Presenting;return new Lease(this,index,slots[index].Buffer.DangerousGetHandle(),slots[index].Width,slots[index].Height);}}
 internal void AbortWrite(IntPtr pointer){lock(gate){int index=Find(pointer);if(index>=0&&slots[index].State==State.Writing)slots[index].State=State.Free;TryFree();}}
 void Release(int index){lock(gate){if(slots[index].State!=State.Presenting)throw new InvalidOperationException("Invalid video lease release");slots[index].State=State.Free;Presented++;TryFree();}}
 void TryFree(){if(!closing||freed||Array.Exists(slots,s=>s.State==State.Writing||s.State==State.Presenting))return;foreach(var slot in slots)slot.Buffer.Dispose();freed=true;}
 // Only call after the native entry has returned: no producer can still use its slot.
 internal void ProducerCompleted(){lock(gate){closing=true;pending=-1;scheduled=false;foreach(var slot in slots)if(slot.State==State.Writing||slot.State==State.Pending)slot.State=State.Free;TryFree();}}
 public void Dispose(){lock(gate){closing=true;if(pending>=0){slots[pending].State=State.Free;pending=-1;}scheduled=false;TryFree();}}
 internal bool Freed {get{lock(gate)return freed;}}
 internal string Summary {get{lock(gate)return "VIDEO POOL: acquired="+Acquired+" published="+Published+" replaced="+Replaced+" unavailable="+Unavailable+" presented="+Presented+" allocated="+(freed?0:3*bytes);}}
 internal sealed class Lease : IDisposable {
  VideoFramePool owner;readonly int index;internal readonly IntPtr Data;internal readonly int Width,Height;
  internal Lease(VideoFramePool pool,int slot,IntPtr data,int width,int height){owner=pool;index=slot;Data=data;Width=width;Height=height;}
  public void Dispose(){var pool=System.Threading.Interlocked.Exchange(ref owner,null);pool?.Release(index);}
 }
}
