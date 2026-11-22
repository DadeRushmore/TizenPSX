#ifndef TIZENPS_PERFORMANCE_OPTIONS_H
#define TIZENPS_PERFORMANCE_OPTIONS_H
#include <string.h>
/* Versioned frontend ABI. Invalid indexes fall back to compatibility defaults. */
static const char *performance_option(unsigned flags,const char *key) {
 unsigned skip=flags&3, cpu=(flags>>4)&7, interpolation=(flags>>8)&3;
 static const char *clocks[]={"auto","50","57","75","100"};
 static const char *intervals[]={"0","1","2"};
 static const char *filters[]={"gaussian","simple","off"};
 if(!strcmp(key,"pcsx_rearmed_drc"))return flags&4096?"enabled":"disabled";
 if(!strcmp(key,"pcsx_rearmed_neon_enhancement_enable"))return flags&8192?"enabled":"disabled";
 if(!strcmp(key,"pcsx_rearmed_neon_enhancement_no_main"))return "disabled";
 if(!strcmp(key,"pcsx_rearmed_neon_enhancement_tex_adj_v2"))return "disabled";
 if(skip>2)skip=0;if(cpu>4)cpu=0;if(interpolation>2)interpolation=0;
 if(!strcmp(key,"pcsx_rearmed_frameskip_type"))return skip?"fixed_interval":"disabled";
 if(!strcmp(key,"pcsx_rearmed_frameskip_interval"))return intervals[skip];
 if(!strcmp(key,"pcsx_rearmed_psxclock"))return clocks[cpu];
 if(!strcmp(key,"pcsx_rearmed_spu_interpolation"))return filters[interpolation];
 if(!strcmp(key,"pcsx_rearmed_spu_reverb"))return flags&1024?"disabled":"enabled";
 return 0;
}
#endif
