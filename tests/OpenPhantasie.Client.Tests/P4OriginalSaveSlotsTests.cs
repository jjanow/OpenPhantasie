using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// IV's original three save slots with SAVE SLOTS off (<c>docs/plan/12-save-slots.md</c> section 2): System ▸ S, F5 and the
/// final screen's save question open <c>_diskmenu(1)</c> (P4.X <c>0x1AB4</c>: SPACE, then "*1:Save"–"*3:Save" with '*' on a
/// saved slot, and <c>p4x.0x3f6c6</c> ("* = saved") under them); F9 opens the remaster's load page over the slots that hold a save.
/// </summary>
public class P4OriginalSaveSlotsTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);

    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Find("x68000")!;

    private static P4DungeonScreen Screen(Func<int, bool>? saved = null) =>
        new(P4Preview.StartInTown(2, Code)) { OriginalSlots = true, SlotSaved = saved ?? (_ => false) };

    [OriginalDataFact]
    public void Pack_HasTheSavePagesSpaceIcon_AndItsCaption()
    {
        var set = Pack().CommandPanels!;
        Assert.Equal(new CommandPanelIcon(20, "Space", "p4x.0x3f5e8", " SPACE"), set.DiskSpace); // table entry 1
        Assert.Equal((68, 308, "p4x.0x3f6c6"), (set.DiskCaption!.X, set.DiskCaption.Y, set.DiskCaption.Label)); // _wkprcen(34, 68, 308, …)
        Assert.NotEmpty(set.DiskCaption.Text);
    }

    [OriginalDataFact]
    public void SavePage_ShowsSpaceAndAllThreeSlots_LoadPageOnlyTheSavedOnes()
    {
        var set = Pack().CommandPanels!;
        var save = P4DungeonScreen.DiskPanel(set, P4DungeonScreen.SavePanelName, k => k == 2)!;
        Assert.Equal(["Space", "1", "2", "3"], save.Icons.Select(i => i.Key));
        Assert.Equal("p4x.0x3f6ba", save.Title); // "Select File"
        var load = P4DungeonScreen.DiskPanel(set, P4DungeonScreen.LoadPanelName, k => k == 2)!;
        Assert.Equal(["Space", "2"], load.Icons.Select(i => i.Key)); // _disk(0)'s places: the saved slots in turn
        Assert.Null(P4DungeonScreen.DiskPanel(set, "_syscon", _ => true));
        Assert.Null(P4DungeonScreen.DiskPanel(null, P4DungeonScreen.SavePanelName, _ => true));

        // _diskmenu(1) writes '*' (saved) or ' ' over the label's first byte; the load page keeps the table's '*'.
        Assert.Equal(" 1:Save", P4DungeonScreen.DiskLabel(save, save.Icons[1], "*1:Save", k => k == 2));
        Assert.Equal("*2:Save", P4DungeonScreen.DiskLabel(save, save.Icons[2], "*2:Save", k => k == 2));
        Assert.Equal(" SPACE", P4DungeonScreen.DiskLabel(save, save.Icons[0], " SPACE", k => k == 2));
        Assert.Equal("*2:Save", P4DungeonScreen.DiskLabel(load, load.Icons[1], "*2:Save", _ => false));
        Assert.Equal(Keys.D3, P4DungeonScreen.PanelClickKey(P4DungeonScreen.SavePanelName, save.Icons[3]));
        Assert.Equal(Keys.Space, P4DungeonScreen.PanelClickKey(P4DungeonScreen.SavePanelName, save.Icons[0]));
    }

    [OriginalDataFact]
    public void SystemS_OpensTheSavePage_ASlotSavesThere_ThenBackToTheSystemIcons()
    {
        var s = Screen();
        s.HandleKeys([Keys.S, Keys.S]); // the street's System icon, then _syscon's S: _disk(1)
        Assert.Equal(P4SystemPage.Save, s.SystemPage);
        Assert.Contains(s.BoxLines(), l => l.Text[1..] == UiText.F("p4.system.slot_xy", " ", 3)); // no pack here: the lines (s191: under a cursor)
        Assert.Null(s.HandleKeys([Keys.D4])); // only the three slots and SPACE
        Assert.Equal(P4ScreenRequest.SaveSlot, s.HandleKeys([Keys.D2]));
        Assert.Equal((2, P4SystemPage.Menu), (s.RequestedSlot, s.SystemPage)); // _syscon's loop: its icons again
        s.HandleKeys([Keys.S, Keys.Space]);
        Assert.Equal(P4SystemPage.Menu, s.SystemPage);
        s.HandleKeys([Keys.Space]);
        Assert.Null(s.SystemPage);

        Assert.Null(s.HandleKeys([Keys.F5])); // F5 = the same page; SPACE leaves it
        Assert.Equal(P4SystemPage.Save, s.SystemPage);
        Assert.Equal(UiText.S("p4.system.save_help"), s.Help);
        s.HandleKeys([Keys.Space]);
        Assert.Null(s.SystemPage);
        s.HandleKeys([Keys.F5]);
        Assert.Equal(P4ScreenRequest.SaveSlot, s.HandleKeys([Keys.NumPad3]));
        Assert.Equal((3, (P4SystemPage?)null), (s.RequestedSlot, s.SystemPage));
    }

    [OriginalDataFact]
    public void SaveSlotsOn_LeavesSaveAndLoadToTheShellsPicker()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code)) { OriginalSlots = false };
        Assert.Equal(P4ScreenRequest.Save, s.HandleKeys([Keys.F5]));
        Assert.Equal(P4ScreenRequest.Load, s.HandleKeys([Keys.F9]));
        s.HandleKeys([Keys.S]);
        Assert.Equal(P4ScreenRequest.Save, s.HandleKeys([Keys.S]));
    }

    [OriginalDataFact]
    public void F9_ListsTheSavedSlots_OrSaysThereIsNone()
    {
        var none = Screen();
        Assert.Null(none.HandleKeys([Keys.F9]));
        Assert.Equal((UiText.S("p4.dungeon.no_save"), (P4SystemPage?)null), (none.Note, none.SystemPage));

        var s = Screen(k => k == 3);
        s.HandleKeys([Keys.F9]);
        Assert.Equal(P4SystemPage.Load, s.SystemPage);
        var lines = s.BoxLines().Select(l => l.Text[1..]).ToList(); // s191: the cursor's column
        Assert.Contains(UiText.F("p4.system.slot_xy", "*", 3), lines);
        Assert.DoesNotContain(UiText.F("p4.system.slot_xy", " ", 1), lines);
        Assert.Null(s.HandleKeys([Keys.D1])); // an empty slot does nothing
        Assert.Equal(P4ScreenRequest.LoadSlot, s.HandleKeys([Keys.D3]));
        Assert.Equal((3, (P4SystemPage?)null), (s.RequestedSlot, s.SystemPage));
    }

    [OriginalDataFact]
    public void FinalScreen_Yes_OpensTheSavePage_ThenSelectGame()
    {
        var game = P4Preview.StartEnding(0, Code);
        var s = new P4DungeonScreen(game) { OriginalSlots = true };
        while (game.Town.Prompt == P4TownPrompt.Wait)
            s.HandleKeys([Keys.Space]);
        Assert.Null(s.HandleKeys([Keys.Y])); // _endstory's YES: _disk(1)
        Assert.Equal(P4SystemPage.Save, s.SystemPage);
        Assert.Equal(P4ScreenRequest.SaveSlot, s.HandleKeys([Keys.D1]));
        Assert.Equal(("_acgamend.1", P4SystemPage.SelectGameFinal), (s.PanelName, s.SystemPage));
    }
}
