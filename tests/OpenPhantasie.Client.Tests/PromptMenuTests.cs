using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>DOS menus reopen on the last choice made in that menu id (s45 sleeping-monsters menu, s58 per-prompt YES/NO cursors).</summary>
public class PromptMenuTests
{
    private static SessionPrompt YesNo(string text) =>
        new(SessionPromptKind.DungeonExitTakeIt, [text], [SessionPrompts.Yes, SessionPrompts.No]);

    private static SessionPrompt Sleeping() =>
        new(SessionPromptKind.WildernessEncounter, ["YOU SEE A CAMP OF TRAVELERS"], ["ATTACK", "LEAVE"]);

    private static SessionPrompt Hears() =>
        new(SessionPromptKind.WildernessEncounter, ["crusho HEARS MONSTERS"], ["RUN", "FIGHT"]);

    [Fact]
    public void FirstOpening_HighlightsTheFirstOption()
    {
        var menu = new PromptMenu();
        menu.Sync(Hears());
        Assert.Equal(0, menu.Cursor); // s45: 0x11 opened on RUN
    }

    [Fact]
    public void ArrowsMoveAndEnterPicksTheHighlight()
    {
        var menu = new PromptMenu();
        var p = Sleeping();
        Assert.Null(menu.HandleKey(p, Keys.Right));
        Assert.Equal(1, menu.Cursor);
        Assert.Equal(2, menu.HandleKey(p, Keys.Enter)); // LEAVE
    }

    [Fact]
    public void SleepingMonstersMenu_ReopensOnLeave_s45()
    {
        var menu = new PromptMenu();
        menu.HandleKey(Sleeping(), Keys.Right);
        Assert.Equal(2, menu.HandleKey(Sleeping(), Keys.Enter));
        menu.Sync(null); // back on the map

        menu.Sync(Sleeping());
        Assert.Equal(1, menu.Cursor); // opens on LEAVE
        Assert.Null(menu.HandleKey(Sleeping(), Keys.Right));
        Assert.Equal(1, menu.HandleKey(Sleeping(), Keys.Enter)); // one Right → ATTACK, as in the s45 capture
    }

    private static SessionPrompt Enter() =>
        new(SessionPromptKind.OverworldEntry, ["A CAVERN. ENTER?"], [SessionPrompts.Yes, SessionPrompts.No]) { MenuId = 0x1c };

    private static SessionPrompt SaveMap() =>
        new(SessionPromptKind.DungeonExitSaveMap, [SessionPrompts.ExitSaveMapText], [SessionPrompts.Yes, SessionPrompts.No])
            { MenuId = 0x1e };

    [Fact]
    public void EachYesNoMenu_KeepsItsOwnCursor_s58()
    {
        var menu = new PromptMenu();
        Assert.Null(menu.HandleKey(Enter(), Keys.Right));
        Assert.Equal(2, menu.HandleKey(Enter(), Keys.Enter)); // ENTER? NO
        menu.Sync(null);
        Assert.Equal(1, menu.CursorFor(Enter()));   // m7: reopens on NO

        menu.HandleKey(Enter(), Keys.Left);
        Assert.Equal(1, menu.HandleKey(Enter(), Keys.Enter)); // YES
        menu.Sync(null);
        Assert.Equal(2, menu.HandleKey(SaveMap(), Keys.N));    // SAVE MAP NO
        menu.Sync(null);
        Assert.Equal(0, menu.CursorFor(Enter()));   // d9: ENTER? still on YES
        Assert.Equal(1, menu.CursorFor(SaveMap()));
    }

    [Fact]
    public void KeepIt_CarriesItsNoToTheNextKeepIt_s56()
    {
        var menu = new PromptMenu();
        var keep = new SessionPrompt(SessionPromptKind.Treasure, ["YOU FIND SM AXE. KEEP IT?"], [SessionPrompts.Yes, SessionPrompts.No])
            { MenuId = 0x1b };
        menu.HandleKey(keep, Keys.N);
        menu.Sync(null);
        Assert.Equal(1, menu.CursorFor(keep with { Lines = ["YOU FIND WAND. KEEP IT?"] }));
    }

    [Fact]
    public void YesNo_DownAndUpDoNothing_s58()
    {
        var menu = new PromptMenu();
        Assert.Null(menu.HandleKey(Enter(), Keys.Down));
        Assert.Null(menu.HandleKey(Enter(), Keys.Up));
        Assert.Equal(0, menu.Cursor);
    }

    [Fact]
    public void EventMenu_AlwaysOpensOnOption1_AndWraps_s58()
    {
        var menu = new PromptMenu();
        var ev = new SessionPrompt(SessionPromptKind.DungeonEvent, ["A BLUE GLOOP SAYS -"], ["GREEN/BLUE", "RED", "BLUE", "RED/GREEN"]);
        Assert.Null(menu.HandleKey(ev, Keys.Up));
        Assert.Equal(3, menu.Cursor);                  // §7: Up on option 1 wraps to the last
        Assert.Null(menu.HandleKey(ev, Keys.Down));
        Assert.Equal(0, menu.Cursor);                  // and Down on the last wraps to option 1
        menu.HandleKey(ev, Keys.Up);
        Assert.Equal(4, menu.HandleKey(ev, Keys.Enter));
        menu.Sync(null);
        Assert.Equal(0, menu.CursorFor(ev));           // §5: reopened on option 1, not the last pick
    }

