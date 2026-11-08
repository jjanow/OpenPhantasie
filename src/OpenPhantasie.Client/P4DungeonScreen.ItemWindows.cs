using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s163: the item windows in the picture's place (FORMAT.md 6.9 "Item lists"): the Armory (<c>_armory</c> /
/// <c>_itemlist</c>), Share out (<c>_distribute</c> / <c>_dvitmlst</c>), Trade, U Use and Identify (<c>_trade</c>,
/// <c>_setuse</c>, <c>_setident</c> / <c>_tritmlst</c>), each the titled 312 × 352 window <c>_wopen(-1, 160, 32, 312, 352,
/// 1, title)</c> with the original's rows: the letter in colour 2, the icon, the name, the points / price / users columns
/// at their x, the headers, the colour-0 line and the sunken frames. The text column keeps its lines and keys (as the
/// training hall's); a click on a row presses its key. `[~]`: Share out's equipment table under its divider is not read.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>One row of an item window: its item, letter (null: none), name and the columns' text (null: blank).</summary>
    public sealed record ItemWindowRow(int Item, char? Letter, string Name, string? Points, string? Price,
        IReadOnlyList<int> Users, bool Lit = false);

    /// <summary>The item window up now: its titled key (the title), the layout's key, the rows and the member's name.</summary>
    private (string Key, string Layout, List<ItemWindowRow> Rows, string? Member, string? Message)? CurrentItemWindow()
    {
        if (!Game.LoopBuilt || Game.Battle is not null || Game.Services is not { } s || Extras?.ItemWindows is null)
            return null;
        string? key = null;
        IReadOnlyList<P4Choice> choices = [];
        int? marked = null;
        if (MenuOpen && M!.Prompt == P4MenuPrompt.Pick && M.IconMenu is "_setuse" or "_setident" or "_trade")
            (key, choices, marked) = (M.IconMenu, M.Choices, M.Marked);
        else if (!MenuOpen && Game.Place == P4Place.Town && W.Prompt == P4TownPrompt.Pick
                 && W.IconMenu is "_armory" or "_trade" or "_distribute")
            (key, choices, marked) = (W.IconMenu, W.Choices, W.Marked);
        if (key is null || Extras.Titled?.ContainsKey(key) != true)
            return null;
        var members = Game.Party.Members;
        IReadOnlyList<int> Users(int item) => s.Items[item] is { } info
            ? [.. members.Select((m, i) => (m, i)).Where(p => P4Equipment.CanUse(info, p.m)).Select(p => p.i)] : [];
        int Points(int item) => P4Town.ItemPoints(Game.Options, Game.Tables.Towns, item, s.Items[item]?.Points ?? 0);
        var rows = new List<ItemWindowRow>();
        string? message = null;
        switch (key)
        {
            case "_armory":
            {
                var who = marked ?? 0;
                var notUsable = Extras.ItemWindows.Windows.GetValueOrDefault("_armory")?.NotUsable is { } nu
                    ? PanelText(nu.Label, nu.Text) : "N-U";
                foreach (var (c, i) in choices.TakeWhile(c => c.Item != 0).Select((c, i) => (c, i)))
                {
                    var usable = who < members.Count && s.Items[c.Item] is { } info && P4Equipment.CanUse(info, members[who]);
                    rows.Add(new(c.Item, (char)('A' + i), s.Items.Name(c.Item),
                        c.Item is >= 1 and <= 84 ? usable ? PaddedAmount(Points(c.Item), 4) : notUsable : null,
                        PaddedAmount(W.ShopPrice(c.Item), 5), []));
                }
                message = W.Box.LastOrDefault(l => l.Length > 0);
                break;
            }
            case "_distribute":
                foreach (var (item, i) in new[] { W.HeldItem }.Concat(W.Queue).Take(DistributeRows).Select((x, i) => (x, i)))
                    rows.Add(new(item, null, s.Items.Name(item), item is >= 1 and <= 84 ? PaddedAmount(Points(item), 3) : null,
                        null, Users(item), Lit: i == 0));
                break;
            default: // _trade / _setuse / _setident: the member's nine carried slots
                foreach (var (c, i) in choices.Take(P4Member.CarriedSlots).Select((c, i) => (c, i)))
                    rows.Add(c.Item == 0 ? new(0, null, "", null, null, [])
                        : new(c.Item, (char)('A' + i), s.Items.Name(c.Item),
                            c.Item is >= 1 and <= 84 ? PaddedAmount(Points(c.Item), 3) : null, null, Users(c.Item)));
                break;
        }
        var member = marked is int mk && mk < members.Count ? members[mk].Name : null;
        return (key, key is "_setuse" or "_setident" ? "_trade" : key, rows, member, message);
    }

    /// <summary>
    /// The text an item window draws (pack pixels in its window): headers, then per row the letter (colour
    /// <see cref="ItemWindows.LetterColour"/>), the name, the points, the price and the users' digits at their x (y + 8), the
    /// MORE label, the member's name and the message line. Names that would run into the next column are cut short of it
    /// only in width terms by the caller's font: the column order is the original's.
    /// </summary>
    public static List<(int X, int Y, string Text, int Colour)> ItemWindowText(ItemWindows windows, ItemWindowLayout layout,
        IReadOnlyList<ItemWindowRow> rows, string? member, string? message, Func<TrainingHallSegment, string> text,
        Func<string, int>? width = null)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(text);
        var ink = windows.TextColour;
        var list = layout.Headers.Select(h => (h.X ?? 0, h.Y ?? 0, text(h), h.Colour ?? ink)).ToList();
        var r = layout.Rows;
        for (var i = 0; i < rows.Count && i < r.Count; i++)
        {
            var row = rows[i];
            if (row.Item == 0)
                continue;
            var y = r.Top + r.Step * i + 8;
            var rowInk = row.Lit ? ink ^ layout.LitXor : ink;
            if (row.Letter is char c && r.LetterX is int lx)
                list.Add((lx, y, c.ToString(), row.Lit ? windows.LetterColour ^ layout.LitXor : windows.LetterColour));
            // A name that would run into the points column (long English names) takes the row's two 16-dot lines.
            var room = r.PointsX - r.NameX - 4;
            var cut = width is null || width(row.Name) <= room ? -1
                : Enumerable.Range(1, Math.Max(0, row.Name.Length - 2)).Where(j => row.Name[j] == ' ')
                    .OrderBy(j => Math.Max(width(row.Name[..j]), width(row.Name[(j + 1)..]))).DefaultIfEmpty(-1).First();
            if (cut > 0)
            {
                list.Add((r.NameX, y - 8, row.Name[..cut], rowInk));
                list.Add((r.NameX, y + 8, row.Name[(cut + 1)..], rowInk));
            }
            else
                list.Add((r.NameX, y, row.Name, rowInk));
            if (row.Points is { } p)
                list.Add((r.PointsX, y, p, rowInk));
            if (row.Price is { } price && r.PriceX is int px)
                list.Add((px, y, price, rowInk));
            if (r.UsersX is int ux)
                foreach (var m in row.Users)
                    list.Add((ux + r.UsersStep * m, y, (m + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), rowInk));
        }
        if (layout.More is { } more)
            list.Add((more.X ?? 0, more.Y ?? 0, text(more), ink));
        if (member is not null && layout.Member is { } at)
            list.Add((at.X, at.Y, member, ink));
        if (message is not null && layout.Message is { } mat)
            list.Add((mat.X, mat.Y, message, ink));
        return list;
    }

    private bool DrawItemWindow(SpriteBatch batch, Texture2D pixel)
    {
        if (CurrentItemWindow() is not { } now || Assets?.PackPixelsPerLayoutPixel is not > 0)
            return false;
        var windows = Extras!.ItemWindows!;
        if (windows.Windows.GetValueOrDefault(now.Layout) is not { } layout || Extras.Titled![now.Key] is not { } titled)
            return false;
        return DrawTitledBeside(batch, pixel, titled.Window.Width, titled.Window.Height, PanelText(titled.Title, titled.TitleText),
            (origin, k) => DrawItemWindowAt(batch, pixel, windows, layout, titled, now.Rows, now.Member, now.Message, origin, k));
    }

    private void DrawItemWindowAt(SpriteBatch batch, Texture2D pixel, ItemWindows windows, ItemWindowLayout layout,
        TitledWindow titled, List<ItemWindowRow> rows, string? member, string? message, Vector2 origin, float k)
    {
        void Box(float x, float y, float w, float h, Color c) =>
            batch.Draw(pixel, origin + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k),
                SpriteEffects.None, 0f);
        Box(0, 0, titled.Window.Width, titled.Window.Height, Pal(windows.BackColour, Color.LightGray));
        foreach (var f in layout.Frames)
            DrawSunken(batch, pixel, origin, f, k);
        Box(layout.Line.X, layout.Line.Y, layout.Line.Width, layout.Line.Height, Pal(layout.LineColour, Color.Black));
        var r = layout.Rows;
        for (var i = 0; i < rows.Count && i < r.Count; i++)
        {
            var row = rows[i];
            var y = r.Top + r.Step * i;
            if (row.Lit) // _wxbox(win, 15, 16, y, 280, 32): the row's grey turns colour 14 ^ 15
                Box(16, y, titled.Window.Width - 32, r.Step, Pal(windows.BackColour ^ layout.LitXor, Color.DarkGray));
            var rect = new Rectangle((int)(origin.X + 16 * k), (int)(origin.Y + y * k), (int)((titled.Window.Width - 32) * k),
                (int)Math.Ceiling(r.Step * k));
            if (row.Item != 0 && ItemIconName(row.Item) is { } name && Assets!.Get(name) is { } icon)
                batch.Draw(row.Lit ? LitIcon(icon, layout.LitXor) : icon, origin + new Vector2(r.IconX * k, y * k), null,
                    Color.White, 0f, Vector2.Zero, k,
                    SpriteEffects.None, 0f);
            if (row.Letter is not null) // _setbtn over the row: a click presses the remaster's key for that choice
            {
                var (press, line) = ListPress(i);
                UiOverlays.Hits?.Add(new HitRegion(rect, new P4PanelIconTag("_itemrow", i, line)) { Click = [press] });
            }
        }
        foreach (var (x, y, t, colour) in ItemWindowText(windows, layout, rows, member, message, s => PanelText(s.Label, s.Text),
                     t => PackWidth(t, k)))
            PackText(batch, pixel, origin + new Vector2(x * k, y * k), t, Pal(colour, Color.Black), k);
    }
}
