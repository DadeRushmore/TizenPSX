#include <stdint.h>
/* Shared with host checks: no key/axis data may cross a PS1 player port. */
static int16_t player_input(const uint64_t samples[2], unsigned port, unsigned device, unsigned index, unsigned id) {
 if(port>=2)return 0;
 if(device==RETRO_DEVICE_JOYPAD&&id<16)return (samples[port]>>id)&1;
 if(device==RETRO_DEVICE_ANALOG&&index<=1&&id<=1){
  int8_t raw=(int8_t)((samples[port]>>(32+8*(index*2+id)))&255u);
  return (int16_t)((int)raw*258);
 }
 return 0;
}
