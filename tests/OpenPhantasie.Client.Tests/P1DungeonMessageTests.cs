using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s140: I Amiga's requester 23 in the dungeon (Amiga FORMAT.md "The dungeon's requester 23": h41's eleven calls to h44
/// <c>+0x466</c>) from the lines Core logs, and D-058's remaster-only flashes, which open no box (user s140: shown without a
/// wait, on the combat screen's line).
/// </summary>
public class P1DungeonMessageTests
{
    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;

    private static MapMessageLayout Layout() => Pack().MapMessage!;

    private static GameSession InDng1()
    {
        var session = ClientSessions.Overworld();
        session.EnterDungeon(1, 20, 0);
        Assert.Equal(GameMode.Dungeon, session.Mode);
        return session;
    }

    [OriginalDataFact]
    public void Pack_ListsTheDungeonsTexts_ApartFromTheOverworlds()
    {
        var l = Layout();
        Assert.Equal(13, l.DungeonKeys.Count); // eleven calls; +0x1ea's "%s %s" serves FAILS, SUCCEEDS and PICKS THE LOCK
        Assert.Empty(l.Keys.Intersect(l.DungeonKeys));
        Assert.DoesNotContain("core.encounter.a_day_encounter", l.Keys);    // D-058 (s140): no box
        Assert.DoesNotContain("core.encounter.an_encounter", l.DungeonKeys);
        Assert.Same(l.Keys, l.KeysFor(false));
        Assert.Same(l.DungeonKeys, l.KeysFor(true));
    }

    [OriginalDataFact]
    public void TheWall_IsABox_WithTheAmigasText_InTheDungeonOnly()
    {
        // h41 +0x1a6: Core logs DOS's `core.dungeon.that_is_the_wall`; the box prints the Amiga's h42 +0x5d "THAT IS A WALL" (s157).
        var l = Layout();
        Assert.Equal(["THAT IS A WALL"], P1MapMessageRun.Rows(l, "THAT IS THE WALL", dungeon: true));
        Assert.Null(P1MapMessageRun.Rows(l, "THAT IS THE WALL"));
        Assert.Null(P1MapMessageRun.Rows(l, "THAT IS A WALL", dungeon: true)); // the key is Core's text, not the Amiga's
        Assert.Equal(32, P1MapMessageRun.SoundFor(l, "THAT IS THE WALL", dungeon: true)); // the beep stays
    }

    [OriginalDataTheory]
    [InlineData("VILMAR FAILS")]                        // +0x1ea via +0xb66 (trap disarm, after sound 29)
    [InlineData("VILMAR SUCCEEDS")]                     // +0x1ea via +0xb7c
    [InlineData("VILMAR PICKS THE LOCK")]               // +0x1ea via +0xf12
    [InlineData("STAIRS FROM LEVEL 1  TO 2")]           // +0xe1a
    [InlineData("NO ONE CAN PICK THE LOCK")]            // +0xe68
    [InlineData("SMOKE FILLS THE AIR")]                 // op 1, +0x15ae
    [InlineData("A SPELL IS CAST")]                     // op 3, +0x1870
    [InlineData("THE PARTY GETS SOME GOLD")]            // op 7, +0x18d6
    [InlineData("SOME OF YOUR GOLD IS TAKEN")]          // op 9, +0x1914
    [InlineData("THE PARTY IS MOVED")]                  // op 12, +0x1976
    [InlineData("YOU HEAR A NOISE IN THE DISTANCE")]    // op 14, +0x1a10
    [InlineData("MONSTERS APPEAR")]                     // op 17, +0x1a6e
    public void EachDungeonText_IsAOneRowBox_InTheDungeonOnly(string line)
    {
        var l = Layout();
        Assert.Equal([line], P1MapMessageRun.Rows(l, line, dungeon: true));
        Assert.Null(P1MapMessageRun.Rows(l, line)); // h41 is the dungeon's: the overworld's step (h74) has none of these
    }

