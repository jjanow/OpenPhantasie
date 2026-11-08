using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>A rectangle in pack pixels, relative to what its owner says.</summary>
public readonly record struct PanelRect(int X, int Y, int Width, int Height);

/// <summary>A sunken frame (<c>_whole</c>): <see cref="Rect"/>, 2 pixels of <see cref="Dark"/> top / left and <see cref="Light"/> bottom / right.</summary>
public sealed record SunkenFrame(PanelRect Rect, int Dark, int Light);

/// <summary>The window title (s151, <c>_wttl</c>): font, colour and the glyph cell's top relative to the window's area.</summary>
public sealed record PanelTitleStyle(int Font, int Colour, int Y);

/// <summary>The hover look (s151, <c>_s_ms_get_cp</c> / <c>_xbtn</c>): an icon's opaque pixels' colour ^ <see cref="IconXor"/>, a plain button's ^ <see cref="BoxXor"/>.</summary>
public sealed record PanelHover(int IconXor, int BoxXor);

/// <summary>
/// <c>_ewait</c>'s SPACE bar (s151) in the location map's message window (<see cref="MessageWidth"/> wide, its top
/// <see cref="MessageHeight"/> rows colour <see cref="MessageColour"/>): a line, the label centred on
/// <see cref="LabelX"/> at <see cref="LabelY"/>, a plain button pressing <see cref="Key"/>; INPUTS / INPUTN's band
/// (<see cref="Band"/>, colour <see cref="BandColour"/>) with their prompt at <see cref="PromptX"/>.
/// </summary>
public sealed record EventWaitBar(int MessageWidth, int MessageHeight, int MessageColour, PanelRect Line, int LineColour,
    string Label, string Text, int LabelX, int LabelY, PanelRect Button, string Key, PanelRect Band, int BandColour,
    int PromptX, string Letters, string LettersText, string Number, string NumberText);

/// <summary>One key of <see cref="Keypad"/>: the character drawn, the key it presses (a digit, <c>Back</c>, <c>Enter</c>), <c>_numin</c>'s value.</summary>
public sealed record KeypadKey(string Text, string Key, int Value);

/// <summary>
/// <c>_numin</c>'s keypad (s151) in window <see cref="Window"/> at (<see cref="X"/>, <see cref="Y"/>): key k at
/// (x + step · (k % 3), y + step · (k / 3)), <see cref="Size"/> square, " c " on <see cref="CellColour"/> cells; the
/// sunken <see cref="Frame"/> and the <see cref="Clear"/> grey relative to (x, y); the steady cursor block.
/// </summary>
public sealed record Keypad(int Window, int X, int Y, int Step, int Size, int Digits, SunkenFrame Frame, PanelRect Clear,
    int CellColour, int TextColour, int BackColour, IReadOnlyList<KeypadKey> Keys, int CursorWidth, int CursorHeight,
    int CursorColour);

/// <summary>A place in the shipwright's window and what is printed there.</summary>
public sealed record ShipwrightGold(int X, int Y, string Label, string LabelText, int Digits, string Unit, string UnitText);

/// <summary>
/// <c>_shipyard</c>'s window (s151): <see cref="Window"/> with SHIP.PKH's <see cref="PictureHeight"/> rows at its origin,
/// the rest colour <see cref="BackColour"/>; the offer's line starts, the answer centred at <see cref="AnswerX"/>, the gold line.
/// </summary>
public sealed record ShipwrightWindow(PanelRect Window, int PictureHeight, int BackColour, int TextColour, string Title,
    string TitleText, IReadOnlyList<(int X, int Y)> Lines, int AnswerX, int AnswerY, string Yes, string No,
    ShipwrightGold Gold);

/// <summary>A line of text at (<see cref="X"/>, <see cref="Y"/>) (centred on x where its owner says), its colour and string id.</summary>
public sealed record SkillUpText(int X, int Y, int Colour, string Label, string Text);

/// <summary>
/// <c>_sklup</c>'s rows (s152): <see cref="Count"/> rows from <see cref="Top"/> every <see cref="Step"/> pack pixels, each
/// cleared to <see cref="Back"/>; the key letter, the skill, now and after (<see cref="Digits"/> digits by <c>_wdouti</c>)
/// at their x; the row's plain button <see cref="Button"/> (its y = the row's).
/// </summary>
public sealed record SkillUpRows(int Count, int Top, int Step, int KeyX, int SkillX, int NowX, int AfterX, int Digits,
    int Back, PanelRect Button)
{
    /// <summary>The y of row <paramref name="i"/>.</summary>
    public int YOf(int i) => Top + Step * i;
}

/// <summary>
/// <c>_sklup</c>'s level-up skill window (s152, FORMAT.md 6.9 "Level-up skill picks"): window 32 at <see cref="Window"/>
/// (no fill or frame of its own) over <c>_trang</c>'s window <see cref="Under"/> (grey <see cref="BackColour"/>); the
/// <see cref="Bar"/> above it (y &lt; 0, colour <see cref="BarColour"/>) with <see cref="Title"/> centred, at the end the
/// bar in <see cref="DoneBack"/> with <see cref="Done"/>; the <see cref="Header"/>; <c>_whole</c>'s sunken
/// <see cref="Frame"/>; the rows; the prompt and trained lines' places. Text in font 0, <see cref="TextColour"/> on
/// <see cref="CellColour"/> cell boxes.
/// </summary>
public sealed record SkillUpWindow(PanelRect Window, PanelRect Under, int BackColour, PanelRect Bar, int BarColour,
    SkillUpText Title, SkillUpText Done, int DoneBack, SkillUpText Header, SunkenFrame Frame, SkillUpRows Rows,
    (int X, int Y) Prompt, (int X, int Y) Trained, int TextColour, int CellColour);

