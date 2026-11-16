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
                  "even-column look. No town, item, ending or defeat pictures: the Apple's town is text and CEMETARY / " +
                  "FLASH? draw in BASIC (not replayed [~]); the Client draws its plain screens there."),
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
        ("pictures", J.O(
            ("title.cover", P1Apple2Art.TitleFile),
            ("title.credits", P1Apple2Art.TitleFile),
            ("combat.sparkle", "effects/23-spell-sparkle.png"),
            ("overworld.party", "overworld/party.png"))),
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
