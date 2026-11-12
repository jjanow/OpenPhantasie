using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// P3 "Reset the game" (<c>docs/plan/16-reset-game.md</c>): a game tab's RESET GAME row (main menu only) picks the save
/// (SAVE SLOTS on, and IV always: the slot picker in its Reset mode; else the game's one save), then
/// <see cref="ResetGameScreen"/> asks Keep / Erase and YES / NO, and <see cref="GameReset"/> rewrites that one file.
/// </summary>
public partial class Game1
{
    private readonly ResetGameScreen _reset = new();

    /// <summary>The RESET GAME row was picked for <paramref name="game"/>.</summary>
    private void OpenReset(GameId game)
    {
        var route = SaveSlotRouting.Reset(ClientSettings.DataDirectory, game, _settings.Game(game).SaveSlots);
        switch (route.Kind)
        {
            case ResetEntryKind.Picker:
                OpenSaveSlots(game, SaveSlotMode.Reset);
                break;
            case ResetEntryKind.NoSave:
                _reset.OpenNotice(game, UiText.S("reset.no_save"));
                break;
            case ResetEntryKind.OldSave:
                _reset.OpenNotice(game, UiText.S("continue.old_save"));
                break;
            case ResetEntryKind.Unreadable:
                _reset.OpenNotice(game, UiText.S("saveslots.unreadable_refused"));
                break;
            case ResetEntryKind.Confirm:
                _reset.Open(game, route.Slot!, null);
                break;
        }
    }

    /// <summary>The picker's Reset pick: the confirmation for that slot.</summary>
    private void OpenResetFor(SaveSlot slot) => _reset.Open(slot.Game, slot, SaveSlotScreen.Label(slot));

    /// <summary>Runs a confirmed reset, if one is waiting, and shows its outcome.</summary>
    private void RunResetRequest()
    {
        if (_reset.TakeRequest() is not var (slot, mode))
            return;
        try
        {
            if (slot.Game == GameId.P4)
                GameReset.ResetPhantasie4(slot.Path, mode);
            else
                GameReset.ResetSession(slot.Path, GameDataRepository.DefaultPathFor(slot.Game), mode,
                    GameReset.DosFogFor(slot.Game));
            _reset.ShowResult(mode == ResetMode.Keep ? UiText.S("reset.done_keep") : UiText.S("reset.done_erase"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or KeyNotFoundException
                                       or InvalidOperationException or UnauthorizedAccessException
                                       or System.Text.Json.JsonException or OldSaveFormatException)
        {
            _reset.ShowResult(UiText.F("reset.failed_x", ex.Message));
        }
    }
}
