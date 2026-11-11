# TizenPSX library and drive guide

This sprint makes the PSX library follow the files on your drive. You can add games and artwork without rebuilding the app or editing its bundled catalogue. The PSP and SNES folders below are a shared-drive convention; this release changes TizenPSX only.

## A simple drive layout

Use one folder per system and, for PSX, preferably one folder per game:

```text
USB drive/
  PSX/
    Ninja - Shadow of Darkness/
      Ninja - Shadow of Darkness.cue
      Ninja - Shadow of Darkness.bin
      cover.jpg
      background.jpg
    Another Game/
      Another Game.cue
      Another Game (Track 1).bin
      Another Game (Track 2).bin
      cover.png
  PSP/
    Example Game.iso
    Example Game.jpg
    Example Game.background.jpg
  SNES/
    ...existing SNES game files...
```

The CUE file must keep the correct names of its BIN tracks. Copy the whole game folder together. Keep BIOS files in a separate `BIOS` folder; they are not library games. TizenPSX discovers folders named `PSX` on connected storage, up to four levels below the drive root, and scans their subfolders. Put `PSX` directly at the drive root if possible: root discovery checks at most 512 directories.

For a shared drive, keep the same folder and file names when copying it to a new drive. TizenPSX can reconnect an unambiguous matching path below `PSX` when the TV gives the drive a different mount path. Moving or renaming a game's folder *inside* PSX is a different identity; keep those names stable after starting a save.

## Adding and removing games

1. Close the running game before removing the drive.
2. Add or remove complete game folders on the PC. Add artwork at the same time if you have it.
3. Reconnect the drive, then use **Options → Library & files → Refresh USB library** for an immediate refresh. The library also checks at startup and once a minute while you browse.

Games with missing files are hidden from the normal available collection, but their library records, favourites, history, settings and memory cards are retained. This handles both intentional removal and a temporarily unplugged drive. Use **Options → Library & files → Filters & unavailable games → Unavailable games** to inspect retained entries. The app does not delete game files or artwork from the drive.

A drive mount change is matched only when there is one missing entry and one discovered file with the same relative PSX path. Ambiguous copies are not guessed. A retained card can be copied to the new path identity without overwriting an existing destination card; game settings and history already use the relative PSX identity.

## Browsing a larger collection

Use **Options → Library view** to switch between the carousel and an 18-game grid. The app remembers the selected view. From the carousel, **Down** is a shortcut to the grid, and **Up** opens view/filter choices. In the grid, use all four D-pad directions to browse, **OK** to open a game and **Back** for options.

The grid loads one page of six columns and three rows at a time. The selected game's complete title remains below the grid. Filters provide available games, favourites, recently played games and retained unavailable entries. The app does not preload artwork for all games on the drive.

## Covers and backgrounds for any game

For a separate game folder, the simplest filenames are `cover.jpg` and `background.jpg`. PNG and JPEG are accepted too. For several games in the same folder, match the game's filename:

```text
Example Game.cue
Example Game.jpg
Example Game.background.jpg
```

Matching filename artwork takes priority over folder artwork. Covers use `.jpg`, `.jpeg` or `.png`; folder fallback uses `cover` with those extensions. Backgrounds use `<game>.background` with those extensions, then folder `background` or `fanart`.

A game does not need a hand-created background to appear in the library. Without a separate background, the app enlarges and darkens its cover into a soft backdrop; games without usable cover art keep the standard background. Missing or unreadable images use a fallback. Bespoke art can be added gradually to favourite games. Refresh the library after replacing image files, even if their filenames remain the same.

Empty artwork files and artwork files above 16 MiB are ignored. For a large collection, prepare images on the PC before copying: roughly 600–1000 pixels for covers and 1280×720 or 1920×1080 for landscape backgrounds is enough for this interface. The app is designed to decode visible artwork rather than preload the whole collection. This avoids making every extra game consume another full image in TV memory.

The existing PSP app already understands `<game>.background.jpg`, `.png` and `.jpeg`. Sharing that naming convention makes later work across the apps simpler. Folder-level artwork and the new PSX browsing behavior are not automatically added to the other emulators by this release.

## Current PSX file support

The library scans extracted `.cue`, `.bin`, `.img`, `.iso` and `.exe` files. It recognises BIN tracks referenced by CUE sheets and suppresses them as separate games, including tracks in subfolders. For multi-disc games, retain a separate CUE entry per disc for now.

ZIP, 7z and RAR archives need extracting first. CHD is not enabled in this native build. M3U playlists and PBP are not newly exposed by the scanner: the upstream core contains parsers, but this TV frontend has no complete disc-swap workflow yet. An extension advertised by upstream is not a promise that this port supports it end to end.

The scan has limits of 4096 directories, 50,000 filesystem entries and 32 directory levels. CUE sheets larger than 256 KiB are skipped. These are deliberate bounds for TV responsiveness and malformed files, not a 100-game limit. A reported partial scan is not evidence that absent games were deleted.

## A practical rollout for a larger collection

- Start with a small mixed sample: single BIN/CUE, multi-track CUE, a game without artwork, one with JPGs and one with PNGs.
- Test the carousel and grid, reconnect the drive, add one game and remove one. Confirm favourites and a memory-card save survive the reconnect.
- Expand to a few hundred games. Use favourites for regular play and the grid for quickly spotting covers. Time a refresh on the actual TV before expanding again.
- Keep curated backgrounds optional. A later PC-side library organiser can audit missing artwork, resize images in bulk and prepare metadata manifests without putting that work on the TV.
- Add disc swapping and optional metadata import as separate follow-up work. Do not reorganise or compress the whole collection around formats the current TV build cannot load.

A new drive's USB interface speed alone will not make CPU-limited emulation faster. This sprint improves finding and browsing the collection; it does not change the emulator core or claim a game-speed increase. Before committing a large collection to a new drive, confirm that your TV mounts its filesystem and that the apps can read a sample game from it.





