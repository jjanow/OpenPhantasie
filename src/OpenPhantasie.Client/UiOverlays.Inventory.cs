using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>The paperdoll + stash screen (<see cref="InventoryScreen"/>, presentation option <see cref="GameSettings.InventoryScreen"/>).</summary>
public static partial class UiOverlays
{
    private static readonly Color RefuseText = new(220, 130, 120);
    private static readonly Color BoxFill = new(26, 34, 52);
    private static readonly Color CursorFill = new(48, 62, 92);
    private static readonly Color HeldFill = new(70, 64, 34);

    /// <summary>Screen geometry of the inventory screen (virtual pixels), shared by drawing and the tests.</summary>
    public static class InventoryLayout
    {
        public static readonly Rectangle Panel = new(2, 14, 316, 152);
        public const int TabTop = 16;
        public const int TabWidth = 52;
        public const int TabHeight = 11;
        public const int TabsLeft = 4;
        public const int LeftX = 6;
        public const int LeftWidth = 152;
        public const int PortraitTop = 30;
        public const int SlotsX = 42;
        public const int SlotTop = 39;
        public const int SlotStep = 10;
        public const int CarriedRowStep = 8;
        public const int RightX = 162;
        public const int RightWidth = 152;
        public const int StashTop = 30;
        public const int StashRowsTop = 40;
        public const int DetailTop = 99;
        public const int DetailStep = 8;
        public const int DetailLines = 8;

        public static Rectangle Tab(int i) => new(TabsLeft + i * TabWidth, TabTop, TabWidth - 2, TabHeight);

        /// <summary>
        /// The member tabs for <paramref name="labels"/> ("1SWAT" …): the fixed 52-pixel tabs while every label fits one,
        /// else each tab as wide as its label (s99: no name cut); when even those run past the panel, the row slides so
        /// tab <paramref name="current"/> shows whole (the tabs past either edge are left out, Tab / 1-6 still reach them).
        /// </summary>
        public static List<Rectangle> Tabs(IReadOnlyList<string> labels, int current)
        {
            if (labels.All(l => PixelFont.Measure(l) <= 8 * PixelFont.Advance))
                return [.. labels.Select((_, i) => Tab(i))];
            var widths = labels.Select(l => PixelFont.Measure(l) + 5).ToList();
            var tabs = new List<Rectangle>();
            var x = TabsLeft;
            foreach (var w in widths)
            {
                tabs.Add(new Rectangle(x, TabTop, w, TabHeight));
                x += w + 2;
            }
            var right = Panel.Right - 2;
            if (tabs[^1].Right <= right)
                return tabs;
            var shift = Math.Clamp(tabs[current].Right - right, 0, tabs[^1].Right - right);
            shift = Math.Min(shift, tabs[current].X - TabsLeft);
            return [.. tabs.Select(t => t with { X = t.X - shift })];
        }

        /// <summary>
        /// Where the paperdoll's lines go (s99, user: never lose content to fit). Worn boxes whose names need lines of their
        /// own (<see cref="SlotNameLines"/>) push the same way, last of all the carried header moving right. Under the portrait and the worn boxes
        /// come the gear line, the carried header and the nine carried rows, all at their full 8-pixel pitch. When those
        /// run past the panel the gear line moves to the right column (III: four worn boxes beside a 48-row portrait),
        /// then the name line too, so the worn boxes start at the portrait's top (IV's five boxes). Each line moved to the
        /// right column takes one of the sack's rows; the sack scrolls (a bar), so nothing in it is lost.
        /// </summary>
        public sealed record Arrangement(int Moved, int SlotTop, int GearY, int CarriedHeaderY, int StashTop, int StashRowsTop,
            int StashRowsShown, IReadOnlyList<int> SlotLines)
        {
            /// <summary>All nine carried rows end inside the panel.</summary>
            public bool CarriedFit => CarriedRow(Character.InventorySlots - 1).Bottom <= Panel.Bottom - 1;

            public bool GearRight => Moved >= 1;
            public bool NameRight => Moved >= 2;

            /// <summary>The "CARRIED n/9" header sits in the right column too (the rows start where it was).</summary>
            public bool HeaderRight => Moved >= 3;

            /// <summary>Top of the right column's line <paramref name="i"/> (0 = the name or gear line, above the sack).</summary>
            public int RightLineY(int i) => PortraitTop + i * PixelFont.LineHeight;

            /// <summary>Worn box <paramref name="i"/>: one line, or more when its item's name needs them (<see cref="SlotNameLines"/>).</summary>
            public Rectangle Slot(int i) => new(SlotsX, SlotTop + SlotLines.Take(i).Sum(l => l * SlotStep),
                LeftX + LeftWidth - SlotsX, SlotLines[i] * SlotStep - 1);

            public Rectangle CarriedRow(int i) =>
                new(LeftX, CarriedHeaderY + 9 + i * CarriedRowStep, LeftWidth, CarriedRowStep);

            public Rectangle StashRow(int i) => new(RightX, StashRowsTop + i * CarriedRowStep, RightWidth, CarriedRowStep);
        }

