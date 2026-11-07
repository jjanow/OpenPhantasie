using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class MagicalPoolTests
{
    private sealed class Queue(params int[] values) : IRandomSource
    {
        private readonly Queue<int> _q = new(values);
        public int Calls { get; private set; }
        public int Next(int minInclusive, int maxExclusive)
        {
            Calls++;
            return _q.Dequeue();
        }
    }

    private static CharacterCreationTables Tables => GameDataRepository.LoadPhantasie1().CreationTables;

    private static Character Member(string name, Race race, CharacterStats stats) =>
        new() { Name = name, Race = race, BirthRace = race, Stats = stats, Phantasie1 = { Word90 = 1 } };

    [Theory]
    [InlineData(8, 2)]
    [InlineData(9, 2)]
    [InlineData(13, 3)]
    public void Gain_IsqrtLuckTimesLuckOver11(int luck, int gain) => Assert.Equal(gain, MagicalPool.Gain(luck));

    [OriginalDataFact]
    public void Pool_RaisesOneStat_MarksVisit_OncePerKind()
    {
        var hero = Member("HERO", Race.Human, new CharacterStats(10, 10, 10, 10, 10, Luck: 13));
        var party = new Party();
        party.Members.Add(hero);
        var rng = new Queue(40); // (5·40 + 100)/100 = 3 → DEX

        var lines = MagicalPool.Apply(party, 1, Tables, rng);

        Assert.Equal(13, hero.Stats.Dex);
        Assert.Equal(3, hero.Phantasie1.Word90);
        Assert.Equal([MagicalPool.Banner, "HERO      IS CHANGED"], lines);

        // Same pool again: skipped, no roll.
        MagicalPool.Apply(party, 1, Tables, rng);
        Assert.Equal(1, rng.Calls);
        Assert.Equal(13, hero.Stats.Dex);
    }

    [OriginalDataFact]
    public void Pool_StopsAtManualRaceMaximum_ButStillMarksVisit_D051()
    {
        // Human maximum is 18 in every attribute (manual table). DOS would make STR 20.
        var hero = Member("HERO", Race.Human, new CharacterStats(17, 10, 10, 10, 10, Luck: 13));
        var dead = Member("GONE", Race.Human, new CharacterStats(10, 10, 10, 10, 10, Luck: 13));
        dead.Status = CharacterStatus.Dead;
        var party = new Party();
        party.Members.Add(hero);
        party.Members.Add(dead);

        MagicalPool.Apply(party, 2, Tables, new Queue(0)); // → STR; the dead member draws nothing
        Assert.Equal(18, hero.Stats.Str);
        MagicalPool.Apply(party, 3, Tables, new Queue(0));
        Assert.Equal(18, hero.Stats.Str);
        MagicalPool.Apply(party, 1, Tables, new Queue(0));
        Assert.Equal(105, hero.Phantasie1.Word90); // all three pools: DNG5 ev20's @90 ≥ 100 still reachable
        Assert.Equal(1, dead.Phantasie1.Word90);
    }

    [OriginalDataFact]
    public void Pool_Undead_UsesBirthRaceMaximum()
    {
        var hero = Member("BONES", Race.Undead, new CharacterStats(10, 10, 10, 10, 18, Luck: 13));
        hero.BirthRace = Race.Human;
        var party = new Party();
        party.Members.Add(hero);
        MagicalPool.Apply(party, 1, Tables, new Queue(99)); // → CHA
        Assert.Equal(18, hero.Stats.Cha);
    }

    [OriginalDataFact]
    public void Session_StepOntoPool111_AppliesIt_AndVisitSurvivesSaveLoad()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Bather", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();

        var (region, x, y) = data.OverworldRegions
            .SelectMany(r => Enumerable.Range(0, r.Tiles.Count)
                .SelectMany(ty => Enumerable.Range(0, r.Tiles[ty].Count).Select(tx => (r, tx, ty))))
            .Where(t => t.r.Tiles[t.ty][t.tx] == 111 && t.tx > 0)
            .Select(t => (t.r.RegionId, t.tx, t.ty))
            .First();
        session.Overworld.RegionId = region;
        session.Overworld.X = x - 1;
        session.Overworld.Y = y;
        var mark = session.LogSequence;
        session.MoveOverworld(CardinalDirection.East);

        Assert.Equal((x, y), (session.Overworld.X, session.Overworld.Y));
        Assert.Contains(MagicalPool.Banner, session.LogSince(mark));
        Assert.Equal(3, hero.Phantasie1.Word90);

        var loaded = GameSessionFactory.Create(data, new SeededDosRandomSource(99));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot())));
        Assert.Equal(3, loaded.Party.Members[0].Phantasie1.Word90);
    }
}
