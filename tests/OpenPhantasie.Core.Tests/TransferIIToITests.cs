using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T5: the Phantasie II → I transfer (ST <c>UTILIT2.PRG</c> mode 1, <c>atari-st/phantasie2/logic-specs/
/// transfer.md</c> section 8), pinned to the live Hatari run C (s76, <c>parity/transfer-live.md</c>), and its wiring into a new
/// Phantasie I game (<see cref="GameSession.TransferFromPhantasieII"/>).
/// </summary>
public class TransferIIToITests
{
    private static readonly GameDataRepository P1Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P1));

    private const int IRecord = 282, IIRecord = 318;

    private static int W(byte[] f, int at) => f[at] << 8 | f[at + 1];

    /// <summary>The fields the converter reads, from one big-endian II record (FORMAT.md section 5: bytes 0–281 as I's).</summary>
    private static Character ReadII(byte[] f, int slot)
    {
        var o = slot * IIRecord;
        var c = new Character
        {
            Name = System.Text.Encoding.ASCII.GetString(f, o + 270, 12).TrimEnd('\0', ' '),
            Class = (CharacterClass)f[o + 221],
            Stats = new CharacterStats(W(f, o), W(f, o + 2), W(f, o + 4), W(f, o + 6), W(f, o + 8), W(f, o + 10)),
            MpCeiling = W(f, o + 14), MagicMax = W(f, o + 16), MagicCur = W(f, o + 18),
            HpMax = W(f, o + 20), HpCur = W(f, o + 22), TossRock = W(f, o + 42),
            Level = W(f, o + 88), RosterOrdinal = W(f, o + 92), AgeClock = W(f, o + 264),
            WornShield = W(f, o + 82), WornArmor = W(f, o + 84), WornWeapon = W(f, o + 86),
            WeaponDamageB = W(f, o + 240), ShieldSoak = W(f, o + 242), ArmorSoak = W(f, o + 244),
            GoldInBank = W(f, o + 262),
            Experience = (int)(uint)(W(f, o + 266) << 16 | W(f, o + 268)),
            SpellsKnown = Enumerable.Range(0, CharacterCreation.SpellCount)
                .Select(k => W(f, o + 96 + 2 * SpellId.DosSlot(k))).ToArray(),
            Phantasie2 = new() { QuestScore = W(f, o + 282) }, // II-only bytes 282+: dropped by mode 1
        };
        for (var k = 0; k < 9; k++)
            if (W(f, o + 222 + 2 * k) is var id and not 0)
                c.InventoryItemIds.Add(id);
        return c;
    }

    [OriginalDataFact]
    public void LiveRunC_MatchesTheConverter()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-transfer");
        var input = File.ReadAllBytes(Path.Combine(dir, "setC-p2-guild-in.dat"));
        var live = File.ReadAllBytes(Path.Combine(dir, "setC-p1-guild-live-from-played-p2.dat"));
        Assert.Equal(25 * IIRecord, input.Length);
        Assert.Equal(25 * IRecord, live.Length);

        var roster = TransferIIToI.ConvertRoster(Enumerable.Range(0, 25).Select(k => (Character?)ReadII(input, k)).ToArray());
        Assert.Equal(22, roster.Length);
        for (var slot = 0; slot < 22; slot++)
        {
            var c = roster[slot]!;
            var o = slot * IRecord;
            var at = $"slot {slot} {c.Name}";
            Assert.True((W(live, o + 16), W(live, o + 18)) == (c.MagicMax, c.MagicCur), at + " MP");
            Assert.True((W(live, o + 20), W(live, o + 22)) == (c.HpMax, c.HpCur), at + " HP");
            Assert.True(W(live, o + 88) == c.Level, at + " level");
            Assert.True(W(live, o + 262) == c.GoldInBank, at + " bank");
            Assert.True((W(live, o + 266) << 16 | W(live, o + 268)) == c.Experience, at + " XP"); // no raise in mode 1
            Assert.True(GuildCodec.TownFromDos(W(live, o + 254)) == c.CurrentTown, at + " town"); // I's @254 is 0-based
            Assert.True(W(live, o + 42) == c.TossRock, at + " TOSS");
            Assert.True((W(live, o + 82), W(live, o + 84), W(live, o + 86)) == (c.WornShield, c.WornArmor, c.WornWeapon), at + " worn");
            Assert.True((W(live, o + 240), W(live, o + 242), W(live, o + 244)) == (c.WeaponDamageB, c.ShieldSoak, c.ArmorSoak), at + " points");
            var items = Enumerable.Range(0, 9).Select(k => W(live, o + 222 + 2 * k)).Where(id => id != 0).ToArray();
            Assert.True(items.SequenceEqual(c.InventoryItemIds), at + " items");
            for (var k = 0; k < CharacterCreation.SpellCount; k++)
                Assert.True(W(live, o + 96 + 2 * SpellId.DosSlot(k)) == c.SpellsKnown[k], $"{at} spell {k}");
            Assert.Null(c.Phantasie2); // the 36 II-only bytes are not written
        }
        Assert.Equal(["GLADE", "YOSHI", "FOREST", "KLEPTO", "ORCISH", "DRUM", "FATHER JO"],
            roster.Where(c => c is { RosterOrdinal: > 0 }).Select(c => c!.Name));
        Assert.All(roster.Take(7), c => Assert.Equal(TransferIIToI.StartTown, c!.CurrentTown)); // PINEVILLE
    }

    [Fact]
    public void Options_DoNotApply_TheStsValuesRun()
    {
        // The manual has no II → I, so the converter always runs the ST's values (Monks get FIREFLASH I, gold by formula).
        var monk = new Character
        {
            Name = "YOSHI", Class = CharacterClass.Monk, Level = 20, Experience = 5_000_000, GoldInBank = 9_000,
            RosterOrdinal = 1, SpellsKnown = new int[CharacterCreation.SpellCount],
        };
        monk.SpellsKnown[SpellId.FromDosSlot(57)] = 1; // DIVINE I survives (slots 54+)
        var c = TransferIIToI.Convert(monk);
        Assert.Equal(1, c.SpellsKnown[SpellId.FireflashI]);
        Assert.Equal(1, c.SpellsKnown[SpellId.FromDosSlot(57)]);
        Assert.Equal(20, c.Level); // an undead stays 20
        Assert.Equal(TransferIToII.BankGold(9_000, TransferGoldRule.St), c.GoldInBank);
        Assert.Equal(256L * TransferIToII.XpTier(5_000_000), c.Experience);
        Assert.Equal(CharacterClass.Monk, monk.Class); // input unchanged
        Assert.Equal(9_000, monk.GoldInBank);
    }

    private static GameSession NewI(GameOptions? options = null)
    {
        var s = GameSessionFactory.Create(P1Data, new SeededDosRandomSource(2));
        s.StartNewGame(options);
        return s;
    }

    private static Character IIMember(string name, int ordinal = 0) => new()
    {
        Name = name, Race = Race.Dwarf, BirthRace = Race.Dwarf, Class = CharacterClass.Fighter, Status = CharacterStatus.Ok,
        Level = 6, Stats = new CharacterStats(16, 10, 14, 12, 9, 8), HpMax = 50, HpCur = 44, MagicMax = 0, MagicCur = 0,
        GoldInBank = 3_000, Experience = 40_000, RosterOrdinal = ordinal, InParty = true, TossRock = 60,
        SpellsKnown = new int[CharacterCreation.SpellCount], Phantasie2 = new() { QuestScore = 7, Origin = Phantasie2Origin.Gelnor },
    };

    [OriginalDataFact]
    public void OnlyANewPhantasieIGame_RunsTheTransfer()
    {
        var ii = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2)),
            new SeededDosRandomSource(1));
        ii.StartNewGame();
        Assert.Throws<InvalidOperationException>(() => ii.TransferFromPhantasieII([IIMember("A")]));

        var i = NewI();
        i.OpenGuild();
        var rolled = i.RollNewMember(Race.Human, CharacterClass.Fighter);
        Assert.True(i.TryEnrollNewMember(rolled, "MADE", out _));
        Assert.True(i.TryAddToParty(rolled, out _));
        Assert.Throws<InvalidOperationException>(() => i.TransferFromPhantasieII([IIMember("A")]));
    }

    [OriginalDataFact]
    public void Transfer_IsANewIGame_TheMembersWaitInPineville_AndCanBeAddedInPelnor()
    {
        var i = NewI();
        var result = i.TransferFromPhantasieII([IIMember("GLADE"), null, IIMember("KLEPTO")]);

        Assert.Equal(2, result.Transferred);
        Assert.Equal(22, i.Roster.Slots.Length);
        Assert.Equal("GLADE", i.Roster.Slots[0]!.Name);
        Assert.Null(i.Roster.Slots[1]);
        Assert.Equal("KLEPTO", i.Roster.Slots[2]!.Name);
        Assert.Equal([1, 3], i.Roster.Occupied.Select(c => c.RosterOrdinal)); // remaster-made: slot + 1
        Assert.Equal(1, i.CurrentTownId); // Pelnor
        Assert.Empty(i.Party.Members);
        Assert.True(i.FreshStart);

        var glade = i.Roster.Slots[0]!;
        Assert.False(glade.InParty);
        Assert.Equal(256, glade.Experience); // 40,000: byte 267 = 0, byte 268 = 156 ≥ 20 → tier 1; no level-0 raise
        Assert.Equal((0, 0, 5), (glade.HpMax, glade.HpCur, glade.MagicMax));
        Assert.Null(glade.Phantasie2);
        Assert.Equal(60, glade.TossRock);
        Assert.Equal(TransferIIToI.StartTown, glade.CurrentTown);

        // Pelnor's Guild: a member of another town asks first and ages on Add (I's own rule, OV1 0x2835).
        i.OpenGuild();
        Assert.True(i.AddNeedsMoveConfirm(glade));
        var age = glade.AgeClock;
        Assert.True(i.TryAddToParty(glade, out var err), err);
        Assert.Equal(age + GuildHall.AddAgeCost, glade.AgeClock);
        Assert.Equal(1, glade.CurrentTown);

        // HP 0 / max 0 with status OK (the ST's result; manual silent): I's Inn takes them without fault and has nothing
        // to refill. Training gives HP once they hold the XP for it (spec section 8).
        i.RestAtInn();
        Assert.Equal(CharacterStatus.Ok, glade.Status);
        Assert.Equal((0, 0), (glade.HpMax, glade.HpCur));
    }

    [OriginalDataFact]
    public void GuildLimit_IsIsOwn()
    {
        var big = Enumerable.Range(0, 24).Select(k => (Character?)IIMember($"M{k}", k + 1)).ToArray();

        var game = NewI(GameOptions.ForPreset(GameId.P1).With(RuleOptionRegistry.GuildLimit.Id, "game"));
        var r = game.TransferFromPhantasieII(big);
        Assert.Equal(22, r.Transferred);
        Assert.Equal(["M22", "M23"], r.LeftBehind);

        var none = NewI(GameOptions.ForPreset(GameId.P1).With(RuleOptionRegistry.GuildLimit.Id, "none"));
        Assert.Equal(24, none.TransferFromPhantasieII(big).Transferred);
        Assert.Equal("M23", none.Roster.Slots[23]!.Name);
    }

    [OriginalDataFact]
    public void TheIISaveFile_IsReadAsTheSource()
    {
        var ii = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2)),
            new SeededDosRandomSource(4));
        ii.StartNewGame();
        ii.OpenGuild();
        var rolled = ii.RollNewMember(Race.Elf, CharacterClass.Wizard);
        Assert.True(ii.TryEnrollNewMember(rolled, "MERLA", out var err), err);
        var save = SessionCodec.ReadJson(SessionCodec.WriteJson(ii.CaptureSnapshot()));

        var i = NewI();
        Assert.Equal(1, i.TransferFromPhantasieII(TransferSource.RosterOf(save, GameId.P2)).Transferred);
        Assert.Equal(1, i.Roster.Slots[0]!.SpellsKnown[SpellId.FireflashI]); // a Wizard keeps #5
        Assert.Throws<InvalidDataException>(() => TransferSource.RosterOf(i.CaptureSnapshot(), GameId.P2));
    }

    [OriginalDataFact]
    public void AnHpZeroArrival_TrainsAtLevelZero_AndFightsWithoutFault()
    {
        var i = NewI();
        var veteran = IIMember("GLADE");
        veteran.Experience = 200_000; // b = 3 → tier 3 → 768 XP: enough for I's level 0 → 1
        i.TransferFromPhantasieII([veteran, IIMember("KLEPTO")]);
        var glade = i.Roster.Slots[0]!;
        var klepto = i.Roster.Slots[1]!;
        Assert.Equal((0, 0, 768L), (glade.Level, glade.HpMax, glade.Experience));

        i.OpenGuild();
        Assert.True(i.TryAddToParty(glade, out var err), err);
        Assert.True(i.TryAddToParty(klepto, out err), err);
        glade.GoldInBank = 10_000;
        Assert.True(i.CanTrain(glade, out var why), why);
        i.Train(glade);
        Assert.Equal(1, glade.Level);
        Assert.True(glade.HpMax > 0); // training is what gives an arrival HP

        // KLEPTO (XP 256, HP 0 / 0, status OK) goes into a fight as he is.
        i.DepartGuildToWilderness();
        i.BeginWildernessCombat(P1Data.Monsters.First(m => m.HpBase <= 8).Id);
        for (var round = 0; round < 20 && i.Combat is not null; round++)
        {
            var end = i.ResolveCombatRound();
            if (end.PartyWon || end.PartyWiped)
                break;
        }
    }

    /// <summary>
    /// The ST Phantasie I live check (s146, <c>parity/transfer-live.md</c> section 8): run C's HP 0 / 0 party dies to
    /// the first hit that lands on each member ("COYOTE HITS" → "DRUM DIES"), the party is defeated and the demon
    /// destroys them. The remaster: a hit on an HP-0 member kills, so a lone arrival is wiped.
    /// </summary>
    [OriginalDataFact]
    public void AnHpZeroArrival_DiesToTheFirstHit()
    {
        var i = NewI();
        i.TransferFromPhantasieII([IIMember("KLEPTO")]);
        var klepto = i.Roster.Slots[0]!;
        Assert.Equal((0, 0, CharacterStatus.Ok), (klepto.HpMax, klepto.HpCur, klepto.Status));

        i.OpenGuild();
        Assert.True(i.TryAddToParty(klepto, out var err), err);
        i.DepartGuildToWilderness();
        i.BeginWildernessCombat(P1Data.Monsters.First(m => m.HpBase <= 8).Id);
        var wiped = false;
        for (var round = 0; round < 200 && i.Combat is not null && !wiped; round++)
            wiped = i.ResolveCombatRound().PartyWiped;

        Assert.True(wiped);
        Assert.Equal(CharacterStatus.Dead, klepto.Status);
    }
}