        /// <summary>The longest name line ("NAME CLASS LEVEL") that fits beside the portrait, over the worn boxes.</summary>
        public const int NameLineWidth = 18 * PixelFont.Advance;

        /// <summary>Characters of an item name beside a worn box's label; a longer name goes on its own lines under it.</summary>
        public const int SlotNameChars = 11;

        /// <summary>Characters a worn box holds on a line of its own.</summary>
        public const int SlotLineChars = 18;

        /// <summary>
        /// The lines of a worn box for <paramref name="name"/> (s99: never cut): line 0 goes beside the label; a name too
        /// long for that leaves line 0 empty and takes lines of its own under the label.
        /// </summary>
        public static IReadOnlyList<string> SlotNameLines(string name) =>
            PixelFont.Measure(name) <= SlotNameChars * PixelFont.Advance ? [name] : ["", .. Lines(name, SlotLineChars)];

        public static Arrangement Arrange(int slots, int portraitHeight = PartyStripLayout.PortraitSize, bool nameFits = true) =>
            Arrange([.. Enumerable.Repeat(1, slots)], portraitHeight, nameFits);

        public static Arrangement Arrange(IReadOnlyList<int> slotLines, int portraitHeight = PartyStripLayout.PortraitSize,
            bool nameFits = true)
        {
            var slotRows = slotLines.Sum();
            for (var moved = nameFits ? 0 : 2; ; moved++)
            {
                var slotTop = moved >= 2 ? PortraitTop : SlotTop;
                var gearY = Math.Max(PortraitTop + portraitHeight + 4, slotTop + slotRows * SlotStep + 1);
                var header = moved >= 3 ? gearY - 9 : moved >= 1 ? gearY : gearY + 9;
                var lastRow = header + 9 + Character.InventorySlots * CarriedRowStep;
                if (lastRow <= Panel.Bottom - 1 || moved == 3)
                {
                    var stashTop = StashTop + moved * PixelFont.LineHeight;
                    var rowsTop = stashTop + (StashRowsTop - StashTop);
                    var shown = Math.Min(InventoryScreen.StashRows, (DetailTop - 3 - rowsTop) / CarriedRowStep);
                    return new Arrangement(moved, slotTop, gearY, header, stashTop, rowsTop, shown, slotLines);
                }
            }
        }

        /// <summary>
        /// The most worn-box lines that leave all nine carried rows inside the panel (at least one): the worn column shows
        /// that many of its <paramref name="total"/> lines and scrolls through the rest (s102, user: never cut a name).
        /// </summary>
        public static int SlotLinesThatFit(int total, int portraitHeight = PartyStripLayout.PortraitSize, bool nameFits = true)
        {
            for (var lines = total; lines > 1; lines--)
                if (Arrange([lines], portraitHeight, nameFits).CarriedFit)
                    return lines;
            return 1;
        }

        public static Rectangle Slot(int i) => Arrange(0).Slot(i);

        /// <summary>The gear line's top when it stays under the worn boxes.</summary>
        public static int GearY(int slots, int portraitHeight = PartyStripLayout.PortraitSize) =>
            Arrange(slots, portraitHeight).GearY;

