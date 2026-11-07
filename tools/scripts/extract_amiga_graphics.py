#!/usr/bin/env python3
"""Extract Phantasie I Amiga graphics to PNG under reverse-engineering/amiga/extracted/.

Sources (ADF → ``unpacked/PHANT/``):
  - ``mstr1.pat`` / ``mstr2.pat`` — 80 monster sprites (idle / attacking)
  - ``party1.pat`` / ``party2.pat`` — 16 race portraits (idle / attacking)
  - ``*.pic`` — full-screen / banner bitmaps (320×200×4 or logo strip)

Layout and palette offsets come from Kroah's Sprite Viewer (P1 Amiga path); the layout was validated against the
published ``P1_R00_A`` / ``P1_I08_A`` PNGs. Their colours (Kroah's Amiga->ST nibble LUT) are not what the Amiga shows:
the sprites are drawn only by the combat module (hunk 53, the only code that names ``MSTRx.PAT`` / ``PARTYx.PAT``),
whose screen LoadRGB4s the combat table hunk 69 +0x94 (h53 +0x33c), so they are expanded ``c * 17`` per 4-bit gun
(``hw4``, s125; FORMAT.md "Sprite colours on screen").

Overworld/dungeon *tile* bitmaps are not separate disk files — see
``reverse-engineering/amiga/FORMAT.md``.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from amiga_gfx_common import (
    EXTRACTED,
    MONSTER_ADDR_OFF,
    MONSTER_COUNT,
    MONSTER_DIM_OFF,
    MONSTER_NAME_OFF,
    MONSTER_NAME_STRIDE,
    PALETTE_ALT_OFF,
    PALETTE_COMBAT_OFF,
    RACE_BYTES,
    RACE_COUNT,
    RACE_NAME_OFF,
    UNPACKED,
    decode_planar,
    ensure_dir,
    load_palette,
    palette_raw_words,
    read_cstring,
    read_long,
    read_word,
    slug,
)

# (file, width, height, output, palette offset, hw4). The title covers: the boot code (hunk 77 +0x2de) blits cover1
# and then LoadRGB4s the 16 words at hunk 69 +0xb4 = PALETTE_ALT_OFF (h77 +0x340 -> +0x466 -> +0x4a4); cover2 shows in
# the same palette. LoadRGB4 takes 4-bit guns, so they are expanded c * 17 (FORMAT.md "Title"). The town: entry
# (h29 +0x2e, h20 +0x186) LoadRGB4s hunk 69 +0x54 = PALETTE_TOWN_OFF; pelnor.pic in it equals an FS-UAE capture (s120).
# decor.pic (defeat, h32 +0x55c) and final.pic (ending, h32 +0x6d0) show in the combat table h69 +0x94 set by LoadRGB4 /
# per pen, so also c * 17; both equal FS-UAE captures (s124, FORMAT.md "Defeat and ending pictures").
PALETTE_TOWN_OFF = 0x1947C
PALETTE_MAP_OFF = 0x1949C  # h69 +0x74, the overworld / dungeon map's (FORMAT.md "The map's message line")
SCREEN_PICS = [
    ("cover1.pic", 320, 200, "ui/cover1.png", PALETTE_ALT_OFF, True),
    ("cover2.pic", 320, 200, "ui/cover2.png", PALETTE_ALT_OFF, True),
    ("decor.pic", 320, 200, "ui/decor.png", PALETTE_COMBAT_OFF, True),
    ("pelnor.pic", 320, 200, "ui/pelnor-town.png", PALETTE_TOWN_OFF, True),
    ("final.pic", 320, 200, "ui/final.png", PALETTE_COMBAT_OFF, True),
    # s156: the About Phantasie requester's Image (h81 +0x22: 176 x 80 x 4, plane after plane = 7040 bytes; h80 +0xcb4
    # reads logo.pic into its ImageData, requester 27 shows it on the map screen, palette h69 +0x74).
    ("logo.pic", 176, 80, "ui/logo.png", PALETTE_MAP_OFF, True),
]


def extract_monsters(game: bytes, palette) -> list[dict]:
    mstr1 = (UNPACKED / "mstr1.pat").read_bytes()
    mstr2 = (UNPACKED / "mstr2.pat").read_bytes()
    out_idle = ensure_dir(EXTRACTED / "monsters" / "idle")
    out_atk = ensure_dir(EXTRACTED / "monsters" / "attacking")
    records = []
    for index in range(MONSTER_COUNT):
        addr = read_long(game, MONSTER_ADDR_OFF + index * 4)
        width = read_word(game, MONSTER_DIM_OFF + index * 4)
        height = read_word(game, MONSTER_DIM_OFF + index * 4 + 2)
        name = read_cstring(game, MONSTER_NAME_OFF + index * MONSTER_NAME_STRIDE)
        base = f"{index:02d}-{slug(name)}"
        idle_rel = f"monsters/idle/{base}.png"
        atk_rel = f"monsters/attacking/{base}.png"
        decode_planar(mstr1, addr, width, height, palette).save(EXTRACTED / idle_rel)
        decode_planar(mstr2, addr, width, height, palette).save(EXTRACTED / atk_rel)
        records.append(
            {
                "index": index,
                "name": name,
                "width": width,
                "height": height,
                "pat_offset": addr,
                "idle": idle_rel,
                "attacking": atk_rel,
            }
        )
        print(f"  monster {index:02d} {name:20s} {width}x{height}")
    return records


def extract_races(game: bytes, palette) -> list[dict]:
    party1 = (UNPACKED / "party1.pat").read_bytes()
    party2 = (UNPACKED / "party2.pat").read_bytes()
    out_idle = ensure_dir(EXTRACTED / "portraits" / "idle")
    out_atk = ensure_dir(EXTRACTED / "portraits" / "attacking")
    records = []
    name_off = RACE_NAME_OFF
    for index in range(RACE_COUNT):
        name = read_cstring(game, name_off)
        name_off += len(name) + 1
        addr = RACE_BYTES * index
        base = f"{index:02d}-{slug(name)}"
        idle_rel = f"portraits/idle/{base}.png"
        atk_rel = f"portraits/attacking/{base}.png"
        decode_planar(party1, addr, 32, 32, palette).save(EXTRACTED / idle_rel)
        decode_planar(party2, addr, 32, 32, palette).save(EXTRACTED / atk_rel)
        records.append(
            {
                "index": index,
                "name": name,
                "width": 32,
                "height": 32,
                "pat_offset": addr,
                "idle": idle_rel,
                "attacking": atk_rel,
            }
        )
        print(f"  race    {index:02d} {name}")
    return records


def extract_screens(game: bytes) -> list[dict]:
    records = []
    ensure_dir(EXTRACTED / "ui")
    for filename, width, height, rel, pal_off, hw4 in SCREEN_PICS:
        data = (UNPACKED / filename).read_bytes()
        expected = width * height * 4 // 8
        if len(data) != expected:
            print(
                f"  WARN {filename}: size {len(data)} != {expected} for {width}x{height}x4",
                file=sys.stderr,
            )
        palette = load_palette(game, pal_off, hw4=hw4)
        decode_planar(data, 0, width, height, palette).save(EXTRACTED / rel)
        records.append(
            {
                "source": filename,
                "width": width,
                "height": height,
                "bitplanes": 4,
                "palette_offset": f"0x{pal_off:x}",
                "expansion": "hw4" if hw4 else "amiga_to_st_nibble_lut",
                "path": rel,
            }
        )
        print(f"  screen  {filename} -> {rel}")
    return records


def write_palette_json(game: bytes) -> None:
    ensure_dir(EXTRACTED / "palettes")
    for label, off in (("combat", PALETTE_COMBAT_OFF), ("alt", PALETTE_ALT_OFF)):
        words = palette_raw_words(game, off)
        colors = load_palette(game, off)
        obj = {
            "source": "game",
            "offset": f"0x{off:x}",
            "amiga_0rgb": words,
            "rgba": [
                {"r": r, "g": g, "b": b, "a": a, "hex": f"#{r:02x}{g:02x}{b:02x}"}
                for (r, g, b, a) in colors
            ],
            "expansion": "amiga_to_st_nibble_lut",
        }
        path = EXTRACTED / "palettes" / f"{label}.json"
        path.write_text(json.dumps(obj, indent=2) + "\n")
        # swatch strip
        from PIL import Image

        sw = Image.new("RGBA", (16 * 16, 16))
        for i, c in enumerate(colors):
            for y in range(16):
                for x in range(16):
                    sw.putpixel((i * 16 + x, y), c)
        sw.save(EXTRACTED / "palettes" / f"{label}.png")
        print(f"  palette {label} @ 0x{off:x}")


def build_spritesheets(monsters: list[dict], races: list[dict]) -> None:
    """Optional contact sheets for visual QA."""
    from PIL import Image

    def sheet(records: list[dict], key: str, cols: int, dest: Path, cell: int = 40) -> None:
        rows = (len(records) + cols - 1) // cols
        img = Image.new("RGBA", (cols * cell, rows * cell), (0, 0, 0, 255))
        for i, rec in enumerate(records):
            sprite = Image.open(EXTRACTED / rec[key]).convert("RGBA")
            x = (i % cols) * cell + (cell - sprite.width) // 2
            y = (i // cols) * cell + (cell - sprite.height) // 2
            img.paste(sprite, (x, y), sprite)
        img.save(dest)

    ensure_dir(EXTRACTED / "sheets")
    sheet(monsters, "idle", 10, EXTRACTED / "sheets" / "monsters-idle.png")
    sheet(monsters, "attacking", 10, EXTRACTED / "sheets" / "monsters-attacking.png")
    sheet(races, "idle", 8, EXTRACTED / "sheets" / "portraits-idle.png", cell=36)
    sheet(races, "attacking", 8, EXTRACTED / "sheets" / "portraits-attacking.png", cell=36)
    print("  contact sheets written")


# Bitmaps in hunk 69 (file offset 0x19428) that h32 blits with h77 +0xcfe(kind, data, x, y, w, h) on the ending (h32
# +0x6d0, s127): kind 2 = 2 words × 32 rows, kind 8 = 2 words × 37 (the size table at h69 +0), 4 planes, all pens drawn.
# +0x994: the ghost drawn in each OK member's slot (also the defeat's, +0x4fa); +0x3694: Zeus' raised arm at (160, 48)
# over final.pic. Shown in the combat table, so c * 17 (FORMAT.md "Defeat and ending pictures").
HUNK69_OFF = 0x19428
HUNK69_BITMAPS = [
    (0x994, 32, 32, "ui/ending-ghost.png"),
    (0x3694, 32, 37, "ui/ending-arm.png"),
    # s153: the party defeat (h32 +0x55c): the door (kind 4, 80x90) at (123, 14), sinking 90 steps; the Great Demon's face
    # (kind 3, 80x80) at (123, 19); the 5th defeat's sign (kind 6, 48x40) at (139, 39). Same table, hw4.
    (0x1814, 80, 90, "ui/decor-door.png"),
    (0xB94, 80, 80, "ui/decor-demon-face.png"),
    (0x32D4, 48, 40, "ui/decor-sorry-sign.png"),
]


# Bitmaps in other data hunks, by file offset. h54 +0xe (file 0x167aa): the grave the combat screen draws at a dying
# member's slot + (8, 13) (h53 +0x85e mode 0, kind 9 = 1 word x 19 rows; FORMAT.md "The combat screen", s132).
GAME_BITMAPS = [
    (0x167AA, 16, 19, "ui/combat-grave.png"),
]


# Hardware sprites (graphics.library SimpleSprite data: 2 control words, then per row word A (bit 0 of the colour) and
# word B (bit 1), then 2 end words), by offset in hunk 69. +0x154: the party spell's sparkle (h77 +0xf2a: GetSprite(4),
# SetRGB4 of registers 25 / 26 / 27 = EE0 / E00 / EB0, ChangeSprite, MoveSprite; height 16; FORMAT.md "The combat
# screen", s133). Colour 0 is transparent.
HUNK69_SPRITES = [
    (0x154, 16, ((0xE, 0xE, 0x0), (0xE, 0x0, 0x0), (0xE, 0xB, 0x0)), "ui/combat-sparkle.png"),
]


def extract_hunk69_sprites(game: bytes) -> list[dict]:
    from PIL import Image

    out = []
    for off, height, guns, rel in HUNK69_SPRITES:
        colours = [(0, 0, 0, 0)] + [(r * 17, g * 17, b * 17, 255) for r, g, b in guns]
        img = Image.new("RGBA", (16, height))
        px = img.load()
        base = HUNK69_OFF + off + 4  # past the two control words
        for y in range(height):
            a = read_word(game, base + 4 * y)
            b = read_word(game, base + 4 * y + 2)
            for x in range(16):
                bit = 15 - x
                px[x, y] = colours[((a >> bit) & 1) | (((b >> bit) & 1) << 1)]
        dest = EXTRACTED / rel
        ensure_dir(dest.parent)
        img.save(dest)
        out.append({"hunk69_offset": f"0x{off:x}", "width": 16, "height": height, "path": rel,
                    "expansion": "sprite: colours 1-3 from SetRGB4 (c * 17), 0 transparent"})
    return out


def extract_game_bitmaps(game: bytes) -> list[dict]:
    palette = load_palette(game, PALETTE_COMBAT_OFF, hw4=True)
    out = []
    for off, width, height, rel in GAME_BITMAPS:
        dest = EXTRACTED / rel
        ensure_dir(dest.parent)
        decode_planar(game, off, width, height, palette).save(dest)
        out.append({"file_offset": f"0x{off:x}", "width": width, "height": height, "path": rel, "expansion": "hw4"})
    return out


def extract_hunk69_bitmaps(game: bytes) -> list[dict]:
    palette = load_palette(game, PALETTE_COMBAT_OFF, hw4=True)
    out = []
    for off, width, height, rel in HUNK69_BITMAPS:
        dest = EXTRACTED / rel
        ensure_dir(dest.parent)
        decode_planar(game, HUNK69_OFF + off, width, height, palette).save(dest)
        out.append({"hunk69_offset": f"0x{off:x}", "width": width, "height": height, "path": rel,
                    "expansion": "hw4"})
    return out


def main() -> int:
    if not (UNPACKED / "game").is_file():
        print(
            f"Missing {UNPACKED / 'game'}. Unpack the ADF first:\n"
            "  xdftool reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf "
            "unpack reverse-engineering/amiga/unpacked/",
            file=sys.stderr,
        )
        return 1

    ensure_dir(EXTRACTED)
    game = (UNPACKED / "game").read_bytes()
    # The combat screen's table (h53 +0x33c -> h77 +0x466 -> LoadRGB4): 4-bit guns on screen.
    palette = load_palette(game, PALETTE_COMBAT_OFF, hw4=True)

    print("palettes")
    write_palette_json(game)
    print("monsters")
    monsters = extract_monsters(game, palette)
    print("races")
    races = extract_races(game, palette)
    print("screens")
    screens = extract_screens(game)
    screens += extract_hunk69_bitmaps(game)
    screens += extract_game_bitmaps(game)
    screens += extract_hunk69_sprites(game)
    print("sheets")
    build_spritesheets(monsters, races)

    meta = {
        "_source": "Amiga ADF PHANT volume (phantasie-v1.0-1987-ssi.adf)",
        "_decoder": "tools/scripts/extract_amiga_graphics.py",
        "_palette": {
            "combat_offset": f"0x{PALETTE_COMBAT_OFF:x}",
            "alt_offset": f"0x{PALETTE_ALT_OFF:x}",
            "expansion": "monsters and races: hw4 (c * 17, the combat table); each screen lists its own",
        },
        "monsters": monsters,
        "races": races,
        "screens": screens,
    }
    (EXTRACTED / "extraction-index.json").write_text(
        json.dumps(meta, indent=2, ensure_ascii=False) + "\n"
    )
    print(f"Wrote {EXTRACTED / 'extraction-index.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