/// <summary>
/// One piece of <see cref="TrainingHallWindow"/>'s text: at (<see cref="X"/>, <see cref="Y"/>) when given, else at the text
/// cursor after the last piece; a string (<see cref="Label"/> / <see cref="Text"/>) or, with <see cref="Number"/>, the
/// note's amount (left-aligned, no padding); in <see cref="Font"/> and <see cref="Colour"/> (null = the window's ink).
/// </summary>
public sealed record TrainingHallSegment(int? X, int? Y, string Label, string Text, bool Number, int Font, int? Colour,
    int Style = 0, int Digits = 0);

/// <summary>
/// s162: <c>_endstory</c>'s final screen (FORMAT.md 6.9 "The final screen") in its titled window
/// (<see cref="PanelExtras.Titled"/> <c>_endstory</c>): the <see cref="Heading"/> centred on its x (font 0, style 8 =
/// shadow), the <see cref="Segments"/> (strings and the score, <see cref="TrainingHallSegment.Number"/>) at their places or
/// after the last, and after the SPACE wait the <see cref="Question"/> (font 1, style 1 = bold, colour 2). Rows
/// <see cref="LineStep"/> apart, ink <see cref="TextColour"/>.
/// </summary>
public sealed record FinalScreenWindow(int TextColour, int LineStep, TrainingHallSegment Heading,
    IReadOnlyList<TrainingHallSegment> Segments, TrainingHallSegment Question);

/// <summary>
/// <c>_trang</c>'s own window 32 (s156, FORMAT.md 6.9 "Level-up skill picks"): <see cref="Window"/> in grey
/// <see cref="BackColour"/>, the member's name at <see cref="Name"/>, then the note's segments (keys
/// <c>poor_condition</c>, <c>nothing_more</c>, <c>needs_experience</c>, <c>cost</c>), and <see cref="ShortOfGold"/> after a
/// YES the purse cannot pay. Text in font 0, <see cref="TextColour"/>; rows <see cref="LineStep"/> apart. s158: the
/// window is titled (<c>_wopen</c> flag 1): <see cref="Title"/> is its bar's string id (<see cref="TitleText"/> the
/// original's text).
/// </summary>
/// <para>s165: the same shape holds <c>_mage</c>'s window (the pack's <c>mage</c>): notes <c>poor_condition</c>,
/// <c>nothing_more</c>, <c>not_worthy</c>, <c>offer</c>; <see cref="Results"/> (<c>failed</c>, <c>learned</c>) and
/// <see cref="ShortOfGold"/> at their own place after a pick, the row <see cref="ResultClear"/> cleared first.</para>
public sealed record TrainingHallWindow(PanelRect Window, int BackColour, int TextColour, int LineStep, (int X, int Y) Name,
    IReadOnlyDictionary<string, IReadOnlyList<TrainingHallSegment>> Notes, TrainingHallSegment ShortOfGold,
    string Title = "", string TitleText = "", IReadOnlyDictionary<string, TrainingHallSegment>? Results = null,
    PanelRect? ResultClear = null);

/// <summary>
/// s165: <c>_gatebank</c>'s experience share-out window (titled <c>_gatebank</c>, 312 × 160): the <see cref="Pool"/> line
/// (the party's experience × 10 padded to <see cref="PoolDigits"/>, then <see cref="PoolUnit"/>), the share
/// <see cref="Buttons"/> in their sunken <see cref="Frame"/>, member m's name at (<see cref="NameX"/>, <see cref="RowY"/> +
/// <see cref="RowStep"/> · m) once his share is asked, the share at <see cref="ShareX"/>, overwritten after the last share by
/// the experience given (<see cref="GainDigits"/>).
/// </summary>
public sealed record GateBankWindow(int TextColour, TrainingHallSegment Pool, int PoolDigits, TrainingHallSegment PoolUnit,
    PanelRect Frame, BankShareButtons Buttons, int RowY, int RowStep, int NameX, int ShareX, int GainDigits);

/// <summary>
/// s165: Share out's equipment table under its divider (<c>_init_eq</c> / <c>_disp_eq</c>): the <see cref="Headers"/> at
/// <see cref="HeaderY"/>, the colour-0 <see cref="Line"/>, member m's row at <see cref="RowY"/> + <see cref="RowStep"/> · m:
/// m + 1 in <see cref="NumberColour"/>, the name, the class name cut while its width is below <see cref="ClassWidth"/>, each
/// slot's value (<see cref="Values"/>: slot name and x) padded to <see cref="ValueDigits"/>; a member button over the row;
/// the placed item's slot column boxed in the header (<c>_wxbox</c> XOR <see cref="KindBoxXor"/>).
/// </summary>
public sealed record EquipmentTable(int HeaderY, IReadOnlyList<TrainingHallSegment> Headers, PanelRect Line, int LineColour,
    int RowY, int RowStep, int NumberX, int NumberColour, int NameX, int ClassX, int ClassWidth, int ValueDigits,
    IReadOnlyList<(string Slot, int X)> Values, PanelRect Clear, int ClearColour, PanelRect Button, int KindBoxY,
    int KindBoxWidth, int KindBoxHeight, int KindBoxXor);

