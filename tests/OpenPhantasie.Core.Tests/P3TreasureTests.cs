using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>III's treasure after a fight (root <c>0xee93</c>, <c>encounters.md</c> §4): one Find Item roll per item.</summary>
public class P3TreasureTests
{
    private sealed class Script(params int[] values) : IRandomSource
    {
        private int _i;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.True(_i < values.Length, "script ran out");
            var v = values[_i++];
            Assert.InRange(v, minInclusive, maxExclusive - 1);
            return v;
        }

        public bool Done => _i == values.Length;
    }

    private static Character Member(string name, int findItem, CharacterStatus status = CharacterStatus.Ok) => new()
    {
        Name = name,
        Status = status,
        Skills = new CharacterSkills(50, 0, findItem, 0, 0, 0, 0, 0),
    };

    private static Party PartyOf(params Character[] members)
    {
        var party = new Party(RulesProfile.PhantasieI);
        party.Members.AddRange(members);
        return party;
    }

    [Fact]
    public void FirstMemberWhoRollsUnderFindItem_FindsIt_DeadDrawNoRoll_BFallsBackToAsFinder()
    {
        var dead = Member("DEAD", 99, CharacterStatus.Dead);
        var b = Member("B", 30);
        var c = Member("C", 50, CharacterStatus.Sleep); // a sleeper finds too
        var rng = new Script(31, 50, 100, 100); // a: B 31 > 30, C 50 ≤ 50; b: both fail
        var found = Phantasie3Campaign.FindTreasure(PartyOf(dead, b, c), [66, 67], rng);
        Assert.True(rng.Done);
        Assert.Equal([(66, c), (67, c)], found);
    }

    [Fact]
    public void NoFinderForA_NothingForA_AnItemZeroDrawsNoRoll()
    {
        var b = Member("B", 30);
        var rng = new Script(31, 1);
        var found = Phantasie3Campaign.FindTreasure(PartyOf(b), [66, 67], rng);
        Assert.Equal([(67, b)], found);

        var none = new Script(1);
        Assert.Equal([(67, b)], Phantasie3Campaign.FindTreasure(PartyOf(b), [0, 67], none));
        Assert.True(none.Done);
    }
}
