using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// III's Yes / No questions on DOS's two-item menu root <c>0x75a3</c> (s108 static: every caller listed in
/// <c>dungeon-events.md</c> §2.2): Esc = No, Y / N answer, no cursor memory, and each caller's own highlight ("No" lit for
/// the exit pair, `core.p3.load_saved_dungeon` and the door's "Continue?"; "Yes" for the rest).
/// </summary>
public class P3YesNoPromptTests
{
    private static void AssertMenu75a3(SessionPrompt p, int lit)
    {
        Assert.True(p.IsYesNo);
        Assert.False(p.Remembers);
        Assert.Null(p.MenuId); // not I's COM menu ids
        Assert.Equal(2, p.EscapeChoice);
        Assert.Equal(lit, p.InitialChoice);
    }

    [OriginalDataFact]
    public void ExitPair_OpensOnNo_EscIsNo()
    {
        var s = WithParty();
        Assert.True(s.LeaveTown());
        s.EnterDungeon(5, Campaign(s).World.DungeonEntranceX[4], 0); // on the exit cell
        var prompts = new SessionPrompts(s);
        var take = prompts.Current!;
        Assert.Equal(SessionPromptKind.DungeonExitTakeIt, take.Kind);
        AssertMenu75a3(take, lit: 2); // ovl4 0x888: highlight 2
        prompts.Answer(take.EscapeChoice!.Value);
        Assert.Equal(GameMode.Dungeon, s.Mode); // No: the party stays

        s.Dungeon!.Y = 1;
        s.MoveDungeon(CardinalDirection.North);
        prompts.Answer(yes: true);
        var save = prompts.Current!;
        Assert.Equal(SessionPromptKind.DungeonExitSaveMap, save.Kind);
        AssertMenu75a3(save, lit: 2); // ovl4 0x8c5
        prompts.Answer(save.EscapeChoice!.Value);
        Assert.Equal(GameMode.Overworld, s.Mode);
    }

    [OriginalDataFact]
    public void PlaceEntry_OpensOnYes_TheLoadQuestionOnNo()
    {
        var s = WithParty();
        Assert.True(s.LeaveTown());
        s.CampaignAccess.EntryPrompt = new OverworldEntryPrompt(OverworldEntryKind.Dungeon, 5, 11, 0) { Name = "The Pendragon town archives" };
        var p = new SessionPrompts(s).Current!;
        Assert.Equal(SessionPromptKind.OverworldEntry, p.Kind);
        AssertMenu75a3(p, lit: 1); // ovl3 0x815: highlight 1
        s.CampaignAccess.EntryPrompt = s.EntryPrompt! with { AsksLoad = true };
        AssertMenu75a3(new SessionPrompts(s).Current!, lit: 2); // ovl4 0x6fa: highlight 2
    }

    [OriginalDataFact]
    public void Inn_IsAPlace_NameWillYouEnter_OpensOnYes()
    {
        var s = WithParty();
        s.LeaveTown();
        s.Overworld.X = 24; // The Mystic Inn (24, 20)
        s.Overworld.Y = 21;
        var prompts = new SessionPrompts(s);
        prompts.NoteOverworldMove(s.MoveOverworld(CardinalDirection.North));
        var p = prompts.Current!;
        Assert.Equal(SessionPromptKind.WildernessInn, p.Kind);
        var name = Campaign(s).World.PlaceAt(s.Overworld.RegionId, 24, 20)!.Name;
        Assert.Equal([GameText.F("core.p3.will_you_enter", name)], p.Lines);
        AssertMenu75a3(p, lit: 1);
        var gold = s.Party.GoldCarried;
        prompts.Answer(p.EscapeChoice!.Value);
        Assert.Equal(gold, s.Party.GoldCarried); // No: no stay
        Assert.Null(prompts.Current);
    }

    [OriginalDataFact]
    public void DungeonTrapAndDoor_Questions_TheirOwnHighlight_WhoWillDisarmEscIsCancel()
    {
        var s = WithParty();
        Phantasie3DungeonTests.Enter(s, 5, 11, 1);
        var yesNo = new DungeonMessage(["HERO0 spots a trap. Disarm it?"], [SessionPrompts.Yes, SessionPrompts.No]);
        s.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(3, -1, yesNo); // a trap square: ovl4 0x116c, highlight 1
        AssertMenu75a3(new SessionPrompts(s).Current!, lit: 1);
        s.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(0xba, -1, yesNo); // a door: ovl4 0x100d, highlight 2
        AssertMenu75a3(new SessionPrompts(s).Current!, lit: 2);
        s.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(3, -2, new DungeonMessage(["Who?"], ["HERO0", "HERO1"]));
        var who = new SessionPrompts(s).Current!;
        Assert.Equal(3, who.EscapeChoice); // s163: the picker's cancel (ovl4 0x11bc), the choice after the members
        Assert.Null(who.InitialChoice);
    }

    [OriginalDataTheory]
    [InlineData(WildernessMenu.SleepingMonsters, 1, true)] // Yes = attack
    [InlineData(WildernessMenu.SleepingMonsters, 2, false)] // No (Esc) = leave
    [InlineData(WildernessMenu.HearsMonsters, 1, true)] // Yes = fight
    public void WildernessQuestions_AreYesNo_YesFights(WildernessMenu menu, int answer, bool fights)
    {
        var s = WithParty();
        s.LeaveTown();
        s.CampaignAccess.WildernessPrompt = new WildernessPrompt(menu, 1, 0, "HERO0 spots sleeping monsters! Will you attack?");
        var prompts = new SessionPrompts(s);
        var p = prompts.Current!;
        Assert.Equal(SessionPromptKind.WildernessEncounter, p.Kind);
        AssertMenu75a3(p, lit: 1); // root 0xe33b: highlight 1
        prompts.Answer(answer);
        Assert.Equal(fights, s.Mode == GameMode.Combat);
    }
}
