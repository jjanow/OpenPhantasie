using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P1Dos;

namespace OpenPhantasie.Importer.Pipelines;

/// <summary>
/// Phantasie I, DOS (SSI 1987): every I table of class (b) / (c) (10-importer.md 2.2) and the DOS CGA art pack.
/// Port of <c>re_common</c>, <c>extract_monsters</c> / <c>_items</c> / <c>_names</c> / <c>_towns</c> / <c>_maps</c> /
/// <c>_elementals</c>, <c>decrypt_messages</c>, <c>decrypt_scrolls</c>, <c>extract_dungeon_events</c>,
/// <c>build_item_descriptions</c>, <c>extract_dos_map_tiles</c>, <c>extract_dos_town_picture</c> and
/// <c>extract_dos_defeat_pictures</c>.
/// <para>Only read-only files are used: towns come from TWNS.INT's pristine copy (bytes 2445..5936), not TWNS.DAT.
/// The overworld is read from OUT*.DAT, which play rewrites; the runner warns for a played copy (decision 6).</para>
/// <para>Not written here: <c>asset-manifest.json</c> (its <c>map_tiles</c> are ours, its <c>assets</c> come from the
/// Amiga pack), the class (a) files, <c>guild.json</c> (a test fixture) and <c>strings/en.json</c>.</para>
/// </summary>
public sealed class P1DosPipeline : IImportPipeline
{
    public const string Game = "phantasie1";

    /// <summary>The CGA pack root (<c>graphics/dos-cga.json</c> "root"), kept under the import's <c>files/</c>.</summary>
    public const string CgaRoot = "reverse-engineering/dos/extracted";

    public string ProfileId => "p1-dos-1987";
    public int RecipeVersion => 1;

    public void Run(ImportContext ctx)
    {
        byte[] Read(string name) => ctx.Files.Read(name);

        ctx.Progress("program tables");
        var com = new ComImage(Read("PHANTASI.COM"));
        var twnsInt = Read("TWNS.INT");
        var items = ComTables.Items(com);
        var names = ComTables.Names(com, twnsInt);
        ctx.WriteGameData(Game, "monsters.json", ComTables.Monsters(com));
        ctx.WriteGameData(Game, "items.json", items);
        ctx.WriteGameData(Game, "names.json", names);

        ctx.Progress("towns, elementals, scrolls, messages");
        ctx.WriteGameData(Game, "towns.json", DataTables.Towns(twnsInt));
        ctx.WriteGameData(Game, "elementals.json", DataTables.Elementals(twnsInt), ensureAscii: true);
        var scrolls = DataTables.Scrolls(Read("SCROLLS.DTX"));
        ctx.WriteGameData(Game, "scrolls.json", scrolls);
        ctx.WriteGameData(Game, "messages.json", DataTables.Messages(Read));

        ctx.Progress("maps");
        ctx.WriteGameData(Game, "overworld.json", MapTables.Overworld(Read));
        ctx.WriteGameData(Game, "dungeons.json", MapTables.Dungeons(Read), ensureAscii: true);

        ctx.WriteGameData(Game, "item-descriptions.json", Derived.ItemDescriptions(items, scrolls));
        if (Derived.Spells(ctx.ReadShipped(Game, "spells.json") as JsonObject, ComTables.SpellNames(names)) is { } spells)
            ctx.WriteGameData(Game, "spells.json", spells, ensureAscii: true);
        else
            ctx.Warn("spells.json is not shipped: spell names were not written");

        ctx.Progress("CGA art");
        var (tiles, meta) = CgaArt.Tiles(Read("PAT"), Read("OV2.OVR"));
        foreach (var (path, image) in tiles)
            ctx.WritePng($"{CgaRoot}/tiles/{path}", image, forceAlpha: true);
        ctx.WriteJsonFile($"{CgaRoot}/tiles/tiles.json", meta, ensureAscii: true);
        ctx.WritePng($"{CgaRoot}/ui/town.png", CgaArt.Town(Read("PELNOR.IBM")), forceAlpha: true);
        foreach (var (path, image) in CgaArt.Defeat(Read("CEMEN.IBM"), Read("OV1.OVR"), Read("PRT1IBM.PAT")))
            ctx.WritePng($"{CgaRoot}/ui/{path}", image, forceAlpha: true);

        // The game's own lines of the string tables (10-importer.md 7.6).
        ImportedStrings.Write(ctx);
    }
}