        public static int CarriedHeaderY(int slots, int portraitHeight = PartyStripLayout.PortraitSize) =>
            Arrange(slots, portraitHeight).CarriedHeaderY;

        public static Rectangle CarriedRow(int slots, int i, int portraitHeight = PartyStripLayout.PortraitSize) =>
            Arrange(slots, portraitHeight).CarriedRow(i);

        public static Rectangle StashRow(int i) => Arrange(0).StashRow(i);
    }

    /// <summary>
    /// The gear line, as wide as a column at most (never cut): "DMG 13 ARM 8 SHL 13 DEF 93" runs 4 pixels past it, so a
    /// line too wide drops the space after each label.
    /// </summary>
    public static string GearLine(int damage, int armor, int shield, int defense)
    {
        var line = UiText.F("inventory.gear_x_x_x_x", damage, armor, shield, defense);
        return PixelFont.Measure(line) <= InventoryLayout.RightWidth - 1 ? line
            : UiText.F("inventory.gear_compact_x_x_x_x", damage, armor, shield, defense);
    }

    public static void DrawInventoryScreen(
        SpriteBatch batch,
        Texture2D pixel,
        AssetCatalog assets,
        InventoryScreen screen,
        Point? dragAt)
    {
        Fill(batch, pixel, InventoryLayout.Panel, new Color(10, 14, 24)); // opaque: nothing of the town shows through
        DrawPanel(batch, pixel, InventoryLayout.Panel, PanelBg, PanelEdge);
        if (Hits is { } hits)
        {
            hits.Add(ModalArea, new BackdropTag());
            hits.Wheel = (Keys.PageUp, Keys.PageDown);
        }
        var members = screen.Members;
        var held = screen.Held;
        var layout = screen.Layout;

        // Member tabs.
        var tabs = InventoryLayout.Tabs([.. members.Take(6).Select((ch, i) => $"{i + 1}{ch.Name}")], screen.Member);
        // A row of tabs slid to show the current one gets a bar under it: where the shown part sits in the whole row.
        var tabRow = tabs.Count == 0 ? 0 : tabs[^1].Right - tabs[0].X;
        var tabRoom = InventoryLayout.Panel.Right - 2 - InventoryLayout.TabsLeft;
        if (tabRow > tabRoom)
        {
            var track = new Rectangle(InventoryLayout.TabsLeft, InventoryLayout.TabTop + InventoryLayout.TabHeight + 1, tabRoom, 2);
            Fill(batch, pixel, track, ScrollTrack);
            Fill(batch, pixel, new Rectangle(track.X + (InventoryLayout.TabsLeft - tabs[0].X) * tabRoom / tabRow, track.Y,
                tabRoom * tabRoom / tabRow, 2), ScrollThumb);
        }
        for (var i = 0; i < members.Count && i < 6; i++)
        {
            var r = tabs[i];
            if (r.X < InventoryLayout.TabsLeft || r.Right > InventoryLayout.Panel.Right - 2)
                continue; // slid out of view
            var current = i == screen.Member;
            Fill(batch, pixel, r, current ? CursorFill : BoxFill);
            if (current)
                HLine(batch, pixel, r.X, r.Bottom - 1, r.Width, TitleGold);
            var color = members[i].Status == CharacterStatus.Dead ? RefuseText : current ? TitleGold : DimText;
            PixelFont.Draw(batch, pixel, r.X + 2, r.Y + 2, $"{i + 1}{members[i].Name}", color);
            Hits?.Add(r, new MemberTabSpot(i));
        }
        if (members.Count == 0)
            return;

        var m = screen.Current;
        var cursor = screen.CursorSpot;

        // Paperdoll: the portrait and the worn-item boxes beside it.
        var portrait = assets.GetPortraitIdle((int)m.Race);
        var portraitHeight = assets.PortraitHeight; // III Amiga's 32×48 figures whole (s99)
        var slots = layout.Slots.Count;
        var nameLine = $"{m.Name} {UiText.Name(m.Class).ToUpperInvariant()} {m.Level}";
        // A worn item's name longer than its box gets lines of its own under the label (never cut). When the boxes then
        // leave no room for the nine carried rows, the worn column shows the lines that fit and scrolls (s102): a bar,
        // the wheel over it, and the cursor's box always brought into view.
        var slotNames = layout.Slots.Select(slot => WornItems.Get(m, slot.Id) is var worn and > 0
            ? InventoryLayout.SlotNameLines(screen.ItemName(worn)) : ["-"]).ToList();
        var nameFits = PixelFont.Measure(nameLine) <= InventoryLayout.NameLineWidth;
        var lineCounts = slotNames.Select(n => n.Count).ToList();
        var totalLines = lineCounts.Sum();
        var arranged = InventoryLayout.Arrange(lineCounts, portraitHeight, nameFits);
        var shownLines = totalLines;
        if (!arranged.CarriedFit)
        {
            shownLines = InventoryLayout.SlotLinesThatFit(totalLines, portraitHeight, nameFits);
            arranged = InventoryLayout.Arrange([shownLines], portraitHeight, nameFits) with { SlotLines = lineCounts };
        }
        var wornBox = screen.WornBox;
        if (wornBox.TopFor(screen.Member))
            wornBox.ForgetFollow(); // another member: their cursor box is brought into view even on the same lines
        wornBox.Set(totalLines, shownLines);
        if (cursor is SlotSpot cs && cs.Member == screen.Member
            && layout.Slots.ToList().FindIndex(x => x.Id == cs.SlotId) is var ci and >= 0)
        {
            var first = lineCounts.Take(ci).Sum();
            wornBox.FollowRange(first, first + lineCounts[ci] - 1);
        }
        var wornView = new Rectangle(InventoryLayout.SlotsX, arranged.SlotTop,
            InventoryLayout.LeftX + InventoryLayout.LeftWidth - InventoryLayout.SlotsX, shownLines * InventoryLayout.SlotStep - 1);
        var wornScrolls = wornBox.CanScroll;
        HitScrollArea(wornBox, wornView);
        var pr = new Rectangle(InventoryLayout.LeftX, InventoryLayout.PortraitTop, PartyStripLayout.PortraitSize, portraitHeight);
        if (portrait is not null)
            DrawPortrait(batch, portrait, pr);
        else
            Fill(batch, pixel, pr, new Color(40, 50, 70));
        var rightLine = 0;
        if (arranged.NameRight)
            PixelFont.Draw(batch, pixel, InventoryLayout.RightX + 1, arranged.RightLineY(rightLine++), nameLine, Color.White);
        else
            PixelFont.Draw(batch, pixel, InventoryLayout.SlotsX, InventoryLayout.PortraitTop, nameLine, Color.White);
        for (var i = 0; i < layout.Slots.Count; i++)
        {
            var slot = layout.Slots[i];
            var spot = new SlotSpot(screen.Member, slot.Id);
            var full = arranged.Slot(i);
            var firstLine = lineCounts.Take(i).Sum();
            if (wornScrolls)
                full = new Rectangle(full.X, full.Y - wornBox.Offset * InventoryLayout.SlotStep,
                    full.Width - ScrollBox.BarWidth - 1, full.Height);
            var r = wornScrolls ? Rectangle.Intersect(full, wornView) : full;
            if (r.Height <= 0)
                continue; // scrolled out of the worn column
            bool Shown(int line) => !wornScrolls || (firstLine + line >= wornBox.Offset && firstLine + line < wornBox.Offset + shownLines);
            var worn = WornItems.Get(m, slot.Id);
            Fill(batch, pixel, r, held == spot ? HeldFill : cursor == spot ? CursorFill : BoxFill);
            DrawBorder(batch, pixel, r, cursor == spot ? TitleGold : PanelEdge);
            var names = slotNames[i];
            if (Shown(0))
            {
                PixelFont.Draw(batch, pixel, full.X + 2, full.Y + 1, Fit(InventoryScreen.SlotLabel(slot.Id), 6), DimText);
                PixelFont.Draw(batch, pixel, full.X + 2 + 7 * PixelFont.Advance, full.Y + 1, names[0], worn == 0 ? DimText : Accent);
            }
            for (var k = 1; k < names.Count; k++)
                if (Shown(k))
                    PixelFont.Draw(batch, pixel, full.X + 2, full.Y + 1 + k * InventoryLayout.SlotStep, names[k], Accent);
            Hits?.Add(r, spot);
        }
        DrawScrollBar(batch, pixel, wornBox, wornView);

        var gear = screen.Gear(m);
        PixelFont.Draw(batch, pixel, arranged.GearRight ? InventoryLayout.RightX + 1 : InventoryLayout.LeftX,
            arranged.GearRight ? arranged.RightLineY(rightLine++) : arranged.GearY,
            GearLine(gear.WeaponDamage, gear.ArmorSoak, gear.ShieldSoak, gear.DefenseRating), DimText);

        // Carried: the 9 slots with their holes, so a row is its raw slot.
        PixelFont.Draw(batch, pixel, arranged.HeaderRight ? InventoryLayout.RightX + 1 : InventoryLayout.LeftX,
            arranged.HeaderRight ? arranged.RightLineY(rightLine++) : arranged.CarriedHeaderY,
            UiText.F("inventory.carried_x_x", CarriedItems.Count(m), Character.InventorySlots), TitleGold);
        var listArea = Rectangle.Union(arranged.CarriedRow(0), arranged.CarriedRow(Character.InventorySlots - 1));
        Hits?.Add(listArea, new CarriedSpot(screen.Member, -1));
        for (var i = 0; i < Character.InventorySlots; i++)
        {
            var spot = new CarriedSpot(screen.Member, i);
            var r = arranged.CarriedRow(i);
            var id = screen.ItemAt(spot);
            if (held == spot)
                Fill(batch, pixel, r, HeldFill);
            else if (cursor == spot)
                Fill(batch, pixel, r, CursorFill);
            var worn = id > 0 && InventoryActions.IsWorn(m, i, layout);
            var color = id == 0 ? DimText
                : screen.UnusableReason(m, id) is not null ? DimText
                : worn ? Accent : BodyText;
            var text = id == 0 ? "-" : Fit(screen.ItemName(id), 23);
            PixelFont.Draw(batch, pixel, r.X + 2, r.Y, $"{(cursor == spot ? ">" : " ")}{(worn ? "*" : " ")}{text}", color);
            Hits?.Add(r, spot);
        }

        // The stash (the found-item sack): header with the filter and sort words, then the rows; a bar when the sack has
        // more rows than the column shows.
        var view = screen.StashView();
        var stashTop = arranged.StashTop;
        var header = screen.StashCapacity is { } cap
            ? UiText.F("inventory.sack_x_x", screen.StashCount, cap)
            : UiText.F("inventory.sack_x", screen.StashCount);
        PixelFont.Draw(batch, pixel, InventoryLayout.RightX, stashTop, header, TitleGold);
        var fx = InventoryLayout.RightX + PixelFont.Measure(header + " ");
        var filter = screen.FilterLabel;
        PixelFont.Draw(batch, pixel, fx, stashTop, filter, Accent);
        Hits?.Add(TextSpan(fx, stashTop, filter, 0, filter.Length), new StashFilterSpot());
        var sx = fx + PixelFont.Measure(filter + " ");
        var sort = screen.SortLabel;
        PixelFont.Draw(batch, pixel, sx, stashTop, sort, Accent);
        Hits?.Add(TextSpan(sx, stashTop, sort, 0, sort.Length), new StashSortSpot());

        var shown = arranged.StashRowsShown;
        screen.StashRowsShown = shown;
        screen.StashBox.Set(view.Count, shown);
        var stashArea = Rectangle.Union(arranged.StashRow(0), arranged.StashRow(shown - 1));
        Fill(batch, pixel, stashArea, BoxFill);
        Hits?.Add(stashArea, new StashSpot(-1));
        if (view.Count == 0)
            PixelFont.Draw(batch, pixel, stashArea.X + 2, stashArea.Y, UiText.S("inventory.sack_empty"), DimText);
        for (var i = 0; i < shown && screen.StashScroll + i < view.Count; i++)
        {
            var (slot, id) = view[screen.StashScroll + i];
            var spot = new StashSpot(slot);
            var r = arranged.StashRow(i);
            if (held == spot)
                Fill(batch, pixel, r, HeldFill);
            else if (cursor == spot)
                Fill(batch, pixel, r, CursorFill);
            var color = screen.UnusableReason(m, id) is not null ? DimText : BodyText;
            PixelFont.Draw(batch, pixel, r.X + 2, r.Y, $"{(cursor == spot ? ">" : " ")}{Fit(screen.ItemName(id), 23)}", color);
            Hits?.Add(r, spot);
        }
        DrawScrollBar(batch, pixel, screen.StashBox, stashArea);

        // The item under the cursor: the reason it can't be worn, what wearing it changes, its description; a longer
        // text scrolls ([ / ], the wheel over it, its bar) and starts at its top for each new item.
        HLine(batch, pixel, InventoryLayout.RightX, InventoryLayout.DetailTop - 3, InventoryLayout.RightWidth, PanelEdge);
        var lines = new List<(string Text, Color Color)>();
        if (screen.ReadOnlyReason is { } readOnly)
            foreach (var part in Wrap(readOnly, 25))
                lines.Add((part, RefuseText));
        var sellLine = DetailLines(screen, m, cursor, lines);
        var detail = screen.DetailBox;
        detail.Set(lines.Count, InventoryLayout.DetailLines);
        detail.TopFor(HashCode.Combine(cursor, screen.ItemAt(cursor)));
        var detailArea = new Rectangle(InventoryLayout.RightX, InventoryLayout.DetailTop - 1, InventoryLayout.RightWidth + 1,
            InventoryLayout.DetailLines * InventoryLayout.DetailStep);
        HitScrollArea(detail, detailArea);
        for (var i = 0; i < InventoryLayout.DetailLines && detail.Offset + i < lines.Count; i++)
        {
            var (text, color) = lines[detail.Offset + i];
            PixelFont.Draw(batch, pixel, InventoryLayout.RightX + 1, InventoryLayout.DetailTop + i * InventoryLayout.DetailStep,
                text, color);
            if (detail.Offset + i == sellLine) // QoL modern-inventory: click (or drop an item) to sell
                Hits?.Add(new Rectangle(InventoryLayout.RightX, InventoryLayout.DetailTop + i * InventoryLayout.DetailStep - 1,
                    InventoryLayout.RightWidth, InventoryLayout.DetailStep), new SellSpot());
        }
        DrawScrollBar(batch, pixel, detail, detailArea);

        // A dragged item follows the pointer.
        if (dragAt is { } at && held is not null && screen.ItemAt(held) is var heldId and > 0)
        {
            var name = screen.ItemName(heldId);
            var box = new Rectangle(Math.Min(at.X + 4, 318 - PixelFont.Measure(name) - 4), at.Y + 4, PixelFont.Measure(name) + 4, 10);
            Fill(batch, pixel, box, HeldFill);
            DrawBorder(batch, pixel, box, TitleGold);
            PixelFont.Draw(batch, pixel, box.X + 2, box.Y + 2, name, Color.White);
        }
    }

