#include <assert.h>
#include <string.h>
#include "../pcsx-rearmed-native/tizen-build/performance-options.h"
int main(void) {
  assert(!strcmp(performance_option(0,"pcsx_rearmed_neon_enhancement_enable"),"disabled"));
  assert(!strcmp(performance_option(8192,"pcsx_rearmed_neon_enhancement_enable"),"enabled"));
  assert(!strcmp(performance_option(8192,"pcsx_rearmed_drc"),"disabled"));
  assert(!strcmp(performance_option(13568,"pcsx_rearmed_drc"),"enabled"));
  assert(!strcmp(performance_option(~0u,"pcsx_rearmed_neon_enhancement_no_main"),"disabled"));
  assert(!strcmp(performance_option(~0u,"pcsx_rearmed_neon_enhancement_tex_adj_v2"),"disabled"));
  assert(performance_option(8192,"not_an_option")==0);
  return 0;
}
