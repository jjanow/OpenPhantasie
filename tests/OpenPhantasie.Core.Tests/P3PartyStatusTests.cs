using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s141: III Amiga's Party Status, window 22 (<c>0x8832</c>, FORMAT.md 8.9): days out = the thirds counter ÷ 3, the
/// experience pool, the party's one purse, and the found items counted by the screen's kinds (<c>0x885e</c>).
/// </summary>
public class P3PartyStatusTests
{
    /// <summary>The Amiga's kinds: 1–20 Shields, 21–40 Armour, 41–60 Bows, 61–100 Weapons, 101–120 Potions, 121–140
    /// Scrolls, 141 and up Treasure.</summary>
    private static readonly IReadOnlyList<P3FoundItemKind> Kinds =
        [new(1, 20), new(21, 40), new(41, 60), new(61, 100), new(101, 120), new(121, 140), new(141, null)];

    [Fact]
    public void Count_TheAmigasBoundaries()
    {
        // The FS-UAE run's poked list (s141): counts 1 to 7, every boundary on both sides, 200 past the last item id.
        int[] ids = [20, 21, 40, 41, 60, 50, 61, 100, 80, 90, 101, 120, 110, 115, 105,
            121, 140, 130, 135, 125, 122, 141, 180, 200, 150, 160, 170, 175];
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], P3PartyStatus.Count(ids, Kinds));
        Assert.Equal([0, 0, 0, 0, 0, 0, 0], P3PartyStatus.Count([0, -1], Kinds)); // 0 or less counts nowhere
    }

    [OriginalDataFact]
    public void Of_DaysExperienceGoldAndTheSack()
    {
        var s = WithParty();
        s.TravelTime = 10; // thirds of a day
        s.Sack.Experience = 12345;
        s.Sack.Gold = 7;
        s.Party.GoldCarried = 2763340;
        s.Sack.ItemIds.AddRange([0, 20, 0, 141]); // the holes are not items
        var status = P3PartyStatus.Of(s, Kinds);
        Assert.Equal(3, status.DaysOut);
        Assert.Equal(12345, status.Experience);
        Assert.Equal(2763347, status.Gold); // the one purse: carried + found
        Assert.Equal([1, 0, 0, 0, 0, 0, 1], status.Counts);
    }
}