    /// <summary>
    /// The detail box's lines for the item under the cursor, wrapped to the column (25 characters). Returns the index of the
    /// "SELL FOR N GP" line (QoL <c>modern-inventory</c>, in town), or −1.
    /// </summary>
    private static int DetailLines(InventoryScreen screen, Character m, InventorySpot cursor, List<(string, Color)> lines)
    {
        const int width = 25;
        var id = screen.ItemAt(cursor);
        if (id == 0)
            return -1;
        var owner = screen.Members[screen.OwnerOf(cursor)];
        foreach (var part in Lines(screen.ItemName(id), width))
            lines.Add((part, TitleGold));
        var sellLine = -1;
        if (screen.SellPriceAt(cursor) is { } price)
        {
            sellLine = lines.Count;
            // at most "SELL FOR 65000 GP (LT)": fits 25. s183: the pad's LT, the pad in use
            lines.Add((screen.UsingPad ? UiText.F("inventory.sell_line_pad", price) : UiText.F("inventory.sell_line", price), Accent));
        }
        if (screen.UnusableReason(owner, id) is { } why)
            foreach (var part in Lines(why, width))
                lines.Add((part, RefuseText));
        var worn = cursor switch
        {
            SlotSpot => true,
            CarriedSpot c => InventoryActions.IsWorn(owner, c.Slot, screen.Layout),
            _ => false,
        };
        if (worn)
            lines.Add((UiText.S("inventory.worn"), Accent));
        else if (screen.Compare(owner, id) is { } cmp)
        {
            lines.Add((UiText.F("inventory.cmp_dmg_def", cmp.Before.WeaponDamage, cmp.After.WeaponDamage,
                cmp.Before.DefenseRating, cmp.After.DefenseRating), BodyText));
            lines.Add((UiText.F("inventory.cmp_arm_shl", cmp.Before.ArmorSoak, cmp.After.ArmorSoak,
                cmp.Before.ShieldSoak, cmp.After.ShieldSoak), BodyText));
            if (cmp.Before.Bow is { } bowBefore && cmp.After.Bow is { } bowAfter) // III: the bow slot's points
                lines.Add((UiText.F("inventory.cmp_bow", bowBefore, bowAfter), BodyText));
        }
        foreach (var text in screen.Describe(id).Skip(1))
            foreach (var part in Wrap(text, width))
                lines.Add((part, DimText));
        return sellLine;
    }

