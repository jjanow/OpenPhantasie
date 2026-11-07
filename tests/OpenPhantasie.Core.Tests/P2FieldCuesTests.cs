using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s139: the moments outside combat the II ST sounds (<see cref="FieldCues"/>, <see cref="GameSession.Cue"/>; FORMAT.md 7.4):
/// the entry questions (19), the dungeon wall (the default script), a failed disarm (29), event ops 3 / 12 / 14 (28 / 16 /
/// 26, ops 13 / 15 silent), TRANSPORTATION (22) and the casts that end in a raise or the town's "D O N E !". Core notes the
/// moment only; the sound numbers are the pack's (<c>field_sounds</c>).
/// </summary>
public class P2FieldCuesTests
{
    private static readonly GameDataRepository Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));

    private static GameSession WithParty(int seed = 7)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        s.StartNewGame();
        s.OpenGuild();
        var classes = new[] { CharacterClass.Fighter, CharacterClass.Priest, CharacterClass.Wizard, CharacterClass.Thief };
        var races = new[] { Race.Human, Race.Dwarf, Race.Elf, Race.Halfling };
        for (var i = 0; i < classes.Length; i++)
        {
            var c = s.CreateAndEnroll("HERO" + i, races[i], classes[i]);
            Assert.True(s.TryAddToParty(c, out var err), err);
        }
        s.LeaveGuild();
        return s;
    }

    /// <summary>Dungeon 1 of II with the party on (<paramref name="x"/>, <paramref name="y"/>).</summary>
    private static GameSession InDungeon(int x, int y, int seed = 7)
    {
        var s = WithParty(seed);
        Assert.True(s.LeaveTown());
        s.EnterDungeon(1, x, y);
        return s;
    }

    [OriginalDataFact]
    public void Cue_KeepsTheLastSixteen_InOrder()
    {
        var s = WithParty();
        var mark = s.CueSequence;
        for (var i = 0; i < 20; i++)
            s.Cue("m" + i);
        Assert.Equal(mark + 20, s.CueSequence);
        Assert.Equal(Enumerable.Range(4, 16).Select(i => "m" + i), s.CuesSince(mark));
        Assert.Equal(["m19"], s.CuesSince(s.CueSequence - 1));
        Assert.Empty(s.CuesSince(s.CueSequence));
    }

    [OriginalDataFact]
    public void Wall_CellZero_AndOffTheMap_BothCueTheWall()
    {
        var s = InDungeon(5, 5);
        s.Dungeon!.Level!.Set(5, 6, 0);
        var mark = s.CueSequence;
        Assert.Equal(DungeonMoveKind.BlockedWall, s.MoveDungeon(CardinalDirection.South).Kind);
        Assert.Equal([FieldCues.DungeonWall], s.CuesSince(mark));

        var edge = InDungeon(0, 5); // ST 0x1089c: x < 0 branches to the same 0x1046c
        mark = edge.CueSequence;
        Assert.Equal(DungeonMoveKind.BlockedWall, edge.MoveDungeon(CardinalDirection.West).Kind);
        Assert.Equal([FieldCues.DungeonWall], edge.CuesSince(mark));
    }

    /// <summary>Event cell 0xdd south of the party, running <paramref name="script"/> as script 0 (a message the level lacks:
    /// no text, choice 1).</summary>
    private static IReadOnlyList<string> RunScript(params byte[] script)
    {
        var s = InDungeon(5, 5);
        var level = s.Dungeon!.Level!;
        var message = Enumerable.Range(1, 120).First(m => !level.Messages.ContainsKey(m));
        level.Set(5, 6, 0xdd);
        var rec = DungeonScript.EventRecordOffset(0xdd);
        level.SetBufferByte(rec, (byte)message);
        level.SetBufferByte(rec + 2, 0); // choice 1 → script 0
        for (var i = 0; i < script.Length; i++)
            level.SetBufferByte(DungeonScript.ScriptBase + i, script[i]);
        var mark = s.CueSequence;
        Assert.Equal(DungeonMoveKind.ScriptedEvent, s.MoveDungeon(CardinalDirection.South).Kind);
        return s.CuesSince(mark);
    }

    private const byte More = 0x80; // the continue bit

    [OriginalDataTheory]
    [InlineData(new byte[] { 3, 0 }, new[] { FieldCues.EventSpellCast })]
    [InlineData(new byte[] { 12, 5, 6, 0 }, new[] { FieldCues.EventPartyMoved })]
    [InlineData(new byte[] { 13, 5, 6, 0 }, new string[0])]
    [InlineData(new byte[] { 14, 5, 7, 7 }, new[] { FieldCues.EventNoise })]
    [InlineData(new byte[] { 15, 5, 7, 7 }, new string[0])]
    [InlineData(new byte[] { 1, 0 }, new string[0])] // "SMOKE FILLS THE AIR" 0x11aa0: silent
    [InlineData(new byte[] { 7, 1 }, new string[0])] // the gold ops are silent on the ST (III sounds them)
    [InlineData(new byte[] { 9, 0 }, new string[0])]
    [InlineData(new byte[] { 3 | More, 0, 14 | More, 5, 7, 7, 12, 5, 6, 0 },
        new[] { FieldCues.EventSpellCast, FieldCues.EventNoise, FieldCues.EventPartyMoved })]
    public void EventOps_CueTheirMoment(byte[] script, string[] expected) =>
        Assert.Equal(expected, RunScript(script));

    [OriginalDataFact]
    public void Disarm_ACueOnlyWhenTheRollFails()
    {
        bool sawFail = false, sawSuccess = false;
        for (var seed = 1; seed < 400 && !(sawFail && sawSuccess); seed++)
        {
            var s = InDungeon(5, 5, seed);
            s.Dungeon!.Level!.Set(5, 6, 3); // a trap cell (2..6): spotted or sprung
            s.MoveDungeon(CardinalDirection.South);
            if (s.DungeonTrapPrompt is not { Disarmers.Count: > 0 } trap)
                continue;
            var mark = s.CueSequence;
            var r = s.AnswerDungeonTrap(trap.Disarmers[0]);
            if (r.DisarmFailed)
            {
                sawFail = true;
                Assert.Equal([FieldCues.DisarmFails], s.CuesSince(mark));
                Assert.Contains(r.Log, l => l.Contains("FAILS"));
            }
            else
            {
                sawSuccess = true;
                Assert.Empty(s.CuesSince(mark));
            }
        }
        Assert.True(sawFail && sawSuccess);
    }

    [OriginalDataFact]
    public void TownGate_CuesTheTownPrompt()
    {
        var dirs = new[] { CardinalDirection.North, CardinalDirection.South, CardinalDirection.East, CardinalDirection.West };
        var back = new[] { CardinalDirection.South, CardinalDirection.North, CardinalDirection.West, CardinalDirection.East };
        for (var i = 0; i < dirs.Length; i++)
        {
            var s = WithParty();
            Assert.True(s.LeaveTown()); // on the town's exit; one step off and back is the town tile again
            if (s.MoveOverworld(dirs[i]).Kind != OverworldMoveKind.Moved || s.Mode != GameMode.Overworld)
                continue;
            var mark = s.CueSequence;
            if (s.MoveOverworld(back[i]).Kind != OverworldMoveKind.EnterTown)
                continue;
            Assert.Equal([FieldCues.TownPrompt], s.CuesSince(mark));
            return;
        }
        Assert.Fail("No step off the town's exit and back onto the town.");
    }

    [OriginalDataFact]
    public void DungeonGate_CuesTheDungeonPrompt()
    {
        var s = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(3));
        s.OpenGuild();
        var hero = s.CreateAndEnroll("PROMPT", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(hero, out _));
        s.DepartGuildToWilderness();
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (1, 8, 8); // west of OUT1 (9,8) = DNG1
        var mark = s.CueSequence;
        Assert.Equal(OverworldMoveKind.EnterDungeon, s.MoveOverworld(CardinalDirection.East).Kind);
        Assert.Equal([FieldCues.DungeonPrompt], s.CuesSince(mark));
    }

    [OriginalDataFact]
    public void Transport_APick_Cues_ACancelDoesNot()
    {
        var s = WithParty();
        var caster = s.Party.Members[2];
        caster.SpellsKnown[SpellId.Transportation] = 1;
        caster.MagicCur = caster.MagicMax = caster.MpCeiling = 40;
        Assert.True(s.CastSpell(caster, SpellId.Transportation).Success);
        var mark = s.CueSequence;
        Assert.True(s.ChooseTransportDestination(null));
        Assert.Empty(s.CuesSince(mark));

        Assert.True(s.CastSpell(caster, SpellId.Transportation).Success);
        var to = s.TransportDestinations.First();
        Assert.True(s.ChooseTransportDestination(to));
        Assert.Equal([FieldCues.Transport], s.CuesSince(mark));
        Assert.Equal(to, s.CurrentTownId);
    }

    [OriginalDataFact]
    public void TownHealing_OnALivingMember_CuesDone()
    {
        var s = WithParty();
        var priest = s.Party.Members[1];
        priest.SpellsKnown[SpellId.HealingI] = 1;
        priest.MagicCur = priest.MagicMax = priest.MpCeiling = 20;
        var mark = s.CueSequence;
        Assert.True(s.CastSpell(priest, SpellId.HealingI, s.Party.Members[0]).Success);
        Assert.Equal([FieldCues.TownCastDone], s.CuesSince(mark));
    }

    [Theory]
    [InlineData(SpellId.Resurrection, CastLocation.Field, true, "HERO0 IS RESURRECTED", FieldCues.FieldResurrected)]
    [InlineData(SpellId.Resurrection, CastLocation.Dungeon, true, "HERO0 IS RESURRECTED", FieldCues.FieldResurrected)]
    [InlineData(SpellId.Resurrection, CastLocation.Town, true, "HERO0 IS RESURRECTED", FieldCues.TownCastDone)]
    [InlineData(SpellId.Resurrection, CastLocation.Field, true, "", null)] // no one raised: nothing shows
    [InlineData(SpellId.HealingIi, CastLocation.Town, true, "", FieldCues.TownCastDone)]
    [InlineData(SpellId.HealingIi, CastLocation.Town, false, "", null)] // the ST skips a dead pick (0xe50e)
    [InlineData(SpellId.HealingIi, CastLocation.Field, true, "", null)] // the field heal has no sound
    public void CastCue_PerSpellAndPlace(int spell, CastLocation where, bool alive, string message, string? expected) =>
        Assert.Equal(expected, GameSession.CastCue(MagicResult.Ok(message), spell, where, alive));

    [Fact]
    public void CastCue_AFailedCast_HasNone() =>
        Assert.Null(GameSession.CastCue(MagicResult.Fail("NOT ENOUGH MAGIC"), SpellId.Resurrection, CastLocation.Field, true));
}
