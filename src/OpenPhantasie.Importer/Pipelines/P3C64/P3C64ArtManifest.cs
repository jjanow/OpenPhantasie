using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P3C64;

/// <summary>
/// <c>GameData/phantasie3/graphics/c64.json</c>, III's C64 pack manifest (s220; <see cref="P3C64Art"/>,
/// <c>reverse-engineering/c64/phantasie3/GRAPHICS.md</c>). Nothing depends on the copy (v1.0 and v1.2 (alt) give the same
/// pictures but the title's version line), so the shipped file equals what an import writes.
/// </summary>
public static class P3C64ArtManifest
{
    /// <summary>
    /// Scandor's map bytes the C64 draws as another tile (s220 [C] in the data, the choice per byte [~]): the remaster
    /// plays DOS III's map, whose tile ids the C64's <c>MATERIALMAP</c> keeps in 3,685 of 3,750 cells; for the others
    /// (water 0 / 1 swapped, the town tiles permuted, DOS's desert and mountain edge tiles that the C64 draws as one
    /// tile) each DOS byte takes the C64 tile found most often in its cells (a tie: the byte's own tile if among them,
    /// else the lowest). The planes keep the byte's own tile.
    /// </summary>
    public static readonly (int Dos, int C64)[] ScandorSwaps =
    [
        (0, 1), (1, 0), (30, 31), (31, 30), (32, 33), (33, 34), (34, 35), (35, 36), (36, 37), (37, 38), (38, 32),
        (91, 244), (93, 100), (95, 244), (96, 100), (101, 100), (104, 90), (105, 98), (107, 111), (112, 242), (114, 197),
        (115, 133), (116, 133), (118, 143), (122, 146), (123, 146), (126, 10), (131, 139), (132, 146), (138, 143),
        (144, 143), (155, 158), (156, 158), (157, 158), (159, 158), (160, 158), (161, 158), (162, 158), (163, 158),
        (164, 158), (165, 158), (166, 158), (167, 158), (168, 158), (169, 19), (170, 158), (171, 158), (172, 158),
        (173, 158), (174, 158), (175, 18), (176, 158), (177, 158), (178, 158), (179, 158), (180, 16), (181, 158),
        (182, 19), (183, 158), (184, 17), (185, 17), (186, 158), (187, 158), (243, 237), (249, 0),
    ];

    /// <summary>The remaster's region id of Scandor (the swaps apply there only).</summary>
    public const int ScandorRegion = 0;

