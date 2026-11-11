# 0.3.38 beta validation

## Evidence

- User reported the 0.3.36/0.3.37 application works well on their setup; no compatibility percentage is inferred.
- Fresh exported PCSX ReARMed source rebuilt using checksum-verified Zig 0.13.0. Its SHA-256 equals the working bundled core: `f4ddeada13923de8cfe6ee2cfd2e662fa0cedd2d8f1f58b7749d8d9fbb3e69ff`.
- The public candidate changes version strings and packaged artwork/notices. It does not modify emulator or controller behavior.
- Original emblem, title covers and current white wordmark regenerated. Existing procedural background/frame assets retained. Windows antivirus blocked the separate background regeneration helper; it was not bypassed.

- Managed Release build: zero warnings and errors.
- Library discovery/scanning checks: pass, including a 420-game fixture, artwork precedence, missing drives and persistence.
- UI/navigation checks: pass; nine native-layout host previews rendered with text bounds verified.
- Player/controller, DualSense, settings migration, audio and ELF relocation checks: pass. Isolated test harnesses emit unused-field warnings; the release application does not.
- The older library check project was repaired to reference current helpers and metadata and to create malformed-row fixtures inside the current checksum envelope; application behavior was not changed.

The release's `AUDIT.json` records the final package and source-archive scan. These are pattern-based technical checks, not a legal opinion or proof that arbitrary user files are lawful.

## Device test still required

Install this exact package as an update using the same signing identity. Confirm startup, library and grid, controller input, launch, video/audio, in-game memory-card save, return to library, relaunch and load the save. Confirm an existing save is retained. Desktop checks cannot perform these TV actions.
