using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Town Option → U)SE ITEM (OV1 <c>0x30f1</c>, manual p.12): "WHO WILL USE?" (member picker <c>0x40e5</c>, on the shared
/// <see cref="MemberCursor"/> row: Up/Down + Return, or the digit), then the member's
/// items. A potion (101–120) asks "ON WHOM:" and is drunk (<see cref="Magic.UsePotion"/>); a scroll (121–140) shows its
/// text and is kept (DOS also offers "Print scroll? (y/n)" — no printer here). Anything else: DOS prints "UNUSABLE";
/// D-061 (user s53, manual "find the unusual properties of an item") shows <see cref="GameSession.DescribeItem"/>. The
/// worn weapon / armor / shield are listed after the carried items so their stats can be read too (remaster).
/// </summary>
public sealed class UseItemDialog : IModalDialog, IPagedDialog
{
    private enum Step { Closed, User, Item, Target, Text, Option, PrintAsk, PrinterAsk }

    public static string UserTitle => UiText.S("useitem.who_will_use");

    /// <summary>Characters per line of the text panel (<see cref="UiOverlays.DrawTextPanel"/>).</summary>
    private const int Width = 48;

    private readonly GameDataRepository _data;
    private readonly MemberCursor _members;
    private Step _step;
    private Character? _user;
    private int _slot;
    private int _campaignItem;
    private IReadOnlyList<string> _campaignOptions = [];
    private List<string> _text = [];
    private int _scroll;

    public UseItemDialog(GameDataRepository data, MemberCursor? members = null)
    {
        _data = data;
        _members = members ?? new MemberCursor();
    }

    public bool IsOpen => _step != Step.Closed;

    /// <summary>
    /// The scroll being read when it is a picture rather than text (III's Scroll IV, item 126: the Amiga shows the
    /// full-screen picture <c>D/s6</c>, <c>0x1f7ee</c>); null otherwise. The Client draws the pack's
    /// <see cref="PictureNames.ScrollPicture"/> in place of the text panel when it has one.
    /// </summary>
    public int? PictureItem => _step == Step.Text ? _pictureItem : null;

    private int? _pictureItem;

    /// <summary>
    /// The scroll item (121–140) whose text is being read, or null. A pack with a <see cref="ScrollFrameLayout"/> (III
    /// Amiga) draws the text on its parchment (<c>0x1e2c2</c>) instead of the text panel.
    /// </summary>
    public int? ScrollItem => _step == Step.Text && _pictureItem is null ? _scrollItem : null;

    /// <summary>Counts the scrolls opened, so the Client picks a scroll frame's random edges once per reading.</summary>
    public int ScrollReads { get; private set; }

    /// <summary>
    /// s163: I Amiga's town under AMIGA SCREENS (set by the Client): the list tones count (<see cref="Tones"/>) and the
    /// printout questions are asked (h29 <c>+0x24e</c>, FORMAT.md "Sound effects"). A scroll then asks "DO YOU WANT PRINTOUT?" before its text (<see cref="AskingPrintout"/>): NO shows the
    /// text; YES asks "PREPARE PRINTER" (<see cref="AskingPrinter"/>): O.K. prints the text (<see cref="Printer"/>) and shows
    /// nothing, ABORT shows the text. Off: the text shows at once (DOS's order; DOS asks its own question after it).
    /// </summary>
    public bool AmigaTown { get; set; }

    /// <summary>s163: the remaster's printer for a scroll's lines: returns the line to print (a file written), or throws.</summary>
    public Func<IReadOnlyList<string>, string>? Printer { get; set; }

    /// <summary>"DO YOU WANT PRINTOUT?" is up (h29 <c>+0x1da</c>).</summary>
    public bool AskingPrintout => _step == Step.PrintAsk;

    /// <summary>"PREPARE PRINTER" is up (h29 <c>+0x1f6</c>), after the scroll's YES or a failed print.</summary>
    public bool AskingPrinter => _step == Step.PrinterAsk;

    /// <summary>
    /// s162 / s163: counts I Amiga's beeps of this dialog (h26 <c>+0x6e</c>'s, before each printout question; with
    /// <see cref="AmigaTown"/> off, one as a scroll's text shows, where the Amiga asks its question).
    /// </summary>
    public int PrintoutBeeps { get; private set; }

    /// <summary>
    /// s163: counts I Amiga's list tones of this dialog (h89 <c>+0xd2</c>): the member chooser's cancel (h23 <c>+0x22a</c>),
    /// the item list's answer (h26 <c>+0x16a</c>) and the close of a scroll's text (h29 <c>+0x318</c>).
    /// </summary>
    public int Tones { get; private set; }

    /// <summary>s163: counts the potions drunk (the Amiga's "D O N E !" after each, h8 <c>+0x14ba</c> / <c>+0x1540</c>).</summary>
    public int PotionsDrunk { get; private set; }

    private int? _scrollItem;

    public string Help => _step switch
    {
        Step.Text => UiText.S("useitem.up_down_scroll_any_key_back"),
        Step.PrintAsk => UiText.S("useitem.yes_no_help"),
        Step.PrinterAsk => UiText.S("useitem.ok_abort_help"),
        Step.Closed => "",
        _ => UiText.S("useitem.1_9_choose_esc_cancel"),
    };

    /// <summary>The rows offered for <paramref name="ch"/>: held carried slots (index ≥ 0; empty slots skipped), then worn
    /// items (index −1).</summary>
    public static IReadOnlyList<(int Slot, int ItemId, bool Worn)> Rows(Character ch) =>
        [.. ch.InventoryItemIds.Select((id, i) => (i, id, false)).Where(r => r.id != 0),
         .. new[] { ch.WornWeapon, ch.WornArmor, ch.WornShield }.Where(id => id > 0).Select(id => (-1, id, true))];

    public string? Open(GameSession session)
    {
        if (session.Mode != GameMode.Town || session.Party.Members.Count == 0)
            return UiText.S("useitem.only_in_town");
        _step = Step.User;
        _user = null;
        return null;
    }

    public ListPage? Page => _step == Step.Text ? ListPage.Of(0, InspectDialog.MaxLines, _scroll, _text.Count) : null;

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) => _step switch
    {
        Step.User => [UserTitle, .. session.Party.Members.Select((m, i) =>
            $"{(i == _members.Clamp(session.Party.Members.Count) ? ">" : " ")}{i + 1} {m.Name}")],
        Step.Item => [$"{_user!.Name}:", .. Rows(_user).Take(9).Select((r, i) =>
            $"{i + 1} {ItemName(r.ItemId)}{(r.Worn ? " (worn)" : "")}")],
        Step.Target => [UiText.S("useitem.on_whom"), .. session.Party.Members.Select((m, i) => UiText.F("useitem.x_x_x_x_mp_x", i + 1, m.Name, m.HpCur, m.HpMax, m.MagicCur, m.MagicMax))],
        Step.Text => _text.Skip(_scroll).Take(InspectDialog.MaxLines).ToList(),
        Step.Option => [ItemName(_campaignItem), .. _campaignOptions.Select((o, i) => $"{i + 1} {o}")],
        Step.PrintAsk => P1MapRequesters.Parse(UiText.S("p1field.req.printout")).Lines,
        Step.PrinterAsk => P1MapRequesters.Parse(UiText.S("p1field.req.prepare_printer")).Lines,
        _ => [],
    };

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (_step == Step.Text)
            {
                if (key is Keys.Up or Keys.Down)
                {
                    _scroll = Math.Clamp(_scroll + (key == Keys.Down ? 1 : -1), 0, Math.Max(0, _text.Count - InspectDialog.MaxLines));
                    continue;
                }
                if (AmigaTown && _scrollItem is not null)
                    Tones++; // h29 +0x318: the tone after the scroll's requester 19
                _step = Step.Item; // back to the member's list
                return "";
            }
            if (_step == Step.PrintAsk)
            {
                if (key is Keys.Y or Keys.Enter)
                {
                    _step = Step.PrinterAsk; // h29 +0x1f6
                    PrintoutBeeps++;
                }
                else if (key is Keys.N or Keys.Escape or Keys.Back)
                    _step = Step.Text;
                continue;
            }
            if (_step == Step.PrinterAsk)
            {
                if (key is Keys.Y or Keys.O or Keys.Enter)
                {
                    try
                    {
                        var printed = Printer?.Invoke(_text) ?? throw new InvalidOperationException("no printer");
                        _step = Step.Item; // printed: no text, no tone (h29 +0x2b6)
                        return printed;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                    {
                        PrintoutBeeps++; // asked again (h29 +0x200's loop), with its beep
                        return UiText.F("game.printer_not_ready_x", ex.Message);
                    }
                }
                if (key is Keys.N or Keys.A or Keys.Escape or Keys.Back)
                    _step = Step.Text; // ABORT shows the text (h29 +0x2c4)
                continue;
            }
            if (key is Keys.Escape or Keys.Back)
            {
                if (AmigaTown && _step is Step.User or Step.Item or Step.Target)
                    Tones++; // h23 +0x22a: a member chooser's cancel (WHO WILL USE?, ON WHOM?); h26 +0x16a: the item list's
                _step = Step.Closed;
                return "";
            }
            if (_step == Step.User && _members.Move(key, session.Party.Members.Count))
                continue;
            var digit = _step == Step.User && key == Keys.Enter && session.Party.Members.Count > 0
                ? _members.Clamp(session.Party.Members.Count) + 1
                : DialogKeys.Digit(key);
            if (digit <= 0)
                continue;
            switch (_step)
            {
                case Step.User when digit <= session.Party.Members.Count:
                    _members.Row = digit - 1; // the shared member-list cursor (s58)
                    _user = session.Party.Members[digit - 1];
                    if (Rows(_user).Count == 0)
                    {
                        _step = Step.Closed;
                        return UiText.F("useitem.x_carries_nothing", _user.Name);
                    }
                    _step = Step.Item;
                    return null;
                case Step.Item when digit <= Math.Min(9, Rows(_user!).Count):
                    var row = Rows(_user!)[digit - 1];
                    if (AmigaTown)
                        Tones++; // h26 +0x16a: the tone after the item list's answer
                    // The game's own item uses (II: amulets EXAMINE / WEAR / LEAVE, the wand of Filmon).
                    if (!row.Worn && session.ItemUseOptions(_user!, row.ItemId) is { } options)
                    {
                        if (options.Count == 0)
                        {
                            _step = Step.Closed;
                            return session.UseCampaignItem(_user!, row.ItemId).Message;
                        }
                        _campaignItem = row.ItemId;
                        _campaignOptions = options;
                        _step = Step.Option;
                        return null;
                    }
                    if (!row.Worn && Magic.IsPotion(row.ItemId))
                    {
                        _slot = row.Slot;
                        _step = Step.Target;
                        return null;
                    }
                    ShowText(row.ItemId is >= 121 and <= 140 ? ScrollLines(row.ItemId) : session.DescribeItem(row.ItemId));
                    _pictureItem = _data.ScrollItemRecords.TryGetValue(row.ItemId, out var records) && records.Count == 0
                        ? row.ItemId : null;
                    _scrollItem = row.ItemId is >= 121 and <= 140 ? row.ItemId : null;
                    if (_scrollItem is not null)
                    {
                        ScrollReads++;
                        PrintoutBeeps++;
                        if (AmigaTown && _pictureItem is null)
                            _step = Step.PrintAsk; // h29 +0x1da: the question before the text
                    }
                    return null;
                case Step.Option when digit <= _campaignOptions.Count:
                    _step = Step.Closed;
                    return session.UseCampaignItem(_user!, _campaignItem, digit - 1).Message;
                case Step.Target when digit <= session.Party.Members.Count:
                    _step = Step.Closed;
                    var drunk = Magic.UsePotion(_user!, session.Party.Members[digit - 1], _slot);
                    if (drunk.Success)
                        PotionsDrunk++;
                    return drunk.Message;
            }
        }
        return null;
    }

    private string ItemName(int id) => _data.Items.FirstOrDefault(i => i.Id == id)?.Name ?? $"#{id}";

    /// <summary>
    /// SCROLL n's text (OV1 <c>0x45ba</c>: <c>scrolls.dtx</c> entry n − 121, its 20 lines). A game with an item → records
    /// table (III, <c>read_scroll</c> root <c>0xeb</c>, <c>planes.md</c> §7) shows those <c>SCROLLS.DAT</c> records as pages
    /// (an empty line between them); a scroll with no record (III's Scroll IV) is a picture: the pack's picture is drawn (<see cref="PictureItem"/>), else its name.
    /// </summary>
    private IReadOnlyList<string> ScrollLines(int itemId) => ScrollLines(_data, itemId, ItemName(itemId));

    public static IReadOnlyList<string> ScrollLines(GameDataRepository data, int itemId, string name)
    {
        if (data.ScrollItemRecords.TryGetValue(itemId, out var records))
        {
            if (records.Count == 0)
                return [name, UiText.S("useitem.scroll_picture")];
            var pages = records.Select(r => data.Scrolls.FirstOrDefault(sc => sc.Id == r))
                .Where(sc => sc is not null)
                .Select(sc => sc!.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList())
                .ToList();
            return [name, .. pages.SelectMany((p, i) => i == 0 ? p : ["", .. p])];
        }
        return data.Scrolls.FirstOrDefault(sc => sc.Id == itemId - 120) is { } scroll
            ? [name, .. scroll.Lines.Select(l => l.TrimEnd())]
            : [name];
    }

    private void ShowText(IReadOnlyList<string> lines)
    {
        _text = [lines[0], .. lines.Skip(1).SelectMany(l => Wrap(l, Width))];
        _scroll = 0;
        _step = Step.Text;
    }

    /// <summary>Word-wraps <paramref name="text"/> to <paramref name="width"/> layout columns, measured with the font's widths (an empty line stays one line).</summary>
    public static IEnumerable<string> Wrap(string text, int width)
    {
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && PixelFont.Measure(line + " " + word) > width * PixelFont.Advance)
            {
                yield return line;
                line = "";
            }
            line = line.Length == 0 ? word : line + " " + word;
        }
        yield return line;
    }
}