/// <summary>
/// s162: a titled window (<c>_wopen(win, x, y, width, height, 1, title)</c>) a building, the town gate, an item command or
/// the ending opens in the picture's place: <see cref="Window"/>, the bar's string id <see cref="Title"/>
/// (<see cref="TitleText"/> the original's text). Grey colour 14, bar and frame as <c>_wwind</c> / <c>_wactive</c>.
/// </summary>
public sealed record TitledWindow(string Name, PanelRect Window, string Title, string TitleText);

/// <summary>
/// s162: <c>_deadmsg</c>'s screen (a wiped-out party): <c>_xblack</c> fills the whole screen colour
/// <see cref="ScreenColour"/>, then the untitled <see cref="Window"/> filled <see cref="Fill"/> with the
/// <see cref="Frames"/> outlined in <see cref="FrameColour"/> and the <see cref="Lines"/> centred on their x in font
/// <see cref="Font"/>, style <see cref="Style"/>, colour <see cref="TextColour"/>; after the key the screen goes black again.
/// </summary>
public sealed record DeadMessageWindow(int ScreenColour, PanelRect Window, int Fill, IReadOnlyList<PanelRect> Frames,
    int FrameColour, int Font, int Style, int TextColour, IReadOnlyList<TrainingHallSegment> Lines);

/// <summary>
/// s162: <c>_townent</c>'s aging rows in its Town Gate window: member m's row at (<see cref="X"/>, <see cref="Y"/> +
/// <see cref="Step"/> · m), the name then the line (<see cref="GrownOlder"/> / <see cref="Died"/>), ink
/// <see cref="TextColour"/>.
/// </summary>
public sealed record TownGateWindow(int X, int Y, int Step, int TextColour, TrainingHallSegment GrownOlder,
    TrainingHallSegment Died);

/// <summary>
/// s163: <c>_inn</c>'s rows under INN.PKH (FORMAT.md 6.9 "Building windows under the picture"): the sunken box at
/// (<see cref="BoxX"/>, <see cref="BoxY"/>), <see cref="BoxWidth"/> wide and <see cref="BoxPad"/> + <see cref="RowStep"/> ·
/// party size high; member m's row at (<see cref="TextX"/>, <see cref="TextY"/> + <see cref="RowStep"/> · m): the name,
/// <see cref="Slept"/>, and <see cref="MpUp"/> after it when the MP roll is won.
/// </summary>
public sealed record InnWindow(int BoxX, int BoxY, int BoxWidth, int BoxPad, int RowStep, int TextX, int TextY,
    int TextColour, TrainingHallSegment Slept, TrainingHallSegment MpUp);

/// <summary>One of the bank's tables (s163): its sunken <see cref="Frame"/>, headers, the colour-0 <see cref="Line"/>, rows
/// from <see cref="RowY"/> every <see cref="RowStep"/>, column x by name (<c>name</c>, <c>balance</c>, <c>limit</c>,
/// <c>amount</c>, <c>share</c>), amounts <see cref="Digits"/> wide (<c>_wlocdi</c>: two spaces a leading zero).</summary>
public sealed record BankTable(PanelRect Frame, IReadOnlyList<TrainingHallSegment> Headers, PanelRect Line, int LineColour,
    int RowY, int RowStep, IReadOnlyDictionary<string, int> Columns, int Digits);

/// <summary>The deposit's share buttons (s163): " n " for n = 0 … <see cref="Count"/> − 1 at (<see cref="X"/>,
/// <see cref="Y"/> + <see cref="Step"/> · n) on <see cref="Colour"/> cells, <see cref="Pad"/> either side.</summary>
public sealed record BankShareButtons(int X, int Y, int Step, int Width, int Height, int Count, int Colour, string Pad);

/// <summary>
/// s163: the bank's window under BANK.PKH (<c>_bank</c>, <c>_deposit</c>, <c>_withdraw</c>): <see cref="Clear"/> the grey
/// under the picture; <see cref="Accounts"/> while the bank's panel waits; <see cref="Deposit"/> / <see cref="Withdraw"/>
/// under their black <see cref="Band"/> with the heading centred; the deposit's buttons and per-share line; the gold line.
/// </summary>
public sealed record BankWindow(PanelRect Clear, int ClearColour, int TextColour, BankTable Accounts, PanelRect Band,
    int BandColour, TrainingHallSegment DepositHeading, BankTable Deposit, BankShareButtons Buttons, PanelRect ClearPer,
    TrainingHallSegment PerShare, int PerDigits, TrainingHallSegment PerUnit, TrainingHallSegment WithdrawHeading,
    BankTable Withdraw, ShipwrightGold Gold);

/// <summary>
/// s163: <c>_endmsg</c>'s window 33 for an ending page: untitled (fill <see cref="Fill"/>, black frame and shadow) at
/// screen (<see cref="Window"/>.X, <see cref="Window"/>.Y), <see cref="LowerOffset"/> lower for blocks not in
/// <see cref="UpperBlocks"/>; line k at (<see cref="TextX"/>, <see cref="TextY"/> + <see cref="LineStep"/> · k).
/// <see cref="SceneY"/>: the screen y of each ending scene's picture origin (the street (160, 20), the banquet / cell
/// windows (160, 32)).
/// </summary>
public sealed record EndMessageWindow(PanelRect Window, int LowerOffset, IReadOnlyList<int> UpperBlocks, int TextX, int TextY,
    int LineStep, int Lines, int Fill, int TextColour, IReadOnlyDictionary<string, int> SceneY)
{
    /// <summary>The window's screen y for ending block <paramref name="block"/>.</summary>
    public int YFor(int block) => Window.Y + (UpperBlocks.Contains(block) ? 0 : LowerOffset);
}

