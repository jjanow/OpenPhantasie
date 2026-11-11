using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// "Pad coverage" (s182): IV's screens read keys only (<see cref="HandleKeys"/>), and IV gives Return, Space and Esc
/// different meanings from screen to screen (Space is back in a pick list and closes the System menu, Esc asks for the
/// main menu), so the pad's keys follow the prompt up now instead of <see cref="InputMap.MenuPadKeys"/>' fixed map:
/// <list type="bullet">
/// <item>A: Y at a yes / no question, Space on the System's About page, the chosen order in a fight's orders (a blow, else
/// Parry, until X / Y picks another), else Return (every SPACE wait, list pick, typed prompt's end, the target's square).</item>
/// <item>B: N at a question, Space where a list or the System page goes back, Backspace in a fight's target / spell /
/// member pick and in a typed prompt, else Esc (the main-menu question).</item>
/// <item>D-pad / left stick: the arrows (the cursor, a step); on a map or in a fight's orders the perpendicular held with
/// it gives the diagonal (Home / PgUp / End / PgDn, <see cref="ButtonOf"/>).</item>
/// <item>LB / RB: F5 / F9 (save / load, as <see cref="GameAction.Save"/> / <see cref="GameAction.Load"/> in I–III; never
/// PgUp / PgDn, which are moves here). Start: Esc.</item>
/// <item>Y / X: the next / previous button the mouse could click (the Command panel's icons, the SPACE bar, the keypad, a
/// building's or an item window's buttons), lit as under the pointer; A then clicks it, B lets it go. In a fight's orders
/// (no buttons there) they pick the order A gives.</item>
/// </list>
/// s183 "Pad leftovers": a typed prompt (a name, the town's amounts, the dungeon's Letter / Number) gets I–III's pad wheel
/// (<see cref="DialogPad"/>): D-pad / stick Left / Right step a character, Up / Down jump ten (one among the digits), X
/// types it, Y deletes (as B), A ends the entry. The left stick goes through <see cref="StickQuantizer"/> (one direction per
/// push). While the pad is the input in use (<see cref="PadInput"/>) the help line names the pad's buttons.
/// s185: every pad help line where B is Esc names it ("B/Start main menu"), and the main-menu question names A / B; a
/// share-out takes Left / Right for the share A gives (<see cref="PadShare"/>), so it needs no panel buttons.
/// Keyboard keys are untouched; the pad only adds keys.
/// </summary>
public sealed partial class P4DungeonScreen
{
    private int? _padFocus;
    private (object? Tag, Rectangle Bounds)[] _padSeen = [];
    private P4ActionKind? _padAction;
    private readonly DialogPad _wheel = new();
    private object? _wheelOwner; // one per typed prompt: the wheel starts again on the next one
    private readonly StickQuantizer _stick = new();
    private int _padShare = 1;
    private bool _padAutoMove; // s191 (pad fix D26): the pad's pick in a fight's orders is Q auto move
    private IReadOnlyList<(string Label, Keys Key)>? _padCommands; // s191 (pad fix D24): the open command list
    private int _padCommandCursor;

    /// <summary>
    /// s191 (pad fix D24): the commands a command prompt takes by letter, for the pad's command list (Back) where the screen
    /// has no buttons for Y / X (no art pack's command panel): the street's T / D / F / C / U / S (S is F10 under COMMON
    /// TOWN KEYS, where S saves), outdoors' M / F / P / C / U / S, a location map's E / F / P / C / U / S. Null elsewhere.
    /// </summary>
    public IReadOnlyList<(string Label, Keys Key)>? PadCommandEntries
    {
        get
        {
            if (MainMenuAsked || !Game.LoopBuilt || _system is not null || Game.Battle is not null || MenuOpen
                || DoorOpen || MysticBusy || WelcomeUp || _padSeen.Length > 0)
                return null;
            static (string, Keys) Cmd(string label, Keys key) => (label, key);
            (string, Keys)[] party = [Cmd(UiText.S("p4.pad.cmd.profile"), Keys.F), Cmd(UiText.S("p4.pad.cmd.party"), Keys.P), Cmd(UiText.S("p4.pad.cmd.cast"), Keys.C), Cmd(UiText.S("p4.pad.cmd.use"), Keys.U),
                Cmd(UiText.S("p4.pad.cmd.system"), Keys.S)];
            return Game.Place switch
            {
                P4Place.Town when OnTownStreet => [Cmd(UiText.S("p4.pad.cmd.trade"), Keys.T), Cmd(UiText.S("p4.pad.cmd.share"), Keys.D), Cmd(UiText.S("p4.pad.cmd.profile"), Keys.F),
                    Cmd(UiText.S("p4.pad.cmd.cast"), Keys.C), Cmd(UiText.S("p4.pad.cmd.use"), Keys.U), CommonTownKeys ? Cmd(UiText.S("p4.pad.cmd.system_common"), SystemKeyCommon) : Cmd(UiText.S("p4.pad.cmd.system"), Keys.S)],
                P4Place.Outdoors when O.Prompt == P4OutdoorPrompt.Action => [Cmd(UiText.S("p4.pad.cmd.map"), Keys.M), .. party],
                P4Place.Dungeon when D.Prompt == P4DungeonPrompt.Action => [Cmd(UiText.S("p4.pad.cmd.examine"), Keys.E), .. party],
                _ => null,
            };
        }
    }

