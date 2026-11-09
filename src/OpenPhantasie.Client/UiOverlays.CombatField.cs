using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Phantasie III Amiga's combat field (s117, FORMAT.md §8), full-screen 1:1 over the status, help and message lines: black,
/// rank 2's monsters in a row at y 28 and rank 1's at y 78 (a giant group at y 50), the party's portraits below, and the
/// round's line centred on text row 24.
/// </summary>
public static partial class UiOverlays
{
    /// <summary>The palette the field's line colours come from (heros.cmp's, loaded by the combat screen <c>0x19906</c>).</summary>
    public const string CombatPalette = "combat";

    /// <summary>A group's y (fight set-up <c>0x14e10</c> / <c>0x14e26</c>: rank 1 at 78, rank 2 at 28; <c>0x1b4e6</c>: a giant group at 50).</summary>
    public static int FieldGroupY(int rankIndex, bool giant) => giant ? 50 : rankIndex == 0 ? 78 : 28;

    /// <summary>
    /// The group's giant flag <c>+72</c> (<c>0x1a928</c>): record byte 27 &gt; 41, from the record, not the picture. Wolf (65)
    /// and Mud Elemental (48) have no picture on the Amiga (byte 27 ≥ 48, FORMAT.md §6; the remaster gives them borrowed
    /// normal-size ones, D3-067) but are giants all the same: they stand at the giant places and a flash on them gets the
    /// giant's +2 bytes (s125).
    /// </summary>
    public static bool FieldGiant(Monster monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return monster.GroupAloneByte > 41;
    }

    /// <summary>Width of a normal-size monster picture (<c>0x1b5f2</c>: 32×48; giants <c>0x1b654</c>: 80×72).</summary>
    public const int NormalMonsterPictureWidth = 32;

    /// <summary>
    /// Whether a flash on <paramref name="monster"/> gets the giant's +2 bytes. On the Amiga's combat field
    /// (<paramref name="amigaField"/>) the record's flag, as the Amiga (<see cref="FieldGiant"/>: Wolf / Mud Elemental
    /// stand at the giant places with their borrowed normal pictures). On the remaster's own rank list the flash sits on
    /// the rank's one picture, drawn 1:1, so the shift follows that picture: only a giant-size picture (wider than
    /// <see cref="NormalMonsterPictureWidth"/>) shifts; a borrowed normal picture or the no-picture box does not, else
    /// the sparkle lands beside it (s128).
    /// </summary>
    public static bool FlashGiant(Monster monster, bool amigaField, int? pictureWidth) =>
        FieldGiant(monster) && (amigaField || pictureWidth > NormalMonsterPictureWidth);

    /// <summary>
    /// Member <paramref name="m"/>'s x of a group of <paramref name="n"/> (<c>0x1b150</c>, in bytes × 8): normal pictures
    /// from 3 + 7·((5 − n) / 2) (+3 for an even n), 7 apart; giants from 6 (even n) or 15 (odd n), 18 apart.
    /// </summary>
    public static int FieldMonsterX(int m, int n, bool giant)
    {
        if (giant)
            return 8 * ((n % 2 == 1 ? 15 : 6) + 18 * m);
        var start = 7 * ((5 - n) / 2) + 3 + (n % 2 == 0 ? 3 : 0);
        return 8 * (start + 7 * m);
    }

    /// <summary>A portrait's y by the member's combat row word (<c>0x1b42e</c>: Back 0 → 143, Middle 1 → 138, Front 2 → 133).</summary>
    public static int FieldPortraitY(P3PartyRank rank) => rank switch
    {
        P3PartyRank.Back => 143,
        P3PartyRank.Middle => 138,
        _ => 133,
    };

    /// <summary>Slot <paramref name="slot"/>'s portrait x (<c>0x1b77c</c>: 6 bytes a slot).</summary>
    public static int FieldPortraitX(int slot) => 48 * slot;

    /// <summary>Draws the field now; false (nothing drawn) when no round is on it.</summary>
    public static bool DrawCombatField(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, CombatPresenter presenter)
    {
        if (presenter.FieldShown is not { } state || presenter.FieldSnapshot is not { } snapshot)
            return false;
        DrawFieldScene(batch, pixel, assets, presenter, snapshot, state);
        return true;
    }

