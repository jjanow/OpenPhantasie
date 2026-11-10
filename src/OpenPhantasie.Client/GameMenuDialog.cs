using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>What a <see cref="GameMenuDialog"/> pick asks the shell to do.</summary>
public enum GameMenuChoice
{
    Resume,
    Save,
    Load,
    DosImport,
    DosExport,
    /// <summary>
    /// T5: "Transfer from II": Phantasie I's row (the ST utility's II → I; <c>transfer.md</c> section 8) and III's
    /// (D3-004, unless <c>p3.transfer-source</c> = <c>dos</c>).
    /// </summary>
    TransferFromII,
    /// <summary>T5: Phantasie III's "Transfer from I" row (D3-030: the manual's utilities option, any time).</summary>
    TransferFromI,
    /// <summary>The Options screen on the running game's tab (T5.0; its QoL switches, the save's rules read-only).</summary>
    Options,
    /// <summary>Back to the main menu (asks Y/N first, like Quit).</summary>
    MainMenu,
    Quit,
}

/// <summary>
/// The remaster's game menu (not in DOS): Esc where there is no parent menu to back out to opens it. Arrows + Return pick,
/// Esc resumes; Main menu and Quit ask Y/N first. The shell reads the pick with <see cref="TakeChoice"/> once the menu has
/// closed.
/// </summary>
public sealed class GameMenuDialog : IModalDialog, IPadYesNo
{
    public static string Title => UiText.S("gamemenu.game_menu");
    public static string QuitQuestion => UiText.S("gamemenu.quit_the_game_y_n");
    public static string MainMenuQuestion => UiText.S("gamemenu.back_to_the_main_menu_y_n");

    /// <summary>
    /// The rows for <paramref name="game"/> (T5): Phantasie I adds "Transfer from II" under the DOS rows; Phantasie III adds
    /// "Transfer from I" and, unless its <paramref name="options"/> take I only (D3-004), "Transfer from II".
    /// </summary>
    public static (string Label, GameMenuChoice Choice)[] EntriesFor(GameId game, GameOptions? options = null) =>
    [
        (UiText.S("gamemenu.resume"), GameMenuChoice.Resume),
        (UiText.S("gamemenu.save_game_f5"), GameMenuChoice.Save),
        (UiText.S("gamemenu.load_game_f9"), GameMenuChoice.Load),
        (UiText.S("gamemenu.import_dos_save_f7"), GameMenuChoice.DosImport),
        (UiText.S("gamemenu.export_dos_save_f6"), GameMenuChoice.DosExport),
        .. TransferRows(game, options),
        (UiText.S("gamemenu.options"), GameMenuChoice.Options),
        (UiText.S("gamemenu.main_menu"), GameMenuChoice.MainMenu),
        (UiText.S("gamemenu.quit_game"), GameMenuChoice.Quit),
    ];

    private static (string, GameMenuChoice)[] TransferRows(GameId game, GameOptions? options) => game switch
    {
        GameId.P1 => [(UiText.S("gamemenu.transfer_from_ii"), GameMenuChoice.TransferFromII)],
        GameId.P3 when options?.P3Transfer().FromIIAllowed == false =>
            [(UiText.S("gamemenu.transfer_from_i"), GameMenuChoice.TransferFromI)],
        GameId.P3 =>
        [
            (UiText.S("gamemenu.transfer_from_i"), GameMenuChoice.TransferFromI),
            (UiText.S("gamemenu.transfer_from_ii"), GameMenuChoice.TransferFromII),
        ],
        _ => [],
    };

    /// <summary>Main menu or Quit waiting for its Y/N, else null.</summary>
    private GameMenuChoice? _confirm;
    private GameMenuChoice? _choice;

    public bool IsOpen { get; private set; }

    public int Cursor { get; private set; }

    /// <summary>The Quit / Main Menu question is up (Y answers it; Return does not).</summary>
    public bool AsksYesNo => IsOpen && _confirm is not null;

    public string Help => _confirm is not null ? UiText.S("gamemenu.y_yes_n_esc_back") : UiText.S("gamemenu.up_down_enter_select_esc_resume");

    /// <summary>Opens on Resume.</summary>
    public void Open()
    {
        IsOpen = true;
        Cursor = 0;
        _confirm = null;
        _choice = null;
    }

    /// <summary>The pick, once, after the menu closed on it (null after Esc / Resume or when already taken).</summary>
    public GameMenuChoice? TakeChoice()
    {
        var choice = _choice;
        _choice = null;
        return choice;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) =>
        _confirm is { } asked
            ? [Title, "", asked == GameMenuChoice.Quit ? QuitQuestion : MainMenuQuestion, UiText.S("gamemenu.unsaved_progress_is_lost")]
            : [Title, "", .. EntriesFor(session.Game, session.Options).Select((e, i) => (i == Cursor ? ">" : " ") + e.Label)];

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        if (!IsOpen)
            return null;
        var entries = EntriesFor(session.Game, session.Options);
        foreach (var key in newKeys)
        {
            if (_confirm is { } asked)
            {
                if (key == Keys.Y)
                    return Close(asked);
                if (key is Keys.N or Keys.Escape or Keys.Back)
                    _confirm = null;
                continue;
            }
            switch (key)
            {
                case Keys.Escape or Keys.Back:
                    return Close(null);
                case Keys.Up:
                    Cursor = (Cursor + entries.Length - 1) % entries.Length;
                    break;
                case Keys.Down:
                    Cursor = (Cursor + 1) % entries.Length;
                    break;
                case Keys.Enter or Keys.Space:
                    var picked = entries[Cursor].Choice;
                    if (picked is GameMenuChoice.Quit or GameMenuChoice.MainMenu)
                    {
                        _confirm = picked;
                        break;
                    }
                    return Close(picked == GameMenuChoice.Resume ? null : picked);
            }
        }
        return null;
    }

    private string Close(GameMenuChoice? choice)
    {
        IsOpen = false;
        _choice = choice;
        return "";
    }
}
