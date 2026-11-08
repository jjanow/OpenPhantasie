using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>P0.8 endgame (endgame.md): DNG event scripts, runes, the Black Lord win, party defeat.</summary>
public partial class EndgameTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private sealed class Rng(params int[] values) : IDosRandomSource
    {
        private readonly Queue<int> _values = new(values);
        public int Remaining => _values.Count;
        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.Equal((0, 100), (minInclusive, maxExclusive));
            return _values.Dequeue();
        }
        public int NextDosWord() => _values.Dequeue();
    }

    private sealed class Host(params int[] choices) : IDungeonScriptHost
    {
        private readonly Queue<int> _choices = new(choices);
        public List<int> Shown { get; } = new();
        public long FoundGold { get; set; }
        public int ShowMessage(int messageId)
        {
            Shown.Add(messageId);
            return _choices.Count > 0 ? _choices.Dequeue() : 1;
        }
    }

    private static DungeonLevel Level(int n) =>
        Data.Dungeons.Single(d => d.Id == $"DNG{n}").ToRuntime().CloneAsBuffer();

    private static Character Hero(string name = "Hero", int level = 5, int luck = 10) => new()
    {
        Name = name, Class = CharacterClass.Fighter, Race = Race.Human, Status = CharacterStatus.Ok, Level = level,
        HpCur = 30, HpMax = 30, Stats = new CharacterStats(10, 10, 10, 10, 10, luck),
    };

    private static Party PartyOf(params Character[] members)
    {
        var p = new Party();
        p.Members.AddRange(members);
        return p;
    }

    private static byte EventFeature(int k) => (byte)(0xdc + k);

    // --- Messages / menus (OV2 0x2a95) ---------------------------------------------------

    // --- Runes ----------------------------------------------------------------------------

    [OriginalDataFact]
    public void Dng1_OldMan_TalkToHim_GivesTheAirRune_ToEveryLivingMember()
    {
        var level = Level(1);
        var a = Hero("A");
        var dead = Hero("Dead");
        dead.Status = CharacterStatus.Dead;
        var party = PartyOf(a, dead);
        var host = new Host(2); // TALK TO HIM
        var r = DungeonScript.Run(new DungeonState(), level, EventFeature(16), party, host, new Rng());

        Assert.Equal(DungeonScriptEnd.Done, r.End);
        Assert.Equal(1, a.Phantasie1.Runes[(int)Rune.Air]);
        Assert.Equal(0, dead.Phantasie1.Runes[(int)Rune.Air]);       // SET-MIN skips DEAD
        Assert.False(DungeonScript.IsSpent(level, EventFeature(16))); // r0 = 0x67 → repeatable
    }

    [OriginalDataFact]
    public void Dng2_BurnBox_NeedsFire_ElsePushBack()
    {
        // ev31 locked box → BURN BOX: TEST FIRE (45) else the box-will-not-burn line; SET-MIN EARTH (44).
        var level = Level(2);
        var feature = EventFeature(31);
        var rec = DungeonScript.EventRecordOffset(feature);
        var burn = Enumerable.Range(1, 4).First(c =>
        {
            var p = DungeonScript.ScriptBase + 2 * level.GetBufferByte(rec + c + 1);
            return level.GetBufferByte(p) % 128 % 64 == 19 && level.GetBufferByte(p + 1) == 45;
        });

        var hero = Hero();
        var state = new DungeonState { X = 5, Y = 5, PrevX = 4, PrevY = 5 };
        DungeonScript.Run(state, level, feature, PartyOf(hero), new Host(burn), new Rng());
        Assert.Equal(0, hero.Phantasie1.Runes[(int)Rune.Earth]);
        Assert.Equal((4, 5), (state.X, state.Y)); // 0x29a9 push back

        hero.Phantasie1.Runes[(int)Rune.Fire] = 1;
        var fresh = Level(2);
        DungeonScript.Run(new DungeonState(), fresh, feature, PartyOf(hero), new Host(burn), new Rng());
        Assert.Equal(1, hero.Phantasie1.Runes[(int)Rune.Earth]);
    }

    [OriginalDataFact]
    public void Zeus_PrayTooEarly_SpendsHim_PrayReady_GivesTheGodRune()
    {
        var feature = EventFeature(7);
        var early = Level(10);
        var hero = Hero();
        var host = new Host(1); // PRAY TO HIM
        DungeonScript.Run(new DungeonState(), early, feature, PartyOf(hero), host, new Rng());
        Assert.Equal(0, hero.Phantasie1.Runes[(int)Rune.God]);
        Assert.True(DungeonScript.IsSpent(early, feature)); // r0 = 0xc6: one-shot whatever happens
        Assert.Equal(80, host.Shown[^1]);                   // "YOU HAVE NOT PASSED THE SIX TESTS…"

        var ready = Level(10);
        hero.Score = 60;
        hero.Phantasie1.Runes[(int)Rune.Air] = hero.Phantasie1.Runes[(int)Rune.Earth] = hero.Phantasie1.Runes[(int)Rune.Water] = 1;
        DungeonScript.Run(new DungeonState(), ready, feature, PartyOf(hero), new Host(1), new Rng());
        Assert.Equal(1, hero.Phantasie1.Runes[(int)Rune.God]);
    }

    [OriginalDataFact]
    public void Dng9_GodRuneGate_IsRepeatable_AndOpensTheWay()
    {
        var level = Level(9);
        var feature = EventFeature(16);
        var hero = Hero();
        var state = new DungeonState { X = 15, Y = 2, PrevX = 15, PrevY = 1 };
        DungeonScript.Run(state, level, feature, PartyOf(hero), new Host(), new Rng());
        Assert.Equal((15, 1), (state.X, state.Y));
        Assert.False(DungeonScript.IsSpent(level, feature));

        hero.Phantasie1.Runes[(int)Rune.God] = 1;
        state = new DungeonState { X = 15, Y = 2, PrevX = 15, PrevY = 1 };
        DungeonScript.Run(state, level, feature, PartyOf(hero), new Host(), new Rng());
        Assert.Equal(0x08, level.Get(15, 2));
        Assert.Equal((15, 2), (state.X, state.Y));
    }

    [OriginalDataFact]
    public void Dng9_FinalFight_IsTheBlackLord_MonstersAppear_OneShot()
    {
        var level = Level(9);
        var feature = EventFeature(11);
        var r = DungeonScript.Run(new DungeonState(), level, feature, PartyOf(Hero()), new Host(), new Rng());

        Assert.Equal(DungeonScriptEnd.Fight, r.End);
        Assert.Equal(Endgame.BlackLordId, r.MonsterId);
        Assert.Equal(SurpriseFlags.PartySurprised, r.Surprise);
        Assert.Equal(0, level.Get(29, 9));                    // re-sealed
        Assert.True(DungeonScript.IsSpent(level, feature));   // escape → no second fight this load
    }

    // --- Op semantics on a hand-built buffer ----------------------------------------------

    private static DungeonLevel ScriptLevel(byte r0, params byte[] script)
    {
        var buf = new byte[DungeonLevel.BufferSize];
        var rec = DungeonScript.EventRecordOffset(0xdd);
        buf[rec] = r0;
        buf[rec + 2] = 10;                                   // choice 1 → script 10
        script.CopyTo(buf, DungeonScript.ScriptBase + 20);
        return DungeonLevel.FromBuffer(99, "TEST", buf);
    }

    [Fact]
    public void DamageOp_FloorsAtOne_EvenWhenTheReportSaysUnhurt()
    {
        var level = ScriptLevel(0x01, 0x02, 50);             // op 2, a = 50
        var hero = Hero(luck: 13);
        var r = DungeonScript.Run(new DungeonState(), level, 0xdd, PartyOf(hero), new Host(), new Rng(0));
        Assert.Equal(29, hero.HpCur);                        // dmg = 0·50/130 = 0 → 0x124a floors to 1
        Assert.Contains("Hero", r.Log);
        Assert.Equal(DungeonScriptEnd.Done, r.End);
    }

    [OriginalDataFact]
    public void DamageOp_ReportLine_PadsLikeDos_WithASpaceBeforeDamage()
    {
        // s154: ops 1–4 print "%-10.9sTAKES %-3d DAMAGE" (OV2 0x7764; s56 filmed "fighto    TAKES 2   DAMAGE"), one column
        // right of the trap's "%-10.9sTAKES %-3dDAMAGE" (no space, 0x78f7).
        var level = ScriptLevel(0x01, 0x02, 50);             // op 2, a = 50
        var hero = Hero(luck: 10);
        var r = DungeonScript.Run(new DungeonState(), level, 0xdd, PartyOf(hero), new Host(), new Rng(4));
        Assert.Contains("Hero      TAKES 2   DAMAGE", r.Log);  // 4·50/100 = 2
    }

    [OriginalDataFact]
    public void ASession_RecordsAJammedLocksStepBack_AfterItsLine()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        Assert.True(session.TryAddToParty(session.CreateAndEnroll("Fighto", Race.Human, CharacterClass.Fighter), out _));
        session.DepartGuildToWilderness();
        session.EnterDungeon(1, 20, 0);
        session.Dungeon!.Level!.Set(20, 1, 0xc7);            // jammed: `core.dungeon.no_one_can_pick_the_lock`, then the step back
        var moves = session.DungeonMoveSequence;
        session.MoveDungeon(CardinalDirection.South);
        var move = Assert.Single(session.DungeonMovesSince(moves));
        Assert.Equal((session.LogSequence, 1, 20, 1), (move.AfterLine, move.LevelId, move.FromX, move.FromY));
        Assert.Equal((20, 0), (session.Dungeon.X, session.Dungeon.Y));
    }

    [Fact]
    public void DamageOp_KillingEveryone_IsPartyDefeat_WithConLoss()
    {
        var level = ScriptLevel(0x01, 0x82, 255, 0x0b, 5);   // op 2 (continue) then show message 5
        var hero = Hero(luck: 10);
        hero.HpCur = 5;
        var host = new Host();
        var r = DungeonScript.Run(new DungeonState(), level, 0xdd, PartyOf(hero), host, new Rng(99));
        Assert.Equal(DungeonScriptEnd.PartyDefeated, r.End); // 99·255/100 = 252 dmg
        Assert.Equal(CharacterStatus.Dead, hero.Status);
        Assert.Equal(8, hero.Stats.Con);                     // 0x12cf: LUCK 10 < 11 → −2
        Assert.DoesNotContain(5, host.Shown);                // the script stopped
    }

    [Fact]
    public void GoldOps_AddWithClampAndLowByte_TakeWithFloor()
    {
        var host = new Host { FoundGold = 100 };
        DungeonScript.Run(new DungeonState(), ScriptLevel(0x01, 0x07, 2), 0xdd, PartyOf(Hero()), host, new Rng());
        Assert.Equal((100 + 512) | 0xff, host.FoundGold);
        host.FoundGold = 0xff00;
        DungeonScript.Run(new DungeonState(), ScriptLevel(0x01, 0x08, 200), 0xdd, PartyOf(Hero()), host, new Rng());
        Assert.Equal(0xffff, host.FoundGold);
        host.FoundGold = 300;
        DungeonScript.Run(new DungeonState(), ScriptLevel(0x01, 0x0a, 2), 0xdd, PartyOf(Hero()), host, new Rng());
        Assert.Equal(0, host.FoundGold);                     // 300 < 500 → 0
    }

    [Fact]
    public void OneShotOps_AreRewritten_AndTeleportSetsThePreviousCell()
    {
        // 0xcc = continue + one-shot + op 12 (teleport to 4,30), then a one-shot give (0x46 = op 6, last).
        var level = ScriptLevel(0x01, 0xcc, 4, 30, 0, 0x46, 77);
        var state = new DungeonState { X = 1, Y = 1, PrevX = 1, PrevY = 0 };
        var host = new Host();
        var r = DungeonScript.Run(state, level, 0xdd, PartyOf(Hero()), host, new Rng());

        Assert.Equal((4, 30, 4, 30), (state.X, state.Y, state.PrevX, state.PrevY));
        Assert.Equal((DungeonScriptEnd.OfferItem, 77), (r.End, r.ItemId));
        var p = DungeonScript.ScriptBase + 20;
        Assert.Equal(0x80, level.GetBufferByte(p));          // one-shot with continue → 0x80
        Assert.Equal(0x00, level.GetBufferByte(p + 4));      // last one-shot → 0

        // Second visit: the spent 4-byte op is now a 2-byte no-op (0x80), so its old b byte (30) is read as the next
        // op byte — op 30 is a no-op without continue, and the script stops there [C] quirk.
        Assert.Equal(DungeonScriptEnd.Done, DungeonScript.Resume(state, level, PartyOf(Hero()), host, new Rng(), r.Resume!).End);
        state = new DungeonState { X = 1, Y = 1 };
        r = DungeonScript.Run(state, level, 0xdd, PartyOf(Hero()), host, new Rng());
        Assert.Equal(DungeonScriptEnd.Done, r.End);
        Assert.Equal((1, 1), (state.X, state.Y));
    }

    [Fact]
    public void Give_StopsTheScript_TheLaterOpsRunOnResume_s58()
    {
        // DOS (s58 DNG9 wand): op 6 asks KEEP IT? at once; the message op after it shows only after the answer.
        // 0x86 = continue + op 6 (give 77), 0x8b = continue + op 11 (message 9), 0x11 = op 17 fight monster 3 (last).
        var level = ScriptLevel(0x01, 0x86, 77, 0x8b, 9, 0x11, 3);
        var host = new Host();
        var state = new DungeonState { X = 1, Y = 1 };
        var r = DungeonScript.Run(state, level, 0xdd, PartyOf(Hero()), host, new Rng());

        Assert.Equal((DungeonScriptEnd.OfferItem, 77), (r.End, r.ItemId));
        Assert.Equal(new[] { 1 }, host.Shown);               // only the event message so far
        var after = DungeonScript.Resume(state, level, PartyOf(Hero()), host, new Rng(), r.Resume!);
        Assert.Equal(new[] { 1, 9 }, host.Shown);
        Assert.Equal((DungeonScriptEnd.Fight, 3), (after.End, after.MonsterId));
    }

    [Fact]
    public void Teleport_ForgetsTheLastMessage_SetCellDoesNot_s49()
    {
        // 0x27b0..0x27fa: ops 12/13 end with [0x64f2], [0x64f4] := 0. Ops 14/15 (0x2807/0x2817) only write the cell.
        foreach (var op in new byte[] { 12, 13 })
        {
            var state = new DungeonState { X = 1, Y = 1, LastMessageId = 40 };
            DungeonScript.Run(state, ScriptLevel(0x01, op, 4, 30, 0), 0xdd, PartyOf(Hero()), new Host(), new Rng());
            Assert.Equal(0, state.LastMessageId);
        }
        foreach (var op in new byte[] { 14, 15 })
        {
            var state = new DungeonState { X = 1, Y = 1, LastMessageId = 40 };
            var level = ScriptLevel(0x01, op, 3, 2, 0x8b);
            DungeonScript.Run(state, level, 0xdd, PartyOf(Hero()), new Host(), new Rng());
            Assert.Equal(40, state.LastMessageId);
            Assert.Equal(0x8b, level.Get(3, 2));
        }
    }

    [Fact]
    public void Record_StrictlyAbove0x80_IsOneShot()
    {
        var at80 = ScriptLevel(0x80, 0x00, 0);
        DungeonScript.Run(new DungeonState(), at80, 0xdd, PartyOf(Hero()), new Host(), new Rng());
        Assert.False(DungeonScript.IsSpent(at80, 0xdd));
        var above = ScriptLevel(0x81, 0x00, 0);
        DungeonScript.Run(new DungeonState(), above, 0xdd, PartyOf(Hero()), new Host(), new Rng());
        Assert.True(DungeonScript.IsSpent(above, 0xdd));
        var r = DungeonScript.Run(new DungeonState(), above, 0xdd, PartyOf(Hero()), new Host(), new Rng());
        Assert.True(r.WasSpent);
    }

    // --- Party defeat (OV1 0x3bc, D-025) --------------------------------------------------

    [Theory]
    [InlineData(1, new[] { 0x2aaa }, DefeatFate.Undead)]          // L1: one draw, rand/0x2aab = 0 < 1
    [InlineData(1, new[] { 0x2aab }, DefeatFate.Destroyed)]
    [InlineData(3, new[] { 0x1fff }, DefeatFate.Resurrected)]     // rand/0x2000 = 0 < 1
    [InlineData(3, new[] { 0x2000, 0x7fff }, DefeatFate.Undead)]  // 2nd draw /0x2aab = 2 < 3
    [InlineData(6, new[] { 0x7fff }, DefeatFate.Resurrected)]     // L ≥ 6: always (3 < 4)
    public void Judge_DrawsInDosOrder(int level, int[] draws, DefeatFate expected)
    {
        var rng = new Rng(draws);
        var m = Hero(level: level);
        Assert.Equal(expected, PartyDefeat.Judge(m, rng));
        Assert.Equal(0, rng.Remaining);
    }

    [Fact]
    public void Judge_ConZero_IsDestroyed_WithoutADraw()
    {
        var m = Hero(level: 10);
        m.Stats = m.Stats with { Con = 0 };
        Assert.Equal(DefeatFate.Destroyed, PartyDefeat.Judge(m, new Rng()));
    }

    [Fact]
    public void Resolve_AppliesEachFate_AndClearsTheLoot()
    {
        var roster = new GuildRoster();
        var res = Hero("Res", level: 6);
        var und = Hero("Und", level: 2);
        var gone = Hero("Gone", level: 1);
        foreach (var m in new[] { res, und, gone })
        {
            m.Status = CharacterStatus.Dead;
            m.HpCur = 0;
        }
        res.GoldInBank = 0x1234;
        roster.Slots[0] = res;
        roster.Slots[1] = und;
        roster.Slots[2] = gone;
        var party = PartyOf(res, und, gone);
        party.Elemental = Hero("Elem");
        var sack = new LootSack { Gold = 500, Experience = 900 };
        sack.ItemIds.Add(5);

        var result = PartyDefeat.Resolve(party, roster, sack, new Rng(0, 0, 0x7fff));

        Assert.Equal(CharacterStatus.Ok, res.Status);
        Assert.Equal(30, res.HpCur);
        Assert.Equal(0x34, res.GoldInBank);                  // @262 &= 0xff
        Assert.Equal(Race.Undead, und.Race);
        Assert.Equal(Race.Human, und.BirthRace == 0 ? Race.Human : und.BirthRace);
        Assert.Equal(20, und.Level);
        Assert.Equal(15, und.WeaponDamageB);
        Assert.DoesNotContain(gone, party.Members);
        Assert.Null(roster.Slots[2]);
        Assert.Null(party.Elemental);
        Assert.True(result.AnyRevived);
        Assert.Equal((0, 0L, 0L), (sack.ItemIds.Count, sack.Gold, sack.Experience));
    }

    // --- The win (state 7) ----------------------------------------------------------------

    private static GameSession SessionFacingTheBlackLord(out Character hero)
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(9));
        session.OpenGuild();
        hero = session.CreateAndEnroll("Victor", Race.Human, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.EnterDungeon(9, 15, 0);
        session.BeginDungeonCombat(Endgame.BlackLordId, SurpriseFlags.Normal);
        return session;
    }

    [OriginalDataFact]
    public void BlackLordKilled_IsVictory_DivineI_AfterTheGuildWrite_PartyToTown1()
    {
        var session = SessionFacingTheBlackLord(out var hero);
        foreach (var m in session.Combat!.AllMonsters)
            m.HitPoints = 0;
        session.ResolveCombatRound();

        Assert.Equal(EndgameOutcome.Victory, session.LastEndgame);
        Assert.Equal(1, hero.SpellsKnown[SpellId.DivineI]);
        var written = session.VictoryGuildWrite!.Single(c => c?.Name == "Victor")!;
        Assert.Equal(0, written.SpellsKnown[SpellId.DivineI]); // quirk: GUILD.DAT written before DIVINE I
        var slew = new Deed(Deed.Slew, new ContentId(GameId.P1, Endgame.BlackLordId));
        Assert.Equal([slew], hero.Deeds!);    // remaster deeds record (s72)
        Assert.Equal([slew], written.Deeds!); // kept in the saved roster
        Assert.Equal(GameMode.Town, session.Mode);
        Assert.Equal(1, session.CurrentTownId);
        Assert.Null(session.Dungeon);
    }

    [OriginalDataFact]
    public void Victory_WaitsOnZeusScreen_SlotsMarkWhoGetsDivineI()
    {
        // OV1 0x4d5: Zeus' speech, then a key (VictoryScene prompt); the Amiga grants and draws only status-OK slots.
        var session = SessionFacingTheBlackLord(out var hero);
        var sleeper = session.CreateAndEnroll("Nap", Race.Elf, CharacterClass.Priest);
        Assert.True(session.TryAddToParty(sleeper, out _));
        foreach (var m in session.Combat!.AllMonsters)
            m.HitPoints = 0;
        sleeper.Status = CharacterStatus.Dead;
        sleeper.HpCur = 0;
        session.ResolveCombatRound();

        var scene = Assert.IsType<VictoryScene>(session.PendingVictoryScene);
        Assert.Equal([new VictorySlot(Race.Human, true), new VictorySlot(Race.Elf, false)], scene.Slots);
        Assert.Equal(Endgame.ZeusSpeech, scene.SpeechLines);
        Assert.Equal(0, sleeper.SpellsKnown[SpellId.DivineI]);
        var prompts = new SessionPrompts(session);
        Assert.Equal(SessionPromptKind.VictoryScene, prompts.Current!.Kind);
        Assert.Equal(Endgame.ZeusSpeech, prompts.Current.Lines);
        prompts.Answer(1);
        Assert.Null(session.PendingVictoryScene);
        Assert.Null(prompts.Current);
    }

    private sealed class Rolls(params int[] values) : IRandomSource
    {
        private readonly Queue<int> _values = new(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [OriginalDataFact]
    public void BlackLord_FearThreeTimes_ThenDissolve_IsVictory()
    {
        var session = SessionFacingTheBlackLord(out var hero);
        var enc = session.Combat!;
        hero.Level = 15;
        for (var i = 0; i < 3; i++)
            Assert.True(CombatSpells.Fear(enc, hero, new Rolls(0), new List<string>()));
        Assert.True(CombatSpells.CastDissolve(enc, new Rolls(80), new List<string>()));
        Assert.Equal(MoraleOutcome.EndedBySpell, session.StartCombatRound());
        Assert.Equal(EndgameOutcome.Victory, session.LastEndgame);
    }

    [OriginalDataFact]
    public void BlackLord_Teleportation_IsNotAWin()
    {
        var session = SessionFacingTheBlackLord(out _);
        session.Combat!.SpellEnd = SpellCombatEnd.Teleported;
        Assert.Equal(MoraleOutcome.EndedBySpell, session.StartCombatRound());
        Assert.Null(session.LastEndgame);
    }

    [OriginalDataFact]
    public void MutualKill_IsADefeat_NotAWin()
    {
        var enc = SessionFacingTheBlackLord(out _).Combat!;
        foreach (var m in enc.AllMonsters)
            m.HitPoints = 0;
        Assert.False(Endgame.IsBlackLordKilled(enc, new CombatResult { PartyWiped = true, EncounterOver = true }));
        Assert.True(Endgame.IsBlackLordKilled(enc, new CombatResult { PartyWon = true, EncounterOver = true }));
    }

    [OriginalDataFact]
    public void Session_Wipe_PendsDefeat_ThenTheDemonJudges()
    {
        var session = SessionFacingTheBlackLord(out var hero);
        hero.Level = 6;
        hero.HpCur = 0;
        hero.Status = CharacterStatus.Dead;
        session.ResolveCombatRound();
        Assert.True(session.PartyDefeatPending);
        Assert.Null(session.LastEndgame);

        session.TravelTime = 5000;
        session.Overworld.TileClassFightDone = true;
        var result = session.ResolvePartyDefeat();
        Assert.False(session.Overworld.TileClassFightDone); // OV1 state 6 exits clear [0x6496] (s39)
        Assert.Equal(DefeatFate.Resurrected, result.Fates.Single().Fate); // L6: always
        Assert.Equal(0, session.TravelTime); // OV1 0x467 zeroes [0x63f2]
        Assert.Equal(EndgameOutcome.PartyDefeat, session.LastEndgame);
        Assert.Equal(GameMode.Overworld, session.Mode);
        Assert.Equal((1, 20, 5), (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y));
    }

    [OriginalDataFact]
    public void PendingDefeat_SurvivesASave()
    {
        var session = SessionFacingTheBlackLord(out var hero);
        hero.HpCur = 0;
        hero.Status = CharacterStatus.Dead;
        session.ResolveCombatRound();
        Assert.True(session.PartyDefeatPending);
        var copy = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        copy.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot())));
        Assert.True(copy.PartyDefeatPending);
    }

    [OriginalDataFact]
    public void Session_Wipe_NobodyRevived_RunsTheFullTownEntry()
    {
        var session = SessionFacingTheBlackLord(out var hero);
        hero.Stats = hero.Stats with { Con = 0 }; // CON 0 → always DESTROYED
        hero.HpCur = 0;
        hero.Status = CharacterStatus.Dead;
        session.ResolveCombatRound();
        var result = session.ResolvePartyDefeat();

        Assert.False(result.AnyRevived);
        Assert.Equal(GameMode.Town, session.Mode);
        Assert.Equal(1, session.CurrentTownId);
        Assert.Contains("Entered town #1.", session.Log); // s41: state 1 → 0x1e15(0) → 0x1954 (aging + restock)
    }

    [OriginalDataFact]
    public void Session_ScriptedEventCell_RunsTheScript()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(2));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Walker", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.EnterDungeon(9, 15, 0);

        session.Dungeon!.X = 15;
        session.Dungeon.Y = 1;
        var move = session.MoveDungeon(CardinalDirection.South);   // (15,2) = DNG9 ev16, the GOD-rune gate
        Assert.Equal(DungeonMoveKind.ScriptedEvent, move.Kind);
        Assert.Null(move.Message);                                 // s49: 0x24b2 prints only via 0x2a95 (no embedded text)
        Assert.DoesNotContain(session.Log, l => l.Contains("ENDING SEQUENCE", StringComparison.Ordinal));
        Assert.Null(session.DungeonEventPrompt);                   // message 1 is empty → no menu
        Assert.Equal((15, 1), (session.Dungeon.X, session.Dungeon.Y)); // TEST GOD failed → pushed back
    }
}
