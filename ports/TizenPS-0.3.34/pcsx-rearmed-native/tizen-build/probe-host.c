#include "libretro.h"
#include "performance-options.h"
#include "player-input.h"
static uint64_t player_samples[2];
static uint64_t (*live_control2)(void);
#include "../libpcsxcore/r3000a.h"
static unsigned live_options;
static unsigned last_video_width,last_video_height;
static unsigned live_video_received,live_video_converted,live_video_dropped;
static double live_video_cost,live_audio_cost;
static double monotonic_seconds(void);
#include <stdarg.h>
#include <stdio.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>
#include <errno.h>
#include <stdlib.h>
#include <time.h>
#include <stdint.h>
static void (*report)(const char *);
static void log_message(enum retro_log_level level,const char *format,...) {
 char text[768];va_list ap;(void)level;va_start(ap,format);vsnprintf(text,sizeof(text),format,ap);va_end(ap);if(report)report(text);
}
/* Libretro's DualShock subclass ID is defined privately in the core frontend. */
#define RETRO_DEVICE_PSE_DUALSHOCK RETRO_DEVICE_SUBCLASS(RETRO_DEVICE_ANALOG, 1)
extern int padToggleAnalog(unsigned int index);
/* Native entry points remain imports; stat/fstat are local ABI adapters. */
extern int __xstat(int,const char *,struct stat *);
extern int __fxstat(int,int,struct stat *);
static int selected_stat_version=-1;
int stat(const char *path,struct stat *buf){if(selected_stat_version<0){errno=EINVAL;return -1;}return __xstat(selected_stat_version,path,buf);}
int fstat(int fd,struct stat *buf){if(selected_stat_version<0){errno=EINVAL;return -1;}return __fxstat(selected_stat_version,fd,buf);}
union stat_probe_buffer {struct stat value;unsigned char raw[1024];};
static int intact(const union stat_probe_buffer *buf){size_t i;for(i=sizeof(struct stat);i<sizeof(*buf);i++)if(buf->raw[i]!=0xa5)return 0;return 1;}
static int choose_stat_abi(const char *path,unsigned expected_size){
 const int versions[]={0,3};unsigned i;FILE *fp=fopen(path,"rb");int fd;
 if(!fp){log_message(RETRO_LOG_ERROR,"Cannot open ABI fixture: errno=%d",errno);return 0;}fd=fileno(fp);
 selected_stat_version=-1;
 log_message(RETRO_LOG_INFO,"Core stat layout: sizeof=%u size@%u mode@%u",(unsigned)sizeof(struct stat),(unsigned)offsetof(struct stat,st_size),(unsigned)offsetof(struct stat,st_mode));
 for(i=0;i<2;i++){
  union stat_probe_buffer a,b;int ra,rb,ea,eb,ok;
  memset(&a,0xa5,sizeof(a));memset(&b,0xa5,sizeof(b));
  errno=0;ra=__xstat(versions[i],path,&a.value);ea=errno;
  errno=0;rb=__fxstat(versions[i],fd,&b.value);eb=errno;
  ok=ra==0&&rb==0&&intact(&a)&&intact(&b)&&S_ISREG(a.value.st_mode)&&S_ISREG(b.value.st_mode)&&a.value.st_size==expected_size&&b.value.st_size==expected_size&&a.value.st_ino!=0&&a.value.st_ino==b.value.st_ino&&a.value.st_dev==b.value.st_dev&&a.value.st_mode==b.value.st_mode&&a.value.st_nlink>0&&a.value.st_nlink==b.value.st_nlink&&a.value.st_uid==b.value.st_uid&&a.value.st_gid==b.value.st_gid&&a.value.st_mtime==b.value.st_mtime;
  log_message(RETRO_LOG_INFO,"ver=%d stat=%d errno=%d | fstat=%d errno=%d | layout %s",versions[i],ra,ra?ea:0,rb,rb?eb:0,ok?"PASS":"FAIL");
  if(ok&&selected_stat_version<0)selected_stat_version=versions[i];
 }
 fclose(fp);
 if(selected_stat_version<0)return 0;
 log_message(RETRO_LOG_INFO,"Selected stat ABI version %d",selected_stat_version);return 1;
}
static void *(*live_acquire)(unsigned,unsigned);
static void (*live_video)(const void*,unsigned,unsigned);static void (*live_audio)(const void*,unsigned);static uint64_t (*live_control)(void);static uint64_t live_sample;static unsigned live_keys;static const char *live_bios_dir;static unsigned *live_pixels;
static unsigned frames,unique_frames,audio_frames,width,height;static unsigned char *pixels;static int format=RETRO_PIXEL_FORMAT_0RGB1555;
static double monotonic_seconds(void){struct timespec t;clock_gettime(CLOCK_MONOTONIC,&t);return t.tv_sec+t.tv_nsec/1e9;}
static bool environment(unsigned cmd,void *data) {
 switch(cmd){
 case RETRO_ENVIRONMENT_GET_LOG_INTERFACE: ((struct retro_log_callback*)data)->log=log_message;return true;
 case RETRO_ENVIRONMENT_GET_SYSTEM_DIRECTORY: if(!live_bios_dir)return false;*(const char**)data=live_bios_dir;return true;
 case RETRO_ENVIRONMENT_GET_CAN_DUPE: *(bool*)data=true;return true;
 case RETRO_ENVIRONMENT_GET_VARIABLE: {
  struct retro_variable *v=data;
  if(!strcmp(v->key,"pcsx_rearmed_rgb32_output")){v->value="enabled";return true;}
  const char *option=performance_option(live_options,v->key);if(option){v->value=option;return true;}
  if(!strcmp(v->key,"pcsx_rearmed_bios")){v->value=live_bios_dir?"auto":"HLE";return true;}
  if(!strcmp(v->key,"pcsx_rearmed_analog_combo")){v->value="l3+r3";return true;}
  if(!strcmp(v->key,"pcsx_rearmed_memcard1")){v->value="libretro";return true;}
  if(!strcmp(v->key,"pcsx_rearmed_memcard2")){v->value="none";return true;}return false;
 }
 case RETRO_ENVIRONMENT_SET_PIXEL_FORMAT: if(*(unsigned*)data>2)return false;format=*(unsigned*)data;return true;
 default:return false;
 }
}

