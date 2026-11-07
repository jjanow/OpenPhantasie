using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>What a requester draw does, in order: a filled box, a one-pixel outline, or a text (JAM1, the ink only).</summary>
public enum RequesterOpKind { Fill, Outline, Text, Picture }

/// <summary>
/// One drawing step of a requester; <see cref="Title"/> texts use the title font (topaz 8), the rest the z6font. A
/// <see cref="RequesterOpKind.Picture"/> step draws the pack picture named <see cref="Text"/> into <see cref="Rect"/>.
/// </summary>
public readonly record struct RequesterOp(RequesterOpKind Kind, Box Rect, int Pen, string Text = "", bool Title = false);

/// <summary>A button's screen box and the key a click on it sends (the key the remaster's prompt takes for that answer).</summary>
public readonly record struct RequesterHit(Box Rect, Keys Click, int Index);

/// <summary>A requester ready to draw: its palette (16 colours), the steps and the buttons.</summary>
public sealed record RequesterScene(IReadOnlyList<string> Palette, IReadOnlyList<RequesterOp> Ops, IReadOnlyList<RequesterHit> Buttons);

/// <summary>
/// s154: I Amiga's requesters behind AMIGA SCREENS (pack <c>map_requesters</c>, Amiga <c>FORMAT.md</c> "Requesters 0, 22 and
/// 14"): requester 0 as h86 <c>+0x32a</c> fills it from an <c>"a|b[c|d]"</c> text (the map's questions and OK requesters),
/// requester 22 (the Black Knights) as stored, and requester 14 (the character sheet) as h29 <c>+0x93e</c> fills it. Pure: the
/// Client draws the steps (<see cref="UiOverlays.DrawP1Requester"/>), the tests raster them against the FS-UAE shots.
/// </summary>
public static class P1MapRequesters
{
    /// <summary>
    /// h86 <c>+0x32a</c>'s parse: the characters up to '[' are the text lines, '|' ends one; after '[' the button labels,
    /// '|' between them, ']' ends.
    /// </summary>
    public static (IReadOnlyList<string> Lines, IReadOnlyList<string> Buttons) Parse(string spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var lines = new List<string>();
        var buttons = new List<string>();
        var cur = new System.Text.StringBuilder();
        var inButtons = false;
        foreach (var c in spec)
        {
            switch (c)
            {
                case '|':
                    (inButtons ? buttons : lines).Add(cur.ToString());
                    cur.Clear();
                    break;
                case '[':
                    lines.Add(cur.ToString());
                    cur.Clear();
                    inButtons = true;
                    break;
                case ']':
                    break;
                default:
                    cur.Append(c);
                    break;
            }
        }
        (inButtons ? buttons : lines).Add(cur.ToString());
        return (lines, buttons);
    }

    /// <summary>
    /// The key a click on button <paramref name="i"/> sends for <paramref name="prompt"/>: Y / N on a YES / NO question
    /// (<see cref="PromptMenu.DirectChoice"/> takes no digit there), else the option's digit.
    /// </summary>
    public static Keys ClickKey(SessionPrompt? prompt, int i) =>
        prompt is { IsYesNo: true } ? (i == 0 ? Keys.Y : Keys.N) : Keys.D1 + i;

