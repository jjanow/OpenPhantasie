using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s224: draws <see cref="C64WinScreen"/>: the intro / closing page (the ending scene's <c>intro_page</c> /
/// <c>closing_page</c>: its background colour, Core's lines in its text colour), or the hall
/// (<see cref="PictureNames.EndingFinal"/>) with the crowd's frame and the party's figures as the VIC's sprites (their own
/// size, transparent pixels leave the hall), the presented member in its attacking pose in the present colour, and the
/// panel's text on 8 × 8 cells in the pack's font. Colours are the ending palette's.
/// </summary>
public sealed class C64WinView
{
    private const int Width = 320;
    private const int Height = 200;

    private readonly GraphicsDevice _device;
    private readonly AssetCatalog _assets;
    private readonly EndingSceneLayout _ending;
    private readonly C64WinLayout _layout;
    private readonly Color[] _palette;
    private readonly Dictionary<Texture2D, Color[]> _pixels = new(ReferenceEqualityComparer.Instance);
    private Texture2D? _texture;
    private object? _drawnKey;

    public C64WinView(GraphicsDevice device, AssetCatalog assets, EndingSceneLayout ending)
    {
        _device = device;
        _assets = assets;
        _ending = ending;
        _layout = ending.C64Win ?? throw new ArgumentException("no c64_win layout", nameof(ending));
        _palette = [.. ending.Palette.Select(VirtualScreen.ParseColor)];
    }

    public bool Draw(SpriteBatch batch, Texture2D pixel, C64WinScreen screen, C64WinScreen.Frame frame)
    {
        batch.Draw(pixel, new Rectangle(0, 0, Width, UiOverlays.ScreenBottom), Color.Black);
        if (frame.Page != C64WinScreen.PageKind.None)
        {
            var page = frame.Page == C64WinScreen.PageKind.Intro ? _ending.IntroPage : _ending.ClosingPage;
            var lines = frame.Page == C64WinScreen.PageKind.Intro ? screen.Scene.IntroPage : screen.Scene.ClosingPage;
            if (page is null)
                return false;
            Color[] colours = page.Palette is { } own ? [.. own.Select(VirtualScreen.ParseColor)] : _palette;
            if (page.BackgroundPen is int back)
                batch.Draw(pixel, new Rectangle(0, 0, Width, Height), colours[back]);
            for (var i = 0; i < lines.Count; i++)
                PixelFont.Draw(batch, pixel, page.X, page.Y + i * page.LineGap, lines[i].TrimEnd(), colours[page.TextPen]);
            return true;
        }
        if (_assets.Get(PictureNames.EndingFinal) is not { } hall)
            return false;
        var key = (screen, frame.Band, frame.Crowd, frame.Presented);
        if (_texture is null || !Equals(_drawnKey, key))
        {
            _texture ??= new Texture2D(_device, Width, Height);
            _texture.SetData(Compose(hall, screen, frame));
            _drawnKey = key;
        }
        batch.Draw(_texture, new Rectangle(0, 0, Width, Height), Color.White);
        foreach (var row in frame.Text)
            PixelFont.Draw(batch, pixel, row.Col * 8, row.Row * 8, row.Text, _palette[row.Colour]);
        return true;
    }

    private Color[] Compose(Texture2D hall, C64WinScreen screen, C64WinScreen.Frame frame)
    {
        var dst = new Color[Width * Height];
        Array.Fill(dst, Color.Black);
        Blit(dst, hall, 0, 0, null);
        if (!frame.Band)
            return dst;
        if (frame.Crowd >= 0 && frame.Crowd < _layout.Crowd.Count && _assets.Get(_layout.Crowd[frame.Crowd]) is { } crowd)
            Blit(dst, crowd, _layout.CrowdX, _layout.CrowdY, null);
        // WIN 0x5bb: the hero band, slot k at x 110 + 24 k (VIC), every slot from the start, pointer P(168) (its idle figure).
        var slots = screen.Scene.Slots;
        for (var k = 0; k < slots.Count; k++)
        {
            var race = (int)slots[k].Race;
            var x = _layout.RowLeft + _layout.SlotSize * k;
            if (k == frame.Presented && (_assets.GetPortraitAttacking(race) ?? _assets.GetPortraitIdle(race)) is { } pose)
                Blit(dst, pose, x, _layout.RowTop, _palette[_layout.PresentColour]);
            else if (_assets.GetPortraitIdle(race) is { } idle)
                Blit(dst, idle, x, _layout.RowTop, null);
        }
        return dst;
    }

    /// <summary>The picture's opaque pixels at (x0, y0), clipped; with <paramref name="ink"/> all of them in that colour.</summary>
    private void Blit(Color[] dst, Texture2D art, int x0, int y0, Color? ink)
    {
        var src = PixelsOf(art);
        for (var y = 0; y < art.Height; y++)
            for (var x = 0; x < art.Width; x++)
            {
                int sx = x0 + x, sy = y0 + y;
                var c = src[y * art.Width + x];
                if (c.A != 0 && sx is >= 0 and < Width && sy is >= 0 and < Height)
                    dst[sy * Width + sx] = ink ?? c;
            }
    }

    private Color[] PixelsOf(Texture2D art)
    {
        if (_pixels.TryGetValue(art, out var hit))
            return hit;
        var data = new Color[art.Width * art.Height];
        art.GetData(data);
        return _pixels[art] = data;
    }
}
