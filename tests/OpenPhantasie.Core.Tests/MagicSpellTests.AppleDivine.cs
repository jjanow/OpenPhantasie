using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// D-090 (user s227): the Apple II's spell #60 (DIVINE REVIVAL, I's index 55), granted by the victory under
/// <c>p1.victory</c> = <c>apple</c>: DUNGON 6930 → 5500 (Apple <c>GRAPHICS.md</c> section 8.2).
/// </summary>
public partial class MagicSpellTests
{
    private static Character Body(string name, int str, int con, int ageClock) => new()
    {
        Name = name,
        Status = CharacterStatus.Dead,
        HpCur = 0,
        HpMax = 30,
        Stats = new CharacterStats(str, 10, 10, con, 10, 10),
        AgeClock = ageClock,
        SpellsKnown = new int[SpellId.Count],
    };

    private static Character AppleDivineCaster()
    {
        var caster = MakeCaster("Victor", SpellId.HealingI);
        Endgame.LearnAppleDivine(caster);
        caster.Stats = new CharacterStats(10, 10, 10, 10, 10, 10);
        return caster;
    }

    [Fact]
    public void AppleDivine_IsIndex55_Slot59_FourMp_InIOnly()
    {
        Assert.Equal(56, SpellSlots.PhantasieI.Count);
        Assert.Equal(59, SpellSlots.PhantasieI.DosSlot(SpellId.AppleDivine));
        Assert.Equal(4, SpellSlots.PhantasieI.MagicPointCost(SpellId.AppleDivine)); // DUNGON 6886: 60 − 4·14
        Assert.Equal(56, SpellSlots.PhantasieII.DosSlot(SpellId.DivineIi));          // II's index 55 stays DIVINE II
        Assert.True(SpellCaster.IsAppleDivine(SpellId.AppleDivine, GameOptions.ForPreset(GameId.P1)));
        Assert.False(SpellCaster.IsAppleDivine(SpellId.DivineIi, GameOptions.ForPreset(GameId.P2)));
    }

    [Fact]
    public void AppleDivine_InADungeon_RevivesEveryCon0Body_ThenTheCasterLosesItAndAllMp()
    {
        var caster = AppleDivineCaster();
        var worn = Body("Worn", str: 12, con: 0, ageClock: 30 * 26);  // 780 = 3·256 + 12
        var dead = Body("Dead", str: 12, con: 5, ageClock: 520);      // CON > 0: RESURRECTION's case, not #60's
        var blank = Body("Blank", str: 0, con: 0, ageClock: 520);     // STR 0: skipped (W(1) < 1)
        var old = Body("Old", str: 9, con: 0, ageClock: 2000);        // 7·256 + 208
        var party = new Party();
        party.Members.AddRange([caster, worn, dead, blank, old]);

        var result = SpellCaster.CastOutOfCombat(party, caster, SpellId.AppleDivine, null, new ScriptedRandomSource(0, 8),
            CastLocation.Dungeon);

        Assert.True(result.Success);
        Assert.Equal(4, result.MpSpent);
        Assert.Equal("Worn IS REVIVED", result.Message);
        Assert.Equal(["Old IS REVIVED"], result.Log);
        Assert.Equal((CharacterStatus.Ok, 3, 0), (worn.Status, worn.Stats.Con, worn.HpCur)); // CON 3 + 0; HP untouched
        Assert.Equal((CharacterStatus.Ok, 11, 0), (old.Status, old.Stats.Con, old.HpCur));   // CON 3 + 8
        Assert.Equal(1 * 256 + 12, worn.AgeClock);  // high byte 3 · 0.6 → 1, low byte kept
        Assert.Equal(4 * 256 + 208, old.AgeClock);  // 7 · 0.6 → 4
        Assert.Equal((CharacterStatus.Dead, 5, 520), (dead.Status, dead.Stats.Con, dead.AgeClock));
        Assert.Equal((CharacterStatus.Dead, 0), (blank.Status, blank.Stats.Con));
        Assert.Equal(0, caster.MagicCur);
        Assert.False(Magic.KnowsSpell(caster, SpellId.AppleDivine)); // one use
    }

    [Fact]
    public void AppleDivine_NoBodies_SpendsTheMp_KeepsTheSpell()
    {
        var caster = AppleDivineCaster();
        var party = new Party();
        party.Members.AddRange([caster, Body("Dead", str: 12, con: 5, ageClock: 520)]);

        var result = SpellCaster.CastOutOfCombat(party, caster, SpellId.AppleDivine, null, new ScriptedRandomSource(0),
            CastLocation.Dungeon);

        Assert.True(result.Success);
        Assert.Equal("NO USELESS BODIES HERE", result.Message);
        Assert.Equal(6, caster.MagicCur);
        Assert.True(Magic.KnowsSpell(caster, SpellId.AppleDivine));
    }

