using Microsoft.Xna.Framework;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s140, II ST (FORMAT.md 7.4): the defeat scene's sounds 23 / 22 / 20 / 21 (<see cref="P2DefeatSounds"/>), the alert
/// boxes' beep per message (<see cref="P2AlertBeeps"/>, the pack's <c>field_sounds.alerts</c>), the click after a pick
/// and TRANSPORTATION's palette fade (<see cref="StTransportFade"/>, <see cref="TransportFadeFilter"/>).
/// </summary>
public class P2StFieldScenesTests
{
    private static GraphicsPack Pack() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!;

    private static readonly GameDataRepository Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));

    private static P2DefeatSounds.Step[] Seen(P2DefeatSounds run, FieldDefeatSounds map, SessionPromptKind kind,
        DefeatSceneFrame frame, int fates = 0, double now = 100) => [.. run.Observe(map, kind, frame, fates, now)];

    // ==== the defeat scene =============================================================================================

    [OriginalDataFact]
    public void Defeat_PackHoldsTheStsSounds_AtSpeed20()
    {
        var pack = Pack();
        var d = pack.FieldSounds!.Defeat!;
        Assert.Equal((20, 23, 22, 20, 21), (d.SpeedMs, d.Ghosts, d.Door, d.Demon, d.Fate));
        Assert.True(Game1.FieldSoundsWait(pack.FieldSounds, d.SpeedMs)); // 20 >= 9: each waits, whatever the player's Speed
    }

    [OriginalDataFact]
    public void Defeat_GhostsAfterTheLinesHold_ThenDoor_ThenDemonAndOneFatePerMember()
    {
        var map = Pack().FieldSounds!.Defeat!;
        Assert.Equal(3000, map.GhostsAfterMs); // 0x12a7c: line 1 holds 3000 ms; s141 trace: 23 3060 ms after it
        var run = new P2DefeatSounds();
        var closed = new DefeatSceneFrame(DefeatScene.DoorHeightClosed, false);
        Assert.Empty(Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 10));
        Assert.Empty(Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 12.9));
        Assert.Equal([new P2DefeatSounds.Step(23, false)], Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 13));
        Assert.Empty(Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 20)); // once
        Assert.Equal([new P2DefeatSounds.Step(22, false)], Seen(run, map, SessionPromptKind.DefeatOutcome, closed));
        Assert.Empty(Seen(run, map, SessionPromptKind.DefeatOutcome, new DefeatSceneFrame(0, false))); // door open, no face yet
        var face = new DefeatSceneFrame(0, true);
        Assert.Equal([new P2DefeatSounds.Step(20, false)], Seen(run, map, SessionPromptKind.DefeatOutcome, face));
        Assert.Equal([new P2DefeatSounds.Step(21, false)], Seen(run, map, SessionPromptKind.DefeatOutcome, face, 1));
        Assert.Empty(Seen(run, map, SessionPromptKind.DefeatOutcome, face, 1));
        Assert.Equal([new P2DefeatSounds.Step(21, false), new P2DefeatSounds.Step(21, false)],
            Seen(run, map, SessionPromptKind.DefeatOutcome, face, 3));
        Assert.Empty(Seen(run, map, SessionPromptKind.None, face, 3)); // the scene is over
        Assert.Empty(Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 50)); // a new one
        Assert.Equal([new P2DefeatSounds.Step(23, false)], Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 53));
    }

    [OriginalDataFact]
    public void Defeat_TheGhostRowShows_AfterTheFirstLinesHold()
    {
        // s156: 0x12d4c line 1 (3000 ms or a key), 0x12d54 the ghost row, 0x12d5e sound 23: no row before.
        var map = Pack().FieldSounds!.Defeat!;
        var run = new P2DefeatSounds();
        var closed = new DefeatSceneFrame(DefeatScene.DoorHeightClosed, false);
        Assert.True(run.GhostsShown);
        Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 10);
        Assert.False(run.GhostsShown);
        Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 12.9);
        Assert.False(run.GhostsShown);
        Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 13);
        Assert.True(run.GhostsShown);
        Seen(run, map, SessionPromptKind.None, closed, now: 14);
        Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 20);
        Assert.False(run.GhostsShown);
        Seen(run, map, SessionPromptKind.DefeatOutcome, closed, now: 21); // a key left the page early
        Assert.True(run.GhostsShown);
    }

    [OriginalDataFact]
    public void Defeat_PageLeftBeforeTheHold_GhostsThenDoor()
    {
        // A key ends the ST's 3000 ms hold: the ghost row, 23, then (after the astral line) 22, which waits for 23.
        var map = Pack().FieldSounds!.Defeat!;
        var run = new P2DefeatSounds();
        var closed = new DefeatSceneFrame(DefeatScene.DoorHeightClosed, false);
        Assert.Empty(Seen(run, map, SessionPromptKind.PartyDefeat, closed, now: 10));
        Assert.Equal([new P2DefeatSounds.Step(23, false), new P2DefeatSounds.Step(22, false)],
            Seen(run, map, SessionPromptKind.DefeatOutcome, closed, now: 11));
    }

    [OriginalDataFact]
    public void Defeat_FifthDefeat_TheSignThenTheDemon()
    {
        var map = Pack().FieldSounds!.Defeat!;
        var run = new P2DefeatSounds();
        Seen(run, map, SessionPromptKind.PartyDefeat, new DefeatSceneFrame(DefeatScene.DoorHeightClosed, false), now: 10);
        Assert.Equal([new P2DefeatSounds.Step(23, false), new P2DefeatSounds.Step(22, false)],
            Seen(run, map, SessionPromptKind.DefeatDemonSign, new DefeatSceneFrame(DefeatScene.DoorHeightClosed, false)));
        Assert.Empty(Seen(run, map, SessionPromptKind.DefeatDemonSign, new DefeatSceneFrame(0, true))); // the sign, not the face
        Assert.Equal([new P2DefeatSounds.Step(20, false)],
            Seen(run, map, SessionPromptKind.DefeatOutcome, new DefeatSceneFrame(0, true)));
    }

    [OriginalDataFact]
    public void Defeat_MetMidWay_ReplaysNothing()
    {
        var map = Pack().FieldSounds!.Defeat!;
        var run = new P2DefeatSounds();
        Assert.Empty(Seen(run, map, SessionPromptKind.DefeatOutcome, new DefeatSceneFrame(0, true), 2)); // after a load
        Assert.Equal([new P2DefeatSounds.Step(21, false)],
            Seen(run, map, SessionPromptKind.DefeatOutcome, new DefeatSceneFrame(0, true), 3));
    }

    // ==== the alerts' beep =============================================================================================

    private static P2AlertBeeps Beeps() => new(Pack().FieldSounds!.Alerts!);

    [OriginalDataFact]
    public void Alerts_BeepTheDefaultScript()
    {
        Assert.Equal(34, Beeps().Sound);
        Assert.Equal(19, Pack().FieldSounds!.Alerts!.Lines.Count);
    }

    [OriginalDataTheory]
    [InlineData("Party is full (max 6).", GameMode.Guild, true)]
    [InlineData("Not enough gold.", GameMode.Town, true)]
    [InlineData("You can't afford it!", GameMode.Guild, true)]
    [InlineData("You can not afford it!", GameMode.Town, true)]
    [InlineData("You are improved", GameMode.Town, true)]
    [InlineData("You must assemble a group.", GameMode.Town, true)]
    [InlineData("You've learned", GameMode.Guild, true)]
    [InlineData("No one is wise enough to teach You!", GameMode.Guild, true)]
    [InlineData("Not in the town", GameMode.Guild, true)]
    [InlineData("You can't learn any more!", GameMode.Town, true)]
    [InlineData("HERO0 drinks HEALING 3 (+9 HP).", GameMode.Town, true)]
    [InlineData("HERO0 drinks MAGIC 1 (+3 MP).", GameMode.Town, true)]
    [InlineData("HERO0 drinks HEALING 3 (+9 HP).", GameMode.Dungeon, false)] // the field's use has no alert
    [InlineData("UNUSABLE", GameMode.Town, true)]
    [InlineData("You've failed", GameMode.Guild, true)]
    [InlineData("He's just bought ninth item.", GameMode.Town, true)]
    [InlineData("HERO1 has died", GameMode.Town, true)]
    [InlineData("HERO1 has aged", GameMode.Town, true)]
    [InlineData("You need 1200 more experience points. It will cost 50 gold.", GameMode.Guild, true)] // the cost after
    [InlineData("Not enough magic points.", GameMode.Dungeon, true)] // OUT.RSC 0
    [InlineData("Not enough magic points.", GameMode.Overworld, true)]
    [InlineData("Not enough magic points.", GameMode.Town, false)] // the town cast has no such alert
    [InlineData("You are trained.", GameMode.Guild, false)]
    [InlineData("Bought SWORD for 10 GP.", GameMode.Town, false)]
    [InlineData("HERO0 HITS", GameMode.Combat, false)]
    [InlineData("You must assemble a group.", GameMode.Combat, false)]
    public void Alerts_LineAndMode(string line, GameMode mode, bool beeps) =>
        Assert.Equal(beeps, Beeps().Beeps(line, mode));

    [OriginalDataFact]
    public void Alerts_EveryKeysOwnText_BeepsInItsModes()
    {
        var beeps = Beeps();
        foreach (var line in Pack().FieldSounds!.Alerts!.Lines)
        {
            var text = string.Format(GameText.S(line.Key), "HERO0", 3, 9);
            foreach (var mode in line.Modes)
                Assert.True(beeps.Beeps(text, Enum.Parse<GameMode>(mode, ignoreCase: true)), $"{line.Key} in {mode}");
        }
    }

    [OriginalDataFact]
    public void Alerts_ARefusalNotedAndReturned_BeepsOnce()
    {
        var beeps = Beeps();
        string[] noted = ["Not enough gold."];
        Assert.Equal(1, beeps.BeepsFor(noted, noted, GameMode.Town));
        Assert.Equal(2, beeps.BeepsFor(["HERO0 has aged", "HERO1 has aged"], [], GameMode.Town)); // one box each
        Assert.Equal(0, beeps.BeepsFor(["Not enough gold."], [], GameMode.Combat));
    }

    [OriginalDataFact]
    public void Alerts_Unusable_BeepsOnlyFromTheUseItemDialog()
    {
        // s141: PHANT.RSC 10 'UNUSABLE' is use_item's alone (0x1604 <- 0xe07e); the Armory's I-rule line after a buy has
        // the same text and no ST alert, and it reaches the Client through the log, not a dialog's line.
        var beeps = Beeps();
        Assert.Equal(0, beeps.BeepsFor(["UNUSABLE"], [], GameMode.Town));
        Assert.Equal(1, beeps.BeepsFor(["UNUSABLE"], ["UNUSABLE"], GameMode.Town));
        Assert.Equal(1, beeps.BeepsFor(["Not enough gold."], [], GameMode.Town)); // the rest count from the log
    }

    [OriginalDataFact]
    public void Alerts_SessionNotes_ReachTheLog()
    {
        // The refusals Core notes reach the log the Client reads (the rest come back as a dialog's line).
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        s.StartNewGame();
        var mark = s.LogSequence;
        Assert.False(s.LeaveTown()); // no party
        Assert.Equal(1, Beeps().Count(s.LogSince(mark), s.Mode));
    }

    private static GameSession WithParty()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        s.StartNewGame();
        s.OpenGuild();
        var classes = new[] { CharacterClass.Fighter, CharacterClass.Priest, CharacterClass.Wizard, CharacterClass.Thief };
        var races = new[] { Race.Human, Race.Dwarf, Race.Elf, Race.Halfling };
        for (var i = 0; i < classes.Length; i++)
            Assert.True(s.TryAddToParty(s.CreateAndEnroll("HERO" + i, races[i], classes[i]), out var err), err);
        s.LeaveGuild();
        return s;
    }

    [OriginalDataFact]
    public void Alerts_TrainingShortOfXp_AndABuyWithNoGold_ReachTheLog()
    {
        var s = WithParty();
        var beeps = Beeps();
        var mark = s.LogSequence;
        Assert.False(s.Train(s.Party.Members[0]).Kind == TownTransactionKind.Ok); // level 1, no XP: 0x1814's beep
        Assert.Equal(1, beeps.Count(s.LogSince(mark), GameMode.Guild));
        s.Party.GoldCarried = 0;
        var item = Enumerable.Range(TownState.FirstListedItemId, 100).First(id => s.CurrentTown!.GetStock(id) > 0);
        mark = s.LogSequence;
        Assert.Equal(TownTransactionKind.Failed, s.Buy(s.Party.Members[0], item).Kind); // 0x15e0: alert 1
        Assert.Equal(1, beeps.Count(s.LogSince(mark), s.Mode));
    }

    [OriginalDataFact]
    public void Alerts_OtherGamesPacks_HaveNone()
    {
        Assert.Null(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")?.FieldSounds?.Alerts);
        Assert.Equal(33, Pack().FieldSounds!.Click);
    }

    // ==== TRANSPORTATION's fade ========================================================================================

    [OriginalDataFact]
    public void Fade_PackTiming_AndTheTownPalette()
    {
        var pack = Pack();
        var fade = pack.TransportFade!;
        Assert.Equal(pack.Palettes["town"], fade.Palette);
        Assert.Equal((60.0, 10, 100, 7, true), (fade.VblHz, fade.OutVbl, fade.HoldVbl, fade.InVbl, fade.SilencesSound));
        Assert.Equal(16 * 10 + 100 + 16 * 7, fade.TotalVbl); // 372
        Assert.Equal(6.2, fade.TotalSeconds, 6);
    }

    [OriginalDataFact]
    public void Fade_RegistersGoBlackInTurn_AndComeBackInTurn()
    {
        var fade = Pack().TransportFade!;
        double At(int vbl) => vbl / fade.VblHz;
        Assert.True(fade.IsBlack(0, 0));
        Assert.False(fade.IsBlack(1, At(9)));
        Assert.True(fade.IsBlack(1, At(10)));
        Assert.False(fade.IsBlack(15, At(149)));
        Assert.True(fade.IsBlack(15, At(150)));
        Assert.All(Enumerable.Range(0, 16), r => Assert.True(fade.IsBlack(r, At(259)))); // all black for 100 VBL
        Assert.False(fade.IsBlack(0, At(260))); // back one each 7 VBL
        Assert.True(fade.IsBlack(1, At(266)));
        Assert.False(fade.IsBlack(1, At(267)));
        Assert.True(fade.IsBlack(15, At(364)));
        Assert.False(fade.IsBlack(15, At(365)));
        Assert.All(Enumerable.Range(0, 16), r => Assert.False(fade.IsBlack(r, At(372))));
    }

    [OriginalDataFact]
    public void Fade_Filter_BlacksThePixelsOfBlackRegisters()
    {
        var fade = Pack().TransportFade!;
        var filter = new TransportFadeFilter(fade.Palette);
        var green = VirtualScreen.ParseColor(fade.Palette[1]);
        var white = VirtualScreen.ParseColor(fade.Palette[15]);
        Color[] frame = [green, white];
        filter.At(fade, 10 / fade.VblHz); // registers 0 and 1 black
        Assert.True(filter.Active);
        Assert.True(VirtualScreen.FiltersFrame(null, null, filter));
        VirtualScreen.FilterFrame(frame, null, null, filter);
        Assert.Equal(Color.Black, frame[0]);
        Assert.Equal(white, frame[1]);
        filter.At(fade, fade.TotalSeconds); // over: nothing changes
        Assert.False(filter.Active);
        Assert.False(VirtualScreen.FiltersFrame(null, null, filter));
        Assert.Equal(15, filter.NearestRegister(new Color(250, 250, 250))); // a remaster colour: its nearest register
    }
}