/// <summary>
/// s163: an item window's rows (FORMAT.md 6.9 "Item lists"): row i at y = <see cref="Top"/> + <see cref="Step"/> · i, the
/// letter (none when <see cref="LetterX"/> is null), the icon, the name at y + 8, the points (ids 1–84) padded to
/// <see cref="PointsDigits"/>, the price (<see cref="PriceX"/>) and the users' digits (<see cref="UsersX"/>) when the window
/// has them.
/// </summary>
public sealed record ItemRowLayout(int Top, int Step, int Count, int? LetterX, int IconX, int NameX, int PointsX,
    int PointsDigits, int? PriceX, int PriceDigits, int? UsersX, int UsersStep);

/// <summary>s163: one item window's layout (<c>_armory</c>, <c>_trade</c> = Trade / Use / Identify, <c>_distribute</c>).</summary>
public sealed record ItemWindowLayout(ItemRowLayout Rows, IReadOnlyList<TrainingHallSegment> Headers, PanelRect Line,
    int LineColour, IReadOnlyList<PanelRect> Frames, TrainingHallSegment? More, TrainingHallSegment? NotUsable,
    (int X, int Y)? Member, (int X, int Y)? Message, int LitXor, EquipmentTable? Equipment = null);

/// <summary>s163: the item windows (the pack's <c>item_windows</c>): colours and each window's layout by its key.</summary>
public sealed record ItemWindows(int LetterColour, int TextColour, int BackColour,
    IReadOnlyDictionary<string, ItemWindowLayout> Windows);

