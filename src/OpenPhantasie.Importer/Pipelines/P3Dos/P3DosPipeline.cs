using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P3Dos;

/// <summary>
/// Phantasie III, DOS SSI 1988 (also the WizardWorks 1991 re-release: same data files): every III table. Port of
/// <c>extract_dos_p3_common.py</c> (EXEPACK, overlays), <c>extract_dos_p3_tables</c> / <c>_dungeons</c> / <c>_rules</c>
/// / <c>_scrolls</c> / <c>_towns</c> / <c>_world</c> and <c>build_p3_gamedata.py</c>.
/// <para>Writes the (b) / (c) files of 10-importer.md 2.4: dungeons, elementals, items, monsters, names, overworld,
/// rule-tables, scrolls, spells, towns. The build's class (a) outputs (creation-tables, rules, game) ship.</para>
/// <para>Reads from Phantasie I only its shipped numbers and our fields (<c>spells.json</c> category / effect_summary
/// by index, <c>creation-tables.json</c> skill coefficients), so importing III never needs I's files.</para>
/// <para>Played copies: DOS III never rewrites the files read here (it loads them read-only, ovl1 0x5e0 / root
/// 0x7e08 opens with O_RDONLY|O_BINARY; root file_io 0x7bff is called in write mode only for the dungeon saves
/// DNP&lt;c&gt; / DNG&lt;c&gt;, ovl4 0x90a / 0x93a; the armory stock lives in GAMESAVE.DAT), so a played copy
/// imports the same as a fresh one.</para>
/// </summary>
public sealed class P3DosPipeline : IImportPipeline
{
    public const string Game = "phantasie3";

    public string ProfileId => "p3-dos-1988";

    public int RecipeVersion => 1;

    public void Run(ImportContext context)
    {
        var x = new P3Extract(context.Files);
        context.Progress("reading tables");
        var names = x.Names();
        var monsters = x.Monsters();
        var rt = x.RuleTables();

        var p1Spells = context.ReadShipped("phantasie1", "spells.json")?["spells"]?.AsArray()
                       ?? throw new InvalidOperationException("shipped phantasie1/spells.json is missing");
        var p1Creation = context.ReadShipped("phantasie1", "creation-tables.json")?.AsObject()
                         ?? throw new InvalidOperationException("shipped phantasie1/creation-tables.json is missing");

        var items = P3Build.Items(x.Items());
        var spells = P3Build.Spells(x.Spells(), p1Spells);
        var built = new List<(string File, JsonObject Node)>
        {
            ("monsters.json", P3Build.Monsters(monsters)),
            ("items.json", items),
            ("spells.json", spells),
            ("names.json", P3Build.Names(names, monsters["monsters"]!.AsArray(), items["items"]!.AsArray(),
                spells["spells"]!.AsArray())),
            ("overworld.json", P3Build.Overworld(x.World())),
            ("dungeons.json", P3Build.Dungeons(x.Dungeons())),
            ("scrolls.json", P3Build.Scrolls(x.Scrolls())),
            ("towns.json", P3Build.Towns(x.Towns(), Shipped(context, "towns.json"))),
            ("elementals.json", P3Build.Elementals(rt)),
            ("rule-tables.json", P3Build.RuleTables(rt, names, p1Creation, Shipped(context, "rule-tables.json"))),
        };
        foreach (var (file, node) in built)
        {
            context.Cancel.ThrowIfCancellationRequested();
            context.WriteGameData(Game, file, node, indent: 1);
        }

        // The game's own lines of the string tables (10-importer.md 7.6).
        ImportedStrings.Write(context);
    }

    /// <summary>The shipped (c) file whose hand-written parts (our notes and decision blocks) the output keeps.</summary>
    private static JsonObject Shipped(ImportContext context, string file) =>
        context.ReadShipped(Game, file)?.AsObject()
        ?? throw new InvalidOperationException($"shipped phantasie3/{file} is missing (its decision blocks would be lost)");
}
