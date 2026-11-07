using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>III's Guild New (ovl2 <c>0x227d</c>, <c>FORMAT.md</c> §5.1): DOS's draw order and the 3d6 attributes.</summary>
public class P3CreationTests
{
    /// <summary>A seeded source that records the range of every draw once <see cref="Recording"/> is on.</summary>
    private sealed class Recorder(int seed) : IDosRandomSource
    {
        private readonly SeededDosRandomSource _inner = new(seed);
        public bool Recording { get; set; }
        public List<int> Ranges { get; } = [];

        public int Next(int minInclusive, int maxExclusive)
        {
            if (Recording)
                Ranges.Add(maxExclusive - minInclusive);
            return _inner.Next(minInclusive, maxExclusive);
        }

        public int NextDosWord()
        {
            if (Recording)
                Ranges.Add(-1);
            return _inner.NextDosWord();
        }
    }

    private static (GameSession S, Recorder Rng) Session(GameOptions? options)
    {
        var rng = new Recorder(3);
        var s = Phantasie3CampaignTests.NewGame(rng, options);
        s.OpenGuild();
        rng.Recording = true;
        return (s, rng);
    }

    [OriginalDataFact]
    public void Dos_HpDiceFirst_ThenSocialClass_Attributes3d6_Luck_Age()
    {
        var (s, rng) = Session(GameOptions.ForPreset(GameId.P3, OptionPreset.Dos));
        var c = s.RollNewMember(Race.Human, CharacterClass.Fighter);
        var hpDice = rng.Ranges.TakeWhile(r => r == 2).Count();
        Assert.True(hpDice > 0);
        Assert.Equal([100, .. Enumerable.Repeat(6, 15), 6, 7], rng.Ranges.Skip(hpDice));
        Assert.InRange(c.Stats.Luck, 8, 13);
        Assert.Equal(1, c.Level);
        Assert.Equal((int)P3Action.Thrust, c.P3.SavedAction);
    }

    [OriginalDataFact]
    public void Manual_HpNeedsCon_SoItsDiceFollowTheAttributes_RandomIsOneRoll()
    {
        var (s, rng) = Session(GameOptions.ForPreset(GameId.P3));
        var c = s.RollNewMember(Race.Human, CharacterClass.Fighter, randomRace: true);
        Assert.Equal(10, rng.Ranges[0]); // rnd(10): no re-roll for a race in the roster
        Assert.DoesNotContain(c.Race, new[] { Race.Human, Race.Dwarf, Race.Elf, Race.Halfling, Race.Gnome });
        Assert.Equal(75, rng.Ranges[1]); // a monster race rolls social class on rnd(75)
        Assert.Equal([.. Enumerable.Repeat(6, 16)], rng.Ranges.Skip(2).Take(16));
        Assert.All(rng.Ranges.Skip(18).SkipLast(2), r => Assert.Equal(2, r));
        Assert.Equal(7, rng.Ranges[^2]); // DOS's age roll
        Assert.Equal(-1, rng.Ranges[^1]); // D3-041: I's start age (a DOS word) for the manual lifespan
    }

    private sealed class Fixed(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => value;
    }

    [Fact]
    public void Attribute_Is3d6PlusModsMinus17_CappedAtRacePlus8_Floor3()
    {
        Assert.Equal(18, Phantasie3Campaign.RollAttribute(12, 10, new Fixed(6))); // 18 + 12 + 10 − 17 = 23 → race cap 18
        Assert.Equal(3, Phantasie3Campaign.RollAttribute(8, 6, new Fixed(1))); // 3 + 8 + 6 − 17 = 0 → 3
        Assert.Equal(10, Phantasie3Campaign.RollAttribute(11, 10, new Fixed(2))); // 6 + 11 + 10 − 17
    }
}
