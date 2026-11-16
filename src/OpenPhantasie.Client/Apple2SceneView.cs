using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s224: draws an <see cref="Apple2Scene"/> as the Apple's screen shows it, 280 × 192 centred on the 320 × 200 screen:
/// graphics mode = hi-res rows 0–159 (page 1, or <see cref="Apple2Scene.Page2"/> while it flashes) over text rows 21–24;
/// text mode = the whole text page. The hi-res page goes through the packs' NTSC model (<see cref="Apple2Hires.ToRgba"/>),
/// the text through the pack's font on 7 × 8 cells (the Apple's character ROM is not on the disks).
/// </summary>
public sealed class Apple2SceneView
{
    public const int Left = 20;
    public const int Top = 4;
    private const int Rows = 160;

    private readonly GraphicsDevice _device;
    private Texture2D? _texture;
    private (Apple2Scene Scene, int Version, bool Page2)? _drawn;

    public Apple2SceneView(GraphicsDevice device) => _device = device;

    public bool Draw(SpriteBatch batch, Texture2D pixel, Apple2Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        batch.Draw(pixel, new Rectangle(0, 0, 320, UiOverlays.ScreenBottom), Color.Black);
        if (scene.Graphics)
        {
            var key = (scene, scene.Page.Version, scene.ShowPage2);
            if (_texture is null || _drawn != key)
            {
                _texture ??= new Texture2D(_device, Apple2Hires.Width, Rows);
                var page = scene.ShowPage2 && scene.Page2 is { } p2 ? p2 : scene.Page.Mem;
                var rgba = Apple2Hires.ToRgba(page, 0, Rows);
                var colours = new Color[Apple2Hires.Width * Rows];
                for (var i = 0; i < colours.Length; i++)
                    colours[i] = new Color(rgba[4 * i], rgba[4 * i + 1], rgba[4 * i + 2], rgba[4 * i + 3]);
                _texture.SetData(colours);
                _drawn = key;
            }
            batch.Draw(_texture, new Rectangle(Left, Top, Apple2Hires.Width, Rows), Color.White);
            if (!scene.ShowPage2)
                for (var row = 20; row < Apple2TextPage.Rows; row++)
                    DrawRow(batch, pixel, scene.Text, row);
        }
        else
            for (var row = 0; row < Apple2TextPage.Rows; row++)
                DrawRow(batch, pixel, scene.Text, row);
        return true;
    }

    private static void DrawRow(SpriteBatch batch, Texture2D pixel, Apple2TextPage text, int row)
    {
        for (var col = 0; col < Apple2TextPage.Cols; col++)
        {
            var ch = text[col, row];
            if (ch != ' ')
                PixelFont.Draw(batch, pixel, Left + col * 7 + 1, Top + row * 8, ch.ToString(), Color.White);
        }
    }
}
