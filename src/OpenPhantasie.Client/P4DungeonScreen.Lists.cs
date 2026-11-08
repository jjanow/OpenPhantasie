using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The lists the original draws as text buttons in its Command window (s151, FORMAT.md 6.9 "Lists in window 34"): the
/// spell list (<c>_getmagic</c>: its own "Spell List" window at the panel's place; the Magic Training Hall's pages), the
/// Transportation towns under the "Transport" panel's icon (<c>_cast_47</c>), and creation's race / Random Creature / class
/// / Hall lists (<c>_racemenu</c>, <c>_rracemenu</c>, <c>_classmenu</c>, <c>_set_fame_lst</c>, in window 34 as the
/// panel left it). Rows come from the Core's <see cref="P4ListWindow"/>, places from the pack's <see cref="CommandList"/>.
/// A click on a row presses the remaster's key for its line (a digit, or past 9 the cursor there and Return); the text
/// column keeps its lines and keys.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The string id of <c>_mkicon</c>'s default title "Command" (0x401D2), kept by a list drawn in window 34.</summary>
    public const string CommandTitleId = "p4x.0x401d2";

    /// <summary>Pack pixels kept between a list's text and the next column when a long name would touch it.</summary>
    public const int ListColumnGap = 4;

    /// <summary>
    /// The Core list up at this prompt, or null (none, no pack layout for it). In a fight only the combat spell list
    /// (s152, <c>_getmagic(member, 0)</c>), drawn right of the field at the panel's place (s160, <see cref="FightListUp"/>).
    /// </summary>
    private P4ListWindow? CoreList => !Game.LoopBuilt || DoorOpen || MysticBusy ? null
        : Game.Battle is not null ? BattleSpellList
        : MenuOpen ? (M!.Prompt == P4MenuPrompt.Pick ? M.ListWindow : null)
        : _system is null && Game.Place == P4Place.Town && W.Prompt == P4TownPrompt.Pick ? W.ListWindow : null;

    /// <summary>
    /// True while the combat spell list is open (s160, user s159): it takes the Command panel's place right of the field
    /// (the original's x 496) and the text column narrows to the field's side until it closes.
    /// </summary>
    public bool FightListUp => FightSpellListOpen && CurrentList is not null;

    /// <summary>The combat spell list is open (a member's orders, Cast chosen), whatever the art pack.</summary>
    public bool FightSpellListOpen => BattleSpellList is not null;

    /// <summary>
    /// The layout row where a list's frame (its title bar's top line) starts (s160). A list in its own window (the spell
    /// lists: <c>_wopen(34, 496, 32, 136, 360, 1, "Spell List")</c>) sits where the original opens it: the window's area at
    /// screen row 32, so its 21-row bar from row 11 (the screen is the original's 640 × 400, one pack pixel each); the
    /// remaster's Command panel and pictures sit higher than the original's, so this is the original's place on screen,
    /// not its place relative to them. Kept on screen when a window would run past the bottom. Lists drawn in window 34
    /// under "Command" or under a panel keep the panel's top (4).
    /// </summary>
    public static float ListFrameTop(CommandList layout, float k)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Panel is not null || layout.Window is not { } w)
            return 4;
        var top = (w.Y - PanelTitleRows) * k;
        return Math.Max(0, Math.Min(top, 200 - (PanelTitleRows + w.Height + 2) * k));
    }

    /// <summary>
    /// The Command strip's lowest layout row (s157's key-help layout): the panel's (<paramref name="panelHeight"/> layout
    /// pixels from row 4) or a list window's when one is up and lower (<see cref="ListFrameTop"/>, its bar, area
    /// <paramref name="windowHeight"/> high unless the list has its own window, frame and shadow).
    /// </summary>
    public static int StripBottom(float panelHeight, CommandList? list, int windowHeight, float k)
    {
        var bottom = 4 + (int)Math.Ceiling(panelHeight);
        if (list is { Panel: null } l)
            bottom = Math.Max(bottom,
                (int)Math.Ceiling(ListFrameTop(l, k) + (PanelTitleRows + (l.Window?.Height ?? windowHeight) + 2) * k));
        return bottom;
    }

    /// <summary>The list up now with its pack layout, or null.</summary>
    private (P4ListWindow List, CommandList Layout)? CurrentList =>
        CoreList is { } l && Assets?.Pack?.CommandPanels?.Lists.GetValueOrDefault(l.Name) is { } layout ? (l, layout) : null;

    /// <summary>
    /// The key a click on a row (or head button) standing for choice <paramref name="choice"/> presses, and the list line
    /// when that line has no digit key: −1 = Space (back), 0–8 the digit, past 8 Return on the cursor line.
    /// </summary>
    public static (Keys Key, int? Line) ListPress(int choice) =>
        choice < 0 ? (Keys.Space, null) : choice < 9 ? (Keys.D1 + choice, null) : (Keys.Enter, choice);

    /// <summary>
    /// Where the second field of a row goes (s151, the Hall list's race at x 96): its own x, moved right just enough to
    /// clear a name that would run into it (English and long names; the user's rule: nothing overlaps or is cut) `[~]`.
    /// </summary>
    public static int ListExtraX(CommandListRows rows, int textWidth)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var x = rows.ExtraX ?? 0;
        return Math.Max(x, rows.TextX + textWidth + ListColumnGap);
    }

    /// <summary>
    /// The window width every list in <paramref name="set"/> needs (pack pixels) for the texts it may show
    /// (<paramref name="texts"/>, measured by <paramref name="measure"/>): head lines and rows from their x, the second
    /// field after the widest name, centred lines on their centre; 0 without lists. <see cref="PanelWidth"/> takes the
    /// larger of this and the panels' width, so the strip keeps one width (English spell names run past 136).
    /// </summary>
    public static int ListsWidth(CommandPanelSet set, Func<string, IEnumerable<string>> texts, Func<string, int> measure,
        Func<string, string, string> label)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(label);
        var widest = 0;
        foreach (var l in set.Lists.Values)
        {
            foreach (var h in l.Head)
                widest = Math.Max(widest, h.X + measure(label(h.Label, h.Text)) + PanelMargin);
            foreach (var c in l.Centred)
                widest = Math.Max(widest, c.X + (measure(label(c.Label, c.Text)) + 1) / 2 + PanelMargin);
            var r = l.Rows;
            var name = texts(l.Name).Select(measure).DefaultIfEmpty(0).Max();
            widest = Math.Max(widest, r.TextX + name + PanelMargin);
            if (r.ExtraX is not null)
                widest = Math.Max(widest, ListExtraX(r, name) + measure(new string('M', r.ExtraChars)) + PanelMargin);
        }
        return widest;
    }

    /// <summary>The texts list <paramref name="name"/> may show in this game (for <see cref="ListsWidth"/>).</summary>
    private IEnumerable<string> ListTexts(string name)
    {
        var names = Game.Services?.Names;
        return name switch
        {
            "_getmagic" or "_getmagic.8" or P4Battle.SpellListName => names?.Spells ?? [],
            "_cast_47" => Game.Tables.Service.TransportGroups.SelectMany(g => g).Distinct()
                .Select(n => GameText.S(Game.Tables.Service.TownNameIds[n])),
            "_racemenu" or "_rracemenu" => (names?.Races ?? []).Concat([GameText.S("p4x.0x42bc0"), GameText.S("p4x.0x42bd0")]),
            "_classmenu" => names?.Classes ?? [],
            "_set_fame_lst" => (Game.Services?.Hall ?? []).Select(h => h.Name),
            _ => [],
        };
    }

    /// <summary>
    /// Draws the list up now (s151) into the Command strip whose window's top-left (title bar) is <paramref name="at"/>:
    /// its own titled window (the spell list), or window 34 with the "Command" title, or under the panel drawn there
    /// already (Transport); then the rule, the head buttons, centred lines and the rows in font 0, colour 0. Rows and
    /// head lines with a choice are clicks.
    /// </summary>
    private void DrawList(SpriteBatch batch, Texture2D pixel, CommandPanelSet set, P4ListWindow list, CommandList layout,
        Vector2 at, float k)
    {
        Color Pal(int i, Color fallback) => Assets?.PaletteColour("pkh", i) ?? fallback;
        var black = Pal(0, Color.Black);
        var w = PanelWidth(set, k);
        void Box(Vector2 origin, float x, float y, float bw, float bh, Color c) =>
            batch.Draw(pixel, origin + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(bw * k, bh * k),
                SpriteEffects.None, 0f);
        if (layout.Panel is null)
        {
            var height = layout.Window?.Height ?? set.Window.Height;
            var title = layout.Window is null ? PanelText(CommandTitleId, "Command") : PanelText(layout.Title, layout.TitleText);
            DrawTitledFrame(batch, pixel, at, w, height, title, k);
        }
        var body = at + new Vector2(0, PanelTitleRows * k);
        // layout.Clear (_splist's _wfbox, colour 14) is the window's own grey here: nothing to draw.
        if (layout.Rule is { } rule)
        {
            // _wbox: a 1-pixel outline (top, bottom, both ends), here 3 rows high: a groove under the head. The remaster's
            // frame takes the window's first and last column (as DrawPanel's), so the rule runs between them.
            var x0 = Math.Max(rule.X, 1);
            var rw = Math.Min(rule.X + Math.Max(rule.Width, w - rule.X), w - 1) - x0;
            var c = Pal(rule.Colour, Color.DimGray);
            Box(body, x0, rule.Y, rw, 1, c);
            Box(body, x0, rule.Y + rule.Height - 1, rw, 1, c);
            Box(body, x0, rule.Y, 1, rule.Height, c);
            Box(body, x0 + rw - 1, rule.Y, 1, rule.Height, c);
        }
        var font = FontSources.ForRole(Assets?.Pack, MysticFontRole); // P4.X font 0 (the lists' _wfinit(0))
        int Measure(string s) => font is not null ? PixelFont.Measure(s, font) : (int)Math.Ceiling(PixelFont.Measure(s) / k);
        void Text(float x, float y, string s)
        {
            if (font is not null)
                PixelFont.DrawScaled(batch, pixel, body + new Vector2(x * k, y * k), s, black, k, font);
            else
                PixelFont.Draw(batch, pixel, (int)Math.Round(body.X + x * k), (int)Math.Round(body.Y + y * k), s, black);
        }
        var r = layout.Rows;
        var buttonWidth = Math.Max(r.ButtonWidth, w - r.ButtonX); // a strip widened for English keeps whole-row buttons
        void Button(int y, int choice, int index)
        {
            var (key, line) = ListPress(choice);
            UiOverlays.Hits?.Add(new HitRegion(new Rectangle((int)(body.X + r.ButtonX * k), (int)(body.Y + y * k),
                (int)Math.Ceiling(buttonWidth * k), (int)Math.Ceiling(r.ButtonHeight * k)),
                new P4PanelIconTag(list.Name, index, line)) { Click = [key] });
        }
        for (var i = 0; i < layout.Head.Count; i++)
        {
            var h = layout.Head[i];
            Text(h.X, h.Y, PanelText(h.Label, h.Text));
            if (i < list.HeadChoices.Count)
                Button(h.Y, list.HeadChoices[i], -1 - i);
        }
        foreach (var c in layout.Centred)
        {
            var s = PanelText(c.Label, c.Text);
            Text(c.X - (Measure(s) >> 1), c.Y, s); // _wprcen: x − (_fsize >> 1)
        }
        var widest = list.Rows.Select(row => row.Text is { } t ? Measure(t) : 0).DefaultIfEmpty(0).Max();
        var extraX = ListExtraX(r, widest);
        for (var i = 0; i < list.Rows.Count; i++)
        {
            var row = list.Rows[i];
            var y = r.YOf(i);
            Text(r.KeyX, y, r.KeyOf(i).ToString());
            if (row.Text is { } t)
                Text(r.TextX, y, t);
            if (row.Extra is { } e && r.ExtraX is not null)
                Text(extraX, y, r.ExtraChars > 0 && e.Length > r.ExtraChars ? e[..r.ExtraChars] : e);
            if (row.Choice is int choice)
                Button(y, choice, i);
        }
    }

    /// <summary>
    /// A titled window's frame (s141, <c>_wwind</c> / <c>_wactive</c>, FORMAT.md 6.9 "Window frames") with its top-left
    /// (the frame's, one pixel left of the window and <see cref="PanelTitleRows"/> above it) at <paramref name="at"/>:
    /// black frame round the bar and the window, the bar's 18 grey rows, the dark grey (colour 1) line and the black line,
    /// the window's grey (titled: 14), the 1-pixel black shadow right (from the bar's second row) and below.
    /// <paramref name="w"/> is the window's width + 2 (the frame's), <paramref name="height"/> the window's own. The title
    /// is <c>_wttl</c>'s (s151 [C]): font 0, style 0, colour 0, centred on the window (x − (<c>_fsize</c> &gt;&gt; 1)), at
    /// y −18 of the window (3 rows under the bar's black top line). s158: shared by the Command panel, the lists' own
    /// windows and the building windows (the training hall, the shipwright).
    /// </summary>
    private void DrawTitledFrame(SpriteBatch batch, Texture2D pixel, Vector2 at, int w, int height, string title, float k)
    {
        Color Pal(int i, Color fallback) => Assets?.PaletteColour("pkh", i) ?? fallback;
        var black = Pal(0, Color.Black);
        var grey = Pal(PanelBackColour, Color.LightGray);
        void Box(float x, float y, float bw, float bh, Color c) =>
            batch.Draw(pixel, at + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(bw * k, bh * k),
                SpriteEffects.None, 0f);
        Box(0, 0, w, PanelTitleRows + height + 1, black);
        Box(1, 1, w - 2, PanelTitleRows - 3, grey);
        Box(1, PanelTitleRows - 2, w - 2, 1, Pal(PanelBarLineColour, Color.DimGray));
        Box(1, PanelTitleRows, w - 2, height, grey);
        Box(w, 1, 1, PanelTitleRows - 1, black);
        Box(w, PanelTitleRows + 1, 1, height + 1, black);
        Box(2, PanelTitleRows + height + 1, w - 1, 1, black);
        var titleFont = FontSources.ForRole(Assets?.Pack, MysticFontRole);
        var titleY = PanelTitleRows + (Extras?.Title?.Y ?? -18);
        var titleInk = Pal(Extras?.Title?.Colour ?? 0, Color.Black);
        if (titleFont is not null)
            PixelFont.DrawScaled(batch, pixel, at + new Vector2((w / 2 - (PixelFont.Measure(title, titleFont) >> 1)) * k,
                titleY * k), title, titleInk, k, titleFont);
        else
            PixelFont.Draw(batch, pixel, (int)Math.Round(at.X + (w * k - PixelFont.Measure(title)) / 2),
                (int)Math.Round(at.Y + 1), title, black);
    }
}
