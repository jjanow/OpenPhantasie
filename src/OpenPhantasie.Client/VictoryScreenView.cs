using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Draws <see cref="VictoryScreen"/> (I Amiga): the picture, the arm, the ghosts and portraits are put together in pen
/// numbers of the pack's <see cref="EndingSceneLayout.Palette"/> (each art colour is one pen there), so the fades act per
/// pen as the Amiga's <c>SetRGB4</c> calls do; the speech line goes on top in the text pen while that pen shows.
/// II ST (s128) adds the stage portrait, the crowd frames, the row by record, justified bottom lines and the text pages.
/// </summary>
public sealed class VictoryScreenView
{
    private const int Width = 320;
    private const int Height = 200;

    private readonly GraphicsDevice _device;
    private readonly AssetCatalog _assets;
    private readonly EndingSceneLayout _layout;
    private readonly Color[] _palette;
    private readonly Dictionary<Texture2D, byte[]> _pens = new(ReferenceEqualityComparer.Instance);
    private Texture2D? _texture;
    private (int, int, bool, int, int, bool, int, bool, bool, int)? _drawnKey;
    private VictoryScreen? _drawnFor;

    public VictoryScreenView(GraphicsDevice device, AssetCatalog assets, EndingSceneLayout layout)
    {
        _device = device;
        _assets = assets;
        _layout = layout;
        _palette = [.. layout.Palette.Select(VirtualScreen.ParseColor)];
    }

    /// <summary>Draw the screen at <paramref name="frame"/>; false when the pack lacks the picture.</summary>
    public bool Draw(SpriteBatch batch, Texture2D pixel, VictoryScreen screen, VictoryScreen.Frame frame)
    {
        if (_assets.Get(PictureNames.EndingFinal) is not { } picture)
            return false;
        if (frame.Page != VictoryScreen.EndingPageKind.None)
        {
            DrawPage(batch, pixel, screen, frame);
            return true;
        }
        var key = (frame.LitPens, frame.DarkPens, frame.Ghosts, frame.GhostCount, frame.Granted, frame.Arm, frame.Stage, frame.StagePose,
            frame.StageCleared, frame.Crowd);
        if (_texture is null || _drawnKey != key || !ReferenceEquals(_drawnFor, screen))
        {
            _texture ??= new Texture2D(_device, Width, Height);
            _texture.SetData(Compose(picture, screen, frame));
            _drawnKey = key;
            _drawnFor = screen;
        }
        batch.Draw(pixel, new Rectangle(0, 0, Width, UiOverlays.ScreenBottom), Color.Black);
        batch.Draw(_texture, new Rectangle(0, 0, Width, Height), Color.White);
        if (frame.Text is { } line && frame.Shows(_layout.TextPen))
        {
            using var font = PixelFont.With(SpeechFont(_layout)); // II ST (s160): EmuTOS's 8×8; I Amiga: z6font
            if (_layout.JustifyWidth > 0)
                DrawJustified(batch, pixel, line.Trim(), _layout.JustifyWidth, _palette[_layout.TextPen]);
            else
            {
                var (x, text) = SpeechLine(line);
                PixelFont.Draw(batch, pixel, x, _layout.TextY, text, _palette[_layout.TextPen]);
            }
        }
        return true;
    }

    /// <summary>The speech line's font (I Amiga: z6font, the window's font, s153), or null for the pack's own.</summary>
    public static IFontSource? SpeechFont(EndingSceneLayout layout) => FontSources.Named(layout.Font, layout.FontFile);

    /// <summary>A text page's font (II ST, s160: EmuTOS's 8×8 italic), or null for the pack's own.</summary>
    public static IFontSource? PageFont(EndingPage page) => FontSources.Named(page.Font, page.FontFile);

    /// <summary>s162: the OK label's font (II ST: EmuTOS's 6×6 small font, <c>te_font</c> 5), or null for the pack's own.</summary>
    public static IFontSource? ButtonFont(EndingPage page) => FontSources.Named(page.ButtonFont, page.ButtonFontFile);

    /// <summary>The OK form's button (GEM tree 18 at (319 − w, 199 − h), measured on the s80 shots): its border box.</summary>
    public static readonly Rectangle OkButton = new(262, 182, 42, 10);

    /// <summary>
    /// s162: where the OK label starts in the current font: centred in the G_BOXTEXT inside the button's 1-px border
    /// (OUT.RSC tree 18: 5 × 1 cells at (2, 1), <c>te_just</c> centred), the cell's top centred in the 8-px row as
    /// <c>objc_draw</c> places a 6-px font: the s80 shot's O at (277, 184).
    /// </summary>
    public static Point OkLabelAt(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        var inner = new Rectangle(OkButton.X + 1, OkButton.Y + 1, OkButton.Width - 2, OkButton.Height - 2);
        return new Point(inner.X + (inner.Width - PixelFont.Measure(label)) / 2,
            inner.Y + Math.Max(0, (inner.Height - PixelFont.Font.Height) / 2));
    }

