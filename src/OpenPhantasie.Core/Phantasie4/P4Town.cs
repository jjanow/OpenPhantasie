namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie IV towns: the gate share-out, bank, inn, shop prices and gold caps
/// (<c>reverse-engineering/x68000/phantasie4/logic-specs/towns.md</c>). D4-020 (resale), D4-030 (town entry), D4-031 (gold
/// caps), D4-032 (inn time) and D4-035 (healing potions, <see cref="P4Body"/>) are <c>p4.*</c> options. Gold is long:
/// under <c>uncapped</c> it has no limit.
/// </summary>
public static class P4Town
{
    /// <summary>The gold paths of D4-031.</summary>
    public enum GoldPath { Bank, Sale, Loot, LevelUp }

    /// <summary>
    /// The party gold / account after adding on <paramref name="path"/>: 65,000 on every path under the manual (D4-031);
    /// no cap under <c>uncapped</c>.
    /// </summary>
    public static long Cap(GameOptions o, P4TownTables t, long gold, GoldPath path)
    {
        _ = path; // the manual caps every path alike; the path is kept for callers and logs
        return o.P4GoldUncapped() ? gold : Math.Min(t.GoldCapManual, gold);
    }

    /// <summary>
    /// D4-030: true when a town entry pays the party gold into the accounts (by the members' 1–3 shares) before the
    /// experience share-out (manual p. 25); the code only shares out experience.
    /// </summary>
    public static bool DepositsOnEntry(GameOptions o) => !o.P4Code(P4RuleOptions.TownEntry);

    /// <summary>
    /// Splits <paramref name="amount"/> by shares (0–3 per member; <c>_gatebank</c> / <c>_deposit</c>): per share =
    /// trunc(amount / (0.0001 + Σ shares)); each member gets per share × shares; the remainder is lost.
    /// </summary>
    public static long[] ShareOut(long amount, IReadOnlyList<int> shares)
    {
        var total = shares.Sum();
        var per = (long)Math.Truncate(amount / (0.0001 + total));
        return shares.Select(s => per * s).ToArray();
    }

    /// <summary>
    /// The experience share-out at the town gate: the pool (stored ÷ 10) × 10 by shares, each member capped at 9,999,999
    /// (<c>_gatebank</c>).
    /// </summary>
    public static long[] ShareExperience(long poolTenths, IReadOnlyList<long> experience, IReadOnlyList<int> shares)
    {
        var add = ShareOut(poolTenths * 10, shares);
        return experience.Select((x, i) => Math.Min(9_999_999, x + add[i])).ToArray();
    }

    /// <summary>
    /// Bank deposit (also D4-030's entry deposit): party gold shared into the accounts by shares, each account capped (D4-031);
    /// returns the new accounts; party gold becomes 0.
    /// </summary>
    public static long[] Deposit(GameOptions o, P4TownTables t, long partyGold, IReadOnlyList<long> accounts, IReadOnlyList<int> shares)
    {
        var add = ShareOut(partyGold, shares);
        return accounts.Select((a, i) => Cap(o, t, a + add[i], GoldPath.Bank)).ToArray();
    }

    /// <summary>
    /// Withdraw from one account: limited to the balance and, under the manual, to what fits under 65,000 party gold. The
    /// code's unsigned limit that lets an account wrap below 0 when the purse is already above 65,000 is not reproduced
    /// (D4-031: "withdraw bug fixed").
    /// </summary>
    public static (long Account, long PartyGold) Withdraw(GameOptions o, P4TownTables t, long account, long partyGold, long wanted)
    {
        var room = o.P4GoldUncapped() ? long.MaxValue : Math.Max(0, t.GoldCapManual - partyGold);
        var amount = Math.Max(0, Math.Min(wanted, Math.Min(account, room)));
        return (account - amount, partyGold + amount);
    }

