using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>
/// Phantasie IV, X68000 Ver.1.02 (two <c>.dim</c> disks): every IV table, <c>strings/ja.json</c>, the X68000 art pack
/// (PKH pictures, PIX sprites, P4.X's fonts and Command-panel tables) and the raw files the audio reads.
/// Ports <c>x68k_x</c>, <c>extract_x68k_p4_tables</c> / <c>_creation</c> / <c>_text</c>, <c>x68k_p4_events</c>,
/// <c>build_p4_gamedata</c>, <c>x68k_p4_pkh</c>, <c>x68k_p4_pix</c>, <c>x68k_p4_fonts</c> and <c>x68k_p4_panels</c>.
/// <para>English (user decision s158, 10-importer.md 4.6): IV's English ships as the id-keyed patch
/// <c>strings/en.json</c> (X68000 ids). Every English name or line in the tables is looked up by its string id in
/// that shipped file, falling back to the Japanese read from the player's P4.X / O_0xx / SCRn (the game's own
/// English, and ids the patch leaves out because they equal the Japanese). <c>strings/en.json</c> itself is not
/// written. The Japanese-keyed <c>translation/en.json</c> is not read.</para>
/// </summary>
public sealed class P4X68kPipeline : IImportPipeline
{
    public const string Game = "phantasie4";
    /// <summary>The art pack's root (graphics/x68000.json <c>root</c>) and its raw files' folder.</summary>
    public const string ArtRoot = "reverse-engineering/x68000/phantasie4/extracted/graphics";
    public const string RawRoot = "reverse-engineering/x68000/phantasie4/unpacked/disk1";
    /// <summary>The files graphics/x68000.json reads verbatim (OPMDRV tunes, voices, ADPCM sound effects).</summary>
    public static readonly string[] RawFiles = ["P4.X", "P4.SND", "VDATA"];

    public string ProfileId => "p4-x68000-1.02";
    public int RecipeVersion => 1;

    public void Run(ImportContext context)
    {
        var src = new P4Source(context.Files);
        var en = ShippedEnglish(context);

        context.Progress("text");
        var text = new P4Text(src.X, src.EventFile, k => src.Read($"SCR{k}"), en);

        context.Progress("tables");
        var rule = context.ReadShipped(Game, "rule-tables.json");
        if (rule is null)
            context.Warn("rule-tables.json: no shipped overlay; the file is not written");
        foreach (var (path, node) in new P4GameData(src, text).Build(rule))
            context.WriteGameData(Game, path, node, indent: 1);

        context.Progress("art");
        P4Art.Write(context, src);
        context.WriteGameData(Game, "graphics/x68000-panels.json", P4Panels.Build(src.X.Image), indent: 1);
        foreach (var (id, font) in P4Fonts.Fonts(src.X.Image))
            context.WriteGameData(Game, $"graphics/fonts/{id}.json", font, indent: 1);

        foreach (var name in RawFiles)
            context.WriteFile($"{RawRoot}/{name}", src.Read(name));

        // The string tables' own lines (10-importer.md 7.6): the shared p4 area from P4.X, and every IV id whose English
        // is the game's own (equal to the copy's text, so the shipped id-keyed patch leaves it out, 4.6).
        var own = text.Ids.Where(id => text.En(id) == text.Ja(id)).ToDictionary(id => id, text.Ja, StringComparer.Ordinal);
        ImportedStrings.Write(context, new Dictionary<string, IReadOnlyDictionary<string, string>> { [Game] = own });
    }

    /// <summary>The shipped id-keyed English (strings/en.json <c>strings</c>), empty when none ships.</summary>
    private static Dictionary<string, string> ShippedEnglish(ImportContext context)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.ReadShipped(Game, "strings/en.json")?["strings"] is JsonObject s)
            foreach (var (k, v) in s)
                if (v is JsonValue jv && jv.TryGetValue(out string? str))
                    d[k] = str;
        return d;
    }
}
