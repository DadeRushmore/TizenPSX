#include <assert.h>
#include "../pcsx-rearmed-native/deps/libretro-common/include/libretro.h"
#include "../pcsx-rearmed-native/tizen-build/player-input.h"
int main(void){
 uint64_t p[2]={((uint64_t)127<<32)|256,((uint64_t)129<<32)|512};
 assert(player_input(p,0,RETRO_DEVICE_JOYPAD,0,8)==1);
 assert(player_input(p,0,RETRO_DEVICE_JOYPAD,0,9)==0);
 assert(player_input(p,1,RETRO_DEVICE_JOYPAD,0,8)==0);
 assert(player_input(p,1,RETRO_DEVICE_JOYPAD,0,9)==1);
 assert(player_input(p,0,RETRO_DEVICE_ANALOG,0,0)==32766);
 assert(player_input(p,1,RETRO_DEVICE_ANALOG,0,0)==-32766);
 for(unsigned port=0;port<2;port++)for(unsigned bit=0;bit<16;bit++){p[port]=1ULL<<bit;p[1-port]=0;for(unsigned id=0;id<16;id++){assert(player_input(p,port,RETRO_DEVICE_JOYPAD,0,id)==(id==bit));assert(player_input(p,1-port,RETRO_DEVICE_JOYPAD,0,id)==0);}}
 p[0]=p[1]=0;assert(player_input(p,0,RETRO_DEVICE_JOYPAD,0,8)==0);assert(player_input(p,1,RETRO_DEVICE_ANALOG,0,0)==0);assert(player_input(p,2,RETRO_DEVICE_JOYPAD,0,8)==0);
 return 0;
}