    public static JsonObject Pack()
    {
        var pictures = new JsonObject
        {
            ["title.cover"] = "screens/title.png",
            ["town.backdrop"] = "screens/town.png",
            ["overworld.party"] = "overworld/party.png",
            ["scene.astral-plane"] = "screens/astral-plane.png",
            ["scene.reward.good"] = "screens/reward-good.png",
            ["scene.reward.evil"] = "screens/reward-evil.png",
            ["portrait.elemental.idle"] = "portraits/elemental/air-idle.png",
            ["portrait.elemental.attacking"] = "portraits/elemental/air-attacking.png",
        };
        foreach (var c in P3C64Art.ClassFigures)
            foreach (var pose in new[] { "idle", "attacking" })
                pictures[$"portrait.class.{c}.{pose}"] = $"portraits/class/{pose}/{c}-{C64Slug(P3C64Art.Classes[c])}.png";

        return J.O(
            ("_schema", "phantasie.graphics-pack/v1"),
            ("_note", "Commodore 64 Phantasie III art (v1.0 / v1.2; the p3-c64 import, P3C64Art; reverse-engineering/c64/" +
                "phantasie3/GRAPHICS.md). III's own engine, not I / II's. Monsters: MONSTERS' sprite blocks by record byte 27, " +
                "colours from PHANT.OBJ's tables (multicolour or hires), a giant two sprites high; portraits: HEROSPRITES' " +
                "sets, a Monk / Ranger / Priest / Wizard by class (portrait.class.*), a Thief / Fighter by race; the title " +
                "TITLE (Koala); the town strip PTOWN1; the map PSET0's characters by DATA's tile tables; the defeat and the " +
                "gods' pictures TOT / GUT / SCHLECHT. Colours: Colodore (VICE colodore.vpl). Text: the remaster 5x7 font " +
                "(the C64's sets are in font/)."),
            ("id", "c64"),
            ("game", P3C64Art.GameId),
            ("platform", "c64"),
            ("name", "Commodore 64"),
            ("auto_select", true),
            ("root", P3C64Art.RootPath),
            ("requires", J.Strs(["monsters", "overworld/tiles", "dungeon"])),
            ("screen", J.O(("width", 320), ("height", 200), ("pixel_aspect", J.Ints([1, 1])), ("background", "#000000"))),
            ("palette", J.Strs(C64Gfx.ColodoreHex)),
            ("font", "remaster-5x7"),
            ("_tile_maps", "PHANT.OBJ's wilderness view (live s220): 10 x 9 cells of 2 x 2 PSET0 characters (16 x 16), the " +
                "party on view cell (4, 4); tile b = DATA's four character / colour-RAM tables at b, multicolour on black " +
                "with $D022 1, $D023 5. tile_swaps: Scandor's DOS bytes drawn as the C64 tile found in their cells " +
                "(P3C64ArtManifest.ScandorSwaps) [~]. Near the map's edges the view stops as the remaster's other packs " +
                "do (the C64's edge rule not read) [~]. " + P3C64Art.DungeonNote),
            ("tile_maps", J.O(
                ("overworld_tile", J.Ints([16, 16])),
                ("overworld_view", J.Ints([10, 9])),
                ("overworld_party_cell", J.Ints([4, 4])),
                ("dungeon_glyph", J.Ints([P3C64Art.DungeonGlyphSize.W, P3C64Art.DungeonGlyphSize.H])),
                ("dungeon_unseen", P3C64Art.DungeonUnseen),
                ("dungeon_party", P3C64Art.DungeonParty),
                ("dungeon_default", P3C64Art.DungeonDefault),
                ("dungeon_cells", J.A(P3C64Art.DungeonCells.Select(c => (JsonNode?)J.Ints(c)))),
                ("tile_swaps", J.A(ScandorSwaps.Select(s => (JsonNode?)J.Ints([ScandorRegion, s.Dos, s.C64])))))),
            ("title_sequence", J.O(
                ("_note", "PHANTASIE III (boot) loads PHANT.OBJ, which shows TITLE (a Koala picture) while the game loads; " +
                    "shown until a key, as the other C64 packs do."),
                ("canvas", J.Ints([320, 200])),
                ("background", "#000000"),
                ("steps", J.A([J.O(("picture", "title.cover"))])))),
            ("pictures", pictures),
            ("_portraits", "portrait.{race}: HEROSPRITES set PHANT.OBJ $1DFE[race] in colour $1E0E[race], the figure a " +
                "Thief or Fighter shows; portrait.class.{class}: set $1DF2[class] in colour $1DF8[class] for a Monk, Ranger, " +
                "Priest or Wizard (drawn before the race's) [C]. Hires, frames idle / attacking (/ casting, not drawn). The " +
                "elemental: the Mud Elemental's monster picture ($B2) in its kind's colour (Air 1, Mist 14, Mud 8); the " +
                "pack shows Air's [~]."),
            ("picture_sets", J.A(new[]
            {
                ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
                ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
                ("overworld.tile.{n}", "overworld/tiles", "{n:D3}."), ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
            }.Select(s => (JsonNode?)J.O(("name", s.Item1), ("dir", s.Item2), ("prefix", s.Item3), ("ext", ".png"))))));
    }

    private static string C64Slug(string s) => P1C64.C64PhantasieArt.Slug(s);
}