/// <summary>The smaller pieces of IV X68000's panels (s151, FORMAT.md 6.9), each null when the pack's file lacks it.</summary>
public sealed record PanelExtras(PanelTitleStyle? Title, PanelHover? Hover, EventWaitBar? EventWait, Keypad? Keypad,
    ShipwrightWindow? Shipwright, SkillUpWindow? SkillUp = null, TrainingHallWindow? TrainingHall = null,
    IReadOnlyDictionary<string, TitledWindow>? Titled = null, FinalScreenWindow? FinalScreen = null,
    DeadMessageWindow? DeadMessage = null, TownGateWindow? TownGate = null, InnWindow? Inn = null, BankWindow? Bank = null,
    EndMessageWindow? EndMessage = null, ItemWindows? ItemWindows = null, TrainingHallWindow? MagicHall = null,
    GateBankWindow? GateBank = null)
{
    public static PanelExtras Load(string path)
    {
        var d = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path)) ?? new Dto();
        static PanelRect R(RectDto? r) => r is null ? default : new(r.X, r.Y, r.Width, r.Height);
        return new(
            d.Title is { } t ? new(t.Font, t.Colour, t.Y) : null,
            d.Hover is { } h ? new(h.IconXor, h.BoxXor) : null,
            d.EWait is { Message: { } m, Line: { } l, Button: { } b, Band: { } band } e
                ? new(m.Width, m.Height, m.Colour, R(l), l.Colour, e.Label ?? "", e.Text ?? "", e.LabelX, e.LabelY, R(b),
                    e.Key ?? "Space", R(band), band.Colour, e.PromptX, e.Letters ?? "", e.LettersText ?? "", e.Number ?? "",
                    e.NumberText ?? "")
                : null,
            d.NumIn is { Frame: { } f, Clear: { } c, Keys: { } keys, Cursor: { } cur } n
                ? new(n.Window, n.X, n.Y, n.Step, n.Size, n.Digits, new(R(f), f.Dark, f.Light), R(c), n.CellColour,
                    n.TextColour, n.BackColour, [.. keys.Select(k => new KeypadKey(k.Text ?? "", k.Key ?? "", k.Value))],
                    cur.Width, cur.Height, cur.Colour)
                : null,
            d.Ship is { Window: { } w, Lines: { } lines, Answer: { } a, Gold: { } g } s
                ? new(R(w), s.PictureHeight, s.BackColour, s.TextColour, s.Title ?? "", s.TitleText ?? "",
                    [.. lines.Select(p => (p.X, p.Y))], a.X, a.Y, a.Yes ?? "", a.No ?? "",
                    new(g.X, g.Y, g.Label ?? "", g.LabelText ?? "", g.Digits, g.Unit ?? "", g.UnitText ?? ""))
                : null,
            d.Sklup is { Window: { } sw, Under: { } su, Bar: { } sb, Title: { } st, Done: { } sd, Header: { } sh,
                Frame: { } sf, Rows: { Button: { } btn } sr, Prompt: { } sp, Trained: { } stt } k
                ? new(R(sw), R(su), k.BackColour, R(sb), sb.Colour, Line(st), Line(sd), sd.Back, Line(sh),
                    new(R(sf), sf.Dark, sf.Light),
                    new(sr.Count, sr.Top, sr.Step, sr.KeyX, sr.SkillX, sr.NowX, sr.AfterX, sr.Digits, sr.Back, R(btn)),
                    (sp.X, sp.Y), (stt.X, stt.Y), k.TextColour, k.CellColour)
                : null,
            Hall(d.Trang),
            (d.Titled ?? []).Where(p => p.Value.Window is not null).ToDictionary(p => p.Key,
                p => new TitledWindow(p.Key, R(p.Value.Window), p.Value.Title ?? "", p.Value.TitleText ?? ""),
                StringComparer.Ordinal),
            d.EndStory is { Heading: { } eh, Segments: { } es, Question: { } eq } end
                ? new(end.TextColour, end.LineStep, Seg(eh), [.. es.Select(Seg)], Seg(eq))
                : null,
            d.DeadMsg is { Screen: { } ds, Window: { } dw, Frames: { } df, Lines: { } dl } dead
                ? new(ds.Colour, R(dw), dead.Fill, [.. df.Select(R)], dead.FrameColour, dead.Font, dead.Style, dead.TextColour,
                    [.. dl.Select(Seg)])
                : null,
            d.Gate is { GrownOlder: { } go, Died: { } gd } gate
                ? new(gate.X, gate.Y, gate.Step, gate.TextColour, Seg(go), Seg(gd))
                : null,
            d.Inn is { Box: { } ib, Slept: { } isl, MpUp: { } imp } inn
                ? new(ib.X, ib.Y, ib.Width, inn.BoxPad, inn.RowStep, inn.TextX, inn.TextY, inn.TextColour, Seg(isl), Seg(imp))
                : null,
            d.Bank is { Clear: { } bc, Accounts: { } ba, Deposit: { Band: { } bankBand, Heading: { } dh, Buttons: { } bb,
                ClearPer: { } cp, PerShare: { } ps, PerUnit: { } pu } bd, Withdraw: { Heading: { } wh } bw, Gold: { } bg } bank
                ? new(R(bc), bc.Colour, bank.TextColour, Table(ba), R(bankBand), bankBand.Colour, Seg(dh), Table(bd),
                    new(bb.X, bb.Y, bb.Step, bb.Width, bb.Height, bb.Count, bb.Colour, bb.Pad?.Text ?? ""), R(cp), Seg(ps),
                    bd.PerDigits, Seg(pu), Seg(wh), Table(bw),
                    new(bg.X, bg.Y, bg.Label ?? "", bg.LabelText ?? "", bg.Digits, bg.Unit ?? "", bg.UnitText ?? ""))
                : null,
            d.EndMsg is { Window: { } ew } em
                ? new(R(ew), em.LowerOffset, em.UpperBlocks ?? [], em.TextX, em.TextY, em.LineStep, em.Lines, em.Fill,
                    em.TextColour, em.SceneY ?? [])
                : null,
            d.ItemWindows is { } iw ? Items(iw) : null,
            Hall(d.Mage),
            d.GateBank is { Pool: { } gp, PoolUnit: { } gu, Frame: { } gf, Buttons: { } gb, Columns: { } gc } gate2
                ? new(gate2.TextColour, Seg(gp), gate2.PoolDigits, Seg(gu), R(gf),
                    new(gb.X, gb.Y, gb.Step, gb.Width, gb.Height, gb.Count, gb.Colour, gb.Pad?.Text ?? ""), gate2.RowY, gate2.RowStep,
                    gc.GetValueOrDefault("name"), gc.GetValueOrDefault("share"), gate2.GainDigits)
                : null);
        TrainingHallWindow? Hall(TrangDto? tr) =>
            tr is { Window: { } tw, Name: { } tn, Notes: { } notes, ShortOfGold: { } shortOf }
                ? new(R(tw), tr.BackColour, tr.TextColour, tr.LineStep, (tn.X, tn.Y),
                    notes.ToDictionary(p => p.Key, p => (IReadOnlyList<TrainingHallSegment>)[.. p.Value.Select(Seg)]),
                    Seg(shortOf), tr.Title ?? "", tr.TitleText ?? "",
                    tr.Results?.ToDictionary(p => p.Key, p => Seg(p.Value)), tr.ResultClear is { } rc ? R(rc) : null)
                : null;
        EquipmentTable? Equipment(EquipmentDto? e) =>
            e is { Headers: { } eh, Line: { } el, Values: { } ev, Clear: { } ec, Button: { } eb, KindBox: { } ek }
                ? new(e.HeaderY, [.. eh.Select(Seg)], R(el), el.Colour, e.RowY, e.RowStep, e.NumberX, e.NumberColour, e.NameX,
                    e.ClassX, e.ClassWidth, e.ValueDigits, [.. ev.Select(v => (v.Slot ?? "", v.X))],
                    new(ec.X, 0, ec.Width, 0), ec.Colour, new(eb.X, 0, eb.Width, eb.Height), ek.Y, ek.Width, ek.Height, ek.Xor)
                : null;
        ItemWindows Items(Dictionary<string, JsonElement> iw)
        {
            int Int(string key) => iw.TryGetValue(key, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 0;
            var windows = new Dictionary<string, ItemWindowLayout>(StringComparer.Ordinal);
            foreach (var (key, e) in iw)
            {
                if (e.ValueKind != JsonValueKind.Object || e.Deserialize<ItemWindowDto>() is not { Rows: { } r } w)
                    continue;
                windows[key] = new(
                    new(r.Top, r.Step, r.Count, r.LetterX, r.IconX, r.NameX, r.PointsX, r.PointsDigits, r.PriceX, r.PriceDigits,
                        r.UsersX, r.UsersStep),
                    [.. (w.Headers ?? []).Select(Seg)], R(w.Line), w.Line?.Colour ?? 0, [.. (w.Frames ?? []).Select(R)],
                    w.More is { } more ? Seg(more) : null, w.NotUsable is { } nu ? Seg(nu) : null,
                    w.Member is { } mm ? (mm.X, mm.Y) : null, w.Message is { } msg ? (msg.X, msg.Y) : null, w.LitXor,
                    Equipment(w.Equipment));
            }
            return new(Int("letter_colour"), Int("text_colour"), Int("back_colour"), windows);
        }
        BankTable Table(BankTableDto t) => new(R(t.Frame), [.. (t.Headers ?? []).Select(Seg)], R(t.Line), t.Line?.Colour ?? 0,
            t.RowY, t.RowStep, t.Columns ?? [], t.Digits);
        static SkillUpText Line(LineDto l) => new(l.X, l.Y, l.Colour, l.Label ?? "", l.Text ?? "");
        static TrainingHallSegment Seg(SegmentDto s) =>
            new(s.X, s.Y, s.Label ?? "", s.Text ?? "", s.Number, s.Font, s.Colour, s.Style, s.Digits);
    }

    private sealed class Dto
    {
        [JsonPropertyName("title")] public TitleDto? Title { get; set; }
        [JsonPropertyName("hover")] public HoverDto? Hover { get; set; }
        [JsonPropertyName("ewait")] public EWaitDto? EWait { get; set; }
        [JsonPropertyName("numin")] public NumInDto? NumIn { get; set; }
        [JsonPropertyName("shipwright")] public ShipDto? Ship { get; set; }
        [JsonPropertyName("sklup")] public SklupDto? Sklup { get; set; }
        [JsonPropertyName("trang")] public TrangDto? Trang { get; set; }
        [JsonPropertyName("titled")] public Dictionary<string, TitledDto>? Titled { get; set; }
        [JsonPropertyName("endstory")] public EndStoryDto? EndStory { get; set; }
        [JsonPropertyName("deadmsg")] public DeadMsgDto? DeadMsg { get; set; }
        [JsonPropertyName("townent")] public GateDto? Gate { get; set; }
        [JsonPropertyName("inn")] public InnDto? Inn { get; set; }
        [JsonPropertyName("bank")] public BankDto? Bank { get; set; }
        [JsonPropertyName("endmsg")] public EndMsgDto? EndMsg { get; set; }
        [JsonPropertyName("item_windows")] public Dictionary<string, JsonElement>? ItemWindows { get; set; }
        [JsonPropertyName("mage")] public TrangDto? Mage { get; set; }
        [JsonPropertyName("gatebank")] public GateBankDto? GateBank { get; set; }
    }

    private sealed class GateBankDto
    {
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("pool")] public SegmentDto? Pool { get; set; }
        [JsonPropertyName("pool_digits")] public int PoolDigits { get; set; }
        [JsonPropertyName("pool_unit")] public SegmentDto? PoolUnit { get; set; }
        [JsonPropertyName("frame")] public RectDto? Frame { get; set; }
        [JsonPropertyName("buttons")] public BankButtonsDto? Buttons { get; set; }
        [JsonPropertyName("row_y")] public int RowY { get; set; }
        [JsonPropertyName("row_step")] public int RowStep { get; set; }
        [JsonPropertyName("columns")] public Dictionary<string, int>? Columns { get; set; }
        [JsonPropertyName("gain_digits")] public int GainDigits { get; set; }
    }

    private sealed class EquipmentValueDto
    {
        [JsonPropertyName("slot")] public string? Slot { get; set; }
        [JsonPropertyName("x")] public int X { get; set; }
    }

    private sealed class KindBoxDto
    {
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("xor")] public int Xor { get; set; }
    }

    private sealed class EquipmentDto
    {
        [JsonPropertyName("header_y")] public int HeaderY { get; set; }
        [JsonPropertyName("headers")] public List<SegmentDto>? Headers { get; set; }
        [JsonPropertyName("line")] public RectDto? Line { get; set; }
        [JsonPropertyName("row_y")] public int RowY { get; set; }
        [JsonPropertyName("row_step")] public int RowStep { get; set; }
        [JsonPropertyName("number_x")] public int NumberX { get; set; }
        [JsonPropertyName("number_colour")] public int NumberColour { get; set; }
        [JsonPropertyName("name_x")] public int NameX { get; set; }
        [JsonPropertyName("class_x")] public int ClassX { get; set; }
        [JsonPropertyName("class_width")] public int ClassWidth { get; set; }
        [JsonPropertyName("value_digits")] public int ValueDigits { get; set; }
        [JsonPropertyName("values")] public List<EquipmentValueDto>? Values { get; set; }
        [JsonPropertyName("clear")] public RectDto? Clear { get; set; }
        [JsonPropertyName("button")] public RectDto? Button { get; set; }
        [JsonPropertyName("kind_box")] public KindBoxDto? KindBox { get; set; }
    }

    private sealed class ItemRowsDto
    {
        [JsonPropertyName("top")] public int Top { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("letter_x")] public int? LetterX { get; set; }
        [JsonPropertyName("icon_x")] public int IconX { get; set; }
        [JsonPropertyName("name_x")] public int NameX { get; set; }
        [JsonPropertyName("points_x")] public int PointsX { get; set; }
        [JsonPropertyName("points_digits")] public int PointsDigits { get; set; }
        [JsonPropertyName("price_x")] public int? PriceX { get; set; }
        [JsonPropertyName("price_digits")] public int PriceDigits { get; set; }
        [JsonPropertyName("users_x")] public int? UsersX { get; set; }
        [JsonPropertyName("users_step")] public int UsersStep { get; set; }
    }

    private sealed class ItemWindowDto
    {
        [JsonPropertyName("rows")] public ItemRowsDto? Rows { get; set; }
        [JsonPropertyName("headers")] public List<SegmentDto>? Headers { get; set; }
        [JsonPropertyName("line")] public RectDto? Line { get; set; }
        [JsonPropertyName("frames")] public List<RectDto>? Frames { get; set; }
        [JsonPropertyName("more")] public SegmentDto? More { get; set; }
        [JsonPropertyName("not_usable")] public SegmentDto? NotUsable { get; set; }
        [JsonPropertyName("member")] public RectDto? Member { get; set; }
        [JsonPropertyName("message")] public RectDto? Message { get; set; }
        [JsonPropertyName("lit_xor")] public int LitXor { get; set; }
        [JsonPropertyName("equipment")] public EquipmentDto? Equipment { get; set; }
    }

    private sealed class InnDto
    {
        [JsonPropertyName("box")] public RectDto? Box { get; set; }
        [JsonPropertyName("box_pad")] public int BoxPad { get; set; }
        [JsonPropertyName("row_step")] public int RowStep { get; set; }
        [JsonPropertyName("text_x")] public int TextX { get; set; }
        [JsonPropertyName("text_y")] public int TextY { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("slept")] public SegmentDto? Slept { get; set; }
        [JsonPropertyName("mp_up")] public SegmentDto? MpUp { get; set; }
    }

    private class BankTableDto
    {
        [JsonPropertyName("frame")] public RectDto? Frame { get; set; }
        [JsonPropertyName("headers")] public List<SegmentDto>? Headers { get; set; }
        [JsonPropertyName("line")] public RectDto? Line { get; set; }
        [JsonPropertyName("row_y")] public int RowY { get; set; }
        [JsonPropertyName("row_step")] public int RowStep { get; set; }
        [JsonPropertyName("columns")] public Dictionary<string, int>? Columns { get; set; }
        [JsonPropertyName("digits")] public int Digits { get; set; }
        [JsonPropertyName("band")] public RectDto? Band { get; set; }
        [JsonPropertyName("heading")] public SegmentDto? Heading { get; set; }
    }

    private sealed class BankDepositDto : BankTableDto
    {
        [JsonPropertyName("buttons")] public BankButtonsDto? Buttons { get; set; }
        [JsonPropertyName("clear_per")] public RectDto? ClearPer { get; set; }
        [JsonPropertyName("per_share")] public SegmentDto? PerShare { get; set; }
        [JsonPropertyName("per_digits")] public int PerDigits { get; set; }
        [JsonPropertyName("per_unit")] public SegmentDto? PerUnit { get; set; }
    }

    private sealed class BankButtonsDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("colour")] public int Colour { get; set; }
        [JsonPropertyName("pad")] public SegmentDto? Pad { get; set; }
    }

    private sealed class BankDto
    {
        [JsonPropertyName("clear")] public RectDto? Clear { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("accounts")] public BankTableDto? Accounts { get; set; }
        [JsonPropertyName("deposit")] public BankDepositDto? Deposit { get; set; }
        [JsonPropertyName("withdraw")] public BankTableDto? Withdraw { get; set; }
        [JsonPropertyName("gold")] public GoldDto? Gold { get; set; }
    }

    private sealed class EndMsgDto
    {
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("lower_offset")] public int LowerOffset { get; set; }
        [JsonPropertyName("upper_blocks")] public List<int>? UpperBlocks { get; set; }
        [JsonPropertyName("text_x")] public int TextX { get; set; }
        [JsonPropertyName("text_y")] public int TextY { get; set; }
        [JsonPropertyName("line_step")] public int LineStep { get; set; }
        [JsonPropertyName("lines")] public int Lines { get; set; }
        [JsonPropertyName("fill")] public int Fill { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("scene_y")] public Dictionary<string, int>? SceneY { get; set; }
    }

    private sealed class GateDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("grown_older")] public SegmentDto? GrownOlder { get; set; }
        [JsonPropertyName("died")] public SegmentDto? Died { get; set; }
    }

    private sealed class DeadMsgDto
    {
        [JsonPropertyName("screen")] public RectDto? Screen { get; set; }
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("fill")] public int Fill { get; set; }
        [JsonPropertyName("frames")] public List<RectDto>? Frames { get; set; }
        [JsonPropertyName("frame_colour")] public int FrameColour { get; set; }
        [JsonPropertyName("font")] public int Font { get; set; }
        [JsonPropertyName("style")] public int Style { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("lines")] public List<SegmentDto>? Lines { get; set; }
    }

    private sealed class EndStoryDto
    {
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("line_step")] public int LineStep { get; set; }
        [JsonPropertyName("heading")] public SegmentDto? Heading { get; set; }
        [JsonPropertyName("segments")] public List<SegmentDto>? Segments { get; set; }
        [JsonPropertyName("question")] public SegmentDto? Question { get; set; }
    }

    private sealed class TitledDto
    {
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("title_text")] public string? TitleText { get; set; }
    }

    private sealed class SegmentDto
    {
        [JsonPropertyName("x")] public int? X { get; set; }
        [JsonPropertyName("y")] public int? Y { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("number")] public bool Number { get; set; }
        [JsonPropertyName("font")] public int Font { get; set; }
        [JsonPropertyName("colour")] public int? Colour { get; set; }
        [JsonPropertyName("style")] public int Style { get; set; }
        [JsonPropertyName("digits")] public int Digits { get; set; }
    }

    private sealed class TrangDto
    {
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("line_step")] public int LineStep { get; set; }
        [JsonPropertyName("name")] public RectDto? Name { get; set; }
        [JsonPropertyName("notes")] public Dictionary<string, List<SegmentDto>>? Notes { get; set; }
        [JsonPropertyName("short_of_gold")] public SegmentDto? ShortOfGold { get; set; }
        [JsonPropertyName("results")] public Dictionary<string, SegmentDto>? Results { get; set; }
        [JsonPropertyName("result_clear")] public RectDto? ResultClear { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("title_text")] public string? TitleText { get; set; }
    }

    private sealed class LineDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("colour")] public int Colour { get; set; }
        [JsonPropertyName("back")] public int Back { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class SklupRowsDto
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("top")] public int Top { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("key_x")] public int KeyX { get; set; }
        [JsonPropertyName("skill_x")] public int SkillX { get; set; }
        [JsonPropertyName("now_x")] public int NowX { get; set; }
        [JsonPropertyName("after_x")] public int AfterX { get; set; }
        [JsonPropertyName("digits")] public int Digits { get; set; }
        [JsonPropertyName("back")] public int Back { get; set; }
        [JsonPropertyName("button")] public RectDto? Button { get; set; }
    }

    private sealed class SklupDto
    {
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("under")] public RectDto? Under { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("bar")] public RectDto? Bar { get; set; }
        [JsonPropertyName("title")] public LineDto? Title { get; set; }
        [JsonPropertyName("done")] public LineDto? Done { get; set; }
        [JsonPropertyName("header")] public LineDto? Header { get; set; }
        [JsonPropertyName("frame")] public RectDto? Frame { get; set; }
        [JsonPropertyName("rows")] public SklupRowsDto? Rows { get; set; }
        [JsonPropertyName("prompt")] public RectDto? Prompt { get; set; }
        [JsonPropertyName("trained")] public RectDto? Trained { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("cell_colour")] public int CellColour { get; set; }
    }

    private sealed class RectDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("colour")] public int Colour { get; set; }
        [JsonPropertyName("dark")] public int Dark { get; set; }
        [JsonPropertyName("light")] public int Light { get; set; }
    }

    private sealed class TitleDto
    {
        [JsonPropertyName("font")] public int Font { get; set; }
        [JsonPropertyName("colour")] public int Colour { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
    }

    private sealed class HoverDto
    {
        [JsonPropertyName("icon_xor")] public int IconXor { get; set; }
        [JsonPropertyName("box_xor")] public int BoxXor { get; set; }
    }

    private sealed class EWaitDto
    {
        [JsonPropertyName("message")] public RectDto? Message { get; set; }
        [JsonPropertyName("line")] public RectDto? Line { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("label_x")] public int LabelX { get; set; }
        [JsonPropertyName("label_y")] public int LabelY { get; set; }
        [JsonPropertyName("button")] public RectDto? Button { get; set; }
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("band")] public RectDto? Band { get; set; }
        [JsonPropertyName("prompt_x")] public int PromptX { get; set; }
        [JsonPropertyName("letters")] public string? Letters { get; set; }
        [JsonPropertyName("letters_text")] public string? LettersText { get; set; }
        [JsonPropertyName("number")] public string? Number { get; set; }
        [JsonPropertyName("number_text")] public string? NumberText { get; set; }
    }

    private sealed class KeyDto
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("value")] public int Value { get; set; }
    }

    private sealed class NumInDto
    {
        [JsonPropertyName("window")] public int Window { get; set; }
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("size")] public int Size { get; set; }
        [JsonPropertyName("digits")] public int Digits { get; set; }
        [JsonPropertyName("frame")] public RectDto? Frame { get; set; }
        [JsonPropertyName("clear")] public RectDto? Clear { get; set; }
        [JsonPropertyName("cell_colour")] public int CellColour { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("keys")] public List<KeyDto>? Keys { get; set; }
        [JsonPropertyName("cursor")] public RectDto? Cursor { get; set; }
    }

    private sealed class AnswerDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("yes")] public string? Yes { get; set; }
        [JsonPropertyName("no")] public string? No { get; set; }
    }

    private sealed class GoldDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("label_text")] public string? LabelText { get; set; }
        [JsonPropertyName("digits")] public int Digits { get; set; }
        [JsonPropertyName("unit")] public string? Unit { get; set; }
        [JsonPropertyName("unit_text")] public string? UnitText { get; set; }
    }

    private sealed class ShipDto
    {
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("picture_height")] public int PictureHeight { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("title_text")] public string? TitleText { get; set; }
        [JsonPropertyName("lines")] public List<RectDto>? Lines { get; set; }
        [JsonPropertyName("answer")] public AnswerDto? Answer { get; set; }
        [JsonPropertyName("gold")] public GoldDto? Gold { get; set; }
    }
}

public sealed partial class CommandPanelSet
{
    /// <summary>The title, hover look, dungeon wait bar, keypad, shipwright window (s151) and level-up skill window (s152); never null after <see cref="Load"/>.</summary>
    public PanelExtras Extras { get; init; } = new(null, null, null, null, null);
}
