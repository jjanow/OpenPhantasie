using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T3: the Phantasie I → II character transfer (<c>atari-st/phantasie2/logic-specs/transfer.md</c>), pinned from
/// the ST converter's byte table (<c>FORMAT.md</c> §6), and the <c>p2.*</c> rules options (D2-002, D2-003, D2-004).
/// </summary>
public class TransferIToIITests
{
    private static Character Veteran(CharacterClass cls = CharacterClass.Fighter) => new()
    {
        Name = "GLADE",
        Race = Race.Elf,
        BirthRace = Race.Elf,
        Class = cls,
        Status = CharacterStatus.Ok,
        Level = 12,
        Stats = new CharacterStats(17, 15, 16, 14, 12, 11),
        Skills = new CharacterSkills(150, 90, 40, 30, 20, 10, 5, 60),
        HpMax = 120,
        HpCur = 97,
        MagicMax = 40,
        MagicCur = 12,
        MpCeiling = 44,
        GoldInBank = 20_000,
        Experience = 300_000,
        WornShield = 15,
        WornArmor = 36,
        WornWeapon = 77,
        WeaponDamageA = 9,
        WeaponDamageB = 23,
        ShieldSoak = 15,
        ArmorSoak = 16,
        CurrentTown = 9,
        AgeClock = 2_000,
        Lifespan = 75_000,
        RosterOrdinal = 3,
        InParty = true,
        CastSinceRest = true,
        SpellsKnown = Enumerable.Repeat(1, CharacterCreation.SpellCount).ToArray(),
        Phantasie1 = new Phantasie1CharacterData { Runes = [1, 1, 0, 1, 0], Word90 = 15, DosRecord = new byte[282] },
    };

    private static readonly TransferIToIIRules St = TransferIToIIRules.St;

    [Fact]
    public void Convert_ChangesOnlyTheTablesFields_AndNeverTheInput()
    {
        var i = Veteran();
        i.InventoryItemIds.AddRange([15, 36, 77, 101, 121, 150]);
        var before = i.Clone();

        var ii = TransferIToII.Convert(i, St);

        Assert.NotSame(i, ii);
        Assert.Equal(before.Experience, i.Experience); // input untouched
        Assert.Equal(before.InventoryItemIds, i.InventoryItemIds);
        Assert.NotNull(i.Phantasie1.DosRecord);

        // Bytes 17/19: MP low byte := 5; @14 MP ceiling kept.
        Assert.Equal((5, 5, 44), (ii.MagicMax, ii.MagicCur, ii.MpCeiling));
        // Bytes 21/23: HP low byte cleared → 0 below 256.
        Assert.Equal((0, 0), (ii.HpMax, ii.HpCur));
        // Byte 89: level 0.
        Assert.Equal(0, ii.Level);
        // 83/85/87 worn, 223/225/227 carried, 229–239 emptied, 241/243/245 points: the starter kit.
        Assert.Equal((4, 23, 49), (ii.WornShield, ii.WornArmor, ii.WornWeapon));
        Assert.Equal([4, 49, 23], ii.InventoryItemIds);
        Assert.Equal((4, 4, 3), (ii.WeaponDamageB, ii.ShieldSoak, ii.ArmorSoak));
        // Byte 255 := 1 (0-based) → Core town 2.
        Assert.Equal(1, ii.CurrentTown); // Pippacott (II's @254 is 1-based)
        // Bank: hi = 20000 >> 8 = 78 → 78/35 = 2 → 2·256 + 100.
        Assert.Equal(612, ii.GoldInBank);
        // XP 300,000: b = 4 → g = 3 → 768.
        Assert.Equal(768, ii.Experience);

        // Copied unchanged.
        Assert.Equal(("GLADE", Race.Elf, CharacterClass.Fighter, CharacterStatus.Ok), (ii.Name, ii.Race, ii.Class, ii.Status));
        Assert.Equal(i.Stats, ii.Stats);
        Assert.Equal(i.Skills, ii.Skills);
        Assert.Equal((9, 2_000, 75_000, 3), (ii.WeaponDamageA, ii.AgeClock, ii.Lifespan, ii.RosterOrdinal));
        Assert.True(ii.InParty);
        Assert.True(ii.CastSinceRest);
        Assert.Equal([1, 1, 0, 1, 0], ii.Phantasie1.Runes);
        Assert.Equal(15, ii.Phantasie1.Word90);
        Assert.Null(ii.Phantasie1.DosRecord); // I's DOS file carry is not a II record
    }

