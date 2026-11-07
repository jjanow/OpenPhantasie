namespace OpenPhantasie.Client;

/// <summary>
/// The typed prompts' cursor bar (s136; <c>logic-specs/events.md</c> 4.1 "Cursor"): INPUTS / INPUTN show a 1-pixel bar,
/// one text row high, at the text cursor, flipping on and off; on XEiJ (s135 GIF) it flipped about every 86 ms. The flip
/// counts key polls (1000 in <c>_wgetc</c>, 150 in <c>_wiscan</c>), so its rate is the machine's `[~]`; the remaster
/// uses the measured 86 ms for both. The town's name prompts read through the same <c>_wscan</c> (<c>_mkname</c>,
/// <c>_re_name</c>). s151: the Bank's amount (<c>_numin</c>, read) shows a steady block instead,
/// <c>_fbox(0, cx, cy, 8, _ylen)</c>: 8 pixels wide, a row high, drawn before each key wait and erased on a key.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>Milliseconds between the bar's flips (the s135 XEiJ GIF of INPUTS) `[~]`.</summary>
    public const double CursorFlipMilliseconds = 86;

    private double _cursorMs;

    /// <summary>True while a typed prompt reads (the dungeon's "Letter&gt;" / "Number&gt;", the town's amount / name).</summary>
    public bool Typing => Game.LoopBuilt && Game.Battle is null && !MenuOpen && Game.Place switch
    {
        OpenPhantasie.Core.P4Place.Dungeon => D.Prompt is OpenPhantasie.Core.P4DungeonPrompt.Letters or OpenPhantasie.Core.P4DungeonPrompt.Number,
        OpenPhantasie.Core.P4Place.Town => W.Prompt is OpenPhantasie.Core.P4TownPrompt.Number or OpenPhantasie.Core.P4TownPrompt.Text,
        _ => false,
    };

    /// <summary>The bar is drawn now: a prompt reads and the bar is in its "on" half (it starts on).</summary>
    public bool CursorOn => Typing && (KeypadUp || (int)(_cursorMs / CursorFlipMilliseconds) % 2 == 0);

    /// <summary>The cursor's width in layout pixels: <c>_numin</c>'s 8-pixel block at the Bank's amount, else the 1-pixel bar.</summary>
    private int CursorWidth => KeypadUp && Extras?.Keypad is { } pad && Assets?.PackPixelsPerLayoutPixel is > 0 and var ppl
        ? Math.Max(1, (int)Math.Round(pad.CursorWidth / ppl)) : 1;

    private void TickCursor(double milliseconds) => _cursorMs = Typing ? _cursorMs + milliseconds : 0;
}
