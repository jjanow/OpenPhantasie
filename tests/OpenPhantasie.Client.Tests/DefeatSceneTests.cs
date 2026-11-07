using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// Phase 09 T3 (s95): Phantasie II's defeat scene as the ST draws it (FORMAT.md §10.3/§10.4, <c>0x12bea</c>): the door
/// sinking into the floor, the ghost-portrait row and each member's fate, and the mark on a member dying in combat.
/// </summary>
public class DefeatSceneTests
{
    private static readonly GraphicsPackCatalog P2Packs = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2));

    [OriginalDataFact]
    public void StPack_HasTheGhostPortrait_AndTheDeathMark()
    {
        var st = P2Packs.Find("atari-st")!;
        Assert.EndsWith("ui/empty-portrait.png", st.Resolve(PictureNames.PortraitGhost)!.Replace('\\', '/'));
        Assert.EndsWith("ui/ghost-mark.png", st.Resolve(PictureNames.PortraitDeathMark)!.Replace('\\', '/'));
        // I's Amiga pack has its own ghost (h69 +0x994, which its defeat screens draw, h32 +0x4fa; s153) and no death mark
        // (no borrowing from II, user s70).
        var amiga = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga");
        if (amiga is not null)
        {
            Assert.EndsWith("ui/ending-ghost.png", amiga.Resolve(PictureNames.PortraitGhost)!.Replace('\\', '/'));
            Assert.Null(amiga.Resolve(PictureNames.PortraitDeathMark));
        }
    }

    [Theory]
    [InlineData(0.0, 90)]
    [InlineData(1.99, 90)]  // one step done: the first redraw is the whole door
    [InlineData(2.0, 89)]   // two steps: top 89 rows at y 15
    [InlineData(45.0, 46)]  // 45 steps
    [InlineData(89.0, 2)]   // 89 steps
    [InlineData(90.0, 0)]   // 90 steps: only the black top row is left, nothing to see
    [InlineData(900.0, 0)]
    public void Door_SinksOneRowAStep_ItsBottomStaysAt104(double steps, int height)
    {
        // s156: a step is the traced 413 VBLs / 90 (s141 Hatari: 4 Vsyncs + the blit), not the code's bare 4 VBLs.
        Assert.Equal(height, DefeatScene.DoorHeightAt(steps * DefeatScene.DoorSeconds / DefeatScene.Steps));
        var frame = new DefeatSceneFrame(height, false);
        if (height > 0)
            Assert.Equal(104, frame.DoorY + frame.DoorHeight);
        Assert.Equal(413 / 60.0, DefeatScene.DoorSeconds, 6);
    }

    [Theory]
    [InlineData(6, new[] { 18, 68, 118, 168, 218, 268 })] // gap (320 − 192) / 7 = 18
    [InlineData(1, new[] { 144 })]
    [InlineData(3, new[] { 56, 144, 232 })]
    public void Row_IsSpacedAsTheSt(int count, int[] xs) =>
        Assert.Equal(xs, Enumerable.Range(0, count).Select(i => DefeatScene.SlotX(count, i)));

    [Fact]
    public void Clock_ClosedUnderTheDefeatBox_SinksAfterIt_ThenTheFaceAfter3Seconds()
    {
        var clock = new DefeatSceneClock();
        Assert.Equal(new DefeatSceneFrame(90, false), clock.Observe(SessionPromptKind.PartyDefeat, 10));
        Assert.Equal(new DefeatSceneFrame(90, false), clock.Observe(SessionPromptKind.DefeatOutcome, 20)); // starts now
        Assert.Equal(46, clock.Observe(SessionPromptKind.DefeatOutcome, 20 + DefeatScene.DoorSeconds / 2).DoorHeight);
        Assert.Equal(new DefeatSceneFrame(0, false), clock.Observe(SessionPromptKind.DefeatOutcome, 26.9)); // gone (6.87 s), 3 s wait
        Assert.Equal(new DefeatSceneFrame(0, true), clock.Observe(SessionPromptKind.DefeatOutcome, 29.9)); // the face
    }

    [Fact]
    public void Clock_FifthDefeat_SignRightAfterTheDoor_ThenTheFaceAtOnce()
    {
        var clock = new DefeatSceneClock();
        clock.Observe(SessionPromptKind.PartyDefeat, 0);
        Assert.False(clock.Observe(SessionPromptKind.DefeatDemonSign, 1).ShowBehindDoor);
        Assert.Equal(new DefeatSceneFrame(0, true), clock.Observe(SessionPromptKind.DefeatDemonSign, 7.9)); // the sign
        Assert.Equal(new DefeatSceneFrame(0, true), clock.Observe(SessionPromptKind.DefeatOutcome, 8)); // the face
    }

    [Fact]
    public void OutcomePage_ShowsOnlyTheAstralLine_UntilTheFace()
    {
        var astral = GameText.S("core.defeat.arrived_at_astral_plane");
        IReadOnlyList<string> lines = [astral, "THE GREAT DEMON APPEARS AND SPEAKS:", "A YOU HAVE BEEN RESURRECTED"];
        Assert.Equal([astral], DefeatScene.LinesShown(SessionPromptKind.DefeatOutcome, lines, new DefeatSceneFrame(46, false)));
        Assert.Equal(lines, DefeatScene.LinesShown(SessionPromptKind.DefeatOutcome, lines, new DefeatSceneFrame(0, true)));
        Assert.Equal(lines, DefeatScene.LinesShown(SessionPromptKind.PartyDefeat, lines, new DefeatSceneFrame(90, false)));
    }

    [Fact]
    public void Clock_SceneMetMidway_FindsTheDoorOpen() =>
        Assert.Equal(new DefeatSceneFrame(0, true), new DefeatSceneClock().Observe(SessionPromptKind.DefeatOutcome, 5));

    [OriginalDataFact]
    public void Row_GhostsForEveryMember_ThenEachFate()
    {
        var s = ClientSessions.Overworld();
        var row = DefeatScene.Row(s, null, SessionPromptKind.PartyDefeat);
        Assert.Equal(s.Party.Members.Count, row.Count);
        Assert.All(row, p => Assert.Equal(DefeatPortraitKind.Ghost, p.Kind));

        var a = s.Party.Members[0];
        var b = s.Party.Members[1];
        b.Race = Race.Undead;
        var shown = new PartyDefeatResult { Fates = [(a, DefeatFate.Destroyed), (b, DefeatFate.Undead)] };
        Assert.All(DefeatScene.Row(s, shown, SessionPromptKind.DefeatDemonSign), p => Assert.Equal(DefeatPortraitKind.Ghost, p.Kind));
        Assert.Equal([new DefeatPortrait(DefeatPortraitKind.Cleared, 0), new DefeatPortrait(DefeatPortraitKind.Race, (int)Race.Undead)],
            DefeatScene.Row(s, shown, SessionPromptKind.DefeatOutcome));
        // s97: the fates show only once the Great Demon's face does (ST fate loop 0x126bc); ghosts while the door sinks.
        Assert.All(DefeatScene.Row(s, shown, SessionPromptKind.DefeatOutcome, faceShown: false),
            p => Assert.Equal(DefeatPortraitKind.Ghost, p.Kind));
    }

    [Fact]
    public void CombatRow_II_DropsTheDead_OnceTheirDeathMarkEnds()
    {
        var alive = new Character { Name = "A", Status = CharacterStatus.Ok };
        var dead = new Character { Name = "D", Status = CharacterStatus.Dead };
        Assert.True(UiOverlays.CombatRowShows(alive, _ => false));
        Assert.True(UiOverlays.CombatRowShows(dead, _ => true)); // the 500 ms mark (0x18a02) still runs
        Assert.False(UiOverlays.CombatRowShows(dead, _ => false)); // 0x185d6(1) → 0x18176: non-DEAD members only
    }

    [Fact]
    public void CombatRow_II_KeepsSleepers_AndTakesBackTheResurrected_s134()
    {
        // ST 0x18176 tests only status @24 != 0; the status table (0x1f067) has DEAD / OK / SLEEP and nothing else.
        var asleep = new Character { Name = "S", Status = CharacterStatus.Sleep };
        Assert.True(UiOverlays.CombatRowShows(asleep, _ => false));
        // "%s IS RESURRECTED" in combat (0x9240..0x929a, mode 5) rebuilds the row (0x185c4 → 0x185d6(1)): back in it.
        var raised = new Character { Name = "R", Status = CharacterStatus.Dead };
        Assert.False(UiOverlays.CombatRowShows(raised, _ => false));
        raised.Status = CharacterStatus.Ok;
        Assert.True(UiOverlays.CombatRowShows(raised, _ => false));
    }

    [Fact]
    public void CombatRow_II_RepacksInPartyOrder_LaterMembersMoveLeft_s134()
    {
        // 0x18176 lists the non-DEAD members in party order; 0x181ca spreads that shorter list over the screen again.
        Character M(string name, CharacterStatus status) => new() { Name = name, Status = status };
        var party = new[] { M("A", CharacterStatus.Ok), M("B", CharacterStatus.Dead), M("C", CharacterStatus.Ok),
            M("D", CharacterStatus.Sleep) };
        var filter = UiOverlays.CombatRowFilter(GameId.P2, _ => false)!;
        var row = party.Where(filter).ToList();
        Assert.Equal(["A", "C", "D"], row.Select(m => m.Name));
        var area = new Rectangle(8, 34, 304, 56);
        var before = PartyStripLayout.ComputeEvenRow(party.Length, area, 52).Cards;
        var after = PartyStripLayout.ComputeEvenRow(row.Count, area, 52).Cards;
        Assert.True(after[1].X < before[2].X); // C takes over B's place, more or less: further left than its old card
        Assert.True(after[2].X < before[3].X);
        Assert.Equal(DefeatScene.SlotX(3, 1) + 16, after[1].X + after[1].Width / 2); // the ST's 3-portrait spacing
    }

    [Theory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P3)]
    [InlineData(GameId.P4)]
    public void CombatRow_OtherGames_KeepTheDead(GameId game) =>
        Assert.Null(UiOverlays.CombatRowFilter(game, _ => false));

    [Fact]
    public void DefeatScene_IsTheWholeStScreen_NothingCropped()
    {
        // s98 (user: never crop scenes): all 155 DECOR rows (stairs, guards' platform), the row at ST y 158 under them,
        // the text line under the row, all inside 320x200.
        Assert.Equal(155, DefeatScene.DecorRows);
        Assert.Equal(158, UiOverlays.DefeatRowTop);
        Assert.True(UiOverlays.DefeatRowTop >= DefeatScene.DecorRows);
        Assert.True(DefeatScene.TextTop >= UiOverlays.DefeatRowTop + DefeatScene.PortraitSize);
        Assert.True(DefeatScene.TextTop + PixelFont.LineHeight <= UiOverlays.ScreenBottom);
    }

    [Fact]
    public void DefeatScenePager_ShowsOneLineAtATime_ThenAnswers()
    {
        var pager = new DefeatScenePager();
        string[] page = ["YOU HAVE ARRIVED AT THE ASTRAL PLANE", "", "THE GREAT DEMON APPEARS AND SPEAKS:", "A YOU HAVE BEEN DESTROYED"];
        var kind = SessionPromptKind.DefeatOutcome;
        Assert.Equal(page[0], pager.Current(kind, page));
        Assert.True(pager.HasMore(kind, page));
        Assert.True(pager.Advance(kind, page)); // blank lines are skipped
        Assert.Equal(page[2], pager.Current(kind, page));
        Assert.True(pager.Advance(kind, page));
        Assert.Equal(page[3], pager.Current(kind, page));
        Assert.False(pager.HasMore(kind, page));
        Assert.False(pager.Advance(kind, page)); // the last line: Return answers the prompt

        // A new page starts at its first line; so does the same page after Reset.
        string[] banner = ["YOUR PARTY HAS BEEN DEFEATED"];
        Assert.Equal(banner[0], pager.Current(SessionPromptKind.PartyDefeat, banner));
        Assert.False(pager.Advance(SessionPromptKind.PartyDefeat, banner));
        Assert.Equal(page[0], pager.Current(kind, page));
        pager.Advance(kind, page);
        pager.Reset();
        Assert.Equal(page[0], pager.Current(kind, page));
    }

    [Theory]
    // Outcome page: Astral Plane, Great Demon, then one fate line per member (3 members → 5 lines).
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    public void FatesShown_EachSlotTurnsWithItsLine(int lineIndex, int expected) =>
        Assert.Equal(expected, DefeatScene.FatesShown(lineCount: 5, lineIndex, fateCount: 3));

    [Fact]
    public void DefeatScenePager_OutcomeBeforeTheFace_HoldsTheAstralLine_ThenGoesOnWithTheSpeech()
    {
        // Until the face shows, the outcome page has only the Astral Plane line (LinesShown); once it shows, Return goes
        // on from there through the speech and the fates.
        var pager = new DefeatScenePager();
        var kind = SessionPromptKind.DefeatOutcome;
        string[] before = ["YOU HAVE ARRIVED AT THE ASTRAL PLANE"];
        string[] after = [before[0], "THE GREAT DEMON APPEARS AND SPEAKS:"];
        Assert.Equal(before[0], pager.Current(kind, before));
        Assert.False(pager.HasMore(kind, before));
        Assert.True(pager.Advance(kind, after));
        Assert.Equal(after[1], pager.Current(kind, after));
    }

    [OriginalDataFact]
    public void DeathMark_OnAMemberWhoDiedThisRound_For500Ms()
    {
        for (var seed = 1; seed < 60; seed++)
        {
            var s = ClientSessions.Fight(seed);
            var ui = new CombatPresenter(new ClientSettings { TimeLag = 0 });
            ui.Start(s);
            foreach (var m in s.Party.Members)
                m.HpCur = 1; // any hit kills
            for (var round = 0; round < 12 && s.Mode == GameMode.Combat; round++)
            {
                for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving && ui.IsPresenting; i++)
                    ui.HandleKey(s, Keys.Enter);
                if (ui.Phase != CombatPhase.Resolving)
                    break;
                var living = s.Party.Members.Where(m => m.Status != CharacterStatus.Dead).ToList();
                ui.Update(s, 0, fastForward: false);
                var died = living.Where(m => m.Status == CharacterStatus.Dead).ToList();
                if (died.Count == 0 || s.Mode != GameMode.Combat)
                    continue;
                Assert.All(died, m => Assert.True(ui.HasDeathMark(m)));
                Assert.All(s.Party.Members.Except(died), m => Assert.False(ui.HasDeathMark(m)));
                ui.Update(s, CombatPresenter.DeathMarkMs - 1, fastForward: false);
                Assert.True(ui.HasDeathMark(died[0]));
                ui.Update(s, 1, fastForward: false);
                Assert.False(ui.HasDeathMark(died[0]));
                return;
            }
        }
        Assert.Fail("no seed killed a member in a round that left the fight going");
    }
}