    [OriginalDataFact]
    public void TheOverworldsTexts_AreNoBox_InTheDungeon()
    {
        var l = Layout();
        Assert.Null(P1MapMessageRun.Rows(l, NightEncounter.Banner, dungeon: true));
        Assert.Null(P1MapMessageRun.Rows(l, WildernessEncounter.MonstersSurpriseYou, dungeon: true));
        // Other windows, not requester 23: the REPORT list (h71 +0x112e), the trap / lock menus (h86 +0x32a).
        Assert.Null(P1MapMessageRun.Rows(l, GameText.S("core.script.report"), dungeon: true));
        Assert.Null(P1MapMessageRun.Rows(l, GameText.F("core.dungeon.x_spots_a_trap", "VILMAR"), dungeon: true));
        Assert.Null(P1MapMessageRun.Rows(l, GameText.F("core.dungeon.x_hears_monsters_go_back", "VILMAR"), dungeon: true));
        Assert.Null(P1MapMessageRun.Rows(l, GameText.F("core.dungeon.x_avoids_the_trap", "VILMAR"), dungeon: true));
        Assert.Null(P1MapMessageRun.Rows(l, GameText.F("core.combat.x_takes_x_damage", "VILMAR", 3), dungeon: true));
    }

    [OriginalDataFact]
    public void NoDungeonMessage_LooksLikeABox()
    {
        // The wildcard keys ("{0} FAILS" …) must not catch a MESS text (message cells, event menus): DNG1-10, every line.
        var l = Layout();
        var texts = ClientSessions.Data.Dungeons
            .SelectMany(d => d.Messages.Values.SelectMany(m => m.Lines.Concat(m.Options)).Concat(d.EmbeddedTexts.Select(t => t.Text)))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        Assert.NotEmpty(texts);
        Assert.DoesNotContain(texts, t => P1MapMessageRun.Rows(l, t, dungeon: true) is not null);
    }

    [OriginalDataFact]
    public void AWallBump_IsABox()
    {
        var s = InDng1();
        var mark = s.LogSequence;
        Assert.Equal(DungeonMoveKind.BlockedWall, s.MoveDungeon(CardinalDirection.North).Kind); // off the level at (20, -1)
        var run = P1MapMessageRun.ForStep(Layout(), s, mark, 10, null)!;
        Assert.Equal(["THAT IS A WALL"], Assert.Single(run.Boxes).Rows);       // the Amiga's text (s157)
        Assert.Contains("THAT IS THE WALL", s.LogSince(mark));                  // Core's (DOS's) stays in the log
        Assert.Contains("THAT IS THE WALL", run.BoxedLines);                    // and leaves the message line
        Assert.Equal(1000, run.LengthMs); // ⌊(10 + 60) / 60⌋ s, keys swallowed
    }

    [OriginalDataFact]
    public void Stairs_AndAJammedLock_AreBoxes()
    {
        var s = InDng1();
        s.Dungeon!.Level!.Set(20, 1, 0xda);
        var mark = s.LogSequence;
        s.MoveDungeon(CardinalDirection.South);
        var stairs = Assert.Single(P1MapMessageRun.ForStep(Layout(), s, mark, 10, null)!.Boxes).Rows;
        Assert.Equal(["STAIRS FROM LEVEL 1  TO 2"], stairs);
        // h65 +0x1f4 centres by strlen: the Amiga's "…TO 2 " (26) and Core's trimmed 25 both get ⌊(40 − n) / 2⌋ = 7 cells.
        Assert.Equal((40 - 26) / 2, (40 - stairs[0].Length) / 2);

        s.Dungeon!.Level!.Set(20, 2, 0xc7); // jammed (kind 15): h41 +0xe68
        mark = s.LogSequence;
        s.MoveDungeon(CardinalDirection.South);
        Assert.Equal(["NO ONE CAN PICK THE LOCK"], Assert.Single(P1MapMessageRun.ForStep(Layout(), s, mark, 10, null)!.Boxes).Rows);
    }

