using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// III's "Are you sure …?" questions before Load (S447), Save (S390), Distribute ("… redistribute?", <c>DS:0x58e2</c>) and
/// a Purge pick (S407), asked under every III layout (s160, user s159). DOS III asks each through root <c>0x1820</c>(text,
/// 2): the text, then the two-item menu <c>0x75a3</c> with "No" lit every time (Y / Return on Yes = yes, N / Esc / Return
/// on No = no; dungeon-events.md 2.2). The Amiga asks the same lines in window 1 (<c>0xaae2</c>, sound 36 first; OK /
/// CANCEL, Return = OK; FORMAT.md 8.4 / 8.9). The question is a <see cref="SessionPrompt"/> held by the Client (the
/// command it guards is a Client one: load, save, the hand-out screen, the purge list), shown through the prompt path:
/// window 1 when <see cref="InWindow"/> (D3-068's split: Return = OK), the DOS Yes / No prompt otherwise (Return = the
/// lit "No"). Esc is No in both.
/// </summary>
public sealed class P3ConfirmQuestion
{
    /// <summary>The question as a prompt (rows split at "\r", as DOS and the Amiga break them); null when not up.</summary>
    public SessionPrompt? Prompt { get; private set; }

    /// <summary>The command the question guards (<c>load</c>, <c>save</c>, <c>distribute</c>, <c>purge</c>).</summary>
    public string Action { get; private set; } = "";

    public bool Shown => Prompt is not null;

    /// <summary>The question's rows.</summary>
    public IReadOnlyList<string> Lines => Prompt?.Lines ?? [];

    /// <summary>
    /// The prompt for <paramref name="text"/>: Yes / No, "No" lit on every opening (root <c>0x1820</c>'s lit = 2, no
    /// memory), Esc = No.
    /// </summary>
    public static SessionPrompt PromptFor(string text) =>
        new(SessionPromptKind.None, (text ?? "").Split('\r', '\n'), [SessionPrompts.Yes, SessionPrompts.No])
            { Remembers = false, InitialChoice = 2, EscapeChoice = 2 };

    /// <summary>
    /// Whether the question shows in the Amiga's window 1 (OK / CANCEL, Return = OK): III, the game's AMIGA SCREENS switch
    /// on and a pack with window 1 and the OK / CANCEL art. Otherwise the DOS Yes / No prompt (Return = "No").
    /// </summary>
    public static bool InWindow(GameId game, bool originalScreens, bool packHasWindow1) =>
        game == GameId.P3 && originalScreens && packHasWindow1;

    public void Open(string text, string action)
    {
        Prompt = PromptFor(text);
        Action = action ?? "";
    }

    public void Close()
    {
        Prompt = null;
        Action = "";
    }

    /// <summary>True when <paramref name="prompt"/> is this question (the one the prompt path is showing).</summary>
    public bool Owns(SessionPrompt? prompt) => prompt is not null && ReferenceEquals(prompt, Prompt);

    /// <summary>
    /// The answer (1 = Yes / OK, 2 = No / CANCEL): closes the question and returns its action with whether it was taken.
    /// </summary>
    public (string Action, bool Yes) Answer(int choice)
    {
        var action = Action;
        Close();
        return (action, choice == 1);
    }

    /// <summary>
    /// One key's answer (1 = Yes, 2 = No) in the question's look, or null (the key moves the highlight or is dropped): in
    /// window 1 the Amiga's keys (<see cref="Game1.KeepWindowChoice"/>; Esc = CANCEL); in the DOS prompt the menu's keys
    /// through <paramref name="menu"/> (Left / Right move, Return takes the lit word, Y / N), Esc = No.
    /// </summary>
    public int? Choice(Keys key, bool inWindow, PromptMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (Prompt is not { } prompt)
            return null;
        if (key == Keys.Escape)
            return prompt.EscapeChoice;
        return inWindow ? Game1.KeepWindowChoice(key) : menu.HandleKey(prompt, key);
    }
}