    /// <summary>The fight menu bar's rows (0–10: <c>0x14efe</c> pulls a menu down while the pointer is above y 11).</summary>
    public const int FieldMenuBarHeight = 11;

    /// <summary>
    /// The rule <c>0x12d6</c> places a bar by (s156: the pack's <c>keep_windows.menu_bars</c> hold the places it gives, and
    /// these build <see cref="RuleMenuBar"/>, the stand-in for a pack without them and the tests' oracle).
    /// A menu title's text column (<c>0x12d6</c>, run once a menu list at start-up, <c>0x7706</c>…): the first title at
    /// column 0, each next one its length + 2 further (File 0, Options 6, Speed 15; the town's File 0, Guild 6, Options 13).
    /// </summary>
    public static int FieldMenuColumn(IReadOnlyList<string> titles, int menu)
    {
        var column = 0;
        for (var i = 0; i < menu; i++)
            column += titles[i].Length + 2;
        return column;
    }

    /// <summary>A title's x (<c>0x1c5c</c>: Move((column + 1) · 8, 8), JAM2): File 8, Options 56, Speed 128.</summary>
    public static int FieldMenuTitleX(int column) => (column + 1) * 8;

    /// <summary>The cells a pulled-down title inverts (<c>0x184a</c> → <c>0x2b1c</c>(column, 1, length + 2, 10): rows 1–10).</summary>
    public static Rectangle FieldMenuTitleSpan(int column, string title) => new(column * 8, 1, (title.Length + 2) * 8, 10);

    /// <summary>
    /// A pulled-down menu's box (<c>0x12d6</c> sizes, <c>0x28de</c> fill): from the title's column rounded down to even and
    /// y 12, each item's length + 1 rounded up to even, the widest in cells, (items + 1) · 8 rows; solid colour 15, no frame.
    /// The fight's File (0, 12, 80, 16), Options (48, 12, 144, 64), Speed (112, 12, 96, 32); the town's Options (96, 12)–(223, 107).
    /// </summary>
    public static Rectangle FieldMenuBox(int column, IReadOnlyList<string> items) =>
        new((column & ~1) * 8, FieldMenuBoxTop, items.Max(i => (i.Length + 2) & ~1) * 8, (items.Count + 1) * 8);

    /// <summary>A pulled-down box's top row (<c>0x184a</c>: <c>0x28de</c>(…, 12, …)).</summary>
    public const int FieldMenuBoxTop = 12;

    /// <summary>Item <paramref name="i"/>'s row (<c>0x171a</c>: text row 2 + i at the box's column + 1; the highlight inverts the box's width).</summary>
    public static Rectangle FieldMenuItemRow(Rectangle box, int i) => new(box.X, 16 + 8 * i, box.Width, 8);

    /// <summary>
    /// The Speed item as drawn: <c>0x870e</c> writes '*' over character 3 of the setting in force and a blank over the
    /// others' ("+) *Fast"); start-up ticks Normal (<c>0x76c0</c>: <c>0x870e('=')</c>).
    /// </summary>
    public static string FieldSpeedLabel(string label, bool ticked) =>
        label.Length > 3 ? label[..3] + (ticked ? '*' : ' ') + label[4..] : label;

    /// <summary>The bar's colours: ground 15 (<c>0x3894</c>: <c>0x648</c>(0, 0, 319, 10, 15)) and text 0 (menu +6 / +8) of the combat palette.</summary>
    public const int FieldMenuGroundIndex = 15, FieldMenuInkIndex = 0;

