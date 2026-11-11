using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s151: the start menu over a title step (<see cref="TitleStep.Menu"/>), IV's <c>_disk(0)</c> (P4.X <c>0x1C7E</c>,
/// <c>_diskmenu</c> <c>0x1AB4</c>; FORMAT.md 6.10): window 34 titled "Select File" with New Game at icon place 0 and one
/// "*k:Save" icon per save slot k that holds a save, at the next places (the panels' zigzag). <c>_disk</c> reads one
/// <c>_mgetc</c>, which returns only a button: N (or a click on its icon) starts a new game, a slot's digit loads it;
/// no other key does anything. IV keeps the original's three slots either way the SAVE SLOTS switch is
/// (`docs/plan/12-save-slots.md` section 2a).
/// </summary>
public sealed partial class TitleSplash
{
    /// <summary>The menu's icons the original shows now (Game1: New Game and the slots that hold a save); empty = none.</summary>
    public IReadOnlyList<CommandPanelIcon> MenuIcons { get; set; } = [];

    /// <summary>The pack's command panels (the menu's window, icon places and the <c>"disk"</c> panel), or null.</summary>
    public CommandPanelSet? Panels { get; init; }

    /// <summary>The key of the menu icon picked (e.g. "N", "1"), once the menu step has ended on a pick; else null.</summary>
    public string? MenuPick { get; private set; }

    /// <summary>The menu panel of the current step, or null.</summary>
    public CommandPanel? CurrentMenu => Current is { } step ? MenuOf(step) : null;

    /// <summary>A step's menu panel from the pack (null when the step has none or the pack lacks it: then any key ends it).</summary>
    private CommandPanel? MenuOf(TitleStep step) => step.Menu is "disk" ? Panels?.DiskMenu : null;

    /// <summary>
    /// <c>_diskmenu(0)</c>'s icons: New Game (the panel's icons without a digit key), then each save slot whose digit
    /// <paramref name="saved"/> says holds a save, in slot order.
    /// </summary>
    public static IReadOnlyList<CommandPanelIcon> DiskMenuIcons(CommandPanel disk, Func<int, bool> saved)
    {
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(saved);
        return
        [
            .. disk.Icons.Where(i => SlotOf(i) is null),
            .. disk.Icons.Where(i => SlotOf(i) is int slot && saved(slot)),
        ];
    }

    /// <summary>The save slot an icon stands for (its digit key), or null.</summary>
    public static int? SlotOf(CommandPanelIcon icon) =>
        icon.Key is [var c] && char.IsAsciiDigit(c) ? c - '0' : null;

    /// <summary>The icon key <paramref name="key"/> presses: a letter or a digit (top row or keypad); else null.</summary>
    public static string? MenuKey(Keys key) => key switch
    {
        >= Keys.A and <= Keys.Z => ((char)('A' + (key - Keys.A))).ToString(),
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => ((char)('0' + (key - Keys.NumPad0))).ToString(),
        _ => null,
    };

    /// <summary>s192 (pad fix C20): the icon the pad's D-pad has moved to (index into <see cref="MenuIcons"/>), or -1.</summary>
    public int PadCursor { get; private set; } = -1;

    /// <summary>Seconds a menu step ignores the pad's A after it shows, so A mashed through the pictures picks nothing.</summary>
    public const double PadSettleSeconds = 0.5;

    /// <summary>The menu step has been up <see cref="PadSettleSeconds"/> (the pad's A picks).</summary>
    public bool MenuSettled => CurrentMenu is not null && _elapsed >= PadSettleSeconds;

    /// <summary>The D-pad on the menu: <paramref name="delta"/> icons on (wrapping; the first press lights New Game).</summary>
    public void PadStep(int delta)
    {
        if (CurrentMenu is null || MenuIcons.Count == 0 || delta == 0)
            return;
        PadCursor = PadCursor < 0 ? 0 : ((PadCursor + delta) % MenuIcons.Count + MenuIcons.Count) % MenuIcons.Count;
    }

    /// <summary>
    /// The pad's A on the menu: the lit icon (New Game while none is lit), once <see cref="MenuSettled"/>. True on a pick.
    /// </summary>
    public bool PadChoose()
    {
        if (!MenuSettled || MenuIcons.Count == 0)
            return false;
        var icon = PadCursor >= 0 && PadCursor < MenuIcons.Count ? MenuIcons[PadCursor]
            : MenuIcons.FirstOrDefault(i => SlotOf(i) is null);
        return Choose(icon?.Key);
    }

    /// <summary>
    /// A pick on the current step's menu: the icon with key <paramref name="key"/> among <see cref="MenuIcons"/> ends the
    /// splash (<see cref="MenuPick"/>); anything else does nothing (as <c>_mgetc</c> returns only buttons). True on a pick.
    /// </summary>
    public bool Choose(string? key)
    {
        if (CurrentMenu is null || key is null || !MenuIcons.Any(i => i.Key == key))
            return false;
        MenuPick = key;
        Step = Sequence.Steps.Count;
        Player?.Hide();
        return true;
    }

    /// <summary>Pixels between the window's edge and a label or the title (the panels' <see cref="P4DungeonScreen.PanelMargin"/>).</summary>
    private const int Margin = P4DungeonScreen.PanelMargin;

    /// <summary>The menu window's width: the original's 136, or wider when an English label or the title needs it.</summary>
    private static int MenuWidth(CommandPanelSet set, IEnumerable<int> widths) =>
        Math.Max(set.Window.Width, widths.DefaultIfEmpty(0).Max() + 2 * Margin);

    /// <summary>The menu's top-left on the canvas: the step's place, moved left when a wider window would pass the edge.</summary>
    private (int X, int Y) MenuAt(TitleStep step, int width) =>
        (Math.Max(0, Math.Min(step.MenuX, Sequence.Width - width - 1)), step.MenuY);

