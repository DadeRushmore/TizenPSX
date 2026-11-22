using System;

// Independent stereo slowdown resampler. Input and output callers hold the same
// managed lock; no firmware calls occur under this lock. Lower speed lowers pitch.
internal sealed class AudioResampler {
 internal const int Rate=44100,Capacity=16384;
 readonly short[] ring=new short[Capacity*2];
 int read,count,fade;
 double position,first=-1,windowStart,windowFrames,production=1,speed=1;
 short lastLeft,lastRight;
 bool primed,everPrimed;
 internal long InputFrames,OutputFrames,DroppedFrames,MissingFrames,Underruns;
 internal int BufferedFrames=>count;
 internal double Speed=>speed;
 internal double BufferedMs=>1000.0*count/Rate;
 internal void Push(short[] samples,int offset,int frames,double now){
  if(samples==null||offset<0||frames<0||(long)offset+2L*frames>samples.Length)throw new ArgumentOutOfRangeException(nameof(frames));
  if(frames==0)return;
  if(first<0){first=windowStart=now;}
  InputFrames+=frames;windowFrames+=frames;
  if(frames>Capacity){int discard=frames-Capacity;offset+=discard*2;frames=Capacity;DroppedFrames+=discard;}
  int overflow=Math.Max(0,count+frames-Capacity);
  if(overflow>0){read=(read+overflow)%Capacity;count-=overflow;DroppedFrames+=overflow;position=0;fade=64;}
  for(int i=0;i<frames;i++){int index=((read+count+i)%Capacity)*2;ring[index]=samples[offset+i*2];ring[index+1]=samples[offset+i*2+1];}
  count+=frames;
 }
 internal void Render(short[] output,int frames,double now){
  if(output==null||frames<0||(long)frames*2>output.Length)throw new ArgumentOutOfRangeException(nameof(frames));
  if(first>=0&&now-windowStart>=.25){
   double measured=windowFrames/((now-windowStart)*Rate);
   production=Math.Max(.15,Math.Min(1.03,measured));
   windowFrames=0;windowStart=now;
  }
  double targetFrames=Math.Max(441,production*Rate*.09);
  bool ready=first>=0&&now-first>=.30&&count>=Math.Max(441,targetFrames*.5);
  if(!primed&&ready){primed=everPrimed=true;fade=64;speed=production;}
  // Near-full-speed correction is deliberately small; it is not a fast-forward mode.
  double correction=Math.Max(-.04,Math.Min(.04,(count-targetFrames)/(Rate*.5)));
  double wanted=Math.Max(.15,Math.Min(1.05,production+correction));
  speed+=(wanted-speed)*Math.Min(1,frames/(Rate*.12));
  for(int i=0;i<frames;i++){
   if(primed&&count<2){primed=false;position=0;Underruns++;}
   short left=0,right=0;
   if(primed){
    int a=read*2,b=((read+1)%Capacity)*2;
    double gain=fade>0?(64-fade--)/64.0:1;
    left=(short)Math.Round((ring[a]+(ring[b]-ring[a])*position)*gain);
    right=(short)Math.Round((ring[a+1]+(ring[b+1]-ring[a+1])*position)*gain);
    position+=speed;
    int consume=Math.Min((int)position,count-1);read=(read+consume)%Capacity;count-=consume;position-=consume;
   }else{
    if(everPrimed)MissingFrames++;
    // Short decay prevents a discontinuity when the producer really stops.
    left=(short)(lastLeft*.85);right=(short)(lastRight*.85);
   }
   output[i*2]=lastLeft=left;output[i*2+1]=lastRight=right;
  }
  OutputFrames+=frames;
 }
}
