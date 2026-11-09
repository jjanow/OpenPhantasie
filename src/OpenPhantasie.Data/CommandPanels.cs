using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>One icon button of a <see cref="CommandPanel"/>: the pack's icon picture number, its hot key, its label.</summary>
/// <param name="Icon">Record of the pack's icon pictures (IV X68000: <c>icon.pix</c>, <c>p4.icon.N</c>).</param>
/// <param name="Key">The hot key: a letter, or <c>Space</c>.</param>
/// <param name="Label">The label's string id (<c>p4x.0x…</c>).</param>
/// <param name="Text">The label as the original has it (shown when the string tables lack <paramref name="Label"/>).</param>
public sealed record CommandPanelIcon(int Icon, string Key, string Label, string Text);

/// <summary>
/// One icon menu of the original (IV X68000: one <c>_mkicon</c> call), named by the function that opens it. A
/// <paramref name="Row"/> panel (s140: the profile's and Party info's window-36 buttons) has its own untitled window and
/// icon places instead of the Command window's.
/// </summary>
public sealed record CommandPanel(string Name, string Title, string TitleText, IReadOnlyList<CommandPanelIcon> Icons,
    CommandPanelRow? Row = null);

/// <summary>
/// An icon row outside <c>_mkicon</c> (s140; IV X68000 <c>_seticon(36, x, y)</c> in the 150 × 48 window 36 of the profile
/// pages and Party info): an untitled window <paramref name="Width"/> × <paramref name="Height"/>, icon i at
/// (<paramref name="IconX"/>[i], <paramref name="IconY"/>), labels as the panels'. s141: the window lies at
/// (<paramref name="X"/>, <paramref name="Y"/>) of the sheet it belongs to (window 35, <paramref name="SheetWidth"/> ×
/// <paramref name="SheetHeight"/>), with no fill and no frame of its own.
/// </summary>
public sealed record CommandPanelRow(int Width, int Height, IReadOnlyList<int> IconX, int IconY, int X = 0, int Y = 0,
    int SheetWidth = 0, int SheetHeight = 0)
{
    /// <summary>Pixels between the row's window and the sheet's right edge (s141: 472 − 314 − 150 = 8).</summary>
    public int RightInset => Math.Max(0, SheetWidth - X - Width);

    /// <summary>Pixels between the row's window and the sheet's bottom (s141: 338 − 270 − 48 = 20).</summary>
    public int BottomInset => Math.Max(0, SheetHeight - Y - Height);
}

/// <summary>One button of the <see cref="YesNoBox"/>: its box (relative to the box's (x, y)), hot key and label.</summary>
public sealed record YesNoButton(int X, int Y, int Width, int Height, string Key, string Label, string Text);

/// <summary>
/// <c>_noyes</c> (s140; IV X68000, the shipwright's question): a sunken frame (<see cref="Frame"/> relative to
/// (<see cref="X"/>, <see cref="Y"/>), 2 pixels of <see cref="Dark"/> top / left and <see cref="Light"/> bottom / right)
/// round two filled buttons with their text centred, in a window at (<see cref="WindowX"/>, <see cref="WindowY"/>).
/// s141: (<see cref="X"/>, <see cref="Y"/>) counts from the window's origin, which is the shipwright picture's top-left;
/// <see cref="Clear"/> (relative to (x, y)) is the window's grey (<see cref="BackColour"/>) round the frame.
/// </summary>
public sealed record YesNoBox(int WindowX, int WindowY, int X, int Y, (int X, int Y, int Width, int Height) Frame, int Dark,
    int Light, int ButtonColour, int BackColour, IReadOnlyList<YesNoButton> Buttons,
    (int X, int Y, int Width, int Height)? Clear = null);

/// <summary>A text line of a <see cref="CommandList"/>: at (x, y) in the window, a button on <paramref name="Key"/> ("" = none).</summary>
public sealed record CommandListText(int X, int Y, string Key, string Label, string Text);

