using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// QoL SAVE SLOTS (<c>docs/plan/12-save-slots.md</c>): the slot picker (<see cref="SaveSlotScreen"/>) for Save, Load and the
/// main menu's Start in every game, the overwrite question, refusals, the rows' text and that the screen fits.
/// </summary>
public class SaveSlotScreenTests
{
    private const string Dir = "/saves";
    private static readonly DateTime Old = new(2026, 11, 1, 9, 5, 0);
    private static readonly DateTime New = new(2026, 11, 9, 14, 32, 0);

    private static SaveSlotPreview Party(params string[] names) => new(names, SavePlace.Town, 1, 0);

    /// <summary>I's ten slots: AUTO (old save), 1 empty, 2 newest save, 3 unreadable, the rest empty.</summary>
    private static IReadOnlyList<SaveSlotInfo> PhantasieI() =>
        SaveSlots.All(Dir, GameId.P1).Select(s => s.Number switch
        {
            0 => new SaveSlotInfo(s, Old, Party("ALPHA", "BRAVO", "CHARLIE")),
            2 => new SaveSlotInfo(s, New, new SaveSlotPreview(["DELTA"], SavePlace.Dungeon, 4, 2)),
            3 => new SaveSlotInfo(s, Old, null),
            _ => new SaveSlotInfo(s, null, null),
        }).ToArray();

    private static SaveSlotScreen Open(SaveSlotMode mode, GameId game = GameId.P1, IReadOnlyList<SaveSlotInfo>? slots = null)
    {
        var screen = new SaveSlotScreen();
        var all = slots ?? PhantasieI();
        screen.Open(game, mode, mode == SaveSlotMode.Save ? all.Where(s => !s.Slot.IsAuto).ToArray() : all,
            (place, n) => place == SavePlace.Town && n == 1 ? "PELNOR" : null);
        return screen;
    }

    [Fact]
    public void SaveSlots_IsOff_ByDefault_PerGame_AndRoundTrips()
    {
        Assert.False(new ClientSettings().Game(GameId.P1).SaveSlots); // off = the one save, as before
        var on = ClientSettings.FromJson("""{ "games": { "p3": { "saveSlots": true } } }""")!;
        Assert.True(on.Game(GameId.P3).SaveSlots);
        Assert.False(on.Game(GameId.P4).SaveSlots);
    }

    [Fact]
    public void OptionsRow_OnEveryGameTab_TogglesThatGamesSwitch()
    {
        var settings = new ClientSettings();
        var o = new OptionsScreen(settings, _ => [], _ => true);
        o.Open();
        foreach (var game in new[] { GameId.P1, GameId.P2, GameId.P3, GameId.P4 })
        {
            o.HandleKeys([Keys.Right]);
            Assert.Equal(game, o.Tab);
            for (var i = 0; i < 40 && !o.Lines().Any(l => l.Style == MenuLineStyle.Selected && l.Text.Contains("SAVE SLOTS")); i++)
                o.HandleKeys([Keys.Down]);
            var row = o.Lines().Single(l => l.Style == MenuLineStyle.Selected);
            Assert.Contains("OFF", row.Text);
            o.HandleKeys([Keys.Enter]);
            Assert.True(settings.Game(game).SaveSlots);
            o.HandleKeys([Keys.Home]);
        }
    }

    [Fact]
    public void Load_ListsAutoAndNine_CursorOnTheNewest_RowsShowPartyPlaceDate()
    {
        var s = Open(SaveSlotMode.Load);
        Assert.Equal(10, s.Slots.Count);
        Assert.Equal(2, s.Cursor); // slot 2 holds the newest save
        Assert.Equal("AUTO ALPHA +2       PELNOR       11-01 09:05", s.RowText(0));
        Assert.Equal("1    (EMPTY)", s.RowText(1));
        Assert.Equal("2    DELTA          DUNGEON 4    11-09 14:32", s.RowText(2));
        Assert.Equal("3    (CAN'T BE READ)", s.RowText(3));
        var lines = s.Lines();
        Assert.Equal("LOAD GAME", lines[0].Text);
        Assert.Equal("PHANTASIE I", lines[1].Text);
        // The selected slot's details under the list: names, place with the other parties, the full date.
        Assert.Equal(["DELTA", "DUNGEON 4  +2 OTHER PARTIES", "2026-11-09 14:32"], lines.TakeLast(3).Select(l => l.Text));
    }

    [Fact]
    public void Load_EmptyAndUnreadableSlotsAreRefused_AnotherIsPicked()
    {
        var s = Open(SaveSlotMode.Load);
        Assert.False(s.HandleKeys([Keys.D1]));
        Assert.Equal("THAT SLOT IS EMPTY.", s.Lines()[^1].Text);
        Assert.False(s.HandleKeys([Keys.D3]));
        Assert.Equal("THAT SAVE CAN'T BE READ.", s.Lines()[^1].Text);
        Assert.True(s.HandleKeys([Keys.A])); // A = AUTO
        Assert.False(s.IsOpen);
        var pick = s.TakePick();
        Assert.Equal(SaveSlotMode.Load, pick!.Mode);
        Assert.True(pick.Slot!.IsAuto);
        Assert.Null(s.TakePick());
    }

    [Fact]
    public void Save_OffersTheNumberedSlots_StartsOnTheFirstEmpty_EmptySavesAtOnce()
    {
        var s = Open(SaveSlotMode.Save);
        Assert.Equal(9, s.Slots.Count);
        Assert.DoesNotContain(s.Slots, i => i.Slot.IsAuto);
        Assert.Equal(1, s.Slots[s.Cursor].Slot.Number);
        Assert.True(s.HandleKeys([Keys.Enter]));
        Assert.Equal(1, s.TakePick()!.Slot!.Number);
    }

