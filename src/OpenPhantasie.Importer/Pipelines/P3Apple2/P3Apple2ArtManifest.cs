using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P3Apple2;

/// <summary>
/// Port of <c>tools/scripts/build_a2_p3_asset_manifest.py</c>: <c>GameData/phantasie3/graphics/apple2.json</c> from the
/// extraction index (<see cref="P3Apple2Art.Index"/>). The layout text and numbers are ours (the script's constants);
/// the picture paths, the tile swaps and whether the title / ending exist come from the index. From a <c>[cr]</c> copy
/// the pack has no title and no ending (its screens are the cracker's), so those fall back to the Client's own.
/// </summary>
public static class P3Apple2ArtManifest
{
    public static JsonObject Pack(JsonObject index)
    {
        foreach (var need in new[] { "monsters", "portraits", "tiles", "dungeon", "screens" })
            if (index[need] is not JsonObject { Count: > 0 })
                throw new InvalidOperationException($"extraction-index.json has no {need}");
        var screens = index["screens"]!.AsObject();
        string File(JsonNode? entry) => (string)entry!["file"]!;
        var pictures = new JsonObject();
        if (screens.ContainsKey("title"))
        {
            pictures["title.cover"] = File(screens["title-picture"]);
            pictures["title.credits"] = File(screens["title"]);
        }
        if (screens.ContainsKey("ending"))
        {
            pictures["scene.reward.good"] = File(screens["ending"]);
            pictures["scene.reward.evil"] = File(screens["ending"]);
        }
        pictures["overworld.party"] = File(index["tiles"]!["party"]);

        var pack = J.O(
            ("_schema", "phantasie.graphics-pack/v1"),
            ("_note", "Apple II Phantasie III art (tools/scripts/extract_a2_p3_graphics.py + build_a2_p3_asset_manifest.py, " +
                      "reverse-engineering/apple2/phantasie3/GRAPHICS.md). Monsters are MONSTER SHAPES sprites (raw hi-res " +
                      "bytes, drawn by MONSTER PLOT at an odd byte column, as the game); party figures, map tiles and dungeon " +
                      "marks are Applesoft shapes (SHAPES) at the game's HCOLOR; colour is an NTSC artifact model [~]. The " +
                      "title and the ending's gods' room come only from an original copy: the [cr] copy's ICE DRAGON and " +
                      "WIN PIC are the cracker's screens. Text: the remaster 5x7 font (the Apple's hi-res font, IMPTEXT, " +
                      "lacks '.', '!', '/' and more; it is in font/). Screen kept at the remaster's 320 x 200 layout: Apple " +
                      "pictures (280 x 192) are drawn 1:1 in it."),
            ("id", "apple2"),
            ("game", "phantasie3"),
            ("platform", "apple2"),
            ("name", "Apple II"),
            ("auto_select", true),
            ("root", P3Apple2Art.Root),
            ("requires", J.Strs(["monsters", "overworld/tiles", "dungeon"])),
            ("screen", J.O(("width", 320), ("height", 200), ("pixel_aspect", J.Ints([1, 1])), ("background", "#000000"))),
            ("font", "remaster-5x7"),
            ("_tile_maps", "OUTDOORS3 line 30: map byte b draws shape 37 + INT(b / 10) in HCOLOR INT(1 + b / 20), 7 x 6 cells, " +
                           "a 10 x 15 region at a time (OT files). Tiles here are keyed by the remaster's (DOS) map byte, drawn " +
                           "as the Apple class the Apple's own maps give it (GRAPHICS.md section 5); tiles 300 + class are the " +
                           "class pictures, which tile_swaps draws for the Netherworld's own terrain. PATHWAY / BRIDGE are drawn " +
                           "in HCOLOR 4 (black) or are empty shapes: not visible, as on the Apple. DUNGPLOT / DUNGON3 5030: 3 x 3 " +
                           "marks, only next to the party; wall 31; 185-199 32, 200-214 33, 215-217 drawn black (glyph 0), " +
                           "218-220 shape 35, empty in III (glyph 0), 221+ 36; any other byte erases the cell (0); the box starts " +
                           "white (DNGLD3 200: glyph 99, not yet seen); the party is the XDRAW cursor 30."),
            ("tile_maps", J.O(
                ("overworld_tile", J.Ints([7, 6])),
                ("overworld_view", J.Ints([10, 15])),
                ("dungeon_glyph", J.Ints([3, 3])),
                ("dungeon_unseen", P3Apple2Art.UnseenGlyph),
                ("dungeon_party", 30),
                ("dungeon_default", 0),
                ("dungeon_cells", J.A(new[] { new[] { 0, 1, 31 }, [185, 199, 32], [200, 214, 33], [215, 217, 0], [218, 220, 0], [221, 255, 36] }
                    .Select(c => (JsonNode?)J.Ints(c)))),
                ("tile_swaps", index["tile_swaps"]!.DeepClone()))));
        if (screens.ContainsKey("title"))
            pack["title_sequence"] = J.O(
                ("_note", "PHANTASIE III (boot program): ICE DRAGON on hi-res page 1, a FOR-loop pause, then the 7 rows of " +
                          "'HIT ANY KEY TO CONTINUE' POKEd in (lines 245-290) and a wait for a key; a key at any point goes on " +
                          "(GOSUB 300). The pauses are BASIC FOR loops (time not decoded [?]): only the last screen is shown, " +
                          "until a key."),
                ("canvas", J.Ints([280, 192])),
                ("background", "#000000"),
                ("steps", J.A([J.O(("picture", "title.credits"))])));
        pack["pictures"] = pictures;
        pack["_portraits"] = "portrait.{race}: the party figure (TOWN3 2350-2360: races 7+ shape 26, DWARF / HALFLING / GNOME " +
                             "/ PIXIE / SPRITE 25, HUMAN / ELF 19, or 27 for a MONK / PRIEST / WIZARD; social class 3 turns " +
                             "any but 25 into 4, which a race-keyed picture cannot show) with the arm 20 (idle) or 22 " +
                             "(attacking). Every body by pose is in figures/.";
        pack["picture_sets"] = J.A(new[]
        {
            ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
            ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
            ("overworld.tile.{n}", "overworld/tiles", "{n:D3}."), ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
        }.Select(s => (JsonNode?)J.O(("name", s.Item1), ("dir", s.Item2), ("prefix", s.Item3), ("ext", ".png"))));
        return pack;
    }
}
