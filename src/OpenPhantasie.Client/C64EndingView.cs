using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s223: draws <see cref="C64EndingScreen"/>: Zeus (<see cref="PictureNames.EndingFinal"/>) as far as the wipe has come, the
/// clouds / figures, the lightning hand, the face and the grant hand as the VIC's sprites (at their own 24 × 21 size, over
/// the characters; the cells under the face and the hand printed as spaces), and the text in the pack's font on 8 × 8
/// cells. Colours are the ending palette's.
/// </summary>
public sealed class C64EndingView
{
    private const int Width = 320;
    private const int Height = 200;

    private readonly GraphicsDevice _device;
    private readonly AssetCatalog _assets;
    private readonly C64EndingLayout _layout;
    private readonly Color[] _palette;
    private readonly Dictionary<Texture2D, Color[]> _pixels = new(ReferenceEqualityComparer.Instance);
    private Texture2D? _texture;
    private object? _drawnKey;

    public C64EndingView(GraphicsDevice device, AssetCatalog assets, EndingSceneLayout ending)
    {
        _device = device;
        _assets = assets;
        _layout = ending.C64 ?? throw new ArgumentException("no c64 ending layout", nameof(ending));
        _palette = [.. ending.Palette.Select(VirtualScreen.ParseColor)];
    }

    public bool Draw(SpriteBatch batch, Texture2D pixel, C64EndingScreen screen, C64EndingScreen.Frame frame)
    {
        if (_assets.Get(PictureNames.EndingFinal) is not { } zeus)
            return false;
        var key = (screen, frame.CloudY, frame.CloudColour, frame.Figures, frame.ZeusRows, frame.Strike, frame.Face, frame.Hand,
            frame.Page);
        if (_texture is null || !Equals(_drawnKey, key))
        {
            _texture ??= new Texture2D(_device, Width, Height);
            _texture.SetData(Compose(zeus, screen, frame));
            _drawnKey = key;
        }
        batch.Draw(pixel, new Rectangle(0, 0, Width, UiOverlays.ScreenBottom), Color.Black);
        batch.Draw(_texture, new Rectangle(0, 0, Width, Height), Color.White);
        foreach (var row in frame.Text)
            PixelFont.Draw(batch, pixel, row.Col * 8, row.Row * 8, row.Text, _palette[row.Colour]);
        return true;
    }

    private Color[] Compose(Texture2D zeus, C64EndingScreen screen, C64EndingScreen.Frame frame)
    {
        var dst = new Color[Width * Height];
        Array.Fill(dst, Color.Black);
        if (frame.Page)
            return dst; // s224: the closing page: Zeus cleared, every sprite off, only its text
        // ZALL $4CC0: one pixel row of Zeus's 12 columns per step, from the bottom up.
        var src = PixelsOf(zeus);
        for (var y = _layout.WipeY + _layout.WipeHeight - frame.ZeusRows; y < _layout.WipeY + _layout.WipeHeight; y++)
            for (var x = _layout.WipeX; x < _layout.WipeX + _layout.WipeWidth; x++)
                if (x is >= 0 and < Width && y is >= 0 and < Height && y < zeus.Height && x < zeus.Width)
                    dst[y * Width + x] = src[y * zeus.Width + x];
        if (frame.Face >= 0)
            Fill(dst, _layout.FaceBlank);
        if (frame.Hand >= 0)
            Fill(dst, _layout.HandBlank);
        if (frame.CloudY >= 0)
        {
            var xs = C64EndingScreen.CloudXs(screen.Scene.Slots.Count, _layout);
            var cloud = _assets.Get(_layout.Cloud);
            var mc1 = _palette[_layout.CloudMc1];
            for (var k = 0; k < xs.Count; k++)
            {
                if (k < frame.Figures)
                {
                    if (_assets.GetPortraitIdle((int)screen.Scene.Slots[k].Race) is { } figure)
                        Blit(dst, figure, xs[k], frame.CloudY, _ => _palette[screen.FigureColour(k)]);
                }
                else if (cloud is not null)
                    Blit(dst, cloud, xs[k], frame.CloudY,
                        c => c == mc1 ? _palette[frame.CloudColour] : c);
            }
        }
        if (frame.Strike && _assets.Get(_layout.StrikeHand) is { } strike)
            Blit(dst, strike, _layout.StrikeX, _layout.StrikeY, c => c);
        if (frame.Face >= 0 && frame.Face < _layout.Face.Count && _assets.Get(_layout.Face[frame.Face]) is { } face)
            Blit(dst, face, _layout.FaceX, _layout.FaceY, c => c);
        if (frame.Hand >= 0 && frame.Hand < _layout.Hand.Count && _assets.Get(_layout.Hand[frame.Hand]) is { } hand)
            Blit(dst, hand, _layout.HandX, _layout.HandY, c => c);
        return dst;
    }

    private static void Fill(Color[] dst, IReadOnlyList<int> box)
    {
        for (var y = Math.Max(0, box[1]); y < Math.Min(Height, box[1] + box[3]); y++)
            for (var x = Math.Max(0, box[0]); x < Math.Min(Width, box[0] + box[2]); x++)
                dst[y * Width + x] = Color.Black;
    }

    /// <summary>A sprite's opaque pixels at (x0, y0), clipped; <paramref name="ink"/> maps each colour (recolouring).</summary>
    private void Blit(Color[] dst, Texture2D art, int x0, int y0, Func<Color, Color> ink)
    {
        var src = PixelsOf(art);
        for (var y = 0; y < art.Height; y++)
            for (var x = 0; x < art.Width; x++)
            {
                int sx = x0 + x, sy = y0 + y;
                var c = src[y * art.Width + x];
                if (c.A != 0 && sx is >= 0 and < Width && sy is >= 0 and < Height)
                    dst[sy * Width + sx] = ink(c);
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