    [Fact]
    public void NineButtonMenu_EveryButtonReachable_PhantasieII_Dng8Door()
    {
        // II DNG8 message 71: "PUSH BUTTON" 1 … 9 (the door to the Ice Dragon); the Client draws more than six answers
        // as a grid (UiOverlays.DrawPromptGrid) so buttons 7–9 show too.
        var menu = new PromptMenu();
        var door = new SessionPrompt(SessionPromptKind.DungeonEvent, ["PUSH BUTTON"],
            ["1", "2", "3", "4", "5", "6", "7", "8", "9"]);
        Assert.Equal(9, menu.HandleKey(door, Keys.D9));
        menu.Sync(null);
        menu.HandleKey(door, Keys.Up);
        Assert.Equal(8, menu.Cursor);
        Assert.Equal(9, menu.HandleKey(door, Keys.Enter));
    }

    [Fact]
    public void PageMenu_FirstEverOnSkills()
    {
        Assert.Equal(2, new PromptMenu().CursorFor(InspectDialog.PageMenu));
    }

    [Fact]
    public void YesNo_LeftAndRightBothToggle()
    {
        var menu = new PromptMenu();
        var p = YesNo("A BRIDGE. CROSS IT?");
        menu.HandleKey(p, Keys.Left);
        Assert.Equal(1, menu.Cursor);
        menu.HandleKey(p, Keys.Left);
        Assert.Equal(0, menu.Cursor);
        menu.HandleKey(p, Keys.Right);
        Assert.Equal(1, menu.Cursor);
    }

    [Fact]
    public void DifferentMenus_KeepTheirOwnMemory()
    {
        var menu = new PromptMenu();
        menu.HandleKey(Hears(), Keys.D2); // FIGHT
        menu.Sync(null);
        Assert.Equal(0, menu.CursorFor(Sleeping()));
        Assert.Equal(1, menu.CursorFor(Hears()));
    }

    [Fact]
    public void DirectKeys_StillAnswer()
    {
        Assert.Equal(1, PromptMenu.DirectChoice(YesNo("X"), Keys.Y));
        var trap = new SessionPrompt(SessionPromptKind.DungeonTrap, ["A TRAP"], [SessionPrompts.Disarm, SessionPrompts.Leave]);
        Assert.Equal(2, PromptMenu.DirectChoice(trap, Keys.L));
        var shares = new SessionPrompt(SessionPromptKind.BankShares, ["Shares for: X"], ["1", "2", "3"]);
        Assert.Equal(3, PromptMenu.DirectChoice(shares, Keys.D3));
        Assert.Null(PromptMenu.DirectChoice(shares, Keys.D0));
    }

    [Fact]
    public void Continue_TakesEnterOnly()
    {
        var menu = new PromptMenu();
        var p = new SessionPrompt(SessionPromptKind.DefeatOutcome, ["..."], [SessionPrompts.Continue]);
        Assert.Null(menu.HandleKey(p, Keys.Right));
        Assert.Null(menu.HandleKey(p, Keys.Y));
        Assert.Equal(1, menu.HandleKey(p, Keys.Enter));
    }

    [Fact]
    public void IiiMenu75a3_OpensOnTheCallersHighlight_EveryTime_s108()
    {
        // `core.prompt.an_exit_take_it`: ovl4 passes highlight 2 ("No") to 0x75a3 on every call, no memory.
        SessionPrompt Exit() =>
            new(SessionPromptKind.DungeonExitTakeIt, ["A bridge. Will you cross it?"], [SessionPrompts.Yes, SessionPrompts.No])
                { Remembers = false, InitialChoice = 2, EscapeChoice = 2 };
        var menu = new PromptMenu();
        menu.Sync(Exit());
        Assert.Equal(1, menu.Cursor);
        Assert.Null(menu.HandleKey(Exit(), Keys.Left));
        Assert.Equal(1, menu.HandleKey(Exit(), Keys.Enter)); // Yes
        menu.Sync(null);
        menu.Sync(Exit());
        Assert.Equal(1, menu.Cursor); // back on "No"
        Assert.Equal(2, menu.HandleKey(Exit(), Keys.N));
    }

    [Fact]
    public void IiiFightFind_OpensOnYesEveryTime_s98()
    {
        // root 0xf2eb passes highlight 1 ("Yes") to its menu 0x75a3 on every call: no cursor memory.
        SessionPrompt Find() =>
            new(SessionPromptKind.Treasure, ["HERO finds SWORD", "Will the party take it?"], [SessionPrompts.Yes, SessionPrompts.No])
                { Remembers = false };
        var menu = new PromptMenu();
        Assert.Null(menu.HandleKey(Find(), Keys.Right));
        Assert.Equal(2, menu.HandleKey(Find(), Keys.Enter)); // No
        menu.Sync(null);
        menu.Sync(Find());
        Assert.Equal(0, menu.Cursor);
        Assert.Equal(1, menu.HandleKey(Find(), Keys.Enter));
    }
}