    [OriginalDataFact]
    public void MonstersAppear_ThenTheFight_TheBoxIsTheScripts_NotTheFlash()
    {
        var s = InDng1();
        var mark = s.LogSequence;
        s.Note(GameText.S("core.script.monsters_appear")); // op 17's box (h41 +0x1a6e), then the fight (state 11)
        s.BeginDungeonCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
        Assert.Equal((GameMode.Combat, GameMode.Dungeon), (s.Mode, s.CombatReturnMode));
        var run = P1MapMessageRun.ForStep(Layout(), s, mark, 10, null)!;
        Assert.Equal(["MONSTERS APPEAR"], Assert.Single(run.Boxes).Rows);
        Assert.Contains(GameSession.EncounterBanner, s.LogSince(mark)); // D-058's "AN ENCOUNTER": logged, no box
    }

    [OriginalDataFact]
    public void ADayFight_PlansNoBox_SoTheFightStartsAtOnce()
    {
        // A plain day fight and a fixed / tile fight (both BeginWildernessCombat: "A DAY ENCOUNTER" only).
        var s = ClientSessions.Overworld();
        var mark = s.LogSequence;
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
        Assert.Contains(GameSession.DayEncounterBanner, s.LogSince(mark));
        Assert.Null(P1MapMessageRun.ForStep(Layout(), s, mark, 10, null));
        var dungeon = InDng1();
        mark = dungeon.LogSequence;
        dungeon.BeginDungeonCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
        Assert.Null(P1MapMessageRun.ForStep(Layout(), dungeon, mark, 10, null));
    }

    // --- s141: the boxes' sounds, the party under op 12's box, the report between two boxes -----------------------------

    [OriginalDataFact]
    public void Pack_NamesTheSoundBeforeEachBox()
    {
        var l = Layout();
        Assert.Equal(32, P1MapMessageRun.SoundFor(l, "THAT IS THE WALL", dungeon: true)); // h41 +0x19a: h89 +0xe0, the beep
        Assert.Equal(29, P1MapMessageRun.SoundFor(l, "VILMAR FAILS", dungeon: true));     // +0xb50
        Assert.Null(P1MapMessageRun.SoundFor(l, "VILMAR SUCCEEDS", dungeon: true));
        Assert.Equal(28, P1MapMessageRun.SoundFor(l, "A SPELL IS CAST", dungeon: true));  // +0x1862
        Assert.Equal(16, P1MapMessageRun.SoundFor(l, "THE PARTY IS MOVED", dungeon: true)); // +0x1968
        Assert.Equal(26, P1MapMessageRun.SoundFor(l, "YOU HEAR A NOISE IN THE DISTANCE", dungeon: true)); // +0x1a02
        Assert.Null(P1MapMessageRun.SoundFor(l, "SMOKE FILLS THE AIR", dungeon: true));
        Assert.Null(P1MapMessageRun.SoundFor(l, "MONSTERS APPEAR", dungeon: true));
        Assert.Null(P1MapMessageRun.SoundFor(l, NightEncounter.Banner)); // h74's boxes have none
        // 32 is h90 +0x10 itself (the default beep, as 1): the pack plays it.
        Assert.Equal(Pack().DosoundEffects!.Sounds[1], Pack().DosoundEffects!.Sounds[32]);
    }

    [OriginalDataFact]
    public void AWallBump_TheBeepFirst_ThenTheBox()
    {
        var s = InDng1();
        var mark = s.LogSequence;
        s.MoveDungeon(CardinalDirection.North);
        var box = Assert.Single(P1MapMessageRun.ForStep(Layout(), s, mark, 10, null, n => n == 32 ? 0.2 : 0)!.Boxes);
        Assert.Equal((32, 0.0, 200.0, 1200.0), (box.Sound, box.SoundMs, box.StartMs, box.EndMs)); // h89 blocks, then 1 s
        Assert.Null(box.PartyAt); // the party never left its cell
        // Sound switch off (user s129): no sound, no wait.
        var quiet = Assert.Single(P1MapMessageRun.ForStep(Layout(), s, mark, 10, null, _ => 0)!.Boxes);
        Assert.Equal((0.0, 1000.0), (quiet.StartMs, quiet.EndMs));
    }

