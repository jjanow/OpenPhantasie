using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

public class MagicSpellTests
{
    private sealed class ScriptedRandomSource : IDosRandomSource
    {
        public int NextDosWord() => Next(0, 32768);

        private readonly Queue<int> _values;

        public ScriptedRandomSource(params int[] values) =>
            _values = new Queue<int>(values);

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_values.Count == 0)
                return minInclusive;
            var v = _values.Dequeue();
            if (_values.Count == 0)
                _values.Enqueue(v);
            return v;
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 4)]
    [InlineData(4, 1)]
    [InlineData(40, 1)]
    [InlineData(53, 2)]
    [InlineData(54, 2)] // DIVINE I = DOS slot 57 → 57 % 4 + 1 (s41)
    public void MagicPointCost_MatchesOv2Formula(int index, int expected) =>
        Assert.Equal(expected, Magic.MagicPointCost(index));

    [Theory]
    [InlineData(1, 50, 5)]   // ((6*1+4)*50)/100 = 500/100 = 5
    [InlineData(2, 50, 14)]  // ((24+4)*50)/100 = 14
    [InlineData(4, 100, 100)] // ((96+4)*100)/100 = 100 — clamp tested separately
    [InlineData(1, 0, 1)]    // OV2 floors zero heal to 1
    public void HealingAmount_MatchesFormula(int tier, int rng, int expected) =>
        Assert.Equal(expected, Magic.HealingAmount(tier, rng));

    [Fact]
    public void ApplyHealing_ClampsToHpMax()
    {
        var ch = new Character { HpCur = 8, HpMax = 10 };
        Assert.Equal(2, Magic.ApplyHealing(ch, 50));
        Assert.Equal(10, ch.HpCur);
    }

    [Theory]
    [InlineData(101, 1)]
    [InlineData(105, 25)]
    [InlineData(110, 100)]
    public void PotionHealing_IsNSquared(int itemId, int expected) =>
        Assert.Equal(expected, Magic.PotionHealingAmount(itemId));

    [Theory]
    [InlineData(111, 3)]
    [InlineData(115, 15)]
    [InlineData(120, 30)]
    public void PotionMagic_Is3N(int itemId, int expected) =>
        Assert.Equal(expected, Magic.PotionMagicAmount(itemId));

    [Fact]
    public void UsePotion_HealingConsumesSlotAndRestoresHp()
    {
        var owner = new Character { Name = "Wiz", HpCur = 5, HpMax = 20 };
        owner.InventoryItemIds.Add(103); // HEALING 3 → 9 HP
        var result = Magic.UsePotion(owner, owner, 0);
        Assert.True(result.Success);
        Assert.Equal(14, owner.HpCur);
        Assert.Empty(owner.InventoryItemIds);
        Assert.Equal(9, result.HpRestored);
    }

    [Fact]
    public void UsePotion_MagicConsumesSlotAndRestoresMp()
    {
        var owner = new Character { Name = "Wiz", MagicCur = 2, MagicMax = 20 };
        owner.InventoryItemIds.Add(112); // MAGIC 2 → 6 MP
        var result = Magic.UsePotion(owner, owner, 0);
        Assert.True(result.Success);
        Assert.Equal(8, owner.MagicCur);
        Assert.Equal(6, result.MpRestored);
    }

    [Fact]
    public void UsePotion_EmptiesTheSlotInPlace_LaterItemsKeepTheirSlots()
    {
        // [C] s58: OV2 0x446b / OV1 0x31ac `mov word [bx+0x5c8e],0` — no shift.
        var owner = new Character { Name = "Wiz", HpCur = 5, HpMax = 20 };
        owner.InventoryItemIds.AddRange(new[] { 46, 103, 22, 128 });
        Assert.True(Magic.UsePotion(owner, owner, 1).Success);
        Assert.Equal(new[] { 46, 0, 22, 128 }, owner.InventoryItemIds);
        Assert.Equal(3, CarriedItems.Count(owner));
        Assert.Equal(0, DungeonScript.GetField(owner, 153)); // slot 1 reads empty
        Assert.Equal(22, DungeonScript.GetField(owner, 154));
    }

    [Fact]
    public void UsePotion_LastSlot_DropsTrailingEmptySlots()
    {
        var owner = new Character { Name = "Wiz", MagicCur = 0, MagicMax = 20 };
        owner.InventoryItemIds.AddRange(new[] { 46, 0, 111 });
        Assert.True(Magic.UsePotion(owner, owner, 2).Success);
        Assert.Equal(new[] { 46 }, owner.InventoryItemIds);
    }

    [Theory]
    [InlineData(4, 105)]   // FIREFLASH I
    [InlineData(8, 109)]   // QUICKNESS I
    [InlineData(40, 141)]  // CHARM
    [InlineData(54, 155)]  // DIVINE I
    public void CombatAction_CastSpellEncoding(int spellIndex, int packed)
    {
        Assert.Equal(packed, CombatAction.CastSpell(spellIndex));
        Assert.True(CombatAction.TryDecodeCast(packed, out var decoded, out _));
        Assert.Equal(spellIndex, decoded);
    }

    [Fact]
    public void CombatAction_CastHealingEncoding()
    {
        var packed = CombatAction.CastHealing(spellIndex0To3: 2, targetMemberIndex: 1);
        Assert.Equal(3001, packed);
        Assert.True(CombatAction.TryDecodeCast(packed, out var spell, out var target));
        Assert.Equal(2, spell);
        Assert.Equal(1, target);
    }

    [Theory]
    // D-029 (remaster design): manual top at INT 18, scaled down below it, capped at 18; minimum 1.
    [InlineData(0, 3, 18, 1)]
    [InlineData(99, 0, 18, 10)]
    [InlineData(99, 1, 18, 30)]
    [InlineData(99, 2, 18, 60)]
    [InlineData(99, 3, 18, 100)]
    [InlineData(99, 3, 19, 100)]  // ELF ceiling: no more than the manual
    [InlineData(99, 3, 13, 72)]   // 1 + 99·99·13/1782
    [InlineData(99, 0, 13, 7)]
    [InlineData(50, 3, 18, 51)]   // 1 + 50·99·18/1782
    public void FireflashDamage_ManualRanges_ScaledByInt(int roll, int tier, int intel, int expected)
    {
        Assert.Equal(expected, Magic.FireflashDamage(roll, tier, intel));
    }

    [OriginalDataFact]
    public void ResolveRound_HealingSpendsMpAndRestoresHp()
    {
        var party = new Party();
        var priest = new Character
        {
            Name = "Priest",
            Class = CharacterClass.Priest,
            Status = CharacterStatus.Ok,
            HpCur = 5,
            HpMax = 20,
            MagicCur = 10,
            MagicMax = 10,
            MpCeiling = 10,
            SpellsKnown = new int[SpellId.Count],
        };
        priest.SpellsKnown[SpellId.HealingI] = 1;
        party.Members.Add(priest);

        var enc = new CombatEncounter(party);
        enc.AddRank(new Monster { Name = "RAT", HitPoints = 50, Defense = 99, AttackPower = 1 });
        enc.Prepare();
        enc.SetAction(0, CombatAction.CastHealing(0, 0));

        // heal rng = 50 → heal 5
        var rng = new ScriptedRandomSource(50);
        var result = new CombatResolver().ResolveRound(enc, rng);

        Assert.Equal(9, priest.MagicCur); // cost 1
        Assert.Equal(10, priest.HpCur);   // 5 + 5
        Assert.Contains(result.Log, l => l.Contains("healed", StringComparison.OrdinalIgnoreCase)
            || l.Contains("casts", StringComparison.Ordinal));
    }

    [OriginalDataFact]
    public void ResolveRound_FireflashDamagesTarget()
    {
        var party = new Party();
        var wiz = new Character
        {
            Name = "Wiz",
            Class = CharacterClass.Wizard,
            Status = CharacterStatus.Ok,
            HpCur = 20,
            HpMax = 20,
            MagicCur = 10,
            MagicMax = 10,
            MpCeiling = 12,
            SpellsKnown = new int[SpellId.Count],
        };
        wiz.SpellsKnown[SpellId.FireflashI] = 1;
        party.Members.Add(wiz);

        var mon = new Monster
        {
            Name = "VIPER",
            HitPoints = 20,
            Defense = 5,
            Armor = 0,
            AttackPower = 1,
            CombatRating = 5,
        };
        var enc = new CombatEncounter(party);
        enc.AddRank(mon);
        enc.Prepare();
        enc.SetAction(0, CombatAction.CastSpell(SpellId.FireflashI));

        // pick target (only one), damage rng 99 → 99*9/2000+1 = 1
        var rng = new ScriptedRandomSource(99);
        var result = new CombatResolver().ResolveRound(enc, rng);

        Assert.Equal(9, wiz.MagicCur);
        Assert.True(wiz.CastSinceRest); // OV2 0x0a55 — feeds the Inn's MAGIC INCREASES roll
        Assert.Equal(19, mon.HitPoints);
        Assert.Contains(result.Log, l => l.Contains("TAKES", StringComparison.Ordinal));
        // s121: the hit names the line it came before (its "takes" line), where III Amiga's field flashes it (§8.6).
        var hit = Assert.Single(enc.TakeRoundHits(), h => h.Source == DamageSource.PartySpell);
        Assert.Equal(GameText.F("core.combat.x_takes_x_damage", "VIPER", 1), result.Log[hit.Line]);
    }

    [Fact]
    public void FireflashTarget_IsInTheFrontRank_SkippingAWipedOne() // D-059 (user s53): manual
    {
        static Monster Mon(string name, int hp) => new() { Name = name, HitPoints = hp };
        var enc = new CombatEncounter(new Party());
        enc.AddRank(Mon("DEAD", 0));
        enc.AddRank(Mon("ORC1", 5), Mon("ORC2", 5), Mon("ORC3", 5));
        enc.AddRank(Mon("BACK", 5));

        Assert.Equal("ORC3", SpellCaster.PickFireflashTarget(enc, new ScriptedRandomSource(2))!.Name);
        Assert.Equal("ORC1", SpellCaster.PickFireflashTarget(enc, new ScriptedRandomSource(0))!.Name);
    }

    [Fact]
    public void ResolveRound_StrengthRebuildsWeaponDamageFromBase()
    {
        var party = new Party();
        var monk = new Character
        {
            Name = "Monk",
            Class = CharacterClass.Monk,
            Status = CharacterStatus.Ok,
            HpCur = 30,
            HpMax = 30,
            MagicCur = 8,
            MagicMax = 8,
            WeaponDamageA = 10,
            WeaponDamageB = 0,
            SpellsKnown = new int[SpellId.Count],
        };
        monk.SpellsKnown[SpellId.StrengthIi] = 1;
        party.Members.Add(monk);

        var enc = new CombatEncounter(party);
        enc.AddRank(new Monster { Name = "RAT", HitPoints = 100, Defense = 99, AttackPower = 1 });
        enc.Prepare();
        Assert.Equal(10, enc.Prep[0].WeaponDamageEff);
        enc.Prep[0].WeaponDamageEff = 3; // an earlier scaling of @248 (OV2 0x0527 rebuilds from @28 + @240)

        enc.SetAction(0, CombatAction.CastSpell(SpellId.StrengthIi));
        new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0));

        Assert.Equal(14, enc.Prep[0].WeaponDamageEff);
        Assert.Equal(6, monk.MagicCur); // cost 2
    }

    [OriginalDataFact]
    public void ResolveRound_CharmEndsTheFight_LikeGreetings()
    {
        // OV2 0x767 roll (r1·lvl > r2·L); D-022 (manual): the fight ends at once — kill XP, no gold, no treasure.
        var party = new Party();
        var wiz = MakeCaster("Wiz", SpellId.Charm);
        wiz.Level = 5;
        party.Members.Add(wiz);

        var enc = new CombatEncounter(party) { RewardXp = 40, RewardGold = 10, MonsterLevel = 1, ReactionSum = 3 };
        enc.AddRank(new Monster { Name = "ORC", HitPoints = 30, AttackPower = 80, Defense = 5 });
        enc.Prepare();
        enc.MonsterDamaged = true;
        enc.SetAction(0, CombatAction.CastSpell(SpellId.Charm));

        // ready 0, r1 50, r2 50: 250 > 50.
        var result = new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0, 50, 50));
        Assert.True(result.EncounterOver);
        Assert.Equal(40, result.RewardXp);
        Assert.Equal(0, result.RewardGold);
        Assert.True(enc.TreasureForfeited);
        Assert.Contains(result.Log, l => l.Contains("CHARMED", StringComparison.Ordinal));
    }

    [OriginalDataFact]
    public void ResolveRound_DispellUndead_DissolvesAndPaysFrontGroupXp()
    {
        // OV2 0x8fc → 0x85b (s31): SKELETON is undead (D-024 data flag); v = 5·(5 − 1) − 0 > −5 → dissolve.
        var party = new Party();
        var priest = MakeCaster("Priest", SpellId.DispellUndead);
        priest.Class = CharacterClass.Priest;
        priest.Level = 5;
        party.Members.Add(priest);

        var enc = new CombatEncounter(party) { RewardXp = 100, RewardGold = 50, MonsterLevel = 1 };
        enc.AddRank(
            new Monster { Name = "SKELETON", HitPoints = 20, Level = 1, ExperienceValue = 77, Undead = true },
            new Monster { Name = "SKELETON", HitPoints = 20, Level = 1, ExperienceValue = 77, Undead = true });
        enc.Prepare();
        enc.SetAction(0, CombatAction.CastSpell(SpellId.DispellUndead));

        var resolver = new CombatResolver();
        var round = resolver.ResolveRound(enc, new ScriptedRandomSource(0));
        Assert.False(round.EncounterOver); // the round plays out; the end comes at the next round top
        Assert.Contains("MONSTERS START DISSOLVING", round.Log);

        var end = resolver.ResolveRound(enc, new ScriptedRandomSource(0));
        Assert.True(end.EncounterOver);
        Assert.True(end.PartyWon);
        Assert.Equal(177, end.RewardXp); // 100 + 77 · (2 living − 1)
        Assert.Equal(0, end.RewardGold);
        Assert.Contains("MONSTERS KILLED", end.Log);
    }

    [Fact]
    public void ResolveRound_AwakenWakesSleepingPartyMembers()
    {
        // Manual #51: AWAKEN "rouses all sleeping party members".
        var party = new Party();
        var priest = MakeCaster("Priest", SpellId.Awaken);
        priest.Class = CharacterClass.Priest;
        var sleeper = MakeCaster("Dozer", SpellId.Awaken);
        sleeper.Status = CharacterStatus.Sleep;
        party.Members.Add(priest);
        party.Members.Add(sleeper);

        var enc = new CombatEncounter(party);
        enc.AddRank(new Monster { Name = "KOBOLD", HitPoints = 50, AttackPower = 0 });
        enc.Prepare();
        enc.SetAction(0, CombatAction.CastSpell(SpellId.Awaken));

        new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0));
        Assert.Equal(CharacterStatus.Ok, sleeper.Status);
    }

    [Fact]
    public void CastOutOfCombat_RejectsCombatOnlySpell()
    {
        var party = new Party();
        var wiz = MakeCaster("Wiz", SpellId.FireflashI);
        party.Members.Add(wiz);
        var result = SpellCaster.CastOutOfCombat(party, wiz, SpellId.FireflashI, null, new ScriptedRandomSource(0));
        Assert.False(result.Success);
        Assert.Equal(10, wiz.MagicCur); // not spent
    }

    [Theory]
    [InlineData(SpellId.Transportation, CastLocation.Town, true)]
    [InlineData(SpellId.Transportation, CastLocation.Field, false)]
    [InlineData(SpellId.Transportation, CastLocation.Dungeon, false)]
    [InlineData(SpellId.Vision, CastLocation.Town, false)]
    [InlineData(SpellId.Vision, CastLocation.Field, true)]
    [InlineData(SpellId.Vision, CastLocation.Dungeon, false)]
    [InlineData(SpellId.MonsterEval, CastLocation.Town, false)]
    [InlineData(SpellId.MonsterEval, CastLocation.Field, true)]
    [InlineData(SpellId.MonsterEval, CastLocation.Dungeon, true)]
    [InlineData(SpellId.HealingI, CastLocation.Town, true)]
    [InlineData(SpellId.HealingI, CastLocation.Dungeon, true)]
    public void CastOutOfCombat_PlaceRule_D035(int spell, CastLocation where, bool allowed)
    {
        var party = new Party();
        var caster = MakeCaster("Caster", spell);
        party.Members.Add(caster);
        var result = SpellCaster.CastOutOfCombat(party, caster, spell, caster, new ScriptedRandomSource(0), where);
        Assert.Equal(allowed, result.Success);
        if (!allowed)
            Assert.Equal(10, caster.MagicCur); // refused before the MP is spent
        Assert.Equal(allowed, caster.CastSinceRest); // D-070 (manual): any cast counts for MAGIC INCREASES
    }

    [Fact]
    public void CastOutOfCombat_HealingWorks()
    {
        var party = new Party();
        var priest = MakeCaster("Priest", SpellId.HealingIi);
        priest.Class = CharacterClass.Priest;
        priest.HpCur = 3;
        priest.HpMax = 30;
        party.Members.Add(priest);

        var result = SpellCaster.CastOutOfCombat(
            party, priest, SpellId.HealingIi, priest, new ScriptedRandomSource(50));
        Assert.True(result.Success);
        Assert.Equal(8, priest.MagicCur); // cost 2
        Assert.Equal(17, priest.HpCur); // 3 + 14
    }

    [OriginalDataFact]
    public void CastOutOfCombat_ThenInn_CanRaiseMagic_D070() // manual: any cast counts; DOS: combat only
    {
        var party = new Party();
        var priest = MakeCaster("Priest", SpellId.HealingI);
        priest.Class = CharacterClass.Priest;
        priest.MpCeiling = priest.MagicMax + 5;
        party.Members.Add(priest);

        Assert.True(SpellCaster.CastOutOfCombat(party, priest, SpellId.HealingI, priest, new ScriptedRandomSource(0)).Success);
        Assert.True(priest.CastSinceRest);

        var before = priest.MagicMax;
        Assert.Contains("MAGIC INCREASES", TownMenus.Rest(party, new ScriptedRandomSource(0)).Message);
        Assert.Equal(before + 1, priest.MagicMax);
        Assert.False(priest.CastSinceRest);
    }

    // --- MONSTER EVAL. / VISION / DIVINE I (OV2 0x4790, s41) -----------------------------------

    [Theory]
    [InlineData(CastLocation.Field, 0, 9, 2)]   // wilderness: 2·(T+1)
    [InlineData(CastLocation.Field, 3, 0, 8)]
    [InlineData(CastLocation.Field, 5, 0, 12)]
    [InlineData(CastLocation.Field, 9, 0, 12)]  // capped at 12
    [InlineData(CastLocation.Dungeon, 9, 0, 2)] // dungeon: 2·tier + 2, terrain ignored
    [InlineData(CastLocation.Dungeon, 0, 4, 10)]
    public void MonsterEvalLevel_MatchesOv2_0x4860(CastLocation where, int terrain, int tier, int expected) =>
        Assert.Equal(expected, SpellCaster.MonsterEvalLevel(where, terrain, tier));

    [OriginalDataFact]
    public void CastOutOfCombat_MonsterEval_PrintsLevel_NoRoll()
    {
        var party = new Party();
        var caster = MakeCaster("Caster", SpellId.MonsterEval);
        party.Members.Add(caster);
        var result = SpellCaster.CastOutOfCombat(party, caster, SpellId.MonsterEval, null,
            new ScriptedRandomSource(0), CastLocation.Dungeon, new FieldCastContext(DungeonTier: 2));
        Assert.True(result.Success);
        Assert.Equal("MONSTERS ARE AT LEVEL 6", result.Message);
        Assert.Equal(6, caster.MagicCur); // 51 % 4 + 1 = 4
    }

    [Fact]
    public void ManualMonsterEval_D056_RoundedAverage_WhenRollIsRight()
    {
        Assert.Equal(3, SpellCaster.ManualMonsterEvalLevel([2, 3, 4, 4], new ScriptedRandomSource(SpellCaster.MonsterEvalErrorChance)));
    }

    [Theory]
    [InlineData(3, 0, 2)]  // −1
    [InlineData(3, 1, 4)]  // +1
    [InlineData(1, 0, 1)]  // −1, floored at 1
    public void ManualMonsterEval_D056_OffByOne_WhenRollIsWrong(int average, int offset, int expected) =>
        Assert.Equal(expected, SpellCaster.ManualMonsterEvalLevel([average, average], new ScriptedRandomSource(SpellCaster.MonsterEvalErrorChance - 1, offset)));

    [OriginalDataFact]
    public void Session_MonsterEval_D056_ReadsTheLocalMonsterPool()
    {
        var data = OpenPhantasie.Data.GameDataRepository.LoadPhantasie1();
        var session = OpenPhantasie.Data.GameSessionFactory.Create(data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Seer", Race.Human, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(hero, out _));
        hero.SpellsKnown[SpellId.MonsterEval] = 1;
        hero.MagicCur = hero.MagicMax = hero.MpCeiling = 10;
        session.DepartGuildToWilderness();
        var region = session.CurrentOverworldRegion;
        var t = region.GetNormalized(session.Overworld.X, session.Overworld.Y) % 10;
        var expected = (int)Math.Round(Enumerable.Range(12 * t, 12).Where(id => data.Monsters.Any(m => m.Id == id)).Select(id => data.GetMonster(id).Level).Average(),
            MidpointRounding.AwayFromZero);

        var result = session.CastSpell(hero, SpellId.MonsterEval);

        // The seeded roll may land in the 20 % error band: right answer, or off by 1–2.
        var shown = int.Parse(result.Message!["MONSTERS ARE AT LEVEL ".Length..]);
        Assert.InRange(shown, Math.Max(1, expected - 2), expected + 2);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(95, 25)]
    public void VisionRadius_IsMpLeftPlus5Over4(int mpLeft, int expected) =>
        Assert.Equal(expected, SpellCaster.VisionRadius(mpLeft));

    [Fact]
    public void CastOutOfCombat_Vision_D057_RevealsWholeRegion()
    {
        var tiles = Enumerable.Repeat((byte)(8 + OverworldRegion.FogOffset), OverworldRegion.Width * OverworldRegion.Height).ToArray();
        var region = new OverworldRegion(1, "OUT1.DAT", tiles, Array.Empty<OverworldFeature>(), Array.Empty<string>());
        var party = new Party();
        var caster = MakeCaster("Seer", SpellId.Vision); // MP 10 → 9 left → radius (900+500)/400 = 3
        party.Members.Add(caster);

        var result = SpellCaster.CastOutOfCombat(party, caster, SpellId.Vision, null, new ScriptedRandomSource(0),
            CastLocation.Field, new FieldCastContext(Region: region, X: 1, Y: 10));

        Assert.True(result.Success);
        Assert.Equal(9, caster.MagicCur);
        for (var y = 0; y < OverworldRegion.Height; y++)
        for (var x = 0; x < OverworldRegion.Width; x++)
        {
            Assert.Equal(8, region.Tiles[y * OverworldRegion.Width + x]); // D-057: DOS would clear only radius 3
        }
    }

    [Fact]
    public void DivineI_CostsTwo_AndDoesNothing_InTownAndField()
    {
        foreach (var where in new[] { CastLocation.Town, CastLocation.Field, CastLocation.Dungeon })
        {
            var party = new Party();
            var caster = MakeCaster("Victor", SpellId.DivineI);
            party.Members.Add(caster);
            var result = SpellCaster.CastOutOfCombat(party, caster, SpellId.DivineI, null, new ScriptedRandomSource(0), where);
            Assert.True(result.Success);
            Assert.Equal(2, result.MpSpent);
            Assert.Equal(8, caster.MagicCur);
            Assert.Empty(result.Log);
        }
    }

    [Fact]
    public void DosSlot_MapsDivineITo57_AndBack()
    {
        Assert.Equal(53, SpellId.DosSlot(SpellId.Transportation));
        Assert.Equal(57, SpellId.DosSlot(SpellId.DivineI));
        Assert.Equal(SpellId.DivineI, SpellId.FromDosSlot(57));
        foreach (var empty in new[] { 54, 55, 56, 58, 59 })
            Assert.Equal(-1, SpellId.FromDosSlot(empty));
    }

    // --- RESURRECTION (OV1 0x2693 town, OV2 0x113b combat/field) -----------------------------

    private static (Party party, Character priest) PartyWithPriest(int priestLevel = 5)
    {
        var party = new Party();
        var priest = MakeCaster("Priest", SpellId.Resurrection);
        priest.Class = CharacterClass.Priest;
        priest.Level = priestLevel;
        party.Members.Add(priest);
        return (party, priest);
    }

    private static Character AddCorpse(Party party, string name, int level = 5, int con = 10)
    {
        var dead = MakeCaster(name, SpellId.HealingI);
        dead.Status = CharacterStatus.Dead;
        dead.HpCur = 0;
        dead.HpMax = 25;
        dead.Level = level;
        dead.Stats = new CharacterStats(10, 10, 10, con, 10, 10);
        party.Members.Add(dead);
        return dead;
    }

    [Theory]
    [InlineData(16022, true)]  // 16022/327 = 48 < 50
    [InlineData(16349, true)]  // 49
    [InlineData(16350, false)] // 50, not < 50
    public void Resurrection_Town_RollsDosWordOver327(int word, bool raised)
    {
        var (party, priest) = PartyWithPriest();
        var dead = AddCorpse(party, "Dead");

        var result = SpellCaster.CastOutOfCombat(party, priest, SpellId.Resurrection, null,
            new ScriptedRandomSource(word));

        Assert.True(result.Success);
        Assert.Equal(4, result.MpSpent);
        Assert.Equal(6, priest.MagicCur); // 4 MP even on failure
        Assert.Equal(raised ? CharacterStatus.Ok : CharacterStatus.Dead, dead.Status);
        Assert.Equal(raised ? 25 : 0, dead.HpCur); // HP = max, not max/2
        Assert.Equal(10, dead.Stats.Con);          // no CON change (D-009: loss is at death)
    }

    [Fact]
    public void Resurrection_InCombat_RaisedMemberGetsNoActionThisRound()
    {
        // [C] OV2 0x113b (s41): order [0x74fa+2i] := 4 for the raised slot.
        var (party, priest) = PartyWithPriest();
        var dead = AddCorpse(party, "Dead");
        var enc = new CombatEncounter(party);
        enc.AddRank(new Monster { Name = "KOBOLD", HitPoints = 50, AttackPower = 0 });
        enc.Prepare();
        enc.SetAction(1, CombatAction.CastSpell(SpellId.FireflashI)); // stale order on the corpse

        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Resurrection),
            new ScriptedRandomSource(0), new List<string>());

        Assert.Equal(CharacterStatus.Ok, dead.Status);
        Assert.Equal(CombatAction.Cleared, enc.Prep[1].PackedAction);
        Assert.Equal(2, CombatEncounter.Survivors(party)); // [0x6490]++: shares the payout
    }

    [Theory]
    [InlineData(CastLocation.Field)]
    [InlineData(null)] // combat
    public void Resurrection_Ov2_Rolls0To99(CastLocation? location)
    {
        foreach (var (roll, raised) in new[] { (49, true), (50, false) })
        {
            var (party, priest) = PartyWithPriest();
            var dead = AddCorpse(party, "Dead");
            if (location is { } loc)
            {
                SpellCaster.CastOutOfCombat(party, priest, SpellId.Resurrection, null,
                    new ScriptedRandomSource(roll), loc);
            }
            else
            {
                var enc = new CombatEncounter(party);
                enc.AddRank(new Monster { Name = "KOBOLD", HitPoints = 50, AttackPower = 0 });
                enc.Prepare();
                SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Resurrection),
                    new ScriptedRandomSource(roll), new List<string>());
            }

            Assert.Equal(raised ? CharacterStatus.Ok : CharacterStatus.Dead, dead.Status);
            Assert.Equal(6, priest.MagicCur);
        }
    }

    [Theory]
    [InlineData(16, 5, 32767, true)]  // Δ = +11 → 105: even the top town roll (100) succeeds
    [InlineData(1, 11, 0, false)]     // Δ = −10 → 0: even roll 0 fails
    public void Resurrection_LevelGapSetsOdds(int casterLevel, int targetLevel, int word, bool raised)
    {
        var (party, priest) = PartyWithPriest(casterLevel);
        var dead = AddCorpse(party, "Dead", targetLevel);

        SpellCaster.CastOutOfCombat(party, priest, SpellId.Resurrection, null, new ScriptedRandomSource(word));

        Assert.Equal(raised ? CharacterStatus.Ok : CharacterStatus.Dead, dead.Status);
    }

    [OriginalDataFact]
    public void Resurrection_RollsEachCorpseInPartyOrderAndRaisesAtMostOne()
    {
        var (party, priest) = PartyWithPriest();
        var first = AddCorpse(party, "First");
        var second = AddCorpse(party, "Second");
        var third = AddCorpse(party, "Third");
        // First roll 60 fails, second 10 succeeds; third would also succeed but must not be rolled.
        var rng = new CountingRandom(60 * 327, 10 * 327, 0);

        var result = SpellCaster.CastOutOfCombat(party, priest, SpellId.Resurrection, first, rng);

        Assert.Equal(CharacterStatus.Dead, first.Status); // passed-in target is ignored
        Assert.Equal(CharacterStatus.Ok, second.Status);
        Assert.Equal(CharacterStatus.Dead, third.Status);
        Assert.Equal(2, rng.Calls);
        Assert.Equal("Second IS RESURRECTED", result.Message);
    }

    [Fact]
    public void Resurrection_SkipsCon0CorpseWithoutRolling()
    {
        var (party, priest) = PartyWithPriest();
        var aged = AddCorpse(party, "Aged", con: 0); // age-death zeroes stats → can't be raised
        var dead = AddCorpse(party, "Dead");
        var rng = new CountingRandom(0);

        SpellCaster.CastOutOfCombat(party, priest, SpellId.Resurrection, null, rng);

        Assert.Equal(CharacterStatus.Dead, aged.Status);
        Assert.Equal(CharacterStatus.Ok, dead.Status);
        Assert.Equal(1, rng.Calls);
    }

    [Fact]
    public void Resurrection_NoCorpse_StillSpendsMpAndPrintsNothing()
    {
        var (party, priest) = PartyWithPriest();

        var result = SpellCaster.CastOutOfCombat(party, priest, SpellId.Resurrection, null, new ScriptedRandomSource(0));

        Assert.True(result.Success);
        Assert.Equal(4, result.MpSpent);
        Assert.Equal(6, priest.MagicCur);
        Assert.Empty(result.Log);
    }

    // D-010 (user: manual): HEALING uses the combat range 1..c−1 in town and combat alike.
    [Theory]
    [InlineData(SpellId.HealingI, 0, 1)]
    [InlineData(SpellId.HealingI, 99, 9)]
    [InlineData(SpellId.HealingIi, 99, 27)]
    [InlineData(SpellId.HealingIi + 1, 99, 57)]
    [InlineData(SpellId.HealingIv, 99, 99)]
    public void Healing_TownAndCombatGiveSameAmount(int spell, int roll, int expected)
    {
        Character Patient() => new() { Name = "Hurt", Status = CharacterStatus.Ok, HpCur = 1, HpMax = 200 };

        var townParty = new Party();
        var townPriest = MakeCaster("Priest", spell);
        townPriest.Class = CharacterClass.Priest;
        var townPatient = Patient();
        townParty.Members.Add(townPriest);
        townParty.Members.Add(townPatient);
        var town = SpellCaster.CastOutOfCombat(townParty, townPriest, spell, townPatient, new ScriptedRandomSource(roll));

        var combatParty = new Party();
        var combatPriest = MakeCaster("Priest", spell);
        combatPriest.Class = CharacterClass.Priest;
        combatParty.Members.Add(combatPriest);
        combatParty.Members.Add(Patient());
        var enc = new CombatEncounter(combatParty);
        enc.AddRank(new Monster { Name = "KOBOLD", HitPoints = 50, AttackPower = 0 });
        enc.Prepare();
        var combat = SpellCaster.ResolveCombatCast(enc, 0,
            CombatAction.CastHealing(spellIndex0To3: spell, targetMemberIndex: 1),
            new ScriptedRandomSource(roll), new List<string>());

        Assert.Equal(expected, town.HpRestored);
        Assert.Equal(expected, combat.HpRestored);
    }

    private sealed class CountingRandom(params int[] words) : IDosRandomSource
    {
        private int _i;
        public int Calls => _i;
        public int NextDosWord() => words[_i++];
        public int Next(int minInclusive, int maxExclusive) => NextDosWord() / 328;
    }

    [Fact]
    public void Binding_AlwaysLowersDefense_D013()
    {
        // D-013 (user, s23): positive defense scaled as OV2 0x1429; negative pushed further down; defense 0 drops too.
        var party = new Party();
        party.Members.Add(MakeCaster("Mage", SpellId.BindingIv));
        var enc = new CombatEncounter(party);
        var skeleton = new Monster { Name = "SKELETON", HitPoints = 3, CombatRating = 1, Defense = -75 };
        var troll = new Monster { Name = "TROLL", HitPoints = 40, CombatRating = 1, Defense = 0 };
        var zeus = new Monster { Name = "ZEUS", HitPoints = 600, CombatRating = 1, Defense = 999 };
        enc.AddRank(skeleton);
        enc.AddRank(troll);
        enc.AddRank(zeus);
        enc.Prepare();

        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.BindingIv),
            new ScriptedRandomSource(0, 0, 0), new List<string>());

        Assert.Equal(-116, skeleton.Defense); // −75 − 75·55/100 (DOS: −33, harder to hit)
        Assert.Equal(-11, troll.Defense);     // flat minimum 20·55/100 (DOS: 0)
        Assert.Equal(449, zeus.Defense);      // DOS 999·45/100
    }

    [Theory]
    [InlineData(0x55, 3)]
    [InlineData(0x46, 6)]
    [InlineData(0x37, 9)]
    [InlineData(0x2d, 11)]
    public void BoundDefense_FlatMinimumPerTier_D013(int pct, int minimumCut)
    {
        Assert.Equal(-minimumCut, Magic.BoundDefense(0, pct));
        Assert.Equal(5 - minimumCut, Magic.BoundDefense(5, pct));  // small positive: minimum beats the DOS scale
        Assert.Equal(200 * pct / 100, Magic.BoundDefense(200, pct)); // ≥ 20: exactly DOS
        for (var d = -100; d <= 1000; d++)
            Assert.True(Magic.BoundDefense(d, pct) < d);
    }

    [Fact]
    public void Confusion_ScalesSpecialGate_IvStopsSpecials_D014()
    {
        // OV2 0x1403: CONFUSION scales the special-ability gate +0x1e. DOS IV leaves 5 %; manual (D-014): none left.
        var party = new Party();
        party.Members.Add(MakeCaster("Mage", SpellId.ConfusionIv));
        var enc = new CombatEncounter(party);
        var lord = new Monster { Name = "BLACK LORD", HitPoints = 300, CombatRating = 1, SpecialChance = 75 };
        enc.AddRank(lord);
        enc.Prepare();

        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.ConfusionIv),
            new ScriptedRandomSource(0), new List<string>());

        Assert.Equal(0, lord.SpecialChance); // DOS would leave 75 · 5 / 100 = 3
        Assert.True((lord.StatusFlags & MonsterStatusFlags.Confused) != 0);
    }

    [Theory]
    [InlineData(50, true)]  // 50 ≤ 5·(10 − 10) + 50 — inclusive
    [InlineData(51, false)]
    public void ConfusionResist_UsesCasterMpMax16_NotCeiling14(int roll, bool hits)
    {
        // OV2 0x13b5 (s33): [caster·0x11a + 0x5bc0] = @16 MP max (base 0x5bb0). Ceiling @14 = 30 would give chance 150.
        var party = new Party();
        var mage = MakeCaster("Mage", SpellId.ConfusionI);
        mage.MagicMax = 10;
        mage.MpCeiling = 30;
        party.Members.Add(mage);
        var enc = new CombatEncounter(party);
        var mon = new Monster { Name = "ORC", HitPoints = 30, CombatRating = 10, SpecialChance = 50, Level = mage.Level };
        enc.AddRank(mon);
        enc.Prepare();

        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.ConfusionI),
            new ScriptedRandomSource(roll), new List<string>());

        Assert.Equal(hits ? 50 * 80 / 100 : 50, mon.SpecialChance);
    }

    [Theory]
    // D-028 (remaster design): chance = 5·(MP max − CV) + 2·(caster level − group level) + 50, inclusive.
    [InlineData(10, 9, 52, true)]   // 1 level up: 50 + 2 = 52
    [InlineData(10, 9, 53, false)]
    [InlineData(9, 9, 50, true)]    // equal levels: the DOS roll
    [InlineData(9, 9, 51, false)]
    [InlineData(1, 9, 34, true)]    // 8 levels down: 50 − 16 = 34 (DOS would give 50)
    [InlineData(1, 9, 35, false)]
    [InlineData(15, 9, 62, true)]   // 6 levels up: 50 + 12 = 62
    [InlineData(15, 9, 63, false)]
    public void ConfusionResist_LevelTerm_D028(int casterLevel, int groupLevel, int roll, bool hits)
    {
        var party = new Party();
        var mage = MakeCaster("Mage", SpellId.ConfusionI);
        mage.MagicMax = 10;
        mage.Level = casterLevel;
        party.Members.Add(mage);
        var enc = new CombatEncounter(party);
        var mon = new Monster { Name = "TROLL", HitPoints = 30, CombatRating = 10, SpecialChance = 50, Level = groupLevel };
        enc.AddRank(mon);
        enc.Prepare();

        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.ConfusionI),
            new ScriptedRandomSource(roll), new List<string>());

        Assert.Equal(hits ? 50 * 80 / 100 : 50, mon.SpecialChance);
    }

    private static Character MakeCaster(string name, int knownSpell)
    {
        var ch = new Character
        {
            Name = name,
            Class = CharacterClass.Wizard,
            Status = CharacterStatus.Ok,
            HpCur = 20,
            HpMax = 20,
            MagicCur = 10,
            MagicMax = 10,
            MpCeiling = 10,
            SpellsKnown = new int[SpellId.Count],
        };
        ch.SpellsKnown[knownSpell] = 1;
        return ch;
    }
}
