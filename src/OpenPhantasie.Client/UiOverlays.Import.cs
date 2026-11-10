using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenPhantasie.Client;

public static partial class UiOverlays
{
    /// <summary>
    /// s185 (Pad leftovers): the import screen (<see cref="ImportScreen"/>): the menu screen, its rows down to the help line,
    /// and, when the rows (the report, the folder browser's rows) are more than fit, a scroll bar on their right
    /// (<see cref="ImportScreen.Scroll"/>; the wheel over the rows and a click or drag on the bar move it).
    /// </summary>
    public static void DrawImportScreen(SpriteBatch batch, Texture2D pixel, ImportScreen import)
    {
        ArgumentNullException.ThrowIfNull(import);
        var lines = import.Lines(); // lays the scroll out
        var box = import.Scroll;
        var area = ImportRowsArea(import.BodyLine, box.Visible);
        // The wheel's area goes under the rows' own hover and clicks (the topmost region answers the pointer).
        DrawMenuScreen(batch, pixel, lines, import.Help, top: ImportScreen.FirstLineY, helpY: 186,
            scroll: box.CanScroll ? (box, area) : null);
        DrawScrollBar(batch, pixel, box, area); // nothing when everything fits
    }

    /// <summary>
    /// The import screen's rows from screen line <paramref name="bodyLine"/> (the title is line 0), <paramref name="rows"/>
    /// of them: the text's width (x 14 … 302) and the bar's room right of it, inside the panel (x 6 … 313).
    /// </summary>
    public static Rectangle ImportRowsArea(int bodyLine, int rows) =>
        new(12, ImportScreen.FirstLineY + (bodyLine - 1) * ImportScreen.LinePitch - 1, 298, rows * ImportScreen.LinePitch);
}
