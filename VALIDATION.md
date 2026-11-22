# Publication validation — 22 November 2026

The original curated archive's checksum, ZIP integrity and file inventory were verified against the 16 November freeze. This publication additionally removes compiled executables missed by filename-only filtering and selected prebuilt runtime/tool/media assets. It replaces stale landing/build instructions with the actual repository layout.

`python tools/verify-public-source.py` checks every inventoried file hash, path containment, credential patterns and compiled executable/archive signatures. The source inventory covers the published tree except itself, Git metadata and the generated release archive. This is a publication-integrity check, not proof of exhaustive secret detection or legal clearance.

Host regressions were reported as passing on the original private freeze. They have not been rerun as a complete native build of this source edition, whose omitted inputs are documented in DISTRIBUTION.md. No new TV install or gameplay validation is claimed.
