using OpenPhantasie.Core;
using OpenPhantasie.Data;
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
/// Pad fix B7 (s188): a dialog whose steps pick or type by digit / letter with nothing else to reach the pick (no D-pad
/// cursor): <see cref="PadWheel"/> is the open step's characters for the pad wheel (<see cref="DialogPad.Picks"/> for a
/// numbered list), null on a step that reads none. A dialog without this interface gets the wheel only where it types
/// (<see cref="ITypedEntry.TypingAlphabet"/>); every other step keeps D-pad Left / Right, and X / Y do nothing.
/// </summary>
public interface IPadWheel
{
    string? PadWheel(GameSession session);

    /// <summary>
    /// Pad fix B8 (s189): the open step's name where several steps share one help text (Cast's who / spell / heal whom), so
    /// the wheel starts again at a new step (<see cref="DialogPad"/>); null where the help text alone tells the steps apart.
    /// </summary>
    string? PadStep => null;

    /// <summary>
    /// Pad fix B8 (s189): what the wheel's number <paramref name="pick"/> (1…9) picks on the open step (a member's, a spell's
    /// or an item's name), shown after the number on the step's wheel line (<see cref="DialogPad.WheelSuffix"/>); null: the
    /// number alone.
    /// </summary>
    string? PadPickName(GameSession session, GameDataRepository data, int pick) => null;
}

/// <summary>
/// "Pad coverage" (s182): the pad on the remaster's modal dialogs (<see cref="IModalDialog"/>), which read keys only. On
/// top of <see cref="InputMap.MenuPadKeys"/> (D-pad / left stick = arrows, A = Return, B = Esc, LB / RB = PgUp / PgDn):
/// <list type="bullet">
/// <item>a step that picks or types by digit or letter (who casts, which spell, an amount, a name) gets a pad wheel
/// (B7, s188: only such a step, <see cref="IPadWheel"/> / <see cref="ITypedEntry"/>): D-pad / stick Left / Right step a
/// character (the step's characters, else the digits 1…9, 0), X types it, Y is Backspace (the dialog's own Backspace: delete, or back out where it backs out). The help
/// line is the step's own <c>.wheel</c> line with the pick (B8, s189), else the generic one naming the character once the
/// pad has been used in the dialog (<see cref="Help"/>);</item>
/// <item>a Y / N step (<see cref="IPadYesNo"/>): A = Y, X = N;</item>
/// <item>a step whose menu takes Left / Right itself (<see cref="IPadArrows"/>) keeps them, and has no wheel.</item>
/// </list>
/// Keyboard keys are untouched: the pad only adds keys.
/// </summary>
public sealed class DialogPad
{
    /// <summary>The wheel's characters where the dialog types no alphabet of its own: a pick 1…9 first, then 0.</summary>
    public const string Digits = "1234567890";

    /// <summary>
    /// Pad fix B8 (s189): the id suffix of a wheel step's own line: a help text whose id has a twin <c>&lt;id&gt;.wheel</c>
    /// shows that template while the pad is the input, <c>{0}</c> = the wheel's pick (with its name,
    /// <see cref="IPadWheel.PadPickName"/>), naming what X / Y / A / B do on that step (<see cref="WheelTextFor"/>).
    /// </summary>
    public const string WheelSuffix = ".wheel";

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StringTable, Dictionary<string, string>> WheelTwins = new();

