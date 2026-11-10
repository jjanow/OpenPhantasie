using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// "Pad coverage" (s182): the remaster's pad-only command list for screens whose commands are letter keys only: the Guild
/// (A)DD, D)ROP, N)EW, P)URGE, R)ENAME, L)IST, Examine, S)PELLS, T)RAINING, E)XIT GUILD (s183); the Title screen's Add / Drop) and the
/// wilderness / dungeon (F)IGHT, C)AST, I)NSPECT, U)SE POTION, H)EALTH, T)IME LAG, L)OOK); s192 (pad fix C15) the town
/// row's D)ISTRIBUTE, U)SE, I)NSPECT, T)RADE, S)AVE. Opened by the pad only (A in the Guild, RT + A in the field, Back on
/// I–III's DOS town row; Game1.DialogPad.cs); a pick sends that command's key on the next frame (after the
/// bars' and II ST's field-click checks, which it skips), so it opens what the keyboard's letter opens. Up / Down move, Return picks, Esc closes. No original has it: it adds nothing a keyboard
/// player sees.
/// </summary>
public sealed class PadCommandDialog : IModalDialog
{
    public sealed record Entry(string Label, Keys Key);

    private IReadOnlyList<Entry> _entries = [];
    private Keys? _chosen;

    public bool IsOpen { get; private set; }

    public int Cursor { get; private set; }

    public IReadOnlyList<Entry> Entries => _entries;

    public string Help => UiText.S("pad.cmd.help");

    public void Open(IReadOnlyList<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        Cursor = 0;
        _chosen = null;
        IsOpen = entries.Count > 0;
    }

    /// <summary>The picked command's key, once.</summary>
    public Keys? TakeChoice()
    {
        var chosen = _chosen;
        _chosen = null;
        return chosen;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) =>
        [UiText.S("pad.cmd.title"), "", .. _entries.Select((e, i) => (i == Cursor ? ">" : " ") + e.Label)];

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        if (!IsOpen)
            return null;
        foreach (var key in newKeys)
        {
            switch (key)
            {
                case Keys.Escape or Keys.Back:
                    IsOpen = false;
                    return "";
                case Keys.Up:
                    Cursor = (Cursor + _entries.Count - 1) % _entries.Count;
                    break;
                case Keys.Down:
                    Cursor = (Cursor + 1) % _entries.Count;
                    break;
                case Keys.Enter or Keys.Space:
                    IsOpen = false;
                    _chosen = _entries[Cursor].Key;
                    return "";
            }
        }
        return null;
    }

    /// <summary>
    /// The commands <paramref name="mode"/> takes by letter (as Game1's OpenModalDialog / HandleGameplayInput read them),
    /// with their bound keys (<paramref name="keyFor"/>). <paramref name="examine"/>: III's Guild Examine;
    /// <paramref name="timeLag"/>: T is a field command (D-073 off); <paramref name="trade"/>: the town offers III's T Trade;
    /// <paramref name="info"/>: III Amiga's Guild takes ? for File › Info (s192, pad fix C16).
    /// </summary>
    public static IReadOnlyList<Entry> EntriesFor(GameMode mode, Func<GameAction, Keys> keyFor, bool examine, bool timeLag,
        bool trade = false, bool info = false)
    {
        ArgumentNullException.ThrowIfNull(keyFor);
        var list = new List<(string Label, Keys Key)>();
        switch (mode)
        {
            case GameMode.Guild:
                list.Add((UiText.S("pad.cmd.add"), keyFor(GameAction.GuildAdd)));
                list.Add((UiText.S("pad.cmd.drop"), keyFor(GameAction.GuildDrop)));
                list.Add((UiText.S("pad.cmd.new"), keyFor(GameAction.GuildNew)));
                list.Add((UiText.S("pad.cmd.list"), keyFor(GameAction.GuildList)));
                list.Add((UiText.S("pad.cmd.rename"), keyFor(GameAction.GuildRename)));
                list.Add((UiText.S("pad.cmd.purge"), keyFor(GameAction.GuildPurge)));
                if (examine)
                    list.Add((UiText.S("pad.cmd.examine"), keyFor(GameAction.GuildExamine)));
                list.Add((UiText.S("pad.cmd.spells"), Keys.S)); // manual p.4 S)PELLS, T)RAINING: fixed letters
                list.Add((UiText.S("pad.cmd.training"), Keys.T));
                if (info)
                    list.Add((UiText.S("pad.cmd.info"), Keys.OemQuestion)); // s192 (C16): the street bar's File › Info
                list.Add((UiText.S("pad.cmd.exit"), Keys.E)); // s183: manual p.4 E)XIT GUILD (B leaves too)
                break;
            case GameMode.Title:
                list.Add((UiText.S("pad.cmd.add"), keyFor(GameAction.GuildAdd)));
                list.Add((UiText.S("pad.cmd.drop"), keyFor(GameAction.GuildDrop)));
                break;
            case GameMode.Town: // s192 (C15): the manual's town letters (D-066, TownMenu.PickLetter), D first
                list.Add((UiText.S("pad.cmd.distribute"), Keys.D));
                list.Add((UiText.S("pad.cmd.use"), Keys.U));
                list.Add((UiText.S("pad.cmd.inspect"), Keys.I));
                if (trade)
                    list.Add((UiText.S("pad.cmd.trade"), Keys.T));
                list.Add((UiText.S("pad.cmd.save"), Keys.S));
                break;
            case GameMode.Overworld or GameMode.Dungeon:
                list.Add((UiText.S("pad.cmd.fight"), keyFor(GameAction.Fight)));
                list.Add((UiText.S("pad.cmd.cast"), keyFor(GameAction.CastSpell)));
                list.Add((UiText.S("pad.cmd.inspect"), keyFor(GameAction.Inspect)));
                list.Add((UiText.S("pad.cmd.potion"), keyFor(GameAction.UsePotion)));
                list.Add((UiText.S("pad.cmd.health"), keyFor(GameAction.Health)));
                if (mode == GameMode.Dungeon)
                    list.Add((UiText.S("pad.cmd.look"), keyFor(GameAction.Look)));
                if (timeLag)
                    list.Add((UiText.S("pad.cmd.timelag"), keyFor(GameAction.TimeLag)));
                break;
        }
        return list.Where(e => e.Key != Keys.None).Select(e => new Entry(e.Label, e.Key)).ToList();
    }
}