    /// <summary>DNG2's robed men (record 23 on (6, 29)): BRIBE runs script 105, op 3 (A SPELL IS CAST and the damage), op 12.</summary>
    private static (GameSession Session, long Mark) BribeTheRobedMen()
    {
        var s = ClientSessions.Overworld();
        s.EnterDungeon(2, 6, 28);
        s.MoveDungeon(CardinalDirection.South);
        Assert.NotNull(s.DungeonEventPrompt);
        var mark = s.LogSequence;
        s.AnswerDungeonEvent(1);
        return (s, mark);
    }

    [OriginalDataFact]
    public void OpTwelve_TheBoxComesBeforeTheMove_AndTheReportBetweenTheBoxes()
    {
        var (s, mark) = BribeTheRobedMen();
        Assert.Equal((14, 0), (s.Dungeon!.X, s.Dungeon.Y)); // Core has moved the party already
        var run = P1MapMessageRun.ForStep(Layout(), s, mark, 10, null, n => n is 28 or 16 ? 0.1 : 0)!;
        // h41: sound 28, A SPELL IS CAST, then the report (requester 21, waits for EXIT) over the event cell.
        var spell = Assert.Single(run.Boxes);
        Assert.Equal(["A SPELL IS CAST"], spell.Rows);
        Assert.Equal(28, spell.Sound);
        Assert.Equal(new P1MapMessageRun.PartyCell(2, 6, 29), spell.PartyAt);
        Assert.Equal(GameText.S("core.script.report"), run.WaitLines[0]);
        Assert.True(run.HasNext);
        Assert.Equal(new P1MapMessageRun.PartyCell(2, 6, 29), run.PartyAt(run.LengthMs + 5000)); // the wait: still there
        // Then sound 16 and `core.script.the_party_is_moved`, still over the event cell; the move after it.
        var next = run.Next(null)!;
        var moved = Assert.Single(next.Boxes);
        Assert.Equal(["THE PARTY IS MOVED"], moved.Rows);
        Assert.Equal((16, 0.0, 100.0), (moved.Sound, moved.SoundMs, moved.StartMs));
        Assert.Equal(new P1MapMessageRun.PartyCell(2, 6, 29), moved.PartyAt);
        Assert.Null(next.PartyAt(next.LengthMs)); // the box closed: Core's cell (14, 0)
        Assert.False(next.HasNext);
        Assert.Empty(next.WaitLines);
        Assert.Contains("THE PARTY IS MOVED", run.BoxedLines);
    }

    [OriginalDataFact]
    public void AJammedLock_TheBoxOverTheLockCell_ThenTheStepBack()
    {
        var s = InDng1();
        s.Dungeon!.Level!.Set(20, 1, 0xc7); // jammed (kind 15): h41 +0xe68, then the step back
        var mark = s.LogSequence;
        s.MoveDungeon(CardinalDirection.South);
        Assert.Equal((20, 0), (s.Dungeon.X, s.Dungeon.Y));
        var box = Assert.Single(P1MapMessageRun.ForStep(Layout(), s, mark, 10, null)!.Boxes);
        Assert.Equal(new P1MapMessageRun.PartyCell(1, 20, 1), box.PartyAt);
        // A later step's boxes do not take this move (the client reads moves after its mark).
        var mark2 = s.LogSequence;
        s.MoveDungeon(CardinalDirection.North);
        Assert.Null(Assert.Single(P1MapMessageRun.ForStep(Layout(), s, mark2, 10, null)!.Boxes).PartyAt);
    }

    [OriginalDataFact]
    public void TheOverworldsBoxes_StayOnePart()
    {
        // Lines between two overworld boxes split nothing (h74's boxes follow each other at once).
        var run = P1MapMessageRun.Plan(Layout(), [NightEncounter.Banner, "SOMETHING ELSE", WildernessEncounter.MonstersSurpriseYou], 10, null)!;
        Assert.Equal(2, run.Boxes.Count);
        Assert.False(run.HasNext);
    }