static void video(const void *p,unsigned w,unsigned h,size_t pitch){
 unsigned x,y;frames++;
 if(live_video){
  if(!p||!w||!h||w>1024||h>512||pitch<w*(format==RETRO_PIXEL_FORMAT_XRGB8888?4:2))return;
  if(w!=last_video_width||h!=last_video_height){log_message(RETRO_LOG_INFO,"VIDEO OUTPUT: %u x %u | NEON %s",w,h,live_options&8192?"2x requested":"native");last_video_width=w;last_video_height=h;}
  live_video_received++;
  if((live_options&2048)&&live_video_received%2==0){live_video_dropped++;return;}
  double video_start=monotonic_seconds();
  unsigned *target=live_acquire?live_acquire(w,h):live_pixels;
  if(!target){live_video_dropped++;return;}
  live_video_converted++;
  for(y=0;y<h;y++)for(x=0;x<w;x++){unsigned v,r,g,b;
   if(format==RETRO_PIXEL_FORMAT_XRGB8888)v=((const unsigned*)((const char*)p+y*pitch))[x];
   else{v=((const unsigned short*)((const char*)p+y*pitch))[x];r=((v>>(format==RETRO_PIXEL_FORMAT_RGB565?11:10))&31)*255/31;g=((v>>5)&(format==RETRO_PIXEL_FORMAT_RGB565?63:31))*255/(format==RETRO_PIXEL_FORMAT_RGB565?63:31);b=(v&31)*255/31;v=(r<<16)|(g<<8)|b;}
   target[y*w+x]=v|0xff000000u;
  }live_video(target,w,h);live_video_cost+=monotonic_seconds()-video_start;return;
 }
 if(!p||w>640||h>512||!w||!h||!pixels)return;
 if(pitch<w*(format==RETRO_PIXEL_FORMAT_XRGB8888?4:2))return;
 width=w;height=h;unique_frames++;
 for(y=0;y<h;y++)for(x=0;x<w;x++){
  unsigned v;unsigned char *d=pixels+(y*w+x)*3;
  if(format==RETRO_PIXEL_FORMAT_XRGB8888){v=((const unsigned*)((const char*)p+y*pitch))[x];d[0]=v;d[1]=v>>8;d[2]=v>>16;}
  else{v=((const unsigned short*)((const char*)p+y*pitch))[x];d[0]=(v&31)*255/31;d[1]=((v>>5)&(format==RETRO_PIXEL_FORMAT_RGB565?63:31))*255/(format==RETRO_PIXEL_FORMAT_RGB565?63:31);d[2]=((v>>(format==RETRO_PIXEL_FORMAT_RGB565?11:10))&31)*255/31;}
 }
}
static void put32(unsigned char *p,unsigned v){p[0]=v;p[1]=v>>8;p[2]=v>>16;p[3]=v>>24;}
static int snapshot(const char *path){
 unsigned stride=(width*3+3)&~3u,y;unsigned char head[54]={0},pad[3]={0};FILE *fp;if(!unique_frames)return 0;
 head[0]='B';head[1]='M';put32(head+2,54+stride*height);put32(head+10,54);put32(head+14,40);put32(head+18,width);put32(head+22,height);head[26]=1;head[28]=24;
 fp=fopen(path,"wb");if(!fp)return 0;if(fwrite(head,1,54,fp)!=54){fclose(fp);return 0;}
 for(y=height;y>0;y--){if(fwrite(pixels+(y-1)*width*3,1,width*3,fp)!=width*3||fwrite(pad,1,stride-width*3,fp)!=stride-width*3){fclose(fp);return 0;}}
 return fclose(fp)==0;
}
static size_t audio(const int16_t *p,size_t count){audio_frames+=count;if(live_audio){double start=monotonic_seconds();live_audio(p,(unsigned)count);live_audio_cost+=monotonic_seconds()-start;}return count;}
static void poll(void){}
static int16_t input(unsigned port,unsigned device,unsigned index,unsigned id){
 return player_input(player_samples,port,device,index,id);
}
// Synchronous initialization-only host. Never loads a game or calls retro_run.
int retro_probe_initialize(void (*callback)(const char *), const char *test_file, unsigned expected_size, const char *game_path,const char *output_path) {
 struct retro_system_info info;unsigned char *ram;size_t bytes;int result=0;
 unsigned int saved_fpscr;__asm__ volatile("vmrs %0, fpscr":"=r"(saved_fpscr));
 report=callback;
 if(!choose_stat_abi(test_file,expected_size)){report("No verified stat ABI; core not initialized");report=0;return -2;}
 {struct stat a,b;FILE *fp;int ra,rb;ra=stat(test_file,&a);fp=fopen(test_file,"rb");rb=fp?fstat(fileno(fp),&b):-1;if(fp)fclose(fp);
  if(ra||rb||a.st_size!=expected_size||b.st_size!=expected_size){report("Stat adapter self-check failed");report=0;return -2;}
  report("stat/fstat adapter: PASS");}
 retro_get_system_info(&info);log_message(RETRO_LOG_INFO,"Core: %s %s",info.library_name,info.library_version);
 log_message(RETRO_LOG_INFO,"libretro API: %u",retro_api_version());
 if(retro_api_version()!=RETRO_API_VERSION){report=0;return -1;}
 retro_set_environment(environment);retro_set_video_refresh(video);retro_set_audio_sample_batch(audio);retro_set_input_poll(poll);retro_set_input_state(input);
 report("Calling retro_init (interpreter; no game)");retro_init();report("retro_init returned");
 ram=retro_get_memory_data(RETRO_MEMORY_SYSTEM_RAM);bytes=retro_get_memory_size(RETRO_MEMORY_SYSTEM_RAM);
 if(ram && bytes==0x200000){unsigned char before=ram[0];ram[0]=0x5a;if(ram[0]==0x5a)result=1;ram[0]=before;}
 log_message(RETRO_LOG_INFO,"PSX RAM: %u bytes | read/write: %s",(unsigned)bytes,result?"PASS":"FAIL");
 {
  struct retro_game_info game={game_path,0,0,0};struct timespec start,end;unsigned n;double elapsed;int saved;
  frames=unique_frames=audio_frames=width=height=0;pixels=malloc(640*512*3);
  report("Loading bundled original PSX homebrew");
  if(!pixels||!retro_load_game(&game)){report("HOME BREW LOAD FAILED");result=-3;}
  else{
   report("Running up to 120 frames (unpaced benchmark)");clock_gettime(CLOCK_MONOTONIC,&start);
   for(n=0;n<120;n++){retro_run();if(n==0)report("First retro_run returned");clock_gettime(CLOCK_MONOTONIC,&end);if(end.tv_sec-start.tv_sec>=15){n++;break;}}
   elapsed=(end.tv_sec-start.tv_sec)+(end.tv_nsec-start.tv_nsec)/1000000000.0;
   log_message(RETRO_LOG_INFO,"Frames run=%u video=%u fresh=%u | %u x %u",n,frames,unique_frames,width,height);
   log_message(RETRO_LOG_INFO,"Interpreter benchmark: %u fps (simple homebrew)",elapsed>0?(unsigned)(n/elapsed):0);
   saved=snapshot(output_path);
   log_message(RETRO_LOG_INFO,"Snapshot: %s | audio callback frames=%u",saved?"SAVED":"FAILED",audio_frames);
   {unsigned red=0,i;for(i=0;i<width*height;i++)if(pixels[i*3+2]>180&&pixels[i*3+1]<60&&pixels[i*3]<60)red++;
    log_message(RETRO_LOG_INFO,"Red pixels: %u / %u",red,width*height);result=(saved&&red>width*height/2)?1:-4;}
   retro_unload_game();
  }
  free(pixels);pixels=0;
 }
 report("Calling retro_deinit");retro_deinit();report("retro_deinit returned");
 __asm__ volatile("vmsr fpscr, %0"::"r"(saved_fpscr));report=0;return result;
}

