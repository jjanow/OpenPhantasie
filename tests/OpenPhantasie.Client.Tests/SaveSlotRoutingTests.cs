using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// QoL SAVE SLOTS (<c>docs/plan/12-save-slots.md</c>) and Reset the game (<c>16-reset-game.md</c>): <see cref="Game1"/>'s
/// routing (<see cref="SaveSlotRouting"/>): which slots the picker lists, Save / Load with the switch on or off, the main
/// menu's Start, the picker's outcome in every mode (I–III and IV, in game and over the main menu) and the RESET GAME row.
/// File checks run in a temp folder of their own, never the player's data folder.
/// </summary>
public sealed class SaveSlotRoutingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "phantasie-slot-routing-" + Guid.NewGuid().ToString("N"));

    public SaveSlotRoutingTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Touch(string path) => File.WriteAllText(path, "{}");

    private static SaveSlotPick Picked(SaveSlotMode mode, GameId game, int? number) =>
        new(mode, game, number is int n ? new SaveSlot(game, n, $"/saves/{game}-{n}.json") : null);

    // --- The slots listed ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(GameId.P1, SaveSlotMode.Save, 1, 9)]
    [InlineData(GameId.P1, SaveSlotMode.Load, 0, 10)]
    [InlineData(GameId.P1, SaveSlotMode.Start, 0, 10)]
    [InlineData(GameId.P3, SaveSlotMode.Reset, 0, 10)]
    [InlineData(GameId.P4, SaveSlotMode.Save, 1, 3)]
    [InlineData(GameId.P4, SaveSlotMode.Load, 1, 3)]
    [InlineData(GameId.P4, SaveSlotMode.Reset, 1, 3)]
    public void Offered_SaveListsTheNumberedSlots_TheOthersEverySlot(GameId game, SaveSlotMode mode, int first, int count)
    {
        var slots = SaveSlotRouting.Offered(_dir, game, mode);
        Assert.Equal(count, slots.Count);
        Assert.Equal(first, slots[0].Number);
        Assert.All(slots, s => Assert.Equal(game, s.Game));
        if (mode == SaveSlotMode.Save)
            Assert.DoesNotContain(slots, s => s.IsAuto);
    }

    // --- Save / Load in I–III: picker or the one save ------------------------------------------------------------------

    [Theory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void SaveAndLoad_SwitchOn_OpenThePicker_Off_UseTheOneSave(GameId game)
    {
        Assert.Null(SaveSlotRouting.SingleSavePath(_dir, game, switchOn: true));
        Assert.Equal(SaveSlots.SinglePath(_dir, game), SaveSlotRouting.SingleSavePath(_dir, game, switchOn: false));
    }

    // --- IV's original Save page: its three slots ----------------------------------------------------------------------

    [Fact]
    public void P4SlotPath_SlotsOneToThree_TheirFiles_ElseTheOneSave()
    {
        Assert.Equal(SaveSlots.SinglePath(_dir, GameId.P4), SaveSlotRouting.P4SlotPath(_dir, 1)); // slot 1 is the old single save
        Assert.Equal(SaveSlots.PathOf(_dir, GameId.P4, 2), SaveSlotRouting.P4SlotPath(_dir, 2));
        Assert.Equal(SaveSlots.PathOf(_dir, GameId.P4, 3), SaveSlotRouting.P4SlotPath(_dir, 3));
        Assert.Equal(SaveSlots.SinglePath(_dir, GameId.P4), SaveSlotRouting.P4SlotPath(_dir, 0));
        Assert.Equal(SaveSlots.SinglePath(_dir, GameId.P4), SaveSlotRouting.P4SlotPath(_dir, 4));
    }

    // --- The main menu's Start -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P4)]
    public void OffersSaves_OnlyWithTheSwitchOn_AndAFileInSomeSlot(GameId game)
    {
        Assert.False(SaveSlotRouting.OffersSaves(_dir, game, switchOn: true)); // no file: a new game
        Touch(SaveSlots.PathOf(_dir, game, SaveSlots.NumberedCount(game))); // the last slot only
        Assert.True(SaveSlotRouting.OffersSaves(_dir, game, switchOn: true));
        Assert.False(SaveSlotRouting.OffersSaves(_dir, game, switchOn: false));
        Assert.False(SaveSlotRouting.OffersSaves(_dir, game == GameId.P1 ? GameId.P2 : GameId.P3, switchOn: true)); // per game
    }

    // --- The picker's outcome ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(GameId.P1, SaveSlotMode.Save, SlotPickAction.Write)]
    [InlineData(GameId.P3, SaveSlotMode.Save, SlotPickAction.Write)]
    [InlineData(GameId.P4, SaveSlotMode.Save, SlotPickAction.WriteP4)]
    [InlineData(GameId.P2, SaveSlotMode.Load, SlotPickAction.Load)]
    [InlineData(GameId.P4, SaveSlotMode.Load, SlotPickAction.LoadP4)]
    [InlineData(GameId.P1, SaveSlotMode.Reset, SlotPickAction.OpenReset)]
    [InlineData(GameId.P4, SaveSlotMode.Reset, SlotPickAction.OpenReset)]
    [InlineData(GameId.P1, SaveSlotMode.Start, SlotPickAction.StartThenLoad)]
    [InlineData(GameId.P4, SaveSlotMode.Start, SlotPickAction.StartP4Slot)]
    public void Pick_ASlot_RunsTheModesAction(GameId game, SaveSlotMode mode, SlotPickAction expected)
    {
        var inGame = mode is SaveSlotMode.Save or SaveSlotMode.Load;
        var pick = Picked(mode, game, 2);
        Assert.Equal(expected, SaveSlotRouting.Pick(game, mode, pick, inGame && game == GameId.P4, inGame && game != GameId.P4));
    }

    [Theory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P4)]
    public void Pick_NewGameOnStart_StartsANewGame(GameId game) =>
        Assert.Equal(SlotPickAction.StartNew,
            SaveSlotRouting.Pick(game, SaveSlotMode.Start, Picked(SaveSlotMode.Start, game, null), false, false));

    [Theory]
    [InlineData(SaveSlotMode.Load)]
    [InlineData(SaveSlotMode.Start)]
    [InlineData(SaveSlotMode.Reset)]
    public void Esc_OutsideSave_DoesNothing(SaveSlotMode mode)
    {
        Assert.Equal(SlotPickAction.Nothing, SaveSlotRouting.Pick(GameId.P1, mode, null, false, true));
        Assert.Equal(SlotPickAction.Nothing, SaveSlotRouting.Pick(GameId.P4, mode, null, true, false));
        Assert.Equal(SlotPickAction.Nothing, SaveSlotRouting.Pick(GameId.P2, mode, null, false, false));
    }

    [Fact]
    public void Esc_OnSave_SaysNothingWasSaved_OnTheRunningGamesScreen()
    {
        Assert.Equal(SlotPickAction.NotSaved, SaveSlotRouting.Pick(GameId.P2, SaveSlotMode.Save, null, false, true));
        Assert.Equal(SlotPickAction.NotSavedP4, SaveSlotRouting.Pick(GameId.P4, SaveSlotMode.Save, null, true, false));
        Assert.Equal(SlotPickAction.Nothing, SaveSlotRouting.Pick(GameId.P1, SaveSlotMode.Save, null, false, false)); // main menu
    }

    // --- RESET GAME ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Reset_Slotted_PickerWithASave_ElseNoSave()
    {
        Assert.Equal(new ResetEntry(ResetEntryKind.NoSave), SaveSlotRouting.Reset(_dir, GameId.P1, switchOn: true));
        Assert.Equal(new ResetEntry(ResetEntryKind.NoSave), SaveSlotRouting.Reset(_dir, GameId.P4, switchOn: false));
        Touch(SaveSlots.PathOf(_dir, GameId.P1, 5));
        Touch(SaveSlots.PathOf(_dir, GameId.P4, 3));
        Assert.Equal(ResetEntryKind.Picker, SaveSlotRouting.Reset(_dir, GameId.P1, switchOn: true).Kind);
        Assert.Equal(ResetEntryKind.Picker, SaveSlotRouting.Reset(_dir, GameId.P4, switchOn: false).Kind); // IV is always slotted
    }

    [Fact]
    public void Reset_SwitchOff_TheOneSave_NoSaveOldOrUnreadable()
    {
        Assert.Equal(new ResetEntry(ResetEntryKind.NoSave), SaveSlotRouting.Reset(_dir, GameId.P2, switchOn: false));
        Touch(SaveSlots.PathOf(_dir, GameId.P2, 4)); // a slot file does not count with the switch off
        Assert.Equal(new ResetEntry(ResetEntryKind.NoSave), SaveSlotRouting.Reset(_dir, GameId.P2, switchOn: false));
        Touch(SaveSlots.SinglePath(_dir, GameId.P2)); // "{}" reads as an old-format save
        Assert.Equal(new ResetEntry(ResetEntryKind.OldSave), SaveSlotRouting.Reset(_dir, GameId.P2, switchOn: false));
        File.WriteAllText(SaveSlots.SinglePath(_dir, GameId.P2), "not a save");
        Assert.Equal(new ResetEntry(ResetEntryKind.Unreadable), SaveSlotRouting.Reset(_dir, GameId.P2, switchOn: false));
    }

    [Fact]
    public void Reset_TheOneSave_OldRefused_ReadableConfirms()
    {
        var slot = new SaveSlot(GameId.P3, 0, "/saves/savegame-p3.json");
        var when = new DateTime(2026, 11, 9);
        Assert.Equal(new ResetEntry(ResetEntryKind.NoSave),
            SaveSlotRouting.Reset(false, false, new SaveSlotInfo(slot, null, null)));
        Assert.Equal(new ResetEntry(ResetEntryKind.Unreadable),
            SaveSlotRouting.Reset(false, false, new SaveSlotInfo(slot, when, null)));
        Assert.Equal(new ResetEntry(ResetEntryKind.OldSave),
            SaveSlotRouting.Reset(false, false, new SaveSlotInfo(slot, when, null, Old: true)));
        Assert.Equal(new ResetEntry(ResetEntryKind.Confirm, slot),
            SaveSlotRouting.Reset(false, false, new SaveSlotInfo(slot, when, new SaveSlotPreview(["ALPHA"], SavePlace.Town, 1, 0))));
        Assert.Equal(ResetEntryKind.Picker, SaveSlotRouting.Reset(true, true, null).Kind);
    }
}
