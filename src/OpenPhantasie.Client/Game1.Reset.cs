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
        var dir = ClientSettings.DataDirectory;
        if (SaveSlots.Slotted(game, _settings.Game(game).SaveSlots))
        {
            if (!SaveSlots.All(dir, game).Any(s => File.Exists(s.Path)))
            {
                _reset.OpenNotice(game, UiText.S("reset.no_save"));
                return;
            }
            OpenSaveSlots(game, SaveSlotMode.Reset);
            return;
        }
        var single = new SaveSlot(game, SaveSlots.FirstNumber(game), SaveSlots.SinglePath(dir, game));
        var info = SaveSlots.Describe(single);
        if (info.Empty)
            _reset.OpenNotice(game, UiText.S("reset.no_save"));
        else if (info.Unreadable)
            _reset.OpenNotice(game, info.Old ? UiText.S("continue.old_save") : UiText.S("saveslots.unreadable_refused"));
        else
            _reset.Open(game, single, null);
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
