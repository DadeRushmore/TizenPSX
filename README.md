# TizenPSX 0.3.38 beta

A community PlayStation emulator for compatible Samsung Tizen TVs, powered by PCSX ReARMed. This first public beta packages the working 0.3.37 application with original public artwork and distribution notices. The emulation core and controller behavior are unchanged.

**Beta:** successful testing on the developer's setup does not establish a compatibility percentage for other TVs, controllers or games. The exact 0.3.38 package still needs a TV installation and save/reload smoke test.

## Install

Download the `.tpk` from [Releases](https://github.com/DadeRushmore/TizenPSX/releases). Use [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung) to connect to your TV, sign the package for that device and install it. Follow its Developer Mode instructions.

For an existing installation, back up your saves first and use the same signing identity to install as an update. Keep package ID `org.roelof.psxnative`; do not uninstall the existing app as part of an update. A different signer may prevent an update. This beta is a sideloaded community app, not a Samsung store release.

Import your own lawfully obtained BIOS through the app's settings, then add your own game folder. No BIOS, commercial games or commercial cover artwork are included. A small original red-screen homebrew diagnostic is included. You are responsible for having the rights required for your files and artwork under applicable law.

## Current look

![TizenPSX beta library with Mortal Kombat Trilogy selected and original placeholder artwork](library-preview.png)

1920 × 1080 desktop render of the native app layout, not a direct TV capture. Fonts and compositing may differ on the TV. The original placeholders shown are included; users can supply their own local artwork.

## Library and controls

- Automatic discovery of `PSX` folders on connected USB storage, refresh while browsing, favourites and recent games.
- Carousel and 18-game grid views, per-game options and user-supplied covers/backgrounds.
- ARM dynamic recompilation and enhanced resolution, with a safer one-time launch option for troubleshooting.
- TV remote navigation and controller support; USB DualSense testing is reported on the developer's setup. Other combinations need testing.

See the [drive and artwork guide](LIBRARY-DRIVE-GUIDE.md). Keep game paths stable after creating saves. Before removing a drive, return from the game to the library and allow saving to finish.

## Known limitations

CHD, archive loading and a complete multi-disc switching workflow are not supported by this frontend. Extract ZIP/7z/RAR files first. Prefer a CUE with all matching BIN tracks. Some games may need different per-game settings or may not work. The ARM32 native core and TV system-library dependencies limit which devices can run the app; no universal Tizen compatibility is claimed. Network companion integration is experimental and is not part of this release's validated installation path.

Report problems in [Issues](https://github.com/DadeRushmore/TizenPSX/issues), including the TV model, Tizen version, app version, controller/connection, game title/region, settings and reproduction steps. Do not attach BIOS, game files, private certificates or pairing tokens.

## Source, licensing and attribution

The release includes [TizenPSX-0.3.38-source.zip](https://github.com/DadeRushmore/TizenPSX/releases/download/v0.3.38-beta/TizenPSX-0.3.38-source.zip), corresponding to the packaged application and core, plus build scripts and notices. See [BUILD.md](BUILD.md), [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and [LICENSE](LICENSE). The application and Tizen integration retain GPL version 2 licensing; third-party components retain their own licenses. PCSX ReARMed is credited to the PCSX Team, notaz, Exophase and the upstream contributors.

Independent community project; not affiliated with or endorsed by Sony or Samsung. Product and game names identify compatibility only. No third-party trademark or artwork license is granted by the project's software license.

After this beta, development priority moves to TizenPPSSPP performance, with TizenPSX fixes reserved for reported regressions.

For the application source, use the named source archive attached to the release. GitHub’s automatically generated “Source code” archives contain this repository’s release documentation.
