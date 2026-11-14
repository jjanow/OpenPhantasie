# Phase 4 — Amiga Graphics Extraction

> **Closed (historical).** Phase 4 is done; the formats are in `reverse-engineering/amiga/FORMAT.md` (I) and
> `reverse-engineering/amiga/phantasie3/FORMAT.md` (III). Live state: `PLAN.md`.

**Scope note:** this pipeline applies to I and III (both had Amiga releases). II's Atari ST graphics:
`03b-phantasie2-re.md`; III's Amiga side: `03c-phantasie3-re.md`; IV's X68000 graphics: `03d-phantasie4-re.md`.

**Goal:** PNG spritesheets/tilesets in `reverse-engineering/amiga/extracted/`,
plus `asset-manifest.json` mapping each asset to the game-data IDs identified
in Phase 3 (e.g. monster ID 7 → `monsters/07-orc.png`).

Raw extraction is independent of Phase 3; the ID cross-reference needs Phase 3's monster/item tables.

## 4.1 — Unpack the disk image
```bash
xdftool phantasie-amiga.adf list
xdftool phantasie-amiga.adf unpack reverse-engineering/amiga/unpacked/
```
On filesystem errors, fall back to `extract-adf`.

## 4.2 — Identify the graphics format
```bash
strings -a reverse-engineering/amiga/unpacked/* | grep -E "FORM|ILBM|8SVX" 
```
Two outcomes:
- **Hits found** → at least some assets are standard IFF ILBM. Convert those
  directly:
  ```bash
  ilbmtoppm asset.iff | pnmtopng > asset.png
  ```
- **No hits / hits only for a subset** → the rest is a custom-packed format (common for tile/sprite sheets of the
  era): 4.3.

## 4.3 — Custom-packed format (if needed)
1. Locate graphics data in the executable or an emulator (WinUAE/FS-UAE) memory dump with **Maptapper**.
2. If a disk cruncher packed it, decompress with `xfdDecrunch` first.
3. Get width, height, bitplane count and interleaved vs planar-sequential layout from the 68000 blit routine that
   reads the data (Ghidra's 68000 module; the loop that walks bitplanes into the display registers).
4. One Python deinterleave/palette-remap script per format (bitplanes → indexed pixels → palette → PNG).

## 4.4 — Palette extraction
OCS/ECS palettes are tables of 12-bit RGB (4 bits per channel), near the graphics data or loaded by a
`LoadRGB4`-style call; convert each entry to 8-bit RGB.

## 4.5 — Build the asset manifest
`asset-manifest.json` maps each asset file to its game-data ID(s), so `OpenPhantasie.Data` gets a monster's stats
(Phase 3 JSON) and sprite (this manifest) through the same ID.

## Done-criteria for Phase 4
- [x] All in-game graphics (overworld tiles, dungeon tiles, character
      portraits, monster sprites, UI chrome) extracted as PNG
      *(tiles still open — not on ADF as files; see `amiga/FORMAT.md` /
      `asset-manifest.json` → `unmapped`. Portraits, monsters, UI screens done.)*
- [x] Palette correctly applied (visually verify against original screenshots)
      *(pixel-identical to Kroah reference `P1_R00_A` / `P1_I08_A`; town/title
      screens spot-checked)*
- [x] `asset-manifest.json` cross-references every asset to a Phase 3 data ID
      *(monsters 0–79, races 0–14; UI + tiles + Amiga-only UNDEAD portrait in
      `unmapped`)*
- [x] Note any assets that don't cleanly map (e.g. animation frames, UI-only
      elements with no data-table ID) for manual handling in Phase 5
