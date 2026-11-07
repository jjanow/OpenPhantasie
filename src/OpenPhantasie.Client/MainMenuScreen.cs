using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>How a <see cref="MenuLine"/> is drawn.</summary>
public enum MenuLineStyle
{
    Title,
    Header,
    Normal,
    Selected,
    Dim,
}

/// <summary>
/// One line of a full-screen remaster menu (<see cref="UiOverlays.DrawMenuScreen"/>). <paramref name="Icon"/>: a pack
/// picture drawn left of the text (IV's item icons, <c>p4.item.N</c>; only the IV screen's box draws it), null = none.
/// <paramref name="Lit"/> (s158, IV's box only): an icon row drawn highlighted (Share out's row 0, <c>_wxbox</c> XOR 15).
/// </summary>
public readonly record struct MenuLine(string Text, MenuLineStyle Style, string? Icon = null, bool Lit = false);

/// <summary>What a <see cref="MainMenuScreen"/> pick asks the shell to do.</summary>
public enum MainMenuChoice
{
    Play,
    Options,
    Quit,
}

/// <summary>
/// The remaster's start screen (T5.0, <c>09-startup-options.md</c> §1): pick one of the four games, Options or Quit. A shell
/// state, not a <see cref="GameMode"/>: no session exists until a game is picked. A game that can't be started yet (or
/// whose data folder is missing) is listed but can't be picked.
/// </summary>
public sealed class MainMenuScreen
{
    public static IReadOnlyList<GameId> Games { get; } = [GameId.P1, GameId.P2, GameId.P3, GameId.P4];

    private readonly Func<GameId, bool> _playable;

    /// <param name="playable">Whether a game can be started; null = <see cref="IsPlayable"/>.</param>
    public MainMenuScreen(Func<GameId, bool>? playable = null)
    {
        _playable = playable ?? IsPlayable;
        Cursor = Math.Max(0, Games.ToList().FindIndex(g => _playable(g)));
    }

    /// <summary>A game that can be started (<see cref="Campaigns.IsPlayable"/>) and its data present: imported from the
    /// player's copy, or (dev build) the committed GameData (<see cref="GameDataLocator.IsAvailable"/>).</summary>
    public static bool IsPlayable(GameId game) =>
        Campaigns.IsPlayable(game) && GameDataLocator.IsAvailable(game);

    /// <summary>Rows: the four games, then Options, then Quit.</summary>
    public int Cursor { get; private set; }

    private int Count => Games.Count + 2;

    public string Help => UiText.S("menu.help");

    /// <summary>A pick (game with <see cref="MainMenuChoice.Play"/>), or null when nothing was picked this frame.</summary>
    public (MainMenuChoice Choice, GameId Game)? HandleKeys(IEnumerable<Keys> newKeys)
    {
        foreach (var key in newKeys)
        {
            switch (key)
            {
                case Keys.Up:
                    Cursor = (Cursor + Count - 1) % Count;
                    break;
                case Keys.Down:
                    Cursor = (Cursor + 1) % Count;
                    break;
                case Keys.Enter or Keys.Space:
                    if (Cursor < Games.Count)
                    {
                        if (_playable(Games[Cursor]))
                            return (MainMenuChoice.Play, Games[Cursor]);
                        break;
                    }
                    return (Cursor == Games.Count ? MainMenuChoice.Options : MainMenuChoice.Quit, GameId.P1);
            }
        }
        return null;
    }

    /// <summary>Mouse: the row drawn on line <paramref name="line"/> of <see cref="Lines"/>, or null for the title and gaps.</summary>
    public int? RowOfLine(int line)
    {
        if (line >= 2 && line < 2 + Games.Count)
            return line - 2;
        var below = line - (3 + Games.Count); // a gap line, then Options, Quit
        return below is 0 or 1 ? Games.Count + below : null;
    }

    public IReadOnlyList<MenuLine> Lines()
    {
        var lines = new List<MenuLine> { new(UiText.S("menu.title"), MenuLineStyle.Title), new("", MenuLineStyle.Normal) };
        for (var i = 0; i < Games.Count; i++)
        {
            var game = Games[i];
            var name = UiText.S("menu.game." + game.Prefix());
            if (_playable(game))
                lines.Add(Row(i, name));
            else
                lines.Add(new((i == Cursor ? ">" : " ") + $"{name,-18}" + (Campaigns.IsPlayable(game) && !GameDataLocator.IsAvailable(game) ? UiText.S("menu.not_imported") : UiText.S("menu.not_built_yet")),
                    i == Cursor ? MenuLineStyle.Selected : MenuLineStyle.Dim));
        }
        lines.Add(new("", MenuLineStyle.Normal));
        lines.Add(Row(Games.Count, UiText.S("menu.options")));
        lines.Add(Row(Games.Count + 1, UiText.S("menu.quit")));
        return lines;
    }

    private MenuLine Row(int index, string text) =>
        index == Cursor ? new(">" + text, MenuLineStyle.Selected) : new(" " + text, MenuLineStyle.Normal);
}
