using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The panels' smaller pieces (s151; FORMAT.md 6.9 "Dungeon waits, typed prompts, the keypad, the shipwright's window",
/// data from the pack's <see cref="PanelExtras"/>): the hover look of buttons (<c>_s_ms_get_cp</c> / <c>_xbtn</c>),
/// <c>_ewait</c>'s SPACE bar and INPUTS / INPUTN's colour-14 band under a location map's text, <c>_numin</c>'s keypad in
/// the Bank's panel, and <c>_shipyard</c>'s window under SHIP.PKH (its grey, the offer, the answer, the party's gold).
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The mouse pointer in layout pixels while it is shown (the shell sets it each frame), else null.</summary>
    public Point? Pointer { get; set; }

    private PanelExtras? Extras => Assets?.Pack?.CommandPanels?.Extras;

    /// <summary>
    /// True while the panel up is drawn but takes no clicks (s151): a location map's event waits and typed prompts keep
    /// <c>_dsetup</c>'s panel (window 34 stays; <c>_ewait</c> / <c>_wscan</c> read only their own button or keys), and the
    /// Bank's withdrawal keeps the Bank's panel under <c>_numin</c>'s keypad (<c>_withdraw</c> ran <c>_clrbtn</c>).
    /// </summary>
    public bool PanelInert => EventSlotUp || KeypadUp;

    /// <summary>A location map's <c>_ewait</c> (Wait) or typed prompt (Letters / Number) is up, outside menus and fights.</summary>
    private bool EventSlotUp => Game.LoopBuilt && Game.Battle is null && !MenuOpen && _system is null
        && Game.Place == P4Place.Dungeon
        && D.Prompt is P4DungeonPrompt.Wait or P4DungeonPrompt.Letters or P4DungeonPrompt.Number;

    /// <summary>The Bank's withdrawal amount (<c>_withdraw</c>'s <c>_numin</c>) is being typed.</summary>
    public bool KeypadUp => Game.LoopBuilt && Game.Battle is null && !MenuOpen && _system is null
        && Game.Place == P4Place.Town && W.Prompt == P4TownPrompt.Number && W.Scene == P4TownScene.Bank;

    /// <summary>The panel shown at the prompts <see cref="PanelInert"/> covers (null at the others).</summary>
    private string? InertPanel => EventSlotUp ? "_dsetup" : KeypadUp ? "_bank" : null;

    /// <summary>The XOR of a plain button (<c>_xbox(5, …)</c>) under the pointer, else 0.</summary>
    private int BoxXorAt(float x, float y, float w, float h) =>
        Pointer is { } p && p.X >= x && p.X < x + w && p.Y >= y && p.Y < y + h ? Extras?.Hover?.BoxXor ?? 0 : 0;

    // --- Hover: icons ---------------------------------------------------------------------------------------------------

    private readonly Dictionary<Texture2D, Texture2D> _litIcons = [];

    /// <summary>
    /// The icon as <c>_xicon</c> leaves it under the pointer: each opaque pixel's palette index ^ <paramref name="xor"/>
    /// (15: the record's mask keeps the background, the rest XORs with 0xFFFF).
    /// </summary>
    private Texture2D LitIcon(Texture2D icon, int xor)
    {
        if (_litIcons.TryGetValue(icon, out var lit))
            return lit;
        var pixels = new Color[icon.Width * icon.Height];
        icon.GetData(pixels);
        var mapped = PaletteXor(pixels, PkhPalette(), xor);
        lit = new Texture2D(icon.GraphicsDevice, icon.Width, icon.Height);
        lit.SetData(mapped);
        return _litIcons[icon] = lit;
    }

    /// <summary>
    /// Each opaque pixel whose colour is palette entry i becomes entry i ^ <paramref name="xor"/>; transparent pixels and
    /// colours outside the palette stay as they are.
    /// </summary>
    public static Color[] PaletteXor(IReadOnlyList<Color> pixels, IReadOnlyList<Color> palette, int xor)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(palette);
        var result = new Color[pixels.Count];
        for (var i = 0; i < pixels.Count; i++)
        {
            var c = pixels[i];
            var index = -1;
            for (var j = 0; j < palette.Count && index < 0 && c.A != 0; j++)
                if (palette[j].R == c.R && palette[j].G == c.G && palette[j].B == c.B && palette[j].A != 0)
                    index = j;
            result[i] = index >= 0 && (index ^ xor) < palette.Count ? palette[index ^ xor] : c;
        }
        return result;
    }

    /// <summary>
    /// True when an icon under the pointer lights: not while the panel is inert (<c>_ewait</c>, <c>_numin</c> register
    /// button 14 over the whole screen, type 128, which <c>_onbtn</c> finds before the icons' slots 194 on, so nothing
    /// lights) and not at a typed prompt (<c>_wscan</c> / <c>_wiscan</c> never run <c>_mgetc</c>, so no
    /// <c>_s_ms_get_cp</c>).
    /// </summary>
    public bool IconsLight => !PanelInert && !(Typing && !KeypadUp);

    /// <summary>The icon picture to draw at <paramref name="topLeft"/> (layout pixels): lit when the pointer is on its box.</summary>
    private Texture2D IconUnderPointer(Texture2D icon, Vector2 topLeft, float size) =>
        IconsLight && Extras?.Hover is { IconXor: var xor and not 0 } && Pointer is { } p && p.X >= topLeft.X && p.X < topLeft.X + size
            && p.Y >= topLeft.Y && p.Y < topLeft.Y + size ? LitIcon(icon, xor) : icon;

    // --- Text in pack pixels ----------------------------------------------------------------------------------------------

    /// <summary><paramref name="text"/>'s width in pack pixels in P4.X font 0 (the pack's role), or the remaster's font scaled.</summary>
    private int PackWidth(string text, float k) =>
        FontSources.ForRole(Assets?.Pack, MysticFontRole) is { } f ? PixelFont.Measure(text, f)
            : (int)Math.Ceiling(PixelFont.Measure(text) / k);

    /// <summary>Draws <paramref name="text"/> with its cell's top-left at <paramref name="at"/> (layout pixels) in font 0.</summary>
    private void PackText(SpriteBatch batch, Texture2D pixel, Vector2 at, string text, Color colour, float k)
    {
        if (FontSources.ForRole(Assets?.Pack, MysticFontRole) is { } f)
            PixelFont.DrawScaled(batch, pixel, at, text, colour, k, f);
        else
            PixelFont.Draw(batch, pixel, (int)Math.Round(at.X), (int)Math.Round(at.Y), text, colour);
    }

    private Color Pal(int index, Color fallback) => Assets?.PaletteColour("pkh", index) ?? fallback;

    // --- _ewait's bar and the typed prompts' band -----------------------------------------------------------------------

    /// <summary>
    /// Pack rows of the slot under the text box (s151): <c>_ewait</c>'s line and label (the label's cell starts 2 rows under
    /// the line and is 16 high) or the typed prompts' 16-row band; the box above keeps every line (it scrolls).
    /// </summary>
    public static int EventSlotRows(EventWaitBar bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        return Math.Max(bar.Band.Height, bar.LabelY - bar.Line.Y + 16);
    }

    /// <summary>The slot's height in layout pixels now (0 when none is up).</summary>
    private int EventSlotHeight(float k) =>
        EventSlotUp && Extras?.EventWait is { } bar ? (int)Math.Ceiling(EventSlotRows(bar) * k) : 0;

    /// <summary>True while the typed prompt is drawn in the band (so the box leaves its line out).</summary>
    private bool TypedInBand => EventSlotUp && D.Prompt != P4DungeonPrompt.Wait && Extras?.EventWait is not null
        && Assets?.PackPixelsPerLayoutPixel is > 0;

    /// <summary>
    /// <c>_ewait</c>'s bar (the message window's white, a black line, "Space" centred, the bar a button pressing Space; lit
    /// by the pointer: colours ^ 5) or, at INPUTS / INPUTN, the colour-14 band with the prompt, the typed text and the
    /// cursor bar, in <paramref name="slot"/> (layout pixels) under the text box.
    /// </summary>
    private void DrawEventSlot(SpriteBatch batch, Texture2D pixel, Rectangle slot, float k)
    {
        if (Extras?.EventWait is not { } bar)
            return;
        void Box(float x, float y, float w, float h, Color c) =>
            batch.Draw(pixel, new Vector2(slot.X + x * k, slot.Y + y * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k),
                SpriteEffects.None, 0f);
        var rows = EventSlotRows(bar);
        var width = slot.Width / k;
        if (D.Prompt == P4DungeonPrompt.Wait)
        {
            float bx = slot.X, by = slot.Y + (bar.Button.Y - bar.Line.Y) * k, bw = slot.Width, bh = bar.Button.Height * k;
            var xor = BoxXorAt(bx, by, bw, bh);
            Box(0, 0, width, rows, Pal(bar.MessageColour ^ xor, Color.White));
            Box(0, 0, width, bar.Line.Height, Pal(bar.LineColour ^ xor, Color.Black));
            var label = PanelText(bar.Label, bar.Text);
            PackText(batch, pixel, new Vector2(slot.X + ((int)(width / 2) - (PackWidth(label, k) >> 1)) * k,
                slot.Y + (bar.LabelY - bar.Line.Y) * k), label, Pal(PanelTextColour ^ xor, Color.Black), k);
            UiOverlays.Hits?.Add(new HitRegion(new Rectangle((int)bx, (int)by, (int)Math.Ceiling(bw), (int)Math.Ceiling(bh)),
                new P4PanelIconTag("_ewait", 0)) { Click = [Keys.Space] });
            return;
        }
        Box(0, 0, width, rows, Pal(bar.BandColour, Color.LightGray));
        var prompt = D.Prompt == P4DungeonPrompt.Letters ? PanelText(bar.Letters, bar.LettersText)
            : PanelText(bar.Number, bar.NumberText);
        var text = prompt + _typed;
        var at = new Vector2(slot.X + bar.PromptX * k, slot.Y);
        PackText(batch, pixel, at, text, Pal(PanelTextColour, Color.Black), k);
        if (CursorOn) // events.md 4.1: a 1-pixel bar, _ylen (16) high, at the text cursor
            Box(bar.PromptX + PackWidth(text, k), 0, 1, 16, Pal(PanelTextColour, Color.Black));
    }

    // --- _numin's keypad ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>_numin</c>'s keypad (s151) in the Bank's panel window, whose area starts at <paramref name="body"/>: the window grey
    /// over the clear rect, <c>_whole</c>'s sunken frame, twelve 16 × 16 keys " c " on colour-4 cells; a click on a digit
    /// types it, B erases, R ends (Return); a key under the pointer is lit (colours ^ 5).
    /// </summary>
    private void DrawKeypad(SpriteBatch batch, Texture2D pixel, Vector2 body, float k)
    {
        if (Extras?.Keypad is not { } pad)
            return;
        void Box(float x, float y, float w, float h, Color c) =>
            batch.Draw(pixel, body + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k),
                SpriteEffects.None, 0f);
        Box(pad.X + pad.Clear.X, pad.Y + pad.Clear.Y, pad.Clear.Width, pad.Clear.Height, Pal(pad.BackColour, Color.LightGray));
        var f = pad.Frame.Rect;
        float hx = pad.X + f.X, hy = pad.Y + f.Y;
        Color dark = Pal(pad.Frame.Dark, Color.DimGray), light = Pal(pad.Frame.Light, Color.White);
        for (var n = 1; n <= NoYesFrame; n++)
        {
            Box(hx - n, hy - n, f.Width + 2 * n, 1, dark);
            Box(hx - n, hy - n, 1, f.Height + 2 * n, dark);
            Box(hx - n, hy + f.Height + n - 1, f.Width + 2 * n, 1, light);
            Box(hx + f.Width + n - 1, hy - n, 1, f.Height + 2 * n, light);
        }
        var space = PackWidth(" ", k);
        for (var i = 0; i < pad.Keys.Count; i++)
        {
            var key = pad.Keys[i];
            int kx = pad.X + pad.Step * (i % 3), ky = pad.Y + pad.Step * (i / 3);
            var at = body + new Vector2(kx * k, ky * k);
            var size = pad.Size * k;
            var xor = BoxXorAt(at.X, at.Y, size, size);
            // _wputc(' '), _wputc(c), _wputc(' ') on the colour-4 cell box (_bgcol 4): the cells run as wide as the advances.
            Box(kx, ky, 2 * space + PackWidth(key.Text, k), pad.Size, Pal(pad.CellColour ^ xor, Color.SteelBlue));
            PackText(batch, pixel, body + new Vector2((kx + space) * k, ky * k), key.Text, Pal(pad.TextColour ^ xor, Color.Black), k);
            var rect = new Rectangle((int)at.X, (int)at.Y, (int)Math.Ceiling(size), (int)Math.Ceiling(size));
            if (key.Key is [var d] && char.IsAsciiDigit(d))
                UiOverlays.Hits?.Add(new HitRegion(rect, new P4KeypadTag(d)));
            else if (KeypadKey(key) is var press and not Keys.None)
                UiOverlays.Hits?.Add(new HitRegion(rect, new P4KeypadTag(null)) { Click = [press] });
        }
    }

    /// <summary>The key a non-digit keypad key presses: <c>Back</c> = Backspace, <c>Enter</c> = Return.</summary>
    public static Keys KeypadKey(KeypadKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Key switch { "Back" => Keys.Back, "Enter" => Keys.Enter, _ => Keys.None };
    }

    /// <summary>A click on a keypad digit: typed as the keyboard's digit is.</summary>
    public void Keypad(char digit) => OnTextInput(digit);

    // --- The shipwright's window ----------------------------------------------------------------------------------------

    /// <summary>True when the shipwright's window is drawn round SHIP.PKH (the pack has it and the scene is the shipwright's).</summary>
    private bool ShipwrightWindowUp => SceneName == "p4.shipwright" && Extras?.Shipwright is not null;

    /// <summary>
    /// The offer's rows and the answer from the town's lines (s151): the lines before the answer, each wrapped to the
    /// window (<paramref name="fits"/>: the width a row starting at x has), and the answer ("Thank you" / "Come again"),
    /// or null. Row r starts at the original's line r (the last one's x for rows past it).
    /// </summary>
    public static (List<(int X, int Y, string Text)> Rows, string? Answer) ShipwrightText(ShipwrightWindow ship,
        IEnumerable<string> lines, string yes, string no, Func<string, int, IEnumerable<string>> fits)
    {
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentNullException.ThrowIfNull(fits);
        var list = lines.Where(l => l.Length > 0).ToList();
        string? answer = null;
        if (list.Count > 0 && (list[^1] == yes || list[^1] == no))
        {
            answer = list[^1];
            list.RemoveAt(list.Count - 1);
        }
        var rows = new List<(int, int, string)>();
        foreach (var line in list)
            foreach (var part in fits(line, ship.Lines[Math.Min(rows.Count, ship.Lines.Count - 1)].X))
            {
                var at = ship.Lines[Math.Min(rows.Count, ship.Lines.Count - 1)];
                rows.Add((at.X, ship.Lines[0].Y + 16 * rows.Count, part));
            }
        return (rows, answer);
    }

    /// <summary>Pack rows the window grows by: 16 for each offer row past the original's (wrapped English lines).</summary>
    public static int ShipwrightExtraRows(ShipwrightWindow ship, int rows)
    {
        ArgumentNullException.ThrowIfNull(ship);
        return 16 * Math.Max(0, rows - ship.Lines.Count);
    }

    /// <summary>
    /// <c>_disppcg</c>'s amount: <c>_wdout</c> pads each leading zero of a <paramref name="digits"/>-digit field with two
    /// spaces (one digit's width in font 0).
    /// </summary>
    public static string PaddedAmount(long amount, int digits)
    {
        var s = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new string(' ', 2 * Math.Max(0, digits - s.Length)) + s;
    }

    /// <summary>The shipwright's rows now (wrapped in font 0 to the window, 8 pixels short of its right edge).</summary>
    private (List<(int X, int Y, string Text)> Rows, string? Answer) ShipwrightRows(ShipwrightWindow ship, float k)
    {
        var font = FontSources.ForRole(Assets?.Pack, MysticFontRole);
        return ShipwrightText(ship, W.Box, GameText.S(ship.Yes), GameText.S(ship.No), (line, x) =>
        {
            var room = ship.Window.Width - x - 8;
            return font is not null ? UiOverlays.Wrap(line, room / PixelFont.Advance, font)
                : UiOverlays.Wrap(line, (int)(room * k) / PixelFont.Advance);
        });
    }

    /// <summary>The window's height in pack pixels now (the original's 352, plus rows the translation needs).</summary>
    private int ShipwrightHeight(ShipwrightWindow ship, float k) =>
        ship.Window.Height + ShipwrightExtraRows(ship, ShipwrightRows(ship, k).Rows.Count);

    /// <summary>
    /// The shipwright's window under SHIP.PKH at <paramref name="origin"/> (s151): the window grey (titled: 14) from the
    /// picture's last row down, the offer rows, the YES / NO box (moved down by the extra rows), the answer centred on x
    /// 156 at y 300 once the box is gone, the party's gold at (68, 336). Text in font 0, black (<c>_font(0, 0, 0, 16)</c>).
    /// </summary>
    private void DrawShipwrightWindow(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k)
    {
        if (Extras?.Shipwright is not { } ship)
            return;
        var (rows, answer) = ShipwrightRows(ship, k);
        var shift = ShipwrightExtraRows(ship, rows.Count);
        var height = ship.Window.Height + shift;
        batch.Draw(pixel, origin + new Vector2(0, ship.PictureHeight * k), null, Pal(ship.BackColour, Color.LightGray), 0f,
            Vector2.Zero, new Vector2(ship.Window.Width * k, (height - ship.PictureHeight) * k), SpriteEffects.None, 0f);
        var ink = Pal(ship.TextColour, Color.Black);
        foreach (var (x, y, text) in rows)
            PackText(batch, pixel, origin + new Vector2(x * k, y * k), text, ink, k);
        if (PanelName == NoYesName && Assets?.Pack?.CommandPanels?.NoYes is { } noYes)
            DrawNoYes(batch, pixel, noYes, origin + new Vector2(0, shift * k), k);
        if (answer is not null)
            PackText(batch, pixel, origin + new Vector2((ship.AnswerX - (PackWidth(answer, k) >> 1)) * k, (ship.AnswerY + shift) * k),
                answer, ink, k);
        var g = ship.Gold;
        var gold = PanelText(g.Label, g.LabelText) + PaddedAmount(Game.Party.Gold, g.Digits) + PanelText(g.Unit, g.UnitText);
        PackText(batch, pixel, origin + new Vector2(g.X * k, (g.Y + shift) * k), gold, ink, k);
    }
}

/// <summary>A click on <c>_numin</c>'s keypad: a digit to type (null: the key's Click keys, Backspace or Return).</summary>
public sealed record P4KeypadTag(char? Digit);