int retro_player_run_v2(void (*logger)(const char*),void (*video_cb)(const void*,unsigned,unsigned),void (*audio_cb)(const void*,unsigned),uint64_t (*control_cb)(void),const char *fixture,const char *game_path,const char *bios_dir,const char *save_path,int dualshock_mode,unsigned options,void (*metrics)(unsigned,unsigned,unsigned,unsigned)){
 struct retro_game_info game={game_path,0,0,0};struct retro_system_av_info av;struct timespec stamp;double deadline,period,now,started,last_audio,last_metrics,last_cost,run_cost=0,sleep_cost=0;unsigned ticks=0,key_frames=0,analog_frames=0,saved_fpscr;int loaded=0,result=0,save_ok=1;FILE *fp;void *card;size_t card_size;
 __asm__ volatile("vmrs %0, fpscr":"=r"(saved_fpscr));report=logger;live_options=options;last_video_width=last_video_height=0;live_video_received=live_video_converted=live_video_dropped=0;live_video_cost=live_audio_cost=0;
 if(!choose_stat_abi(fixture,5012)){report("File ABI mismatch");report=0;return -2;}
 live_bios_dir=(bios_dir&&*bios_dir)?bios_dir:0;live_video=video_cb;live_audio=audio_cb;live_control=control_cb;live_keys=0;live_sample=0;player_samples[0]=player_samples[1]=0;
 live_pixels=live_acquire?0:malloc(1024*512*4);if(!live_acquire&&!live_pixels){report("Video allocation failed");live_video=0;live_audio=0;live_control=0;live_bios_dir=0;report=0;return -3;}
 retro_set_environment(environment);retro_set_video_refresh(video);retro_set_audio_sample_batch(audio);retro_set_input_poll(poll);retro_set_input_state(input);
 log_message(RETRO_LOG_INFO,"Performance: skip=%s CPU=%s interpolation=%s reverb=%s",performance_option(options,"pcsx_rearmed_frameskip_interval"),performance_option(options,"pcsx_rearmed_psxclock"),performance_option(options,"pcsx_rearmed_spu_interpolation"),performance_option(options,"pcsx_rearmed_spu_reverb"));
 report(options&8192?"VIDEO REQUEST: NEON 2x Enhanced (speed hack OFF)":"VIDEO REQUEST: NEON Native");
 retro_init();report("Loading game...");loaded=retro_load_game(&game);
 if(!loaded){report("Game load failed");result=-4;goto done;}
 report(psxCpu==&psxRec?"CPU ACTIVE: ARM dynarec":"CPU ACTIVE: interpreter");
 retro_set_controller_port_device(0,dualshock_mode?RETRO_DEVICE_PSE_DUALSHOCK:RETRO_DEVICE_JOYPAD);
 retro_set_controller_port_device(1,live_control2?(dualshock_mode?RETRO_DEVICE_PSE_DUALSHOCK:RETRO_DEVICE_JOYPAD):RETRO_DEVICE_NONE);
 if(live_control2&&dualshock_mode)padToggleAnalog(1);
 if(dualshock_mode)padToggleAnalog(0); /* start DualShock with analog LED logically ON */
 report(dualshock_mode?"Controller: DualShock analog":"Controller: standard digital");
 card=retro_get_memory_data(RETRO_MEMORY_SAVE_RAM);card_size=retro_get_memory_size(RETRO_MEMORY_SAVE_RAM);
 if(card&&card_size==131072&&(fp=fopen(save_path,"rb"))){unsigned char *temp=malloc(card_size);if(temp){if(fread(temp,1,card_size,fp)==card_size&&fgetc(fp)==EOF)memcpy(card,temp,card_size);else {save_ok=0;report("Existing memory card invalid; preserving original without saving");}free(temp);}else save_ok=0;fclose(fp);}
 retro_get_system_av_info(&av);period=av.timing.fps>40&&av.timing.fps<70?1.0/av.timing.fps:1.0/60;
 clock_gettime(CLOCK_MONOTONIC,&stamp);deadline=stamp.tv_sec+stamp.tv_nsec/1e9;started=last_audio=last_metrics=last_cost=deadline;audio_frames=0;report("Playing. Return or hold Create + Options to stop and save.");
 while(1){unsigned before_audio=audio_frames;uint64_t control=live_control();if(control&0x10000ULL)break;live_sample=control;player_samples[0]=control;player_samples[1]=live_control2?live_control2():0;live_keys=(unsigned)control&65535;if(live_keys)key_frames++;if(dualshock_mode&&(live_sample>>32)!=0)analog_frames++;double run_start=monotonic_seconds();retro_run();run_cost+=monotonic_seconds()-run_start;ticks++;deadline+=period;clock_gettime(CLOCK_MONOTONIC,&stamp);now=stamp.tv_sec+stamp.tv_nsec/1e9;
  if(audio_frames!=before_audio)last_audio=now;
  if(metrics&&now-last_metrics>=1){metrics(ticks,audio_frames,(unsigned)((now-started)*1000),(unsigned)(1000.0/period+0.5));last_metrics=now;}
  if(now-last_cost>=5){log_message(RETRO_LOG_INFO,"PERF COST cumulative: run=%.3fs video=%.3fs audio=%.3fs sleep=%.3fs frames in=%u converted=%u dropped=%u",run_cost,live_video_cost,live_audio_cost,sleep_cost,live_video_received,live_video_converted,live_video_dropped);last_cost=now;}
  /* PulseAudio blocking writes are the clock while sound is active. Do not also sleep per frame. */
  if((control&0x20000)&&now-last_audio<0.25){deadline=now;continue;}
  if(deadline>now){double wait=deadline-now;struct timespec delay={(time_t)wait,(long)((wait-(time_t)wait)*1e9)};double sleep_start=monotonic_seconds();nanosleep(&delay,0);sleep_cost+=monotonic_seconds()-sleep_start;}else if(now-deadline>0.25)deadline=now;
 }
 clock_gettime(CLOCK_MONOTONIC,&stamp);now=stamp.tv_sec+stamp.tv_nsec/1e9;if(metrics)metrics(ticks,audio_frames,(unsigned)((now-started)*1000),(unsigned)(1000.0/period+0.5));log_message(RETRO_LOG_INFO,"Game speed: %u fps / target %u | frames %u",now>started?(unsigned)(ticks/(now-started)):0,(unsigned)(1/period+0.5),ticks);
 log_message(RETRO_LOG_INFO,"Input samples: buttons %u / %u frames, analog %u / %u frames",key_frames,ticks,analog_frames,ticks);
 if(save_ok&&card&&card_size==131072){char temporary[4096];if(snprintf(temporary,sizeof(temporary),"%s.tmp",save_path)<sizeof(temporary)&&(fp=fopen(temporary,"wb"))){int ok=fwrite(card,1,card_size,fp)==card_size;ok=(fclose(fp)==0)&&ok;if(ok&&rename(temporary,save_path)==0)report("Memory card saved");else report("Memory card save failed");}}
 done: if(loaded)retro_unload_game();retro_deinit();free(live_pixels);live_pixels=0;live_video=0;live_audio=0;live_control=0;live_bios_dir=0;report=0;__asm__ volatile("vmsr fpscr, %0"::"r"(saved_fpscr));return result;
}

