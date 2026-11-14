# Map tile graphics — overworld `PAT` + dungeon map glyphs (P0.7, s41)

Static RE of OV2 (capstone `CS_MODE_16` on file offsets; OV2 string/data immediate = file + `0x433d`).
Extractor: `tools/scripts/extract_dos_map_tiles.py` → `reverse-engineering/dos/extracted/tiles/` (PNGs + `tiles.json`).
The selection rules are shipped as data in `src/OpenPhantasie.Data/GameData/phantasie1/asset-manifest.json` → `map_tiles`.

> **These CGA images are drawn only by the `dos-cga` art pack** (the Amiga set is the default, `../../amiga/FORMAT.md`
> section map graphics). The dungeon cell → glyph rule (`0x21df`) is used by every pack.

## Screen + palette

- CGA 320×200 4-colour mode, 2 bits/pixel, MSB pixel first; even rows at `B800:0000`, odd rows at `BA00:0000`
  (80 bytes per row in each bank) — the draw code writes one row to `0xb800`, the next to `0xba00`, then `+0x50`.
- Palette **[C — DOSBox capture]**: 0 black, 1 `#55FFFF`, 2 `#FF55FF`, 3 `#FFFFFF` (CGA palette 1, high intensity),
  sampled from `working/snapshots/h3-status-s10/s3.png`. No `int 10h` / port `0x3d9` write was found in the COM,
  OV1, OV2 or the TSRs, so the palette is whatever the mode set leaves; the capture is the evidence.

## Overworld — file `PAT` (450 bytes)

**Load [C]:** OV2 `0x530d` (region load) reads `OUT%-d.DAT` (1024 B → `0xc6fa`), then `"pat"` (string at OV2 file
`0x7e1f`, imm `0xc15c`) via COM read `0xc75a` with length `0x1c2` into DS `0xc434`. Called from `0x5de2`.

**Format [C]:** 15 patterns × 30 bytes. Each pattern is **12×10 px**: 10 rows top to bottom, 3 bytes (= 12 px at
2 bpp) per row. Map cell (x, y) is drawn at screen byte offset `y·400 + x·3` (`0x54fa`), i.e. pixel (12·x, 10·y);
26×20 cells fill 312×200.

**Cell draw `0x54fa(x, y)` [C]:**
```
raw = region[y·26 + x]                                  # 0x538b, raw byte (fog included)
if raw ≥ 120:  draw the 3-byte pattern at OV2 0xc16b (file 0x7e2e = 00 00 00) on all 10 rows   # fog = black
else:
    visited[y][x] := 1                                  # [bx − 0x2f42]
    p = 13 if raw == 41 else raw / 10
    if [0xc5f6] (region number) ∈ {18, 19} and p ∈ {6, 7}:  p = 14
    copy pattern PAT[p·30 .. p·30+29]
```
`tile % 10` never affects the picture — only `tile / 10` (plus the two overrides).
`0x547b` clears the screen, draws all 26×20 cells (`0x54fa`), then `0x53c5` the party.

**Party marker `0x53c5(x, y)` [C]:** marks the cell visited, un-fogs it (`0x53a5`), then copies 30 bytes from
DS `0xc59c` = `0xc434 + 12·30` → **pattern 12 is the party marker** (a "P"). `0x564c` = redraw old cell + party at
new cell; `0x52fa` stores the party position in `[0xc13a]/[0xc13c]`.

