using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public static partial class UiOverlays
{
    /// <summary>
    /// s154: one of I Amiga's requesters (<see cref="P1MapRequesters"/>): its steps in order, the titles in topaz 8 and the
    /// rest in z6font (the pack's <c>map_requesters</c> fonts). Every click stops at it (the Amiga's h86 wait drops clicks
    /// outside a gadget); a click on a button sends the button's key.
    /// </summary>
    public static void DrawP1Requester(SpriteBatch batch, Texture2D pixel, MapRequestersLayout layout, RequesterScene scene,
        AssetCatalog? assets = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(scene);
        Color[] pens = [.. scene.Palette.Select(VirtualScreen.ParseColor)];
        var body = FontSources.Named(layout.Font, layout.FontFile);
        var title = FontSources.Named(layout.TitleFont, layout.TitleFontFile) ?? body;
        foreach (var op in scene.Ops)
        {
            var r = new Rectangle(op.Rect.X, op.Rect.Y, op.Rect.W, op.Rect.H);
            switch (op.Kind)
            {
                case RequesterOpKind.Fill:
                    Fill(batch, pixel, r, pens[op.Pen]);
                    break;
                case RequesterOpKind.Outline:
                    Outline(batch, pixel, r, pens[op.Pen]);
                    break;
                case RequesterOpKind.Picture:
                    if (assets?.Get(op.Text) is { } picture)
                        batch.Draw(picture, r, Color.White);
                    break;
                default:
                    using (PixelFont.With(op.Title ? title : body))
                        PixelFont.Draw(batch, pixel, r.X, r.Y, op.Text, pens[op.Pen]);
                    break;
            }
        }
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
        foreach (var b in scene.Buttons)
            Hits?.Add(new HitRegion(new Rectangle(b.Rect.X, b.Rect.Y, b.Rect.W, b.Rect.H), new MapMenuButtonTag(b.Index)) { Click = [b.Click] });
    }
}