    /// <summary>The pad's command list while open (its labels; <see cref="PadCommandCursor"/> on one), else null.</summary>
    public IReadOnlyList<(string Label, Keys Key)>? PadCommands => _padCommands;

    /// <summary>The command list's cursor line.</summary>
    public int PadCommandCursor => _padCommandCursor;

    /// <summary>
    /// The pad's buttons while its command list is open: D-pad / stick Up / Down move, A sends the line's key (and closes
    /// the list), B or Back close it. Every other button does nothing there.
    /// </summary>
    private void PadCommandButtons(IReadOnlyCollection<Buttons> pressed, List<Keys> keys)
    {
        var list = _padCommands!;
        foreach (var b in pressed)
            switch (b)
            {
                case Buttons.DPadUp or Buttons.LeftThumbstickUp:
                    _padCommandCursor = (_padCommandCursor + list.Count - 1) % list.Count;
                    break;
                case Buttons.DPadDown or Buttons.LeftThumbstickDown:
                    _padCommandCursor = (_padCommandCursor + 1) % list.Count;
                    break;
                case Buttons.A:
                    keys.Add(list[_padCommandCursor].Key);
                    _padCommands = null;
                    return;
                case Buttons.B or Buttons.Back:
                    _padCommands = null;
                    return;
            }
    }

    /// <summary>
    /// s185 (Pad leftovers): the share the pad's A gives the member whose turn it is in a share-out (the bank's
    /// <c>_deposit</c>, the Town Gate's <c>_gatebank</c>), or null outside one. Without the art pack's panel there are no
    /// share buttons for Y / X to pick, so D-pad / stick Left / Right set it (0–3; never 0 for a member in good health, whom
    /// the original's "0" button is off for: <see cref="GiveShare"/>). It starts at 1 in each share-out and stays between
    /// its members, so one share for all is A, A, A. The help line shows it.
    /// </summary>
    public int? PadShare
    {
        get
        {
            if (MainMenuAsked || !Game.LoopBuilt || SystemUp || Game.Battle is not null || MenuOpen
                || Game.Place != P4Place.Town || W.Prompt != P4TownPrompt.Shares)
                return null;
            return Math.Max(_padShare, PadShareMin);
        }
    }

    /// <summary>The least share the member whose turn it is may take: 0 only when he is not Okay.</summary>
    private int PadShareMin => _shares.Length < Game.Party.Members.Count
        && Game.Party.Members[_shares.Length].Condition != P4Member.Okay ? 0 : 1;

    /// <summary>Left (−1) / Right (+1) in a share-out: the pad's share one down / up, kept within the member's 0–3.</summary>
    private void StepPadShare(int step) => _padShare = Math.Clamp((PadShare ?? 1) + step, PadShareMin, 3);

    /// <summary>
    /// The pad is the input in use (a button was pressed since the last key or mouse move): the help line names the pad's
    /// buttons. s185: this is <see cref="PadWording.Active"/>, the one flag every game's help follows (the shell's
    /// <see cref="PadWording.Note"/> sets and clears it each frame; IV kept its own copy beside it until s185).
    /// </summary>
    public bool PadInput
    {
        get => PadWording.Active;
        set => PadWording.Active = value;
    }

    /// <summary>The typed prompt's characters for the pad wheel (names and Letter: <see cref="NameEntry.Alphabet"/>; amounts
    /// and Number: digits); null when no typed prompt takes keys now.</summary>
    public string? PadTypingAlphabet => MainMenuAsked || !Game.LoopBuilt || SystemUp || Game.Battle is not null || MenuOpen
        || DoorOpen || MysticBusy || WelcomeUp ? null
        : Game.Place switch
        {
            P4Place.Town => W.Prompt switch
            {
                P4TownPrompt.Text => NameEntry.Alphabet,
                P4TownPrompt.Number => DialogPad.Digits,
                _ => null,
            },
            P4Place.Dungeon => D.Prompt switch
            {
                P4DungeonPrompt.Letters => NameEntry.Alphabet,
                P4DungeonPrompt.Number => DialogPad.Digits,
                _ => null,
            },
            _ => null,
        };

