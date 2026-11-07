using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class AgingTests
{
    private static CharacterCreationTables Tables => GameDataRepository.LoadPhantasie1().CreationTables;

    private static Character Elder(int lifespan, int ageClock) => new()
    {
        Name = "elder",
        Status = CharacterStatus.Ok,
        Stats = new CharacterStats(15, 10, 12, 14, 9, 11),
        Word12 = 10,
        Lifespan = lifespan,
        AgeClock = ageClock,
    };

    private static Party PartyOf(params Character[] members)
    {
        var p = new Party();
        p.Members.AddRange(members);
        return p;
    }

    [OriginalDataFact]
    public void TownEntry_Past70Percent_DeclinesOnceByFourFifths()
    {
        var m = Elder(lifespan: 1000, ageClock: 701);
        var lines = Aging.ApplyTownEntry(PartyOf(m), townId: 3);
        Assert.Equal(["elder has aged"], lines);
        Assert.Equal(new CharacterStats(12, 8, 9, 11, 7, 8), m.Stats);
        Assert.Equal(8, m.Word12);
        Assert.True(m.HasAged);
        Assert.Equal(3, m.CurrentTown);

        // Second entry: already aged → no further decline.
        Assert.Empty(Aging.ApplyTownEntry(PartyOf(m), 3));
        Assert.Equal(new CharacterStats(12, 8, 9, 11, 7, 8), m.Stats);
    }

    [Fact]
    public void TownEntry_At70Percent_Exactly_DoesNothing()
    {
        var m = Elder(lifespan: 1000, ageClock: 700);
        Assert.Empty(Aging.ApplyTownEntry(PartyOf(m), 1));
        Assert.False(m.HasAged);
    }

    [OriginalDataFact]
    public void TownEntry_PastLifespan_DiesWithStatsZeroed()
    {
        var m = Elder(lifespan: 1000, ageClock: 1001);
        Assert.Equal(["elder has died"], Aging.ApplyTownEntry(PartyOf(m), 1));
        Assert.Equal(CharacterStatus.Dead, m.Status);
        Assert.Equal(new CharacterStats(0, 0, 0, 0, 0, 0), m.Stats);
        Assert.Equal(0, m.Word12);
    }

    [Fact]
    public void TownEntry_SkipsDeadMembers_AndUnknownLifespan()
    {
        var dead = Elder(1000, 2000);
        dead.Status = CharacterStatus.Dead;
        var unknown = Elder(0, 5000);
        Assert.Empty(Aging.ApplyTownEntry(PartyOf(dead, unknown), 1));
        Assert.Equal(15, dead.Stats.Str);
        Assert.Equal(CharacterStatus.Ok, unknown.Status);
    }

    [OriginalDataFact]
    public void TownEntry_DosFixtureParty_NobodyAgesOrDies()
    {
        // DOS @252 is converted ×256 on load; a fresh DOS party must survive its first town.
        var party = new Party();
        party.Members.AddRange(GuildCodec.ReadBinaryFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "GUILD.pass4.dat"), Tables).OfType<Character>());
        Assert.Equal(6, party.Members.Count);
        Assert.Empty(Aging.ApplyTownEntry(party, 1));
        Assert.All(party.Members, m => Assert.Equal(CharacterStatus.Ok, m.Status));
    }

    [OriginalDataFact]
    public void DosImport_UsesD003Bands_NotDosLifespan()
    {
        // User decision (s17): characters from DOS saves get the remaster band for their birth race.
        var chars = GuildCodec.ReadBinaryFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "GUILD.pass4.dat"), Tables)
            .OfType<Character>().ToDictionary(c => c.Name);
        Assert.Equal(CharacterCreation.LifespanFor(Race.Elf, 9, Tables), chars["wizardo"].Lifespan);     // DOS 171 steps
        Assert.Equal(CharacterCreation.LifespanFor(Race.Human, 12, Tables), chars["priesto"].Lifespan);  // DOS 12
        Assert.Equal(CharacterCreation.LifespanFor(Race.Goblin, 10, Tables), chars["gobbo"].Lifespan);   // DOS 4
        Assert.All(chars.Values, c => Assert.InRange(
            c.Lifespan,
            26 * Tables.LifespanBandsByRace[(int)c.BirthRace].Lo,
            26 * Tables.LifespanBandsByRace[(int)c.BirthRace].Hi));
    }

    [OriginalDataFact]
    public void DosImport_UndeadOrAgedCharacter_UsesBirthRaceAndClampedSeed()
    {
        var ch = new Character
        {
            Race = Race.Undead,
            BirthRace = Race.Dwarf,
            Stats = new CharacterStats(8, 8, 8, 8, 8, 6), // @10 = 6 after a ×4/5 decline
        };
        Assert.Equal(180 * 26, CharacterCreation.LifespanForImported(ch, Tables));
    }

    [OriginalDataFact]
    public void GameSession_EnterTown_RunsAging()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(5));
        var m = Elder(1000, 1001);
        session.Party.Members.Add(m);
        session.EnterTown(1);
        Assert.Equal(CharacterStatus.Dead, m.Status);
        Assert.Contains("elder has died", session.Log);
    }

    [OriginalDataTheory]
    // D-003 approved bands: lifespan = round(26 · (lo + (@10 − 8)·(hi − lo)/5)) in 2-week units.
    [InlineData(Race.Elf, 8, 2700 * 26)]
    [InlineData(Race.Elf, 13, 3300 * 26)]
    [InlineData(Race.Dwarf, 10, 196 * 26)]
    [InlineData(Race.Gnome, 13, 220 * 26)]
    [InlineData(Race.Human, 8, 72 * 26)]
    [InlineData(Race.Human, 10, 2038)]   // 78.4 years
    [InlineData(Race.Human, 13, 88 * 26)]
    [InlineData(Race.Pixie, 13, 50 * 26)]
    [InlineData(Race.Halfling, 8, 40 * 26)]
    [InlineData(Race.Minotaur, 9, 42 * 26)]
    public void Lifespan_FollowsApprovedBands_D003(Race race, int seed10, int expected)
    {
        Assert.Equal(expected, CharacterCreation.LifespanFor(race, seed10, Tables));
    }

    [OriginalDataFact]
    public void StartingAge_IsTenYearsPlus10To12Point5PercentOfLifespan()
    {
        var lifespan = 88 * 26;
        Assert.Equal(260 + (int)(lifespan * 0.1), CharacterCreation.StartingAgeClock(lifespan, new Fixed(0)));
        var max = CharacterCreation.StartingAgeClock(lifespan, new Fixed(32767));
        Assert.InRange(max, 260 + (int)(lifespan * 0.124), 260 + (int)(lifespan * 0.125));

        // Creation wires both: every human starts 17–21 years old (D-003 notes).
        for (var seed = 0; seed < 50; seed++)
        {
            var ch = CharacterCreation.Create(
                new CharacterCreationRequest { Name = "h", Race = Race.Human, Class = CharacterClass.Fighter },
                Tables, new SeededDosRandomSource(seed));
            Assert.Equal(CharacterCreation.LifespanFor(Race.Human, ch.Stats.Luck, Tables), ch.Lifespan);
            Assert.InRange(ch.AgeYears, 17, 21);
        }
    }

    private sealed class Fixed(int word) : IDosRandomSource
    {
        public int NextDosWord() => word;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    [OriginalDataFact]
    public void GameSession_LeaveTown_PutsThePartyOnTheTownTile()
    {
        // s41 [C] OV1 0x3309..0x331a: Exit copies TWNS +0x111/+0x113/+0x115 into the party position.
        var session = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(5));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Exiter", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.EnterTown(2); // PINEVILLE
        Assert.True(session.LeaveTown());
        Assert.Equal((5, 20, 5), (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y));
    }

    [OriginalDataFact]
    public void GameSession_LeaveTown_RevealsThe3x3AroundTheParty()
    {
        // [D✓] s65 DOSBox: town Exit un-fogs the 3×3 round the party at once.
        var session = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(5));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Exiter", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.EnterTown(2);
        session.Overworld.RegionId = 5; // PINEVILLE's exit region
        var region = session.CurrentOverworldRegion;
        for (var y = 4; y <= 6; y++)
        for (var x = 19; x <= 21; x++)
            if (region.GetRaw(x, y) < OverworldRegion.FogOffset)
                region.Tiles[y * OverworldRegion.Width + x] += OverworldRegion.FogOffset;
        Assert.True(session.LeaveTown());
        for (var y = 4; y <= 6; y++)
        for (var x = 19; x <= 21; x++)
            Assert.True(region.GetRaw(x, y) < OverworldRegion.FogOffset, $"({x},{y}) still fogged");
    }

    [OriginalDataTheory]
    [InlineData(1, "THAT IS THE OCEAN")]
    [InlineData(16, "THAT IS THE OCEAN")]
    [InlineData(17, "THE RIVER STYX")]
    [InlineData(19, "THE RIVER STYX")]
    public void BlockedStep_Message_ByRegion(int region, string text) =>
        Assert.Equal(text, GameSession.BlockedMessage(region)); // [C] OV2 0x618e, [D✓] s65

    [OriginalDataFact]
    public void GameSession_LeaveTown_RefusedWithEmptyParty()
    {
        var session = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(5));
        session.Party.Members.Clear();
        session.EnterTown(1);

        Assert.False(session.LeaveTown());
        Assert.Equal(GameMode.Town, session.Mode);
        Assert.Equal(TownExit.EmptyPartyMessage, session.LastMessage);
    }

    // --- Guild → Add (OV1 0x2786, s49) ---------------------------------------------------------------------------

    [OriginalDataFact]
    public void GuildAdd_SameTown_DoesNotAge_SetsTownAndPartyFlag_D067()
    {
        // [C] 0x2835..0x2851: DOS ages every Add (Q8); D-067 (user s53): only a member from another town ages.
        var session = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(5));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Adder", Race.Human, CharacterClass.Fighter);
        var before = hero.AgeClock;
        Assert.False(session.AddNeedsMoveConfirm(hero)); // created in Pelnor (CurrentTown 0 = DOS @254 0)
        Assert.True(session.TryAddToParty(hero, out _));
        Assert.Equal(before, hero.AgeClock);
        Assert.Equal(1, hero.CurrentTown);
        Assert.True(hero.InParty);

        // Drop (0x225c) clears @218 and leaves age and town alone; a re-Add in the same town doesn't age either.
        Assert.True(GuildHall.TryDropFromParty(session.Party, hero, out _));
        Assert.False(hero.InParty);
        Assert.True(session.TryAddToParty(hero, out _));
        Assert.Equal(before, hero.AgeClock);
    }

    [Fact]
    public void GuildAdd_FromAnotherTown_AsksFirst_ThenMovesTheMember()
    {
        var roster = new GuildRoster();
        var party = new Party();
        var hero = Elder(lifespan: 100_000, ageClock: 500);
        hero.CurrentTown = 1; // left in Pelnor
        Assert.True(GuildHall.TryEnroll(roster, hero, out _, out _));
        Assert.True(GuildHall.AddNeedsMoveConfirm(hero, guildTownId: 2)); // Guild of PINEVILLE
        Assert.False(GuildHall.AddNeedsMoveConfirm(hero, guildTownId: 1));
        Assert.True(GuildHall.TryAddToParty(roster, party, hero, 2, out _));
        Assert.Equal(2, hero.CurrentTown);
        Assert.Equal(504, hero.AgeClock);
        Assert.False(GuildHall.AddNeedsMoveConfirm(hero, guildTownId: 2));
    }

    [Fact]
    public void GuildAdd_Refused_OrElemental_ChangesNoAge()
    {
        var roster = new GuildRoster();
        var party = new Party();
        var outsider = Elder(lifespan: 100_000, ageClock: 500); // not enrolled → refused
        Assert.False(GuildHall.TryAddToParty(roster, party, outsider, 1, out _));
        Assert.Equal(500, outsider.AgeClock);
        Assert.False(outsider.InParty);

        // Elemental rows (pick > 0x15) copy the template into slot 7 — no age or town write (0x27d3..0x27e7).
        for (var i = 0; i < Party.MaxMembers; i++)
        {
            var m = Elder(100_000, 300);
            m.Name = $"m{i}";
            Assert.True(GuildHall.TryEnroll(roster, m, out _, out _));
            Assert.True(GuildHall.TryAddToParty(roster, party, m, 1, out _));
        }
        var fire = Elder(100_000, 700);
        Assert.True(GuildHall.TryAddElemental(party, fire, out _));
        Assert.Equal(700, party.Elemental!.AgeClock);
        Assert.Equal(0, party.Elemental.CurrentTown);
    }

    [OriginalDataFact]
    public void TownEntry_LoadedElemental_LastAdventurerSkipsAgingThatVisit_s49()
    {
        // OV1 0x19dc lowers [0x648e] before the aging loop when slot 7 holds an elemental; seen live in s49
        // (capture_s49_2026-10-23.md §3: only the control aged, the 6th aged on the next entry).
        var roster = new GuildRoster();
        var party = new Party();
        for (var i = 0; i < Party.MaxMembers; i++)
        {
            var m = Elder(lifespan: 1000, ageClock: 100);
            m.Name = $"m{i}";
            Assert.True(GuildHall.TryEnroll(roster, m, out _, out _));
            Assert.True(GuildHall.TryAddToParty(roster, party, m, 1, out _));
        }
        foreach (var m in party.Members)
            m.AgeClock = m.Name is "m0" or "m5" ? 800 : 100;
        Assert.True(GuildHall.TryAddElemental(party, Elder(100_000, 0), out _));

        Assert.Equal(["m0 has aged"], Aging.ApplyTownEntry(party, 1));
        Assert.Null(party.Elemental);
        Assert.All(party.Members, m => Assert.Equal(1, m.CurrentTown)); // @254 loop covers every slot

        Assert.Equal(["m5 has aged"], Aging.ApplyTownEntry(party, 1)); // next entry, no elemental
    }
}
