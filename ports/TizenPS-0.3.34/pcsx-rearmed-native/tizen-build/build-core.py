import os,subprocess,concurrent.futures,json
from pathlib import Path
# Keep upstream's diagnostic build-time string reproducible across source exports.
os.environ['SOURCE_DATE_EPOCH']='0'
root=Path(__file__).resolve().parents[1]
tools=root.parent/'native-build-tools'
zig=Path(os.environ.get('TIZENPS_ZIG', str(tools/'zig/zig-windows-x86_64-0.13.0/zig.exe')))
cache=root/'tizen-build/.cache'
cache.mkdir(parents=True,exist_ok=True)
os.environ['ZIG_GLOBAL_CACHE_DIR']=str(cache/'global');os.environ['ZIG_LOCAL_CACHE_DIR']=str(cache/'local')
core="cdriso cdrom cdrom-async cheat database decode_xa mdec misc plugins ppf psxbios psxcommon psxcounters psxdma psxhw psxinterpreter psxmem psxevents r3000a sio spu gpu pad gte gte_nf gte_divider".split()
sources=['libpcsxcore/'+n+'.c' for n in core]+['libpcsxcore/new_dynarec/emu_if.c']
sources+=['plugins/dfsound/'+n+'.c' for n in 'dma freeze registers spu out nullsnd'.split()]
sources+=['plugins/gpulib/'+n+'.c' for n in 'gpu vout_pl prim'.split()]
sources+=['plugins/gpu_neon/psx_gpu_if.c','plugins/gpu_neon/psx_gpu/psx_gpu_arm_neon.S','deps/libchdr/deps/miniz-3.1.2/miniz.c']
sources+=['frontend/'+n+'.c' for n in 'cspace libretro main plugin'.split()]
sources+=['tizen-build/probe-host.c','libpcsxcore/new_dynarec/new_dynarec.c','libpcsxcore/new_dynarec/pcsxmem.c','libpcsxcore/new_dynarec/linkage_arm.S']
sources+=['libpcsxcore/gte_arm.S','libpcsxcore/gte_neon.S','libpcsxcore/gte_nf_neon.S','libpcsxcore/gte_nf_arm.S','plugins/dfsound/arm_utils.S','frontend/cspace_arm.S']
base=[str(zig),'cc','-target','arm-linux-gnueabi.2.17','-mcpu=cortex_a72','-marm','-O3','-fPIC','-fno-stack-protector','-fno-unwind-tables','-fno-asynchronous-unwind-tables','-ffunction-sections','-fdata-sections','-fno-strict-aliasing','-Wno-incompatible-pointer-types','-Wno-int-conversion','-Iinclude','-Ideps/libretro-common/include','-Ideps/libchdr/deps/miniz-3.1.2','-DP_HAVE_MMAP=1','-DP_HAVE_POSIX_MEMALIGN=1','-DDISABLE_MEM_LUTS=0','-DTIZEN_DYNAREC=1','-DBASE_ADDR_DYNAMIC=1','-DGPU_NEON','-DUSE_MINIZ','-DHAVE_LIBRETRO','-DNO_FRONTEND','-DNDEBUG','-D_GNU_SOURCE']
base += ['-g0', '-ffile-prefix-map='+str(root.parent)+'=/src/tizenps', '-fdebug-prefix-map='+str(root.parent)+'=/src/tizenps', '-Wno-builtin-macro-redefined', '-D__DATE__="Jan  1 1970"', '-D__TIME__="00:00:00"']
build=root/'tizen-build';objects=[];commands=[]
def compile_one(src):
 obj=build/(src.replace('/','_')+'.o');cmd=base+(['-Xclang','-O3'] if src.endswith('.c') else [])+['-c',src,'-o',str(obj)]
 if src in ['frontend/main.c','libpcsxcore/misc.c']:cmd+=['-DBUILTIN_GPU=neon']
 if src=='plugins/gpu_neon/psx_gpu_if.c':cmd+=['-DNEON_BUILD','-DTEXTURE_CACHE_4BPP','-DTEXTURE_CACHE_8BPP']
 if src=='frontend/cspace.c':cmd+=['-DHAVE_bgr555_to_rgb565']
 if src.endswith('miniz.c'):cmd+=['-DMINIZ_NO_STDIO','-DMINIZ_NO_DEFLATE_APIS','-DMINIZ_NO_ARCHIVE_APIS']
 commands.append(cmd)
 inputs=[root/src,Path(__file__)]+list((root/'plugins/gpu_neon').rglob('*.h'))
 if src=='tizen-build/probe-host.c':inputs.extend([build/'performance-options.h',build/'player-input.h'])
 if obj.exists() and obj.stat().st_mtime>max(p.stat().st_mtime for p in inputs):return obj
 p=subprocess.run(cmd,cwd=root,capture_output=True,text=True)
 if p.returncode:raise RuntimeError(src+'\n'+p.stderr)
 print('compiled '+src,flush=True);return obj
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:objects=list(pool.map(compile_one,sources))
# Generate target-specific glibc nonshared wrappers through Zig's normal libc link.
check=build/'libc-link-check.c'
check.write_text('#include <sys/stat.h>\nint check(const char *p,struct stat *s){return stat(p,s); }\n')
subprocess.run([str(zig),'cc','-target','arm-linux-gnueabi.2.17','-mcpu=cortex_a72','-shared','-fPIC','-o',str(build/'libc-link-check.so'),str(check)],cwd=root,check=True)
archives=list((cache/'global/o').glob('*/libc_nonshared.a'))
if len(archives)!=1:raise RuntimeError('Expected one target-specific libc_nonshared.a; use a clean dedicated cache')
cmd=base[:9]+['-shared','-nostdlib','-Wl,--hash-style=sysv','-Wl,-z,max-page-size=4096','-Wl,--gc-sections','-Wl,--strip-debug','-Wl,-soname,libpcsx_tizen.so','-Wl,--version-script=frontend/libretro-version-script','-o',str(build/'libpcsx_tizen.so')]+[str(p) for p in objects]+[str(archives[0])]
commands.append(cmd);(build/'commands.json').write_text(json.dumps(commands,indent=2))
subprocess.run(cmd,cwd=root,check=True)
print('BUILT '+str(build/'libpcsx_tizen.so'))
