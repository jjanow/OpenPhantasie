using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P2St;

/// <summary>
/// Phantasie II, Atari ST v1.0 (profile <c>p2-st-1.0</c>): every II table, the Atari ST art pack and the raw files its
/// audio reads (START.PRG's title tune, PHANT.PRG's sound scripts). Ports <c>extract_st_phantasie2.py</c>,
/// <c>extract_st_p2_maps / _dungeons / _scrolls / _towns.py</c>, <c>build_p2_gamedata.py</c>,
/// <c>extract_st_p2_graphics.py</c> and <c>build_st_p2_asset_manifest.py</c> (with <c>st_prg.py</c> and
/// <c>extract_st_p2_common.py</c>).
/// <para>Both ST sets known (Pasti and TOSEC) are played copies; the region grids come from <c>MAPS.INT</c> and only
/// the static bytes of <c>OUTn.DAT</c> are read (<see cref="P2StSource"/>), so a played copy imports the same as a
/// fresh one.</para>
/// </summary>
public sealed class P2StPipeline : IImportPipeline
{
    public const string GameId = "phantasie2";
    private const string Disk1 = "reverse-engineering/atari-st/phantasie2/unpacked/disk1";

    public string ProfileId => "p2-st-1.0";

    /// <summary>2 (s163): the remaster's wording for the lines the ST lacks (shared / I tables, II's DOS-I-only ids).</summary>
    public int RecipeVersion => 2;

    public void Run(ImportContext context)
    {
        var src = new P2StSource(context.Files);

        context.Progress("art");
        var gfx = new P2StGraphics(context, src);
        gfx.Run();

        context.Progress("tables");
        var names = P2StExtract.Names(src.Phant);
        var monsters = P2StExtract.Monsters(src.Phant);
        var overworld = P2StExtract.Overworld(src);
        var dungeons = P2StExtract.Dungeons(src);
        var messages = P2StExtract.Messages(src);
        var scrolls = P2StExtract.Scrolls(src, names);
        var towns = P2StExtract.Towns(src);
        var creation = P2StExtract.CreationTables(src);
        var elementals = P2StExtract.Elementals(src);

        var b = new P2GameDataBuilder(context);
        var built = new Dictionary<string, JsonObject>
        {
            ["monsters.json"] = P2GameDataBuilder.Monsters(monsters, gfx.Index),
            ["monster-traits.json"] = P2GameDataBuilder.MonsterTraits(monsters),
            ["items.json"] = b.Items(names["items"]!.AsArray()),
            ["spells.json"] = b.Spells(names),
            ["names.json"] = P2GameDataBuilder.Names(names, monsters),
            ["overworld.json"] = P2GameDataBuilder.Overworld(overworld),
            ["dungeons.json"] = P2GameDataBuilder.Dungeons(dungeons, messages),
            ["messages.json"] = P2GameDataBuilder.Messages(messages),
            ["scrolls.json"] = P2GameDataBuilder.Scrolls(scrolls),
            ["towns.json"] = P2GameDataBuilder.Towns(towns),
            ["creation-tables.json"] = b.Creation(creation),
            ["elementals.json"] = P2GameDataBuilder.Elementals(elementals),
            ["wilderness-encounters.json"] = P2GameDataBuilder.WildernessEncounters(),
        };
        built["game.json"] = P2GameDataBuilder.Manifest(built);
        foreach (var (file, node) in built)
            context.WriteGameData(GameId, file, node, indent: 1);

        context.Progress("graphics pack");
        context.WriteJsonFile($"{P2StGraphics.Root}/asset-manifest.json", P2StManifest.AssetManifest(gfx.Index, monsters, names));
        var pack = P2StManifest.Pack(gfx.Index, context.ReadShipped(GameId, "graphics/atari-st.json"), src);
        var packBytes = PyJson.SerializeUtf8(pack, 2);
        context.WriteFile($"{P2StGraphics.Root}/pack.json", packBytes);
        context.WriteGameDataBytes(GameId, "graphics/atari-st.json", packBytes);

        // Raw files the pack's audio reads verbatim (title_sequence.tune.file, sound_effects.file).
        context.WriteFile($"{Disk1}/START.PRG", src.Start.Raw);
        context.WriteFile($"{Disk1}/PHANT.PRG", src.Phant.Raw);

        // The game's own lines of the string tables (10-importer.md 7.6).
        ImportedStrings.Write(context);
        // s163: the remaster's wording for the ids of II's chain the ST does not give (10-importer.md 7.6).
        ImportedStrings.WriteFallback(context, ImportedStrings.CommonFallbackGame, "phantasie2");
    }
}
