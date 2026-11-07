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
            if (_layout.JustifyWidth > 0)
                DrawJustified(batch, pixel, line.Trim(), _layout.JustifyWidth, _palette[_layout.TextPen]);
            else
            {
                using var font = PixelFont.With(SpeechFont(_layout));
                var (x, text) = SpeechLine(line);
                PixelFont.Draw(batch, pixel, x, _layout.TextY, text, _palette[_layout.TextPen]);
            }
        }
        return true;
    }

    /// <summary>The speech line's font (I Amiga: z6font, the window's font, s153), or null for the pack's own.</summary>
    public static IFontSource? SpeechFont(EndingSceneLayout layout) => FontSources.Named(layout.Font, layout.FontFile);

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
    /// The x of each character of II's bottom line (TOS <c>v_justified</c>, word and character spacing on): the line
    /// centred on the screen and stretched to <paramref name="width"/>. Each space gets up to half a character cell
    /// more, the rest is spread over the gaps between characters (fitted to the s80 title shot <c>[~]</c>). A line wider
    /// than <paramref name="width"/> keeps its natural spacing.
    /// </summary>
    public static IReadOnlyList<int> JustifiedXs(string text, int width)
    {
        ArgumentNullException.ThrowIfNull(text);
        var xs = new List<int>();
        if (text.Length == 0)
            return xs;
        var natural = PixelFont.Measure(text);
        var extra = Math.Max(0, width - natural);
        var spaces = text.Count(c => c == ' ');
        var perSpace = spaces == 0 ? 0 : Math.Min((double)extra / spaces, PixelFont.Advance / 2.0);
        var gaps = text.Length - 1;
        var perGap = gaps == 0 ? 0 : (extra - perSpace * spaces) / gaps;
        var x = (Width - Math.Max(natural, width)) / 2.0;
        foreach (var ch in text)
        {
            xs.Add((int)Math.Round(x));
            x += PixelFont.Measure(ch.ToString()) + perGap + (ch == ' ' ? perSpace : 0);
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
    /// prints them, the form in the button pen; a page without its own palette dims with the fade-out.
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
        for (var i = 0; i < lines.Count; i++)
            PixelFont.Draw(batch, pixel, page.X, page.Y + i * page.LineGap, lines[i].TrimEnd(), Pen(page.TextPen));
        // The OK form (GEM tree 18 at (319 − w, 199 − h)), measured on the s80 shots: frame 247…318 × 175…198, button
        // 262…303 × 182…191.
        var button = Pen(page.ButtonPen);
        Outline(batch, pixel, new Rectangle(247, 175, 72, 24), button);
        Outline(batch, pixel, new Rectangle(262, 182, 42, 10), button);
        const string ok = "OK";
        PixelFont.Draw(batch, pixel, 262 + (42 - PixelFont.Measure(ok)) / 2, 184, ok, button);
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
