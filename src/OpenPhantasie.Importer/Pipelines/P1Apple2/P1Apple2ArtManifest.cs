using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P1Apple2;

/// <summary>
/// <c>GameData/phantasie1/graphics/apple2.json</c>, the Apple II pack's manifest (s223). Everything in it is the layout
/// the Apple's code gives (GRAPHICS.md) and the file names <see cref="P1Apple2Art"/> writes; nothing depends on the copy,
/// so the shipped file equals what an import writes. The layout is II's Apple pack's (the same engine).
/// </summary>
public static class P1Apple2ArtManifest
{
    public static JsonObject Pack() => J.O(
        ("_schema", "phantasie.graphics-pack/v1"),
        ("_note", "Apple II Phantasie I art (VERSION 1.0; the p1-apple2 import, P1Apple2Art; reverse-engineering/apple2/" +
                  "phantasie1/GRAPHICS.md). Every picture is an Applesoft shape (SHAPES, 58 shapes) drawn at the game's " +
                  "SCALE / HCOLOR and turned into colour by an NTSC artifact model [~], except the title picture (HPIC). " +
                  "I's engine is II's Apple engine, so the pack is laid out as II's. The Apple's text is the ROM font (not " +
                  "on the disks): the remaster 5x7 font stands in. Screen kept at the remaster's 320 x 200 layout: Apple " +
                  "pictures (280 x 192 hi-res pixels) are drawn 1:1 in it. Map: the Apple draws its 20 x 26 map rotated " +
                  "against the remaster's 26 x 20 grid (FORMAT.md 3.6); here the tiles follow the remaster's grid, 22 x 20 " +
                  "cells of 7 x 6. A tile's colour fringes depend on its screen column on the Apple: the pack uses the " +
                  "even-column look. No town, item, ending or defeat pictures: the Apple's town is text; CEMETARY / FLASH? " +
                  "draw in BASIC, replayed from apple2_scenes (s224)."),
        ("id", "apple2"),
        ("game", P1Apple2Art.GameId),
        ("platform", "apple2"),
        ("name", "Apple II"),
        ("auto_select", true),
        ("root", P1Apple2Art.Root),
        ("requires", J.Strs(["monsters", "overworld/tiles", "dungeon"])),
        ("screen", J.O(("width", 320), ("height", 200), ("pixel_aspect", J.Ints([1, 1])), ("background", "#000000"))),
        ("font", "remaster-5x7"),
        ("_tile_maps", "OUTDOORS 5030-5040: map byte b < 120 draws shape 37 + b / 10 (7 x 6 cell) in HCOLOR " +
                       "INT(1 + .51 * class); b >= 120 is fog, not drawn (black tiles 120-255); the party is the XDRAW " +
                       "block 43 (5175). DUNGON 5025-5062: only cells next to the party are drawn; 4 x 4 marks: bytes 0-1 " +
                       "wall (31), 185-199 (32), 200-214 (33), 215-217 drawn in HCOLOR 0 (nothing: glyph 0), 218-220 (35), " +
                       "221-255 (36), every other byte erases the cell (glyph 0); the party is the XDRAW cursor 30. Glyph " +
                       "numbers are the shape numbers."),
        ("tile_maps", J.O(
            ("overworld_tile", J.Ints([7, 6])),
            ("overworld_view", J.Ints([22, 20])),
            ("dungeon_glyph", J.Ints([4, 4])),
            ("dungeon_unseen", 0),
            ("dungeon_party", 30),
            ("dungeon_default", 0),
            ("dungeon_cells", J.A(new[] { new[] { 0, 1, 31 }, [185, 199, 32], [200, 214, 33], [215, 217, 0], [218, 220, 35], [221, 255, 36] }
                .Select(c => (JsonNode?)J.Ints(c)))))),
        ("title_sequence", J.O(
            ("_note", "HELLO prints STRATEGIC SIMULATIONS PRESENTS / VERSION 1.0 (text page, ROM font) and runs INTRO, which " +
                      "loads SHAPES and INFO, RECALLs HPIC onto hi-res page 1, shows it full-screen (lines 1000-1010) and " +
                      "waits for a key (1276); then the PHANTASIE / 1 - START IN PELNOR / 2 - CONTINUE text menu. Shown: " +
                      "HPIC until a key."),
            ("canvas", J.Ints([280, 192])),
            ("background", "#000000"),
            ("steps", J.A([J.O(("picture", "title.credits"))])))),
        ("apple2_field", J.O(
            ("_note", "OUTDOORS / DUNGON (the same lines) 1480-2006, 4100-4145, 4790-4811; GRAPHICS.md section 7. The round " +
                      "(1560-2005) plays on the mixed hi-res screen (graphics rows 0-159, text rows 20-23: POKE 34,20); the " +
                      "status, ranks, PARTY OPTIONS and orders (1000-1470) are a TEXT page, the remaster's screen there. " +
                      "Lines: the set-up's HPLOTs (4810), white; the field is left of x 138, the map right of it (not drawn " +
                      "here, black [~]). Party: slot k's body + arm at (19k, 158) (1525-1534; dead members not drawn, the " +
                      "slot kept), arm 22 from a swing (1695), 21 from a cast (1760) or a parry that took (1532), else 20; " +
                      "bodies by TOWN 2350-2356. Monsters: front rank y 135, next 135 - YS% of the front, third that - " +
                      "YS% of the second, not below 0 (1480-1500); monster i of n at H% + INT(i * 120 / n) (1550); pose B " +
                      "(S% + 1) from its swing, cast or breath to the round's end (2006); erased at its DIES (4145). The " +
                      "sparkle (23) at (19k + 19, 136) during the cast tones (1762-1764), the spell hit (24) at the " +
                      "monster's SCALE for 500 loops (4132). H%, SCALE, YS% and each picture's DRAW point are the Apple's " +
                      "data: the import's extraction-index.json (index). Pauses: PA = 120 * TIME LAG loops a line " +
                      "(361 / 7560); loop_ms is a rule of thumb for an empty Applesoft FOR / NEXT pass [~]; sparkle_ms " +
                      "the cast tones' length (CALL 770 not decoded) [?]."),
            ("screen_x", 20), ("screen_y", 4), ("width", 280), ("graphics_rows", 160), ("divider", 138),
            ("lines", J.A(new[] { new[] { 0, 0, 279, 0 }, [0, 159, 279, 159], [138, 0, 138, 159] }
                .Select(l => (JsonNode?)J.Ints(l)))),
            ("party_step", 19), ("party_y", 158), ("rank_y", 135), ("spread", 120), ("sparkle_dx", 19), ("sparkle_y", 136),
            ("text_top", 20), ("text_rows", 4), ("columns", 40), ("cell_width", 7), ("cell_height", 8),
            ("index", "extraction-index.json"),
            ("line_loops_per_time_lag", 120), ("hit_loops", 500), ("loop_ms", 1.0), ("sparkle_ms", 300.0),
            ("bodies", J.O(("monster_races_from", 7), ("monster_body", 26), ("human_elf", J.Ints([0, 2])),
                ("small_body", 25), ("plain_body", 19), ("robed_classes", J.Ints([1, 4, 5])), ("robed_body", 27),
                ("elemental_body", 26))))),
        ("pictures", J.O(
            ("title.cover", P1Apple2Art.TitleFile),
            ("title.credits", P1Apple2Art.TitleFile),
            ("combat.sparkle", "effects/23-spell-sparkle.png"),
            ("overworld.party", "overworld/party.png"))),
        ("apple2_scenes", P1Apple2Scenes.Manifest()),
        ("_portraits", "portrait.{race}: the party figure (TOWN 2350-2356, record byte 168: races 7+ shape 26, DWARF / " +
                       "HALFLING / GNOME / PIXIE / SPRITE 25, HUMAN / ELF 19, or 27 for a MONK / PRIEST / WIZARD, which a " +
                       "race-keyed picture cannot show [~]) with the arm overlay 20 (idle) or 22 (attacking). The figures by " +
                       "body and pose are in figures/."),
        ("picture_sets", J.A(new[]
        {
            ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
            ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
            ("overworld.tile.{n}", "overworld/tiles", "{n:D3}."), ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
        }.Select(s => (JsonNode?)J.O(("name", s.Item1), ("dir", s.Item2), ("prefix", s.Item3), ("ext", ".png"))))));
}