    /// <summary>The <c>.wheel</c> template of the help text <paramref name="text"/> in <paramref name="table"/>, or null.</summary>
    public static string? WheelTextFor(StringTable table, string? text)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrEmpty(text))
            return null;
        Dictionary<string, string> twins;
        lock (WheelTwins)
            twins = WheelTwins.GetValue(table, t =>
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (id, wheel) in t.Strings.OrderBy(p => p.Key, StringComparer.Ordinal))
                    if (id.EndsWith(WheelSuffix, StringComparison.Ordinal) && t.Contains(id[..^WheelSuffix.Length]))
                        map.TryAdd(t.Get(id[..^WheelSuffix.Length]), wheel);
                return map;
            });
        return twins.TryGetValue(text, out var w) ? w : null;
    }

    /// <summary>The wheel for a numbered list of <paramref name="count"/> picks: 1…count (1…9 at most), null when empty.</summary>
    public static string? Picks(int count) => count <= 0 ? null : Digits[..Math.Min(count, 9)];

    private object? _owner;
    private string _alphabet = Digits;
    private int _at;
    private string? _stepKey; // the open step's own help and step name (StepChanged)

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
        _stepKey = null;
        Active = false;
        WheelUsed = false;
    }

    /// <summary>
    /// s185 live pass: the dialog moved to a step whose own help has a pad wording (III / I training's "A agree  B not"
    /// after the WHO pick by X): the wheel's line steps back so that wording shows, until the wheel is used again.
    /// B8 (s189): a new step (its help, or <paramref name="step"/> where steps share one help) also starts the wheel again
    /// at its first pick (Cast's heal target no longer starts on the caster's number).
    /// <paramref name="stepHelp"/>: the step's help now (null: not tracked).
    /// </summary>
    private void StepChanged(string? stepHelp, string? step)
    {
        if (stepHelp is null)
            return;
        var key = step is null ? stepHelp : stepHelp + "\n" + step;
        if (key == _stepKey)
            return;
        var first = _stepKey is null;
        _stepKey = key;
        if (first)
            return;
        _at = StartOf(_alphabet);
        if (PadWording.PadTextFor(UiText.Table, stepHelp) is not null)
            WheelUsed = false;
    }

    /// <summary>
    /// The keys this frame's newly pressed <paramref name="buttons"/> stand for in <paramref name="dialog"/> (an open
    /// dialog; a new one resets the wheel). <paramref name="typingAlphabet"/>: what the step types (null: digits).
    /// </summary>
    public IReadOnlyList<Keys> KeysFor(object dialog, IEnumerable<Buttons> buttons, string? typingAlphabet = null,
        bool yesNo = false, bool leftRightNative = false, string? stepHelp = null, bool wheel = true, string? stepName = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(buttons);
        if (!ReferenceEquals(dialog, _owner))
        {
            Reset();
            _owner = dialog;
        }
        var noWheel = leftRightNative || !wheel; // B7: a step with no wheel keeps Left / Right; X / Y send nothing
        if (wheel)
            Sync(typingAlphabet);
        StepChanged(stepHelp, stepName);
        var keys = new List<Keys>();
        foreach (var b in buttons)
        {
            Active = true;
            if (yesNo)
            {
                keys.AddRange(YesNoKeys([b]));
                continue;
            }
            var step = noWheel ? 0 : b switch
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
            if (!noWheel && b == Buttons.X && TypingGrid.KeyFor(Current) is { } typed)
            {
                WheelUsed = true;
                keys.Add(typed);
            }
            else if (!noWheel && b == Buttons.Y)
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
    /// not been used in the open dialog (its own help then). B8 (s189): a wheel step whose help has a <c>.wheel</c> twin
    /// (<see cref="WheelTextFor"/>) shows that line at once, with the pick and <paramref name="pickName"/>'s name for it.
    /// </summary>
    public string? Help(object dialog, string? typingAlphabet, bool yesNo, bool leftRightNative, string? stepHelp = null,
        bool wheel = true, string? step = null, Func<int, string?>? pickName = null)
    {
        if (leftRightNative || !ReferenceEquals(dialog, _owner))
            return null;
        if (wheel && !yesNo && WheelTextFor(UiText.Table, stepHelp) is { } template)
        {
            Sync(typingAlphabet);
            StepChanged(stepHelp, step);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, template, PickText(pickName));
        }
        if (!Active)
            return null;
        StepChanged(stepHelp, step);
        if (yesNo)
            return UiText.S("pad.dialog_yes_no");
        if (!wheel)
            return null; // B7: a step with no wheel shows its own help (in the pad's words where it has them)
        Sync(typingAlphabet);
        if (!WheelUsed)
            return null;
        return UiText.F("pad.dialog_wheel_x", Current == ' ' ? UiText.S("typing.space") : Current.ToString());
    }

    /// <summary>The wheel's pick as a step's line shows it: the character (the space by name), then what a number picks.</summary>
    private string PickText(Func<int, string?>? pickName)
    {
        if (Current == ' ')
            return UiText.S("typing.space");
        var name = char.IsAsciiDigit(Current) && Current != '0' ? pickName?.Invoke(Current - '0') : null;
        return string.IsNullOrEmpty(name) ? Current.ToString() : $"{Current} {name}";
    }

    /// <summary>A step typing another alphabet (Guild New's class digits, then the name) starts the wheel on it again.</summary>
    private void Sync(string? typingAlphabet)
    {
        var alphabet = WheelAlphabet(typingAlphabet);
        if (alphabet == _alphabet)
            return;
        _alphabet = alphabet;
        _at = StartOf(alphabet);
    }

    /// <summary>Where the wheel starts on <paramref name="alphabet"/>: on 1 for numbers, else the first character.</summary>
    private static int StartOf(string alphabet) =>
        !alphabet.Any(char.IsLetter) && alphabet.IndexOf('1', StringComparison.Ordinal) is var one and >= 0 ? one : 0;

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