    /// <summary>
    /// A centred speech line in the current font: DOS pads its lines with spaces to justify them; the Amiga centres
    /// single-spaced lines on 320 (<c>h80 +0x84a</c>, IntuiTextLength of the whole text; h33 <c>+0x2</c> …).
    /// </summary>
    public static (int X, string Text) SpeechLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return ((Width - PixelFont.Measure(text)) / 2, text);
    }

    /// <summary>
    /// The x of each character of II's bottom line in the current font: TOS <c>v_justified</c> with word and character
    /// spacing on, centred on the screen (s160, read from EmuTOS <c>vdi_text.c</c> <c>vdi_v_justified</c> /
    /// <c>output_text</c>, integer arithmetic as there): the line starts at 160 − <paramref name="width"/> / 2; each space
    /// gets (width − natural) / spaces more, capped at ± half the font's widest cell (the cap drops the remainder), the
    /// first remainder spaces one more; what is left over is spread over the gaps between characters,
    /// (rest) / (characters − 1) each and one more for the first remainder ones. A line wider than
    /// <paramref name="width"/> is closed up the same way (negative gaps), as the ST does. Matches the s80 title shot
    /// ("KLEPTO - the bold from Ferronrah": 5, 15, 25, 35, 44, …) pixel for pixel.
    /// </summary>
    public static IReadOnlyList<int> JustifiedXs(string text, int width)
    {
        ArgumentNullException.ThrowIfNull(text);
        var xs = new List<int>();
        if (text.Length == 0)
            return xs;
        var advances = text.Select(c => PixelFont.Font.Glyph(c).Advance).ToArray();
        var natural = advances.Sum();
        var spaces = text.Count(c => c == ' ');
        int delWord = 0, rmWord = 0, wordDir = 1;
        if (spaces > 0)
        {
            delWord = (width - natural) / spaces;
            rmWord = (width - natural) % spaces;
            if (rmWord < 0)
                (wordDir, rmWord) = (-1, -rmWord);
            var expand = advances.Max() / 2; // max_cell_width / 2
            if (delWord > expand || delWord < -expand)
                (delWord, rmWord) = (Math.Clamp(delWord, -expand, expand), 0);
            natural += delWord * spaces + rmWord * wordDir;
        }
        int delChar = 0, rmChar = 0, charDir = 1;
        if (text.Length > 1)
        {
            delChar = (width - natural) / (text.Length - 1);
            rmChar = (width - natural) % (text.Length - 1);
            if (rmChar < 0)
                (charDir, rmChar) = (-1, -rmChar);
        }
        var x = Width / 2 - width / 2;
        for (var i = 0; i < text.Length; i++)
        {
            xs.Add(x);
            x += advances[i] + delChar;
            if (rmChar > 0)
            {
                x += charDir;
                rmChar--;
            }
            if (text[i] != ' ')
                continue;
            x += delWord;
            if (rmWord > 0)
            {
                x += wordDir;
                rmWord--;
            }
        }
        return xs;
    }

    private void DrawJustified(SpriteBatch batch, Texture2D pixel, string text, int width, Color colour)
    {
        var xs = JustifiedXs(text, width);
        for (var i = 0; i < text.Length; i++)
            if (text[i] != ' ')
                PixelFont.Draw(batch, pixel, xs[i], _layout.TextY, text[i].ToString(), colour);
    }

    /// <summary>
    /// II's text pages (TOS <c>v_gtext</c> on a black screen, then the OK form at the bottom right): the lines as the ST
    /// prints them in the page's font (<see cref="PageFont"/>), the form in the button pen; a page without its own palette
    /// dims with the fade-out; the OK label in the page's button font (s162: EmuTOS's 6×6 small font, <c>te_font</c> 5).
    /// </summary>
    private void DrawPage(SpriteBatch batch, Texture2D pixel, VictoryScreen screen, VictoryScreen.Frame frame)
    {
        var intro = frame.Page == VictoryScreen.EndingPageKind.Intro;
        if ((intro ? _layout.IntroPage : _layout.ClosingPage) is not { } page)
            return;
        var lines = intro ? screen.Scene.IntroPage : screen.Scene.ClosingPage;
        Color[] palette = page.Palette is { } own ? [.. own.Select(VirtualScreen.ParseColor)] : _palette;
        Color Pen(int pen) => page.Palette is not null || frame.Shows(pen) ? palette[pen] : Color.Black;
        batch.Draw(pixel, new Rectangle(0, 0, Width, UiOverlays.ScreenBottom), Color.Black);
        using (PixelFont.With(PageFont(page))) // s160: EmuTOS's 8×8 italic, its cell 4 px left of the v_gtext point
            for (var i = 0; i < lines.Count; i++)
                PixelFont.Draw(batch, pixel, page.X + page.FontDx, page.Y + i * page.LineGap, lines[i].TrimEnd(),
                    Pen(page.TextPen));
        // The OK form (GEM tree 18 at (319 − w, 199 − h)), measured on the s80 shots: frame 247…318 × 175…198, button
        // 262…303 × 182…191.
        var button = Pen(page.ButtonPen);
        Outline(batch, pixel, new Rectangle(247, 175, 72, 24), button);
        Outline(batch, pixel, OkButton, button);
        const string ok = "OK";
        using (PixelFont.With(ButtonFont(page)))
        {
            var at = OkLabelAt(ok);
            PixelFont.Draw(batch, pixel, at.X, at.Y, ok, button);
        }
    }

    private static void Outline(SpriteBatch batch, Texture2D pixel, Rectangle r, Color colour)
    {
        batch.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), colour);
        batch.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), colour);
        batch.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), colour);
        batch.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), colour);
    }

    private Color[] Compose(Texture2D picture, VictoryScreen screen, VictoryScreen.Frame frame)
    {
        var pens = new byte[Width * Height];
        Blit(pens, picture, 0, 0);
        var slots = screen.Scene.Slots;
        if (frame.Ghosts)
        {
            if (frame.Arm && _layout.Arm is { } armName && _assets.Get(armName) is { } arm) // s128: after the sound between them
                Blit(pens, arm, _layout.ArmX, _layout.ArmY);
            var xs = VictoryScreen.SlotXs(slots, _layout.SlotSize);
            var ghost = _layout.Ghost is { } ghostName ? _assets.Get(ghostName) : null;
            foreach (var (slot, portrait) in VictoryScreen.Drawn(slots, frame.Ghosts, frame.Granted, frame.GhostCount))
                if ((portrait ? _assets.GetPortraitIdle((int)slots[slot].Race) : ghost) is { } art)
                    Blit(pens, art, xs[slot], _layout.RowTop);
        }
        if (_layout.Crowd is { } crowd && frame.Crowd >= 0 && frame.Crowd < crowd.Frames.Count &&
            _assets.Get(crowd.Frames[frame.Crowd]) is { } cheer)
            Blit(pens, cheer, crowd.X, crowd.Y);
        if (_layout.RowByRecord)
        {
            var xs = VictoryScreen.SlotXs(slots, _layout.SlotSize);
            foreach (var (slot, _) in VictoryScreen.DrawnByRecord(slots, frame.Granted))
                if (_assets.GetPortraitIdle((int)slots[slot].Race) is { } art)
                    Blit(pens, art, xs[slot], _layout.RowTop);
        }
        if (_layout.StageX is int sx && _layout.StageY is int sy)
        {
            if (frame.StageCleared)
                Clear(pens, sx, sy, _layout.SlotSize, _layout.SlotSize);
            else if (frame.Stage >= 0 && frame.Stage < slots.Count)
            {
                var race = (int)slots[frame.Stage].Race;
                if ((frame.StagePose ? _assets.GetPortraitAttacking(race) : _assets.GetPortraitIdle(race)) is { } art)
                    Blit(pens, art, sx, sy);
            }
        }
        var colours = new Color[pens.Length];
        for (var i = 0; i < pens.Length; i++)
            colours[i] = frame.Shows(pens[i]) ? _palette[pens[i]] : Color.Black;
        return colours;
    }

    /// <summary>Set a box to pen 0, clipped (II's <c>vro_cpyfm</c> mode 0 on the stage spot).</summary>
    private static void Clear(byte[] pens, int x0, int y0, int w, int h)
    {
        for (var y = Math.Max(0, y0); y < Math.Min(Height, y0 + h); y++)
            for (var x = Math.Max(0, x0); x < Math.Min(Width, x0 + w); x++)
                pens[y * Width + x] = 0;
    }

    /// <summary>Copy every pixel of <paramref name="art"/> (all pens: the Amiga's blit is a plain copy), clipped.</summary>
    private void Blit(byte[] pens, Texture2D art, int x0, int y0)
    {
        var src = PensOf(art);
        for (var y = 0; y < art.Height; y++)
        {
            var sy = y0 + y;
            if (sy is < 0 or >= Height)
                continue;
            for (var x = 0; x < art.Width; x++)
            {
                var sx = x0 + x;
                if (sx is >= 0 and < Width)
                    pens[sy * Width + sx] = src[y * art.Width + x];
            }
        }
    }

    /// <summary>The pens of <paramref name="art"/>: each colour's pen in the table (transparent = 0, else the nearest).</summary>
    private byte[] PensOf(Texture2D art)
    {
        if (_pens.TryGetValue(art, out var hit))
            return hit;
        var data = new Color[art.Width * art.Height];
        art.GetData(data);
        var pens = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
            pens[i] = data[i].A == 0 ? (byte)0 : Nearest(data[i]);
        return _pens[art] = pens;
    }

    private byte Nearest(Color c)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var p = 0; p < _palette.Length; p++)
        {
            var d = Sq(c.R - _palette[p].R) + Sq(c.G - _palette[p].G) + Sq(c.B - _palette[p].B);
            if (d < bestDistance)
                (best, bestDistance) = (p, d);
        }
        return (byte)best;
    }

    private static int Sq(int v) => v * v;
}
