namespace OpenPhantasie.Core;

/// <summary>
/// One worn-item slot of a game (09 T2, <c>inventory-actions.md</c> §2): its id and the item ids it takes. Phantasie I:
/// <c>shield</c> 1–20, <c>armor</c> 21–40, <c>weapon</c> 41–100 (manual p.21–22).
/// </summary>
public sealed record EquipmentSlot(string Id, int FirstItemId, int LastItemId)
{
    public bool Takes(int itemId) => itemId >= FirstItemId && itemId <= LastItemId;
}

/// <summary>
/// A game's worn-item slots, from <c>equipment_slots</c> in its <c>rules.json</c>. Compared by value, so a
/// <see cref="RulesProfile"/> loaded from JSON equals the built-in one.
/// </summary>
public sealed class EquipmentLayout : IEquatable<EquipmentLayout>
{
    public EquipmentLayout(IEnumerable<EquipmentSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        Slots = slots.ToArray();
    }

    /// <summary>The slots in display order.</summary>
    public IReadOnlyList<EquipmentSlot> Slots { get; }

    /// <summary>Phantasie I: one shield, one armor, one weapon (manual p.11).</summary>
    public static EquipmentLayout PhantasieI { get; } = new([
        new EquipmentSlot(WornItems.Shield, 1, 20),
        new EquipmentSlot(WornItems.Armor, 21, 40),
        new EquipmentSlot(WornItems.Weapon, 41, 100),
    ]);

    /// <summary>The slot that takes <paramref name="itemId"/>, or null for an item that isn't worn.</summary>
    public EquipmentSlot? SlotFor(int itemId) => Slots.FirstOrDefault(s => s.Takes(itemId));

    /// <summary>The slot with id <paramref name="slotId"/>, or null.</summary>
    public EquipmentSlot? Find(string slotId) => Slots.FirstOrDefault(s => s.Id == slotId);

    /// <summary>Throws <see cref="ArgumentException"/> for no slots, a bad range, overlapping ranges or a duplicate id.</summary>
    public EquipmentLayout Validate()
    {
        if (Slots.Count == 0)
            throw new ArgumentException("equipment_slots must list at least one slot.");
        foreach (var s in Slots)
        {
            if (string.IsNullOrWhiteSpace(s.Id))
                throw new ArgumentException("An equipment slot has no id.");
            if (s.FirstItemId < 1 || s.LastItemId < s.FirstItemId)
                throw new ArgumentException($"Equipment slot '{s.Id}' has a bad item range {s.FirstItemId}..{s.LastItemId}.");
            if (!WornItems.Supports(s.Id))
                throw new ArgumentException($"Equipment slot '{s.Id}' has no field in the character model.");
        }
        if (Slots.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != Slots.Count)
            throw new ArgumentException("Equipment slot ids must be unique.");
        for (var i = 0; i < Slots.Count; i++)
            for (var j = i + 1; j < Slots.Count; j++)
                if (Slots[i].FirstItemId <= Slots[j].LastItemId && Slots[j].FirstItemId <= Slots[i].LastItemId)
                    throw new ArgumentException($"Equipment slots '{Slots[i].Id}' and '{Slots[j].Id}' overlap.");
        return this;
    }

    public bool Equals(EquipmentLayout? other) => other is not null && Slots.SequenceEqual(other.Slots);

    public override bool Equals(object? obj) => Equals(obj as EquipmentLayout);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var s in Slots)
            hash.Add(s);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Maps an equipment slot id onto the shared character record (<c>inventory-actions.md</c> §2). A worn item is an item id
/// that also sits in <see cref="Character.InventoryItemIds"/>; 0 = nothing worn. Phantasie III's <c>bow</c> slot is
/// <see cref="Character.WornBow"/> (T4).
/// </summary>
public static class WornItems
{
    public const string Shield = "shield";
    public const string Armor = "armor";
    public const string Weapon = "weapon";
    public const string Bow = "bow";
    public const string Thrown = "thrown";

    /// <summary>True when the character model has a field for slot <paramref name="slotId"/>.</summary>
    public static bool Supports(string slotId) => slotId is Shield or Armor or Weapon or Bow or Thrown;

    public static int Get(Character ch, string slotId)
    {
        ArgumentNullException.ThrowIfNull(ch);
        return slotId switch
        {
            Shield => ch.WornShield,
            Armor => ch.WornArmor,
            Weapon => ch.WornWeapon,
            Bow => ch.WornBow,
            Thrown => ch.WornThrown,
            _ => throw new NotSupportedException($"No worn-item field for slot '{slotId}'."),
        };
    }

    /// <summary>Sets the worn id of <paramref name="slotId"/> (no point recompute; see <see cref="CombatGear"/>).</summary>
    public static void Set(Character ch, string slotId, int itemId)
    {
        ArgumentNullException.ThrowIfNull(ch);
        switch (slotId)
        {
            case Shield: ch.WornShield = itemId; break;
            case Armor: ch.WornArmor = itemId; break;
            case Weapon: ch.WornWeapon = itemId; break;
            case Bow: ch.WornBow = itemId; break;
            case Thrown: ch.WornThrown = itemId; break;
            default: throw new NotSupportedException($"No worn-item field for slot '{slotId}'.");
        }
    }
}
