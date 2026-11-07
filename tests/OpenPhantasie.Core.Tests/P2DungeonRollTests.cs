using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// II's dungeon trap, lock, listen and ambush rolls (ST <c>trap_disarm 0x10db0</c>, <c>door_lock 0x111d6</c>, s92): I's
/// tests on II's roll scale <c>rand / 327</c> (0…100), passed as <see cref="TerrainRoll.Rand327"/>.
/// </summary>
public class P2DungeonRollTests
{
    /// <summary>Always the top of the asked range.</summary>
    private sealed class Top : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;
    }

    private static Party PartyWith(int skill)
    {
        var party = new Party(RulesProfile.PhantasieI);
        party.Members.Add(new Character
        {
            Name = "A",
            Status = CharacterStatus.Ok,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
            Skills = new CharacterSkills(50, 0, 0, skill, skill, skill, skill, 0),
            HpCur = 50,
            HpMax = 50,
        });
        return party;
    }

    [Fact]
    public void TopRoll_IsNinetyNineInI_AndOneHundredInII()
    {
        Assert.Equal(99, TerrainRoll.Percent.Draw(new Top()));
        Assert.Equal(100, TerrainRoll.Rand327.Draw(new Top())); // 32767 / 327
    }

    [Fact]
    public void Skill99_AlwaysSpotsAndDisarmsInI_ButIIsHundredFails()
    {
        var party = PartyWith(99);
        Assert.Equal(0, DungeonTraps.FindSpotter(party, new Top()));
        Assert.Null(DungeonTraps.FindSpotter(party, new Top(), TerrainRoll.Rand327));
        Assert.True(DungeonTraps.DisarmSucceeds(party.Members[0], new Top()));
        Assert.False(DungeonTraps.DisarmSucceeds(party.Members[0], new Top(), TerrainRoll.Rand327));
    }

    [Fact]
    public void Skill100_PicksAndListensInI_ButNotOnIIsHundred()
    {
        var party = PartyWith(100);
        Assert.Equal(0, DungeonLocks.FindPicker(party, new Top()));
        Assert.Null(DungeonLocks.FindPicker(party, new Top(), TerrainRoll.Rand327)); // 100 < 100 fails
        Assert.Null(DungeonLocks.FindListener(party, new Top(), TerrainRoll.Rand327));
    }

    [Fact]
    public void SpringDamage_ReachesTheTopOfIIsScale()
    {
        var party = PartyWith(0);
        party.Members[0].Stats = new CharacterStats(10, 10, 0, 10, 10, 10); // DEX 0: no avoid
        DungeonTraps.Spring(party, 6, new Top(), [], out var i1);
        Assert.Equal(99 * 5 * 5 / 100, i1);
        var party2 = PartyWith(0);
        party2.Members[0].Stats = new CharacterStats(10, 10, 0, 10, 10, 10);
        DungeonTraps.Spring(party2, 6, new Top(), [], out var i2, TerrainRoll.Rand327);
        Assert.Equal(100 * 5 * 5 / 100, i2);
    }
}
