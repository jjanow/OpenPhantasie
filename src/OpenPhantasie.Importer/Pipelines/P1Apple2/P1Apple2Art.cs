using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2Apple2;
using OpenPhantasie.Importer.Pipelines.P2St;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P1Apple2;

/// <summary>
/// The Apple II art of Phantasie I (s223; formats and evidence in <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c>).
/// I's engine is II's: OUTDOORS / DUNGON / TOWN draw the monsters, party figures, map tiles, dungeon marks and spell
/// effects with the same lines, so II's shape steps (<see cref="A2Graphics.RunShapes"/>) draw I's <c>SHAPES</c> (58
/// shapes; no rock, shape 59) and <c>MN0</c>..<c>MN9</c>. The title is <c>HPIC</c> (INTRO 1000–1276). Then
/// (<see cref="Import"/>) the pack <c>GameData/phantasie1/graphics/apple2.json</c> (<see cref="P1Apple2ArtManifest"/>).
/// </summary>
public static class P1Apple2Art
{
    public const string GameId = "phantasie1";
    public const string Root = "reverse-engineering/apple2/phantasie1/extracted/graphics";

    /// <summary>I's programs name no terrain (II's OUTDOORS2 line 120 does): the files are labelled by class.</summary>
    private static readonly string[] TerrainLabels = [.. Enumerable.Range(0, 12).Select(t => $"class-{t:D2}")];

    /// <summary>The effects I draws (OUTDOORS / DUNGON 1762 / 1764 and 4132); there is no shape 59 (II's rock).</summary>
    private static readonly (int Shape, string Use)[] Effects = [(23, "spell-sparkle"), (24, "spell-hit")];

    public const string TitleFile = "screens/title.png";

    /// <summary>Draw every picture from the run's files (side A: <c>SHAPES</c>, <c>HPIC</c>, <c>MN0</c>..<c>MN9</c>).</summary>
    internal static A2Graphics Build(FileSet files, ImportContext? ctx)
    {
        var a = new P1Apple2Tables.Sides(files);
        var gfx = new A2Graphics(ctx, Root);
        gfx.RunShapes(new ShapeTable(a.A("SHAPES")), P1Apple2Tables.MonsterRecords(files), TerrainLabels, Effects,
            screens =>
            {
                var hpic = a.A("HPIC");
                if (hpic.Length < 0x2000)
                    throw new InvalidDataException($"HPIC: {hpic.Length} bytes, expected a hi-res page (8192)");
                gfx.Save(TitleFile, HiresPage.FromApple(hpic).ToImage(0, 0, 280, 192, transparentBlack: false));
                screens["title"] = J.O(("file", TitleFile), ("size", J.Ints([280, 192])),
                    ("note", "HPIC (INTRO 1000-1010: RECALLed onto page 1, shown full-screen, then a key, line 1276)."));
            },
            "Phantasie I Apple II (VERSION 1.0) side A: SHAPES ($451F, 58 shapes), HPIC (hi-res page), MN0..MN9 " +
            "(monster shape / scale / hcolor). The p1-apple2 import (P1Apple2Art); notes " +
            "reverse-engineering/apple2/phantasie1/GRAPHICS.md.");
        return gfx;
    }

    /// <summary>The import step: the art under <see cref="Root"/> and the pack <c>graphics/apple2.json</c>.</summary>
    public static void Import(ImportContext context)
    {
        Build(context.Files, context);
        context.WriteGameDataBytes(GameId, "graphics/apple2.json", PyJson.SerializeUtf8(P1Apple2ArtManifest.Pack(), 2, ensureAscii: true));
    }
}
