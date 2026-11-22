using System;
using System.Diagnostics;
using System.Threading;
using ElmSharp;

partial class PlayerApp {
 VideoFramePool activeVideoFrames;
 string VideoPipeline=>Volatile.Read(ref activeVideoFrames)!=null?"Buffered (experimental)":"Compatible";
 void PublishBuffered(VideoFramePool pool,IntPtr pointer,int width,int height,Action present){
  try{
   if(stop){pool.AbortWrite(pointer);return;}
   Interlocked.Increment(ref frames);long started=Stopwatch.GetTimestamp();try{bool schedule=pool.Publish(pointer,width,height);Interlocked.Exchange(ref displayReplaced,Interlocked.Read(ref pool.Replaced));if(schedule)EcoreMainloop.PostAndWakeUp(present);}finally{Interlocked.Add(ref copyWork,Stopwatch.GetTimestamp()-started);}
  }catch{pool.AbortWrite(pointer);throw;}
 }
 void PresentBuffered(VideoFramePool pool){
  long started=Stopwatch.GetTimestamp();
  try{
   if(!EcoreMainloop.IsMainThread)throw new Exception("Buffered presentation called off UI thread");
   using(var lease=pool.Take()){
    if(lease==null||!playing||stop||!ReferenceEquals(Volatile.Read(ref activeVideoFrames),pool))return;
    if(pw!=lease.Width||ph!=lease.Height){Screen.evas_object_image_size_set(image,lease.Width,lease.Height);pw=lease.Width;ph=lease.Height;}
    Screen.evas_object_image_data_copy_set(image,lease.Data);
    Screen.evas_object_image_data_update_add(image,0,0,lease.Width,lease.Height);Screen.evas_object_show(image);if(options.Overlay==0)label.Hide();Interlocked.Increment(ref presented);
    if(presented==1)Log("First buffered frame submitted: "+lease.Width+" x "+lease.Height);
   }
  }catch(Exception e){RequestGameStop("Buffered presentation failed: "+e.Message);}finally{Interlocked.Add(ref presentWork,Stopwatch.GetTimestamp()-started);}
 }
 void FinishBufferedVideo(VideoFramePool pool){if(pool==null)return;Log(pool.Summary);pool.ProducerCompleted();pool.Dispose();if(ReferenceEquals(Volatile.Read(ref activeVideoFrames),pool))Volatile.Write(ref activeVideoFrames,null);}
}