/// <summary>
/// The rows of a <see cref="CommandList"/> (pack pixels in the window): row i's key character ('A' + i, or the digit i + 1
/// when <paramref name="Digits"/>) at (<paramref name="KeyX"/>, <paramref name="Top"/> + <paramref name="Step"/> · i), its
/// text at <paramref name="TextX"/>, the first <paramref name="ExtraChars"/> characters of a second field at
/// <paramref name="ExtraX"/>; the click box (<paramref name="ButtonX"/>, y, <paramref name="ButtonWidth"/>,
/// <paramref name="ButtonHeight"/>).
/// </summary>
public sealed record CommandListRows(int Top, int Step, int KeyX, int TextX, bool Digits, int ButtonX, int ButtonWidth,
    int ButtonHeight, int? ExtraX = null, int ExtraChars = 0)
{
    /// <summary>The key character of row <paramref name="i"/>.</summary>
    public char KeyOf(int i) => Digits ? (char)('1' + i) : (char)('A' + i);

    /// <summary>The y of row <paramref name="i"/>.</summary>
    public int YOf(int i) => Top + Step * i;
}

/// <summary>
/// A list the original draws as text buttons in the Command window (s151; IV X68000: the spell list <c>_getmagic</c>,
/// Transportation's towns <c>_cast_47</c>, creation's race / class / Hall lists): its own window (<see cref="Window"/>,
/// titled <see cref="Title"/>) or window 34 as the Command panel left it (null), the <c>_mkicon</c> panel drawn first in it
/// (<see cref="Panel"/>), text buttons above the rows (<see cref="Head"/>), centred lines, a <c>_wbox</c> outline
/// (<see cref="Rule"/>), the list area's clear (<see cref="Clear"/>) and the rows.
/// </summary>
public sealed record CommandList(string Name, (int X, int Y, int Width, int Height)? Window, string Title, string TitleText,
    string? Panel, IReadOnlyList<CommandListText> Head, IReadOnlyList<CommandListText> Centred,
    (int X, int Y, int Width, int Height, int Colour)? Rule, (int X, int Y, int Width, int Height, int Colour)? Clear,
    CommandListRows Rows);

/// <summary>
/// The original's Command panel (s136; IV X68000 <c>_mkicon</c> / <c>_seticon</c>, FORMAT.md 6.9 "Command panel"): one
/// window of icon buttons beside the picture, laid out in pack pixels: icon i at (<see cref="IconX"/>[i &amp; 1],
/// <see cref="IconTop"/> + <see cref="IconStep"/> · i) inside <see cref="Window"/>, its label under it. Loaded from the
/// file a pack names in <c>"command_panels"</c> (<c>tools/scripts/x68k_p4_panels.py</c>). s140: also the icon rows outside
/// <c>_mkicon</c> (<see cref="Rows"/>) and the shipwright's YES / NO box (<see cref="NoYes"/>).
/// </summary>
public sealed partial class CommandPanelSet
{
    public required (int X, int Y, int Width, int Height) Window { get; init; }
    public required IReadOnlyList<int> IconX { get; init; }
    public required int IconTop { get; init; }
    public required int IconStep { get; init; }
    public required int IconSize { get; init; }
    public required IReadOnlyDictionary<string, CommandPanel> Panels { get; init; }

    /// <summary>The icon rows outside <c>_mkicon</c> (s140), each a <see cref="CommandPanel"/> with its <see cref="CommandPanel.Row"/>.</summary>
    public IReadOnlyDictionary<string, CommandPanel> Rows { get; init; } = new Dictionary<string, CommandPanel>();

    /// <summary>The shipwright's YES / NO box (<c>_noyes</c>, s140), or null.</summary>
    public YesNoBox? NoYes { get; init; }

    /// <summary>The lists drawn as text buttons in the Command window (s151, <see cref="CommandList"/>), by name.</summary>
    public IReadOnlyDictionary<string, CommandList> Lists { get; init; } = new Dictionary<string, CommandList>();
    /// <summary>
    /// s151: the title's New Game / Saved Game panel (<c>_diskmenu(0)</c>, <c>"disk"</c>), or null: New Game, then the three
    /// save-slot icons, of which the original shows only the slots that hold a save, each at the next icon place.
    /// </summary>
    public CommandPanel? DiskMenu { get; init; }

    /// <summary>
    /// <c>_diskmenu(1)</c>'s first icon (the System menu's Save, <c>_disk(1)</c>): " SPACE" (table entry 1) at icon place 0,
    /// before all three slot icons of <see cref="DiskMenu"/>; null when the pack lacks it.
    /// </summary>
    public CommandPanelIcon? DiskSpace { get; init; }

