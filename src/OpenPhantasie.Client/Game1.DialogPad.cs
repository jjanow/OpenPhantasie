using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>"Pad coverage" (s182): the pad on the modal dialogs, the DOS save dialog and the prompts (<see cref="DialogPad"/>).</summary>
public partial class Game1
{
    private readonly DialogPad _dialogPad = new();
    private readonly PadCommandDialog _padCommands = new();
    private Keys? _padCommandKey; // the command picked in _padCommands, sent as its key on the next frame
    private readonly PadPressTracker _scenePadPresses = new(); // I Amiga's victory / defeat screens: presses and releases
    private string? _padEventPrompt; // the event page the pad's pick belongs to (EventPadKey)
    private int _padEventPick; // 0: the pad has not picked on that page yet

    /// <summary>
    /// The pad's command list (<see cref="PadCommandDialog"/>): A in the Guild (and the Title screen's Guild), where A is
    /// nothing else; RT + A on the wilderness / dungeon map (A alone is Fight there; RT alone only turns the right stick
    /// to the terrain key). Opens it and returns true then.
    /// </summary>
    private bool OpenPadCommands(GameSession s, GamePadState pad)
    {
        if (!_input.NewButtons(pad).Contains(Buttons.A))
            return false;
        var asked = s.Mode is GameMode.Guild or GameMode.Title
            || s.Mode is GameMode.Overworld or GameMode.Dungeon && SidebarScroll.RightTriggerHeld(pad);
        if (!asked)
            return false;
        _padCommands.Open(PadCommandDialog.EntriesFor(s.Mode, _settings.GetKey,
            GuildPickDialog.ExaminesRoster(s.Game), TimeLagDialog.IsCommand(s.Mode, _settings.Game(s.Game))));
        if (!_padCommands.IsOpen)
            return false;
        SetFeedback(_message, _padCommands.Help);
        return true;
    }

    /// <summary>Sends the pad list's pick as its key (it then runs as the keyboard's command, this frame).</summary>
    private void InjectPadCommand()
    {
        if (_padCommandKey is not { } key)
            return;
        _padCommandKey = null;
        _input.Inject([key]);
    }

    /// <summary>The keys the pad's new buttons stand for in the open <paramref name="modal"/>.</summary>
    private IEnumerable<Keys> ModalPadKeys(IModalDialog modal, GamePadState pad) =>
        _dialogPad.KeysFor(modal, _input.NewButtons(pad), ModalWheel(modal, _session!),
            modal is IPadYesNo { AsksYesNo: true }, ModalTakesLeftRight(modal), modal.Help, ModalWheel(modal, _session!) is not null);

    /// <summary>
    /// Pad fix B7 (s188): the open step's wheel characters (<see cref="IPadWheel"/>, else what it types,
    /// <see cref="ITypedEntry"/>); null: the step reads no digit / letter that the D-pad cannot reach, so it has no wheel.
    /// </summary>
    public static string? ModalWheel(IModalDialog modal, GameSession session) =>
        modal is IPadWheel w ? w.PadWheel(session) : (modal as ITypedEntry)?.TypingAlphabet;

    /// <summary>
    /// <paramref name="modal"/>'s help line, or the pad's while the pad is in use there. s185: a Y / N step whose help has a
    /// pad wording of its own (Guild New's "A keep  X roll again", Time lag's sound) keeps its help, shown in the pad's words
    /// by <see cref="PadWording.Help"/>, instead of the generic Y / N line.
    /// </summary>
    /// <remarks>s185 live pass: only while the pad is the last input (<see cref="PadWording.Active"/>); a key pressed after
    /// the pad brings the dialog's own keyboard help back (the wheel's line stayed up for the rest of the dialog before).</remarks>
    private string ModalHelp(IModalDialog modal) =>
        !PadWording.Active ? modal.Help
        : modal is IPadYesNo { AsksYesNo: true } && PadWording.PadTextFor(UiText.Table, modal.Help) is not null ? modal.Help
        : _dialogPad.Help(modal, ModalWheel(modal, _session!), modal is IPadYesNo { AsksYesNo: true },
            ModalTakesLeftRight(modal), modal.Help, ModalWheel(modal, _session!) is not null) ?? modal.Help;

    /// <summary>A dialog step whose menu moves with Left / Right (<see cref="IPadArrows"/>): the pad's wheel stays off there.</summary>
    private static bool ModalTakesLeftRight(IModalDialog modal) => modal is IPadArrows { TakesLeftRight: true };

    /// <summary>The DOS save dialog's pad: its Y / N steps take A = Y, X = N; elsewhere the menu pad (A = Return, B = Esc).</summary>
    private IEnumerable<Keys> DosDialogPadKeys(GamePadState pad) =>
        _dosDialog.AsksYesNo ? DialogPad.YesNoKeys(_input.NewButtons(pad)) : InputMap.MenuPadKeys(_input.NewButtons(pad));

