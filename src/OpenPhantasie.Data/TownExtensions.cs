using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

public static class TownExtensions
{
    public static TownState ToRuntime(this TownDefinition def) =>
        new(def.Id, def.Index, def.Name, def.PriceIndex, def.Stock)
        {
            DosRecord = def.DosRecord is null ? null : (byte[])def.DosRecord.Clone(),
            RestockItems = def.RestockItems.ToArray(),
            Exit = def.Exit,
            TransportDestinations = def.TransportDestinations?.ToArray(),
        };
}

/// <summary>Adapts loaded <see cref="ItemDefinition"/> rows to Core's <see cref="IItemCatalog"/>.</summary>
public sealed class ItemCatalog : IItemCatalog
{
    private readonly Dictionary<int, ItemCatalogEntry> _byId;

    public ItemCatalog(IEnumerable<ItemDefinition> items, IReadOnlyDictionary<int, ItemDescription>? descriptions = null)
    {
        _byId = items.ToDictionary(
            i => i.Id,
            i => new ItemCatalogEntry
            {
                Id = i.Id,
                Name = i.Name,
                ValueFactor = i.ValueFactor,
                Value = i.Value,
                Kind = i.Kind,
                Points = i.Points,
                Requirement = i.MinStr,
                StrCheck = i.StrCheck,
                FullName = descriptions?.GetValueOrDefault(i.Id)?.FullName,
                Description = descriptions?.GetValueOrDefault(i.Id)?.Text,
            });
    }

    public int Count => _byId.Count;

    public ItemCatalogEntry? Get(int itemId) =>
        _byId.TryGetValue(itemId, out var e) ? e : null;
}
