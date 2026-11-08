using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The original's Command panel (s136; FORMAT.md 6.9 "Command panel", <c>_mkicon</c> / <c>_seticon</c>): the pack's icon
/// buttons in a window right of everything, for the prompts whose panel is read: the street (<c>_townmenu</c>: the six
/// F / C / T / D / U / S icons, or S alone without a party), outdoors (<c>_outdoormenu</c>) and on a location map
/// (<c>_dsetup</c>); s137 / s138 the building screens, creation, the SPACE waits, the Okay / Cancel questions and the
/// System screen. A click on an icon presses its hot key (or the line digit it stands for); the keys and the digit picks
/// work as before. Screens without one of the 40 panels keep their text lines `[~]`.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The pack's font role for the icon labels (P4.X font 1: <c>_seticon</c>'s <c>_wfinit(1)</c>).</summary>
    public const string PanelLabelFontRole = "p4.panel.label";

    /// <summary>
    /// Rows of <c>_wopen</c>'s title bar above the window's area (seen on XEiJ s110: a black line, 18 grey rows with the
    /// title, a grey line, a black line).
    /// </summary>
    public const int PanelTitleRows = 21;

    /// <summary>Palette colours: the window's grey (14), the hot-key letter (2), the rest of the label (0).</summary>
    public const int PanelBackColour = 14, PanelKeyColour = 2, PanelTextColour = 0;

    /// <summary>
    /// The panel the original shows now (a <see cref="CommandPanelSet.Panels"/> key), or null. Town: the street's menu, then
    /// the building screen's own (<see cref="P4TownLoop.IconMenu"/>), else <c>_spc_wait</c> at a SPACE wait and
    /// <c>_confirm</c> at an Okay / Cancel question (s138: P4.X calls them from the inn, the bank, the Mystic, the
    /// shipwright, the town entry, the Hall, creation and the outdoor sites); outdoors the action prompt, waits and
    /// questions; a location map's action prompt (s151: its waits and typed prompts keep it, inert: <see cref="PanelInert"/>); the System
    /// screen's. s140: the party menus' (<see cref="P4Menus.IconMenu"/>: <c>_inspect</c>, the profile / Party info icon
    /// rows, <c>_ocast</c>, <c>_cast_47</c>, <c>_setident</c>, <c>_setuse</c>; their waits <c>_spc_wait</c>, questions
    /// <c>_confirm</c>), the shipwright's <c>_noyes</c> box, and in a fight only the found-item question's <c>_findit</c>.
    /// </summary>
    public string? PanelName => !Game.LoopBuilt || DoorOpen || MysticBusy || WelcomeUp ? null
        : Game.Battle is { } fight ? (fight.Prompt == P4BattlePrompt.Treasure ? "_findit" : null)
        : MenuOpen ? M!.IconMenu is { } m ? (m.Length == 0 ? null : m)
            : DefaultPanel(M.Prompt == P4MenuPrompt.Wait, M.Prompt == P4MenuPrompt.Confirm)
        : _system is { } page ? SystemPanel(page)
        : InertPanel is { } inert ? inert // s151: _ewait / typed prompts keep _dsetup's panel, _numin the Bank's
        : Game.Place switch
        {
            P4Place.Town when W.Prompt == P4TownPrompt.Menu && W.Scene == P4TownScene.Street =>
                Game.Party.Members.Count == 0 ? "_townmenu.no_party" : "_townmenu",
            P4Place.Town when W.IconMenu is { } menu => menu.Length == 0 ? null : menu,
            P4Place.Town => DefaultPanel(W.Prompt == P4TownPrompt.Wait, W.Prompt == P4TownPrompt.Confirm),
            P4Place.Outdoors when O.Prompt == P4OutdoorPrompt.Action => "_outdoormenu",
            P4Place.Outdoors => DefaultPanel(O.Prompt == P4OutdoorPrompt.Wait, O.Prompt == P4OutdoorPrompt.Confirm),
            P4Place.Dungeon when D.Prompt == P4DungeonPrompt.Action => "_dsetup",
            _ => null,
        };

    /// <summary><c>_spc_wait</c>'s SPACE panel at a wait, <c>_confirm</c>'s Okay / Cansel at a question, else none.</summary>
    private static string? DefaultPanel(bool wait, bool confirm) => wait ? "_spc_wait" : confirm ? "_confirm" : null;

    /// <summary>
    /// True where the panel's strip is kept beside the text column (the whole town, outdoors, a location map, outside
    /// fights), whether or not a panel is up at this prompt: the column keeps one width from step to step. Not in a fight:
    /// the found-item question's panel lies over the field (<see cref="PanelOverField"/>); the combat spell list takes the
    /// strip only while it is open (<see cref="FightListUp"/>).
    /// </summary>
    public bool PanelPlace => Game.LoopBuilt && Game.Battle is null
        && Game.Place is P4Place.Town or P4Place.Outdoors or P4Place.Dungeon;

    /// <summary>
    /// The found-item question (s140, <c>_findit</c>): its panel covers the field's right side (user s142, option a; the
    /// original puts window 34 right of the field), so the text column keeps its width.
    /// </summary>
    public bool PanelOverField => Game.LoopBuilt && Game.Battle is { Prompt: P4BattlePrompt.Treasure };

    /// <summary>
    /// The found-item message window's x and width (field pixels) with the panel over the field from
    /// <paramref name="panelLeft"/> (null: no panel): the original's (32, 272) when it clears the panel; else it ends 4
    /// pixels left of the panel (room for its shadow) and starts no further left than x 8.
    /// </summary>
    public static (int X, int Width) FindItMessageSpan(int? panelLeft)
    {
        if (panelLeft is not int left || left >= MessageX + MessageWidth + 4)
            return (MessageX, MessageWidth);
        var right = left - 4;
        var x = Math.Max(8, Math.Min(MessageX, right - MessageWidth));
        return (x, right - x);
    }

    /// <summary>The found-item panel's left edge in field pixels, or null when it is not over the field.</summary>
    private int? FindItPanelLeft(float k) =>
        PanelOverField && CurrentPanel is { Row: null } && Assets?.Pack?.CommandPanels is { } set
            ? FieldWidth - (PanelWidth(set, k) + 1) : null;

    /// <summary>The panel or icon row now up from the pack (null without one, or when no panel is shown).</summary>
    private CommandPanel? CurrentPanel =>
        PanelName is { } name && Assets?.Pack?.CommandPanels?.Find(name) is { } p ? p : null;

    /// <summary>The name of the shipwright's YES / NO box (<c>_noyes</c>) in <see cref="PanelName"/>.</summary>
    public const string NoYesName = "_noyes";

    /// <summary>
    /// Panels over a numbered pick list of fixed lines (s137, s138): the icon keys in the order of the list's lines (an
    /// icon not in the list presses its own key, e.g. SPACE). An icon presses its line's digit (the list's own keys stay).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> PickPanels = new Dictionary<string, string[]>
    {
        ["_bank"] = ["D", "W", "L"],
        ["_guildmenu"] = ["G", "T", "M", "L"], // the pack's panel has L and G only; T and M stay text lines
        ["_hall"] = ["H", "P", "T", "L"],
        ["_mlistmenu"] = ["P", "N", "F", "D", "R"], // the roster: SPACE and N only; P, F, D, R stay text lines
        ["_makemenu"] = ["E", "Y", "N"],
        ["_make1menu"] = ["N", "H"],
        ["_make5menu"] = ["S", "D", "H"],
    };

    /// <summary>
    /// The list line (0-based) an icon of a pick panel presses now: a fixed one (<see cref="PickPanels"/>), or one that
    /// follows the shop page or the party: the Armory's Leave (after the page and Next), Share out's P (the pack) and S
    /// (sell) after the members, Trade's P, S, L after the carried slots and the members. Null for the other icons.
    /// </summary>
    private int? PanelLineNow(string panel, string key)
    {
        var members = Game.Party.Members.Count;
        return (panel, key) switch
        {
            ("_armory", "L") => W.ShopPage.Count + 1,
            // s140: the profile pages' N (the page's last line), U's "L:Done" (the list's last line).
            ("_detailmenu" or "_page3", "N") => 0,
            ("_p_info3menu", "N") => 5,
            ("_setuse", "L") => P4Member.CarriedSlots + members,
            ("_distribute", "P") => members,
            ("_distribute", "S") => members + 1,
            ("_trade", "P") => P4Member.CarriedSlots + members,
            ("_trade", "S") => P4Member.CarriedSlots + members + 1,
            ("_trade", "L") => P4Member.CarriedSlots + members + 2,
            // T5: the III transfer list's O (Okay) and X (Abort) are the two lines after the characters.
            ("_show_gm", "O") => W.Choices.Count - 2,
            ("_show_gm", "X") => W.Choices.Count - 1,
            _ => PickPanels.TryGetValue(panel, out var order) && Array.IndexOf(order, key) is >= 0 and var n ? n : null,
        };
    }

    /// <summary>
    /// What a click on <paramref name="icon"/> does now: the key it presses, and the list line when that line has no digit
    /// key (past 9: the click puts the cursor there and presses Return).
    /// </summary>
    public (Keys Key, int? Line) PanelPress(string panel, CommandPanelIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return PanelLineNow(panel, icon.Key) is int line
            ? line < 9 ? (Keys.D1 + line, null) : (Keys.Enter, line)
            : (PanelClickKey(panel, icon), null);
    }

    /// <summary>
    /// The key a click on <paramref name="icon"/> of <paramref name="panel"/> presses: a fixed pick panel's line digit; the
    /// remaster's key where it does not read the icon's letter (Okay / Cansel = Y / N, the Elemental pick's SPACE = Return
    /// on the cursor line, End training / the Hall list's Done = SPACE, the history's N = SPACE); else the icon's own key.
    /// </summary>
    public static Keys PanelClickKey(string panel, CommandPanelIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        if (PickPanels.TryGetValue(panel, out var order) && Array.IndexOf(order, icon.Key) is >= 0 and var n)
            return Keys.D1 + n;
        return (panel, icon.Key) switch
        {
            ("_party", "Space") => Keys.Enter,
            ("_trang" or "_mage", "E") => Keys.Space,
            ("_hprofmenu", "L") => Keys.Space,
            ("_hhist", "N") => Keys.Space,
            ("_confirm" or "_htran" or "_acend" or "_acend.2" or "_findit", "O") => Keys.Y,
            ("_confirm" or "_htran" or "_acend" or "_acend.2" or "_findit", "C") => Keys.N,
            _ => PanelKey(icon),
        };
    }

    /// <summary>The key an icon presses: its letter, or Space.</summary>
    public static Keys PanelKey(CommandPanelIcon icon) =>
        icon.Key == "Space" ? Keys.Space
        : icon.Key is [var c] && char.IsAsciiLetterUpper(c) ? Keys.A + (c - 'A')
        : Keys.None;

    /// <summary>
    /// <c>_seticon</c>'s label start: x + 16 − (<c>_fsize</c>(label) &gt;&gt; 1), y + 32 (pack pixels in the window), font 1
    /// with its plain advances.
    /// </summary>
    public static (int X, int Y) PanelLabelAt(int iconX, int iconY, int iconSize, int labelWidth) =>
        (iconX + iconSize / 2 - (labelWidth >> 1), iconY + iconSize);

    /// <summary>
    /// <see cref="PanelLabelAt(int, int, int, int)"/> kept inside a window <paramref name="windowWidth"/> wide (s138): a
    /// label that would cross the frame moves in just enough (English labels run longer than P4.X's; the user's rule: no
    /// cropping). <see cref="FittedPanelWidth"/> makes the window wide enough for the longest label.
    /// </summary>
    public static (int X, int Y) PanelLabelAt(int iconX, int iconY, int iconSize, int labelWidth, int windowWidth)
    {
        var (x, y) = PanelLabelAt(iconX, iconY, iconSize, labelWidth);
        return (Math.Max(PanelMargin, Math.Min(x, windowWidth - PanelMargin - labelWidth)), y);
    }

    /// <summary>Pack pixels between the window's edge and a label or the title: the 1-pixel frame and one pixel of grey.</summary>
    public const int PanelMargin = 2;

    /// <summary>
    /// The window's width for every panel of <paramref name="set"/> (pack pixels): the original's 136, or wider when a
    /// label or title (as <paramref name="labelWidth"/> / <paramref name="titleWidth"/> measure them in the current
    /// language) would not fit inside the frame. One width for all panels, so the text column beside them does not change
    /// width from prompt to prompt (s138; in English "H:A person from the Hall" is 150 pixels).
    /// </summary>
    public static int FittedPanelWidth(CommandPanelSet set, Func<CommandPanelIcon, int> labelWidth, Func<CommandPanel, int> titleWidth)
    {
        ArgumentNullException.ThrowIfNull(set);
        var widest = set.Panels.Values.SelectMany(p => p.Icons.Select(labelWidth).Append(titleWidth(p))).DefaultIfEmpty(0).Max();
        // s141: the icon rows sit in the strip's lower part (RowAt), so they count; the YES / NO box is in the shipwright's
        // window under the picture and does not.
        var rows = set.Rows.Values.Select(r => RowLayout(r, set.IconSize, labelWidth).Width).DefaultIfEmpty(0).Max();
        return Math.Max(Math.Max(set.Window.Width, widest + 2 * PanelMargin), rows);
    }

    /// <summary>Pack pixels between neighbouring labels of an icon row (s140) when they would touch.</summary>
    public const int RowLabelGap = 4;

    /// <summary>
    /// An icon row's window width and label starts (s140): each label centred under its icon as <c>_seticon</c> puts it,
    /// moved right just enough to clear the label before it (English labels run longer than P4.X's: "N:Previous page"
    /// under the second icon would cross "SPACE"), the window widened to hold the last one (the user's rule: no cropping).
    /// </summary>
    public static (int Width, IReadOnlyList<int> LabelX) RowLayout(CommandPanel row, int iconSize, Func<CommandPanelIcon, int> labelWidth)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(labelWidth);
        var r = row.Row ?? throw new ArgumentException("Not an icon row.", nameof(row));
        var ws = row.Icons.Select(labelWidth).ToArray();
        var xs = new int[ws.Length];
        // Centred, then pushed right off the label before; then pulled left inside the window; then pushed right again
        // (a row that cannot fit its window widens it).
        void PushRight()
        {
            var end = PanelMargin - RowLabelGap;
            for (var i = 0; i < xs.Length; i++)
                end = (xs[i] = Math.Max(xs[i], end + RowLabelGap)) + ws[i];
        }
        for (var i = 0; i < xs.Length; i++)
            xs[i] = PanelLabelAt(r.IconX[i], r.IconY, iconSize, ws[i]).X;
        PushRight();
        var limit = r.Width - PanelMargin;
        for (var i = xs.Length - 1; i >= 0; i--)
            limit = (xs[i] = Math.Min(xs[i], limit - ws[i])) - RowLabelGap;
        PushRight();
        var last = xs.Length == 0 ? 0 : xs[^1] + ws[^1];
        return (Math.Max(r.Width, last + PanelMargin), xs);
    }

    /// <summary>The YES / NO box's width (s140): its sunken frame and 2 pixels of window on each side.</summary>
    public static int NoYesWidth(YesNoBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return box.Frame.Width + 2 * NoYesFrame + 2 * PanelMargin;
    }

    /// <summary>Pixels of <c>_hole</c>'s sunken frame round the YES / NO box: two nested 1-pixel frames.</summary>
    public const int NoYesFrame = 2;

    private int? _panelWidth;

    /// <summary><see cref="FittedPanelWidth"/> for the pack's fonts and the current language (measured once).</summary>
    private int PanelWidth(CommandPanelSet set, float k)
    {
        if (_panelWidth is int w)
            return w;
        var labelFont = FontSources.ForRole(Assets?.Pack, PanelLabelFontRole);
        var titleFont = FontSources.ForRole(Assets?.Pack, MysticFontRole);
        int Measure(string text, IFontSource? font) =>
            font is not null ? PixelFont.Measure(text, font) : (int)Math.Ceiling(PixelFont.Measure(text) / k);
        _panelWidth = w = Math.Max(FittedPanelWidth(set, i => Measure(PanelText(i.Label, i.Text), labelFont),
            p => Measure(PanelText(p.Title, p.TitleText), titleFont)),
            ListsWidth(set, ListTexts, s => Measure(s, titleFont), PanelText)); // s151: the lists' rows (font 0)
        return w;
    }

    /// <summary>The label's colour runs: the first character in colour 2, the rest in 0; a label starting with a space all in 2.</summary>
    public static IReadOnlyList<(string Text, int Colour)> PanelLabelRuns(string label) =>
        label.Length == 0 ? []
        : label[0] == ' ' ? [(label, PanelKeyColour)]
        : [(label[..1], PanelKeyColour), (label[1..], PanelTextColour)];

    private readonly Dictionary<string, string> _panelText = [];

    /// <summary>The label / title in the current language: the string tables' text, else the original's.</summary>
    private string PanelText(string id, string original)
    {
        if (_panelText.TryGetValue(id, out var t))
            return t;
        try
        {
            t = GameText.S(id);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            t = original;
        }
        return _panelText[id] = t;
    }

    /// <summary>
    /// Layout pixels the panel takes: its window plus the title bar, the frame and the 1-pixel shadow right and below
    /// (s141, <c>_wwind</c>), at <paramref name="k"/> layout pixels per pack pixel.
    /// </summary>
    private Vector2 PanelSize(CommandPanelSet set, float k) =>
        new((PanelWidth(set, k) + 1) * k, (set.Window.Height + PanelTitleRows + 2) * k);

    /// <summary>Palette colour 1 (dark grey): the line on the title bar's last row (s141, <c>_wwind</c>).</summary>
    public const int PanelBarLineColour = 1;

    /// <summary>The label width of an icon row's icon in the pack's font (or the remaster's, scaled).</summary>
    private int RowLabelWidth(CommandPanelIcon i, float k)
    {
        var labelFont = FontSources.ForRole(Assets?.Pack, PanelLabelFontRole);
        return labelFont is not null ? PixelFont.Measure(PanelText(i.Label, i.Text), labelFont)
            : (int)Math.Ceiling(PixelFont.Measure(PanelText(i.Label, i.Text)) / k);
    }

    /// <summary>
    /// Where an icon row goes (s141, layout pixels): window 36 lies at (314, 270) of the sheet's 472 × 338 area, 8 pixels in
    /// from its right edge and 20 up from its bottom (<see cref="CommandPanelRow.RightInset"/> / <c>BottomInset</c>). The
    /// remaster's sheet is the text box with the strip beside it (the strip is empty while a row is up), so the row keeps
    /// those insets from the strip's right edge <paramref name="right"/> and the box's bottom <paramref name="bottom"/>; it
    /// never starts left of the strip (<paramref name="stripLeft"/>) or above <paramref name="top"/>.
    /// </summary>
    public static Vector2 RowAt(CommandPanelRow row, int width, float k, float right, float bottom, float stripLeft, float top)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new(Math.Max(stripLeft, right - (row.RightInset + width) * k),
            Math.Max(top, bottom - (row.BottomInset + row.Height) * k));
    }

    /// <summary>
    /// Draws <paramref name="panel"/> with its top-left at <paramref name="at"/> (layout pixels): the title bar, the grey
    /// window, each icon picture (masked) and its label; registers each icon as a click on its key.
    /// </summary>
    private void DrawPanel(SpriteBatch batch, Texture2D pixel, CommandPanelSet set, CommandPanel panel, Vector2 at, float k)
    {
        Color Pal(int i, Color fallback) => Assets?.PaletteColour("pkh", i) ?? fallback;
        var black = Pal(0, Color.Black);
        var grey = Pal(PanelBackColour, Color.LightGray);
        void Box(float x, float y, float w, float h, Color c) =>
            batch.Draw(pixel, at + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k),
                SpriteEffects.None, 0f);
        var w = PanelWidth(set, k);
        var labelFont = FontSources.ForRole(Assets?.Pack, PanelLabelFontRole);
        var body = at + new Vector2(0, PanelTitleRows * k);
        IReadOnlyList<int>? rowLabels = null;
        if (panel.Row is { } row)
        {
            // s141: window 36 (_wopen68 flag 0) draws no fill and no frame (_wwind returns at once): the icons sit on the
            // sheet's grey (window 35, titled: colour 14). The remaster's sheet is the text box, so the row's area gets
            // that grey and no frame.
            var (rw, xs) = RowLayout(panel, set.IconSize, i => RowLabelWidth(i, k));
            rowLabels = xs;
            Box(0, 0, rw, row.Height, grey);
            body = at;
        }
        else
        {
            // _wwind / _wactive (s141 [C], FORMAT.md 6.9 "Window frames"): a black frame round the bar and the window; the
            // bar's 18 grey rows, a dark grey (colour 1) line, a black line; the window's grey (titled: 14); a 1-pixel black
            // shadow right (rows of the bar and from the window's second row) and below.
            // s151 [C]: _wttl prints the title in font 0, style 0, colour 0 at y −18 (XEiJ "Command", pixel-equal).
            DrawTitledFrame(batch, pixel, at, w, set.Window.Height, PanelText(panel.Title, panel.TitleText), k);
        }

        for (var i = 0; i < panel.Icons.Count; i++)
        {
            var icon = panel.Icons[i];
            var (x, y) = set.IconAt(panel, i);
            var topLeft = body + new Vector2(x * k, y * k);
            if (Assets?.Get($"p4.icon.{icon.Icon}") is { } picture) // s151: lit under the pointer (_xicon)
                batch.Draw(IconUnderPointer(picture, topLeft, set.IconSize * k), topLeft, null, Color.White, 0f, Vector2.Zero, k,
                    SpriteEffects.None, 0f);
            var label = PanelText(icon.Label, icon.Text);
            if (labelFont is not null)
            {
                var (lx, ly) = PanelLabelAt(x, y, set.IconSize, PixelFont.Measure(label, labelFont), w);
                if (rowLabels is not null)
                    lx = rowLabels[i];
                var pen = (float)lx;
                foreach (var (text, colour) in PanelLabelRuns(label))
                {
                    PixelFont.DrawScaled(batch, pixel, body + new Vector2(pen * k, ly * k), text, Pal(colour, black), k, labelFont);
                    pen += PixelFont.Measure(text, labelFont);
                }
            }
            else
            {
                var lw = PixelFont.Measure(label);
                var lx = rowLabels is not null ? (int)Math.Round(body.X + rowLabels[i] * k)
                    : (int)Math.Round(topLeft.X + set.IconSize * k / 2 - lw / 2f);
                var ly = (int)Math.Round(topLeft.Y + set.IconSize * k);
                foreach (var (text, colour) in PanelLabelRuns(label))
                {
                    PixelFont.Draw(batch, pixel, lx, ly, text, Pal(colour, black));
                    lx += PixelFont.Measure(text);
                }
            }
            var size = (int)Math.Ceiling(set.IconSize * k);
            if (!PanelInert && PanelPress(panel.Name, icon) is (var key and not Keys.None, var line))
                UiOverlays.Hits?.Add(new HitRegion(new Rectangle((int)topLeft.X, (int)topLeft.Y, size, size),
                    new P4PanelIconTag(panel.Name, i, line)) { Click = [key] });
        }
        if (KeypadUp && panel.Row is null)
            DrawKeypad(batch, pixel, body, k); // s151: _numin's keys in window 34, under the Bank's icons
    }

    /// <summary>
    /// The shipwright's YES / NO box (s140, <c>_noyes</c>, FORMAT.md 6.9) in the shipwright's window, whose origin is the
    /// picture's top-left <paramref name="at"/> (layout pixels; s141): the box at (104, 300) under the picture, the window's
    /// grey round it (<see cref="YesNoBox.Clear"/>, <c>_wfbox(win, 14, x − 10, y − 10, 124, 36)</c>), <c>_hole</c>'s sunken
    /// 2-pixel frame (dark top / left, light bottom / right), the two buttons filled in their colour with YES / NO centred
    /// in font 0, black (s151 [C]: <c>_noyes</c>'s <c>_font(0, 0, 0, 16)</c>); each button is a click on its key, lit under the
    /// pointer (colours ^ 5). The rest of the window: <see cref="DrawShipwrightWindow"/>.
    /// </summary>
    private void DrawNoYes(SpriteBatch batch, Texture2D pixel, YesNoBox box, Vector2 at, float k)
    {
        Color Pal(int i, Color fallback) => Assets?.PaletteColour("pkh", i) ?? fallback;
        var black = Pal(0, Color.Black);
        void Box(float x, float y, float w, float h, Color c) =>
            batch.Draw(pixel, at + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k),
                SpriteEffects.None, 0f);
        var clear = box.Clear ?? (box.Frame.X - NoYesFrame, box.Frame.Y - NoYesFrame, box.Frame.Width + 2 * NoYesFrame,
            box.Frame.Height + 2 * NoYesFrame);
        Box(box.X + clear.X, box.Y + clear.Y, clear.Width, clear.Height, Pal(box.BackColour, Color.LightGray));
        // The frame's inside (the hole) starts at (x, y) + frame (x, y); the buttons sit at (x, y) + their own (x, y).
        float hx = box.X + box.Frame.X, hy = box.Y + box.Frame.Y;
        int fw = box.Frame.Width, fh = box.Frame.Height;
        Color dark = Pal(box.Dark, Color.DimGray), light = Pal(box.Light, Color.White);
        for (var n = 1; n <= NoYesFrame; n++)
        {
            Box(hx - n, hy - n, fw + 2 * n, 1, dark);          // top
            Box(hx - n, hy - n, 1, fh + 2 * n, dark);          // left
            Box(hx - n, hy + fh + n - 1, fw + 2 * n, 1, light); // bottom
            Box(hx + fw + n - 1, hy - n, 1, fh + 2 * n, light); // right
        }
        var font = FontSources.ForRole(Assets?.Pack, MysticFontRole);
        foreach (var b in box.Buttons)
        {
            float bx = hx - box.Frame.X + b.X, by = hy - box.Frame.Y + b.Y;
            var xor = BoxXorAt(at.X + bx * k, at.Y + by * k, b.Width * k, b.Height * k); // s151: lit under the pointer
            Box(bx, by, b.Width, b.Height, Pal(box.ButtonColour ^ xor, Color.SteelBlue));
            var text = PanelText(b.Label, b.Text);
            if (font is not null)
                PixelFont.DrawScaled(batch, pixel,
                    at + new Vector2((bx + (b.Width - PixelFont.Measure(text, font)) / 2f) * k,
                        (by + (b.Height - font.Height) / 2f) * k), text, Pal(xor, black), k, font);
            else
                PixelFont.Draw(batch, pixel, (int)Math.Round(at.X + bx * k + (b.Width * k - PixelFont.Measure(text)) / 2),
                    (int)Math.Round(at.Y + by * k + (b.Height * k - PixelFont.GlyphHeight) / 2), text, Pal(xor, black));
            if (NoYesKey(b) is var key and not Keys.None)
                UiOverlays.Hits?.Add(new HitRegion(new Rectangle((int)(at.X + bx * k), (int)(at.Y + by * k),
                    (int)Math.Ceiling(b.Width * k), (int)Math.Ceiling(b.Height * k)), new P4PanelIconTag(NoYesName,
                    box.Buttons.ToList().IndexOf(b))) { Click = [key] });
        }
    }

    /// <summary>The key a YES / NO button presses: its letter (Y / N, the town's answer keys).</summary>
    public static Keys NoYesKey(YesNoButton button)
    {
        ArgumentNullException.ThrowIfNull(button);
        return button.Key is [var c] && char.IsAsciiLetterUpper(c) ? Keys.A + (c - 'A') : Keys.None;
    }
}

/// <summary>
/// A click target on IV's Command panel: the panel and the icon's index; its click presses the icon's key, after putting
/// the cursor on <paramref name="Line"/> when that list line has no digit key.
/// </summary>
public sealed record P4PanelIconTag(string Panel, int Index, int? Line = null);
