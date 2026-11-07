using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// Port of <c>build_a2_p2_asset_manifest.py</c>: <c>GameData/phantasie2/graphics/apple2.json</c>. Everything in it is
/// our own layout data (the script's constants, kept here) except the picture paths, read from the extraction index.
/// </summary>
internal static class A2Manifest
{
    public static JsonObject Pack(JsonObject index)
    {
        foreach (var need in new[] { "monsters", "portraits", "tiles", "dungeon", "effects", "screens" })
            if (index[need] is not JsonObject { Count: > 0 })
                throw new InvalidOperationException($"extraction-index.json has no {need}");
        string File(string group, string key) => (string)index[group]![key]!["file"]!;
        return J.O(
            ("_schema", "phantasie.graphics-pack/v1"),
            ("_note", "Apple II Phantasie II art (v2/5/86; tools/scripts/extract_a2_p2_graphics.py + build_a2_p2_asset_manifest.py, " +
                      "reverse-engineering/apple2/phantasie2/GRAPHICS.md). Every picture is an Applesoft shape (SHAPES) drawn " +
                      "at the game's SCALE / HCOLOR and turned into colour by an NTSC artifact model [~], except the title " +
                      "picture (ICE DRAGON). The Apple's text is the ROM font (not on the disks): the remaster 5x7 font " +
                      "stands in. Screen kept at the remaster's 320 x 200 layout: Apple pictures (280 x 192 hi-res pixels) " +
                      "are drawn 1:1 in it. Map: the Apple draws its 20 x 26 map rotated against the remaster's 26 x 20 " +
                      "grid (FORMAT.md overworld); here the tiles follow the remaster's grid, 22 x 20 cells of 7 x 6. A tile's " +
                      "colour fringes depend on its screen column on the Apple: the pack uses the even-column look."),
            ("id", "apple2"),
            ("game", "phantasie2"),
            ("platform", "apple2"),
            ("name", "Apple II"),
            ("auto_select", true),
            ("root", A2Graphics.Root),
            ("requires", J.Strs(["monsters", "overworld/tiles", "dungeon"])),
            ("screen", J.O(("width", 320), ("height", 200), ("pixel_aspect", J.Ints([1, 1])), ("background", "#000000"))),
            ("font", "remaster-5x7"),
            ("_tile_maps", "OUTDOORS2 5030-5040: map byte b < 120 draws shape 37 + b / 10 (7 x 6 cell); b >= 120 is fog, not " +
                           "drawn (black tiles 120-255). DUNGON2 5025-5062: only cells next to the party are drawn; 4 x 4 marks: " +
                           "bytes 0-1 wall (31), 185-199 (32), 200-214 (33), 215-217 drawn in HCOLOR 0 (nothing: glyph 0), " +
                           "218-220 (35), 221-255 (36), every other byte erases the cell (glyph 0); the party is the XDRAW " +
                           "cursor 30. Glyph numbers are the shape numbers."),
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
                ("_note", "PHANTASIE II (boot program): a text page (SSI PRESENTS / PHANTASIE II / DESCENT INTO THE " +
                          "NETHERWORLD, ROM font), then ICE DRAGON on hi-res page 2 with HPLOT lightning flashing on page 1 " +
                          "(random lines, lines 50-96), then the 'HIT ANY KEY TO CONTINUE' rows POKEd in (243-257) and a wait " +
                          "for a key; a key at any point goes on (line 300). The pauses are BASIC FOR loops (time not " +
                          "decoded [?]) and the lightning is random, so only the last screen is shown, until a key."),
                ("canvas", J.Ints([280, 192])),
                ("background", "#000000"),
                ("steps", J.A([J.O(("picture", "title.credits"))])))),
            ("pictures", J.O(
                ("title.cover", File("screens", "ice-dragon")),
                ("title.credits", File("screens", "title")),
                ("combat.sparkle", File("effects", "23")),
                ("combat.rock", File("effects", "59")),
                ("overworld.party", File("tiles", "party")))),
            ("_portraits", "portrait.{race}: the party figure (TOWN2 2350-2356: races 7+ shape 26, DWARF / HALFLING / GNOME / " +
                           "PIXIE / SPRITE 25, HUMAN / ELF 19, or 27 for a MONK / PRIEST / WIZARD, which a race-keyed picture " +
                           "cannot show) with the arm overlay 20 (idle) or 22 (attacking). The figures by body and pose are " +
                           "in figures/."),
            ("picture_sets", J.A(new[]
            {
                ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
                ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
                ("overworld.tile.{n}", "overworld/tiles", "{n:D3}."), ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
            }.Select(s => (JsonNode?)J.O(("name", s.Item1), ("dir", s.Item2), ("prefix", s.Item3), ("ext", ".png"))))));
    }
}