/* Retain the old entry for tools/packages using the original ABI. */
int retro_player_run(void (*logger)(const char*),void (*video_cb)(const void*,unsigned,unsigned),void (*audio_cb)(const void*,unsigned),uint64_t (*control_cb)(void),const char *fixture,const char *game_path,const char *bios_dir,const char *save_path,int dualshock_mode){
 return retro_player_run_v2(logger,video_cb,audio_cb,control_cb,fixture,game_path,bios_dir,save_path,dualshock_mode,0,0);
}

/* v3 borrows frontend-owned writable slots; v2 retains its original video ABI. */
int retro_player_run_v3(void (*logger)(const char*),void (*video_cb)(const void*,unsigned,unsigned),void (*audio_cb)(const void*,unsigned),uint64_t (*control_cb)(void),const char *fixture,const char *game_path,const char *bios_dir,const char *save_path,int dualshock_mode,unsigned options,void (*metrics)(unsigned,unsigned,unsigned,unsigned),void *(*acquire)(unsigned,unsigned)){
 if(!acquire)return -5;
 live_acquire=acquire;
 int result=retro_player_run_v2(logger,video_cb,audio_cb,control_cb,fixture,game_path,bios_dir,save_path,dualshock_mode,options,metrics);
 live_acquire=0;
 return result;
}

/* v4 adds an independent player-2 callback. Older entry points remain intact. */
int retro_player_run_v4(void (*logger)(const char*),void (*video_cb)(const void*,unsigned,unsigned),void (*audio_cb)(const void*,unsigned),uint64_t (*control_cb)(void),const char *fixture,const char *game_path,const char *bios_dir,const char *save_path,int dualshock_mode,unsigned options,void (*metrics)(unsigned,unsigned,unsigned,unsigned),void *(*acquire)(unsigned,unsigned),uint64_t (*control2_cb)(void)){
 if(!control2_cb)return -6;
 live_control2=control2_cb;live_acquire=acquire;
 int result=retro_player_run_v2(logger,video_cb,audio_cb,control_cb,fixture,game_path,bios_dir,save_path,dualshock_mode,options,metrics);
 live_control2=0;live_acquire=0;player_samples[0]=player_samples[1]=0;
 return result;
}
