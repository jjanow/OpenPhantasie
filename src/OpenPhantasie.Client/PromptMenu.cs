using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The highlighted answer of a modal <see cref="SessionPrompt"/>, the DOS way: DOS menus are cursor bars (arrows move,
/// Return picks) and <b>each menu id reopens on the last choice made in it</b> (message-and-text.md §Menus: one selection
/// word per menu at DS <c>0x24 + 16·id</c>; DOSBox s45 sleeping-monsters menu reopened on LEAVE; s58 "A DUNGEON. ENTER?"
/// reopened on NO after a NO, but a NO to SAVE MAP left it on YES). The direct keys the manual names (Y/N, the number, the
/// first letter before the ")") keep working (D-066; DOS ignores Y/N) and are remembered too.
/// </summary>
/// <remarks>
/// Memory key: <see cref="SessionPrompt.MenuId"/>, else the prompt kind plus its option labels. First-ever highlight: the
/// DOS initial value for the ids that have one (<see cref="FirstEverCursor"/>), else the first option. Dungeon event menus
/// never remember (s58: DNG7 ev19 reopened on option 1 each time). YES/NO boxes move with Left/Right only (s58: Down does
/// nothing); other menus wrap both ways (s58: Up on option 1 goes to the last). Kept in memory only (not saved).
/// </remarks>
public sealed class PromptMenu
{
    /// <summary>DOS first-ever highlights that are not the first option (static, message-and-text.md §Menus), by menu id.</summary>
    private static readonly Dictionary<int, int> FirstEverCursor = new()
    {
        [0xa] = 2,  // SPELLS / EQUIPMENT / SKILLS / POTIONS: SKILLS (s58 DOSBox §1)
        [0x18] = 3, // PARTY OPTIONS: FIGHT (CombatText.DefaultPartyOptionIndex)
    };

    private readonly Dictionary<string, int> _last = new();
    private SessionPrompt? _shown;

    /// <summary>Highlighted option (0-based) of the prompt last passed to <see cref="Sync"/>.</summary>
    public int Cursor { get; private set; }

    /// <summary>Call every frame with the open prompt; a newly opened prompt starts on its remembered option.</summary>
    public void Sync(SessionPrompt? prompt)
    {
        if (prompt is null)
        {
            _shown = null;
            return;
        }
        if (_shown is not null && SamePrompt(_shown, prompt))
            return;
        _shown = prompt;
        Cursor = InitialCursor(prompt);
    }

    /// <summary>The highlight to draw for <paramref name="prompt"/> (its remembered option if it has just opened).</summary>
    public int CursorFor(SessionPrompt prompt) =>
        _shown is not null && SamePrompt(_shown, prompt) ? Cursor : InitialCursor(prompt);

    /// <summary>Where <paramref name="prompt"/> opens: the last choice made in that menu, else its first-ever highlight.</summary>
    public int InitialCursor(SessionPrompt prompt)
    {
        if (Remembers(prompt) && _last.TryGetValue(MemoryKey(prompt), out var at) && at < prompt.Options.Count)
            return at;
        if (prompt.InitialChoice is int lit && lit >= 1 && lit <= prompt.Options.Count)
            return lit - 1; // III's menu 0x75a3: the caller's highlight, every time
        return prompt.MenuId is int id && FirstEverCursor.TryGetValue(id, out var first) && first < prompt.Options.Count
            ? first
            : 0;
    }

    /// <summary>
    /// One key for the open prompt: arrows move the highlight, Return/Space picks it, or a direct key picks an answer.
    /// Returns the 1-based answer for <see cref="SessionPrompts.Answer"/>, or null.
    /// </summary>
    public int? HandleKey(SessionPrompt prompt, Keys key)
    {
        Sync(prompt);
        var count = prompt.Options.Count;
        if (count > 1)
        {
            if (IsYesNoBox(prompt) && key is Keys.Up or Keys.Down)
                return null; // s58: a YES/NO box ignores Down (and Up)
            switch (key)
            {
                case Keys.Left or Keys.Up:
                    Cursor = (Cursor + count - 1) % count;
                    return null;
                case Keys.Right or Keys.Down:
                    Cursor = (Cursor + 1) % count;
                    return null;
            }
        }
        var choice = key is Keys.Enter or Keys.Space ? Cursor + 1 : DirectChoice(prompt, key);
        if (choice is { } c)
        {
            Cursor = c - 1;
            if (count > 1 && Remembers(prompt))
                _last[MemoryKey(prompt)] = c - 1;
        }
        return choice;
    }

    /// <summary>The manual's direct keys: Y/N, the first letter of an "X)…" menu, a digit (bank shares: the share count).</summary>
    public static int? DirectChoice(SessionPrompt prompt, Keys key)
    {
        if (prompt.IsYesNo)
            return key switch { Keys.Y => 1, Keys.N => 2, _ => null };
        if (prompt.Options.Count == 1)
            return null;
        if (prompt.Hotkeys is { } letters)
        {
            var at = key is >= Keys.A and <= Keys.Z ? letters.IndexOf((char)('A' + (key - Keys.A))) : -1;
            if (at >= 0)
                return at + 1; // manual p.10: "the appropriate number or the first letter before the )" — digits too
        }
        var digit = key switch
        {
            >= Keys.D0 and <= Keys.D9 => key - Keys.D0,
            >= Keys.NumPad0 and <= Keys.NumPad9 => key - Keys.NumPad0,
            _ => -1,
        };
        if (prompt.IsShares)
        {
            // The options are the share counts themselves ("0".."3"): the key typed is the number of shares.
            var at = prompt.Options.ToList().IndexOf(digit.ToString());
            return at >= 0 ? at + 1 : null;
        }
        return digit >= 1 && digit <= prompt.Options.Count ? digit : null;
    }

    public static string MemoryKey(SessionPrompt prompt) =>
        prompt.MenuId is int id ? $"menu:{id:x}" : $"{prompt.Kind}:{string.Join("|", prompt.Options)}";

    /// <summary>A side-by-side YES/NO box in either order ("INSPECT MEMBER?" is NO / YES).</summary>
    private static bool IsYesNoBox(SessionPrompt prompt) =>
        prompt.Options.Count == 2 && prompt.Options.All(o => o == SessionPrompts.Yes || o == SessionPrompts.No);

    /// <summary>
    /// Dungeon event menus (MESS menus, OV2 <c>0x2a95</c>) open on option 1 every time (s58); so does III's fight find
    /// ("Yes", <see cref="SessionPrompt.Remembers"/>, root <c>0xf2eb</c>).
    /// </summary>
    private static bool Remembers(SessionPrompt prompt) => prompt.Remembers && prompt.Kind != SessionPromptKind.DungeonEvent;

    private static bool SamePrompt(SessionPrompt a, SessionPrompt b) =>
        a.Kind == b.Kind && a.Lines.SequenceEqual(b.Lines) && a.Options.SequenceEqual(b.Options);
}
