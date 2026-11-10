using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// "Pad coverage" (s182): a dialog step that asks Y / N and does not take Return for Yes (the game menu's Quit / Main Menu
/// question, Time Lag's sound question, Guild New's roll, the DOS save dialog's questions). The pad then answers A = Y,
/// X = N (<see cref="DialogPad.YesNoKeys"/>).
/// </summary>
public interface IPadYesNo
{
    bool AsksYesNo { get; }
}

/// <summary>
/// "Pad coverage" (s182): a dialog step whose menu moves with Left / Right (a side-by-side YES / NO box, the transport
/// list): the pad's D-pad Left / Right stay arrows there, and the step has no pad wheel (<see cref="DialogPad"/>).
/// </summary>
public interface IPadArrows
{
    bool TakesLeftRight { get; }
}

/// <summary>
/// "Pad coverage" (s182): the pad on the remaster's modal dialogs (<see cref="IModalDialog"/>), which read keys only. On
/// top of <see cref="InputMap.MenuPadKeys"/> (D-pad / left stick = arrows, A = Return, B = Esc, LB / RB = PgUp / PgDn):
/// <list type="bullet">
/// <item>a dialog that picks or types by digit or letter (who casts, which spell, an amount, a name) gets a pad wheel:
/// D-pad / stick Left / Right step a character (the dialog's <see cref="ITypedEntry.TypingAlphabet"/>, else the digits
/// 1…9, 0), X types it, Y is Backspace (the dialog's own Backspace: delete, or back out where it backs out). The help
/// line names the character (<see cref="Help"/>) once the pad has been used in the dialog;</item>
/// <item>a Y / N step (<see cref="IPadYesNo"/>): A = Y, X = N;</item>
/// <item>a step whose menu takes Left / Right itself (<see cref="IPadArrows"/>) keeps them, and has no wheel.</item>
/// </list>
/// Keyboard keys are untouched: the pad only adds keys.
/// </summary>
public sealed class DialogPad
{
    /// <summary>The wheel's characters where the dialog types no alphabet of its own: a pick 1…9 first, then 0.</summary>
    public const string Digits = "1234567890";

    private object? _owner;
    private string _alphabet = Digits;
    private int _at;
    private string? _stepHelp; // the open step's own help (StepChanged)

    /// <summary>The pad has been used in the open dialog (a Y / N step's help line then names the pad's keys).</summary>
    public bool Active { get; private set; }

    /// <summary>The wheel has been used in the open dialog (Left / Right, X or Y): its help line then names the wheel.</summary>
    public bool WheelUsed { get; private set; }

    /// <summary>The wheel's character (a space for the name's space).</summary>
    public char Current => _alphabet[_at];

    /// <summary>The dialog closed or another opened: the wheel starts again (not shown until the pad is used).</summary>
    public void Reset()
    {
        _owner = null;
        _alphabet = Digits;
        _at = 0;
        _stepHelp = null;
        Active = false;
        WheelUsed = false;
    }

    /// <summary>
    /// s185 live pass: the dialog moved to a step whose own help has a pad wording (III / I training's "A agree  B not"
    /// after the WHO pick by X): the wheel's line steps back so that wording shows, until the wheel is used again.
    /// <paramref name="stepHelp"/>: the step's help now (null: not tracked).
    /// </summary>
    private void StepChanged(string? stepHelp)
    {
        if (stepHelp is null || stepHelp == _stepHelp)
            return;
        var first = _stepHelp is null;
        _stepHelp = stepHelp;
        if (!first && PadWording.PadTextFor(UiText.Table, stepHelp) is not null)
            WheelUsed = false;
    }

