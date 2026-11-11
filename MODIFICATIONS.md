# Modified-source notice — 0.3.38 beta

This is a modified distribution of libretro/pcsx_rearmed baseline
`7b0e8f8fadfbce3d170d195b9d9eecaabfa08c8e`, distributed 2026-11-11.
Original authorship and licenses are retained. The changes predate this public
packaging revision; the date above identifies this distribution.

Tizen host integration and build files are added under `pcsx-rearmed-native/tizen-build/`.
They connect video, audio, input, files, memory cards and performance options to
the managed TV frontend. ARM dynarec and memory integration changes are included.
`include/revision.h` supplies the build revision. Two unused upstream CHD helper
scripts use a relative import path instead of a developer-specific absolute path.

Compared against the pinned upstream source archive, these existing files differ:

- `pcsx-rearmed-native/frontend/libretro.c`
- `pcsx-rearmed-native/libpcsxcore/r3000a.c`
- `pcsx-rearmed-native/libpcsxcore/new_dynarec/assem_arm.c`
- `pcsx-rearmed-native/libpcsxcore/new_dynarec/emu_if.c`
- `pcsx-rearmed-native/libpcsxcore/new_dynarec/new_dynarec.c`
- `pcsx-rearmed-native/libpcsxcore/new_dynarec/new_dynarec.h`
- `pcsx-rearmed-native/libpcsxcore/new_dynarec/pcsxmem.c`
- `pcsx-rearmed-native/libpcsxcore/new_dynarec/pcsxmem.h`
- `pcsx-rearmed-native/deps/libretro-common/tools/chd/avhuff_decode.py`
- `pcsx-rearmed-native/deps/libretro-common/tools/chd/chd_cd_test.py`

The managed app, loader and its tests are supplied under `roelofs-psx-native/`
and the check directories. For 0.3.38, version strings, original artwork and
distribution notices were updated; emulator and controller behavior were retained.
The UI preview adapter now follows the application's placeholder fallback, and
the older library test project includes its current helper files and 42-title catalog.

Unneeded upstream frontend image assets and the optional precompiled C64x DSP
binary are omitted from the public source export. Their code remains present;
neither is used to build the Tizen native core. Generated caches, temporary test
files, personal artwork and private signing material are also excluded.