    /// <summary>
    /// Word-wrap <paramref name="text"/> to <paramref name="width"/> layout columns, measured with the font's widths (a
    /// word longer than the line goes on over as many lines as it needs, s99: nothing is cut; empty text gives no lines).
    /// </summary>
    public static IEnumerable<string> Wrap(string text, int width) => Wrap(text, width, PixelFont.Font);

    /// <summary>
    /// <paramref name="text"/> as it is when it fits <paramref name="width"/> columns (its spacing kept: padded columns,
    /// a leading cursor mark), else <see cref="Wrap(string, int)"/>.
    /// </summary>
    public static IReadOnlyList<string> Lines(string text, int width) =>
        PixelFont.Measure(text ?? "") <= width * PixelFont.Advance ? [text ?? ""] : [.. Wrap(text ?? "", width)];

    public static IEnumerable<string> Wrap(string text, int width, IFontSource font)
    {
        var max = width * PixelFont.Advance;
        var line = "";
        foreach (var whole in (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = whole;
            while (PixelFont.Measure(w, font) > max)
            {
                if (line.Length > 0)
                {
                    yield return line;
                    line = "";
                }
                var cut = Math.Max(1, PixelFont.CutLength(w, max, font));
                // s156: a word too wide for the line ("NUMPAD/ARROWS" in a narrow column) breaks after its last '/' or
                // '-' that fits rather than inside a word part; with none, between characters as before.
                var mark = w.LastIndexOfAny(['/', '-'], Math.Min(cut, w.Length) - 1);
                if (mark >= 0 && mark + 1 < w.Length)
                    cut = mark + 1;
                yield return w[..cut];
                w = w[cut..];
            }
            if (w.Length == 0)
                continue;
            if (line.Length == 0)
                line = w;
            else if (PixelFont.Measure(line + " " + w, font) <= max)
                line += " " + w;
            else
            {
                yield return line;
                line = w;
            }
        }
        if (line.Length > 0)
            yield return line;
    }

    private static void DrawBorder(SpriteBatch batch, Texture2D pixel, Rectangle r, Color edge)
    {
        HLine(batch, pixel, r.X, r.Y, r.Width, edge);
        HLine(batch, pixel, r.X, r.Bottom - 1, r.Width, edge);
        VLine(batch, pixel, r.X, r.Y, r.Height, edge);
        VLine(batch, pixel, r.Right - 1, r.Y, r.Height, edge);
    }
}