    /// <summary>
    /// The keys this frame's newly pressed <paramref name="buttons"/> stand for in <paramref name="dialog"/> (an open
    /// dialog; a new one resets the wheel). <paramref name="typingAlphabet"/>: what the step types (null: digits).
    /// </summary>
    public IReadOnlyList<Keys> KeysFor(object dialog, IEnumerable<Buttons> buttons, string? typingAlphabet = null,
        bool yesNo = false, bool leftRightNative = false, string? stepHelp = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(buttons);
        if (!ReferenceEquals(dialog, _owner))
        {
            Reset();
            _owner = dialog;
        }
        Sync(typingAlphabet);
        StepChanged(stepHelp);
        var keys = new List<Keys>();
        foreach (var b in buttons)
        {
            Active = true;
            if (yesNo)
            {
                keys.AddRange(YesNoKeys([b]));
                continue;
            }
            var step = leftRightNative ? 0 : b switch
            {
                Buttons.DPadLeft or Buttons.LeftThumbstickLeft => -1,
                Buttons.DPadRight or Buttons.LeftThumbstickRight => 1,
                _ => 0,
            };
            if (step != 0)
            {
                WheelUsed = true;
                _at = (_at + step + _alphabet.Length) % _alphabet.Length;
                continue;
            }
            if (!leftRightNative && b == Buttons.X && TypingGrid.KeyFor(Current) is { } typed)
            {
                WheelUsed = true;
                keys.Add(typed);
            }
            else if (!leftRightNative && b == Buttons.Y)
            {
                WheelUsed = true;
                keys.Add(Keys.Back);
            }
            else if (InputMap.MenuPadKey(b) is var k && k != Keys.None)
                keys.Add(k);
        }
        return keys;
    }

    /// <summary>A Y / N step's pad: A = Y, X = N, and the rest as <see cref="InputMap.MenuPadKey"/> (B = Esc, the D-pad).</summary>
    public static IEnumerable<Keys> YesNoKeys(IEnumerable<Buttons> buttons) =>
        buttons.Select(b => b switch
        {
            Buttons.A => Keys.Y,
            Buttons.X => Keys.N,
            _ => InputMap.MenuPadKey(b),
        }).Where(k => k != Keys.None);

    /// <summary>
    /// The help line while the pad is in use: the wheel's character and its keys, or the Y / N keys; null when the pad has
    /// not been used in the open dialog (its own help then).
    /// </summary>
    public string? Help(object dialog, string? typingAlphabet, bool yesNo, bool leftRightNative, string? stepHelp = null)
    {
        if (!Active || leftRightNative || !ReferenceEquals(dialog, _owner))
            return null;
        Sync(typingAlphabet);
        StepChanged(stepHelp);
        if (yesNo)
            return UiText.S("pad.dialog_yes_no");
        if (!WheelUsed)
            return null;
        return UiText.F("pad.dialog_wheel_x", Current == ' ' ? UiText.S("typing.space") : Current.ToString());
    }

    /// <summary>A step typing another alphabet (Guild New's class digits, then the name) starts the wheel on it again.</summary>
    private void Sync(string? typingAlphabet)
    {
        var alphabet = WheelAlphabet(typingAlphabet);
        if (alphabet == _alphabet)
            return;
        _alphabet = alphabet;
        _at = !alphabet.Any(char.IsLetter) && alphabet.IndexOf('1', StringComparison.Ordinal) is var one and >= 0 ? one : 0;
    }

    /// <summary>The wheel's characters for a step typing <paramref name="typingAlphabet"/> (null: <see cref="Digits"/>).</summary>
    public static string WheelAlphabet(string? typingAlphabet)
    {
        if (typingAlphabet is null)
            return Digits;
        var chars = new string(typingAlphabet.Where(c => TypingGrid.KeyFor(c) is not null).Distinct().ToArray());
        return chars.Length == 0 ? Digits : chars;
    }
}

/// <summary>
/// "Pad coverage" (s182): pad presses and releases for the screens that count both as input events (I Amiga's victory and
/// defeat screens, as a key's press and release, s130 / s153). A button counts as released only after it was pressed here.
/// </summary>
public sealed class PadPressTracker
{
    private readonly HashSet<Buttons> _down = [];

    /// <summary>This frame's presses (<paramref name="newButtons"/>) and releases of buttons pressed before.</summary>
    public (int Presses, int Releases) Events(IEnumerable<Buttons> newButtons, GamePadState pad)
    {
        ArgumentNullException.ThrowIfNull(newButtons);
        var released = _down.Where(pad.IsButtonUp).ToList();
        foreach (var b in released)
            _down.Remove(b);
        var presses = 0;
        foreach (var b in newButtons)
        {
            _down.Add(b);
            presses++;
        }
        return (presses, released.Count);
    }
}
