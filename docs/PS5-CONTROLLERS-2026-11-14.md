# Simplified PS5 controls: PSX 0.3.39 and PSP 0.24.4

Controllers opens the visual mapper directly. The only actions below the diagram are Restore PS5 defaults and Save/return. Input source, slot, profile, face-fix toggles, opt-in two-player mode and join/reassignment steps are no longer exposed.

Both apps compile the same full native HID reader and physical-button mapping class. The mapper and gameplay consume the same independent Circle/Triangle identities. Defaults work without mapping. Assignments swap physical inputs, are displayed with PS5 button names, and are saved in a new native-map file. Old ambiguous SDK mappings remain on disk but are not applied.

PSX automatically assigns two readable controllers to its two emulated ports, using ABI v4. A third connected controller is not an extra PS1 player. Existing player 2 does not become player 1 when player 1 disconnects. A replacement can take a disconnected slot after activity; held input is blocked until release. P1 owns app navigation and the Create+Options return chord. The game still needs a local two-player mode.

PSP tracks up to three controllers and uses the one active in the library; gameplay keeps that controller until it disconnects. PSP still has one local player. This does not add PSP network multiplayer.

Why the previous frontend failed the mapper test: 0.3.38's mapper used SDK raw button codes and suspended the separate native two-player path. Gameplay also required an opt-in mode and explicit Options joins. This revision removes those conditions rather than offering another workaround toggle. Installed versions were checked: PSX 0.3.38 and PSP 0.24.3. The exact cause of the user's MK session was not observed in a runtime capture.

Validation: automatic assignment with no join press; distinct/simultaneous face buttons across two ports; USB and Bluetooth formats; CRC rejection; axes/hats; stale/disconnect/pause release; worker descriptor ownership; three-controller tracking; physical-map persistence, defaults and swaps for both PSX/PSP target layouts. Native ABI callback isolation and cleanup tests pass. Existing PSX player and PSP regression suites pass (the PSP suite required loopback networking outside the restricted sandbox). Final builds and package payload/signing checks are performed by each build-package script.

Physical two-controller gameplay and visual-mapper interaction on the TV remain to be verified. No performance improvement is claimed. Emulator core binaries are unchanged by this frontend simplification.
