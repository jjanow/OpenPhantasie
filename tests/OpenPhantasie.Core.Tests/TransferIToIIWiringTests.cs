using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T5: the I → II transfer wired into a Phantasie II game (<see cref="GameSession.TransferFromPhantasieI"/>,
/// <c>atari-st/phantasie2/logic-specs/transfer.md</c> §7) and I's roster file (<see cref="TransferSource"/>).
/// </summary>
public class TransferIToIIWiringTests
{
    private static readonly GameDataRepository P1Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P1));
    private static readonly GameDataRepository P2Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));

    private static GameSession NewIi(GameOptions? options = null)
    {
        var s = GameSessionFactory.Create(P2Data, new SeededDosRandomSource(76));
        s.StartNewGame(options ?? GameOptions.ForPreset(GameId.P2));
        return s;
    }

    private static Character Veteran(string name, CharacterClass cls, int ordinal = 0) => new()
    {
        Name = name,
        Race = Race.Human,
        BirthRace = Race.Human,
        Class = cls,
        Status = CharacterStatus.Ok,
        Level = 9,
        Stats = new CharacterStats(15, 14, 13, 12, 11, 10),
        HpMax = 80,
        HpCur = 60,
        MagicMax = 20,
        MagicCur = 20,
        GoldInBank = 20_000,
        Experience = 200_000,
        RosterOrdinal = ordinal,
        InParty = true,
        SpellsKnown = new int[CharacterCreation.SpellCount],
    };

    /// <summary>A remaster I game's roster: characters made through I's Guild (no DOS <c>@92</c>), two in the party.</summary>
    private static GameSession IGameWithParty()
    {
        var i = GameSessionFactory.Create(P1Data, new SeededDosRandomSource(1));
        i.StartNewGame();
        i.OpenGuild();
        foreach (var (name, cls) in new[] { ("GLADE", CharacterClass.Fighter), ("RANGO", CharacterClass.Ranger), ("MERLA", CharacterClass.Wizard) })
        {
            var rolled = i.RollNewMember(Race.Human, cls);
            Assert.True(i.TryEnrollNewMember(rolled, name, out var err), err);
            if (name != "MERLA")
                Assert.True(i.TryAddToParty(rolled, out err), err);
        }
        return i;
    }

    [OriginalDataFact]
    public void OnlyANewPhantasieIIGame_RunsTheTransfer()
    {
        var i = GameSessionFactory.Create(P1Data, new SeededDosRandomSource(1));
        i.StartNewGame();
        Assert.Throws<InvalidOperationException>(() => i.TransferFromPhantasieI([Veteran("A", CharacterClass.Fighter)]));

        var ii = NewIi();
        ii.OpenGuild();
        var rolled = ii.RollNewMember(Race.Human, CharacterClass.Fighter);
        Assert.True(ii.TryEnrollNewMember(rolled, "MADE", out _));
        Assert.True(ii.TryAddToParty(rolled, out _));
        Assert.Throws<InvalidOperationException>(() => ii.TransferFromPhantasieI([Veteran("A", CharacterClass.Fighter)]));
    }

    [OriginalDataFact]
    public void Transfer_IsANewGame_WithIsConvertedRoster()
    {
        var i = IGameWithParty();
        var save = SessionCodec.ReadJson(SessionCodec.WriteJson(i.CaptureSnapshot())); // I's roster file
        var ii = NewIi();
        var result = ii.TransferFromPhantasieI(TransferSource.RosterOf(save, GameId.P1));

        Assert.Equal(3, result.Transferred);
        Assert.Empty(result.LeftBehind);
        Assert.Equal(22, ii.Roster.Slots.Length);
        // Slot for slot (Guild number = slot + 1), names kept, I's party flag dropped, II-only fields zero.
        for (var k = 0; k < i.Roster.Slots.Length; k++)
            Assert.Equal(i.Roster.Slots[k]?.Name, ii.Roster.Slots[k]?.Name);
        foreach (var c in ii.Roster.Occupied)
        {
            Assert.False(c.InParty);
            Assert.Equal(0, c.Level);
            Assert.Equal([TransferIToII.StarterShield, TransferIToII.StarterWeapon, TransferIToII.StarterArmor], c.InventoryItemIds);
            Assert.Equal(Phantasie2Origin.Gelnor, c.Phantasie2!.Origin);
            Assert.Equal(0, c.Phantasie2.QuestScore);
            Assert.Equal(Phantasie2Campaign.HomeTownId, c.CurrentTown); // Pippacott
        }
        // A new game: Pippacott, nobody in the party, no purse, sack or dungeon save; I's game is not changed.
        Assert.Equal(GameMode.Town, ii.Mode);
        Assert.Equal(Phantasie2Campaign.HomeTownId, ii.CurrentTownId);
        Assert.True(ii.FreshStart);
        Assert.Empty(ii.Party.Members);
        Assert.Null(ii.Party.Elemental);
        Assert.Equal(0, ii.Party.GoldCarried);
        Assert.Empty(ii.Sack.Items);
        Assert.Null(ii.DungeonSave);
        Assert.Empty(ii.OtherParties);
        Assert.Equal(2, i.Party.Members.Count);
        Assert.All(i.Party.Members, m => Assert.True(m.InParty));
        // II's elementals are II's own templates, not I's (the Guild's elemental list is the campaign's data).
        Assert.DoesNotContain(ii.Roster.Occupied, c => c.Name.Contains("ELEM", StringComparison.Ordinal));
    }

    [OriginalDataFact]
    public void RemasterMadeCharacters_GetTheLevelZeroXpRaise()
    {
        // A character made in the remaster has no DOS @92 (RosterOrdinal 0); its slot is occupied all the same, so the
        // §4 step 2 raise ("enough experience to improve") runs for it.
        var i = IGameWithParty();
        foreach (var c in i.Roster.Occupied)
        {
            Assert.Equal(0, c.RosterOrdinal);
            c.Experience = 0;
        }
        var ii = NewIi();
        ii.TransferFromPhantasieI(i.Roster.Slots);
        foreach (var c in ii.Roster.Occupied)
        {
            Assert.True(c.Experience > 0, c.Name);
            Assert.Equal(TransferIToII.LevelZeroMinimum(c), c.Experience);
        }
        Assert.Equal([1, 2, 3], ii.Roster.Occupied.Select(c => c.RosterOrdinal));
        Assert.All(i.Roster.Occupied, c => Assert.Equal(0, c.RosterOrdinal)); // the input is never changed
    }

    [OriginalDataFact]
    public void TheSavesTransferOptions_ReachTheConversion_D2002()
    {
        Character?[] roster = [Veteran("RANGO", CharacterClass.Ranger, 1), Veteran("MONK", CharacterClass.Monk, 2)];

        var manual = NewIi(GameOptions.ForPreset(GameId.P2, OptionPreset.Manual));
        manual.TransferFromPhantasieI(roster);
        Assert.Equal(0, manual.Roster.Slots[0]!.SpellsKnown[SpellId.HealingI]);
        Assert.Equal(0, manual.Roster.Slots[1]!.SpellsKnown[SpellId.FireflashI]);

        var st = NewIi(GameOptions.ForPreset(GameId.P2, OptionPreset.Dos)); // Original (Atari ST)
        st.TransferFromPhantasieI(roster);
        Assert.Equal(1, st.Roster.Slots[0]!.SpellsKnown[SpellId.HealingI]);
        Assert.Equal(1, st.Roster.Slots[1]!.SpellsKnown[SpellId.FireflashI]);

        var noGain = NewIi(GameOptions.ForPreset(GameId.P2).With(RuleOptionRegistry.TransferGold.Id, "no-gain"));
        var poor = Veteran("POOR", CharacterClass.Fighter, 1);
        poor.GoldInBank = 40;
        noGain.TransferFromPhantasieI([poor]);
        Assert.Equal(40, noGain.Roster.Slots[0]!.GoldInBank);
    }

    /// <summary>An I roster grown past 22 slots under I's <c>guild-limit</c> = <c>none</c>.</summary>
    private static Character?[] LongRoster()
    {
        var slots = new Character?[30];
        slots[0] = Veteran("FIRST", CharacterClass.Fighter, 1);
        slots[21] = Veteran("LAST22", CharacterClass.Priest, 22);
        slots[22] = Veteran("PAST1", CharacterClass.Wizard, 23);
        slots[29] = Veteran("PAST2", CharacterClass.Thief, 30);
        return slots;
    }

    [OriginalDataFact]
    public void GuildLimitNone_ConvertsEverySlot_AtItsGuildNumber()
    {
        var ii = NewIi(GameOptions.ForPreset(GameId.P2).With(RuleOptionRegistry.GuildLimit.Id, "none"));
        var result = ii.TransferFromPhantasieI(LongRoster());
        Assert.Equal(4, result.Transferred);
        Assert.Empty(result.LeftBehind);
        Assert.Equal(30, ii.Roster.Slots.Length);
        Assert.Equal("PAST1", ii.Roster.Slots[22]!.Name);
        Assert.Equal("PAST2", ii.Roster.Slots[29]!.Name);
        Assert.Equal(0, ii.Roster.Slots[29]!.Level);
        Assert.True(ii.Roster.HasRoom); // unlimited: N)EW still has room
    }

    [OriginalDataFact]
    public void GuildLimitGame_ConvertsTheFirst22_AndNamesTheRest()
    {
        var ii = NewIi(GameOptions.ForPreset(GameId.P2).With(RuleOptionRegistry.GuildLimit.Id, "game"));
        var result = ii.TransferFromPhantasieI(LongRoster());
        Assert.Equal(2, result.Transferred);
        Assert.Equal(["PAST1", "PAST2"], result.LeftBehind);
        Assert.Equal(22, ii.Roster.Slots.Length);
        Assert.Equal("LAST22", ii.Roster.Slots[21]!.Name);
    }

    [OriginalDataFact]
    public void Transferred_TrainsToLevelOne_ForNothing_WithHpStillZero()
    {
        // roster-live.md §4.3 (live): level 0 → 1 costs 0 gold; HP max rises, HP current stays 0 until the inn.
        var ii = NewIi();
        ii.TransferFromPhantasieI([Veteran("ZENNO", CharacterClass.Fighter, 1)]);
        var zenno = ii.Roster.Slots[0]!;
        ii.OpenGuild();
        Assert.True(ii.TryAddToParty(zenno, out var err), err);
        Assert.Equal(0, ii.TrainingGoldCost(zenno));
        Assert.True(ii.TrainingWouldSucceed(zenno));
        ii.Train(zenno);
        Assert.Equal(1, zenno.Level);
        Assert.True(zenno.HpMax > 0);
        Assert.Equal(0, zenno.HpCur);
    }

    [OriginalDataFact]
    public void RosterFile_MustBeAPhantasieISave()
    {
        var ii = NewIi();
        Assert.Throws<InvalidDataException>(() => TransferSource.RosterOf(ii.CaptureSnapshot(), GameId.P1));

        var dir = Directory.CreateTempSubdirectory("p2-transfer-");
        try
        {
            var path = Path.Combine(dir.FullName, "savegame-p1.json");
            SessionCodec.WriteJsonFile(path, IGameWithParty().CaptureSnapshot());
            var roster = TransferSource.ReadRoster(path, GameId.P1);
            Assert.Equal(["GLADE", "RANGO", "MERLA"], roster.OfType<Character>().Select(c => c.Name));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
