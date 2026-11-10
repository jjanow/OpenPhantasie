using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Phantasie I Amiga's combat screen (s132, Amiga FORMAT.md "The combat screen"; the pack's <see cref="CombatFieldLayout"/>):
/// the black field in its pen-1 frame, the menu strip, the party row at y 150 and the monster groups stacked above it,
/// the round's line in pen 3 under the party; over it the Party Options menu and the orders requesters.
/// </summary>
public static partial class UiOverlays
{
    private static readonly ConditionalWeakTable<Texture2D, Texture2D> Silhouettes = new();

    /// <summary>The pen colours of <paramref name="layout"/>.</summary>
    private static Color[] Pens(CombatFieldLayout layout) => [.. layout.Palette.Select(VirtualScreen.ParseColor)];

    /// <summary>
    /// A shape's silhouette (h77 <c>+0x1332</c>: the four planes ORed into plane 0 of a cleared buffer): white where the shape
    /// has any pen but 0, black elsewhere; drawn tinted with pen 1.
    /// </summary>
    private static Texture2D Silhouette(Texture2D shape) => Silhouettes.GetValue(shape, s =>
    {
        var px = new Color[s.Width * s.Height];
        s.GetData(px);
        for (var i = 0; i < px.Length; i++)
            px[i] = px[i].A > 0 && (px[i].R | px[i].G | px[i].B) != 0 ? Color.White : Color.Black;
        var t = new Texture2D(s.GraphicsDevice, s.Width, s.Height);
        t.SetData(px);
        return t;
    });

    /// <summary>The string ids I's combat screen builds at run time (the order buttons, menus, prompts and help lines).</summary>
    public static IEnumerable<string> P1FieldStringKeys() =>
        new[] { CombatChoice.Thrust, CombatChoice.Attack, CombatChoice.Slash, CombatChoice.Lunge, CombatChoice.Cast, CombatChoice.Parry }
            .Select(c => $"p1field.order.{c.ToString().ToLowerInvariant()}")
            .Concat(CombatPresenter.P1FieldMenus.SelectMany(m => m.Items.Select(i => i.LabelId).Prepend(m.TitleId)))
            .Concat(["p1field.fight", "p1field.ok", "p1field.at_rank", "p1field.spells_available", "p1field.on_whom", "p1field.help",
                "p1field.orders_help", "p1field.order_help", "p1field.exit", "p1field.target_row", "p1field.status.dead",
                "p1field.status.ok", "p1field.status.sleep", "p1field.spell_help", "p1field.target_help"]);

    /// <summary>Every party slot's x on the I field for a row of <paramref name="n"/> (h53 <c>+0x86</c>).</summary>
    public static int P1SlotX(CombatFieldLayout layout, int i, int n) => CombatFieldLayout.SlotX(i, n, layout.Slot);

