# Roelof's PSX Native 0.2.3 — controller source patch (TV TEST REQUIRED)

This folder is **source code, not an installable 0.2.3 TPK**. It has not been
built or tested on your Samsung Tizen 5 TV. **Do not install the included old
0.2.2 TPK as though it contains these fixes.** The old binary in `res/` is
incompatible with the modified C#/native callback ABI and `core.sha256` is
intentionally invalid until the core is rebuilt.

## Controller changes

- Tizen gamepad polling checks all four player slots when needed and picks a
  connected controller with activity; normal gameplay polls the chosen slot.
  Polling exceptions no longer permanently disable input.
- Controller test shows selected player slot, buttons, raw left and right stick
  coordinates and worst UI-thread polling gap. Analog values here are the
  **Tizen API input**, before entering the emulator.
- Menu entry `PS1 controller` selects **Standard DIGITAL** (default, either
  stick becomes D-pad) or **DualShock ANALOG** (real analog X/Y to the PS1 core).
  The choice is saved and applies to the next game.
- In DualShock mode, the PS1 analog state starts ON. Press both sticks (L3+R3)
  together to toggle emulated analog mode in a game. Some older PS1 games only
  work with `Standard DIGITAL`. Analog signals are quantized to signed 8-bit
  and passed as signed libretro 16-bit axes; Tizen positive Y is inverted. A 10% software dead zone limits stick drift.
- A controller packet now returns 64 bits: lower 16 digital buttons, bit 16
  exit, bit 17 audio pacing, and top four bytes LX/LY/RX/RY. **Both** the
  native C core and managed app must be rebuilt together.
- Native end-of-game diagnostics report frames with digital button samples and
  frames with non-neutral analog samples. Controller input still needs
  verification using actual TV hardware and a known responsive game.

## Build on your existing Windows workstation

Run from this archive's extracted root:

```powershell
python roelofs-psx-native/tools/rebuild-0.2.3.py
```

This uses the existing `native-build-tools` sibling directory required by
`pcsx-rearmed-native/tizen-build/build-core.py` (Zig 0.13.0), rebuilds the
ARM32 native library, updates the resource checksum and provenance, and runs
`dotnet restore` / `dotnet build -c Release`. If you do not have the original
Zig and .NET toolchains, the build **cannot complete**; do not substitute an
unchanged 0.2.2 `libpcsx_tizen.so`.

After successfully building, re-sign the new TPK with the same signing identity
and install it **as an update** (same package/app ID `org.roelof.psxnative`).
Do not uninstall the old application, because uninstalling could delete BIOS,
settings and memory cards.

## TV test sequence

1. Plug in your DualSense with a USB **data** cable. Launch the 0.2.3 build.
2. Open `Controller test`. Look for `Connected: True (slot 1/2/3/4)`.
3. Hold D-pad Right for ~5 seconds. `Right` must remain continuously held.
4. Push the **left** stick in all directions: raw LX/LY should change smoothly
   towards -1/+1. Repeat with the right stick: RX/RY.
5. Observe the worst polling gap: regular spikes well above 500 ms would
   suggest the UI thread is being blocked and snapshots may expire.
6. Start a game in `Standard DIGITAL` first. Check Start, Cross, D-pad and
   digital conversion from the left stick. Stop and inspect diagnostics.
7. Switch to `DualShock ANALOG`, restart a game that supports DualShock and
   test both sticks. Try L3+R3 if the game's analog mode is inactive.
8. If test page values remain zero, the problem precedes PCSX-ReARMed and
   needs a TV-specific gamepad transport/driver investigation.

## Original 0.2.2 notes

# Roelof's PSX Native 0.2.2

Audio/controller update for the native interpreter preview for the 2019 Samsung Tizen 5 TV. Separate application from Native Test, with a new purple/cyan controller icon. No Sony BIOS or commercial games included.

## Install and play

1. Send the default-signed TPK through Apps2Samsung with re-signing, as for the native tests. Open **Roelof's PSX Native**.
2. Put your own 512 KB PS1 BIOS and an extracted game on USB. Keep the CUE and all its BIN tracks together; filenames must match the CUE exactly, including case. ZIP and CHD are not supported in this build.
3. Use the TV remote to select **Import PS1 BIOS**. The file is copied into private app storage. Your SCPH-101 BIOS is suitable for this selection.
4. Select **Choose game from USB**, preferably the CUE, then **Play selected game**. A single-track BIN/IMG/ISO or PS-X EXE can also be selected.
5. Connect DualSense by USB data cable. D-pad and either thumbstick provide digital direction; the face buttons and shoulders map to their PS1 positions. Create is Select; Options is Start. Analog PS1 controller mode is not implemented yet.
6. **TV Return**, or **Create + Options held together for one second**, stops play and writes the memory card. Use this before exiting or powering off. A single held button does not open the menu.

Keyboard fallback: arrows move; Z=Cross, X=Circle, A=Square, S=Triangle, Q=L1, W=R1, Enter=Start, Shift=Select, Esc=stop. Browse with remote/keyboard, not the controller. The TV detects the DualSense, but the updated held-button and stick handling still needs TV verification.

