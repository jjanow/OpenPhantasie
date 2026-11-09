using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// QoL <c>shared-aging</c> (<c>docs/plan/13-aging-normalised.md</c>, PLAN P3 from D4-033): one aging model for I–IV. On:
/// the same lifespan per race and luck step, 1/26-year age units, grown older past 70 % (× 4/5 once), dead past 100 %, and
/// age / lifespan / "has aged" carried unchanged by every transfer. Off: each game's own rule.
/// </summary>
public class SharedAgingTests
{
    private static readonly SharedAgingModel Model = SharedAgingLoader.Shipped!;

    private static GameOptions Shared(GameId game, OptionPreset preset = OptionPreset.Manual) =>
        GameOptions.ForPreset(game, preset).With("shared-aging", "on");

    private static GameSession NewGame(GameId game, GameOptions options, int seed = 5)
    {
        var s = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(game)),
            new SeededDosRandomSource(seed));
        s.StartNewGame(options);
        s.OpenGuild();
        return s;
    }

    // ---- the option -------------------------------------------------------------------------------------------------

    [Fact]
    public void Descriptor_EveryGameQol_OffInTheOriginalPresets_OnInRemaster()
    {
        var d = RuleOptionRegistry.SharedAging;
        Assert.True(d.EveryGame);
        Assert.True(d.Qol);
        Assert.Null(d.Difference); // a remaster rule
        Assert.Equal(["off", "on"], d.Values);
        Assert.Equal(("off", "off", "on"), (d.Manual, d.Dos, d.Remaster));
        foreach (var game in Enum.GetValues<GameId>())
        {
            Assert.Single(RuleOptionRegistry.For(game), o => o.Id == "shared-aging");
            Assert.False(GameOptions.ForPreset(game).SharedAging);
            Assert.False(GameOptions.ForPreset(game, OptionPreset.Dos).SharedAging);
            Assert.True(GameOptions.ForPreset(game, OptionPreset.Remaster).SharedAging);
        }
    }

    [Fact]
    public void On_OverridesEachGamesOwnLifespanSwitches()
    {
        var iDos = GameOptions.ForPreset(GameId.P1, OptionPreset.Dos);
        Assert.True(iDos.OriginalLifespans);
        Assert.False(iDos.With("shared-aging", "on").OriginalLifespans);
        var iiSt = GameOptions.ForPreset(GameId.P2, OptionPreset.Dos);
        Assert.True(iiSt.OriginalLifespans);
        Assert.False(iiSt.With("shared-aging", "on").OriginalLifespans);
        var iiiDos = GameOptions.ForPreset(GameId.P3, OptionPreset.Dos);
        Assert.True(iiiDos.P3NoLifespans());
        Assert.False(iiiDos.With("shared-aging", "on").P3NoLifespans());
    }

    // ---- the model ----------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Model_IsIsManualBands_ElfThreeThousand()
    {
        var i = GameTables.Load(GameId.P1).CreationTables;
        Assert.Equal(i.LifespanBandsByRace, Model.BandsByRace); // I's D-003 bands (the I / II / III manuals ±10 %)
        Assert.Equal((2700, 3300), Model.Band((int)Race.Elf));
        Assert.Equal((180, 220), Model.Band((int)Race.Gnome));
        Assert.Equal((72, 88), Model.Band((int)Race.Human));
        Assert.Equal((40, 50), Model.Band((int)Race.Halfling));
        Assert.Equal((40, 50), Model.Band(P4Ids.HillGiant)); // IV's races past 14: "the others"
    }

    [OriginalDataTheory]
    [InlineData(Race.Human, 0, 1872)]  // 72 years
    [InlineData(Race.Human, 2, 2038)]  // round(26 × 78.4)
    [InlineData(Race.Elf, 5, 85800)]   // 3,300 years
    [InlineData(Race.Pixie, 5, 1300)]  // 50 years
    public void SameRaceAndLuckStep_SameLifespan_InAllFourGames(Race race, int step, int units)
    {
        Assert.Equal(units, Model.Lifespan(GameId.P1, (int)race, 8 + step));
        Assert.Equal(units, Model.Lifespan(GameId.P2, (int)race, 8 + step));
        Assert.Equal(units, Model.Lifespan(GameId.P3, (int)race, 8 + step));
        Assert.Equal(units, Model.Lifespan(GameId.P4, (int)race, 9 + step)); // IV's Luck is 9–14
        // I's own manual lifespan agrees for the same seed.
        Assert.Equal(units, CharacterCreation.LifespanFor(race, 8 + step, GameTables.Load(GameId.P1).CreationTables));
    }

    [Fact]
    public void Lifespan_LuckOutsideTheRange_ClampsToTheBand()
    {
        Assert.Equal(88 * 26, Model.Lifespan(GameId.P2, (int)Race.Human, 14)); // II's rare seed 14
        Assert.Equal(72 * 26, Model.Lifespan(GameId.P4, (int)Race.Human, 3));  // a declined Luck
    }

    // ---- I, II and III ----------------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void NewCharacter_UnderTheOriginalPreset_GetsTheSharedLifespan(GameId game)
    {
        var s = NewGame(game, Shared(game, OptionPreset.Dos)); // DOS / ST lifespans, III none: all overridden
        var elf = s.RollNewMember(Race.Elf, CharacterClass.Wizard);
        Assert.Equal(Model.Lifespan(game, (int)Race.Elf, elf.Stats.Luck), elf.Lifespan);
        Assert.InRange(elf.AgeClock, 260 + elf.Lifespan / 10, 260 + elf.Lifespan / 8); // I's start age: 10–12.5 %

        var off = NewGame(game, GameOptions.ForPreset(game, OptionPreset.Dos));
        var elfOff = off.RollNewMember(Race.Elf, CharacterClass.Wizard);
        Assert.NotEqual(Model.Lifespan(game, (int)Race.Elf, elfOff.Stats.Luck), elfOff.Lifespan); // the game's own
    }

    [Fact]
    public void TownEntry_UsesAgeUnits_NotTheDosSteps()
    {
        // Under shared-aging I's DOS lifespans are off: the compare is in age units (span 2,000 → 70 % = 1,400).
        var m = new Character { Name = "OLD", Lifespan = 2000, AgeClock = 1401, Stats = new CharacterStats(10, 10, 10, 10, 10, 10) };
        var party = new Party();
        party.Members.Add(m);
        var lines = Aging.ApplyTownEntry(party, 1, dosLifespans: Shared(GameId.P1, OptionPreset.Dos).OriginalLifespans,
            ageUnits: 0);
        Assert.Single(lines);
        Assert.True(m.HasAged);
        Assert.Equal(new CharacterStats(8, 8, 8, 8, 8, 8), m.Stats); // × 4/5
    }

    // ---- IV -------------------------------------------------------------------------------------------------------------

    private static P4Tables T4 => GameTables.Load(GameId.P4).Phantasie4!.Tables;

    [OriginalDataFact]
    public void IV_Creation_StoresTheSharedLifespan_AndIsStartAge()
    {
        var o = Shared(GameId.P4, OptionPreset.Dos);
        var c = P4CreationRecord.Create(o, T4, new SeededRandomSource(4), "ELF", P4Ids.Elf, P4Ids.Wizard,
            [10, 16, 12, 10, 10], [], 1);
        var luck = c.Abilities[P4Member.Luck];
        Assert.Equal(Model.Lifespan(GameId.P4, P4Ids.Elf, luck), c.Lifespan);
        Assert.InRange(c.AgeUnits, 260 + c.Lifespan / 10, 260 + c.Lifespan / 8);
        Assert.Equal(c.Lifespan, P4Member.From(c).Lifespan);

        // Off: no stored lifespan; the same draws give IV's own age.
        var off = P4CreationRecord.Create(GameOptions.ForPreset(GameId.P4, OptionPreset.Dos), T4, new SeededRandomSource(4),
            "ELF", P4Ids.Elf, P4Ids.Wizard, [10, 16, 12, 10, 10], [], 1);
        Assert.Equal(0, off.Lifespan);
        Assert.Equal(luck, off.Abilities[P4Member.Luck]); // the shared start age keeps the draw count
    }

    [OriginalDataFact]
    public void IV_Check_UsesTheStoredLifespan_DeclinesByFourFifths_EvenUnderX68000()
    {
        var o = Shared(GameId.P4, OptionPreset.Dos); // p4.lifespans and p4.aging = x68000, both overridden
        var a = T4.Aging;
        const int span = 2000;
        Assert.Equal(P4AgeEvent.None, P4Aging.Check(o, a, P4Ids.Human, 9, 0, 1400, false, span));
        Assert.Equal(P4AgeEvent.GrownOlder, P4Aging.Check(o, a, P4Ids.Human, 9, 0, 1401, false, span));
        Assert.Equal(P4AgeEvent.None, P4Aging.Check(o, a, P4Ids.Human, 9, 0, 1900, true, span));
        // Luck plays no part: a declined Luck does not shorten the stored lifespan.
        Assert.Equal(P4AgeEvent.None, P4Aging.Check(o, a, P4Ids.Human, 3, 0, 2000, true, span));
        Assert.Equal(P4AgeEvent.DiedOfOldAge, P4Aging.Check(o, a, P4Ids.Human, 14, 255, 2001, true, span));
        Assert.Equal(P4AgeEvent.None, P4Aging.Check(o, a, P4Ids.Human, 9, 0, 60_000, false, 0)); // none: never ages out
        Assert.Equal(12, P4Aging.ScaleOnAging(o, 15));
        Assert.Equal(0, P4Aging.ScaleOnAging(GameOptions.ForPreset(GameId.P4, OptionPreset.Dos), 15)); // off: X68000's 0
    }

    [Fact]
    public void IV_AgeCap_LiftedSoElvesCanDie()
    {
        Assert.Equal(P4Aging.AgeCap, P4Aging.AgeCapFor(GameOptions.ForPreset(GameId.P4)));
        Assert.True(P4Aging.AgeCapFor(Shared(GameId.P4)) > 3300 * 26);
    }

    [OriginalDataFact]
    public void IV_MemberSnapshot_KeepsTheLifespan()
    {
        var m = P4Member.From(P4CreationRecord.Create(Shared(GameId.P4), T4, new SeededRandomSource(4), "DWARF",
            P4Ids.Dwarf, P4Ids.Fighter, [16, 10, 12, 14, 10], [], 1));
        Assert.True(m.Lifespan > 0);
        Assert.Equal(m.Lifespan, P4MemberSnapshot.From(m).ToMember().Lifespan);
        Assert.Equal(m.Lifespan, m.Clone().Lifespan);
    }

    // ---- transfers --------------------------------------------------------------------------------------------------

    private static Character Veteran(Race race = Race.Elf) => new()
    {
        Name = "OLDEN", Race = race, BirthRace = race, Class = CharacterClass.Wizard, Status = CharacterStatus.Ok, Level = 7,
        Stats = new CharacterStats(12, 16, 13, 11, 10, 10), HpMax = 40, HpCur = 40, AgeClock = 9_999, Lifespan = 77_777,
        HasAged = true, SpellsKnown = new int[CharacterCreation.SpellCount],
        Phantasie3 = new Phantasie3CharacterData { SocialClass = 3, Origin = Phantasie3Origin.Scandor },
    };

    [OriginalDataFact]
    public void IToII_AndIIToI_CarryAgeLifespanAndHasAged()
    {
        var ii = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2)),
            new SeededDosRandomSource(76));
        ii.StartNewGame(Shared(GameId.P2, OptionPreset.Dos));
        var noSpan = Veteran(Race.Human);
        noSpan.Name = "NOSPAN";
        noSpan.Lifespan = 0;
        ii.TransferFromPhantasieI([Veteran(), noSpan]);
        var c = ii.Roster.Occupied.Single(m => m.Name == "OLDEN");
        Assert.Equal((9_999, 77_777, true), (c.AgeClock, c.Lifespan, c.HasAged));
        var none = ii.Roster.Occupied.Single(m => m.Name == "NOSPAN");
        Assert.Equal(Model.Lifespan(GameId.P2, (int)Race.Human, 10), none.Lifespan); // a missing one gets the band

        var i = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(1));
        i.StartNewGame(Shared(GameId.P1));
        i.TransferFromPhantasieII([c]);
        var back = i.Roster.Occupied.Single(m => m.Name == "OLDEN");
        Assert.Equal((9_999, 77_777, true), (back.AgeClock, back.Lifespan, back.HasAged));
    }

    [OriginalDataFact]
    public void IntoIII_UnderIiisDosRule_KeepsTheLifespan()
    {
        var s = Phantasie3CampaignTests.NewGame(options: Shared(GameId.P3, OptionPreset.Dos));
        s.TransferIntoPhantasieIII([Veteran()], GameId.P1);
        var c = s.Roster.Occupied.Single(m => m.Name == "OLDEN");
        Assert.Equal((9_999, 77_777, true), (c.AgeClock, c.Lifespan, c.HasAged));

        var off = Phantasie3CampaignTests.NewGame(options: GameOptions.ForPreset(GameId.P3, OptionPreset.Dos));
        off.TransferIntoPhantasieIII([Veteran()], GameId.P1);
        Assert.Equal(0, off.Roster.Occupied.Single(m => m.Name == "OLDEN").Lifespan); // DOS III: none
    }

    [OriginalDataFact]
    public void IIIToIVGuildJoin_CarriesAgeLifespanAndHasAged()
    {
        var o = Shared(GameId.P4, OptionPreset.Dos);
        var m = P4Hall.JoinFromPhantasieIII(o, T4, new SeededRandomSource(4), Veteran(), 2, 1);
        Assert.Equal((9_999, 77_777, true), (m.AgeUnits, m.Lifespan, m.HasAged));

        var noSpan = Veteran(Race.Dwarf);
        noSpan.Lifespan = 0;
        var d = P4Hall.JoinFromPhantasieIII(o, T4, new SeededRandomSource(4), noSpan, 3, 1);
        Assert.Equal(Model.Lifespan(GameId.P3, P4Ids.Dwarf, 10), d.Lifespan);

        // Off: IV's join rolls a new age, as before.
        var off = P4Hall.JoinFromPhantasieIII(GameOptions.ForPreset(GameId.P4, OptionPreset.Dos), T4,
            new SeededRandomSource(4), Veteran(), 2, 1);
        Assert.NotEqual(9_999, off.AgeUnits);
        Assert.False(off.HasAged);
        Assert.Equal(0, off.Lifespan);
    }
}