    [Fact]
    public void Save_OverAnOccupiedSlot_AsksFirst_NKeepsIt_YOverwrites()
    {
        var s = Open(SaveSlotMode.Save);
        Assert.False(s.HandleKeys([Keys.D2]));
        Assert.True(s.Confirming);
        Assert.Equal("OVERWRITE SLOT 2? (Y/N)", s.Lines()[^1].Text);
        Assert.Equal("Y overwrite  N / Esc back", s.Help);
        Assert.False(s.HandleKeys([Keys.N]));
        Assert.False(s.Confirming);
        Assert.True(s.IsOpen);
        s.HandleKeys([Keys.D2]);
        Assert.True(s.HandleKeys([Keys.Y]));
        Assert.Equal(2, s.TakePick()!.Slot!.Number);
    }

    [Fact]
    public void Save_UnreadableSlot_AlsoAsksBeforeOverwriting()
    {
        var s = Open(SaveSlotMode.Save);
        s.HandleKeys([Keys.D3]);
        Assert.True(s.Confirming);
    }

    [Fact]
    public void Esc_ClosesWithoutAPick()
    {
        var s = Open(SaveSlotMode.Save);
        Assert.True(s.HandleKeys([Keys.Escape]));
        Assert.False(s.IsOpen);
        Assert.Null(s.TakePick());
    }

    [Fact]
    public void Start_NewGameFirst_CursorOnTheNewestSave_NPicksANewGame()
    {
        var s = Open(SaveSlotMode.Start);
        Assert.Equal("NEW GAME", s.RowText(0));
        Assert.Equal(3, s.Cursor); // NEW GAME, AUTO, 1, then slot 2 (the newest)
        Assert.Equal("PHANTASIE I", s.Lines()[0].Text);
        Assert.Equal("A NEW GAME OR WHICH SAVE?", s.Lines()[1].Text);
        Assert.True(s.HandleKeys([Keys.N]));
        var pick = s.TakePick()!;
        Assert.Equal(SaveSlotMode.Start, pick.Mode);
        Assert.Null(pick.Slot);

        s = Open(SaveSlotMode.Start);
        Assert.True(s.HandleKeys([Keys.Enter]));
        Assert.Equal(2, s.TakePick()!.Slot!.Number);
    }

    [Fact]
    public void IV_ThreeSlots_SlotOneIsItsOneSave_NoAuto()
    {
        var slots = SaveSlots.All(Dir, GameId.P4)
            .Select(s => s.Number == 1 ? new SaveSlotInfo(s, New, Party("Jaela")) : new SaveSlotInfo(s, null, null)).ToArray();
        var s = Open(SaveSlotMode.Load, GameId.P4, slots);
        Assert.Equal(3, s.Slots.Count);
        Assert.Equal("1    Jaela          PELNOR       11-09 14:32", s.RowText(0));
        Assert.False(s.HandleKeys([Keys.A])); // no AUTO in IV: nothing happens
        Assert.True(s.IsOpen);
        Assert.True(s.HandleKeys([Keys.D1]));
        Assert.Equal(GameId.P4, s.TakePick()!.Game);
    }

    [Fact]
    public void Lines_FitTheMenuScreen_ALongListScrollsWithTheCursor_RowsMapToLines()
    {
        // Start lists eleven rows; with a long names line the detail takes more room and the list must scroll.
        var names = Enumerable.Range(0, 6).Select(i => $"LONGNAME{i}").ToArray();
        var slots = SaveSlots.All(Dir, GameId.P1)
            .Select(s => new SaveSlotInfo(s, Old.AddMinutes(s.Number), new SaveSlotPreview(names, SavePlace.Wilderness, null, 3)))
            .ToArray();
        var s = Open(SaveSlotMode.Start, slots: slots);
        for (var i = 0; i < 12; i++)
        {
            var lines = s.Lines();
            Assert.True(lines.Count - 1 <= SaveSlotScreen.ScreenLines, $"{lines.Count} lines");
            Assert.All(lines, l => Assert.True(l.Text.Length <= SaveSlotScreen.Columns, l.Text));
            var shown = Enumerable.Range(0, lines.Count).Where(l => s.RowOfLine(l) == s.Cursor).ToList();
            Assert.NotEmpty(shown); // the cursor's row is always on screen
            Assert.StartsWith(">", lines[shown[0]].Text);
            s.HandleKeys([Keys.Down]);
        }
        // Every name of the selected save is there, wrapped, never cut.
        s.HandleKeys([Keys.Home, Keys.Down]);
        var detail = string.Join(" ", s.Lines().Select(l => l.Text));
        Assert.All(names, n => Assert.Contains(n, detail));
    }

    [Fact]
    public void Click_ChoosesTheRowUnderThePointer_PointSelectsIt()
    {
        var s = Open(SaveSlotMode.Load);
        var lines = s.Lines();
        var autoLine = Enumerable.Range(0, lines.Count).Single(l => s.RowOfLine(l) == 0);
        s.Point(autoLine);
        Assert.Equal(0, s.Cursor);
        Assert.True(s.Click(autoLine));
        Assert.True(s.TakePick()!.Slot!.IsAuto);
    }

    [Fact]
    public void Pad_AChooses_BGoesBack()
    {
        Assert.Equal([Keys.Down, Keys.Y, Keys.Enter, Keys.Escape],
            SaveSlotScreen.PadKeys([Buttons.DPadDown, Buttons.A, Buttons.B]));
    }
}
