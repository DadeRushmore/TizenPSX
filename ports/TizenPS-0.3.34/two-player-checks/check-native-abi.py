"""Execute the actual v4 wrapper on the host, with a stub game loop."""
from pathlib import Path
import json,os,subprocess
r=Path(__file__).resolve().parents[1]
source=(r/'pcsx-rearmed-native/tizen-build/probe-host.c').read_text()
wrapper=source[source.index('int retro_player_run_v4('):]
test=r/'two-player-checks/native-abi.c'
test.write_text('''#include <assert.h>
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
'''+wrapper+'''
int main(void){
 for(int buffered=0;buffered<2;buffered++)for(int error=0;error<2;error++){
  result=error?-4:0;
  assert(retro_player_run_v4(0,0,0,one,"","","","",0,0,0,buffered?acquire:0,two)==result);
  assert(!live_control2&&!live_acquire&&!player_samples[0]&&!player_samples[1]);
 }
 assert(retro_player_run_v4(0,0,0,one,"","","","",0,0,0,0,0)==-6);
 return 0;
}
''')
repo=r.parents[1];zig=json.loads((repo/'build/libtizenpsp_triangle.so.json').read_text())['command'][0]
env=dict(os.environ,ZIG_GLOBAL_CACHE_DIR=str(repo/'.cache/zig-global'),ZIG_LOCAL_CACHE_DIR=str(repo/'.cache/zig-local'))
exe=test.with_suffix('.exe');subprocess.run([zig,'cc',str(test),'-o',str(exe)],env=env,check=True);subprocess.run([str(exe)],check=True)
print('PASS actual v4 wrapper: separate callbacks, both video paths, success/failure cleanup, null player-2 rejection')