    /// <summary>The pad wheel's character in the open typed prompt (shown on the help line).</summary>
    public char PadWheelChar
    {
        get
        {
            SyncWheel(PadTypingAlphabet);
            return _wheel.Current;
        }
    }

    private object? _wheelPrompt; // the typed prompt the wheel's owner belongs to

    /// <summary>
    /// A new typed prompt (another place / prompt / alphabet, or the last one ended by the pad's A) gets a new wheel owner,
    /// so the wheel starts again on its first character (1 among the digits).
    /// </summary>
    private void SyncWheel(string? alphabet)
    {
        if (alphabet is null)
        {
            _wheelOwner = null;
            _wheelPrompt = null;
            return;
        }
        object prompt = (Game.Place, Game.Place == P4Place.Town ? (int)W.Prompt : (int)D.Prompt, alphabet);
        if (_wheelOwner is not null && Equals(prompt, _wheelPrompt))
            return;
        _wheelOwner = new object();
        _wheelPrompt = prompt;
        _wheel.KeysFor(_wheelOwner, [], alphabet); // reset onto the alphabet, nothing pressed
    }

    /// <summary>
    /// s183: the help line by pad buttons (<see cref="PadInput"/>), as <see cref="PadConfirmKey"/> / <see cref="PadBackKey"/>
    /// and the Y / X pick map them on the prompt up now; null where the busy waits read nothing.
    /// </summary>
    private string? PadHelp
    {
        get
        {
            if (MysticBusy && MysticSkippable && !MainMenuAsked)
                return UiText.S("p4.pad.help_mystic_skip"); // s193 (pad fix E28): QoL MYSTIC SKIP's A
            if (DoorOpen || MysticBusy || WelcomeUp || MainMenuAsked)
                return null;
            if (_padCommands is not null)
                return UiText.S("p4.pad.help_commands");
            if (_padFocus is not null)
                return UiText.S("p4.pad.help_focus");
            if (PadTypingAlphabet is not null)
                return UiText.F("p4.pad.help_type_x", PadWheelChar == ' ' ? UiText.S("typing.space") : PadWheelChar.ToString());
            if (!Game.LoopBuilt)
                return UiText.S("p4.pad.help_left");
            if (SystemUp && SystemChoices is not null)
                return _system == P4SystemPage.SelectGameFinal ? UiText.S("p4.pad.help_choices_final") : UiText.S("p4.pad.help_choices");
            if (SystemUp)
                return _system switch
                {
                    P4SystemPage.EndGame or P4SystemPage.BackToTitle => UiText.S("p4.pad.help_confirm"),
                    P4SystemPage.About => UiText.S("p4.pad.help_about"),
                    P4SystemPage.SelectGameFinal => UiText.S("p4.pad.help_buttons"),
                    _ => UiText.S("p4.pad.help_system"),
                };
            if (PartyLostUp)
                return UiText.S("p4.pad.help_wiped"); // s193 (E28): _deadmsg's any key
            if (Game.Battle is { } b)
                return b.Prompt switch
                {
                    P4BattlePrompt.Options when b.AutoCombat => UiText.S("p4.pad.help_next_round"), // s193 (E28)
                    P4BattlePrompt.Options => UiText.S("p4.pad.help_battle_options"),
                    P4BattlePrompt.Order => _mode switch
                    {
                        BattleMode.Target => UiText.S("p4.pad.help_target"),
                        BattleMode.Spells or BattleMode.Member => UiText.S("p4.pad.help_battle_pick"),
                        _ when b.AutoCombat => UiText.S("p4.pad.help_next_round"),
                        _ when _padAutoMove => UiText.F("p4.pad.help_orders_x", UiText.S("p4.battle.action.auto_move")),
                        _ => PadOrderNow(PadOrders(b)) is { } order
                            ? UiText.F("p4.pad.help_orders_x", ActionName(order)) : UiText.S("p4.pad.help_wait_nosave"),
                    },
                    P4BattlePrompt.Treasure => UiText.S("p4.pad.help_confirm"),
                    _ => UiText.S("p4.pad.help_wait_nosave"), // no save in a fight
                };
            if (MenuOpen)
                return M!.Prompt switch
                {
                    P4MenuPrompt.Wait => PadWaitHelp,
                    P4MenuPrompt.Confirm => UiText.S("p4.pad.help_confirm"),
                    _ => M.CanBack ? IconHelp(UiText.S("p4.pad.help_pick"), UiText.S("p4.pad.help_pick.plain"))
                        : IconHelp(UiText.S("p4.pad.help_pick_menu"), UiText.S("p4.pad.help_pick_menu.plain")),
                };
            return Game.Place switch
            {
                P4Place.Outdoors => O.Prompt switch
                {
                    P4OutdoorPrompt.Action => IconHelp(UiText.S("p4.pad.help_move"), UiText.S("p4.pad.help_move.plain")),
                    P4OutdoorPrompt.Wait => PadWaitHelp,
                    P4OutdoorPrompt.Confirm => UiText.S("p4.pad.help_confirm"),
                    P4OutdoorPrompt.Fight => UiText.S("p4.pad.help_fight"),
                    _ => UiText.S("p4.pad.help_left"),
                },
                P4Place.Town => W.Prompt switch
                {
                    P4TownPrompt.Menu => IconHelp(UiText.S("p4.pad.help_street"), UiText.S("p4.pad.help_street.plain")),
                    P4TownPrompt.Pick or P4TownPrompt.Who => W.CanBack ? IconHelp(UiText.S("p4.pad.help_pick"), UiText.S("p4.pad.help_pick.plain"))
                        : IconHelp(UiText.S("p4.pad.help_pick_menu"), UiText.S("p4.pad.help_pick_menu.plain")),
                    P4TownPrompt.Wait when W.CanLeaveHistory => UiText.S("p4.pad.help_history"),
                    P4TownPrompt.Wait when W.TreasuryLines is not null => UiText.S("p4.pad.help_treasury"), // s193 (D27, E28)
                    P4TownPrompt.Wait => PadWaitHelp,
                    P4TownPrompt.Confirm => UiText.S("p4.pad.help_confirm"),
                    P4TownPrompt.Shares => _padSeen.Length > 0 ? UiText.F("p4.pad.help_shares_buttons_x", PadShare ?? 1)
                        : UiText.F("p4.pad.help_shares_x", PadShare ?? 1),
                    P4TownPrompt.Ending when W.EndingStep == P4EndingStep.Question => UiText.S("p4.pad.help_confirm"),
                    _ => UiText.S("p4.pad.help_left"),
                },
                _ => D.Prompt switch
                {
                    P4DungeonPrompt.Action => IconHelp(UiText.S("p4.pad.help_move"), UiText.S("p4.pad.help_move.plain")),
                    P4DungeonPrompt.Wait => PadWaitHelp,
                    P4DungeonPrompt.Menu or P4DungeonPrompt.Who => IconHelp(UiText.S("p4.pad.help_pick_menu"),
                        UiText.S("p4.pad.help_pick_menu.plain")),
                    P4DungeonPrompt.Fight => UiText.S("p4.pad.help_fight"),
                    _ => UiText.S("p4.pad.help_left"),
                },
            };
        }
    }

