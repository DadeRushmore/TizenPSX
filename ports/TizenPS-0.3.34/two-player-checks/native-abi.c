#include <assert.h>
#include <stdint.h>
static uint64_t (*live_control2)(void);
static void *(*live_acquire)(unsigned,unsigned);
static uint64_t player_samples[2];
static int result;
static uint64_t one(void){return 256;}
static uint64_t two(void){return 512;}
static void *acquire(unsigned w,unsigned h){return 0;}
int retro_player_run_v2(void (*logger)(const char*),void (*video)(const void*,unsigned,unsigned),void (*audio)(const void*,unsigned),uint64_t (*control)(void),const char *fixture,const char *game,const char *bios,const char *save,int dual,unsigned options,void (*metrics)(unsigned,unsigned,unsigned,unsigned)){
 assert(control()==256);assert(live_control2&&live_control2()==512);
 player_samples[0]=control();player_samples[1]=live_control2();return result;
}
int retro_player_run_v4(void (*logger)(const char*),void (*video_cb)(const void*,unsigned,unsigned),void (*audio_cb)(const void*,unsigned),uint64_t (*control_cb)(void),const char *fixture,const char *game_path,const char *bios_dir,const char *save_path,int dualshock_mode,unsigned options,void (*metrics)(unsigned,unsigned,unsigned,unsigned),void *(*acquire)(unsigned,unsigned),uint64_t (*control2_cb)(void)){
 if(!control2_cb)return -6;
 live_control2=control2_cb;live_acquire=acquire;
 int result=retro_player_run_v2(logger,video_cb,audio_cb,control_cb,fixture,game_path,bios_dir,save_path,dualshock_mode,options,metrics);
 live_control2=0;live_acquire=0;player_samples[0]=player_samples[1]=0;
 return result;
}

int main(void){
 for(int buffered=0;buffered<2;buffered++)for(int error=0;error<2;error++){
  result=error?-4:0;
  assert(retro_player_run_v4(0,0,0,one,"","","","",0,0,0,buffered?acquire:0,two)==result);
  assert(!live_control2&&!live_acquire&&!player_samples[0]&&!player_samples[1]);
 }
 assert(retro_player_run_v4(0,0,0,one,"","","","",0,0,0,0,0)==-6);
 return 0;
}
