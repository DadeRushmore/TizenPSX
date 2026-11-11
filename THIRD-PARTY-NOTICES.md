# Licensing and attribution

TizenPS application, C# ELF loader, native host bridge, tests and original UI artwork are distributed under GNU GPL version 2; see [LICENSE](LICENSE). This continues the licensing recorded in the original application documentation. Third-party files retain their own notices and licenses. No license here grants rights to Sony/Samsung trademarks, game artwork, games or BIOS images.

## Emulator and Tizen integration

PCSX ReARMed is the work of the **PCSX Team, notaz, Exophase**, the PCSX-Reloaded/PCSX-df contributors, and the contributors named in its source notices. TizenPS uses the [Libretro fork](https://github.com/libretro/pcsx_rearmed), baseline `7b0e8f8fadfbce3d170d195b9d9eecaabfa08c8e`, with the supplied Tizen host changes. Its source and GPLv2 license are in `pcsx-rearmed-native/` and `pcsx-rearmed-native/COPYING`.

Our `DynamicElf.cs` parses ARM32 ELF segments and relocations. `Native.cs` resolves imports against TV system libraries, maps the image with segment permissions and calls `retro_player_run`. The native host connects emulator video/audio/input callbacks to the C# frontend. Native display uses Evas, audio uses PulseAudio, and controller handling uses the platform interfaces already present in the port. This is not a claim that we wrote the emulation core.

## Other components

| Component | Origin and license | Included material |
|---|---|---|
| Overscan ABI fixture | Apps2Samsung/Overscan, Patrick Stel, MIT; revision `bf9b194c9c3d28574cafaa3294fcda6465da61d0` | `libovprobe.so`, notice in `roelofs-psx-native/res/LICENSE-Overscan.txt`; used as a known-size fixture file by the current host |
| Zig | Zig contributors, MIT | Compiler is an external build prerequisite; notice in `res/LICENSE-Zig.txt` |
| GNU C Library wrappers/headers | GNU C Library contributors, LGPL-2.1-or-later and per-file notices | Corresponding subset in `toolchain-glibc-source/`; license in `LICENSES/LGPL-2.1.txt`, additional notices in `toolchain-glibc-source/LICENSES`; copies included in app resources |
| PCSX bundled dependencies | libretro-common, miniz, libchdr and other vendored components | Retain their individual source notices. Inclusion in the source tree does not mean every dependency is linked into this build; consult `tizen-build/build-core.py` |
| Newtonsoft.Json | James Newton-King, MIT | Existing notice retained in `res/LICENSE-Newtonsoft.txt` |
| Tizen .NET / ElmSharp | Official SDK/package prerequisites | Referenced by version in `Player.csproj`; do not add installed SDK directories or signing credentials to this repository |

The original 4096-byte red-screen homebrew demo is generated from the instruction words in `roelofs-psx-native/native/homebrew-words.json`; it is not a commercial game or BIOS.

The 0.3.38 source distribution also includes the upstream Newtonsoft.Json 10.0.3 and pinned Overscan source archives under `third-party-source/`, with their download URLs and hashes. Newtonsoft.Json.dll is the only third-party managed assembly copied into the TPK. The Overscan fixture is 5012 bytes; it is inspected as a known-size file, not executed as an emulator. Original relocation-test fixture source and binary are under `roelofs-native-probe/`.

Public beta distribution revision: 2026-11-11. Version strings and default artwork/notices differ from the private 0.3.37 build. The native core SHA-256 is unchanged and has been reproduced from the provided source. Original project artwork replaces personal covers and official emblems.

## Distribution scope

Provide the complete corresponding source, host changes, scripts, notices and build instructions alongside any distributed native package. GPL licensing does not establish that every third-party asset is cleared. This document records provenance, not a blanket legal clearance.