    /// <summary>
    /// s185 live pass: a pad help that names Y / X's icon or button pick (<paramref name="withIcons"/>), or its wording with
    /// no Y / X (<paramref name="plain"/>) while the screen has no buttons for them (no art pack's command panel), as the
    /// share-out's help does.
    /// </summary>
    private string IconHelp(string withIcons, string plain) => _padSeen.Length == 0 ? plain : withIcons;

    /// <summary>The middle of the button the pad has picked (Y / X), or null; the shell lights it as under the pointer.</summary>
    public Point? PadPointer { get; private set; }

    /// <summary>The fight order the pad's A gives (X / Y pick it), or null before the pad picked one or while it picked auto
    /// move (<see cref="PadAutoMove"/>).</summary>
    public P4ActionKind? PadAction => _padAutoMove ? null : _padAction;

    /// <summary>s191 (pad fix D26): the pad's pick in a fight's orders is Q auto move (the last step of Y / X's cycle).</summary>
    public bool PadAutoMove => _padAutoMove;

    /// <summary>Lets go of the pad's picked button (the mouse moved).</summary>
    public void ClearPadFocus()
    {
        _padFocus = null;
        PadPointer = null;
    }

    /// <summary>A region the pad can pick: one of IV's own buttons (a Command panel icon and its kin, a keypad key).</summary>
    public static bool IsPadTarget(HitRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return region.Tag switch
        {
            P4PanelIconTag => region.Click.Count > 0,
            P4KeypadTag k => k.Digit is not null || region.Click.Count > 0,
            _ => false,
        };
    }

    /// <summary>The regions of <paramref name="regions"/> the pad can pick, in drawing order.</summary>
    public static IReadOnlyList<HitRegion> PadTargets(IEnumerable<HitRegion> regions) =>
        [.. regions.Where(IsPadTarget)];