    /// <summary>Requester 0 for the h86 text <paramref name="spec"/>; a click on a button sends <paramref name="click"/>(i).</summary>
    public static RequesterScene Menu(MapRequestersLayout layout, string spec, Func<int, Keys> click)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(click);
        var m = layout.Menu ?? throw new InvalidOperationException("The pack has no map_requesters.menu.");
        var (lines, labels) = Parse(spec);
        var ops = new List<RequesterOp>();
        var b = m.Box;
        Frame(ops, b, m.FillPen, m.Borders);
        for (var i = 0; i < lines.Count; i++)
            if (lines[i].Length > 0)
                ops.Add(new RequesterOp(RequesterOpKind.Text, new Box(b.X + m.RowX(lines[i].Length), b.Y + m.RowY(lines.Count, i), 1, 1),
                    m.RowPen, lines[i]));
        var hits = new List<RequesterHit>();
        var places = m.ButtonsFor(labels.Count);
        for (var i = 0; i < labels.Count && i < places.Count; i++) // h86 links at most gadgets 0 and 1
        {
            var r = new Box(b.X + places[i].X, b.Y + places[i].Y, m.ButtonSize.W, m.ButtonSize.H);
            Button(ops, r, m.ButtonFacePen, m.ButtonBorderPen, labels[i], m.LabelX(labels[i].Length), m.LabelY, m.LabelPen);
            hits.Add(new RequesterHit(r, click(i), i));
        }
        return new RequesterScene(layout.Palette, ops, hits);
    }

    /// <summary>
    /// s156: requester 0 in the town (h86 <c>+0x32a</c> with the stored pens: <see cref="MapRequestersLayout.PenUnmap"/> on the
    /// town palette) for the question <paramref name="key"/> of the pack's <c>town</c> map, or null.
    /// </summary>
    public static RequesterScene? Town(MapRequestersLayout layout, string key, Func<int, Keys> click, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Menu is null || layout.TownPalette is not { } palette || layout.PenRemap.Count != 16
            || !layout.Town.TryGetValue(key, out var id))
            return null;
        return Remap(Menu(layout, UiText.F(id, args), click), layout.PenUnmap, palette);
    }

    /// <summary>
    /// s156: requester 27, Info › About Phantasie (h80 <c>+0xcb4</c>): the frame and the logo; the picture is its one gadget,
    /// whose click (Return) closes it.
    /// </summary>
    public static RequesterScene About(MapRequestersLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var a = layout.About ?? throw new InvalidOperationException("The pack has no map_requesters.about.");
        var ops = new List<RequesterOp>();
        Frame(ops, a.Box, a.FillPen, a.Borders);
        var pic = new Box(a.Box.X + a.PictureBox.X, a.Box.Y + a.PictureBox.Y, a.PictureBox.W, a.PictureBox.H);
        ops.Add(new RequesterOp(RequesterOpKind.Picture, pic, 0, a.Picture));
        return new RequesterScene(layout.Palette, ops, [new RequesterHit(pic, Keys.Enter, 0)]);
    }

    /// <summary>Requester 22 as stored ("BLACK KNIGHTS!!!", RUN / FIGHT / SURRENDER).</summary>
    public static RequesterScene Fixed(IReadOnlyList<string> palette, FixedRequester r, Func<int, Keys> click)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(click);
        var ops = new List<RequesterOp>();
        Frame(ops, r.Box, r.FillPen, r.Borders);
        ops.Add(new RequesterOp(RequesterOpKind.Text, new Box(r.Box.X + r.TitleAt.X, r.Box.Y + r.TitleAt.Y, 1, 1), r.TitlePen,
            UiText.S(r.Title), Title: true));
        var hits = new List<RequesterHit>();
        for (var i = 0; i < r.Buttons.Count; i++)
        {
            var g = r.Buttons[i];
            var box = new Box(r.Box.X + g.Rect.X, r.Box.Y + g.Rect.Y, g.Rect.W, g.Rect.H);
            Button(ops, box, g.FacePen, g.BorderPen, UiText.S(g.Label), g.TextX, g.TextY, g.TextPen);
            hits.Add(new RequesterHit(box, click(i), i));
        }
        return new RequesterScene(palette, ops, hits);
    }

    /// <summary>z6font's advance (XSize 6) and the room kept right of a row's last cell before the two border lines.</summary>
    private const int GlyphAdvance = 6, EdgeRoom = 3;

    /// <summary>
    /// Requester 14 filled for <paramref name="member"/> (h29 <c>+0x93e</c>); SPELLS … POTIONS send 1–4, EXIT Esc. With
    /// <paramref name="rolled"/> it is Guild › New's roll (h23 <c>+0xc08</c>): no name, PURGE (sends N: roll again) and KEEP
    /// (sends Y) in gadgets 41 / 43's places.
    /// </summary>
    public static RequesterScene Sheet(SheetRequester s, Character member, bool rolled = false)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(member);
        var ops = new List<RequesterOp>();
        var st = member.Stats;
        var level = UiText.F("inspect.level_x_x_x_x", member.Level,
            InspectDialog.Named(InspectDialog.ClassNames, (int)member.Class), InspectDialog.Named(InspectDialog.RaceNames, (int)member.Race),
            InspectDialog.Named(InspectDialog.StatusNames, (int)member.Status));
        object[][] values =
        [
            [st.Str], [st.Int], [st.Dex], [st.Con], [st.Cha], [st.Luck],
            [member.AgeYears], [member.HpCur, member.HpMax], [member.MagicCur, member.MagicMax], [member.GoldInBank],
            // the manual's longer label leaves six cells: a 6-digit or longer value keeps a blank before it (InspectDialog's rule)
            [member.Experience >= 100000 ? $" {member.Experience}" : member.Experience.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            [member.Score],
        ];
        var rows = new string[Math.Min(s.Rows.Count, values.Length)];
        for (var i = 0; i < rows.Length; i++)
            rows[i] = UiText.F(s.Rows[i].Text, values[i]);
        // D-064 (user s53, s155: the manual's wording on the Amiga sheet too): "EXPER. POINTS" leaves the row less room than
        // the Amiga's "EX. PTS", so a row that would reach the border widens the requester instead of being cut.
        var b = s.Box;
        var need = s.LevelAt.X + level.Length * GlyphAdvance;
        for (var i = 0; i < rows.Length; i++)
            need = Math.Max(need, s.Rows[i].X + rows[i].Length * GlyphAdvance);
        if (need + EdgeRoom > b.W)
            b = b with { W = need + EdgeRoom };
        Frame(ops, b, s.FillPen, s.Borders, b.W - s.Box.W);
        if (!rolled) // h23 +0xc08 points IText 123 at the blank slot
            Text(ops, b, s.NameAt, s.NamePen, member.Name, title: true);
        Text(ops, b, s.LevelAt, s.LevelPen, level);
        for (var i = 0; i < rows.Length; i++)
            Text(ops, b, (s.Rows[i].X, s.Rows[i].Y), s.RowPen, rows[i]);
        // h29 +0xc18: no rune row unless rune 0, 2 or 3 is held (DOS's rule too); then each rune held by name.
        var runes = member.Phantasie1.Runes;
        bool Has(int i) => i < runes.Length && runes[i] != 0;
        if (Has(0) || Has(2) || Has(3))
            for (var i = 0; i < s.Runes.Count; i++)
                if (i == 0 || Has(i - 1))
                    Text(ops, b, (s.Runes[i].X, s.RunesY), s.RunesPen, UiText.S(s.Runes[i].Text));
        var hits = new List<RequesterHit>();
        var buttons = rolled ? s.RolledButtons : s.Buttons;
        for (var i = 0; i < buttons.Count; i++)
        {
            var g = buttons[i];
            var box = new Box(b.X + g.Rect.X, b.Y + g.Rect.Y, g.Rect.W, g.Rect.H);
            Button(ops, box, g.FacePen, g.BorderPen, UiText.S(g.Label), g.TextX, g.TextY, g.TextPen);
            var key = rolled ? (i == 0 ? Keys.N : Keys.Y) : i == buttons.Count - 1 ? Keys.Escape : Keys.D1 + i;
            hits.Add(new RequesterHit(box, key, i));
        }
        return new RequesterScene(s.Palette, ops, hits);
    }

    /// <summary>The sheet's views the pack draws, by the remaster's page index (1 equipment, 2 skills, 3 potions).</summary>
    public static string? PageName(int page) => page switch { 1 => "equipment", 2 => "skills", 3 => "potions", _ => null };

    /// <summary>
    /// s156: the sheet's view <paramref name="page"/> (1 EQUIPMENT = requester 10, 2 SKILLS = 9, 3 POTIONS = 11; h29
    /// <c>+0xee2</c> / <c>+0xd46</c> / <c>+0xfb6</c>) for <paramref name="member"/>, or null (SPELLS, or a pack without it).
    /// EXIT sends Esc (back to the sheet).
    /// </summary>
    public static RequesterScene? SheetPageFor(SheetRequester s, int page, Character member, GameDataRepository data)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(member);
        if (PageName(page) is not { } name || !s.Pages.TryGetValue(name, out var p))
            return null;
        var rows = page switch
        {
            1 => InspectDialog.EquipmentLines(member, data),
            2 => SkillRows(member),
            _ => PotionRows(member),
        };
        var ops = new List<RequesterOp>();
        Frame(ops, p.Box, p.FillPen, p.Borders);
        for (var i = 0; i < rows.Count && i < p.Rows.Count; i++)
            if (rows[i].Length > 0)
                Text(ops, p.Box, (p.Rows[i].X, p.Rows[i].Y), p.Rows[i].Pen, rows[i]);
        var g = p.Exit;
        var box = new Box(p.Box.X + g.Rect.X, p.Box.Y + g.Rect.Y, g.Rect.W, g.Rect.H);
        Button(ops, box, g.FacePen, g.BorderPen, UiText.S(g.Label), g.TextX, g.TextY, g.TextPen);
        return new RequesterScene(s.Palette, ops, [new RequesterHit(box, Keys.Escape, 0)]);
    }

    /// <summary>
    /// s158: the sheet's SPELLS view, the fight's requester 12 "SPELLS AVAILABLE:" (h29 <c>+0xe90</c> → h23 <c>+0x570(record
    /// +0x60, 1)</c>: every known spell in slot order, none ghosted, browse only; live s158 over the map,
    /// <c>p1-amiga-map-sheet-spells</c>): the box and title, the list's frame and fill, a page of <paramref name="spells"/>
    /// from <paramref name="first"/>, the two scroll gadgets (ghosted when they cannot move: Up / Down) and EXIT (Esc); a row
    /// sends its digit. <paramref name="l"/> holds the screen pens (the combat field's, through h69 <c>+0x19c</c>); the scene
    /// holds the stored pens (<paramref name="unmap"/>, h69 <c>+0x1ac</c>), as the sheet's others: the town draws them as they
    /// are, the map through <see cref="Remap"/>.
    /// </summary>
    public static RequesterScene SheetSpells(CombatFieldList l, IReadOnlyList<string> palette, IReadOnlyList<int> unmap,
        IReadOnlyList<string> spells, int first)
    {
        ArgumentNullException.ThrowIfNull(l);
        ArgumentNullException.ThrowIfNull(spells);
        var ops = new List<RequesterOp>();
        int P(int pen) => unmap.Count == 16 ? unmap[pen] : pen;
        Frame(ops, l.Box, P(l.FillPen), [new RequesterOutline(new Box(0, 0, l.Box.W, l.Box.H), P(l.BorderPen)),
            new RequesterOutline(new Box(1, 1, l.Box.W - 2, l.Box.H - 2), P(l.BorderPen))]);
        ops.Add(new RequesterOp(RequesterOpKind.Text, new Box(l.TitleX, l.TitleY, 1, 1), P(l.TitlePen), UiText.S("p1field.spells_available")));
        if (l.ListFrame is { } frame)
            ops.Add(new RequesterOp(RequesterOpKind.Outline, frame, P(l.ListFramePen)));
        if (l.ListFill is { } fill)
            ops.Add(new RequesterOp(RequesterOpKind.Fill, fill, P(l.ListFillPen)));
        var hits = new List<RequesterHit>();
        for (var k = 0; k < l.Rows && first + k < spells.Count; k++)
        {
            var row = l.Row(k);
            var name = spells[first + k];
            ops.Add(new RequesterOp(RequesterOpKind.Text, new Box(row.X + 1, row.Y + 1, 1, 1), P(l.RowPen), name.Length > 29 ? name[..29] : name));
            hits.Add(new RequesterHit(row, Keys.D1 + k, k));
        }
        void Scroll(Box? at, IReadOnlyList<string> glyph, bool live, Keys key)
        {
            if (at is not { } b)
                return;
            Button(ops, b, P(l.ButtonFillPen), P(l.ScrollBorderPen), "", 0, 0, P(l.ButtonTextPen));
            for (var y = 0; y < glyph.Count; y++)
                for (var x = 0; x < glyph[y].Length; x++)
                    if (glyph[y][x] == '#')
                        ops.Add(new RequesterOp(RequesterOpKind.Fill, new Box(b.X + l.GlyphDx + x, b.Y + l.GlyphDy + y, 1, 1), P(l.ButtonTextPen)));
            if (live)
                hits.Add(new RequesterHit(b, key, hits.Count));
            else
                foreach (var (x, y) in UiOverlays.P1GhostDots(b, l.GhostStep))
                    ops.Add(new RequesterOp(RequesterOpKind.Fill, new Box(x, y, 1, 1), P(l.GhostPen)));
        }
        Scroll(l.ScrollUp, l.UpGlyph, first > 0, Keys.Up);
        Scroll(l.ScrollDown, l.DownGlyph, spells.Count > first + l.Rows, Keys.Down);
        Button(ops, l.Exit, P(l.ButtonFillPen), P(l.ExitBorderPen), UiText.S(l.ExitLabel), l.ExitTextDx, l.ExitTextDy, P(l.ButtonTextPen));
        hits.Add(new RequesterHit(l.Exit, Keys.Escape, hits.Count));
        return new RequesterScene(palette, ops, hits);
    }

    /// <summary>Requester 9's rows (h29 <c>+0xd46</c>): ATTACK, PARRY, SWIM, LISTEN, then SPOT TRAP, DISARM TRAP, FIND ITEM, PICK LOCK.</summary>
    public static IReadOnlyList<string> SkillRows(Character m)
    {
        var k = m.Skills;
        return
        [
            UiText.F("p1field.sheet.attack", k.Attack), UiText.F("p1field.sheet.parry", k.Parry),
            UiText.F("p1field.sheet.swim", k.Swim), UiText.F("p1field.sheet.listen", k.Listen),
            UiText.F("p1field.sheet.spot_trap", k.SpotTrap), UiText.F("p1field.sheet.disarm_trap", k.DisarmTrap),
            UiText.F("p1field.sheet.find_item", k.FindItem), UiText.F("p1field.sheet.pick_lock", k.PickLock),
        ];
    }

    /// <summary>
    /// Requester 11's rows (h29 <c>+0xfb6</c> / <c>+0x842</c>): the potion and scroll / treasure counts, "RINGS: " with a ring's
    /// letter at its place (none: the row blank), "THE WAND OF NIKADEMUS" when item 150 is carried (else blank).
    /// </summary>
    public static IReadOnlyList<string> PotionRows(Character m)
    {
        var buf = Enumerable.Repeat(' ', 10).ToArray();
        var ring = false;
        foreach (var id in m.InventoryItemIds.Where(id => id is >= 141 and <= 150))
        {
            buf[id - 141] = (char)('A' + id - 141);
            ring = true;
        }
        return
        [
            UiText.F("p1field.sheet.potions_row", m.HealingPotions, m.MagicPotions),
            UiText.F("p1field.sheet.scrolls_row", m.ScrollCount, m.Treasures),
            ring ? UiText.F("p1field.sheet.rings_row", new string(buf, 0, 9)).TrimEnd() : "",
            buf[9] != ' ' ? UiText.S("p1field.sheet.wand_row") : "",
        ];
    }

    /// <summary>
    /// <paramref name="scene"/> with every pen put through <paramref name="table"/> and drawn on <paramref name="palette"/>:
    /// h77 <c>+0x112e</c>'s remap (h69 <c>+0x19c</c>) for a requester the map shows with its stored pens moved (the sheet over
    /// the map), or its inverse for one the town shows stored (requester 0 in the town).
    /// </summary>
    public static RequesterScene Remap(RequesterScene scene, IReadOnlyList<int> table, IReadOnlyList<string> palette)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(palette);
        return new RequesterScene(palette, [.. scene.Ops.Select(o => o with { Pen = table[o.Pen] })], scene.Buttons);
    }

    /// <summary>Every UI string id the pack's requesters name (texts, titles, labels, sheet rows).</summary>
    public static IEnumerable<string> StringKeys(MapRequestersLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        IEnumerable<string> ids = [.. layout.Menus.Values, .. layout.Oks.Values, .. layout.Kinds.Values, .. layout.Town.Values];
        if (layout.BlackKnights is { } k)
            ids = ids.Append(k.Title).Concat(k.Buttons.Select(b => b.Label));
        if (layout.Sheet is { } s)
            ids = ids.Concat(s.Rows.Select(r => r.Text)).Concat(s.Runes.Select(r => r.Text)).Concat(s.Buttons.Select(b => b.Label))
                .Concat(s.RolledButtons.Select(b => b.Label)).Concat(s.Pages.Values.Select(p => p.Exit.Label))
                .Concat(["p1field.sheet.attack", "p1field.sheet.parry", "p1field.sheet.swim", "p1field.sheet.listen",
                    "p1field.sheet.spot_trap", "p1field.sheet.disarm_trap", "p1field.sheet.find_item", "p1field.sheet.pick_lock",
                    "p1field.sheet.potions_row", "p1field.sheet.scrolls_row", "p1field.sheet.rings_row", "p1field.sheet.wand_row"]);
        return ids;
    }

    private static void Text(List<RequesterOp> ops, Box b, (int X, int Y) at, int pen, string text, bool title = false) =>
        ops.Add(new RequesterOp(RequesterOpKind.Text, new Box(b.X + at.X, b.Y + at.Y, 1, 1), pen, text, title));

    private static void Frame(List<RequesterOp> ops, Box b, int fill, IReadOnlyList<RequesterOutline> borders, int grow = 0)
    {
        ops.Add(new RequesterOp(RequesterOpKind.Fill, b, fill));
        foreach (var o in borders) // grow: a widened requester's outlines widen with it
            ops.Add(new RequesterOp(RequesterOpKind.Outline, new Box(b.X + o.Rect.X, b.Y + o.Rect.Y, o.Rect.W + grow, o.Rect.H), o.Pen));
    }

    /// <summary>The game's button render: Borders 0 / 1 at (−2, −2) and (−1, −1), the face, the label (JAM1).</summary>
    private static void Button(List<RequesterOp> ops, Box r, int face, int border, string label, int dx, int dy, int ink)
    {
        ops.Add(new RequesterOp(RequesterOpKind.Outline, new Box(r.X - 2, r.Y - 2, r.W + 4, r.H + 4), border));
        ops.Add(new RequesterOp(RequesterOpKind.Outline, new Box(r.X - 1, r.Y - 1, r.W + 2, r.H + 2), border));
        ops.Add(new RequesterOp(RequesterOpKind.Fill, r, face));
        ops.Add(new RequesterOp(RequesterOpKind.Text, new Box(r.X + dx, r.Y + dy, 1, 1), ink, label));
    }
}