| Pattern | Drawn for | Picture | Terrain reading |
|---|---|---|---|
| 0 | class 0 (tiles 0–4) | sparse cyan dots on black | open land / grassland **[~]** (visual) |
| 1 | class 1 (10–16) | cyan trees with magenta trunks | forest **[~]** (visual) |
| 2 | class 2 (20–26) | magenta/cyan scales | hills **[~]** (visual) |
| 3 | class 3 (31–36) | magenta/white peaks, denser | mountains **[~]** (visual) |
| 4 | class 4 (tile 40 = town) | castle | town [C] (tile 40 = "A TOWN. ENTER?") |
| 5 | class 5 (50, 51) | arch | dungeon entrance [C] |
| 6 | class 6 (60–67) | **all black** | **path / road [~]** — see below |
| 7 | class 7 outside OUT18/19 (only tile 74) | planks between magenta bars | **bridge [~]** — see below |
| 8 | class 8 (80–87) | sparse magenta dots | desert / sand? **[~] weak** |
| 9 | class 9 (93) | magenta dashes | desert / dunes? **[~] weak** |
| 10 | class 10 (100, 101, 106) | cyan/white waves | water [C] (state 10 = swimming); tile 100 is the blocking edge |
| 11 | class 11 (111–113) | cyan disc | magical pool [C] |
| 12 | party marker | "P" | [C] (`0x53c5`) |
| 13 | tile 41 | small building | wilderness inn [C] |
| 14 | classes 6/7 in OUT18/OUT19 | magenta dashes | the Black Lord's realm ground [~] (tile 75 = class-7 fight tile, 698 cells) |

**Class 6 = path [~] (structural, not a decode).** Class-6 cells (60/63/66) form one-cell-wide lines that join towns,
inns and signs: OUT1 a line of `60` from the town (14,15) west past the signs `66` (13,15) and (8,13) to the inn
`41` (7,13); OUT8 a line of `63` from the inn (5,10) and sign `66` (6,9) east to the town `40` (18,15). Location-text
tiles (`x6`) sit on these lines. **Tile 74 = bridge [~]:** its only cell, OUT8 (14,11), is where that `63` road
crosses the river column `101` at x = 14 (and it is the class-7 fight tile → WARRIOR, a bridge guard).
Class 0 is not the path (an older guess from the encounter mix, `overworld-movement.md` section Day encounter).

The manual's terrain key (PDF p.25) pictures are not the DOS art (other platform), so they cannot be matched to
patterns. Names for classes 0–3, 8, 9 are visual judgements; only the costs and the code paths are [C].

## Dungeon map glyphs (no file — table inside OV2)

**Glyph table [C]:** 8 glyphs × 20 bytes at DS `0xc09a` (OV2 file `0x7d5d`). Each glyph is **5×4 px, one byte per
pixel = colour index 0..3**, row-major. Plotted pixel by pixel through a COM put-pixel at
`(5·cx + 0x48, 4·cy + 0x18)` (`0x588a(cx, cy, g)`, `g = 0xe6..0xed`), so the 33×38 level fills 165×152 px at
screen (72, 24). Glyph `0xed` (party) also stores the position via `0x52fa`.

**Cell → glyph `0x21df(x, y)` [C]** (`cell = buf[y·33 + x]`, `0x20c3`):

| Cell byte | Glyph | Picture |
|---|---|---|
| `0x00`, `0x01` | `e6` | solid white |
| `0x02`–`0xb8` | `e7` | black (traps, wandering, description, forced-fight, message cells all look like floor) |
| `0xb9`–`0xc7` | `ea` | two magenta horizontal bars (lock / ambush bank 1) |
| `0xc8`–`0xd6` | `e9` | two magenta vertical bars (lock bank 2) |
| `0xd7`–`0xd9` | `eb` | cyan "M"-like mark (exit) |
| `0xda`–`0xdc` | `ec` | cyan diagonal (stairs) |
| `≥ 0xdd` | `e8` | magenta diamond (scripted event) |
| party | `ed` | solid cyan |

Quirk [C]: cell `0x01` (walkable, 42 cells over 10 levels) draws exactly like a wall `0x00`.
The map window starts cleared (black) and `0x20e5` redraws the cells round the party as it moves (radius 1, radius 0
on `0x01` and `0xb9..0xc7`, `0x2102`; L)OOK draws more — `dungeon-representation.md` section Sight), so unseen cells and floor are both black. The window image is what `DNG.SAV` stores
(`dungeon-representation.md` §Writer: 76 rows × 2 banks × 42 bytes from `0x3d2`).

## Open
- `[?]` pattern 7 is reachable only through tile 74 (OUT8); classes 6/7 in OUT18/19 use 14. Whether any other class-7
  tile ever shows pattern 7 — none on the shipped maps.
- `[~]` terrain names for classes 0–3, 8, 9 (visual only).
