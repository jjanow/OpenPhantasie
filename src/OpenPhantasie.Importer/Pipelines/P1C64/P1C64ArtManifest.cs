using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// <c>GameData/phantasie1/graphics/c64.json</c>, the C64 pack's manifest (s218). Everything in it is the layout the C64
/// code gives (GRAPHICS.md) and the file names <see cref="P1C64Art"/> writes; nothing depends on the copy, so the shipped
/// file equals what an import writes.
/// </summary>
public static class P1C64ArtManifest
{
    /// <summary>DOS's cell → glyph ranges (OV2 0x21df), which the C64's dungeon characters $E6–$EC follow (live s218).</summary>
    public static readonly int[][] DungeonCells =
        [[0, 1, 0xE6], [2, 184, 0xE7], [185, 199, 0xEA], [200, 214, 0xE9], [215, 217, 0xEB], [218, 220, 0xEC], [221, 255, 0xE8]];

    public static JsonObject Pack()
    {
        var pictures = new JsonObject
        {
            ["title.cover"] = "screens/title.png",
            ["town.backdrop"] = "screens/town.png",
            ["overworld.party"] = "overworld/party.png",
            [$"overworld.tile.{P1C64Art.TownTile}"] = "overworld/town.png",
            [$"overworld.tile.{P1C64Art.InnTile}"] = "overworld/inn.png",
            [$"overworld.tile.{P1C64Art.DungeonTile}"] = "overworld/dungeon.png",
        };
        for (var b = 0; b < 256; b++)
        {
            var name = b >= P1C64Art.FogFrom ? "fog" : $"class-{b / 10:D2}";
            pictures[$"overworld.tile.{b}"] = $"overworld/tiles/{name}-even.png";
            pictures[$"overworld.tile.{b + P1C64Art.OddColumnTile}"] = $"overworld/tiles/{name}-odd.png";
        }

        return J.O(
            ("_schema", "phantasie.graphics-pack/v1"),
            ("_note", "Commodore 64 Phantasie I art (v1.4; the p1-c64 import, P1C64Art; reverse-engineering/c64/phantasie1/" +
                      "GRAPHICS.md). Monsters are the MX n sprite bands (one figure: the band's first sprites, expanded as the " +
                      "VIC does); portraits the HSPRIT pair of each race (none for UNDEAD); the title PIC.* (multicolour " +
                      "bitmap, doubled pixels); town.backdrop the town screen's six buildings at their places (FONT+HER's town " +
                      "characters); the map from FONT's characters as UTIL-3.O's routine prints them. Colours: Colodore " +
                      "(VICE colodore.vpl). Text: the remaster 5x7 font (the C64's sets are in font/). Not drawn: the item / " +
                      "feature pictures (items/; the Client has no such slot)."),
            ("id", "c64"),
            ("game", P1C64Art.GameId),
            ("platform", "c64"),
            ("name", "Commodore 64"),
            ("auto_select", true),
            ("root", P1C64Art.Root),
            ("requires", J.Strs(["monsters", "overworld/tiles", "dungeon"])),
            ("screen", J.O(("width", 320), ("height", 200), ("pixel_aspect", J.Ints([1, 1])), ("background", "#000000"))),
            ("palette", J.Strs(C64Gfx.ColodoreHex)),
            ("font", "remaster-5x7"),
            ("_tile_maps", "UTIL-3.O $C5C3 (SYS 49152 + 63): map byte b >= 120 prints a blank in black (fog); else class " +
                           "b / 10 prints PETSCII $C695[class] OR (text column AND 1) in colour $C688[class], one 8 x 8 " +
                           "character a cell, the region's 26 x 20 cells whole (text columns 1-26, rows 5-24): tile b is the " +
                           "even map column's character, b + 256 the odd one's. Bytes 40, 41 and 50-59 also place a yellow " +
                           "sprite (town 93, inn 94, dungeon 95) 4 pixels left of the cell (overlays, drawn at their size); " +
                           "the party is white sprite 92 the same way. The Client's map box holds 19 x 18 of the cells " +
                           "around the party (the C64 shows all 26 x 20). Dungeon: FONT characters $E6-$EC in grey (12) on " +
                           "black by DOS's cell ranges (as on screen, live s218); a cell not yet seen is the solid $20; the " +
                           "party is yellow sprite 91 over its cell."),
            ("tile_maps", J.O(
                ("overworld_tile", J.Ints([8, 8])),
                ("overworld_view", J.Ints([19, 18])),
                ("dungeon_glyph", J.Ints([8, 8])),
                ("dungeon_unseen", P1C64Art.UnseenGlyph),
                ("dungeon_party", P1C64Art.PartyGlyph),
                ("dungeon_party_over", true),
                ("dungeon_default", 0xE7),
                ("dungeon_cells", J.A(DungeonCells.Select(c => (JsonNode?)J.Ints(c)))),
                ("odd_column_tile", P1C64Art.OddColumnTile),
                ("overlays", J.A(new[]
                {
                    new[] { 40, 40, P1C64Art.TownTile, -4, 0 }, [41, 41, P1C64Art.InnTile, -4, 0], [50, 59, P1C64Art.DungeonTile, -4, 0],
                }.Select(o => (JsonNode?)J.Ints(o)))),
                ("party_offset", J.Ints([-4, 0])))),
            ("title_sequence", J.O(
                ("_note", "B lines 200-420: the title picture (PIC.*) stays up while UTIL-3.O and the two fonts load (no key " +
                          "wait; its time is the disk's [?]), then the '1 - Start in Pelnor / 2 - Continue' text screen. " +
                          "Shown until a key, as the Apple packs do."),
                ("canvas", J.Ints([320, 200])),
                ("background", "#000000"),
                ("steps", J.A([J.O(("picture", "title.cover"))])))),
            ("pictures", pictures),
            ("_portraits", "portrait.{race}: HSPRIT blocks 2 race (idle) and 2 race + 1 (attacking), the pair the fight copies " +
                           "for a member of that race (live s218); yellow, the combat row's colour [~]. No UNDEAD pair."),
            ("picture_sets", J.A(new[]
            {
                ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
                ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
                ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
            }.Select(s => (JsonNode?)J.O(("name", s.Item1), ("dir", s.Item2), ("prefix", s.Item3), ("ext", ".png"))))));
    }
}
