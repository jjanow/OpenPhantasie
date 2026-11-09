using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public static partial class UiOverlays
{
    /// <summary>The overworld's palette (<c>0x24010</c>) and the dungeon's (<c>dng.csh</c>, <c>0x244de</c>) in the pack.</summary>
    public const string MapPalette = "map", DungeonPalette = "dungeon";

    /// <summary>
    /// s136: III Amiga's menu bar outside the towns (<see cref="AmigaOutsideBar"/>, list <c>0x23910</c>: File, Options,
    /// Speed) over the screen's top rows 0–10, in colours 15 / 0 of the palette in force (the map's on the overworld, the
    /// dungeon's below); the Speed menu ticks the setting in force ('*', <c>0x870e</c>).
    /// </summary>
    /// <param name="gold">P3 QoL: the session whose Guild treasury gold line the bar's free right end shows (null: none).</param>
    public static void DrawOutsideMenuBar(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, AmigaOutsideBar bar,
        bool inDungeon, AmigaCombatSpeed speed, GameSession? gold = null)
    {
        var palette = inDungeon ? DungeonPalette : MapPalette;
        var places = OutsideMenuBar(assets);
        var open = bar.Open;
        var labels = AmigaMenuLabels(places, open, AmigaOutsideBar.SpeedMenu, speed);
        DrawAmigaMenuBar(batch, pixel, places, i => assets.PaletteColour(palette, i) ?? DosBlack, open, labels, bar.Item,
            (m, span) => new HitRegion(span, new OutsideMenuTitleTag(m)),
            (i, row) => new HitRegion(row, new OutsideMenuItemTag(i)), gold);
    }

    /// <summary>
    /// s156: III Amiga's town bar (<see cref="AmigaTownBar"/>, list <c>0x238a4</c>: File, Guild, Options) over the town
    /// picture's rows 0–10 from the pack's places (<c>keep_windows.menu_bars.town</c>), in colours 15 / 0 of the town's
    /// palette (w4's = the map palette; the dim towns 16–17 fade to <c>town_dim</c>, <paramref name="palette"/>).
    /// <paramref name="input"/> registers the titles, the open menu's items and a backdrop that closes it.
    /// </summary>
    public static void DrawTownMenuBar(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, CombatFieldMenuBar places,
        AmigaTownBar bar, string palette, bool input, GameSession? gold = null)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(bar);
        if (input && bar.Open is not null)
            Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new TownMenuBackdropTag()));
        var labels = AmigaMenuLabels(places, bar.Open, -1, AmigaCombatSpeed.Normal);
        var hits = Hits;
        if (!input)
            Hits = null;
        try
        {
            DrawAmigaMenuBar(batch, pixel, places, i => assets.PaletteColour(palette, i) ?? DosBlack, bar.Open, labels, bar.Item,
                (m, span) => new HitRegion(span, new TownMenuTitleTag(m)),
                (i, row) => new HitRegion(row, new TownMenuItemTag(i)), gold);
        }
        finally
        {
            Hits = hits;
        }
    }

    /// <summary>The bar outside the towns (<c>0x23910</c>): the pack's "outside", else the rule over <see cref="AmigaOutsideBar.Menus"/>.</summary>
    public static CombatFieldMenuBar OutsideMenuBar(AssetCatalog? assets) => P3MenuBar(assets, "outside",
        [.. AmigaOutsideBar.Menus.Select(m => (m.TitleId, (IReadOnlyList<string>)[.. m.Items.Select(i => i.LabelId)]))]);

    /// <summary>
    /// s140: File › Info's credits, III Amiga's window 0 (<c>0x86e6</c> → <c>0x3904</c>, FORMAT.md 8.5): the box and frame,
    /// the text (<see cref="KeepWindowsLayout.MessageWindow.TextIds"/>[0]) as <c>0x854</c> lays a title out — a "\r" goes
    /// one row (8 px) down at the title column, and each row's leading blanks are the Amiga's 8-px cells, so every row starts
    /// where the original's does (the remaster's narrower font then runs on from there) — and the one OK button (a click
    /// presses O). In a dungeon the button shows in the pack's dungeon palette (the Amiga keeps <c>dng.csh</c>'s, s125).
    /// False when the pack lacks the OK art.
    /// </summary>
    public static bool DrawCreditsWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, KeepWindowsLayout layout,
        KeepWindowsLayout.MessageWindow w, bool inDungeon, Point? held)
    {
        var palette = inDungeon ? layout.DungeonPalette : null;
        if (assets.GetInPalette(PictureNames.Button("ok"), layout.ButtonPalette, palette) is not { } ok)
            return false;
        DrawWindowFrame(batch, pixel, layout, w.Box[0], w.Box[1], w.Box[2], w.Box[3]);
        var ink = VirtualScreen.ParseColor(w.TextColour);
        using (PixelFont.With(FontSources.Named(w.Font, w.FontFile))) // s141: topaz 8, the Amiga's 8-px cells
            foreach (var (x, y, text) in CreditsRows(w, UiText.S(w.TextIds[0])))
                PixelFont.Draw(batch, pixel, x, y, text, ink);
        var area = new Rectangle(w.Ok.X, w.Ok.Y, layout.ButtonWidth, layout.ButtonHeight);
        var picture = held is { } p && area.Contains(p)
            && assets.GetInPalette(PictureNames.Button("ok", chosen: true), layout.ButtonPalette, palette) is { } down ? down : ok;
        batch.Draw(picture, new Rectangle(area.X, area.Y, picture.Width, picture.Height), Color.White);
        Hits?.Add(new HitRegion(area) { Click = [Keys.O] });
        return true;
    }

    /// <summary>
    /// The credits' rows as <c>0x854</c> places a window title: row i at y = origin + 8 i (split at "\r" or "\n"), its
    /// first non-blank character at x = origin + 8 · (leading blanks); blank rows are left out. s141: the pack names topaz 8
    /// for the window (<see cref="KeepWindowsLayout.MessageWindow.Font"/>), so every character fills its 8-px cell.
    /// </summary>
    public static IReadOnlyList<(int X, int Y, string Text)> CreditsRows(KeepWindowsLayout.MessageWindow w, string text) =>
        TitleRows(w.Text, text);

    /// <summary><c>0x854</c>'s layout of a window title from <paramref name="origin"/> (see <see cref="CreditsRows"/>).</summary>
    public static IReadOnlyList<(int X, int Y, string Text)> TitleRows((int X, int Y) origin, string text) =>
        text.Split('\r', '\n')
            .Select((row, i) => (X: origin.X + 8 * (row.Length - row.TrimStart(' ').Length), Y: origin.Y + 8 * i,
                Text: row.Trim(' ')))
            .Where(r => r.Text.Length > 0).ToList();

    /// <summary>
    /// s141: Options › Party Status, III Amiga's window 22 (<c>0x8832</c>, FORMAT.md 8.9): the box and frame, the labels
    /// (<see cref="KeepWindowsLayout.PartyStatusWindow.TextIds"/>[0], laid out as <see cref="TitleRows"/>), the values
    /// left-aligned at the value column (<see cref="StatusValues"/>), both in the window's colour index of the palette in
    /// force (the map's outside, the dungeon's below), and the one OK (a click presses O). False when the pack lacks the
    /// OK art.
    /// </summary>
    public static bool DrawPartyStatusWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets,
        KeepWindowsLayout layout, KeepWindowsLayout.PartyStatusWindow w, P3PartyStatus status, bool inDungeon,
        Point? held)
    {
        var palette = inDungeon ? layout.DungeonPalette : null;
        if (assets.GetInPalette(PictureNames.Button("ok"), layout.ButtonPalette, palette) is not { } ok)
            return false;
        DrawWindowFrame(batch, pixel, layout, w.Box[0], w.Box[1], w.Box[2], w.Box[3]);
        var ink = assets.PaletteColour(inDungeon ? DungeonPalette : MapPalette, w.ColourIndex) ?? DosWhite;
        using (PixelFont.With(FontSources.Named(w.Font, w.FontFile)))
            foreach (var (x, y, text) in TitleRows(w.Text, UiText.S(w.TextIds[0])).Concat(StatusValues(w, status)))
                PixelFont.Draw(batch, pixel, x, y, text, ink);
        var area = new Rectangle(w.Ok.X, w.Ok.Y, layout.ButtonWidth, layout.ButtonHeight);
        var picture = held is { } p && area.Contains(p)
            && assets.GetInPalette(PictureNames.Button("ok", chosen: true), layout.ButtonPalette, palette) is { } down ? down : ok;
        batch.Draw(picture, new Rectangle(area.X, area.Y, picture.Width, picture.Height), Color.White);
        Hits?.Add(new HitRegion(area) { Click = [Keys.O] });
        return true;
    }

    /// <summary>
    /// Window 22's values (<c>0xab54</c> / <c>0xab1c</c>: plain decimal from the value column): days out, experience and
    /// gold on the first three value rows, then each kind's count.
    /// </summary>
    public static IReadOnlyList<(int X, int Y, string Text)> StatusValues(KeepWindowsLayout.PartyStatusWindow w,
        P3PartyStatus status) =>
        [.. new[] { status.DaysOut, status.Experience, status.Gold }.Concat(status.Counts.Select(c => (long)c))
            .Take(w.ValueRows.Count)
            .Select((v, i) => (w.ValueX, w.ValueRows[i], v.ToString(System.Globalization.CultureInfo.InvariantCulture)))];
}
