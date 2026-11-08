using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s140: I Amiga's requester 23 in the dungeon (Amiga FORMAT.md "The dungeon's requester 23": h41's eleven calls to h44
/// <c>+0x466</c>) from the lines Core logs, and D-058's remaster-only flashes, which open no box (user s140: shown without a
/// wait, on the combat screen's line).
/// </summary>
public partial class P1DungeonMessageTests
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
