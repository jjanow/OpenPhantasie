using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Phantasie III Amiga's combat orders screen (s118, FORMAT.md §8.3), full-screen 1:1: drawn by <c>0x18c48</c>, answered by
/// <c>0x188e4</c>. A box per member with its name and order, the order list and the Fight! button, the monster groups on
/// the bottom two rows and the party roster with the body figures on the right. Text cells are 8 × 8 (column · 8, row · 8);
/// colours are map-palette indices (the screen ends in the palette <c>0x2437e</c>; matched on the s116 capture `[~]`).
/// </summary>
public static partial class UiOverlays
{
    /// <summary>The palette the orders screen's colour indices name.</summary>
    public const string OrdersPalette = "map";

    /// <summary>Map-palette indices: member boxes and roster text 4, roster names 5, orders 8, monster lines and H / M 9,
    /// the Fight! box 10, names and the order list 15.</summary>
    public const int OrdersBoxColour = 4, OrdersRosterNameColour = 5, OrdersLabelColour = 8, OrdersMonsterColour = 9,
        OrdersFightColour = 10, OrdersTextColour = 15;

    /// <summary>Member <paramref name="i"/>'s box (<c>0xac48</c>: (6, 6 + 16 i)–(194, 17 + 16 i), inclusive).</summary>
    public static Rectangle OrdersMemberBox(int i) => new(6, 6 + 16 * i, 189, 12);

    /// <summary>Member <paramref name="i"/>'s text row (name at column 1, its order at column 9).</summary>
    public static int OrdersMemberRow(int i) => 2 * i + 1;

    /// <summary>The Fight! button's box (<c>0x1902a</c>: (6, 166)–(73, 177), colour 10).</summary>
    public static readonly Rectangle OrdersFightBox = new(6, 166, 68, 12);

    /// <summary>
    /// The order list's entries (hit table <c>0x21fdc</c>: column, row, width in cells, key): two columns from row 15, the
    /// Fight! line at (1, 21). The text is drawn from <c>0x231d4</c>'s block at column 1, row 15 (" Fight!" one cell in).
    /// </summary>
    public static readonly IReadOnlyList<(int Col, int Row, int Width, Keys Key, string Id)> OrdersList =
    [
        (1, 15, 11, Keys.A, "A"), (1, 16, 11, Keys.L, "L"), (1, 17, 11, Keys.S, "S"), (1, 18, 11, Keys.T, "T"),
        (1, 19, 11, Keys.P, "P"), (12, 15, 11, Keys.B, "B"), (12, 16, 11, Keys.M, "M"), (12, 17, 11, Keys.C, "C"),
        (12, 18, 11, Keys.D1, "1"), (12, 19, 11, Keys.D2, "2"), (12, 20, 11, Keys.D3, "3"), (1, 21, 8, Keys.F, "F"),
    ];

    /// <summary>A monster group's text row (<c>0x1a384</c>: rank 2's group on row 23, rank 1's on row 24).</summary>
    public static int OrdersGroupRow(int rankIndex) => rankIndex == 0 ? 24 : 23;

    /// <summary>
    /// Roster slot <paramref name="slot"/>'s text cell (<c>0x9198</c> / <c>0x93e0</c> with <c>0x240d8</c> = 1): column 25,
    /// rows 1 / 9 / 17 for slots 0–2, then column 33.
    /// </summary>
    public static (int Col, int Row) OrdersRosterCell(int slot) => (slot < 3 ? 25 : 33, 1 + 8 * (slot % 3));

    /// <summary>Roster slot <paramref name="slot"/>'s body figure origin (table <c>0x220c2</c>, y − 8 here: <c>0x9736</c>).</summary>
    public static Point OrdersFigureAt(int slot) => new(slot < 3 ? 216 : 280, 40 + 64 * (slot % 3) - 8);

    /// <summary>
    /// The label key (<c>p3combat.order.N</c>) of a packed order (<c>0x22a38</c>: the action's index, + 11 for rank 2).
    /// Null for a cast (the screen shows the spell's name, <c>0x22d4c</c>), <paramref name="spellIndex"/> then its index.
    /// Anything else (no order yet) shows Parries. Aim has no rank-2 label on the Amiga; it shows its rank-1 one `[~]`.
    /// </summary>
    public static string? OrderLabelId(int packed, out int spellIndex)
    {
        if (CombatAction.TryDecodeCast(packed, out spellIndex, out _))
            return null;
        spellIndex = -1;
        if (!P3CombatModule.TryUnpack(packed, out var choice))
            return "p3combat.order.5";
        var index = (int)choice.Action;
        if (choice.Rank == 2 && choice.Action is not (P3Action.Parry or P3Action.Aim or P3Action.CastSpell))
            index += 11;
        return $"p3combat.order.{index}";
    }

