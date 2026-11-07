using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T5, the I / II → III transfer (<c>phantasie3/FORMAT.md</c> section 6; D3-001…D3-007, D3-016, D3-030):
/// <see cref="GameSession.TransferIntoPhantasieIII"/> and <see cref="Phantasie3Campaign.AdmitTransferred"/>.
/// </summary>
public class TransferToIIITests
{
    private static GameSession NewIii(GameOptions? options = null, IDosRandomSource? rng = null) =>
        Phantasie3CampaignTests.NewGame(rng, options);

    private static GameOptions Dos => GameOptions.ForPreset(GameId.P3, OptionPreset.Dos);

    private static Character IMember(string name, CharacterClass cls = CharacterClass.Fighter, Race race = Race.Human,
        int level = 7, int score = 0) => new()
    {
        Name = name, Race = race, BirthRace = race, Class = cls, Status = CharacterStatus.Ok, Level = level,
        Stats = new CharacterStats(15, 12, 13, 11, 10, 9), HpMax = 60, HpCur = 50, MagicMax = 9, MagicCur = 4,
        GoldInBank = 7_000, Experience = 30_000, Score = score, AgeClock = 600, Lifespan = 1_500,
        SpellsKnown = new int[CharacterCreation.SpellCount], InventoryItemIds = { 150, 141 }, WornWeapon = 75,
    };

    [OriginalDataFact]
    public void Manual_LevelZero_XpPerOldLevel_Noble_KitAndClassSpell_StatsAndAgeKept()
    {
        var s = NewIii();
        var wizard = IMember("MERLA", CharacterClass.Wizard, Race.Elf, level: 7);
        var result = s.TransferIntoPhantasieIII([wizard], GameId.P1);

        Assert.Equal(1, result.Transferred);
        var c = s.Roster.Occupied.Single(m => m.Name == "MERLA");
        Assert.Equal(0, c.Level); // D3-007 / D3-016: the manual's level 0
        Assert.Equal(700, c.Experience); // 100 × old level
        Assert.Equal(3, c.P3.SocialClass); // Noble
        var fromCreationRoll = c.GoldInBank - 7_000 / 35; // the social-class roll's gold + old ÷ 35
        Assert.Contains(fromCreationRoll, new[] { 0, 256, 512, 768 });
        Assert.Equal(new CharacterStats(15, 12, 13, 11, 10, 9), c.Stats);
        Assert.Equal((Race.Elf, CharacterClass.Wizard), (c.Race, c.Class));
        Assert.Equal((600, 1_500), (c.AgeClock, c.Lifespan));
        Assert.Equal(Phantasie3Campaign.HomeTownId, c.CurrentTown);
        Assert.Equal(Phantasie3Origin.Gelnor, c.P3.Origin);
        Assert.Equal([4, 22, 41, 64], c.InventoryItemIds); // the class kit; I's items are gone
        Assert.Equal((4, 22, 41, 64), (c.WornShield, c.WornArmor, c.WornBow, c.WornWeapon));
        Assert.Equal(1, c.SpellsKnown[SpellId.FireflashI]); // the class's first spell
        Assert.Equal(57, c.SpellsKnown.Length);
        Assert.False(c.P3.DivineAid);
        Assert.Equal(0, c.P3.QuestBits);
        Assert.Equal(P3PartyRank.Front, c.P3.Rank);
        Assert.False(c.InParty);
        Assert.Equal(7, wizard.Level); // the source is never changed
    }

    [OriginalDataFact]
    public void Dos_LevelOne_UndeadTwenty_NoDivine_NoWand_AndIOnly()
    {
        var s = NewIii(Dos);
        var priest = IMember("PRIESTO", CharacterClass.Priest, level: 3, score: 150);
        priest.SpellsKnown[SpellId.DivineI] = 1;
        var undead = IMember("BONES", race: Race.Undead, level: 2);
        s.TransferIntoPhantasieIII([priest, undead], GameId.P1);

        var p = s.Roster.Occupied.Single(m => m.Name == "PRIESTO");
        Assert.Equal((1, 300L), (p.Level, p.Experience));
        Assert.False(p.P3.DivineAid);
        Assert.DoesNotContain(180, p.InventoryItemIds);
        Assert.Equal(0, p.P3.QuestBits);
        Assert.Equal(150, p.P3.Phantasie1Score); // the I score at +0x5c
        Assert.Equal(1, p.SpellsKnown[SpellId.HealingI]);
        var u = s.Roster.Occupied.Single(m => m.Name == "BONES");
        Assert.Equal((Race.Undead, 20, 200L), (u.Race, u.Level, u.Experience));

        Assert.Throws<InvalidOperationException>(() => s.TransferIntoPhantasieIII([IMember("TWO")], GameId.P2));
    }