The built-in demo is an intentionally static red screen with no sound. Return leaves it. Real games are needed to test audio and movement. After stopping a game, open **Last game diagnostics** for actual core-loop FPS, controller status and errors. If the app crashes, reopen it and inspect that page before starting another game.

## Behavior and limits

- Live 4:3 picture uses the full 1080-pixel height with side bars. Evas presents frames through the TV compositor. The worker follows the core's PAL/NTSC timing; this is not a claim of direct vsync synchronization.
- Stereo audio uses PulseAudio at 44.1 kHz with a 120 ms target buffer (previously 80 ms). Audio output paces the emulator when active; the independent frame sleep is then disabled. This trades some audio latency for resilience to brief scheduling gaps. Sound can be disabled before launching a game. Slow emulation can still cause broken audio.
- The proven interpreter is enabled, **not the dynarec**. The previous 85 FPS result was a trivial red-screen program, not Tekken performance. Commercial-game speed, compatibility, live presentation and DualSense support must be checked on the TV.
- Slot 1 memory cards are private per game path and saved on normal stop; no save states or autosave during gameplay. Moving/renaming a game can select a different card. An invalid existing card is preserved rather than overwritten. Uninstalling the app can erase its BIOS/settings/cards.
- The app cannot interrupt a stuck native operation immediately. Return requests shutdown at the next frame boundary. Do not remove USB while playing.
- The existing native tests and Arcade/NES/SNES apps are separate and unchanged.

## Validation and source

Local .NET build passes with zero warnings/errors. Managed checks parse and relocate the actual player ELF using synthetic addresses, test data/BSS and all three supported relocation types, and reject malformed inputs. This is not TV execution validation of the new frontend.

Application/host source is GPL-2.0 as supplied with PCSX-ReARMed; dependency notices remain applicable. The corresponding source ZIP includes the application, full PCSX tree, host/build scripts, loader checks, original demo instruction words, and glibc wrapper sources. See LICENSE and provenance.json. The ABI fixture libovprobe.so is from the cited Overscan revision and is only read as a known-size file by this player.

Build tools: Zig 0.13.0 (ARM32 arm-linux-gnueabi.2.17), .NET SDK 3.1.426. Place portable toolchains in sibling native-build-tools as expected by pcsx-rearmed-native/tizen-build/build-core.py, then run that script. Copy its libpcsx_tizen.so to app res, update res/core.sha256 and provenance.json, and run dotnet restore followed by dotnet build Player.csproj -c Release. The resulting package uses default development certificates and needs re-signing for the TV. tools/icon.ps1 regenerates the original icon.

## 0.2.1 display fix

Frame presentation and shutdown now use EcoreMainloop.PostAndWakeUp, the thread-safe main-loop dispatcher, instead of adding an idler from the native worker. EcoreMainloop is initialized on the UI thread before gameplay. The video surface is explicitly above the background. Diagnostics distinguish received frames from frames submitted to the display (submission does not prove physical screen visibility). Core, audio, controller mappings, package ID and saved data paths are unchanged. Install as an update with the same signing identity; do not uninstall.

0.2.0 TV evidence: Crash produced audio, detected the controller, received 312 video callbacks and ran at 51 core FPS against a 60 FPS target, but remained on the loading screen. The new display path requires TV verification.

## 0.2.2 audio and controller changes

- GamePad.GetState now runs at 60 Hz on the UI thread, using no SDK dead zone. The worker reads an immutable snapshot instead of invoking the gamepad API itself. Held buttons are state, not one-shot events; release/disconnect clears them. A snapshot older than 0.5 seconds is ignored to prevent stuck input if the UI stalls.
- Either stick acts as digital directions with a 25% dead zone. Physical D-pad takes priority; left stick takes priority over right. This is compatible with games predating DualShock, not native PS1 analog mode.
- New Controller test page displays held buttons, continuous hold duration, and raw left/right X/Y. Hold Right for several seconds and confirm the timer increases continuously; release and confirm it clears. Check each stick separately. TV Return leaves the test.
- PulseAudio output is now the timing source while sound is active. Silent playback and audio failures fall back to core PAL/NTSC frame timing. A quarter-second without audio callbacks also enables the timer fallback. No pitch shifting, arbitrary sample dropping or adaptive resampling is added.
- Diagnostics include generated stereo frames and time spent writing audio. Persistent under-speed emulation can still cause gaps; a larger buffer cannot make a slow core sustain full-speed audio.
- Frame image dimensions are changed only when resolution changes, reducing UI work.

Local checks: zero-warning application build; updated ARM ELF parses/relocates; controller tests cover sustained state, release, disconnect, dead zone, both sticks, diagonals, input priority and simultaneous buttons. Actual API behavior and audio quality require the TV test. Install as an update using the same signer; keep your BIOS and saves.

API references used: https://developer.samsung.com/smarttv/develop/tizen-net-tv/guides/user-interaction.html and the Tizen.NET.TV 4.4 reference XML. The API documents normalized X/Y axes and current button state; UI-thread polling is a compatibility fix to test on this firmware, not a proven diagnosis of its controller driver.
