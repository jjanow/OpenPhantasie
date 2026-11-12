using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P1Dos;
using OpenPhantasie.Importer.Profiles;

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
public class P1DosPipeline : IImportPipeline
{
    public const string Game = "phantasie1";

    /// <summary>The CGA pack root (<c>graphics/dos-cga.json</c> "root"), kept under the import's <c>files/</c>.</summary>
    public const string CgaRoot = "reverse-engineering/dos/extracted";

    /// <summary>The profile whose <c>strings/en.sources.json</c> alternatives this pipeline resolves.</summary>
    public const string StringSourcesProfile = "p1-dos-1987";

    public virtual string ProfileId => StringSourcesProfile;
    public int RecipeVersion => 1;

    public void Run(ImportContext ctx)
    {
        byte[] Read(string name) => ctx.Files.Read(name);

        ctx.Progress("program tables");
        var com = new ComImage(ProgramFile(ctx.Files));
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
        ctx.WriteGameData(Game, "overworld.json", MapTables.Overworld(ctx.RestoreMaps ? RestoredRegion(Read) : Read));
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
        // The Bonus build's image and overlays hold them at 1987's offsets, so it resolves 1987's alternatives.
        ImportedStrings.Write(ctx, sourcesProfile: StringSourcesProfile);
    }

    /// <summary>
    /// s205: a played copy's regions as a fresh copy has them, when the player chose so: DOS I's own Reset rule
    /// (<see cref="Core.GameSession.DosResetCells"/>, OV1 <c>0x4914</c>) on the 26×20 grid of every region but OUT1 / OUT5
    /// (shipped open, no fights, left alone by the Reset). On a fresh copy it changes nothing.
    /// </summary>
    internal static Func<string, byte[]> RestoredRegion(Func<string, byte[]> read) => name =>
    {
        var data = read(name);
        if (!name.StartsWith("OUT", StringComparison.OrdinalIgnoreCase) || name.Equals("OUT1.DAT", StringComparison.OrdinalIgnoreCase)
            || name.Equals("OUT5.DAT", StringComparison.OrdinalIgnoreCase) || data.Length < GridBytes)
            return data;
        var copy = (byte[])data.Clone();
        var grid = copy[..GridBytes];
        Core.GameSession.DosResetCells(grid);
        grid.CopyTo(copy, 0);
        return copy;
    };

    private const int GridBytes = 26 * 20;

    /// <summary>The program file: 1987's <c>PHANTASI.COM</c> or the Bonus Edition's <c>PHANTASI.EXE</c>.</summary>
    internal static byte[] ProgramFile(FileSet files) =>
        ComImage.ProgramFiles.Select(n => files.Find(n)?.Data).FirstOrDefault(d => d is not null)
        ?? throw new FileNotFoundException("PHANTASI.COM / PHANTASI.EXE not found");
}

/// <summary>
/// Phantasie I, DOS, WizardWorks Bonus Edition Ver 1.0 9/21/90 (sold as Bonus v1.1 1991; the Memorial Set's I).
/// Its program is 1987's repacked (<see cref="ComImage"/>), and every data file and every table, art or string offset
/// the 1987 pipeline reads is unchanged (<c>reverse-engineering/dos/bonus-edition.md</c>), so it runs the same steps.
/// </summary>
public sealed class P1DosBonusPipeline : P1DosPipeline
{
    public override string ProfileId => "p1-dos-bonus-1991";
}