    /// <summary>Member <paramref name="i"/>'s line in its box: "is dead" / "is asleep" / "is out", else its order.</summary>
    public static string OrdersMemberLine(Character member, int packed, GameDataRepository data) =>
        P3Fight.StatusOf(member) switch
        {
            P3Status.Dead => UiText.S("p3combat.is_dead"),
            P3Status.Asleep => UiText.S("p3combat.is_asleep"),
            P3Status.Out => UiText.S("p3combat.is_out"),
            _ => OrderLabelId(packed, out var spell) is { } id ? UiText.S(id)
                : spell >= 0 && spell < data.Spells.Count ? data.GetSpell(spell).Name : UiText.S("p3combat.order.7"),
        };

    /// <summary>Every string id the orders screen builds at run time (the string table test).</summary>
    public static IEnumerable<string> OrdersStringKeys() =>
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15 }.Select(i => $"p3combat.order.{i}")
            .Concat(OrdersList.Select(e => $"p3combat.list.{e.Id}"))
            .Concat(Enum.GetValues<P3Status>().Select(st => $"p3combat.status.{(int)st}"))
            .Concat(Enum.GetValues<P3Status>().Select(st => $"p3combat.window_status.{(int)st}"))
            .Concat(Enum.GetValues<P3PartyRank>().Select(r => $"p3combat.row.{(int)r}"))
            .Concat(Enum.GetValues<CharacterClass>().Select(c => $"p3combat.class.{c}"))
            .Concat(CombatPresenter.AmigaFieldMenus.SelectMany(m => m.Items.Select(i => i.LabelId).Prepend(m.TitleId)))
            .Concat(AmigaOutsideBar.Menus.SelectMany(m => m.Items.Select(i => i.LabelId).Prepend(m.TitleId))) // s136
            .Concat(["p3field.monster_identification", "p3field.rank", "p3field.health_labels"])
            .Concat(Enum.GetValues<P3AreaState>().Select(st => $"p3field.area_state.{(int)st}"));

    private static void DrawCell(SpriteBatch batch, Texture2D pixel, int col, int row, string text, Color colour) =>
        PixelFont.Draw(batch, pixel, col * 8, row * 8, text, colour);

    /// <summary>Text down one column, one character a row (<c>0xab8c</c>).</summary>
    private static void DrawDown(SpriteBatch batch, Texture2D pixel, int col, int row, string text, Color colour)
    {
        for (var i = 0; i < text.Length; i++)
            if (text[i] != ' ')
                DrawCell(batch, pixel, col, row + i, text[i].ToString(), colour);
    }

    private static void Outline(SpriteBatch batch, Texture2D pixel, Rectangle r, Color c)
    {
        Fill(batch, pixel, new Rectangle(r.X, r.Y, r.Width, 1), c);
        Fill(batch, pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        Fill(batch, pixel, new Rectangle(r.X, r.Y, 1, r.Height), c);
        Fill(batch, pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }

    /// <summary>Draws the orders screen now; false (nothing drawn) when it is not up.</summary>
    public static bool DrawCombatOrders(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, GameSession session,
        GameDataRepository data, CombatPresenter presenter, Point? held)
    {
        if (!presenter.AmigaOrdersShown || session.Combat is not { } combat)
            return false;
        Color C(int index) => assets.PaletteColour(OrdersPalette, index) ?? DosWhite;
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), DosBlack);
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));

        // The member boxes (0x18c82 loop): name in colour 15 at column 1, the order (or "is asleep" …) in 8 at column 9.
        var members = session.Party.Members;
        var picked = presenter.OrderMember;
        for (var i = 0; i < members.Count && i < 7; i++)
        {
            var row = OrdersMemberRow(i);
            Outline(batch, pixel, OrdersMemberBox(i), C(OrdersBoxColour));
            if (i == picked)
            {
                // 0x3d86(1, row, 7): the picked name's cells XOR-inverted (colour 15 ↔ 0), "Select option:" in its order's place.
                Fill(batch, pixel, new Rectangle(8, row * 8, 56, 8), C(OrdersTextColour));
                DrawCell(batch, pixel, 1, row, members[i].Name, C(0));
                if (!presenter.AsksRankWindow && !presenter.AsksAmigaSpell && !presenter.AsksAmigaHeal)
                    DrawCell(batch, pixel, 9, row, UiText.S("p3combat.select_option"), C(OrdersTextColour));
                else
                    DrawCell(batch, pixel, 9, row, OrdersMemberLine(members[i], presenter.PackedOrder(i), data), C(OrdersLabelColour));
            }
            else
            {
                DrawCell(batch, pixel, 1, row, members[i].Name, C(OrdersTextColour));
                DrawCell(batch, pixel, 9, row, OrdersMemberLine(members[i], presenter.PackedOrder(i), data), C(OrdersLabelColour));
            }
            if (picked < 0 && CombatPresenter.AmigaPickable(members[i]))
                Hits?.Add(new HitRegion(new Rectangle(8, row * 8, 23 * 8, 8)) { Click = [Keys.D1 + i] });
        }

        // The order list (0x18ff8, colour 15) and the Fight! box (colour 10).
        foreach (var (col, row, width, key, id) in OrdersList)
        {
            var text = UiText.S($"p3combat.list.{id}");
            DrawCell(batch, pixel, key == Keys.F ? col + 1 : col, row, text, C(OrdersTextColour));
            if (key == Keys.F || picked >= 0 && !presenter.AsksRankWindow && !presenter.AsksAmigaSpell && !presenter.AsksAmigaHeal)
                Hits?.Add(new HitRegion(new Rectangle(col * 8, row * 8, width * 8, 8)) { Click = [key] });
        }
        Outline(batch, pixel, OrdersFightBox, C(OrdersFightColour));

        // The monster groups (0x1a264(2)): count at column 0, name at 2, a status letter a monster from column 19.
        var fight = P3Fight.Of(combat);
        foreach (var rank in combat.Ranks.Where(r => !r.IsWiped && r.RankIndex is 0 or 1))
        {
            var row = OrdersGroupRow(rank.RankIndex);
            var living = rank.Living.ToList();
            if (living.Count == 0)
                continue;
            DrawCell(batch, pixel, 0, row, living.Count.ToString(), C(OrdersMonsterColour));
            DrawCell(batch, pixel, 2, row, UiText.F("p3combat.group_x_s", living[0].Name), C(OrdersMonsterColour));
            for (var k = 0; k < living.Count && fight is not null; k++)
                DrawCell(batch, pixel, 19 + k, row,
                    P3Encounters.StatusLetter(fight.StatusOf(living[k]), combat.Rules).ToString(), C(OrdersMonsterColour));
        }

        DrawOrdersRoster(batch, pixel, assets, session, C);

        if (presenter.AsksRankWindow && assets.KeepWindows is { Rank: { } rankWindow } layout
            && assets.Get(PictureNames.Button("ok")) is { } ok && assets.Get(PictureNames.Button("cancel")) is { } cancel)
        {
            var b = rankWindow.Box;
            DrawWindowFrame(batch, pixel, layout, b[0], b[1], b[2], b[3]);
            PixelFont.Draw(batch, pixel, rankWindow.Text[0].X, rankWindow.Text[0].Y, UiText.S("p3combat.rank_question"),
                VirtualScreen.ParseColor(layout.TextColour));
            DrawWindowButtons(batch, assets, layout, ok, cancel, rankWindow.Ok, rankWindow.Cancel, held);
        }
        else if (presenter.AsksAmigaSpell && assets.KeepWindows is { Spell: { } spellWindow } spellLayout)
            DrawSpellWindow(batch, pixel, assets, spellLayout, spellWindow, session, data, presenter, held);
        else if (presenter.AsksAmigaHeal && assets.KeepWindows is { Heal: { } healWindow } healLayout)
            DrawHealWindow(batch, pixel, assets, healLayout, healWindow, session, presenter, held);
        if (presenter.AmigaMessageShown && assets.KeepWindows is { Message: { } messageWindow } messageLayout)
            DrawMessageWindow(batch, pixel, assets, messageLayout, messageWindow, presenter.CastNotice, held);
        return true;
    }

    /// <summary>
    /// III Amiga's message box, window 7 (<c>0xaaac</c>, s134): the box and frame, the text as the window's title (a
    /// "\r" or "\n" starts the next row 8 px down at the same column; a row too wide for the box goes on at a word on the
    /// next row, never cut) and the one OK button (a click presses O). False when the pack lacks the OK art.
    /// </summary>
    public static bool DrawMessageWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, KeepWindowsLayout layout,
        KeepWindowsLayout.MessageWindow w, string text, Point? held)
    {
        if (assets.Get(PictureNames.Button("ok")) is not { } ok)
            return false;
        var (x0, y0, x1, y1) = (w.Box[0], w.Box[1], w.Box[2], w.Box[3]);
        var rows = text.Split('\r', '\n')
            .SelectMany(line => Lines(line, Math.Max(1, (x1 - 3 - w.Text.X) / PixelFont.Advance))).ToList();
        // Rows that would reach the button grow the box (and move the button) down, never cut (s99).
        var grow = Math.Max(0, w.Text.Y + 8 * rows.Count + 2 - w.Ok.Y);
        DrawWindowFrame(batch, pixel, layout, x0, y0, x1, y1 + grow);
        var ink = VirtualScreen.ParseColor(w.TextColour);
        for (var i = 0; i < rows.Count; i++)
            PixelFont.Draw(batch, pixel, w.Text.X, w.Text.Y + 8 * i, rows[i], ink);
        var area = new Rectangle(w.Ok.X, w.Ok.Y + grow, layout.ButtonWidth, layout.ButtonHeight);
        var picture = held is { } p && area.Contains(p) && assets.Get(PictureNames.Button("ok", chosen: true)) is { } down
            ? down : ok;
        batch.Draw(picture, new Rectangle(area.X, area.Y, picture.Width, picture.Height), Color.White);
        Hits?.Add(new HitRegion(area) { Click = [Keys.O] });
        return true;
    }

    /// <summary>
    /// III Amiga's spell window 11 (<c>0xf770</c>, s119): "Select &lt;name&gt;'s spell:" (colour 8), then a page of up to 12
    /// known spells (<c>0xff82</c>: highest first, name and MP cost, colour 4, the cursor's row in 8), OK / CANCEL, and MORE
    /// when the member knows more than 12. A press on a row moves the cursor (<see cref="AmigaRowTag"/>).
    /// </summary>
    private static void DrawSpellWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, KeepWindowsLayout layout,
        KeepWindowsLayout.Window w, GameSession session, GameDataRepository data, CombatPresenter presenter, Point? held)
    {
        if (w.Text.Count < 3)
            return;
        DrawWindowFrame(batch, pixel, layout, w.Box[0], w.Box[1], w.Box[2], w.Box[3]);
        var normal = VirtualScreen.ParseColor(layout.ListColours[0]);
        var bright = VirtualScreen.ParseColor(layout.ListColours[1]);
        var name = session.Party.Members[presenter.OrderMember].Name;
        PixelFont.Draw(batch, pixel, w.Text[0].X, w.Text[0].Y, UiText.F("p3combat.select_spell_x", name), bright);
        var page = presenter.AmigaSpellPage(session);
        for (var i = 0; i < page.Count; i++)
        {
            var colour = i == presenter.AmigaListCursor ? bright : normal;
            var y = w.Text[1].Y + 8 * i;
            PixelFont.Draw(batch, pixel, w.Text[1].X, y, data.GetSpell(page[i]).Name, colour);
            PixelFont.Draw(batch, pixel, w.Text[2].X, y,
                Magic.MagicPointCost(page[i], SpellSlots.For(session.Options)).ToString(), colour);
            // 0x1776(6, …, 8, 27): the press area is columns 8–27 of the listed rows
            Hits?.Add(new HitRegion(new Rectangle(w.Text[1].X, y, w.Text[2].X + 8 - w.Text[1].X, 8), new AmigaRowTag(i)));
        }
        DrawListWindowButtons(batch, assets, layout, w,
            presenter.AmigaSpellList(session).Count > CombatPresenter.AmigaSpellRows, held);
    }

    /// <summary>
    /// The heal target (spells 1–4) in window 6 (<c>0x127ce(33)</c>, s119): "Who shall be healed in combat?" (colour 8), one
    /// member a row (<c>0x12ac8</c>: name, race, level, class, status; colour 4, the cursor's row in 8), OK / CANCEL.
    /// </summary>
    private static void DrawHealWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, KeepWindowsLayout layout,
        KeepWindowsLayout.Window w, GameSession session, CombatPresenter presenter, Point? held)
    {
        if (w.Text.Count < 6)
            return;
        DrawWindowFrame(batch, pixel, layout, w.Box[0], w.Box[1], w.Box[2], w.Box[3]);
        var normal = VirtualScreen.ParseColor(layout.ListColours[0]);
        var bright = VirtualScreen.ParseColor(layout.ListColours[1]);
        PixelFont.Draw(batch, pixel, w.Text[0].X, w.Text[0].Y, UiText.S("p3combat.heal_question"), bright);
        var members = session.Party.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var m = members[i];
            var colour = i == presenter.AmigaListCursor ? bright : normal;
            var y = w.Text[1].Y + 8 * i;
            string[] cells = [m.Name, UiText.Name(m.Race), m.Level.ToString(), UiText.Name(m.Class),
                UiText.S($"p3combat.window_status.{(int)P3Fight.StatusOf(m)}")];
            for (var c = 0; c < cells.Length; c++)
                PixelFont.Draw(batch, pixel, w.Text[1 + c].X, y, cells[c], colour);
            // 0x1776(9, …, 2, 37): columns 2–37 of the member rows
            Hits?.Add(new HitRegion(new Rectangle(w.Text[1].X, y, 36 * 8, 8), new AmigaRowTag(i)));
        }
        DrawListWindowButtons(batch, assets, layout, w, more: false, held);
    }

    /// <summary>OK / CANCEL, and MORE (key M) when <paramref name="more"/> and the window has its place.</summary>
    private static void DrawListWindowButtons(SpriteBatch batch, AssetCatalog assets, KeepWindowsLayout layout,
        KeepWindowsLayout.Window w, bool more, Point? held)
    {
        if (assets.Get(PictureNames.Button("ok")) is not { } ok || assets.Get(PictureNames.Button("cancel")) is not { } cancel)
            return;
        DrawWindowButtons(batch, assets, layout, ok, cancel, w.Ok, w.Cancel, held);
        if (!more || w.More is not { } at || assets.Get(PictureNames.Button("more")) is not { } up)
            return;
        var area = new Rectangle(at.X, at.Y, layout.ButtonWidth, layout.ButtonHeight);
        var picture = held is { } p && area.Contains(p) && assets.Get(PictureNames.Button("more", chosen: true)) is { } down
            ? down : up;
        batch.Draw(picture, new Rectangle(at.X, at.Y, picture.Width, picture.Height), Color.White);
        Hits?.Add(new HitRegion(area) { Click = [Keys.M] });
    }

    /// <summary>
    /// The party roster in its combat place (<c>0x9198</c> then <c>0x93e0</c>, <c>0x240d8</c> = 1), every member but a
    /// summoned elemental: name (colour 5) at the cell, HP and MP "cur:max" on the next two rows (current right-aligned to
    /// column + 2, <c>0xabec</c>; the colon at + 3, the most at + 4), "H" / "M" at + 7 for slots 0–2 (colour 9), the class
    /// (FGTR …) down column + 0 and the status (OKAY …) down + 6 from row + 3, the body figure, "(Front)" at row + 7.
    /// </summary>
    private static void DrawOrdersRoster(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, GameSession session,
        Func<int, Color> colour)
    {
        var members = session.Party.Members.Where(m => !ReferenceEquals(m, session.Party.Elemental)).Take(6).ToList();
        for (var slot = 0; slot < members.Count; slot++)
        {
            var m = members[slot];
            var (c, r) = OrdersRosterCell(slot);
            var text = colour(OrdersBoxColour);
            DrawCell(batch, pixel, c, r, m.Name, colour(OrdersRosterNameColour));
            DrawRight(c + 2, r + 1, m.HpCur);
            DrawCell(batch, pixel, c + 3, r + 1, ":", text);
            DrawCell(batch, pixel, c + 4, r + 1, m.HpMax.ToString(), text);
            DrawRight(c + 2, r + 2, m.MagicCur);
            DrawCell(batch, pixel, c + 3, r + 2, ":", text);
            DrawCell(batch, pixel, c + 4, r + 2, m.MagicMax.ToString(), text);
            if (slot < 3)
            {
                DrawCell(batch, pixel, c + 7, r + 1, UiText.S("p3combat.hp_letter"), colour(OrdersMonsterColour));
                DrawCell(batch, pixel, c + 7, r + 2, UiText.S("p3combat.mp_letter"), colour(OrdersMonsterColour));
            }
            DrawDown(batch, pixel, c, r + 3, UiText.S($"p3combat.class.{m.Class}"), text);
            var status = P3Fight.StatusOf(m);
            DrawDown(batch, pixel, c + 6, r + 3, UiText.S($"p3combat.status.{(int)status}"), text);
            if (status != P3Status.Dead && assets.BodyFigure is { } figure)
                DrawBodyFigure(batch, pixel, assets, figure, m, OrdersFigureAt(slot));
            DrawCell(batch, pixel, c, r + 7,
                UiText.S($"p3combat.row.{(int)(m.Phantasie3?.Rank ?? P3PartyRank.Front)}"), text);

            void DrawRight(int col, int row, int value) // 0xabec: 10–99 one cell left, 100–999 two; 1000+ not moved
                => DrawCell(batch, pixel, col - (value >= 1000 ? 0 : value >= 100 ? 2 : value >= 10 ? 1 : 0), row,
                    value.ToString(), text);
        }
    }
}
