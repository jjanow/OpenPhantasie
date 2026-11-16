using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// The C64 packs' manifest (I's and II's <c>graphics/c64.json</c>, s218 / s219): the layout the shared engine gives
/// (<see cref="C64PhantasieArt"/>) and the file names it writes; a game gives its notes and its map sprites' bytes.
/// Nothing depends on the copy, so the shipped file equals what an import writes.
/// </summary>
public static class C64PhantasieArtManifest
{
    /// <summary>DOS's cell → glyph ranges (OV2 0x21df), which the C64's dungeon characters $E6–$EC follow (live s218).</summary>
    public static readonly int[][] DungeonCells =
        [[0, 1, 0xE6], [2, 184, 0xE7], [185, 199, 0xEA], [200, 214, 0xE9], [215, 217, 0xEB], [218, 220, 0xEC], [221, 255, 0xE8]];

    /// <summary>A game's words in the manifest and the map bytes that place the town / inn / dungeon sprites.</summary>
    public sealed record Spec(
        string GameId, string Root, string Note, string TileMapsNote, string TitleNote, string PortraitsNote,
        (int Min, int Max) Town, (int Min, int Max) Inn, (int Min, int Max) Dungeon);

    public static JsonObject Pack(Spec spec)
    {
        var pictures = new JsonObject
        {
            ["title.cover"] = "screens/title.png",
            ["town.backdrop"] = "screens/town.png",
            ["overworld.party"] = "overworld/party.png",
            [$"overworld.tile.{C64PhantasieArt.TownTile}"] = "overworld/town.png",
            [$"overworld.tile.{C64PhantasieArt.InnTile}"] = "overworld/inn.png",
            [$"overworld.tile.{C64PhantasieArt.DungeonTile}"] = "overworld/dungeon.png",
        };
        for (var b = 0; b < 256; b++)
        {
            var name = b >= C64PhantasieArt.FogFrom ? "fog" : $"class-{b / 10:D2}";
            pictures[$"overworld.tile.{b}"] = $"overworld/tiles/{name}-even.png";
            pictures[$"overworld.tile.{b + C64PhantasieArt.OddColumnTile}"] = $"overworld/tiles/{name}-odd.png";
        }

        return J.O(
            ("_schema", "phantasie.graphics-pack/v1"),
            ("_note", spec.Note),
            ("id", "c64"),
            ("game", spec.GameId),
            ("platform", "c64"),
            ("name", "Commodore 64"),
            ("auto_select", true),
            ("root", spec.Root),
            ("requires", J.Strs(["monsters", "overworld/tiles", "dungeon"])),
            ("screen", J.O(("width", 320), ("height", 200), ("pixel_aspect", J.Ints([1, 1])), ("background", "#000000"))),
            ("palette", J.Strs(C64Gfx.ColodoreHex)),
            ("font", "remaster-5x7"),
            ("_tile_maps", spec.TileMapsNote),
            ("tile_maps", J.O(
                ("overworld_tile", J.Ints([8, 8])),
                ("overworld_view", J.Ints([19, 18])),
                ("dungeon_glyph", J.Ints([8, 8])),
                ("dungeon_unseen", C64PhantasieArt.UnseenGlyph),
                ("dungeon_party", C64PhantasieArt.PartyGlyph),
                ("dungeon_party_over", true),
                ("dungeon_default", 0xE7),
                ("dungeon_cells", J.A(DungeonCells.Select(c => (JsonNode?)J.Ints(c)))),
                ("odd_column_tile", C64PhantasieArt.OddColumnTile),
                ("overlays", J.A(new[]
                {
                    new[] { spec.Town.Min, spec.Town.Max, C64PhantasieArt.TownTile, -4, 0 },
                    [spec.Inn.Min, spec.Inn.Max, C64PhantasieArt.InnTile, -4, 0],
                    [spec.Dungeon.Min, spec.Dungeon.Max, C64PhantasieArt.DungeonTile, -4, 0],
                }.Select(o => (JsonNode?)J.Ints(o)))),
                ("party_offset", J.Ints([-4, 0])))),
            ("title_sequence", J.O(
                ("_note", spec.TitleNote),
                ("canvas", J.Ints([320, 200])),
                ("background", "#000000"),
                ("steps", J.A([J.O(("picture", "title.cover"))])))),
            ("pictures", pictures),
            ("_portraits", spec.PortraitsNote),
            ("picture_sets", J.A(new[]
            {
                ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
                ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
                ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
            }.Select(s => (JsonNode?)J.O(("name", s.Item1), ("dir", s.Item2), ("prefix", s.Item3), ("ext", ".png"))))));
    }
}
