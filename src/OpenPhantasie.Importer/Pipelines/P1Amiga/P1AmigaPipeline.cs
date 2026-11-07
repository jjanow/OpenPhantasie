using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// Phantasie I Amiga v1.0 (1987): the Amiga art pack (<c>graphics/amiga.json</c>, root
/// <c>reverse-engineering/amiga/extracted</c>), its fonts, and the raw files its audio reads (<c>tamb.dat</c>, the title
/// tune; <c>game</c>, the sound effects). Ports <c>amiga_gfx_common.py</c>, <c>extract_amiga_graphics.py</c>,
/// <c>extract_amiga_map_graphics.py</c>, <c>build_amiga_asset_manifest.py</c> and <c>extract_amiga_fonts.py</c>.
/// <para>Reads I's DOS tables (<c>overworld.json</c>, <c>map-patches.json</c>, <c>monsters.json</c>, <c>names.json</c>)
/// for the map pictures and the asset manifest, so it runs after <c>p1-dos-1987</c>; topaz 8 comes from the Kickstart
/// 1.3 ROM when the player gave it (profile <c>kickstart-1.3</c>), otherwise it is skipped with a warning.</para>
/// </summary>
public sealed class P1AmigaPipeline : IImportPipeline
{
    /// <summary>The pack root, as <c>graphics/amiga.json</c> names it.</summary>
    internal const string Root = "reverse-engineering/amiga/extracted/";

    private const string TopazSource = "Kickstart 1.3 (34.5) ROM topaz 8, ROM 0x910a (TextAttr h78 +0x22: the screen " +
                                       "font, menu bar and menu items).";

    /// <summary>III's copy of topaz 8 is I's file with this sentence put before its note (s141, hand-written).</summary>
    private const string P3TopazNotePrefix = "s141: a copy of phantasie1/graphics/fonts/amiga-topaz-8.json (the same ROM " +
                                             "font): III Amiga draws its window titles (0x854) in the screen font, topaz 8; " +
                                             "window 22's labels matched it pixel for pixel (FORMAT.md 8.9). ";

    public string ProfileId => "p1-amiga-1.0";
    public int RecipeVersion => 1;
    public IReadOnlyList<string> RunsAfter => ["p1-dos-1987"];

    public void Run(ImportContext ctx)
    {
        var game = ctx.Files.Read("game");
        // Pillow wrote every art image in mode RGBA except the font previews (RGB).
        void Png(string rel, RgbaImage img) => ctx.WritePng(Root + rel, img, forceAlpha: true);

        ctx.Progress("sprites and pictures");
        var index = P1AmigaGraphics.Run(ctx, game, Png);
        ctx.WriteJsonFile(Root + "extraction-index.json", index);

        ctx.Progress("map pictures");
        P1AmigaMaps.Run(ctx, game, Png);

        P1AmigaManifest.Run(ctx, index);

        ctx.Progress("fonts");
        Fonts(ctx);

        // The raw files the pack's audio reads verbatim (amiga.json: ../unpacked/PHANT/tamb.dat, …/game).
        ctx.WriteFile("reverse-engineering/amiga/unpacked/PHANT/tamb.dat", ctx.Files.Read("tamb.dat"));
        ctx.WriteFile("reverse-engineering/amiga/unpacked/PHANT/game", game);

        // The game's own lines of the string tables (10-importer.md 7.6).
        ImportedStrings.Write(ctx);
    }

    private static void Fonts(ImportContext ctx)
    {
        var z6 = AmigaFonts.Z6Font(ctx.Files.Read("fonts/z6font/6"));
        ctx.WriteGameData("phantasie1", "graphics/fonts/amiga-z6font-6.json", AmigaFonts.ToFontJson("amiga-z6font-6", z6,
            "Phantasie I Amiga disk, fonts/z6font/6 (TextAttr h78 +0x36: requesters, the line)."), indent: 1);
        ctx.WritePng(Root + "ui/fonts/amiga-z6font-6.png", AmigaFonts.Preview(z6));

        // Topaz 8 (the screen font) is the Kickstart ROM's, imported by KickstartFontPipeline when the ROM is given.
        if (ctx.OptionalFiles("kickstart-1.3") is null)
            ctx.Warn("No Kickstart 1.3 ROM was given, so the Amiga screen font (topaz 8: menu bar and menus) was not " +
                     "imported; the menus use the remaster's font. Add a Kickstart 1.3 (34.5) ROM and import again.");
    }

    /// <summary>The topaz 8 files (I's font JSON and preview, III's copy) from a Kickstart 1.3 ROM; false when none.</summary>
    internal static bool WriteTopaz(ImportContext ctx, Profiles.FileSet? romFiles)
    {
        var rom = romFiles?.All().FirstOrDefault(f => f.Length == 262144 && AmigaFonts.Topaz8(f.Data) is not null);
        var topaz = rom is null ? null : AmigaFonts.Topaz8(rom.Data);
        if (topaz is null)
            return false;
        var json = AmigaFonts.ToFontJson("amiga-topaz-8", topaz, TopazSource);
        ctx.WriteGameData("phantasie1", "graphics/fonts/amiga-topaz-8.json", json, indent: 1);
        ctx.WritePng(Root + "ui/fonts/amiga-topaz-8.png", AmigaFonts.Preview(topaz));

        // III Amiga draws its window titles in the same ROM font: the same file, with III's note.
        var p3 = (JsonObject)json.DeepClone();
        p3["_note"] = P3TopazNotePrefix + (string)json["_note"]!;
        ctx.WriteGameData("phantasie3", "graphics/fonts/amiga-topaz-8.json", p3, indent: 1);
        return true;
    }
}
