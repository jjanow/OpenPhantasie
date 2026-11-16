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

    /// <summary>s221: the note on <c>find_pictures</c> and the <c>event.*</c> slots (the same for I and II).</summary>
    public const string FindPicturesNote =
        "s221 [C] (I's GRAPHICS.md 7, the compiled BASIC of OUTDOORS / DUNGEON, II's the same): a found item shows " +
        "I$((IT - 1) / 20) (SHIELD ... GOOD TREASURE) in colour IC = 15, 7 if IX = 2 or IX >= 6, 14 if IX = 3, 3 if " +
        "IX = 4; IX is a float, so the single-id colours hit ids 41, 61 and 81 only. find_pictures: [first id, last id, " +
        "picture]. event.trap (light red 10, a spotted trap), event.stairs (grey 12), event.lock (brown 9, a locked " +
        "door, kind 14, whatever the pick), event.exit (green 5, 8 x 6). DOOR is loaded by no program (no slot). The " +
        "C64 prints them over the map; the Client draws each beside its text.";

    /// <summary>
    /// A game's words in the manifest and the map bytes that place the town / inn / dungeon sprites; s221: its ending and
    /// party-wipe slots (<see cref="EndPictures"/>, the <c>ending_scene</c> layout, I's <c>defeat_pictures</c> layout and
    /// its <c>defeat.figure.{n}</c> set).
    /// </summary>
    public sealed record Spec(
        string GameId, string Root, string Note, string TileMapsNote, string TitleNote, string PortraitsNote,
        (int Min, int Max) Town, (int Min, int Max) Inn, (int Min, int Max) Dungeon)
    {
        public IReadOnlyList<(string Slot, string File)> EndPictures { get; init; } = [];
        public JsonObject? EndingScene { get; init; }
        public JsonObject? DefeatPictures { get; init; }
    }

    /// <summary>s221: the Colodore palette as the 16 pens of an <c>ending_scene</c> (each art colour is one pen).</summary>
    public static JsonArray Pens() => J.Strs(C64Gfx.ColodoreHex);

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
        foreach (var (_, name, _, slot) in C64PhantasieArt.ItemPictureColours)
            if (slot is not null)
                pictures[slot] = $"items/{name}.png";
        foreach (var (slot, file) in spec.EndPictures)
            pictures[slot] = file;
        for (var b = 0; b < 256; b++)
        {
            var name = b >= C64PhantasieArt.FogFrom ? "fog" : $"class-{b / 10:D2}";
            pictures[$"overworld.tile.{b}"] = $"overworld/tiles/{name}-even.png";
            pictures[$"overworld.tile.{b + C64PhantasieArt.OddColumnTile}"] = $"overworld/tiles/{name}-odd.png";
        }

        var sets = new List<(string Name, string Dir, string Prefix)>
        {
            ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
            ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
            ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
        };
        // s221: CEMETARY's fate figures are CSPRIT blocks 0-14 = HSPRIT's idle blocks (the portraits, in yellow: the
        // RESURRECTED colour); no UNDEAD figure.
        if (spec.DefeatPictures is not null)
            sets.Add(("defeat.figure.{n}", "portraits/idle", "{n:D2}-"));
        var pack = J.O(
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
            ("_find_pictures", FindPicturesNote),
            ("find_pictures", J.A(C64PhantasieArt.FindPictures.Select(f => (JsonNode?)J.A([f.First, f.Last, $"event.find.{f.Name}"])))),
            ("_portraits", spec.PortraitsNote));
        if (spec.EndingScene is not null)
            pack["ending_scene"] = spec.EndingScene;
        if (spec.DefeatPictures is not null)
            pack["defeat_pictures"] = spec.DefeatPictures;
        pack["picture_sets"] = J.A(sets.Select(s => (JsonNode?)J.O(("name", s.Name), ("dir", s.Dir), ("prefix", s.Prefix), ("ext", ".png"))));
        return pack;
    }
}