    [Fact]
    public void DungeonSwitch_ReadsTheLevelAndCell()
    {
        Assert.Equal((2, 6, 28), TestParty.DungeonCell(["x", TestParty.DungeonSwitch, "2", "6", "28"]));
        Assert.Null(TestParty.DungeonCell(["x", TestParty.DungeonSwitch, "2", "6"]));
        Assert.Null(TestParty.DungeonCell(["x"]));
    }

    // --- D-058 on the combat screen's line (user s140: shown, no wait) ---------------------------------------------------

    private static CombatPresenter Presenter() => new(new ClientSettings())
    {
        P1Layout = Pack().CombatField, AmigaP1Field = () => true, P1SoundSeconds = n => n == 17 ? 1.1 : 0,
    };

    [OriginalDataFact]
    public void DayFight_TheFlashIsOnTheLine_UntilTheFirstRoundsLine()
    {
        var s = ClientSessions.Fight();
        var ui = Presenter();
        ui.Start(s);
        Assert.Equal(GameSession.DayEncounterBanner, ui.P1Banner);
        Assert.Equal(GameSession.DayEncounterBanner, ui.P1LineText);      // over the black and the set-up: no wait added
        ui.Update(s, 5000, fastForward: false);                         // the intro is over: Party Options
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        Assert.Equal(GameSession.DayEncounterBanner, ui.P1LineText);      // still there while the player chooses

        ui.HandleKey(s, Microsoft.Xna.Framework.Input.Keys.Down);       // Party Options → Fight
        ui.HandleKey(s, Microsoft.Xna.Framework.Input.Keys.Enter);
        ui.HandleKey(s, Microsoft.Xna.Framework.Input.Keys.F);          // FIGHT
        ui.P1Speed = () => 1;
        ui.Update(s, 0, fastForward: false);
        var run = ui.P1Run!;
        var first = run.Lines.First(l => !string.IsNullOrEmpty(l.Text));
        ui.Update(s, first.PrintMs + 1, fastForward: false);
        Assert.Null(ui.P1Banner);                                        // the round's first line takes the line
        Assert.NotEqual(GameSession.DayEncounterBanner, ui.P1LineText);
    }

    [OriginalDataFact]
    public void SurpriseOpening_TheFlashShows_OverTheBlackAndTheSetUp()
    {
        var s = ClientSessions.Overworld();
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.MonstersSurprised);
        var ui = Presenter();
        ui.Start(s);
        ui.Update(s, 1, fastForward: false);                            // round 1 at once
        Assert.True(ui.P1Shown!.Black);
        Assert.Equal(GameSession.DayEncounterBanner, ui.P1LineText);
        ui.Update(s, ui.P1Run!.LengthMs + 1, fastForward: false);
        Assert.Null(ui.P1Banner);                                        // gone with round 1
    }

    [OriginalDataFact]
    public void DungeonFight_FlashesAnEncounter_NightFightNone()
    {
        var dungeon = InDng1();
        dungeon.BeginDungeonCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
        var ui = Presenter();
        ui.Start(dungeon);
        Assert.Equal(GameSession.EncounterBanner, ui.P1Banner);

        // A night fight: the Amiga's own `core.encounter.a_night_encounter` box has shown it over the map (h74 +0x6c).
        Assert.Null(CombatPresenter.RemasterBanner([NightEncounter.Banner, "Combat vs 1 ORC", "A IS ON WATCH"]));
        Assert.Equal(GameSession.DayEncounterBanner,
            CombatPresenter.RemasterBanner([NightEncounter.Banner, "x", GameSession.DayEncounterBanner, "Combat vs 1 ORC"]));
        Assert.Null(CombatPresenter.RemasterBanner(["Combat vs 1 ORC"]));
    }
}
