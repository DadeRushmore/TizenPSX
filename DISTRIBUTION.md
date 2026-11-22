# Distribution review, 22 November 2026

Frontend/PCSX ReARMed GPL-2.0; glibc subset LGPL-2.1-or-later; Overscan MIT; vendored components retain their notices.

This source release does not constitute blanket legal clearance. Individual source notices are retained. No private TPK, compiled emulator core, retail BIOS, commercial ROM, downloaded cover scan, signing credential or pairing file is included. Do not describe the mixed tree as entirely covered by its top-level LICENSE.

Before a binary release:

- Rebuild without commercial cover scans and verify original homebrew fixture reproduction and every native object/source pairing.

GPL source distribution reference: https://www.gnu.org/licenses/old-licenses/gpl-2.0.html (section 3). Core-license overview: https://docs.libretro.com/development/licenses/. Local pinned license texts take precedence over this summary.

## Publication corrections

The audit now checks executable magic bytes as well as filenames. Versioned and extensionless compiled files missed by the original audit are excluded. Additional prebuilt runtime/tool/media assets are omitted; PUBLIC-SOURCE.json lists their exact paths and hashes. Upstream source test vectors may remain and are not commercial games.
