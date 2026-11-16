using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s223: draws <see cref="C64DefeatScreen"/> as the VIC shows <c>CEMETARY</c>: the door (sprite 7, behind the characters:
/// it shows only where the temple's pixels are background), the temple, the ghosts and figures (their own 24 × 21) and the
/// face (in front), the text on 8 × 8 cells in the pack's font. Colours are the pack's palette (Colodore).
/// </summary>
public sealed class C64DefeatView
{
    private const int Width = 320;
    private const int Height = 200;

    private readonly GraphicsDevice _device;
    private readonly AssetCatalog _assets;
    private readonly C64DefeatLayout _layout;
    private readonly Color[] _palette;
    private readonly Dictionary<Texture2D, Color[]> _pixels = new(ReferenceEqualityComparer.Instance);
    private Texture2D? _texture;
    private object? _drawnKey;

    public C64DefeatView(GraphicsDevice device, AssetCatalog assets, C64DefeatLayout layout, IReadOnlyList<string> palette)
    {
        _device = device;
        _assets = assets;
        _layout = layout;
        _palette = [.. palette.Select(VirtualScreen.ParseColor)];
    }

    public bool Draw(SpriteBatch batch, Texture2D pixel, C64DefeatScreen screen, C64DefeatScreen.Frame frame)
    {
        if (_assets.Get(_layout.Temple) is not { } temple)
            return false;
        var key = (screen, frame.Temple, frame.Ghosts, frame.DoorY, frame.Face, frame.Fates, frame.MorphSlot, frame.MorphBytes);
        if (_texture is null || !Equals(_drawnKey, key))
        {
            _texture ??= new Texture2D(_device, Width, Height);
            _texture.SetData(Compose(temple, screen, frame));
            _drawnKey = key;
        }
        batch.Draw(pixel, new Rectangle(0, 0, Width, UiOverlays.ScreenBottom), Color.Black);
        batch.Draw(_texture, new Rectangle(0, 0, Width, Height), Color.White);
        foreach (var row in frame.Text)
            PixelFont.Draw(batch, pixel, row.Col * 8, row.Row * 8, row.Text, _palette[row.Colour]);
        return true;
    }

    private Color[] Compose(Texture2D temple, C64DefeatScreen screen, C64DefeatScreen.Frame frame)
    {
        var dst = new Color[Width * Height];
        Array.Fill(dst, Color.Black);
        if (!frame.Temple)
            return dst;
        if (_assets.Get(_layout.Door) is { } door)
            Blit(dst, door, _layout.DoorX, frame.DoorY, c => c);
        // The temple's characters over the door ($D01B bit 7: sprite 7 behind the foreground).
        var src = PixelsOf(temple);
        for (var y = 0; y < Math.Min(Height, temple.Height); y++)
            for (var x = 0; x < Math.Min(Width, temple.Width); x++)
            {
                var c = src[y * temple.Width + x];
                if (c.A != 0 && (c.R | c.G | c.B) != 0)
                    dst[y * Width + x] = c;
            }
        var fates = screen.Defeat.Fates;
        var xs = C64DefeatScreen.GhostXs(fates.Count, _layout);
        var ghost = _assets.Get(_layout.Ghost);
        for (var k = 0; k < Math.Min(frame.Ghosts, fates.Count); k++)
        {
            if (k == frame.MorphSlot && ghost is not null)
            {
                // s224: the ghost's block overwritten byte by byte with the figure's, still white (every fate, the destroyed too).
                var figure = _assets.GetPortraitIdle((int)fates[k].Member.BirthRace);
                Morph(dst, ghost, figure, xs[k], _layout.RowY, frame.MorphBytes);
                continue;
            }
            if (k < frame.Fates)
            {
                var (member, fate) = fates[k];
                if (fate == DefeatFate.Destroyed)
                    continue; // CEMETARY 0x863: the copy, then the sprite off
                // CSPRIT block P(145): the member's own figure (an UNDEAD fate keeps it; only the race becomes 15).
                if (_assets.GetPortraitIdle((int)member.BirthRace) is { } figure)
                    Blit(dst, figure, xs[k], _layout.RowY, _ => _palette[screen.FateColour(fate)]);
            }
            else if (ghost is not null)
                Blit(dst, ghost, xs[k], _layout.RowY, c => c);
        }
        if (frame.Face && _assets.Get(_layout.Face) is { } face)
            Blit(dst, face, _layout.FaceX, _layout.FaceY, c => c);
        return dst;
    }

    /// <summary>
    /// s224: a sprite block half copied: its first <paramref name="bytes"/> bytes (byte i = pixel row i / 3, pixels
    /// 8 · (i mod 3) … + 7) from <paramref name="figure"/>, the rest from <paramref name="ghost"/>, all in the ghost's colour.
    /// </summary>
    private void Morph(Color[] dst, Texture2D ghost, Texture2D? figure, int x0, int y0, int bytes)
    {
        var g = PixelsOf(ghost);
        var f = figure is null ? null : PixelsOf(figure);
        var ink = Color.Black;
        foreach (var c in g)
            if (c.A != 0)
            {
                ink = c;
                break;
            }
        for (var y = 0; y < ghost.Height; y++)
            for (var x = 0; x < ghost.Width; x++)
            {
                var copied = y * 3 + x / 8 < bytes;
                var on = copied
                    ? f is not null && x < figure!.Width && y < figure.Height && f[y * figure.Width + x].A != 0
                    : g[y * ghost.Width + x].A != 0;
                int sx = x0 + x, sy = y0 + y;
                if (on && sx is >= 0 and < Width && sy is >= 0 and < Height)
                    dst[sy * Width + sx] = ink;
            }
    }

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
