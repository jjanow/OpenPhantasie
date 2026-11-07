using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

public static class OverworldRegionExtensions
{
    public static OverworldRegion ToRuntime(this OverworldRegionDefinition def)
    {
        var flat = new byte[def.GridWidth * def.GridHeight];
        for (var y = 0; y < def.GridHeight; y++)
        for (var x = 0; x < def.GridWidth; x++)
            flat[y * def.GridWidth + x] = (byte)def.Tiles[y][x];

        var features = def.Features
            .Select(f => new OverworldFeature
            {
                X = f.X,
                Y = f.Y,
                Arg = f.Arg,
                DestX = f.DestX,
                DestY = f.DestY,
            })
            .ToList();

        // Text pages may be sparse by index — pad to max index.
        var maxPage = def.TextPages.Count == 0 ? -1 : def.TextPages.Max(p => p.Index);
        var pages = Enumerable.Repeat("", Math.Max(0, maxPage + 1)).ToList();
        var rawPages = Enumerable.Repeat("", Math.Max(0, maxPage + 1)).ToList();
        foreach (var p in def.TextPages)
        {
            pages[p.Index] = p.Text;
            rawPages[p.Index] = p.Raw ?? p.Text;
        }

        // T4: a plane map (III) has its own size and stores raw tile ids 0-255, so it has no fog encoding.
        var region = def.Plane is null
            ? new OverworldRegion(def.RegionId, def.Id, flat, features, pages)
            : new OverworldRegion(def.RegionId, def.Id, flat, features, pages, def.GridWidth, def.GridHeight, hasFog: false);
        region.RawTextPages = rawPages;
        return region;
    }

    /// <summary>
    /// The overworld of <paramref name="data"/>, with <paramref name="campaign"/>'s town aliases (default: the data's
    /// game's campaign; none for a game whose campaign is not built).
    /// </summary>
    public static OverworldTraversal CreateTraversal(this GameDataRepository data, ICampaign? campaign = null)
    {
        campaign ??= Campaigns.IsBuilt(data.Game) ? Campaigns.For(data.Game) : null;
        return new(data.OverworldRegions.Select(r => r.ToRuntime()), data.TravelTime, campaign?.OverworldTownAliases,
            data.Rules.Terrain);
    }
}