    [Fact]
    public void AppleDivine_TownAndWilderness_Refused_NotListed()
    {
        foreach (var where in new[] { CastLocation.Town, CastLocation.Field })
        {
            var caster = AppleDivineCaster();
            var worn = Body("Worn", str: 12, con: 0, ageClock: 520);
            var party = new Party();
            party.Members.AddRange([caster, worn]);

            var result = SpellCaster.CastOutOfCombat(party, caster, SpellId.AppleDivine, null, new ScriptedRandomSource(0), where);

            Assert.False(result.Success); // D-035 manual: refused before any MP is spent
            Assert.Equal(10, caster.MagicCur);
            Assert.Equal(CharacterStatus.Dead, worn.Status);
            Assert.DoesNotContain(SpellId.AppleDivine, SpellCaster.OutOfCombatChoices(caster, where));
        }
        Assert.Contains(SpellId.AppleDivine, SpellCaster.OutOfCombatChoices(AppleDivineCaster(), CastLocation.Dungeon));
        Assert.False(SpellCaster.IsCombatCastable(SpellId.AppleDivine)); // DUNGON 1310: #1-51 only
    }

    [Fact]
    public void AppleDivine_DosCastList_InTown_SpendsTheMp_DoesNothing()
    {
        // D-035 dos: every known spell is listed and charged; the Apple's TOWN 6886-6930 charges 4 MP and refuses too.
        var rules = GameOptions.ForPreset(GameId.P1).With(RuleOptionRegistry.CastList.Id, "dos");
        var caster = AppleDivineCaster();
        var worn = Body("Worn", str: 12, con: 0, ageClock: 520);
        var party = new Party();
        party.Members.AddRange([caster, worn]);

        var result = SpellCaster.CastOutOfCombat(party, caster, SpellId.AppleDivine, null, new ScriptedRandomSource(0),
            CastLocation.Town, rules: rules);

        Assert.True(result.Success);
        Assert.Equal(6, caster.MagicCur);
        Assert.Equal(CharacterStatus.Dead, worn.Status);
        Assert.True(Magic.KnowsSpell(caster, SpellId.AppleDivine));
    }

    [Fact]
    public void AppleDivine_GuildDat_Slot59At214_RoundTrips()
    {
        var hero = new Character { Name = "Victor", SpellsKnown = new int[SpellId.Count] };
        Assert.Equal(214, GuildCodec.SpellFlagOffset(SpellId.AppleDivine));
        Assert.Equal(0, GuildCodec.WriteBinary([hero])[214]);
        Endgame.LearnAppleDivine(hero);
        var bytes = GuildCodec.WriteBinary([hero]);
        Assert.Equal(1, bytes[214]);
        Assert.Equal(0, bytes[210]); // not DIVINE I
    }

    [OriginalDataFact]
    public void AppleDivine_GuildDat_ReadsBack_OnlyAKnownFlag()
    {
        var tables = GameDataRepository.LoadPhantasie1().CreationTables;
        var hero = new Character { Name = "Victor", RosterOrdinal = 1, SpellsKnown = new int[SpellId.Count] };
        Endgame.LearnAppleDivine(hero);
        var bytes = GuildCodec.WriteBinary([hero]);
        Assert.Equal(1, GuildCodec.ReadBinary(bytes, tables)[0]!.SpellsKnown[SpellId.AppleDivine]);

        bytes[214] = 2; // not a known flag: a DOS record reads as before (55 spells)
        Assert.Equal(SpellId.Count, GuildCodec.ReadBinary(bytes, tables)[0]!.SpellsKnown.Length);
    }

    [OriginalDataFact]
    public void AppleDivine_SurvivesSaveAndLoad()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var keeps = session.CreateAndEnroll("Keeps", Race.Human, CharacterClass.Wizard);
        var spent = session.CreateAndEnroll("Spent", Race.Elf, CharacterClass.Priest);
        Assert.True(session.TryAddToParty(keeps, out _));
        Assert.True(session.TryAddToParty(spent, out _));
        Endgame.LearnAppleDivine(keeps);
        Endgame.LearnAppleDivine(spent);
        spent.SpellsKnown[SpellId.AppleDivine] = 0; // cast away

        var loaded = GameSessionFactory.Create(data, new SeededDosRandomSource(3));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot())));

        Assert.True(Magic.KnowsSpell(loaded.Party.Members.Single(m => m.Name == "Keeps"), SpellId.AppleDivine));
        Assert.False(Magic.KnowsSpell(loaded.Party.Members.Single(m => m.Name == "Spent"), SpellId.AppleDivine));
    }

    [Fact]
    public void AppleDivine_StaysBehind_InTheIToIITransfer()
    {
        var hero = new Character { Name = "Victor", Class = CharacterClass.Fighter, SpellsKnown = new int[SpellId.Count] };
        hero.SpellsKnown[SpellId.DivineI] = 1;
        Endgame.LearnAppleDivine(hero);
        var spells = TransferIToII.Spells(hero, TransferIToIIRules.St);
        Assert.Equal(0, spells[SpellId.DivineIi]); // II's index 55 is DIVINE II: never set from I's #60
        Assert.Equal(1, spells[SpellId.DivineI]);
    }

    [OriginalDataFact]
    public void AppleDivine_IsInIsSpellData_AfterTheCopysTable()
    {
        var data = GameDataRepository.LoadPhantasie1();
        Assert.Equal(56, data.Spells.Count);
        var s = data.GetSpell(SpellId.AppleDivine);
        Assert.Equal(("DIVINE REVIVAL", 4, 59), (s.Name, s.MagicPointCost, s.DosSlot));
        Assert.Equal(55, data.Names.Spells.Count); // names.json is the copy's table alone
    }
}
