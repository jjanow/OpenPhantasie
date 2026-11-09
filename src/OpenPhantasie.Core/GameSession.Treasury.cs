namespace OpenPhantasie.Core;

/// <summary>
/// The Guild treasury (P3 QoL, <c>docs/plan/11-guild-treasury.md</c>) for I–III: one gold pool per save, top level,
/// shared by every party. Trip gold stays in the purse (<see cref="Party.GoldCarried"/>) and found gold
/// (<see cref="LootSack.Gold"/>) until a sweep banks it; spends draw on it through <see cref="Party.Treasury"/>.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The save's Guild treasury; empty and unused while the <c>treasury</c> option is off.</summary>
    public Treasury Treasury { get; private set; } = new();

    /// <summary>True when this save keeps one Guild treasury (<see cref="GameOptions.TreasuryOn"/>).</summary>
    public bool TreasuryOn => Options.TreasuryOn;

    /// <summary>Gold the active party carries that is not banked yet: the purse plus found gold.</summary>
    public long TripGold => Party.GoldCarried + Sack.Gold;

    /// <summary>Everything the party can spend: trip gold, plus the treasury when it is on.</summary>
    public long SpendableGold => TripGold + (TreasuryOn ? Treasury.Balance : 0);

    /// <summary>The party is in a town or its Guild (where a sweep banks the purse and found gold).</summary>
    private bool InTownForTreasury => CurrentTownId is not null && Mode is GameMode.Town or GameMode.Guild;

    /// <summary>Points the party's spends at the treasury when the option is on (none when it is off).</summary>
    private void BindTreasury()
    {
        if (Party is not null)
            Party.Treasury = Options.TreasuryOn ? Treasury : null;
    }

    /// <summary>
    /// Banks what belongs in the treasury (<c>11-guild-treasury.md</c> section 3). Every roster member's account (each
    /// character once; a parked party's members are roster records too) always; in town, the active party's purse and
    /// found gold too, logged as <paramref name="townKind"/>; with <c>safe</c>, the purse and found gold anywhere
    /// (<see cref="TreasuryEntryKind.Loot"/> outside town). Idempotent; does nothing while the option is off.
    /// </summary>
    public void SweepTreasury(TreasuryEntryKind townKind = TreasuryEntryKind.Sale)
    {
        if (!TreasuryOn)
            return;
        long accounts = 0;
        foreach (var c in Roster.Slots)
            if (c is { GoldInBank: > 0 })
            {
                accounts += c.GoldInBank;
                c.GoldInBank = 0;
            }
        if (Party.Elemental is { GoldInBank: > 0 } e)
        {
            accounts += e.GoldInBank;
            e.GoldInBank = 0;
        }
        Treasury.Add(accounts, TreasuryEntryKind.Accounts);
        var inTown = InTownForTreasury;
        if (!inTown && !Options.TreasurySafe)
            return;
        var trip = Party.GoldCarried + Sack.Gold;
        Party.GoldCarried = 0;
        Sack.Gold = 0;
        Treasury.Add(trip, inTown ? townKind : TreasuryEntryKind.Loot);
    }

    /// <summary>With <c>safe</c>: bank gold won in the field at once, so no loss finds any.</summary>
    internal void BankFieldGoldIfSafe()
    {
        if (TreasuryOn && Options.TreasurySafe)
            SweepTreasury(TreasuryEntryKind.Loot);
    }

    /// <summary>The party leaves town: bank what it holds, then a new trip starts.</summary>
    private void StartTreasuryTrip()
    {
        if (!TreasuryOn)
            return;
        SweepTreasury(TreasuryEntryKind.Sale);
        Treasury.StartTrip();
    }
}

/// <summary>
/// A transfer out of a save with the Guild treasury (<c>11-guild-treasury.md</c> section 5): each character leaves with
/// an even share of the treasury (balance ÷ roster characters, rounded down) as its account gold, so the target's own
/// rules apply to it. The source save is not changed (transfers copy characters, as the originals do).
/// </summary>
public static class TreasuryShares
{
    /// <summary>The share each roster character of <paramref name="save"/> takes (0 when the save has no treasury on).</summary>
    public static long PerCharacter(SessionSnapshot save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var on = save.Options?.ToOptions(save.Game).TreasuryOn ?? false;
        var count = save.RosterSlots.Count(c => c is not null);
        return !on || count == 0 || save.Treasury is not { Balance: > 0 } t ? 0 : t.Balance / count;
    }

    /// <summary>
    /// <paramref name="save"/>'s roster with each character's share added to its account, as clones; the roster itself when
    /// there is nothing to share.
    /// </summary>
    public static IReadOnlyList<Character?> Roster(SessionSnapshot save)
    {
        var share = PerCharacter(save);
        if (share <= 0)
            return save.RosterSlots;
        return save.RosterSlots.Select(c =>
        {
            if (c?.Clone() is not { } copy)
                return null;
            copy.GoldInBank = (int)Math.Min(int.MaxValue, copy.GoldInBank + share);
            return copy;
        }).ToArray();
    }
}