    /// <summary>
    /// s156: a III Amiga bar as <c>0x12d6</c> lays it out from its strings (the rule above): titles at their columns, the
    /// title spans, the boxes and item rows, pens 15 / 0, topaz 8. The pack's <c>keep_windows.menu_bars</c> hold these places
    /// written out; this stands in for a pack without them, and the tests check the pack against it.
    /// </summary>
    public static CombatFieldMenuBar RuleMenuBar(IReadOnlyList<(string TitleId, IReadOnlyList<string> ItemIds)> menus)
    {
        var titles = menus.Select(m => UiText.S(m.TitleId)).ToList();
        var list = new List<CombatFieldMenu>();
        for (var m = 0; m < menus.Count; m++)
        {
            var column = FieldMenuColumn(titles, m);
            var span = FieldMenuTitleSpan(column, titles[m]);
            var items = menus[m].ItemIds;
            var box = items.Count > 0 ? FieldMenuBox(column, [.. items.Select(UiText.S)]) : Rectangle.Empty;
            list.Add(new CombatFieldMenu(menus[m].TitleId, FieldMenuTitleX(column), new Box(span.X, span.Y, span.Width, span.Height),
                items, new Box(box.X, box.Y, box.Width, box.Height), 8, box.X + 8, 16));
        }
        return new CombatFieldMenuBar(FieldMenuBarHeight, FieldMenuGroundIndex, FieldMenuInkIndex, 2, list)
        {
            BoxPen = FieldMenuGroundIndex, ItemPen = FieldMenuInkIndex, HighlightPen = FieldMenuInkIndex,
            HighlightTextPen = FieldMenuGroundIndex, Frame = false, Font = "amiga-topaz-8",
        };
    }

    /// <summary>The pack's III bar <paramref name="name"/> (<c>keep_windows.menu_bars</c>), else <see cref="RuleMenuBar"/>.</summary>
    public static CombatFieldMenuBar P3MenuBar(AssetCatalog? assets, string name,
        IReadOnlyList<(string TitleId, IReadOnlyList<string> ItemIds)> menus) =>
        assets?.KeepWindows?.MenuBars.TryGetValue(name, out var bar) == true ? bar : RuleMenuBar(menus);

    /// <summary>
    /// III Amiga's own menu bar (<c>0x3894</c>) and a pulled-down menu (<c>0x184a</c>), from <paramref name="bar"/>'s places
    /// (s156: the pack's): the bar over its rows in its pen, the titles in the text pen (the open one inverted over its
    /// span), the open menu's solid box with its items, the highlighted row inverted over the box's width. Pens through
    /// <paramref name="pen"/> (the palette in force). Shared by the fight's bar (s119), the bar outside the towns (s136) and
    /// the town's (s156).
    /// </summary>
    internal static void DrawAmigaMenuBar(SpriteBatch batch, Texture2D pixel, CombatFieldMenuBar bar, Func<int, Color> pen,
        int? open, IReadOnlyList<string> openLabels, int lit,
        Func<int, Rectangle, HitRegion> titleHit, Func<int, Rectangle, HitRegion> itemHit, GameSession? gold = null)
    {
        using var font = PixelFont.With(FontSources.Named(bar.Font, bar.FontFile));
        Fill(batch, pixel, new Rectangle(0, 0, 320, bar.Rows), pen(bar.BarPen));
        // P3 QoL: the Guild treasury's gold line at the bar's free right end (III Amiga's town and outside bars).
        var usedRight = bar.Menus.Count == 0 ? 0 : bar.Menus.Max(m => Math.Max(ToRect(m.Highlight).Right, m.TitleX + PixelFont.Measure(UiText.S(m.Title))));
        DrawBarGold(batch, pixel, gold, usedRight, bar.TextY, pen(bar.TextPen));
        for (var m = 0; m < bar.Menus.Count; m++)
        {
            var menu = bar.Menus[m];
            var span = ToRect(menu.Highlight);
            if (open == m)
                Fill(batch, pixel, span, pen(bar.HighlightPen)); // 0x2b1c: XOR, colour 15 ↔ 0
            PixelFont.Draw(batch, pixel, menu.TitleX, bar.TextY, UiText.S(menu.Title),
                pen(open == m ? bar.HighlightTextPen : bar.TextPen));
            Hits?.Add(titleHit(m, new Rectangle(span.X, 0, span.Width, bar.Rows)));
        }
        if (open is not { } shown || shown >= bar.Menus.Count || openLabels.Count == 0)
            return;
        var openMenu = bar.Menus[shown];
        var box = ToRect(openMenu.Box);
        Fill(batch, pixel, box, pen(bar.BoxPen));
        if (bar.Frame)
            Outline(batch, pixel, box, pen(bar.BorderPen));
        for (var i = 0; i < openLabels.Count; i++)
        {
            var row = new Rectangle(box.X, openMenu.ItemY + i * openMenu.ItemHeight, box.Width, openMenu.ItemHeight);
            if (i == lit)
                Fill(batch, pixel, row, pen(bar.HighlightPen));
            PixelFont.Draw(batch, pixel, openMenu.ItemX, row.Y, openLabels[i], pen(i == lit ? bar.HighlightTextPen : bar.ItemPen));
            Hits?.Add(itemHit(i, row));
        }
    }

