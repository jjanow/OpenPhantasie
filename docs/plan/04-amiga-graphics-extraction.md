# Phase 4 — Amiga Graphics Extraction

> **Closed (historical).** Phase 4 is done; the formats are in `reverse-engineering/amiga/FORMAT.md` (I) and
> `reverse-engineering/amiga/phantasie3/FORMAT.md` (III). Live state: `PLAN.md`.

**Scope note:** this pipeline applies to Phantasie I and III, both of which
got Amiga releases. Phantasie II did not: its Atari ST graphics are done and
documented in `03b-phantasie2-re.md` (III's Amiga side in `03c-phantasie3-re.md`,
IV's X68000 graphics in `03d-phantasie4-re.md`).

**Goal:** PNG spritesheets/tilesets in `reverse-engineering/amiga/extracted/`,
plus `asset-manifest.json` mapping each asset to the game-data IDs identified
in Phase 3 (e.g. monster ID 7 → `monsters/07-orc.png`).

This phase is independent of Phase 3's code work but depends on Phase 3's data
table extraction to build the ID cross-reference — do the raw extraction any
time, but do the cross-referencing after Phase 3's monster/item tables exist.

## 4.1 — Unpack the disk image
```bash
xdftool phantasie-amiga.adf list
xdftool phantasie-amiga.adf unpack reverse-engineering/amiga/unpacked/
```
If the image has filesystem errors, fall back to `extract-adf` for a more
resilient extraction.

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
- **No hits / hits only for a subset** → the remaining graphics are stored in
  a custom-packed format (very common for tile/sprite sheets in this era, to
  save floppy space). This is the harder branch — proceed to 4.3.

## 4.3 — Custom-packed format (if needed)
1. Use **Maptapper** to locate sprite/graphics data inside the executable or a
   memory dump taken from an Amiga emulator (WinUAE/FS-UAE) mid-game — it's
   built exactly for finding graphics blobs in Amiga game binaries.
2. Check whether the data is run through a demoscene-style disk cruncher
   first (common on Amiga floppies to fit more content) — if so, decompress
   with `xfdDecrunch` before attempting to interpret it as pixel data.
3. Once you have raw decompressed bitplane data, you need to know: width,
   height, bitplane count (color depth), and whether it's interleaved or
   planar-sequential. Cross-reference against what the 68000 disassembly's
   blit routine expects (a quick pass in Ghidra's 68000 module on whatever
   routine reads this data — you're looking for the loop that walks bitplanes
   into the Amiga's bitplane display registers, which tells you the exact
   layout).
4. Write a small Python deinterleave/palette-remap script (bitplanes →
   chunky/indexed pixels → apply the extracted palette → PNG). This is a
   one-time algorithm to get right per unique format found; reuse it across
   all assets in that format.

## 4.4 — Palette extraction
Amiga OCS/ECS games store their palette as a small table of 12-bit RGB
values (4 bits per channel) — locate this table (usually near the graphics
data or loaded via a `LoadRGB4`-style call in the disassembly) and convert
each entry to 8-bit RGB for the PNG output.

## 4.5 — Build the asset manifest
Once Phase 3's monster/item/spell JSON tables exist, write
`asset-manifest.json` mapping each extracted asset filename to the
corresponding game-data ID(s). This is what `OpenPhantasie.Data` will consume in
Phase 5 — the engine looks up "monster ID 7" and gets both stats (from Phase
3's JSON) and sprite (from this manifest) through the same ID.

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
