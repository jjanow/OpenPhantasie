using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenPhantasie.Client;

public static partial class UiOverlays
{
    /// <summary>
    /// s156: one of I DOS's map pages at DOS's places (<see cref="P1DosPageLayout"/>): the cells black, a band's 2-px white
    /// rules in its top row's lines 1–2 and its bottom row's lines 5–6 across the screen, or a report window's frame inset the
    /// same way in its edge cells (DOSBox-X: the wall band s154, the gate text s156, the swim / inn windows s43 / s45); each
    /// character in its own 8-px cell, in the remaster's font (DOS's CGA font is not drawn), white.
    /// </summary>
    public static void DrawP1DosPage(SpriteBatch batch, Texture2D pixel, P1DosPageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        const int cell = 8;
        int x0 = layout.Left * cell, x1 = (layout.Right + 1) * cell, y0 = layout.Top * cell, y1 = (layout.Bottom + 1) * cell;
        Fill(batch, pixel, new Rectangle(x0, y0, x1 - x0, y1 - y0), Color.Black);
        if (layout.Framed)
        {
            int left = x0 + 1, right = layout.Right * cell + 5, top = y0 + 1, bottom = layout.Bottom * cell + 5;
            Fill(batch, pixel, new Rectangle(left, top, right + 2 - left, 2), Color.White);
            Fill(batch, pixel, new Rectangle(left, bottom, right + 2 - left, 2), Color.White);
            Fill(batch, pixel, new Rectangle(left, top, 2, bottom + 2 - top), Color.White);
            Fill(batch, pixel, new Rectangle(right, top, 2, bottom + 2 - top), Color.White);
        }
        else
        {
            Fill(batch, pixel, new Rectangle(x0, y0 + 1, x1 - x0, 2), Color.White);
            Fill(batch, pixel, new Rectangle(x0, layout.Bottom * cell + 5, x1 - x0, 2), Color.White);
        }
        foreach (var text in layout.Texts)
        {
            var i = 0;
            foreach (var r in text.Text.EnumerateRunes())
            {
                var x = (text.Column + i++) * cell;
                if (r.Value != ' ')
                    PixelFont.Draw(batch, pixel, x + (cell - PixelFont.GlyphWidth) / 2, text.Row * cell, r.ToString(), Color.White);
            }
        }
        Hits?.Clear(); // a key or a click turns the page
    }
}
