# Point 6: moving the finished picture

Source audit, 16 November 2026. No TV timing measurements or installation were
performed. Frozen versions: PSP 0.24.4, PSX 0.3.39, RetroTizen 0.7.1, N64 0.1.7.

The flight-reading chat's PSP triangle checkpoint was obsolete. The current PSP
frontend already uses the GLView framebuffer path. Root is useful for inspecting
the installed graphics stack, process scheduling and permissions; it is not
evidence that a replacement GPU driver is needed or available.

| App | Current presentation | First investigation |
| --- | --- | --- |
| TizenPPSSPP | `NativeCoreGame` / `CoreGlView` / native libretro bridge, core renders to GLView FBO; no frontend `glReadPixels` | Attribute core-internal readbacks, GL command time, queue waits and Evas composition separately |
| TizeN64 | GLES pbuffer → `glReadPixels` → CPU flip/swizzle → native memcpy → managed array copy → BMP encoding → Evas `SetStream` | Measure readback, conversion, copies and stream upload/decode independently |
| RetroTizen | Software core video → managed per-pixel conversion/new array → BMP stream → Evas `SetStream` | Measure CPU conversion, allocation and BMP/Evas overhead; direct image buffer is a smaller first experiment |
| TizenPSX | Existing buffered/pool software-frame presentation | Preserve working baseline; measure before replacing it |

## Source evidence

- `native/libretro-bridge/bridge.c`: the hardware callback supplies the active
  framebuffer and does not perform readback, Evas image copies or swapping.
- `app/Program.cs` still has an Evas image-copy call for diagnostic fallback
  output; its presence is not evidence that PSP gameplay takes that path.
- `n64-test/native/frontend.c`, `video`: synchronous RGBA `glReadPixels`, then
  vertical flip and channel conversion. `n64_step` copies pixels to the output.
- `n64-test/app/Program.cs`, `Run`: allocates another frame and `Array.Copy`s it.
  `Tick`: holds the frame lock while encoding a BMP stream and calling `SetStream`.
- `retro-native/app/Program.cs`: the software video callback allocates/converts
  frames, and the UI supplies a BMP stream to Evas.

For N64 at 320×240 RGBA, one frame is 307,200 bytes; a single full-frame transfer
at 60 Hz is 18.432 MB/s. At 640×480 it is 73.728 MB/s. These are arithmetic payload
rates, not measured memory bandwidth. Multiple passes, synchronization, driver
work and BMP processing add costs. The readback may wait for earlier GPU work,
so its CPU duration is not a pure transfer-time measurement.

## First experiment after the freeze

1. Keep all four frozen packages and hashes. Use a separate development branch
   and new version for any instrumented app.
2. N64: timestamp `retro_run`, `glReadPixels`, flip/swizzle, native output copy,
   managed copy and BMP/`SetStream` separately with a monotonic clock. Count
   produced, replaced and presented frames and allocated bytes. Aggregate once
   per second, not a log line per pixel/frame. Timings are nested: do not sum
   readback time into an already inclusive `retro_run` total.
3. Use a known-booting game/scene, same resolution, sound, controller and power
   state. Warm up, collect 60 seconds, repeat A/B/B/A. Record median/p95 timings,
   emulation speed and audio underruns. If N64 cannot boot reliably, first run
   an isolated animated GLES test to compare presentation without the core.
4. First candidate: replace BMP stream encoding with an owned reusable Evas
   image buffer. Respect UI ownership, pitch/format, producer/consumer lifetime,
   resize and teardown. This still has GPU readback and is not zero-copy.
5. Second candidate: adapt the proven PSP GLView/libretro lifecycle for N64,
   keeping the final frame GPU-side. N64's current framebuffer callback returns
   zero; changing it requires auditing GLideN64/GLSM assumptions, GL dispatch,
   context-reset/destroy, current-context thread and FBO state restoration.
6. Only compare Evas direct-render hints or native surfaces after the above is
   correct. Direct mode may fall back to composition and is not guaranteed
   scanout. Do not guess Evas structure layouts or substitute a random Mali DDK.

## Controller and graphics driver decision

The apps already have userspace DualSense HID readers for USB and Bluetooth.
PSP/PSX now share native physical-button mapping with automatic controller
selection/assignment. Root is not a prerequisite for decoding reports already
readable by the app. Investigate an actual missing device, permission denial or
bad report before considering a kernel driver. Root-shell access and normal
launcher process access can differ.

A custom application video backend is realistic; replacing the TV's GPU kernel
driver/userspace stack is a separate, device-specific undertaking with no
demonstrated need here. Retain the working Samsung GLES stack for the first tests.

References:
- [Linux hidraw documentation](https://kernel.org/doc/html/v5.11/hid/hidraw.html)
- [Tizen Evas GL helper and direct-render constraints](https://docs.stg.tizen.org/application/native/api/common/9.0/group__Elementary__GL__Helpers.html)

These describe interfaces and design constraints, not proof that every interface
is available on this TV firmware. Actual gains require on-TV measurement.