    /// <summary>
    /// <c>_diskmenu(1)</c>'s line under the slots (<c>_wkprcen(34, 68, 308, p4x.0x3f6c6)</c>): centred on
    /// <see cref="CommandListText.X"/> at <see cref="CommandListText.Y"/> in window 34; null when the pack lacks it.
    /// </summary>
    public CommandListText? DiskCaption { get; init; }

    /// <summary>The top-left of icon <paramref name="index"/> inside the window (pack pixels).</summary>
    public (int X, int Y) IconAt(int index) => (IconX[index & 1], IconTop + IconStep * index);

    /// <summary>The top-left of icon <paramref name="index"/> of <paramref name="panel"/>: its row's place, else the panel's.</summary>
    public (int X, int Y) IconAt(CommandPanel panel, int index)
    {
        ArgumentNullException.ThrowIfNull(panel);
        return panel.Row is { } r ? (r.IconX[index], r.IconY) : IconAt(index);
    }

    /// <summary>A panel or a row by name, or null.</summary>
    public CommandPanel? Find(string name) => Panels.GetValueOrDefault(name) ?? Rows.GetValueOrDefault(name);

    public static CommandPanelSet Load(string path)
    {
        var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path}: empty command panel file.");
        if (dto.Window is not { } w || dto.IconX is not { Length: 2 } || dto.Panels is null)
            throw new InvalidDataException($"{path}: window, icon_x and panels are required.");
        return new CommandPanelSet
        {
            Window = (w.X, w.Y, w.Width, w.Height),
            IconX = dto.IconX,
            IconTop = dto.IconTop,
            IconStep = dto.IconStep,
            IconSize = dto.IconSize,
            Panels = dto.Panels.ToDictionary(kv => kv.Key, kv => new CommandPanel(kv.Key, kv.Value.Title ?? "",
                kv.Value.TitleText ?? "", Icons(kv.Value.Icons)), StringComparer.Ordinal),
            Rows = (dto.Rows ?? []).ToDictionary(kv => kv.Key, kv => kv.Value is { Window: { } rw, IconX: { } x } r
                    && x.Length == (r.Icons?.Count ?? 0)
                    ? new CommandPanel(kv.Key, "", "", Icons(r.Icons), new CommandPanelRow(rw.Width, rw.Height, x, r.IconY,
                        rw.X, rw.Y, r.Sheet?.Width ?? 0, r.Sheet?.Height ?? 0))
                    : throw new InvalidDataException($"{path}: row {kv.Key} needs a window and one icon_x per icon."),
                StringComparer.Ordinal),
            NoYes = dto.NoYes is { Window: { } nw, Frame: { } f, Buttons: { } b } n
                ? new YesNoBox(nw.X, nw.Y, n.X, n.Y, (f.X, f.Y, f.Width, f.Height), f.Dark, f.Light, n.ButtonColour,
                    n.BackColour, [.. b.Select(x => new YesNoButton(x.X, x.Y, x.Width, x.Height, x.Key ?? "", x.Label ?? "",
                        x.Text ?? ""))], n.Clear is { } c ? (c.X, c.Y, c.Width, c.Height) : null)
                : null,
            Lists = (dto.Lists ?? []).ToDictionary(kv => kv.Key, kv => List(path, kv.Key, kv.Value), StringComparer.Ordinal),
            Extras = PanelExtras.Load(path),
            DiskMenu = dto.Disk is { } d
                ? new CommandPanel("_diskmenu", d.Title ?? "", d.TitleText ?? "", Icons(d.Icons))
                : null,
            DiskSpace = dto.Disk?.Space is { } sp ? Icons([sp])[0] : null,
            DiskCaption = dto.Disk?.Caption is { } cap
                ? new CommandListText(cap.X, cap.Y, cap.Key ?? "", cap.Label ?? "", cap.Text ?? "")
                : null,
        };
    }

    private static CommandList List(string path, string name, ListDto l)
    {
        if (l.Rows is not { } r)
            throw new InvalidDataException($"{path}: list {name} needs rows.");
        static CommandListText Text(TextDto t) => new(t.X, t.Y, t.Key ?? "", t.Label ?? "", t.Text ?? "");
        static (int X, int Y, int Width, int Height, int Colour)? Box(BoxDto? b) =>
            b is null ? null : (b.X, b.Y, b.Width, b.Height, b.Colour);
        return new CommandList(name, l.Window is { } w ? (w.X, w.Y, w.Width, w.Height) : null, l.Title ?? "", l.TitleText ?? "",
            l.Panel, [.. (l.Head ?? []).Select(Text)], [.. (l.Centred ?? []).Select(Text)], Box(l.Rule), Box(l.Clear),
            new CommandListRows(r.Top, r.Step, r.KeyX, r.TextX, r.Keys == "digit", r.ButtonX, r.ButtonWidth, r.ButtonHeight,
                r.ExtraX, r.ExtraChars));
    }

    private static List<CommandPanelIcon> Icons(List<IconDto>? icons) =>
        [.. (icons ?? []).Select(i => new CommandPanelIcon(i.Icon, i.Key ?? "", i.Label ?? "", i.Text ?? ""))];

    private sealed class Dto
    {
        [JsonPropertyName("window")] public WindowDto? Window { get; set; }
        [JsonPropertyName("icon_x")] public int[]? IconX { get; set; }
        [JsonPropertyName("icon_top")] public int IconTop { get; set; }
        [JsonPropertyName("icon_step")] public int IconStep { get; set; }
        [JsonPropertyName("icon_size")] public int IconSize { get; set; }
        [JsonPropertyName("panels")] public Dictionary<string, PanelDto>? Panels { get; set; }
        [JsonPropertyName("rows")] public Dictionary<string, RowDto>? Rows { get; set; }
        [JsonPropertyName("noyes")] public NoYesDto? NoYes { get; set; }
        [JsonPropertyName("disk")] public PanelDto? Disk { get; set; }
        [JsonPropertyName("lists")] public Dictionary<string, ListDto>? Lists { get; set; }
    }

    private sealed class ListDto
    {
        [JsonPropertyName("window")] public WindowDto? Window { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("title_text")] public string? TitleText { get; set; }
        [JsonPropertyName("panel")] public string? Panel { get; set; }
        [JsonPropertyName("head")] public List<TextDto>? Head { get; set; }
        [JsonPropertyName("centred")] public List<TextDto>? Centred { get; set; }
        [JsonPropertyName("rule")] public BoxDto? Rule { get; set; }
        [JsonPropertyName("clear")] public BoxDto? Clear { get; set; }
        [JsonPropertyName("rows")] public ListRowsDto? Rows { get; set; }
    }

    private sealed class TextDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class BoxDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("colour")] public int Colour { get; set; }
    }

    private sealed class ListRowsDto
    {
        [JsonPropertyName("top")] public int Top { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("key_x")] public int KeyX { get; set; }
        [JsonPropertyName("text_x")] public int TextX { get; set; }
        [JsonPropertyName("keys")] public string? Keys { get; set; }
        [JsonPropertyName("button_x")] public int ButtonX { get; set; }
        [JsonPropertyName("button_width")] public int ButtonWidth { get; set; }
        [JsonPropertyName("button_height")] public int ButtonHeight { get; set; }
        [JsonPropertyName("extra_x")] public int? ExtraX { get; set; }
        [JsonPropertyName("extra_chars")] public int ExtraChars { get; set; }
    }

    private sealed class WindowDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
    }

    private sealed class PanelDto
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("title_text")] public string? TitleText { get; set; }
        [JsonPropertyName("icons")] public List<IconDto>? Icons { get; set; }
        [JsonPropertyName("space")] public IconDto? Space { get; set; }
        [JsonPropertyName("caption")] public TextDto? Caption { get; set; }
    }

    private sealed class IconDto
    {
        [JsonPropertyName("icon")] public int Icon { get; set; }
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class RowDto
    {
        [JsonPropertyName("window")] public WindowDto? Window { get; set; }
        [JsonPropertyName("icon_x")] public int[]? IconX { get; set; }
        [JsonPropertyName("icon_y")] public int IconY { get; set; }
        [JsonPropertyName("sheet")] public WindowDto? Sheet { get; set; }
        [JsonPropertyName("icons")] public List<IconDto>? Icons { get; set; }
    }

    private sealed class NoYesDto
    {
        [JsonPropertyName("window")] public WindowDto? Window { get; set; }
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("frame")] public FrameDto? Frame { get; set; }
        [JsonPropertyName("clear")] public WindowDto? Clear { get; set; }
        [JsonPropertyName("button_colour")] public int ButtonColour { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("buttons")] public List<ButtonDto>? Buttons { get; set; }
    }

    private sealed class FrameDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("dark")] public int Dark { get; set; }
        [JsonPropertyName("light")] public int Light { get; set; }
    }

    private sealed class ButtonDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