    /// <summary>Icon <paramref name="index"/>'s box on the canvas (the panels' places, under the 21-row title bar).</summary>
    private Rectangle MenuIconBox(CommandPanelSet set, (int X, int Y) at, int index)
    {
        var (x, y) = set.IconAt(index);
        return new Rectangle(at.X + x, at.Y + P4DungeonScreen.PanelTitleRows + y, set.IconSize, set.IconSize);
    }

    /// <summary>Where <see cref="DrawMenu"/> last put the menu window (canvas pixels), or null before it is drawn.</summary>
    private (int X, int Y)? _menuAt;

    /// <summary>The key of the menu icon at canvas point <paramref name="p"/>, or null (the click box is the picture only).</summary>
    public string? MenuKeyAt(Point p)
    {
        if (CurrentMenu is null || Panels is not { } set || _menuAt is not { } at)
            return null;
        for (var i = 0; i < MenuIcons.Count; i++)
            if (MenuIconBox(set, at, i).Contains(p))
                return MenuIcons[i].Key;
        return null;
    }

    /// <summary>
    /// Draw the menu window (canvas pixels, 1:1) as the Command panel is drawn (<c>_wwind</c>: black frame, the title bar's
    /// grey rows with the title, a dark grey and a black line, the window's grey, a 1-pixel shadow), each icon picture and
    /// its label (font 1; first character colour 2, the rest colour 0). <paramref name="text"/> turns a string id into the
    /// current language (the original's text when the table lacks it).
    /// </summary>
    public void DrawMenu(SpriteBatch batch, Texture2D pixel, AssetCatalog? assets, Func<string, string, string> text,
        string? help = null)
    {
        if (Current is not { } step || CurrentMenu is not { } menu || Panels is not { } set)
            return;
        Color Pal(int i, Color fallback) => assets?.PaletteColour("pkh", i) ?? fallback;
        var black = Pal(0, Color.Black);
        var grey = Pal(P4DungeonScreen.PanelBackColour, Color.LightGray);
        var labelFont = FontSources.ForRole(assets?.Pack, P4DungeonScreen.PanelLabelFontRole);
        var titleFont = FontSources.ForRole(assets?.Pack, P4DungeonScreen.MysticFontRole); // font 0 [~], as the panels
        int Measure(string s, IFontSource? font) => font is not null ? PixelFont.Measure(s, font) : PixelFont.Measure(s);
        var title = text(menu.Title, menu.TitleText);
        var labels = MenuIcons.Select(i => text(i.Label, i.Text)).ToList();
        var w = MenuWidth(set, labels.Select(l => Measure(l, labelFont)).Append(Measure(title, titleFont)));
        var at = MenuAt(step, w);
        _menuAt = at;
        void Box(int x, int y, int bw, int bh, Color c) =>
            batch.Draw(pixel, new Rectangle(at.X + x, at.Y + y, bw, bh), c);
        var bar = P4DungeonScreen.PanelTitleRows;
        var h = set.Window.Height;
        Box(0, 0, w, bar + h + 1, black);
        Box(1, 1, w - 2, bar - 3, grey);
        Box(1, bar - 2, w - 2, 1, Pal(P4DungeonScreen.PanelBarLineColour, Color.DimGray));
        Box(1, bar, w - 2, h, grey);
        Box(w, 1, 1, bar - 1, black);
        Box(w, bar + 1, 1, h + 1, black);
        Box(2, bar + h + 1, w - 1, 1, black);
        var titleX = at.X + (w - Measure(title, titleFont)) / 2;
        if (titleFont is not null)
            PixelFont.DrawScaled(batch, pixel, new Vector2(titleX, at.Y + 2), title, black, 1f, titleFont);
        else
            PixelFont.Draw(batch, pixel, titleX, at.Y + 1, title, black);
        for (var i = 0; i < MenuIcons.Count; i++)
        {
            var box = MenuIconBox(set, at, i);
            if (assets?.Get($"p4.icon.{MenuIcons[i].Icon}") is { } picture)
                batch.Draw(picture, new Vector2(box.X, box.Y), Color.White);
            var label = labels[i];
            var lw = Measure(label, labelFont);
            var (lx, ly) = P4DungeonScreen.PanelLabelAt(box.X - at.X, box.Y - at.Y - bar, set.IconSize, lw, w);
            float pen = at.X + lx;
            foreach (var (run, colour) in P4DungeonScreen.PanelLabelRuns(label))
            {
                var c = Pal(colour, black);
                var y = at.Y + bar + ly;
                if (labelFont is not null)
                    PixelFont.DrawScaled(batch, pixel, new Vector2(pen, y), run, c, 1f, labelFont);
                else
                    PixelFont.Draw(batch, pixel, (int)pen, y, run, c);
                pen += Measure(run, labelFont);
            }
            if (i == PadCursor) // s192 (C20): the pad's pick, outlined as the fights' pad picks are
                UiOverlays.DrawPadMark(batch, pixel, box);
        }
        // s192 (C20): the remaster's help line under the window while the pad is in use (the original's _disk shows none)
        if (help is { Length: > 0 })
        {
            var hy = Math.Min(at.Y + bar + h + 6, Sequence.Height - PixelFont.Font.Height - 2);
            var hx = Math.Max(2, Math.Min(at.X + (w - PixelFont.Measure(help)) / 2, Sequence.Width - PixelFont.Measure(help) - 2));
            batch.Draw(pixel, new Rectangle(hx - 2, hy - 2, PixelFont.Measure(help) + 4, PixelFont.Font.Height + 4), Color.Black);
            PixelFont.Draw(batch, pixel, hx, hy, help, Color.White);
        }
    }
}