    /// <summary>
    /// The pad on a prompt (<see cref="HandlePrompt"/>): the D-pad / stick are the arrows and A is Return. B never gets here
    /// (Esc answers through <see cref="AnswerPromptWithEscape"/> or opens the game menu first), and LB / RB stay the game's
    /// Save / Load (<see cref="HandleGlobalInput"/>), so they do not also scroll the prompt: s183, the right stick up / down
    /// do (PgUp / PgDn, <see cref="PromptScrollPadKeys"/>) when the prompt has more text than its panel holds. III's "Press
    /// a number" event page has no highlight: the D-pad steps a pick shown on the help line and A sends its number
    /// (<see cref="EventPadKeys"/>).
    /// </summary>
    private IEnumerable<Keys> PromptPadKeys(SessionPrompt prompt, GamePadState pad)
    {
        var buttons = _input.NewButtons(pad).ToList();
        var scroll = PromptScrollPadKeys(buttons, UiOverlays.PromptScroll.CanScroll
            || EventPageFor(prompt) is not null && UiOverlays.ScrollFrameScroll.CanScroll).ToList();
        if (EventPageFor(prompt) is { AnyKey: false })
            return scroll.Concat(EventPadKeys(prompt, buttons));
        var keys = PromptPadKeys(buttons).ToList();
        if (keys.Any(k => k is Keys.Up or Keys.Down or Keys.Left or Keys.Right))
            _padPromptMoved = EventPadId(prompt);
        return scroll.Concat(keys);
    }

    /// <summary>
    /// s183 (Pad leftovers): a long prompt's scroll by pad: the right stick up / down as PgUp / PgDn, only when
    /// <paramref name="canScroll"/> (so they never reach the prompt's menu, and the stick never counts as an answer).
    /// </summary>
    public static IEnumerable<Keys> PromptScrollPadKeys(IEnumerable<Buttons> buttons, bool canScroll) =>
        canScroll
            ? buttons.Select(b => b switch
            {
                Buttons.RightThumbstickUp => Keys.PageUp,
                Buttons.RightThumbstickDown => Keys.PageDown,
                _ => Keys.None,
            }).Where(k => k != Keys.None)
            : [];

    private string? _padPromptMoved; // the prompt whose answer the pad's D-pad has moved (PadRequesterMark)

    /// <summary>
    /// s182 live pass: I Amiga's requesters (<see cref="P1MapRequesterFor"/>) draw no highlight, so the D-pad moved an
    /// unseen answer (a fight's find: Right then A dropped the item). Once the pad has moved on <paramref name="prompt"/>,
    /// the button of the answer A takes is outlined, as the fight's pad picks are; null otherwise.
    /// </summary>
    private Rectangle? PadRequesterMark(SessionPrompt prompt, RequesterScene requester)
    {
        if (_padPromptMoved != EventPadId(prompt))
            return null;
        var cursor = _promptMenu.CursorFor(prompt);
        foreach (var b in requester.Buttons)
            if (b.Index == cursor)
                return new Rectangle(b.Rect.X, b.Rect.Y, b.Rect.W, b.Rect.H);
        return null;
    }

    /// <summary>A prompt's pad keys: arrows and Return only (<see cref="PromptPadKeys(SessionPrompt, GamePadState)"/>).</summary>
    public static IEnumerable<Keys> PromptPadKeys(IEnumerable<Buttons> buttons) =>
        InputMap.MenuPadKeys(buttons).Where(k => k is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter);

    private IEnumerable<Keys> EventPadKeys(SessionPrompt prompt, IEnumerable<Buttons> buttons)
    {
        var id = EventPadId(prompt);
        if (id != _padEventPrompt)
        {
            _padEventPrompt = id;
            _padEventPick = 0;
        }
        var (pick, key) = EventPadStep(_padEventPick, prompt.Options.Count, buttons);
        _padEventPick = pick;
        return key is { } k ? [k] : [];
    }

    /// <summary>
    /// III's "Press a number" page by pad: the D-pad / stick step <paramref name="pick"/> through 1…<paramref name="count"/>
    /// (wrapping; the first press shows 1), A sends the pick's number (1 when nothing was picked). Returns the new pick and
    /// the key to send, if any.
    /// </summary>
    public static (int Pick, Keys? Key) EventPadStep(int pick, int count, IEnumerable<Buttons> buttons)
    {
        Keys? send = null;
        foreach (var b in buttons)
        {
            var step = InputMap.MenuPadKey(b) switch
            {
                Keys.Up or Keys.Left => -1,
                Keys.Down or Keys.Right => 1,
                _ => 0,
            };
            if (count < 1)
                continue;
            if (step != 0)
                pick = pick == 0 ? 1 : (pick - 1 + step + count) % count + 1;
            else if (b == Buttons.A && send is null)
                send = Keys.D0 + Math.Clamp(pick == 0 ? 1 : pick, 1, Math.Min(count, 9));
        }
        return (pick, send);
    }

    /// <summary>The event page's help line while the pad has picked on it, or null.</summary>
    private string? EventPadHelp(SessionPrompt prompt) =>
        _padEventPick > 0 && EventPadId(prompt) == _padEventPrompt && _padEventPick <= prompt.Options.Count
            ? UiText.F("pad.event_pick_x_x", _padEventPick, prompt.Options[_padEventPick - 1])
            : null;

    private static string EventPadId(SessionPrompt prompt) =>
        $"{prompt.Kind}\n{string.Join("\n", prompt.Lines)}\n{string.Join("\n", prompt.Options)}";

    /// <summary>The pad's presses and releases on I Amiga's victory / defeat screens (each an input event, as a key's).</summary>
    /// <remarks>
    /// The prompt's buttons only (A, the D-pad): the others are the game's own (X inventory, LB save…) and act as well.</remarks>
    private (int Presses, int Releases) ScenePadEvents(GamePadState pad) =>
        _scenePadPresses.Events(_input.NewButtons(pad).Where(b => PromptPadKeys([b]).Any()), pad);
}