    [Theory]
    [InlineData(20, 20)] // an undead (level 20) stays 20
    [InlineData(15, 0)]
    [InlineData(1, 0)]
    public void Level_IsZeroUnlessTwenty(int level, int expected)
    {
        var i = Veteran();
        i.Level = level;
        Assert.Equal(expected, TransferIToII.Convert(i, St).Level);
    }

    [Theory]
    [InlineData(255, 0)]
    [InlineData(256, 256)] // only the low byte is cleared
    [InlineData(300, 256)]
    public void Hp_LowByteCleared(int hp, int expected)
    {
        var i = Veteran();
        i.HpMax = i.HpCur = hp;
        var ii = TransferIToII.Convert(i, St);
        Assert.Equal((expected, expected), (ii.HpMax, ii.HpCur));
    }

    [Fact]
    public void DeadStaysDead()
    {
        var i = Veteran();
        i.Status = CharacterStatus.Dead;
        Assert.Equal(CharacterStatus.Dead, TransferIToII.Convert(i, St).Status);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(99, 100)]        // gains gold (D2-003)
    [InlineData(8_959, 100)]     // hi 34
    [InlineData(8_960, 356)]     // hi 35
    [InlineData(17_919, 356)]    // hi 69
    [InlineData(17_920, 612)]    // hi 70
    [InlineData(65_535, 1_892)]  // hi 255 → 7·256 + 100: the most anyone keeps
    [InlineData(1_000_000, 1_892)] // a lifted-cap balance reads as the u16 maximum
    public void BankGold_StFormula_D2003(int bank, int expected) =>
        Assert.Equal(expected, TransferIToII.BankGold(bank, TransferGoldRule.St));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 50)]       // the formula's 100 would be a gain
    [InlineData(150, 100)]
    [InlineData(65_535, 1_892)]
    public void BankGold_NoGain_IsTheLowerOfOldAndFormula_D2003(int bank, int expected) =>
        Assert.Equal(expected, TransferIToII.BankGold(bank, TransferGoldRule.NoGain));

    [Theory]
    [InlineData(0L, 0)]
    [InlineData(5_119L, 0)]
    [InlineData(5_120L, 1)]
    [InlineData(65_535L, 1)]
    [InlineData(65_536L, 2)]        // b = 1
    [InlineData(131_071L, 2)]
    [InlineData(131_072L, 3)]       // b = 2
    [InlineData(393_215L, 3)]       // b = 5
    [InlineData(393_216L, 4)]       // b = 6
    [InlineData(851_967L, 4)]       // b = 12
    [InlineData(851_968L, 5)]       // b = 13
    [InlineData(1_638_399L, 5)]     // b = 24
    [InlineData(1_638_400L, 6)]     // b = 25
    [InlineData(2_686_975L, 6)]     // b = 40
    [InlineData(2_686_976L, 7)]     // b = 41
    [InlineData(4_653_055L, 7)]     // b = 70
    [InlineData(4_653_056L, 8)]     // b = 71
    [InlineData(6_619_135L, 8)]     // b = 100
    [InlineData(6_619_136L, 9)]     // b = 101
    [InlineData(9_895_935L, 9)]     // b = 150
    [InlineData(9_895_936L, 10)]    // b = 151
    [InlineData(14_483_455L, 10)]   // b = 220
    [InlineData(14_483_456L, 11)]   // b = 221
    [InlineData(5_000_000_000L, 11)] // read as I's s32 word (2^31 − 1): b 255
    public void Experience_TierTable(long xp, int g)
    {
        Assert.Equal(g, TransferIToII.XpTier(xp));
        Assert.Equal(256L * g, TransferIToII.Experience(xp));
    }

    [Theory]
    [InlineData(CharacterClass.Fighter, 5, 9, 3, 9, 9, 507)]   // live: 200·33/(2·5 + 3)
    [InlineData(CharacterClass.Priest, 3, 4, 9, 9, 9, 659)]    // live: 200·36.3/(3 + 2·4)
    [InlineData(CharacterClass.Thief, 5, 9, 5, 9, 9, 263)]     // live: 263.9965 truncates
    [InlineData(CharacterClass.Wizard, 9, 4, 9, 9, 3, 779)]
    [InlineData(CharacterClass.Monk, 4, 4, 4, 9, 9, 824)]
    [InlineData(CharacterClass.Ranger, 3, 3, 9, 3, 9, 1173)]
    public void LevelZeroMinimum_IsIsTrainingFormula_WithIIsSums(
        CharacterClass cls, int str, int intel, int dex, int cha, int luck, long expected)
    {
        var c = new Character { Class = cls, Stats = new CharacterStats(str, intel, dex, 9, cha, luck) };
        Assert.Equal(expected, TransferIToII.LevelZeroMinimum(c));
    }

    [Fact]
    public void XpIsRaisedToTheMinimum_OnlyForOccupiedRecords()
    {
        var i = Veteran(CharacterClass.Fighter);
        i.Stats = new CharacterStats(5, 9, 3, 9, 9, 9);
        i.Experience = 100;
        Assert.Equal(507, TransferIToII.Convert(i, St).Experience);
        i.RosterOrdinal = 0;
        Assert.Equal(0, TransferIToII.Convert(i, St).Experience);
    }

    [Fact]
    public void Experience_WrapsAt2Pow24_ByteTwoSixSevenOnly()
    {
        Assert.Equal(2, TransferIToII.XpTier(16_847_216)); // 0x01011170, live → 512
        Assert.Equal(0, TransferIToII.XpTier(16_777_216)); // 2^24 → b 0, byte 268 0
        Assert.Equal(11, TransferIToII.XpTier(300_000_000)); // b 225
    }

    // ---- live Hatari vectors (s76, parity/transfer-live.md): I rosters in, UTILIT2's II rosters out -------------------

    private const int IRecord = 282, IIRecord = 318;

    private static int W(byte[] f, int at) => f[at] << 8 | f[at + 1];

    /// <summary>The fields the converter reads, from one big-endian I record (FORMAT.md §5).</summary>
    private static Character ReadI(byte[] f, int slot)
    {
        var o = slot * IRecord;
        var c = new Character
        {
            Name = System.Text.Encoding.ASCII.GetString(f, o + 270, 12).TrimEnd('\0', ' '),
            Class = (CharacterClass)f[o + 221],
            Stats = new CharacterStats(W(f, o), W(f, o + 2), W(f, o + 4), W(f, o + 6), W(f, o + 8), W(f, o + 10)),
            MpCeiling = W(f, o + 14), MagicMax = W(f, o + 16), MagicCur = W(f, o + 18),
            HpMax = W(f, o + 20), HpCur = W(f, o + 22),
            Level = W(f, o + 88), RosterOrdinal = W(f, o + 92), AgeClock = W(f, o + 264),
            WornShield = W(f, o + 82), WornArmor = W(f, o + 84), WornWeapon = W(f, o + 86),
            WeaponDamageB = W(f, o + 240), ShieldSoak = W(f, o + 242), ArmorSoak = W(f, o + 244),
            GoldInBank = W(f, o + 262),
            Experience = (int)(uint)(W(f, o + 266) << 16 | W(f, o + 268)),
            SpellsKnown = Enumerable.Range(0, CharacterCreation.SpellCount)
                .Select(k => W(f, o + 96 + 2 * SpellId.DosSlot(k))).ToArray(),
        };
        for (var k = 0; k < 9; k++)
            if (W(f, o + 222 + 2 * k) is var id and not 0)
                c.InventoryItemIds.Add(id);
        return c;
    }

    [OriginalDataTheory]
    [InlineData("setA")]
    [InlineData("setB")]
    [InlineData("setD")]
    public void LiveVectors_MatchTheConverter(string set)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-transfer");
        var input = File.ReadAllBytes(Path.Combine(dir, $"{set}-p1-guild-in.dat"));
        var live = File.ReadAllBytes(Path.Combine(dir, $"{set}-p2-guild-live.dat"));
        Assert.Equal(25 * IRecord, input.Length);
        Assert.Equal(25 * IIRecord, live.Length);

        var roster = TransferIToII.ConvertRoster(
            Enumerable.Range(0, 25).Select(k => (Character?)ReadI(input, k)).ToArray(), St);
        Assert.Equal(22, roster.Length);
        for (var slot = 0; slot < 22; slot++)
        {
            var c = roster[slot]!;
            var o = slot * IIRecord;
            var at = $"{set} slot {slot} {c.Name}";
            Assert.True((W(live, o + 16), W(live, o + 18)) == (c.MagicMax, c.MagicCur), at + " MP");
            Assert.True((W(live, o + 20), W(live, o + 22)) == (c.HpMax, c.HpCur), at + " HP");
            Assert.True(W(live, o + 14) == c.MpCeiling, at + " MP ceiling");
            Assert.True(W(live, o + 88) == c.Level, at + " level");
            Assert.True(W(live, o + 262) == c.GoldInBank, at + " bank");
            Assert.True((W(live, o + 266) << 16 | W(live, o + 268)) == c.Experience, at + " XP");
            Assert.True(live[o + 255] == c.CurrentTown, at + " town"); // II's @254 is 1-based (FORMAT §9)
            // Item and points words keep their (noise-record) high bytes; the converter writes the low byte's value.
            Assert.True((live[o + 83], live[o + 85], live[o + 87]) == (c.WornShield, c.WornArmor, c.WornWeapon), at + " worn");
            Assert.True((live[o + 241], live[o + 243], live[o + 245])
                == (c.WeaponDamageB & 0xFF, c.ShieldSoak & 0xFF, c.ArmorSoak & 0xFF), at + " points");
            var items = Enumerable.Range(0, 9).Select(k => (int)live[o + 223 + 2 * k]).Where(id => id != 0).ToArray();
            Assert.True(items.SequenceEqual(c.InventoryItemIds), at + " items");
            for (var k = 0; k < CharacterCreation.SpellCount; k++)
            {
                var slotNo = SpellId.DosSlot(k);
                var word = W(live, o + 96 + 2 * slotNo);
                // Slots 54+ are copied whole; below, compare the low byte (slot 0's high byte 96 is copied too).
                var ok = slotNo >= 54 ? word == c.SpellsKnown[k] : (word & 0xFF) == (c.SpellsKnown[k] & 0xFF);
                Assert.True(ok, $"{at} spell {k}: live {word}, converter {c.SpellsKnown[k]}");
            }
        }
    }

    /// <summary>
    /// 3b.9 (<c>parity/roster-live.md</c>): the real Phantasie I ST roster (set D) converted live by UTILIT2 and opened in
    /// II's <c>PHANT.PRG</c>. Each row is what the Guild Inspect sheet and its SPELLS page showed; the converter must
    /// predict every number on it.
    /// </summary>
    [OriginalDataTheory]
    // slot, name, level, HP max, HP cur, MP max, MP cur, bank, XP, age, spells page
    [InlineData(0, "ZENNO", 0, 0, 0, 5, 5, 100, 1024, 24, "")]
    [InlineData(1, "THOLIE", 0, 0, 0, 5, 5, 100, 1024, 23, "HEALING I")]
    [InlineData(2, "ADAM", 0, 0, 0, 5, 5, 1892, 1024, 49, "FIREFLASH I")]
    [InlineData(3, "FINGON", 0, 0, 0, 5, 5, 100, 1024, 226, "FIREFLASH I")]
    [InlineData(4, "BILBO", 0, 0, 0, 5, 5, 100, 1024, 42, "")]
    [InlineData(5, "ORTODOX", 0, 0, 0, 5, 5, 1380, 1024, 25, "HEALING I")]
    [InlineData(7, "BUTCHIE", 0, 0, 0, 5, 5, 100, 256, 17, "")]
    [InlineData(9, "ZALF", 0, 0, 0, 5, 5, 100, 256, 15, "HEALING I")]
    [InlineData(11, "FUCKER", 0, 0, 0, 5, 5, 100, 140, 15, "")]
    [InlineData(12, "GIMLI", 0, 0, 0, 5, 5, 100, 512, 33, "")]
    public void RealRoster_SheetsInPhantII_MatchTheConverter(
        int slot, string name, int level, int hpMax, int hpCur, int mpMax, int mpCur, int bank, long xp, int age,
        string spells)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-transfer");
        var input = File.ReadAllBytes(Path.Combine(dir, "setD-p1-guild-in.dat"));
        var roster = TransferIToII.ConvertRoster(
            Enumerable.Range(0, 25).Select(k => (Character?)ReadI(input, k)).ToArray(), St);
        var c = roster[slot]!;

        Assert.Equal(name, c.Name);
        Assert.Equal((level, hpMax, hpCur, mpMax, mpCur), (c.Level, c.HpMax, c.HpCur, c.MagicMax, c.MagicCur));
        Assert.Equal((bank, xp, age), (c.GoldInBank, c.Experience, c.AgeYears));
        Assert.Equal([4, 49, 23], c.InventoryItemIds); // EQUIPMENT page: MACE 4, LEATHER 3, SM SHIELD 4
        var known = Enumerable.Range(0, CharacterCreation.SpellCount).Where(k => c.SpellsKnown[k] == 1).ToList();
        var expected = spells switch
        {
            "HEALING I" => new List<int> { SpellId.HealingI },
            "FIREFLASH I" => [SpellId.FireflashI],
            _ => [],
        };
        Assert.Equal(expected, known);
    }

    [OriginalDataFact]
    public void RealRoster_EmptySlotsStayOffTheGuildList()
    {
        // GLOIN, GANDALF, PEREGRIN and GIMLIE have names but @92 = 0: PHANT.PRG does not list them (live).
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-transfer");
        var input = File.ReadAllBytes(Path.Combine(dir, "setD-p1-guild-in.dat"));
        var roster = TransferIToII.ConvertRoster(
            Enumerable.Range(0, 25).Select(k => (Character?)ReadI(input, k)).ToArray(), St);
        Assert.Equal([0, 1, 2, 3, 4, 5, 7, 9, 11, 12],
            Enumerable.Range(0, roster.Length).Where(k => roster[k] is { RosterOrdinal: > 0 }).ToArray());
    }

    // ---- spells (D2-002, D2-004) ----------------------------------------------------------------------------------

    [Theory]
    // class, manual rule: HEALING I, FIREFLASH I; ST rule: HEALING I, FIREFLASH I
    [InlineData(CharacterClass.Priest, 1, 0, 1, 0)]
    [InlineData(CharacterClass.Wizard, 0, 1, 0, 1)]
    [InlineData(CharacterClass.Ranger, 0, 0, 1, 0)]
    [InlineData(CharacterClass.Monk, 0, 0, 0, 1)]
    [InlineData(CharacterClass.Fighter, 0, 0, 0, 0)]
    [InlineData(CharacterClass.Thief, 0, 0, 0, 0)]
    public void StartingSpells_ByClass_D2002(CharacterClass cls, int mHeal, int mFire, int stHeal, int stFire)
    {
        var i = Veteran(cls);
        i.SpellsKnown = new int[CharacterCreation.SpellCount]; // knew nothing: the flag is set anyway (the ST)
        var manual = TransferIToII.Spells(i, St with { Spells = TransferSpellRule.Manual });
        var st = TransferIToII.Spells(i, St);
        Assert.Equal((mHeal, mFire), (manual[SpellId.HealingI], manual[SpellId.FireflashI]));
        Assert.Equal((stHeal, stFire), (st[SpellId.HealingI], st[SpellId.FireflashI]));
        Assert.Equal(mHeal + mFire, manual.Count(f => f != 0)); // nothing else is set
        Assert.Equal(stHeal + stFire, st.Count(f => f != 0));
    }

    [Fact]
    public void Spells_OneToFiftyFourCleared_DivineIKept_D2004()
    {
        var i = Veteran(CharacterClass.Fighter);
        foreach (var rule in new[] { TransferDivineRule.FromP27, TransferDivineRule.FromP14 })
        {
            var spells = TransferIToII.Spells(i, St with { Divine = rule });
            Assert.All(Enumerable.Range(0, SpellId.DivineI), k => Assert.Equal(0, spells[k]));
            Assert.Equal(1, spells[SpellId.DivineI]); // DIVINE I = slot 57 = #58: kept by both readings
        }
    }

    [Theory]
    [InlineData(TransferDivineRule.FromP27, 54)] // #55 and above (p.27, the ST)
    [InlineData(TransferDivineRule.FromP14, 57)] // #58+ (p.14): II's DIVINE II (slot 56, #57) would be lost
    public void FirstKeptSlot_D2004(TransferDivineRule rule, int slot) =>
        Assert.Equal(slot, TransferIToII.FirstKeptSlot(rule));

    [Fact]
    public void Wand_IsTheScoreFlag_KeptThroughTheTransfer_D2001()
    {
        Assert.Equal([4, 49, 23], TransferIToII.StarterItems);
        var i = Veteran();
        i.InventoryItemIds.Add(150); // I's wand item is dropped like every item …
        i.Score = 100;
        var ii = TransferIToII.Convert(i, St);
        Assert.Equal(100, ii.Score);   // … but the score is copied (@70)
        Assert.True(TransferIToII.HasWandOfNikademus(ii)); // > 99: II's "WAND OF NIK"
        i.Score = 99;
        Assert.False(TransferIToII.HasWandOfNikademus(TransferIToII.Convert(i, St)));
    }

    [Fact]
    public void ConvertRoster_TwentyTwoSlots_EmptyStaysEmpty_ElementalsLeftOut()
    {
        var slots = new Character?[25];
        slots[0] = Veteran();
        slots[5] = Veteran(CharacterClass.Wizard);
        slots[22] = new Character { Name = "FIRE_ELEM", Level = 20 };
        var ii = TransferIToII.ConvertRoster(slots, St);
        Assert.Equal(22, ii.Length);
        Assert.Equal(2, ii.Count(c => c is not null));
        Assert.Equal(0, ii[0]!.Level);
        Assert.Equal(1, ii[5]!.SpellsKnown[SpellId.FireflashI]);
    }

    [Fact]
    public void ConvertRoster_GuildLimitNone_ConvertsEverySlot()
    {
        var slots = new Character?[30];
        slots[0] = Veteran();
        slots[21] = Veteran(CharacterClass.Wizard);
        slots[22] = Veteran(CharacterClass.Priest);
        slots[29] = Veteran(CharacterClass.Wizard);
        var ii = TransferIToII.ConvertRoster(slots, St, guildUnlimited: true);
        Assert.Equal(30, ii.Length);
        Assert.Equal(4, ii.Count(c => c is not null));
        Assert.Equal(1, ii[22]!.SpellsKnown[SpellId.HealingI]);
        Assert.Equal(1, ii[29]!.SpellsKnown[SpellId.FireflashI]);
        Assert.Equal(0, ii[29]!.Level);
        // a short roster still gives II's 22 slots
        Assert.Equal(22, TransferIToII.ConvertRoster(new Character?[5], St, guildUnlimited: true).Length);
    }

    // ---- options ----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("p2.transfer-spells", "manual", "st", "manual", "D2-002")]
    [InlineData("p2.transfer-divine", "p27", "p27", "p27", "D2-004")]
    [InlineData("p2.transfer-gold", "st", "st", "st", "D2-003")]
    public void Descriptor_PresetValues(string id, string manual, string original, string remaster, string difference)
    {
        var d = RuleOptionRegistry.For(GameId.P2).Single(o => o.Id == id);
        Assert.Equal((manual, original, remaster, difference), (d.Manual, d.Dos, d.Remaster, d.Difference));
        Assert.Equal(GameId.P2, d.Game);
        Assert.DoesNotContain(d, RuleOptionRegistry.For(GameId.P1));
    }

    [Fact]
    public void Presets_GiveTheTransferRules()
    {
        Assert.Equal(new TransferIToIIRules(TransferSpellRule.Manual, TransferDivineRule.FromP27, TransferGoldRule.St),
            GameOptions.ForPreset(GameId.P2).TransferIToII);
        Assert.Equal(TransferIToIIRules.St, GameOptions.ForPreset(GameId.P2, OptionPreset.Dos).TransferIToII);
        Assert.Equal(TransferSpellRule.Manual, GameOptions.ForPreset(GameId.P2, OptionPreset.Remaster).TransferIToII.Spells);

        var custom = GameOptions.ForPreset(GameId.P2)
            .With("p2.transfer-divine", "p14").With("p2.transfer-gold", "no-gain");
        Assert.Equal(OptionPreset.Custom, custom.Preset);
        Assert.Equal(new TransferIToIIRules(TransferSpellRule.Manual, TransferDivineRule.FromP14, TransferGoldRule.NoGain),
            custom.TransferIToII);

        // Phantasie I has none of these options: its rules read the first value of each.
        Assert.Equal(TransferSpellRule.Manual, GameOptions.ForPreset(GameId.P1, OptionPreset.Dos).TransferIToII.Spells);
    }

    [Fact]
    public void Options_DriveTheConverter()
    {
        var ranger = Veteran(CharacterClass.Ranger);
        ranger.SpellsKnown = new int[CharacterCreation.SpellCount];
        ranger.GoldInBank = 50;
        var manual = TransferIToII.Convert(ranger, GameOptions.ForPreset(GameId.P2).TransferIToII);
        var st = TransferIToII.Convert(ranger, GameOptions.ForPreset(GameId.P2, OptionPreset.Dos).TransferIToII);
        Assert.Equal(0, manual.SpellsKnown[SpellId.HealingI]);
        Assert.Equal(1, st.SpellsKnown[SpellId.HealingI]);
        Assert.Equal(100, st.GoldInBank);
        var noGain = GameOptions.ForPreset(GameId.P2).With("p2.transfer-gold", "no-gain").TransferIToII;
        Assert.Equal(50, TransferIToII.Convert(ranger, noGain).GoldInBank);
    }

    [Fact]
    public void PresetLabel_IsPerGame_WithFallback()
    {
        var table = new HashSet<string> { "options.preset.dos", "options.preset.dos.p2", "options.preset.manual" };
        Assert.Equal("options.preset.dos.p2", OptionPreset.Dos.LabelId(GameId.P2, table.Contains));
        Assert.Equal("options.preset.dos", OptionPreset.Dos.LabelId(GameId.P1, table.Contains));
        Assert.Equal("options.preset.manual", OptionPreset.Manual.LabelId(GameId.P2, table.Contains));
    }
}