    /// <summary>Draws I Amiga's combat screen when it is up; false (nothing drawn) otherwise.</summary>
    public static bool DrawP1CombatField(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, GameSession session,
        CombatPresenter presenter, GameDataRepository? data = null)
    {
        if (!presenter.P1FieldShown || presenter.P1Layout is not { } layout)
            return false;
        // The fight's last boxes (requester 23) may still play after Core has closed the encounter: the snapshot holds the field.
        // s165: after a won fight its finds are asked over the field as state 12's set-up left it (presenter.P1AfterFight).
        if ((presenter.P1Snapshot ?? (session.Combat is { } combat ? P1CombatFieldSnapshot.Of(session.Party, combat) : null)
                ?? (presenter.P1AfterFightShown ? presenter.P1AfterFight : null))
            is not { } snapshot)
            return false;
        // The window's text (the line, the requesters) in the game's z6font, the bar in the screen's topaz 8 (s134).
        using var font = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        var pens = Pens(layout);
        var state = presenter.P1Shown;
        if (state is { Relaid: true })
        {
            // The round's end set-up (s136, h71 +0x1050 / h44 +0x614): the field as it stands after the round, re-laid and
            // re-ranked (Core's re-rank is DOS's, the same rule as h38 +0xe72); a won fight's groups are emptied.
            snapshot = session.Combat is { } now ? P1CombatFieldSnapshot.Of(session.Party, now)
                : new P1CombatFieldSnapshot(session.Party.Members.Where(m => m.Status != CharacterStatus.Dead).ToList(), []);
            state = state with
            {
                Graves = new HashSet<Character>(), Removed = new HashSet<Character>(), Gone = new HashSet<Monster>(),
            };
        }
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), pens[0]);
        // The fight's start (s135, h53 +0x274): the whole screen black while sound 17 holds, then the set-ups.
        var intro = presenter.P1Intro(snapshot.Row.Count, snapshot.Groups.Sum(g => g.Count));
        if (intro is { Black: true } || state is { Black: true } || presenter.P1OpeningPending)
        {
            // D-058 (s140): the remaster's own encounter flash on the line over the black too (the Amiga's black is bare).
            if (presenter.P1Banner is { Length: > 0 } banner)
                PixelFont.Draw(batch, pixel, (320 - PixelFont.Measure(banner.Trim())) / 2, layout.LineY, banner.Trim(), pens[layout.LinePen]);
            Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
            return true;
        }
        // The set-up (h53 +0x2fc): the fill, then one picture a WaitTOF: the row first, then the monsters.
        var setup = intro?.SetupDrawn ?? state?.SetupDrawn;
        var rowCount = snapshot.Row.Count(m => state?.Removed.Contains(m) != true);
        var rowBudget = setup is int s1 ? Math.Max(0, s1 - 1) : int.MaxValue;
        var monsterBudget = setup is int s2 ? Math.Max(0, s2 - 1 - rowCount) : int.MaxValue;
        if ((intro is null or { Frame: true }) && state?.FrameHidden != true)
            DrawP1Frame(batch, pixel, layout, pens); // after a BltClear the frame comes last (h77 +0x6dc)

        // The monster groups (h53 +0x2fc): front rank lowest, each group's row by the gap rule with its count.
        var heights = snapshot.Groups.Select(g => assets.GetMonsterIdle(g[0].Id)?.Height ?? layout.Slot).ToList();
        var tops = layout.GroupTops(heights);
        Rectangle? sparkleOn = null;
        var blitted = 0;
        for (var g = 0; g < snapshot.Groups.Count; g++)
        {
            var group = snapshot.Groups[g];
            for (var i = 0; i < group.Count; i++)
            {
                var monster = group[i];
                if (state?.Gone.Contains(monster) == true || blitted++ >= monsterBudget)
                    continue;
                var idle = assets.GetMonsterIdle(monster.Id);
                if (idle is null)
                    continue;
                var picture = ReferenceEquals(state?.AttackingMonster, monster) ? assets.GetMonsterAttacking(monster.Id) ?? idle : idle;
                var at = new Point(CombatFieldLayout.SlotX(i, group.Count, idle.Width), tops[g]);
                if (ReferenceEquals(state?.Sparkle?.Monster, monster))
                    sparkleOn = new Rectangle(at.X, at.Y, idle.Width, idle.Height);
                if (ReferenceEquals(state?.FlashMonster, monster))
                    batch.Draw(Silhouette(idle), new Rectangle(at.X, at.Y, idle.Width, idle.Height), pens[1]);
                else
                    batch.Draw(picture, new Rectangle(at.X, at.Y, picture.Width, picture.Height), Color.White);
            }
        }

        // The party row (h53 +0x478): members not DEAD; a dying member's grave until the row is re-laid without it.
        var row = snapshot.Row.Where(m => state?.Removed.Contains(m) != true).ToList();
        for (var i = 0; i < row.Count && i < rowBudget; i++)
        {
            var member = row[i];
            var x = P1SlotX(layout, i, row.Count);
            if (state?.Graves.Contains(member) == true)
            {
                if (layout.Grave is { } grave && assets.Get(grave) is { } mark)
                    batch.Draw(mark, new Rectangle(x + layout.GraveX, layout.PartyY + layout.GraveY, mark.Width, mark.Height), Color.White);
                continue;
            }
            var idle = assets.GetPortraitIdle((int)member.Race);
            if (idle is null)
                continue;
            if (ReferenceEquals(state?.FlashMember, member))
                batch.Draw(Silhouette(idle), new Rectangle(x, layout.PartyY, idle.Width, idle.Height), pens[1]);
            else
            {
                var picture = ReferenceEquals(state?.AttackingMember, member)
                    ? assets.GetPortraitAttacking((int)member.Race) ?? idle : idle;
                batch.Draw(picture, new Rectangle(x, layout.PartyY, picture.Width, picture.Height), Color.White);
            }
        }

        // The line (h80 +0x84a(text, 3, 185)): centred on 320, the last printed one stays; before the fight's first line the
        // remaster's D-058 flash (no wait, s140).
        if (!presenter.P1AfterFightShown // s165: the set-up's fill cleared the line before the finds
            && (state?.Text ?? presenter.P1LastLine ?? presenter.P1Banner) is { Length: > 0 } text)
            PixelFont.Draw(batch, pixel, (320 - PixelFont.Measure(text.Trim())) / 2, layout.LineY, text.Trim(), pens[layout.LinePen]);

        // A party spell's sparkle (h53 +0xab8 -> h77 +0xf2a, s133): a hardware sprite over the field at a random 16x16
        // spot inside the monster (+0xa54).
        if (sparkleOn is { } on && state?.Sparkle is { } shown && layout.Sparkle is { } sp && assets.Get(sp.Picture) is { } star)
        {
            var x = on.X + CombatFieldSparkle.Offset(shown.RandX, on.Width - sp.Size);
            var y = on.Y + CombatFieldSparkle.Offset(shown.RandY, on.Height - sp.Size);
            batch.Draw(star, new Rectangle(x, y, star.Width, star.Height), Color.White);
        }

        if (intro is not null)
        {
            // BltClear took the bar too; the Party Options wait prints it after the set-up (s138 live: a frame after the frame).
            Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
            return true;
        }
        if (state?.BarCleared != true) // a surprise opening's round: the BltClear took the bar until Party Options (s138)
            DrawP1MenuBar(batch, pixel, layout, pens, presenter);
        if (presenter.P1OrdersShown && layout.Orders is { } orders)
            DrawP1Orders(batch, pixel, layout, orders, pens, session, presenter, data);
        if (state?.Box is { } box && layout.Message is { } message)
            DrawP1Message(batch, pixel, message, pens, box);
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
        return true;
    }

    /// <summary>The frame (h77 <c>+0x6dc</c>): <see cref="CombatFieldLayout.FrameWidth"/> nested outlines in its pen.</summary>
    private static void DrawP1Frame(SpriteBatch batch, Texture2D pixel, CombatFieldLayout layout, Color[] pens)
    {
        var f = layout.Frame;
        for (var k = 0; k < layout.FrameWidth; k++)
            Outline(batch, pixel, new Rectangle(f.X + k, f.Y + k, f.W - 2 * k, f.H - 2 * k), pens[layout.FramePen]);
    }

    /// <summary>Rows of the remaster's hit line on I Amiga's field (<see cref="DrawP1HitLine"/>).</summary>
    public const int P1HitLinePitch = 8;

    /// <summary>
    /// s183: where the remaster's hit line (<see cref="CombatPresenter.FormatHitTooltip"/>, not the Amiga's) goes on I
    /// Amiga's field: the field's top rows, under the frame, where nothing is drawn while the round plays (the party row at
    /// <see cref="CombatFieldLayout.PartyY"/> and the line under it are the Amiga's). When the monster groups stacked above
    /// the party (<paramref name="groupHeights"/>, front rank first) reach up into those rows, the line goes on the menu
    /// bar's row instead (<c>OnBar</c>; user s184: never left off). The line shows only while the round plays, when the
    /// bar's titles take no pick, as the overworld puts a dialog's help on its bar (<see cref="P1MapHelpOnBar"/>).
    /// </summary>
    public static (int Top, bool OnBar) P1HitLinePlace(CombatFieldLayout layout, IReadOnlyList<int> groupHeights, int rows = 1)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(groupHeights);
        var top = layout.Field.Y + 1;
        var bottom = top + rows * P1HitLinePitch; // the band's first row under it
        if (groupHeights.Count == 0 || layout.GroupTops(groupHeights).Min() > bottom || layout.MenuBar is not { } bar)
            return (top, false);
        return (Math.Max(0, bar.TextY), true);
    }

    /// <summary>
    /// s183: the remaster's hit line on I Amiga's field at <see cref="P1HitLinePlace"/> (before, it took the remaster's help
    /// strip at y 168, over the party row): centred, on the field's pen 0, or on the menu bar's ground in its title pen.
    /// </summary>
    public static void DrawP1HitLine(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, GameSession session,
        CombatPresenter presenter, string text)
    {
        if (string.IsNullOrEmpty(text) || presenter.P1Layout is not { } layout)
            return;
        var snapshot = presenter.P1Snapshot ?? (session.Combat is { } combat ? P1CombatFieldSnapshot.Of(session.Party, combat) : null);
        var heights = snapshot?.Groups.Select(g => assets.GetMonsterIdle(g[0].Id)?.Height ?? layout.Slot).ToList() ?? [];
        var lines = Lines(text, (layout.Field.W - 4) / PixelFont.Advance);
        var (top, onBar) = P1HitLinePlace(layout, heights, lines.Count);
        var pens = Pens(layout);
        if (onBar && layout.MenuBar is { } bar)
        {
            lines = Lines(text, (320 - 4) / PixelFont.Advance); // the bar's whole width
            Fill(batch, pixel, new Rectangle(0, 0, 320, Math.Max(bar.Rows, top + lines.Count * P1HitLinePitch)), pens[bar.BarPen]);
            for (var i = 0; i < lines.Count; i++)
                PixelFont.Draw(batch, pixel, (320 - PixelFont.Measure(lines[i])) / 2, top + i * P1HitLinePitch, lines[i],
                    pens[bar.TextPen]);
            return;
        }
        Fill(batch, pixel, new Rectangle(layout.Field.X, top - 1, layout.Field.W, lines.Count * P1HitLinePitch + 1), pens[0]);
        for (var i = 0; i < lines.Count; i++)
            PixelFont.Draw(batch, pixel, (320 - PixelFont.Measure(lines[i])) / 2, top + i * P1HitLinePitch, lines[i],
                new Color(200, 220, 255));
    }

    /// <summary>
    /// The menu strip (h81): the titles on the bar; with a menu pulled down (Party Options waiting) its title's highlight
    /// box, the item box with its border and the items, the highlighted one complemented.
    /// </summary>
    private static void DrawP1MenuBar(SpriteBatch batch, Texture2D pixel, CombatFieldLayout layout, Color[] pens,
        CombatPresenter presenter)
    {
        if (layout.MenuBar is not { } bar)
            return;
        using var font = PixelFont.With(FontSources.Named(bar.Font, bar.FontFile));
        Fill(batch, pixel, new Rectangle(0, 0, 320, bar.Rows), pens[bar.BarPen]);
        var open = presenter.P1FieldMenuShown ? presenter.FieldMenuOpen : null;
        for (var m = 0; m < bar.Menus.Count; m++)
        {
            var menu = bar.Menus[m];
            var title = UiText.S(menu.Title);
            var lit = open == m;
            if (lit)
                Fill(batch, pixel, ToRect(menu.Highlight), pens[bar.HighlightPen]);
            // At rest the bar is the screen title (pen 13); with a menu down Intuition draws the strip (pen 15).
            var pen = lit ? bar.HighlightTextPen : open is null ? bar.TextPen : bar.ItemPen;
            PixelFont.Draw(batch, pixel, menu.TitleX, bar.TextY, title, pens[pen]);
            if (presenter.P1FieldMenuShown)
                Hits?.Add(new HitRegion(ToRect(menu.Highlight), new AmigaMenuTitleTag(m)));
        }
        if (open is not { } shown || shown >= bar.Menus.Count || bar.Menus[shown] is not { Items.Count: > 0 } openMenu)
            return;
        var box = ToRect(openMenu.Box);
        Fill(batch, pixel, box, pens[bar.BoxPen]);
        Outline(batch, pixel, box, pens[bar.BorderPen]);
        Outline(batch, pixel, new Rectangle(box.X + 1, box.Y, box.Width - 2, box.Height), pens[bar.BorderPen]); // 2 px sides
        var keys = CombatPresenter.P1FieldMenus[Math.Min(shown, CombatPresenter.P1FieldMenus.Count - 1)].Items;
        for (var i = 0; i < openMenu.Items.Count; i++)
        {
            var y = openMenu.ItemY + i * openMenu.ItemHeight;
            var rowBox = new Rectangle(box.X + 2, y, box.Width - 4, openMenu.ItemHeight);
            var lit = i == presenter.FieldMenuItem;
            if (lit)
                Fill(batch, pixel, rowBox, pens[bar.HighlightPen]);
            var label = UiText.S(openMenu.Items[i]); // Speed's '*' is Normal's own text on the Amiga (h81): it never moves
            PixelFont.Draw(batch, pixel, openMenu.ItemX, y + 1, label, pens[lit ? bar.HighlightTextPen : bar.ItemPen]);
            Hits?.Add(new HitRegion(rowBox, new AmigaMenuItemTag(i)) { Click = i < keys.Count && keys[i].Key != Keys.None ? [keys[i].Key] : [] });
        }
    }

    private static Rectangle ToRect(Box b) => new(b.X, b.Y, b.W, b.H);

    /// <summary>
    /// Requester 23 (h44 <c>+0x466</c> …): the box with its two borders and the message's rows, each centred in the pack's
    /// <c>center_columns</c> cells (h65 <c>+0x1f4</c>, s135) from the IntuiTexts' LeftEdge; one line takes the middle row.
    /// </summary>
    private static void DrawP1Message(SpriteBatch batch, Texture2D pixel, CombatFieldMessage message, Color[] pens,
        IReadOnlyList<string> rows)
    {
        DrawRequester(batch, pixel, message.Box, pens[message.FillPen], pens[message.BorderPen]);
        var at = message.RowIndices(rows.Count);
        for (var i = 0; i < rows.Count && i < at.Count; i++)
            PixelFont.Draw(batch, pixel, message.TextLeft(rows[i].Length, PixelFont.Measure(" ")), message.RowsY[at[i]], rows[i],
                pens[message.TextPen]);
    }

    /// <summary>
    /// s154: Party Options › Show names on I Amiga's field (h44 <c>+0x138c</c>): requester 23 with the rank lines written
    /// straight into its rows from the top (h23 <c>+0xd28</c>: not centred, from the IntuiTexts' LeftEdge), in the combat
    /// screen's pens and z6font. More lines than the box's rows grow it downward (the Amiga blanks three; I has at most three
    /// groups, <c>EncounterBuilder.MaxGroups</c>).
    /// </summary>
    public static void DrawP1NamesList(SpriteBatch batch, Texture2D pixel, CombatFieldLayout layout, IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(rows);
        if (layout.Message is not { RowsY.Count: > 0 } message)
            return;
        var pens = Pens(layout);
        var step = message.RowsY.Count > 1 ? message.RowsY[1] - message.RowsY[0] : PixelFont.LineHeight;
        var extra = Math.Max(0, rows.Count - message.RowsY.Count) * step;
        DrawRequester(batch, pixel, message.Box with { H = message.Box.H + extra }, pens[message.FillPen], pens[message.BorderPen]);
        using var font = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        for (var k = 0; k < rows.Count; k++)
            PixelFont.Draw(batch, pixel, message.TextX, message.RowsY[0] + k * step, rows[k], pens[message.TextPen]);
    }

    /// <summary>
    /// Requester 23 over I Amiga's map (s139, pack <c>map_message</c>): the combat screen's box and rows in the map
    /// palette's pens (shot <c>p1-amiga-combat-33</c>), its text in the combat screen's z6font.
    /// </summary>
    public static void DrawP1MapMessage(SpriteBatch batch, Texture2D pixel, MapMessageLayout layout, IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(rows);
        using var font = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        Color[] pens = [.. layout.Palette.Select(VirtualScreen.ParseColor)];
        DrawP1Message(batch, pixel, layout.Message, pens, rows);
    }

    /// <summary>
    /// s144: requester 21 as I Amiga's MESS text window over the dungeon map (h41 <c>+0x11e4</c>, pack
    /// <c>map_message.window</c>): no border, title or button, Top 22, 8·n + 33 high, the rows as logged (40-byte records,
    /// not centred) in the z6font from x 41 (shots <c>p1-amiga-dungeon-44</c> / <c>-46</c>).
    /// </summary>
    public static void DrawP1MapTextWindow(SpriteBatch batch, Texture2D pixel, MapMessageLayout layout, MapMessageWindow window,
        IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(rows);
        using var font = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        Color[] pens = [.. layout.Palette.Select(VirtualScreen.ParseColor)];
        Fill(batch, pixel, ToRect(window.TextBox(rows.Count)), pens[window.FillPen]);
        for (var k = 0; k < rows.Count; k++)
            PixelFont.Draw(batch, pixel, window.RowX, window.TextRowY(k), rows[k], pens[window.RowPen]);
    }

    /// <summary>
    /// s153: the rows of the timed MESS window as click targets (gadgets 11 + k, RELVERIFY: a click ends the wait early).
    /// Call after the requester took every other click.
    /// </summary>
    public static void HitP1MapTextRows(MapMessageWindow window, int rows)
    {
        ArgumentNullException.ThrowIfNull(window);
        for (var k = 0; k < rows; k++)
            if (window.TextRowGadget(k) is { } gadget)
                Hits?.Add(new HitRegion(ToRect(gadget), new MapWindowRowTag(k)));
    }

    /// <summary>
    /// s153: I Amiga's event menu (h41 <c>+0x1440</c>, FORMAT.md "Requester 21 on the map"): the MESS text window
    /// (<see cref="DrawP1MapTextWindow"/>) and requester 25 under it, its buttons the stored gadgets 93 + i with
    /// <paramref name="cells"/> as stored (11 or 3 characters, blanks kept). A click on a button answers with its digit, as
    /// a key would; clicks elsewhere go nowhere (the Amiga drops them).
    /// </summary>
    public static void DrawP1MapMenu(SpriteBatch batch, Texture2D pixel, MapMessageLayout layout, MapMessageWindow window,
        IReadOnlyList<string> rows, IReadOnlyList<string> cells)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(cells);
        DrawP1MapTextWindow(batch, pixel, layout, window, rows);
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
        if (window.Menu is not { } menu)
            return;
        Color[] pens = [.. layout.Palette.Select(VirtualScreen.ParseColor)];
        var text = window.TextBox(rows.Count);
        var top = text.Y + text.H; // h43 +0 = 8n + 55: requester 25 starts under requester 21
        Fill(batch, pixel, ToRect(menu.BoxFor(top, cells.Count)), pens[menu.FillPen]);
        using var font = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        for (var i = 0; i < cells.Count; i++)
        {
            if (menu.Button(top, i) is not { } button)
                continue; // past the ten stored gadgets [?]: no MESS menu has more than nine
            DrawP1Button(batch, pixel, button, pens[menu.ButtonFillPen], pens[menu.ButtonBorderPen], cells[i], menu.Label.X,
                menu.Label.Y, pens[menu.LabelPen]);
            Hits?.Add(new HitRegion(ToRect(button), new MapMenuButtonTag(i)) { Click = i < 9 ? [HitTest.Digit(i + 1)] : [] });
        }
    }

    /// <summary>
    /// s144: requester 21 as I Amiga's damage report (h71 <c>+0x112e</c> / h86 <c>+0(0x15)</c>): the box with its two
    /// borders, <paramref name="lines"/>[0] as the title (topaz 8, pen 3, shot <c>p1-amiga-dungeon-45</c>), the other lines on
    /// the member rows (z6font), and the OK button. More lines than the Amiga's seven rows grow the box downward. s154: a
    /// title Core logs in DOS's text is drawn as the Amiga's (<see cref="P1MapReports.Report"/>); <paramref name="palette"/>
    /// (16 colours) replaces the map's (the fight's Party health is drawn in the combat screen's palette).
    /// </summary>
    public static void DrawP1MapReport(SpriteBatch batch, Texture2D pixel, MapMessageLayout layout, MapMessageWindow window,
        IReadOnlyList<string> lines, IReadOnlyList<string>? palette = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(lines);
        lines = P1MapReports.Report(window, lines);
        Color[] pens = [.. (palette is { Count: 16 } ? palette : layout.Palette).Select(VirtualScreen.ParseColor)];
        var rows = Math.Max(0, lines.Count - 1);
        DrawRequester(batch, pixel, window.ReportBox(rows), pens[window.FillPen], pens[window.BorderPen]);
        if (lines.Count > 0)
            using (PixelFont.With(FontSources.Named(window.TitleFont ?? layout.Font, window.TitleFont is null ? layout.FontFile : window.TitleFontFile)))
                PixelFont.Draw(batch, pixel, window.TitleX, window.TitleY, lines[0], pens[window.TitlePen]);
        using var font = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        for (var k = 0; k < rows; k++)
            PixelFont.Draw(batch, pixel, window.RowX, window.RowY + k * window.RowStep, lines[k + 1], pens[window.RowPen]);
        DrawP1Button(batch, pixel, window.OkFor(rows), pens[window.OkFillPen], pens[window.OkBorderPen], UiText.S(window.OkLabel),
            window.OkTextX - window.Ok.X, window.OkTextY - window.Ok.Y, pens[window.OkTextPen]);
    }

    /// <summary>Requester box: fill and two nested borders (Intuition <c>Border</c> pairs at (0, 0) and (1, 1)).</summary>
    private static void DrawRequester(SpriteBatch batch, Texture2D pixel, Box b, Color fill, Color border)
    {
        var r = ToRect(b);
        Fill(batch, pixel, r, fill);
        Outline(batch, pixel, r, border);
        Outline(batch, pixel, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), border);
    }

    /// <summary>A button (requesters 21 / 24 / 26): its fill, two borders one and two px outside it, its text.</summary>
    private static void DrawP1Button(SpriteBatch batch, Texture2D pixel, Box b, Color fill, Color border, string text, int textDx,
        int textDy, Color ink)
    {
        var r = ToRect(b);
        Outline(batch, pixel, new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4), border);
        Outline(batch, pixel, new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), border);
        Fill(batch, pixel, r, fill);
        PixelFont.Draw(batch, pixel, r.X + textDx, r.Y + textDy, text, ink);
    }

    /// <summary>
    /// One row of requester 21 (h71 <c>+0x2b2</c> + <c>+0x446</c>): <c>"%-10.9sHP:%-4.3dMP:%-3d "</c> and the order (the
    /// Amiga's text shows no leading zeros for <c>%-4.3d</c>, s128 shot).
    /// </summary>
    public static string P1OrderRow(Character member, string order)
    {
        ArgumentNullException.ThrowIfNull(member);
        var name = member.Name.Length > 9 ? member.Name[..9] : member.Name;
        return $"{name,-10}HP:{member.HpCur,-4}MP:{member.MagicCur,-3} {order}";
    }

    /// <summary>
    /// I Amiga's orders requesters (h71 <c>+0x90</c>): requester 21 with a row a listed member (status OK) and FIGHT; with a
    /// member picked (no row highlight, as the Amiga) requester 24's six order
    /// buttons, or requester 26 asking the rank (digit buttons) or the spell / its target (the typed digits `[~]`).
    /// </summary>
    private static void DrawP1Orders(SpriteBatch batch, Texture2D pixel, CombatFieldLayout layout, CombatFieldOrders o,
        Color[] pens, GameSession session, CombatPresenter presenter, GameDataRepository? data)
    {
        var listUp = presenter.P1AsksSpell || presenter.P1AsksTarget; // requester 12 / 6 over requester 21
        DrawRequester(batch, pixel, o.Box, pens[o.FillPen], pens[o.BorderPen]);
        var rows = CombatPresenter.P1OrderRows(session);
        var picked = presenter.P1PickedMember;
        for (var k = 0; k < rows.Count; k++)
        {
            var index = rows[k];
            var y = o.RowY + k * o.RowStep;
            var rowBox = new Rectangle(o.RowX - 1, y - 1, o.Box.W - 14, o.RowStep - 1);
            // s132 capture: a picked row shows no highlight (requester 24 opens below the list).
            // h71 +0x8e6: a cast's order text is the spell's name (s133).
            var order = layout.SpellList is not null && data is not null
                        && CombatAction.TryDecodeCast(presenter.PackedOrder(index), out var spell, out _)
                ? P1SpellName(spell, data) : presenter.OrderLabelFor(session, index);
            var text = P1OrderRow(session.Party.Members[index], order);
            PixelFont.Draw(batch, pixel, o.RowX, y, text, pens[o.RowPen]);
            if (!listUp)
                Hits?.Add(new HitRegion(rowBox) { Click = [Keys.D1 + k] });
        }
        DrawP1Button(batch, pixel, o.Fight, pens[o.FightFillPen], pens[o.FightBorderPen], "", 0, 0, pens[o.FightTextPen]);
        PixelFont.Draw(batch, pixel, o.FightTextX, o.FightTextY, UiText.S(o.FightLabel), pens[o.FightTextPen]);
        Hits?.Add(new HitRegion(ToRect(o.Fight)) { Click = [Keys.F] });
        if (picked < 0)
            return;
        if (listUp && layout.SpellList is { } lists)
        {
            if (presenter.P1AsksSpell)
                DrawP1SpellList(batch, pixel, lists.Spells, pens, session, presenter, data);
            else
                DrawP1TargetList(batch, pixel, lists.Target, pens, session);
            return;
        }
        if (presenter.P1AsksOrder)
        {
            DrawRequester(batch, pixel, o.ChoiceBox, pens[o.FillPen], pens[o.BorderPen]);
            var choices = CombatPresenter.MenuChoices(session);
            for (var b = 0; b < o.ChoiceButtons.Count && b < choices.Count; b++)
            {
                var label = UiText.S($"p1field.order.{choices[b].ToString().ToLowerInvariant()}");
                DrawP1Button(batch, pixel, o.ChoiceButtons[b], pens[o.FightFillPen], pens[o.FightBorderPen], label, 2, 2,
                    pens[o.FightTextPen]);
                Hits?.Add(new HitRegion(ToRect(o.ChoiceButtons[b])) { Click = [Keys.D1 + b] });
            }
            return;
        }
        // Requester 26: the rank (h71 +0x318: "<ORDER> AT RANK" and a digit button a living rank) or the spell steps.
        DrawRequester(batch, pixel, o.PromptBox, pens[o.FillPen], pens[o.BorderPen]);
        string prompt;
        if (presenter.AsksRank)
        {
            // h71 +0x318: the order's word and " AT RANK" (s132 capture: "THRUST AT RANK" and the buttons 1 2 3).
            prompt = CombatText.OrderLabel(new RepeatOrder(presenter.PendingChoice, -1, -1, 0)) + UiText.S("p1field.at_rank");
            var ranks = session.Combat?.Ranks.Count(r => !r.IsWiped) ?? 0;
            for (var b = 0; b < o.RankButtons.Count && b < ranks; b++)
            {
                var r = o.RankButtons[b];
                Outline(batch, pixel, new Rectangle(r.X - 1, r.Y - 1, r.W + 2, r.H + 2), pens[o.RankBorderPen]); // pen 1 (s132)
                Fill(batch, pixel, ToRect(r), pens[o.FightFillPen]);
                PixelFont.Draw(batch, pixel, r.X + 3, r.Y + 2, (b + 1).ToString(), pens[o.FightTextPen]);
                Hits?.Add(new HitRegion(ToRect(r)) { Click = [Keys.D1 + b] });
            }
        }
        else
            prompt = (presenter.AsksHealTarget ? UiText.S("p1field.on_whom") : UiText.S("p1field.spells_available"))
                     + " " + presenter.SpellDigits;
        PixelFont.Draw(batch, pixel, o.PromptTextX, o.PromptTextY, prompt, pens[o.PromptTextPen]);
    }

    /// <summary>
    /// Requester 12 `inspect.spells_available` (s133, h23 <c>+0x570</c>): the box, the title, the list's frame and fill, a page of
    /// six spell names (white on blue, JAM2), the two scroll gadgets (ghosted when they cannot move) and EXIT.
    /// </summary>
    private static void DrawP1SpellList(SpriteBatch batch, Texture2D pixel, CombatFieldList l, Color[] pens, GameSession session,
        CombatPresenter presenter, GameDataRepository? data)
    {
        DrawP1ListBox(batch, pixel, l, pens, UiText.S("p1field.spells_available"));
        if (l.ListFrame is { } frame)
            Outline(batch, pixel, ToRect(frame), pens[l.ListFramePen]);
        if (l.ListFill is { } fill)
            Fill(batch, pixel, ToRect(fill), pens[l.ListFillPen]);
        Hits?.Add(new HitRegion(ToRect(l.Box))); // the requester takes the clicks under it
        var page = presenter.P1SpellPage(session);
        for (var k = 0; k < page.Count && k < l.Rows; k++)
        {
            var row = l.Row(k);
            PixelFont.Draw(batch, pixel, row.X + 1, row.Y + 1, P1SpellName(page[k], data), pens[l.RowPen]);
            Hits?.Add(new HitRegion(ToRect(row)) { Click = [Keys.D1 + k] });
        }
        if (l.ScrollUp is { } up)
            DrawP1ScrollButton(batch, pixel, l, up, l.UpGlyph, presenter.P1SpellCanScrollUp, Keys.Up, pens);
        if (l.ScrollDown is { } down)
            DrawP1ScrollButton(batch, pixel, l, down, l.DownGlyph, presenter.P1SpellCanScrollDown(session), Keys.Down, pens);
        DrawP1Exit(batch, pixel, l, pens);
    }

    /// <summary>
    /// Requester 6 "ON WHOM:" (s133, h62 <c>+0x508</c> → <c>+0x28e</c> mode 4): every member in record order, DEAD and
    /// asleep too, one row each in the Amiga's format (<see cref="P1TargetRow"/>), and EXIT.
    /// </summary>
    private static void DrawP1TargetList(SpriteBatch batch, Texture2D pixel, CombatFieldList l, Color[] pens, GameSession session)
    {
        DrawP1ListBox(batch, pixel, l, pens, UiText.S("p1field.on_whom"));
        Hits?.Add(new HitRegion(ToRect(l.Box)));
        var members = session.Party.Members;
        for (var k = 0; k < members.Count && k < l.Rows; k++)
        {
            var row = l.Row(k);
            PixelFont.Draw(batch, pixel, row.X + 1, row.Y + 1, P1TargetRow(members[k]), pens[l.RowPen]);
            Hits?.Add(new HitRegion(ToRect(row)) { Click = [Keys.D1 + k] });
        }
        DrawP1Exit(batch, pixel, l, pens);
    }

    /// <summary>A list requester's box (fill, two borders) and its title (JAM1).</summary>
    private static void DrawP1ListBox(SpriteBatch batch, Texture2D pixel, CombatFieldList l, Color[] pens, string title)
    {
        DrawRequester(batch, pixel, l.Box, pens[l.FillPen], pens[l.BorderPen]);
        PixelFont.Draw(batch, pixel, l.TitleX, l.TitleY, title, pens[l.TitlePen]);
    }

    /// <summary>The EXIT gadget (id 0): two borders, the fill, "EXIT" (E).</summary>
    private static void DrawP1Exit(SpriteBatch batch, Texture2D pixel, CombatFieldList l, Color[] pens)
    {
        DrawP1Button(batch, pixel, l.Exit, pens[l.ButtonFillPen], pens[l.ExitBorderPen], UiText.S(l.ExitLabel), l.ExitTextDx,
            l.ExitTextDy, pens[l.ButtonTextPen]);
        Hits?.Add(new HitRegion(ToRect(l.Exit)) { Click = [Keys.E] });
    }

    /// <summary>A scroll gadget: two borders, the fill, its glyph; a disabled one (GADGDISABLED) under the ghost's dots.</summary>
    private static void DrawP1ScrollButton(SpriteBatch batch, Texture2D pixel, CombatFieldList l, Box b, IReadOnlyList<string> glyph,
        bool live, Keys key, Color[] pens)
    {
        DrawP1Button(batch, pixel, b, pens[l.ButtonFillPen], pens[l.ScrollBorderPen], "", 0, 0, pens[l.ButtonTextPen]);
        for (var y = 0; y < glyph.Count; y++)
        {
            for (var x = 0; x < glyph[y].Length; x++)
            {
                if (glyph[y][x] == '#')
                    Fill(batch, pixel, new Rectangle(b.X + l.GlyphDx + x, b.Y + l.GlyphDy + y, 1, 1), pens[l.ButtonTextPen]);
            }
        }
        if (live)
        {
            Hits?.Add(new HitRegion(ToRect(b)) { Click = [key] });
            return;
        }
        foreach (var (x, y) in P1GhostDots(b, l.GhostStep))
            Fill(batch, pixel, new Rectangle(x, y, 1, 1), pens[l.GhostPen]);
    }

    /// <summary>
    /// Intuition's ghost over a disabled gadget (s132 shot -07): a dot every <paramref name="step"/> px from the box's left on
    /// its even rows, shifted by half a step on the odd rows.
    /// </summary>
    public static IEnumerable<(int X, int Y)> P1GhostDots(Box b, int step)
    {
        for (var y = 0; y < b.H; y++)
        {
            for (var x = y % 2 == 0 ? 0 : step / 2; x < b.W; x += step)
                yield return (b.X + x, b.Y + y);
        }
    }

    /// <summary>A spell's name on requester 12 (<c>"%-29.29s"</c>, h24 <c>+0x113</c>).</summary>
    public static string P1SpellName(int spell, GameDataRepository? data)
    {
        var name = data is not null && spell >= 0 && spell < data.Spells.Count ? data.GetSpell(spell).Name
            : CombatText.OrderLabel(new RepeatOrder(CombatChoice.Cast, 0, spell, 0));
        return name.Length > 29 ? name[..29] : name;
    }

    /// <summary>
    /// A member's row on requester 6: <c>"%-10.9s%-6.5sHP%4d/%-4dMP%4d/%-3d"</c> (h63 <c>+0x48</c>): name, status (the
    /// Amiga's DEAD / OK / SLEEP, h12 <c>+0xb78</c>), HP now / max, MP now / max.
    /// </summary>
    public static string P1TargetRow(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        static string Cut(string s, int n) => s.Length > n ? s[..n] : s;
        var status = member.Status switch
        {
            CharacterStatus.Dead => UiText.S("p1field.status.dead"),
            CharacterStatus.Ok => UiText.S("p1field.status.ok"),
            CharacterStatus.Sleep => UiText.S("p1field.status.sleep"),
            _ => ((int)member.Status).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return UiText.F("p1field.target_row", Cut(member.Name, 9), Cut(status, 5), member.HpCur, member.HpMax, member.MagicCur,
            member.MagicMax);
    }
}
