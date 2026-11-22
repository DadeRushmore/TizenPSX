# Public artwork and local overrides

The default public preview and TV resources use original abstract placeholder covers and an original geometric play emblem. Their editable SVG/rendering source is `tools/render-public-art.cjs`; they are covered by the project GPLv2 license. They contain no official game cover scans or Sony emblem. Game names and publisher metadata identify titles; they are not claims of affiliation or endorsement.

Regenerate assets with Node, Playwright and Edge using `node tools/render-public-art.cjs`. `TIZENPS_PLAYWRIGHT` may point to an existing Playwright installation.

## Personal artwork

The native app accepts `cover.png`, `cover.jpg` or `<game filename without extension>.png` next to a game. These take priority over packaged artwork. Optional `tools/fetch-covers.py` downloads the historical Libretro mappings into ignored local cache directories; this does not grant redistribution permission. Never include those caches in a public package. The browser preview continues to show the original placeholders.

`artwork-sources.json` documents earlier downloads for provenance; it is not a license to redistribute them. Historical commits still contain the old screenshot and branding until a separate approved history cleanup.