    [OriginalDataFact]
    public void Manual_WandForEveryone_WhenAnyoneInTheSourceRosterHeldIt_DivineIAndIIBecomeDivineAid()
    {
        var s = NewIii();
        var hero = IMember("HERO", level: 12, score: 100); // I score above 99: II's "WAND OF NIK" (D2-001)
        var priest = IMember("CLERIC", CharacterClass.Priest);
        priest.SpellsKnown[SpellId.DivineI] = 1;
        var other = IMember("OTHER");
        // The wand holder stays behind (not kept); Apple III counts the whole roster.
        s.TransferIntoPhantasieIII([hero, priest, other], GameId.P1, keep: [1, 2]);

        Assert.Equal(["CLERIC", "OTHER"], s.Roster.Occupied.Select(m => m.Name).Order());
        foreach (var c in s.Roster.Occupied)
        {
            Assert.Equal([4, 22, 41, 64, 180], c.InventoryItemIds);
            Assert.Equal(1 << 2, c.P3.QuestBits); // DOS III's "has the wand"
        }
        Assert.True(s.Roster.Occupied.Single(m => m.Name == "CLERIC").P3.DivineAid);
        Assert.False(s.Roster.Occupied.Single(m => m.Name == "OTHER").P3.DivineAid);

        var none = NewIii();
        none.TransferIntoPhantasieIII([IMember("PLAIN", score: 99)], GameId.P1);
        Assert.DoesNotContain(180, none.Roster.Occupied.Single().InventoryItemIds);

        var ii = NewIii();
        var fromIi = IMember("SAGE", CharacterClass.Priest);
        fromIi.SpellsKnown = new int[56];
        fromIi.SpellsKnown[SpellId.DivineIi] = 1; // II's DIVINE II
        ii.TransferIntoPhantasieIII([fromIi], GameId.P2);
        Assert.True(ii.Roster.Occupied.Single().P3.DivineAid);
    }

    [OriginalDataFact]
    public void FromII_BringsIIsQuestScore_AndFerronrah_UnlessIIsRecordSaysGelnor()
    {
        var s = NewIii();
        var native = IMember("NATIVE");
        native.Phantasie2 = new() { QuestScore = 200, Origin = Phantasie2Origin.Ferronrah };
        var veteran = IMember("VETERAN", score: 40);
        veteran.Phantasie2 = new() { QuestScore = 120, Origin = Phantasie2Origin.Gelnor };
        s.TransferIntoPhantasieIII([native, veteran], GameId.P2);

        var n = s.Roster.Occupied.Single(m => m.Name == "NATIVE");
        Assert.Equal((Phantasie3Origin.Ferronrah, 200), (n.P3.Origin, n.P3.Phantasie2Score));
        var v = s.Roster.Occupied.Single(m => m.Name == "VETERAN");
        Assert.Equal((Phantasie3Origin.Gelnor, 120, 40), (v.P3.Origin, v.P3.Phantasie2Score, v.P3.Phantasie1Score));
    }

    [OriginalDataFact]
    public void AddsToTheRoster_ATakenNameOrAFullGuildStaysBehind()
    {
        var s = NewIii(Dos); // D3-029 DOS: 17 characters
        s.OpenGuild();
        for (var i = 0; i < 16; i++)
            Assert.True(s.TryEnrollNewMember(s.RollNewMember(Race.Human, CharacterClass.Fighter), $"OLD{i}", out var err), err);

        var result = s.TransferIntoPhantasieIII([IMember("OLD3"), IMember("NEWA"), IMember("NEWB")], GameId.P1);
        Assert.Equal(1, result.Transferred);
        Assert.Equal(["OLD3", "NEWB"], result.LeftBehind);
        Assert.Equal(17, s.Roster.Occupied.Count());
        Assert.Contains(s.Roster.Occupied, m => m.Name == "OLD0"); // DOS adds; nothing is erased (D3-003)
    }

    [OriginalDataFact]
    public void EraseOption_ErasesEveryIIICharacterAndTheParty_InTownOnly()
    {
        var s = Phantasie3CampaignTests.WithParty(options: GameOptions.ForPreset(GameId.P3).With("p3.transfer-roster", "erase"));
        Assert.NotEmpty(s.Party.Members);
        s.TransferIntoPhantasieIII([IMember("SOLO")], GameId.P1);
        Assert.Equal(["SOLO"], s.Roster.Occupied.Select(m => m.Name));
        Assert.Empty(s.Party.Members);
    }

    [OriginalDataFact]
    public void DrawOrder_IsCreationsInTransferMode_HpSocialAge_NoAttributes()
    {
        var rng = new RecordingRandom(5);
        var s = NewIii(Dos, rng);
        rng.Recording = true;
        s.TransferIntoPhantasieIII([IMember("ORDER")], GameId.P1);
        var hpDice = rng.Ranges.TakeWhile(r => r == 2).Count();
        Assert.True(hpDice > 0);
        Assert.Equal([100, 7], rng.Ranges.Skip(hpDice)); // social class rnd(100) (a Human), the age roll
    }

    [OriginalDataFact]
    public void LevelZero_ReadsLevelOnesTrainingCells()
    {
        var tables = Phantasie3CampaignTests.NewGame().Campaign is Phantasie3Campaign p3
            ? p3.World.Training
            : throw new InvalidOperationException();
        var stats = new CharacterStats(15, 12, 13, 11, 10, 9);
        P3Trainee At(int level) => new(CharacterClass.Fighter, stats, level, 0, 0, 3, 10, P3Status.Okay, false);
        Assert.Equal(P3Training.ExperienceNeeded(At(1), tables), P3Training.ExperienceNeeded(At(0), tables));
        Assert.Equal(P3Training.Cost(1, 10, 150, tables), P3Training.Cost(0, 10, 150, tables));
        Assert.True(P3Training.MayTrain(At(0)));
    }

    [OriginalDataFact]
    public void OnlyPhantasieIIITakesTheTransfer()
    {
        var i = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P1)),
            new SeededDosRandomSource(1));
        i.StartNewGame();
        Assert.Throws<InvalidOperationException>(() => i.TransferIntoPhantasieIII([IMember("A")], GameId.P1));
    }

    private sealed class RecordingRandom(int seed) : IDosRandomSource
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
}