    /// <summary>
    /// The pad's pick after one Y (<paramref name="step"/> 1) or X (−1) among <paramref name="count"/> buttons: the next /
    /// previous one, wrapping; from none, the first / last. Null when there is none.
    /// </summary>
    public static int? StepFocus(int? current, int count, int step)
    {
        if (count <= 0)
            return null;
        if (current is not int c || c >= count)
            return step >= 0 ? 0 : count - 1;
        return ((c + step) % count + count) % count;
    }

    /// <summary>The pad buttons held down in <paramref name="pad"/>.</summary>
    public static IReadOnlyCollection<Buttons> HeldButtons(GamePadState pad) =>
        [.. Enum.GetValues<Buttons>().Where(pad.IsButtonDown)];

    /// <summary>
    /// This frame's pad input as IV's keys (to follow the keyboard's into <see cref="HandleKeys"/>): <paramref name="pressed"/>
    /// are the buttons newly down, <paramref name="held"/> all buttons down (for the diagonals), <paramref name="regions"/>
    /// the last drawing's click targets. <paramref name="press"/> is the picked button when A clicks it (the shell acts on
    /// it as on a mouse click), else null.
    /// </summary>
    public IReadOnlyList<Keys> PadKeys(IReadOnlyCollection<Buttons> pressed, IReadOnlyCollection<Buttons> held,
        IReadOnlyList<HitRegion> regions, out HitRegion? press)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(regions);
        press = null; // s194 (F33): PadInput is the shell's PadWording.Note (a key or the mouse wins a frame with both)
        var targets = PadTargets(regions);
        var seen = targets.Select(t => (t.Tag, t.Bounds)).ToArray();
        if (!seen.SequenceEqual(_padSeen))
            _padFocus = null; // another set of buttons: the old pick would press something else
        _padSeen = seen;
        var alphabet = PadTypingAlphabet;
        SyncWheel(alphabet);
        if (alphabet is not null)
            _padFocus = null; // Y / X are the wheel's here, and A ends the entry
        var keys = new List<Keys>();
        if (_padCommands is not null && PadCommandEntries is null)
            _padCommands = null; // the prompt it was opened on has gone
        if (_padCommands is not null)
        {
            PadCommandButtons(pressed, keys);
            PadPointer = null;
            return keys;
        }
        if (pressed.Contains(Buttons.Back) && alphabet is null && PadCommandEntries is { } commands)
        {
            // s191 (pad fix D24): Back opens the command list (the screen has no buttons for Y / X here).
            _padCommands = commands;
            _padCommandCursor = 0;
            _padFocus = null;
            PadPointer = null;
            return keys;
        }
        if (!(DoorOpen || MysticBusy || WelcomeUp)) // the busy waits read nothing (HandleKeys)
        {
            var wheel = new List<Buttons>();
            foreach (var b in pressed)
            {
                if (alphabet is not null && WheelStep(b, alphabet) is { } steps)
                {
                    wheel.AddRange(steps);
                    continue;
                }
                switch (b)
                {
                    case Buttons.Y or Buttons.X:
                        var step = b == Buttons.Y ? 1 : -1;
                        if (PadOrdersUp is { } fight)
                            CyclePadAction(fight, step);
                        else if (!MainMenuAsked)
                            _padFocus = StepFocus(_padFocus, targets.Count, step);
                        break;
                    case Buttons.A when _padFocus is int f && !MainMenuAsked:
                        press ??= targets[f];
                        break;
                    case Buttons.A:
                        keys.Add(PadConfirmKey());
                        break;
                    case Buttons.B when _padFocus is not null && !MainMenuAsked:
                        _padFocus = null;
                        break;
                    case Buttons.B:
                        keys.Add(PadBackKey());
                        break;
                    case Buttons.LeftShoulder:
                        keys.Add(Keys.F5);
                        break;
                    case Buttons.RightShoulder:
                        keys.Add(Keys.F9);
                        break;
                    case Buttons.Start:
                        keys.Add(Keys.Escape);
                        break;
                    case Buttons.LeftTrigger or Buttons.RightTrigger when TreasuryWaitUp:
                        // s193 (pad fix D27): the Guild treasury's ledger a page up / down (PgUp / PgDn); LB / RB save / load
                        keys.Add(b == Buttons.LeftTrigger ? Keys.PageUp : Keys.PageDown);
                        break;
                }
            }
            if (alphabet is null && PadShare is not null)
            {
                // s185: the share-out's Left / Right set the share A gives (no other key reads them there).
                foreach (var b in pressed)
                    if (b is Buttons.DPadLeft or Buttons.LeftThumbstickLeft or Buttons.DPadRight or Buttons.LeftThumbstickRight)
                        StepPadShare(b is Buttons.DPadLeft or Buttons.LeftThumbstickLeft ? -1 : 1);
                keys.AddRange(PadMoves([.. pressed.Where(b => b is not (Buttons.DPadLeft or Buttons.LeftThumbstickLeft
                    or Buttons.DPadRight or Buttons.LeftThumbstickRight))], held, PadDiagonals));
            }
            else if (alphabet is null)
                keys.AddRange(PadMoves(pressed, held, PadDiagonals));
            else if (wheel.Count > 0)
                foreach (var k in _wheel.KeysFor(_wheelOwner!, wheel, alphabet))
                {
                    if (k == Keys.Back)
                        keys.Add(Keys.Back);
                    else
                        OnTextInput(_wheel.Current); // IV types characters (OnTextInput), not keys
                }
            if (alphabet is not null && pressed.Contains(Buttons.A))
                _wheelOwner = null; // A ends the entry: a next typed prompt of the same kind starts its wheel again

        }
        PadPointer = _padFocus is int i ? targets[i].Bounds.Center : null;
        return keys;
    }

    /// <summary>
    /// As <see cref="PadKeys(IReadOnlyCollection{Buttons}, IReadOnlyCollection{Buttons}, IReadOnlyList{HitRegion}, out HitRegion?)"/>,
    /// with the left stick read from its position (<paramref name="stick"/>, after <paramref name="elapsedMs"/>) through
    /// <see cref="StickQuantizer"/> instead of its buttons: one direction per push, a diagonal at once (never N then NE).
    /// </summary>
    public IReadOnlyList<Keys> PadKeys(IReadOnlyCollection<Buttons> pressed, IReadOnlyCollection<Buttons> held,
        IReadOnlyList<HitRegion> regions, Vector2 stick, double elapsedMs, out HitRegion? press)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(held);
        IReadOnlyList<Buttons> pushed = _stick.Update(stick, elapsedMs, PadDiagonals) is { } dir ? StickQuantizer.ButtonsOf(dir) : [];
        return PadKeys([.. pressed.Where(b => !StickQuantizer.IsStickButton(b)), .. pushed],
            [.. held.Where(b => !StickQuantizer.IsStickButton(b))], regions, out press);
    }

    /// <summary>
    /// <paramref name="b"/> on the typed prompt's wheel: Left / Right one step, Up / Down ten (one among the digits) as that
    /// many Left / Right presses, X / Y themselves (type, delete); null for a button the wheel does not take.
    /// </summary>
    private static IEnumerable<Buttons>? WheelStep(Buttons b, string alphabet)
    {
        var jump = DialogPad.WheelAlphabet(alphabet).Length > 10 ? 10 : 1;
        return b switch
        {
            Buttons.DPadLeft or Buttons.LeftThumbstickLeft or Buttons.DPadRight or Buttons.LeftThumbstickRight
                or Buttons.X or Buttons.Y => [b],
            Buttons.DPadUp or Buttons.LeftThumbstickUp => Enumerable.Repeat(Buttons.DPadLeft, jump),
            Buttons.DPadDown or Buttons.LeftThumbstickDown => Enumerable.Repeat(Buttons.DPadRight, jump),
            _ => null,
        };
    }

    private static readonly Buttons[] DPad = [Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight];

    private static readonly Buttons[] Stick =
        [Buttons.LeftThumbstickUp, Buttons.LeftThumbstickDown, Buttons.LeftThumbstickLeft, Buttons.LeftThumbstickRight];

    /// <summary>
    /// The arrows for the D-pad's and the left stick's new presses (up, down, left, right). With
    /// <paramref name="diagonals"/>, a new press while the perpendicular of the same control is down gives the diagonal
    /// key instead (Home up-left, PgUp up-right, End down-left, PgDn down-right: <see cref="ButtonOf"/>), one per control.
    /// </summary>
    public static IEnumerable<Keys> PadMoves(IReadOnlyCollection<Buttons> pressed, IReadOnlyCollection<Buttons> held, bool diagonals)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(held);
        Keys[] arrows = [Keys.Up, Keys.Down, Keys.Left, Keys.Right];
        foreach (var control in new[] { DPad, Stick })
        {
            if (!control.Any(pressed.Contains))
                continue;
            bool Down(int n) => held.Contains(control[n]) || pressed.Contains(control[n]);
            var dy = (Down(1) ? 1 : 0) - (Down(0) ? 1 : 0);
            var dx = (Down(3) ? 1 : 0) - (Down(2) ? 1 : 0);
            if (diagonals && dx != 0 && dy != 0)
            {
                yield return (dx, dy) switch
                {
                    (< 0, < 0) => Keys.Home,
                    (> 0, < 0) => Keys.PageUp,
                    (< 0, > 0) => Keys.End,
                    _ => Keys.PageDown,
                };
                continue;
            }
            for (var n = 0; n < control.Length; n++)
                if (pressed.Contains(control[n]))
                    yield return arrows[n];
        }
    }

    /// <summary>The final screen's save question is up (as <see cref="HandleKeys"/> tests it).</summary>
    private bool EndingQuestionUp => _system is null && Game.Battle is null && Game.Place == P4Place.Town
        && W.Prompt == P4TownPrompt.Ending && W.EndingStep == P4EndingStep.Question;

    /// <summary>s193 (pad fix E28): a wait's pad help; it names LB save where a save is taken (<c>CanSave</c>: not in a
    /// building, a menu or a fight).</summary>
    private string PadWaitHelp => Game.CanSave ? UiText.S("p4.pad.help_wait") : UiText.S("p4.pad.help_wait_nosave");

    /// <summary>The Guild treasury's ledger waits on the town's screen (Up / Down / PgUp / PgDn scroll it, <see cref="TownKey"/>).</summary>
    private bool TreasuryWaitUp => !MainMenuAsked && Game.LoopBuilt && _system is null && Game.Battle is null && !MenuOpen
        && Game.Place == P4Place.Town && W.Prompt == P4TownPrompt.Wait && W.TreasuryLines is not null;

    /// <summary>s158 "The party has been wiped out.": <c>_deadmsg</c> waits for any key (as <see cref="HandleKeys"/> tests it).</summary>
    private bool PartyLostUp => Game.LoopBuilt && _system is null && Game.Battle is null && Game.PartyLost;

    /// <summary>
    /// s193 (pad fix E28): the text <paramref name="id"/> as shown in a box or list: its <c>.pad</c> twin while the pad is
    /// the input (<see cref="PadInput"/>) and the table has one (as <see cref="PadWording"/> does for I–III's help).
    /// </summary>
    private string PadS(string id) =>
        PadInput && UiText.Table.Contains(id + PadWording.Suffix) ? UiText.S(id + PadWording.Suffix) : UiText.S(id);

    /// <summary>The System screen takes the keys (as <see cref="HandleKeys"/> tests it).</summary>
    private bool SystemUp => _system is not null && (Game.Battle is null || DiskPageUp);

    /// <summary>The fight whose orders are asked with no target, spell or member pick open; else null.</summary>
    private P4Battle? PadOrdersUp => !MainMenuAsked && Game.LoopBuilt && !SystemUp
        && Game.Battle is { Prompt: P4BattlePrompt.Order, Current: not null } b && !b.AutoCombat && _mode == BattleMode.Normal
            ? b : null;

    /// <summary>The D-pad steps here (a map's action prompt, a fight's orders or target), so its diagonals count.</summary>
    private bool PadDiagonals => !MainMenuAsked && Game.LoopBuilt && !SystemUp && !EndingQuestionUp
        && (Game.Battle is { } b
            ? b.Prompt == P4BattlePrompt.Order && _mode is BattleMode.Normal or BattleMode.Target
            : !MenuOpen && Game.Place switch
            {
                P4Place.Outdoors => O.Prompt == P4OutdoorPrompt.Action,
                P4Place.Dungeon => D.Prompt == P4DungeonPrompt.Action,
                _ => false,
            });

    /// <summary>The key the pad's A stands for now.</summary>
    private Keys PadConfirmKey()
    {
        if (MainMenuAsked)
            return Keys.Y;
        if (!Game.LoopBuilt)
            return Keys.Enter;
        if (EndingQuestionUp)
            return Keys.Y;
        if (SystemUp)
            return _system switch
            {
                P4SystemPage.EndGame or P4SystemPage.BackToTitle => Keys.Y,
                P4SystemPage.About => Keys.Space,
                _ => Keys.Enter,
            };
        if (Game.Battle is { } b)
        {
            if (b.Prompt == P4BattlePrompt.Treasure)
                return Keys.Y;
            if (PadOrdersUp is { } fight && PadOrderKey(fight) is { } order)
                return order;
            return Keys.Enter;
        }
        if (MenuOpen)
            return M!.Prompt == P4MenuPrompt.Confirm ? Keys.Y : Keys.Enter;
        return Game.Place switch
        {
            P4Place.Town when W.Prompt == P4TownPrompt.Confirm => Keys.Y,
            P4Place.Town when PadShare is int share => Keys.D0 + share, // s185: the share the pad set (Left / Right)
            P4Place.Outdoors when O.Prompt == P4OutdoorPrompt.Confirm => Keys.Y,
            _ => Keys.Enter,
        };
    }

    /// <summary>The key the pad's B stands for now.</summary>
    private Keys PadBackKey()
    {
        if (MainMenuAsked)
            return Keys.N;
        if (!Game.LoopBuilt)
            return Keys.Escape;
        if (EndingQuestionUp)
            return Keys.N;
        if (SystemUp)
            return _system switch
            {
                P4SystemPage.EndGame or P4SystemPage.BackToTitle => Keys.N,
                P4SystemPage.SelectGameFinal => Keys.Escape, // no way back: the main-menu question
                _ => Keys.Space,
            };
        if (Game.Battle is { } b)
            return b.Prompt switch
            {
                P4BattlePrompt.Treasure => Keys.N,
                P4BattlePrompt.Order when _mode != BattleMode.Normal => Keys.Back,
                _ => Keys.Escape,
            };
        if (MenuOpen)
            return M!.Prompt switch
            {
                P4MenuPrompt.Pick or P4MenuPrompt.Who when M.CanBack => Keys.Space,
                P4MenuPrompt.Confirm => Keys.N,
                _ => Keys.Escape,
            };
        return Game.Place switch
        {
            P4Place.Town => W.Prompt switch
            {
                P4TownPrompt.Pick or P4TownPrompt.Who when W.CanBack => Keys.Space,
                P4TownPrompt.Confirm => Keys.N,
                P4TownPrompt.Number or P4TownPrompt.Text => Keys.Back,
                P4TownPrompt.Wait when W.CanLeaveHistory => Keys.L, // s183: the Hall's history ends early (L)
                _ => Keys.Escape,
            },
            P4Place.Outdoors when O.Prompt == P4OutdoorPrompt.Confirm => Keys.N,
            P4Place.Dungeon when D.Prompt is P4DungeonPrompt.Letters or P4DungeonPrompt.Number => Keys.Back,
            _ => Keys.Escape,
        };
    }

    /// <summary>The orders the pad can give in <paramref name="b"/>: the offered ones that have a key.</summary>
    private static List<P4ActionKind> PadOrders(P4Battle b) => [.. b.Offered().Where(ActionKeys.ContainsValue)];

    /// <summary>
    /// The order A gives before the pad picks one: the first of these offered (a blow when a foe is in reach, else Parry,
    /// so A never opens the spell list unasked).
    /// </summary>
    public static readonly IReadOnlyList<P4ActionKind> PadOrderDefaults =
    [
        P4ActionKind.Attack, P4ActionKind.Thrust, P4ActionKind.Slash, P4ActionKind.Lunge, P4ActionKind.AimBlow,
        P4ActionKind.FireBow, P4ActionKind.Throw, P4ActionKind.Parry,
    ];

    /// <summary>The pad's order now: its pick when offered, else the first of <see cref="PadOrderDefaults"/> offered, else the
    /// first offered; null with none.</summary>
    private P4ActionKind? PadOrderNow(List<P4ActionKind> offered) =>
        offered.Count == 0 ? null
        : _padAction is { } k && offered.Contains(k) ? k
        : PadOrderDefaults.Where(offered.Contains).Cast<P4ActionKind?>().FirstOrDefault() ?? offered[0];

    /// <summary>The order key A gives in <paramref name="b"/>'s orders (<see cref="PadOrderNow"/>, or Q when the pad picked
    /// auto move; it stays the pad's pick).</summary>
    private Keys? PadOrderKey(P4Battle b)
    {
        if (_padAutoMove)
            return Keys.Q;
        if (PadOrderNow(PadOrders(b)) is not { } kind)
            return null;
        _padAction = kind;
        return ActionKeys.First(p => p.Value == kind).Key;
    }

    /// <summary>X / Y in the orders: the previous / next offered order becomes the pad's pick; s191 (pad fix D26): after the
    /// last comes Q auto move (keyboard Q), then the first again.</summary>
    private void CyclePadAction(P4Battle b, int step)
    {
        var offered = PadOrders(b);
        var at = _padAutoMove ? offered.Count : PadOrderNow(offered) is { } now ? offered.IndexOf(now) : -1;
        if (StepFocus(at >= 0 ? at : null, offered.Count + 1, step) is not int n)
            return;
        _padAutoMove = n == offered.Count;
        if (!_padAutoMove)
            _padAction = offered[n];
    }
}