    /// <summary>
    /// <c>_inn</c> (<c>0x2188</c>, towns.md §4), the town inn's and the outdoor inns' stay: every living member "slept",
    /// gets full MP and HP and <c>_heal(member, 2)</c> on the wounds; then every member's carried Rings of Magic Power /
    /// Healing and those in the pack get 20 charges. A living member whose MP-growth flag +72 is set (a spell cast in
    /// combat) may first gain 1 max MP (<see cref="P4TownRules.InnMpGrowth"/>, training.md §4.2; needs
    /// <paramref name="services"/> and <paramref name="rng"/>). s163: returns the rows the original prints in the inn's
    /// window (<c>_inn</c>: member m's row, the MP line after "slept" on the same row).
    /// </summary>
    public static IReadOnlyList<P4InnRow> InnStay(P4Party party, P4MessageWindow window, P4ServiceTables? services = null,
        IRandomSource? rng = null, P4TownTables? towns = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(window);
        var rows = new List<P4InnRow>();
        for (var index = 0; index < party.Members.Count; index++)
        {
            var m = party.Members[index];
            if (m.Alive)
            {
                // s163: _inn prints `p4x.0x3f6fc` after `p4x.0x3f6f0` on the member's row (both _wprint).
                var mpUp = services is not null && rng is not null && P4TownRules.InnMpGrowth(services, rng, m);
                window.Line(m.Name + GameText.S("p4x.0x3f6f0") + (mpUp ? GameText.S("p4x.0x3f6fc") : ""));
                rows.Add(new(index, m.Name, mpUp));
                m.Mp = m.MpMax;
                m.Hp = m.HpMax;
                P4Body.Heal(m.Wounds, towns?.InnHealPoints ?? 2);
            }
            for (var i = 0; i < P4Member.CarriedSlots; i++)
            {
                if (m.Items[i] is P4Party.RingA or P4Party.RingB)
                    m.ItemCharges[i] = P4Party.RingCharges;
            }
        }
        for (var i = 0; i < P4Party.PackSlots; i++)
        {
            if (party.Pack[i] is P4Party.RingA or P4Party.RingB)
                party.PackCharges[i] = P4Party.RingCharges;
        }
        return rows;
    }

    /// <summary>Days a town inn stay passes: two weeks under the manual (D4-032; added to the party's day counter), none in the code.</summary>
    public static int InnStayDays(GameOptions o, P4TownTables t) => o.P4Code(P4RuleOptions.InnTime) ? 0 : t.TownInnDaysManual;

    /// <summary>A town's price factor T (%) by map number (<c>_ctab</c>: 80 / 100 / 120).</summary>
    public static int PriceIndex(P4TownTables t, int map) => t.PriceIndexByMap[map];

    /// <summary>Shop price of a new item: trunc(value × T / 100) (<c>_buyitem</c>).</summary>
    public static int BuyPrice(int value, int priceIndex) => (int)Math.Truncate(value * (priceIndex / 100.0));

    /// <summary>What the shop pays: trunc(value / 4) + 1 (<c>_sell</c>).</summary>
    public static int SellPrice(int value) => value / 4 + 1;

    /// <summary>
    /// The price of an item the party sold back on the shelf: trunc(paid × 1.25) under the manual (D4-020, user s82; paid =
    /// what the shop paid), the full town price in the code.
    /// </summary>
    public static int ResalePrice(GameOptions o, P4TownTables t, int value, int priceIndex) =>
        o.P4Code(P4RuleOptions.Resale)
            ? BuyPrice(value, priceIndex)
            : SellPrice(value) * t.ResaleNumerator / t.ResaleDenominator;

    /// <summary>An item's points (DEF / ATT): D4-018 gives the Long Sword 14 under the manual (the record keeps the code's 13).</summary>
    public static int ItemPoints(GameOptions o, P4TownTables t, int itemId, int recordPoints) =>
        !o.P4Code(P4RuleOptions.LongSword) && t.ItemPointsManual.TryGetValue(itemId, out var p) ? p : recordPoints;

    /// <summary>
    /// The 8 random shop items of a town load (<c>_igen8</c>, §6.3): entry rand(2 min(m, 12) + 5) + 1 of the list (1-based
    /// rand, so entries 2 … 30), m = the sum of the party's levels (<c>_igen</c> adds the level bytes; s102).
    /// </summary>
    public static IReadOnlyList<int> RandomShopItems(P4TownTables t, IRandomSource rng, int levelSum) =>
        Enumerable.Range(0, 8)
            .Select(_ => t.ShopRandomItems[P4Dice.Rand(rng, 2 * Math.Min(levelSum, 12) + 5) + 1])
            .ToList();
}

/// <summary>s163: a row of the inn's window (<c>_inn</c>): party member <see cref="Member"/> slept, and gained 1 max MP when <see cref="MpUp"/>.</summary>
public sealed record P4InnRow(int Member, string Name, bool MpUp);

/// <summary>
/// s165: <c>_gatebank</c>'s share-out window: the party's experience shared out (<see cref="Pool"/>, the stored pool × 10)
/// and, after the shares, each member's <see cref="Gains"/> (share × per share) in party order; null while asking.
/// </summary>
public sealed record P4GateShare(long Pool, IReadOnlyList<long>? Gains);
