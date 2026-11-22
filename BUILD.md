# Working with TizenPSX 0.3.39 source

This is a source publication, not a complete installer build. The source corresponds to the frozen development tree; omitted prebuilt inputs are listed in PUBLIC-SOURCE.json. Do not copy private binaries into a public package without completing DISTRIBUTION.md.

## Prerequisites and layout

- Python 3 for the source audit and helper scripts.
- .NET SDK 3.1.426 for the Tizen 5.0 application. Package references are pinned in `ports/TizenPS-0.3.34/roelofs-psx-native/Player.csproj`; some host checks use a different SDK pinned in their directory.
- Zig 0.13.0 for native integration, plus the core-specific prerequisites documented by the build scripts.
- Tizen signing tooling and your own signing identity for any later device package. Keep all credentials outside Git.

Application: `ports/TizenPS-0.3.34/roelofs-psx-native/`. Native integration/build tooling: `ports/TizenPS-0.3.34/pcsx-rearmed-native/tizen-build/`. Upstream source: `ports/TizenPS-0.3.34/pcsx-rearmed-native/`. The original project-relative layout is retained because projects share source through relative paths.

## Inspect and verify

```sh
python tools/verify-public-source.py
```

To regenerate original application artwork, run `python render-public-art.py` from the repository root. These images are already included. The SHA-256 inventory describes this published snapshot; intentional edits require regenerating the manifest before release.

## Before building an installer

1. Resolve the specific items in DISTRIBUTION.md and restore/rebuild omitted native and runtime inputs from recorded sources with their required notices.
2. Follow the native build scripts in the directories above and place rebuilt resources where the application expects them. Do not assume the source audit proves a complete build.
3. Supply `AuthorPath`, `AuthorPass`, `DistributorPath` and `DistributorPass` through your local environment and run `python build.py`. That helper builds the managed project; it does not rebuild missing native resources.
4. Run the applicable host checks and test the exact package on the target TV before describing it as tested or stable.

No runtime behavior is changed by this publication work. The exact private baseline hash is recorded solely for recovery/reference, not as a distributable artifact.
