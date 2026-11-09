using OpenPhantasie.Core;
using OpenPhantasie.Data;
using Xunit;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// D-033 <c>dos</c> (<c>p1.summon-party</c>, <c>p1.monster-spell-target</c>): DOS's living list <c>[0x64a2]</c> and word <c>[0x6490]</c>
/// (<see cref="DosLivingList"/>, <c>combat-resolution.md</c> §Summon Elemental) and D-053's BEG leftover.
/// </summary>
public partial class AuthenticModeRestTests
{
    private static readonly int[] SpecialSingleTarget = [1, 1, 1];

    private static Monster Caster() => new()
    {
        Name = "MAGE", HitPoints = 20, Level = 1, ExperienceValue = 10, Specials = SpecialSingleTarget,
    };

    [Fact]
    public void DosLiving_RebuildKeepsTheTail_DeathAndResurrectionOnlyMoveTheWord()
    {
        var members = Enumerable.Range(0, 6).Select(i => Member($"M{i}")).ToList();
        var list = new DosLivingList();
        Assert.Equal(5, list.Rebuild(members));
        members[2].Status = CharacterStatus.Dead;
        list.Recount(members);
        Assert.Equal(5, list.Word);
        Assert.Equal([0, 1, 3, 4, 5, 5, null], list.Entries); // entry 5 keeps slot 5
        list.MemberDied();
        Assert.Equal(4, list.Word);

        list.OrdersPhase(members); // index 4
        members[2].Status = CharacterStatus.Ok;
        list.Resurrected(members); // 0x11d4 rebuild, 0x11d7 word + 1: still an index
        Assert.Equal(5, list.Word);
        Assert.Equal([0, 1, 2, 3, 4, 5, null], list.Entries);
    }

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Summon_Dos_AfterResurrection_TheWordStaysAnIndex_D033(bool dos)
    {
        // One DEAD, orders (index 4), RESURRECTION raises it: DOS's word is 5 → the summon works (0x11d7 only adds 1).
        var enc = Party6WithElemental(dos ? Dos(RuleOptionRegistry.SummonParty) : Manual, dead: 1);
        var party = enc.Party;
        party.DosLiving.OrdersPhase(party.Members);
        var raised = party.Members[5];
        party.Members[0].SpellsKnown[SpellId.Resurrection] = 1;
        var log = new List<string>();
        var r = SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Resurrection), new Strict(0), log);
        Assert.True(r.Success);
        Assert.Equal(CharacterStatus.Ok, raised.Status);
        Assert.Equal(5, party.DosLiving.Word);
        Assert.True(CombatSpells.SummonElemental(enc));
        Assert.Equal(7, party.Members.Count);
    }

    [OriginalDataTheory]
    [InlineData(false, 5)] // manual: rand·count/100 over the six → the last member can be hit
    [InlineData(true, 4)]  // dos: before any monster turn the word is the last index 5 → 99·5/100 = 4
    public void Special_FirstMonsterTurnOfTheRound_PicksOverTheIndex_D033(bool dos, int hit)
    {
        var enc = Party6WithElemental(dos ? Dos(RuleOptionRegistry.MonsterSpellTarget) : Manual, dead: 0);
        enc.Party.DosLiving.OrdersPhase(enc.Party.Members);
        var rng = new Strict(0, 99, 99); // special id, target, damage (99·12/100 = 11)
        MonsterSpecials.Use(enc, enc.Ranks[0], Caster(), rng, []);
        Assert.Equal(0, rng.Remaining);
        for (var i = 0; i < 6; i++)
            Assert.Equal(i == hit ? 9 : 20, enc.Party.Members[i].HpCur);
    }

    [OriginalDataFact]
    public void Summon_Dos_AfterAMonsterTurn_ListsTheElementalOnePlaceTooFar_D033()
    {
        var enc = Party6WithElemental(BothDos, dead: 0);
        var party = enc.Party;
        party.DosLiving.OrdersPhase(party.Members); // six listed: entry 5 = slot 5
        party.Members[5].Status = CharacterStatus.Dead;
        party.DosLiving.Recount(party.Members);     // a monster turn: count 5
        Assert.True(CombatSpells.SummonElemental(enc));
        Assert.Equal(7, party.Members.Count);
        Assert.Equal(6, party.DosLiving.Word);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], party.DosLiving.Entries); // entry 5 stale (DEAD slot 5), 6 = the elemental

        // A special now: 99·6/100 = 5 → the DEAD slot 5: rolled, but nobody takes it and no line (0x124a returns).
        var log = new List<string>();
        var rng = new Strict(0, 99, 99);
        MonsterSpecials.Use(enc, enc.Ranks[0], Caster(), rng, log);
        Assert.Equal(0, rng.Remaining);
        Assert.All(party.Members.Take(5), m => Assert.Equal(20, m.HpCur));
        Assert.Equal(party.Elemental!.HpMax, party.Elemental.HpCur);
        Assert.Single(log); // only "MAGE CASTS A SPELL"
    }

    private static GameOptions BothDos =>
        Manual.With(RuleOptionRegistry.SummonParty.Id, "dos").With(RuleOptionRegistry.MonsterSpellTarget.Id, "dos");

    [OriginalDataTheory]
    [InlineData(false)] // summon dos alone: the summon works (word 5), the pick stays manual → it can hit the elemental
    [InlineData(true)]  // target dos alone: the manual summon refuses five non-DEAD; the pick reads the list → slot 4
    public void TheTwoOptions_AreIndependent_D033(bool targetDos)
    {
        // A monster turn with slot 5 DEAD (count 5), a summon, then a single-target special rolling 99.
        var rules = targetDos ? Dos(RuleOptionRegistry.MonsterSpellTarget) : Dos(RuleOptionRegistry.SummonParty);
        var enc = Party6WithElemental(rules, dead: 0);
        var party = enc.Party;
        party.DosLiving.OrdersPhase(party.Members);
        party.Members[5].Status = CharacterStatus.Dead;
        party.DosLiving.Recount(party.Members);
        Assert.Equal(!targetDos, CombatSpells.SummonElemental(enc));
        Assert.Equal(targetDos ? 5 : 6, party.DosLiving.Word);

        var rng = new Strict(0, 99, 99);
        MonsterSpecials.Use(enc, enc.Ranks[0], Caster(), rng, []);
        Assert.Equal(0, rng.Remaining);
        var hurt = Enumerable.Range(0, party.Members.Count).Where(i => party.Members[i].HpCur != party.Members[i].HpMax);
        // manual: 99·6/100 = 5 over the six non-DEAD (slots 0–4 and the elemental); BothDos would hit the stale DEAD slot 5
        Assert.Equal([targetDos ? 4 : 6], hurt);
    }

    [OriginalDataFact]
    public void ManualSummon_WithDosTargets_ListsTheElementalAtTheNextRebuild_D033()
    {
        var enc = Party6WithElemental(Dos(RuleOptionRegistry.MonsterSpellTarget), dead: 0);
        var party = enc.Party;
        party.DosLiving.OrdersPhase(party.Members);
        party.DosLiving.Recount(party.Members); // a monster turn: count 6
        Assert.True(CombatSpells.SummonElemental(enc)); // manual: six non-DEAD
        Assert.Equal(6, party.DosLiving.Word);
        Assert.Equal([0, 1, 2, 3, 4, 5, null], party.DosLiving.Entries); // nothing written past the list

        var rng = new Strict(0, 99, 99); // 99·6/100 = 5 → slot 5
        MonsterSpecials.Use(enc, enc.Ranks[0], Caster(), rng, []);
        Assert.Equal(9, party.Members[5].HpCur);
        party.DosLiving.Recount(party.Members); // the special's own rebuild (0x1065) now lists slot 6
        Assert.Equal(7, party.DosLiving.Word);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], party.DosLiving.Entries);
    }

    [OriginalDataTheory]
    [InlineData(true, 6, 5)]  // failed Flee: no orders phase, the word stays the count 6 → the last member can be hit
    [InlineData(false, 5, 4)] // FIGHT: the orders phase stores the last index 5 → member 5 can't be the target
    public void FailedFlee_KeepsTheWordThroughTheRound_D033(bool flee, int word, int hit)
    {
        var enc = Party6WithElemental(Dos(RuleOptionRegistry.MonsterSpellTarget), dead: 0);
        var party = enc.Party;
        party.DosLiving.OrdersPhase(party.Members);
        party.DosLiving.Recount(party.Members); // last round ended after a monster turn: count 6
        var orc = enc.Ranks[0].Living.Single();
        orc.StatusFlags |= MonsterStatusFlags.Asleep; // the round has no monster turn (and so no recount)

        var option = PartyOptions.Choose(
            enc, flee ? PartyOption.Flee : PartyOption.Fight, new Strict(flee ? [99] : []), false, 0);
        Assert.Equal(flee ? PartyOptionOutcome.LoseRound : PartyOptionOutcome.GiveOrders, option.Outcome);
        foreach (var prep in enc.Prep)
            prep.PackedAction = CombatAction.Cleared; // FIGHT with no orders given: nobody rolls

        var rng = new Strict(99, 99, 99); // the ORC's wake roll in passes 1, 3 and 5 (≥ 15: stays asleep)
        new CombatResolver().ResolveRound(enc, rng);
        Assert.Equal(0, rng.Remaining);
        Assert.False(enc.OrdersSkipped);
        Assert.Equal(word, party.DosLiving.Word);

        // The next special, before any monster turn: 99·word/100.
        var pick = new Strict(0, 99, 99);
        MonsterSpecials.Use(enc, enc.Ranks[0], Caster(), pick, []);
        for (var i = 0; i < 6; i++)
            Assert.Equal(i == hit ? 9 : 20, party.Members[i].HpCur);
    }

    [OriginalDataTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Summon_Dos_SecondCastOnAFightingElemental_OnlyMovesTheList_D033(bool dos, bool works)
    {
        var enc = Party6WithElemental(dos ? Dos(RuleOptionRegistry.SummonParty) : Manual, dead: 0);
        var party = enc.Party;
        party.DosLiving.OrdersPhase(party.Members);
        Assert.True(CombatSpells.SummonElemental(enc));
        party.Members[2].Status = CharacterStatus.Dead;
        party.DosLiving.OrdersPhase(party.Members); // next round: seven members, one DEAD → index 5
        var elemental = party.Elemental!;
        var prep = enc.Prep[6].PackedAction;

        Assert.Equal(works, CombatSpells.SummonElemental(enc));
        Assert.Equal(7, party.Members.Count);
        Assert.Same(elemental, party.Members[6]);
        Assert.Equal(prep, enc.Prep[6].PackedAction);
        if (dos)
        {
            Assert.Equal(6, party.DosLiving.Word);
            Assert.Equal([0, 1, 3, 4, 5, 6, 6], party.DosLiving.Entries); // slot 6 listed twice
        }
    }

    [OriginalDataFact]
    public void DosLiving_SurvivesASaveAndLoad()
    {
        var s = NewGame(Dos(RuleOptionRegistry.SummonParty));
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("A", Race.Human, CharacterClass.Fighter), out _));
        s.Party.DosLiving.Restore([0, 3, 6], 2);
        var snap = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(9));
        loaded.ApplySnapshot(snap);
        Assert.Equal([0, 3, 6, null, null, null, null], loaded.Party.DosLiving.Entries);
        Assert.Equal(2, loaded.Party.DosLiving.Word);
    }

    [OriginalDataFact]
    public void FailedFlee_OrdersSkipped_SurvivesACombatSave_D033()
    {
        // A save between the failed Flee and its round must not bring back the orders phase (the word would be rebuilt).
        var s = WildernessFight(Dos(RuleOptionRegistry.MonsterSpellTarget), "A", "B");
        s.Combat!.OrdersSkipped = true;
        var snap = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(9));
        loaded.ApplySnapshot(snap);
        Assert.True(loaded.Combat!.OrdersSkipped);
    }

    // ---- D-053: BEG's leftover stays in the one purse -------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(500, 56, 4)] // DOSBox s65: 556 GP, 6 survivors → 4 left
    [InlineData(3, 2, 5)]    // a purse smaller than n stays whole
    public void Beg_Dos_LeftoverStaysInTheFoundGold_D053(long found, long carried, long left)
    {
        var s = WildernessFight(Dos(RuleOptionRegistry.CombatShares), "A", "B", "C", "D", "E", "F");
        s.Sack.Gold = found;
        s.Party.GoldCarried = carried;
        var r = s.ChoosePartyOption(PartyOption.BegMercy);
        Assert.Equal(PartyOptionOutcome.CombatOver, r.Outcome);
        Assert.Equal(left, s.Sack.Gold); // DOS [0x63f6] = found + withdrawn gold; Core's found gold is that purse's share
        Assert.Equal(0, s.Party.GoldCarried);
    }
}
