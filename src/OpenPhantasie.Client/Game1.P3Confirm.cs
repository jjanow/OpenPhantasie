using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s160 (user s159): III's "Are you sure …?" questions before Load, Save, Distribute and a Purge pick under every III layout
/// (<see cref="P3ConfirmQuestion"/>): the Amiga town bar, the remaster's DOS row, the Guild's keys, the game menu and the
/// Save / Load keys in a town. The question goes through the prompt path (<see cref="CurrentPrompt"/>): window 1 under the
/// Amiga look, DOS's Yes / No prompt with "No" lit otherwise.
/// </summary>
public partial class Game1
{
    private readonly P3ConfirmQuestion _confirm = new();

    /// <summary>The question shows in window 1 (<see cref="P3ConfirmQuestion.InWindow"/>).</summary>
    private bool ConfirmInWindow =>
        _session is { } s && P3ConfirmQuestion.InWindow(s.Game, CurrentGameSettings.OriginalScreens, ConfirmWindowLayout() is not null);

    /// <summary>
    /// Whether <paramref name="action"/> asks first: III only. DOS III offers Load / Save only in a town's Options (root
    /// <c>0x8348</c>), so the game menu's and the keys' Load / Save ask in the town and the Guild; outside a town they are the
    /// remaster's own and ask nothing. Distribute and Purge are town / Guild commands and always ask.
    /// </summary>
    public static bool AsksFirst(GameId game, GameMode mode, string action) =>
        game == GameId.P3 && action switch
        {
            "load" or "save" => mode is GameMode.Town or GameMode.Guild,
            "distribute" or "purge" => true,
            _ => false,
        };

    /// <summary>The question for <paramref name="action"/> (DOS III's lines, the Amiga's too; "\r" breaks the rows).</summary>
    public static string ConfirmText(string action) => action switch
    {
        "load" => UiText.S("p3bar.confirm_load"), // S447
        "save" => UiText.S("p3bar.confirm_save"), // S390
        "distribute" => UiText.S("p3bar.confirm_distribute"), // DS:0x58e2
        "purge" => GuildPickDialog.PurgeQuestion, // S407
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    /// <summary>Asks before <paramref name="action"/> when III does (sound 36 first, 0xaaf2); false = run it now.</summary>
    private bool AskConfirm(GameSession s, string action)
    {
        if (!AsksFirst(s.Game, s.Mode, action))
            return false;
        _confirm.Open(ConfirmText(action), action);
        s.AskConfirmQuestion();
        return true;
    }

    /// <summary>The game menu's / the Save key's save: III's town asks first.</summary>
    private void SaveOrAsk(GameSession s)
    {
        if (_confirm.Shown || AskConfirm(s, "save"))
            return;
        SaveGame();
    }

    /// <summary>The game menu's / the Load key's load: III's town asks first.</summary>
    private void LoadOrAsk(GameSession s)
    {
        if (_confirm.Shown || AskConfirm(s, "load"))
            return;
        LoadGame();
    }

    /// <summary>
    /// A Guild list's purge pick waiting for its question (<see cref="GuildPickDialog.PurgeAsked"/>): asks S407.
    /// </summary>
    private void OfferPurgeQuestion(GameSession s)
    {
        if (_guildPickDialog.PurgeAsked is null || _confirm.Shown)
            return;
        if (!AskConfirm(s, "purge"))
            AnswerPurge(s, yes: true); // a game that does not ask (no caller today)
    }

    /// <summary>The answer to the question: Yes runs its command; No drops it (a purge's list comes back).</summary>
    private void AnswerConfirm(GameSession s, int choice)
    {
        var (action, yes) = _confirm.Answer(choice);
        switch (action)
        {
            case "load" when yes:
                LoadGame();
                break;
            case "save" when yes:
                SaveGame();
                _messageBand.Restart();
                break;
            case "distribute" when yes:
                RunTownChoice(s, TownChoice.Distribute, null, confirmed: true);
                break;
            case "purge":
                AnswerPurge(s, yes);
                break;
        }
    }

    private void AnswerPurge(GameSession s, bool yes)
    {
        if (_guildPickDialog.AnswerPurge(s, yes) is not { } line)
            return;
        var open = OpenModal;
        SetFeedback(line.Length > 0 ? line : _message, open?.Help ?? HelpForMode(s.Mode));
        if (line.Length > 0)
        {
            _messageBand.Restart();
            OfferAmigaMessage(line);
        }
    }
}
