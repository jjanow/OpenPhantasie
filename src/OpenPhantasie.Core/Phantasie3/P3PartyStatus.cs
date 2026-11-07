namespace OpenPhantasie.Core;

/// <summary>One kind the Party Status screen counts found items by: ids <see cref="First"/>…<see cref="Last"/> (null = no
/// upper bound).</summary>
public sealed record P3FoundItemKind(int First, int? Last)
{
    public bool Holds(int id) => id >= First && (Last is not { } last || id <= last);
}

/// <summary>
/// s141: III Amiga's Party Status, window 22 (<c>0x8832</c>, FORMAT.md 8.9) — the trip summary, the manual's Wilderness
/// Inspect: days out (the counter in thirds of a day ÷ 3, <see cref="P3TravelTime.DaysOut"/>), the party's experience
/// pool, the gold the party carries (its one purse), and the found items counted by kind. The kinds are the screen's own
/// table (pack data: the Amiga's 1–20 Shields … 141 and up Treasure, <c>0x885e</c>); an id in no kind is not counted.
/// </summary>
public sealed record P3PartyStatus(int DaysOut, long Experience, long Gold, IReadOnlyList<int> Counts)
{
    /// <summary>The summary of <paramref name="session"/> now, the found items counted by <paramref name="kinds"/>.</summary>
    public static P3PartyStatus Of(GameSession session, IReadOnlyList<P3FoundItemKind> kinds) =>
        new(session.Campaign.DaysOut(session.TravelTime), session.Sack.Experience,
            Phantasie3Campaign.PartyGold(session), Count(session.Sack.Items, kinds));

    /// <summary>Per kind, how many of <paramref name="ids"/> it holds (the first kind that holds an id counts it).</summary>
    public static IReadOnlyList<int> Count(IEnumerable<int> ids, IReadOnlyList<P3FoundItemKind> kinds)
    {
        var counts = new int[kinds.Count];
        foreach (var id in ids)
            for (var k = 0; k < kinds.Count; k++)
                if (kinds[k].Holds(id))
                {
                    counts[k]++;
                    break;
                }
        return counts;
    }
}