    /// <summary>The labels of <paramref name="bar"/>'s menu <paramref name="menu"/>, the Speed one ticking <paramref name="speed"/> ('*', <c>0x870e</c>).</summary>
    internal static IReadOnlyList<string> AmigaMenuLabels(CombatFieldMenuBar bar, int? menu, int speedMenu, AmigaCombatSpeed speed) =>
        menu is { } m && m < bar.Menus.Count
            ? [.. bar.Menus[m].Items.Select((id, i) => m == speedMenu ? FieldSpeedLabel(UiText.S(id), i == (int)speed - 1) : UiText.S(id))]
            : [];

    /// <summary>The fight's bar (<c>0x2397c</c>): the pack's "fight", else the rule over <see cref="CombatPresenter.AmigaFieldMenus"/>.</summary>
    public static CombatFieldMenuBar FightMenuBar(AssetCatalog? assets) => P3MenuBar(assets, "fight",
        [.. CombatPresenter.AmigaFieldMenus.Select(m => (m.TitleId, (IReadOnlyList<string>)[.. m.Items.Select(i => i.LabelId)]))]);

    /// <summary>
    /// III Amiga's fight between rounds (s119, FORMAT.md §8.5, <c>0x14ed2</c>): the field as it stands, no line, and the
    /// game's own menu bar "File  Options  Speed" over its top; a menu pulled down (a press on its title, or the
    /// remaster's arrows) shows its items, the highlighted one inverted. False when the field is not waiting there.
    /// </summary>
    public static bool DrawFieldMenuScreen(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, GameSession session,
        CombatPresenter presenter)
    {
        if (!presenter.AmigaFieldMenuShown || session.Combat is not { } combat)
            return false;
        var snapshot = CombatFieldSnapshot.Of(session.Party, combat);
        DrawFieldScene(batch, pixel, assets, presenter, snapshot, null);
        var bar = FightMenuBar(assets);
        var open = presenter.FieldMenuOpen;
        var items = open is { } menu ? CombatPresenter.AmigaFieldMenus[menu].Items : [];
        var labels = AmigaMenuLabels(bar, open, CombatPresenter.AmigaSpeedMenu, presenter.FieldSpeed);
        DrawAmigaMenuBar(batch, pixel, bar, i => assets.PaletteColour(CombatPalette, i) ?? DosBlack, open, labels, presenter.FieldMenuItem,
            (m, span) => new HitRegion(span, new AmigaMenuTitleTag(m)),
            (i, row) => new HitRegion(row, new AmigaMenuItemTag(i)) { Click = items[i].Key == Keys.None ? [] : [items[i].Key] });
        switch (presenter.FieldWindow)
        {
            case AmigaFieldWindow.MonsterNames when assets.KeepWindows?.MonsterNames is { } names:
                DrawMonsterNamesWindow(batch, pixel, assets, names, combat);
                break;
            case AmigaFieldWindow.PartyHealth when assets.KeepWindows?.PartyHealth is { } health:
                DrawPartyHealthWindow(batch, pixel, assets, health, session);
                break;
        }
        if (presenter.FieldWindow != AmigaFieldWindow.None) // 0x1e8c4(1): a mouse button closes it too
            Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom)) { Click = [Keys.Space] });
        return true;
    }

    /// <summary>An info window's frame (fill index 0, outer / inner frame) in its palette (<c>0x3904</c>).</summary>
    private static Func<int, Color> DrawInfoFrame(SpriteBatch batch, Texture2D pixel, AssetCatalog assets,
        KeepWindowsLayout.InfoWindow w)
    {
        Color C(int index) => assets.PaletteColour(w.Palette, index) ?? DosWhite;
        DrawWindowFrame(batch, pixel, assets.PaletteColour(w.Palette, 0) ?? DosBlack, C(w.Frame[0]), C(w.Frame[1]),
            w.Box[0], w.Box[1], w.Box[2], w.Box[3]);
        return C;
    }

    /// <summary>
    /// M on the field (<c>0x1a264(1)</c>, s120): window 23 at (16, 128)–(300, 191), `p3field.monster_identification`, then a row a
    /// rank with monsters (<c>0x1a44c</c>): rank 2 one row above rank 1, "Rank", its digit, ":", the count and the name
    /// with "(s)", all in one colour. The Amiga prints the group's count word +2 (whether slain monsters stay in it is not read `[~]`); the remaster shows the orders screen's living count and name (§8.3).
    /// </summary>
    private static void DrawMonsterNamesWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets,
        KeepWindowsLayout.InfoWindow w, CombatEncounter combat)
    {
        var colour = DrawInfoFrame(batch, pixel, assets, w)(w.Colours[0]);
        PixelFont.Draw(batch, pixel, w.Text[0].X, w.Text[0].Y, UiText.S("p3field.monster_identification"), colour);
        foreach (var (rank, n, name) in MonsterNamesRows(combat))
        {
            var dy = -8 * (rank - 1); // 0x1a44c: row 22 − rank
            void At(int i, string text) => PixelFont.Draw(batch, pixel, w.Text[i].X, w.Text[i].Y + dy, text, colour);
            At(1, UiText.S("p3field.rank"));
            At(2, rank.ToString());
            At(3, ":");
            At(4, n.ToString());
            At(5, UiText.F("p3combat.group_x_s", name));
        }
    }

    /// <summary>The ranks M lists, rank 2 first (<c>0x1a264</c>): its number (1 or 2), the count and the name; ranks with none left out. The count is the living monsters `[~]` (the Amiga's +2 not fully read).</summary>
    public static IEnumerable<(int Rank, int Count, string Name)> MonsterNamesRows(CombatEncounter combat) =>
        combat.Ranks.Where(r => !r.IsWiped && r.RankIndex is 0 or 1).OrderByDescending(r => r.RankIndex)
            .Select(r => (Rank: r.RankIndex + 1, Living: r.Living.ToList()))
            .Where(r => r.Living.Count > 0)
            .Select(r => (r.Rank, r.Living.Count, r.Living[0].Name));

    /// <summary>The members H shows (<c>0xadac</c>: the party count less a summoned elemental), at most six.</summary>
    public static IReadOnlyList<Character> PartyHealthMembers(Party party) =>
        party.Members.Where(m => !ReferenceEquals(m, party.Elemental)).Take(6).ToList();

    /// <summary>
    /// H on the field (<c>0xadac</c>, s120): window 24, the labels (the window's title, "Name:" … "Rt Leg:"), one column a
    /// member (<c>0xae2c</c>; the labels again for the second row of members): name (colour 4), status, HP and MP
    /// "current:most" (current right-aligned, <c>0xabec</c>), then the six body areas Okay / Injured / Broken / Gone in
    /// colours 15 / 14 / 13 / 12.
    /// </summary>
    private static void DrawPartyHealthWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets,
        KeepWindowsLayout.InfoWindow w, GameSession session)
    {
        var c = DrawInfoFrame(batch, pixel, assets, w);
        var labels = UiText.S("p3field.health_labels").Split('\n');
        var members = PartyHealthMembers(session.Party);
        void Lines(int x, int y, IReadOnlyList<string> lines, Color colour)
        {
            for (var i = 0; i < lines.Count; i++)
                PixelFont.Draw(batch, pixel, x, y + 8 * i, lines[i], colour);
        }
        Lines(w.Text[0].X, w.Text[0].Y, labels, c(w.Colours[0]));
        if (members.Count > 3)
            Lines(w.Text[1].X, w.Text[1].Y, labels, c(w.Colours[0]));
        var value = c(w.Colours[2]);
        for (var slot = 0; slot < members.Count; slot++)
        {
            var m = members[slot];
            var (x, y) = w.Text[2 + slot];
            PixelFont.Draw(batch, pixel, x, y, m.Name, c(w.Colours[1]));
            PixelFont.Draw(batch, pixel, x, y + 8, UiText.S($"p3combat.window_status.{(int)P3Fight.StatusOf(m)}"), value);
            foreach (var (row, cur, most) in new[] { (2, m.HpCur, m.HpMax), (3, m.MagicCur, m.MagicMax) })
            {
                PixelFont.Draw(batch, pixel, x + 8 * (2 - AmigaRightShift(cur)), y + 8 * row, cur.ToString(), value);
                PixelFont.Draw(batch, pixel, x + 24, y + 8 * row, ":", value);
                PixelFont.Draw(batch, pixel, x + 32, y + 8 * row, most.ToString(), value);
            }
            var body = P3BodyAreas.Of(m);
            for (var k = 0; k < 6; k++)
            {
                var state = (int)body[(P3BodyArea)k];
                PixelFont.Draw(batch, pixel, x, y + 8 * (4 + k), UiText.S($"p3field.area_state.{state}"), c(w.Colours[3 + state]));
            }
        }
    }

    /// <summary><c>0xabec</c>: a number right-aligned to its cell: 10–99 one cell left, 100–999 two, 1000+ not moved.</summary>
    public static int AmigaRightShift(int value) => value >= 1000 ? 0 : value >= 100 ? 2 : value >= 10 ? 1 : 0;

    private static void DrawFieldScene(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, CombatPresenter presenter,
        CombatFieldSnapshot snapshot, CombatFieldRun.State? state)
    {
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), DosBlack);
        var flash = presenter.FlashShown;
        var flashLayout = presenter.FlashLayout;

        for (var g = 0; g < snapshot.Groups.Count; g++)
        {
            var group = snapshot.Groups[g];
            var n = group.Count;
            for (var m = 0; m < n; m++)
            {
                var monster = group[m];
                if (state?.DeadMonsters.Contains(monster) == true)
                    continue;
                var attacking = ReferenceEquals(state?.AttackingMonster, monster);
                var idle = assets.GetMonsterIdle(monster.Id);
                var picture = attacking ? assets.GetMonsterAttacking(monster.Id) ?? idle : idle;
                var giant = FieldGiant(monster);
                var at = new Point(FieldMonsterX(m, n, giant), FieldGroupY(g, giant));
                if (picture is not null)
                    batch.Draw(picture, new Rectangle(at.X, at.Y, picture.Width, picture.Height), Color.White);
                if (flash is { } f && flashLayout is not null && ReferenceEquals(f.Flash.Monster, monster))
                    DrawCombatFlash(batch, assets, flashLayout, f, at);
            }
        }

        for (var slot = 0; slot < snapshot.Party.Count; slot++)
        {
            var member = snapshot.Party[slot];
            var elemental = slot == 6;
            var attacking = ReferenceEquals(state?.AttackingMember, member);
            var dead = state?.DeadMembers.Contains(member) ?? snapshot.DeadBefore.Contains(member);
            var picture = dead ? assets.PortraitGrave
                : elemental ? (attacking ? assets.PortraitElementalAttacking ?? assets.PortraitElemental : assets.PortraitElemental)
                : attacking ? assets.GetPortraitAttacking((int)member.Race) ?? assets.GetPortraitIdle((int)member.Race)
                : assets.GetPortraitIdle((int)member.Race);
            var at = new Point(FieldPortraitX(slot), FieldPortraitY(member.Phantasie3?.Rank ?? P3PartyRank.Front));
            if (picture is not null)
                batch.Draw(picture, new Rectangle(at.X, at.Y, picture.Width, picture.Height), Color.White);
            if (flash is { } f && flashLayout is not null && ReferenceEquals(f.Flash.Member, member))
                DrawCombatFlash(batch, assets, flashLayout, f, at);
        }

        if (state is { TextShown: true, Line: { } line })
        {
            var colour = assets.PaletteColour(CombatPalette,
                line.Party ? CombatFieldRun.PartyColour : CombatFieldRun.MonsterColour) ?? DosWhite;
            // Centred on text row 24 (0x166ec: column (40 − length) / 2 − 1); a line too long for one row wraps upward.
            var parts = Wrap(line.Text, 52).ToList();
            var top = AstralScene.TextTop - (parts.Count - 1) * 10;
            if (parts.Count > 1)
                Fill(batch, pixel, new Rectangle(0, top - 1, 320, AstralScene.TextTop - top), DosBlack);
            for (var i = 0; i < parts.Count; i++)
                PixelFont.Draw(batch, pixel, (320 - PixelFont.Measure(parts[i])) / 2, top + i * 10, parts[i], colour);
        }
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
    }
}
