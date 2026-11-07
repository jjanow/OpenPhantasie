using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// III's DOS bank, "Deposit some gold" (<c>p3.bank</c> = <c>dos</c>, ovl2 <c>0x297a</c>): an amount field that opens holding
/// the party's gold; Return deposits it (<see cref="GameSession.DosBankDeposit"/>: split evenly, the remainder 1 gold at a
/// time to random members). Digits shift in from the right, Backspace drops the last one, Esc leaves. The field's look
/// is the remaster's (DOS III's screen was not captured).
/// </summary>
public sealed class DosDepositDialog : IModalDialog
{
    private long _amount;

    public bool IsOpen { get; private set; }

    public string Help => UiText.S("towndlg.type_amount_back_clear_digit_enter");

    public void Open(GameSession session)
    {
        IsOpen = true;
        _amount = session.Party.GoldCarried;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) =>
        IsOpen
            ? [UiText.F("towndlg.gold_carried_x", session.Party.GoldCarried), UiText.F("p3town.how_much_to_deposit_x", _amount)]
            : [];

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (!IsOpen)
                return null;
            if (key is Keys.Escape)
            {
                IsOpen = false;
                return "";
            }
            if (key is Keys.Back or Keys.Delete)
            {
                _amount /= 10;
                continue;
            }
            var d = DialogKeys.Digit(key);
            if (d >= 0)
            {
                var shifted = _amount * 10 + d;
                if (shifted <= session.Party.GoldCarried)
                    _amount = shifted;
                continue;
            }
            if (key == Keys.Enter)
            {
                IsOpen = false;
                return session.DosBankDeposit(_amount) ? session.LastMessage : UiText.S("p3town.nothing_deposited");
            }
        }
        return null;
    }
}

/// <summary>
/// III's town Trade Items (manual III PDF 6: "allows an adventurer to give an item to another adventurer, or sell it by
/// trading it to adventurer 7"; D3-027): pick the giver (1–6), one of its items (1–9), then the receiver (1–6) or 7 to
/// sell (manual rule only; <c>give-only</c> = the Amiga's Trade has no 7). After each move the dialog goes back to "who
/// will trade?", as the Amiga's does (seen s116: "Whose item shall be traded?" again, no message); Esc steps back one
/// level. Worn items may be traded: both members re-ready their equipment.
/// </summary>
public sealed class TradeDialog : IModalDialog, IAmigaWindowDialog
{
    private const int SellChoice = 7;

    private Character? _giver;
    private int? _slot;
    private int _cursor;

    /// <summary>
    /// III Amiga's Trade windows (s117, FORMAT.md §8): "Whose item shall be traded?" (the party: name, race, level, class,
    /// status), "Trade which item?" (the giver's items), "Who gets the item?" (the party, plus the sale row under the
    /// manual's rule); Up / Down pick (yellow), Return or O = OK, C = CANCEL (leaves), digits as before.
    /// </summary>
    public AmigaWindowView? View(GameSession session, GameDataRepository data, KeepWindowsLayout layout)
    {
        if (!IsOpen)
            return null;
        var item = _giver is not null && _slot is null;
        if ((item ? layout.TradeItem : layout.TradeWho) is not { Text.Count: >= 2 } w)
            return null;
        var (title, first) = (w.Text[0], w.Text[1]);
        var rows = new List<AmigaWindowRow>();
        var picks = new List<(int, Keys)>();
        var choices = Choices(session, data);
        rows.Add(new(title.X, title.Y, _giver is null ? UiText.S("p3town.trade_whose") : item ? UiText.S("p3town.trade_which") : UiText.S("p3town.trade_who_gets"), false));
        for (var i = 0; i < choices.Count; i++)
        {
            rows.Add(new(first.X, first.Y + 8 * i, choices[i].Text, i == Math.Min(_cursor, choices.Count - 1)));
            picks.Add((rows.Count - 1, choices[i].Key));
        }
        return new AmigaWindowView(w, rows, picks);
    }

    /// <summary>The rows of the current step and the key that picks each.</summary>
    private List<(string Text, Keys Key)> Choices(GameSession session, GameDataRepository data)
    {
        var members = session.Party.Members.Select((m, i) => (
            $"{m.Name,-10} {UiText.Name(m.Race),-9} {m.Level,2} {UiText.Name(m.Class),-8} {StatusWord(m)}",
            DigitKey(i + 1))).ToList();
        if (_giver is null)
            return members;
        if (_slot is null)
            return [.. _giver.InventoryItemIds.Select((id, i) => (id, i)).Where(x => x.id != 0)
                .Select(x => ($"{ItemName(data, x.id)}{Worn(_giver, x.id)}", DigitKey(x.i + 1)))];
        if (session.CanTradeSell)
            members.Add((UiText.F("p3town.trade_sell_x", session.TradeSalePrice(_giver.InventoryItemIds[_slot.Value])),
                DigitKey(SellChoice)));
        return members;
    }

    private static Keys DigitKey(int d) => Keys.D0 + d;

    private static string StatusWord(Character m) => m.Status switch
    {
        CharacterStatus.Dead => UiText.S("health.dead"),
        CharacterStatus.Sleep => UiText.S("health.sleep"),
        _ => UiText.S("health.okay"),
    };

    public bool IsOpen { get; private set; }

    public string Help => _slot is not null ? UiText.S("p3town.trade_to_help") : UiText.S("towndlg.arrows_enter_or_1_6_choose");

    public string? Open(GameSession session)
    {
        if (!session.CanTrade)
            return UiText.S("p3town.no_trade");
        if (session.Party.Members.Count == 0)
            return UiText.S("game.no_party");
        IsOpen = true;
        _giver = null;
        _slot = null;
        _cursor = 0;
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        if (!IsOpen)
            return [];
        var members = session.Party.Members.Select((m, i) => $"{i + 1} {m.Name}  {CarriedItems.Count(m)}/9");
        if (_giver is null)
            return [UiText.S("p3town.who_will_trade"), .. members];
        if (_slot is not { } slot)
            return
            [
                UiText.F("p3town.x_carries", _giver.Name),
                .. _giver.InventoryItemIds.Select((id, i) => id == 0 ? "" : $"{i + 1} {ItemName(data, id)}{Worn(_giver, id)}"),
            ];
        var itemId = _giver.InventoryItemIds[slot];
        List<string> lines = [UiText.F("p3town.trade_x_to_whom", ItemName(data, itemId)), .. members];
        if (session.CanTradeSell)
            lines.Add(UiText.F("p3town.7_sell_for_x", session.TradeSalePrice(itemId)));
        return lines;
    }

    private static string ItemName(GameDataRepository data, int id) => data.Items.FirstOrDefault(i => i.Id == id)?.Name ?? $"#{id}";

    private static string Worn(Character c, int id) =>
        id == c.WornWeapon || id == c.WornArmor || id == c.WornShield || id == c.WornBow ? " *" : "";

    /// <summary>The data the current step's rows need (item names); set by the front end before keys.</summary>
    public GameDataRepository? Data { get; set; }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var raw in newKeys)
        {
            if (!IsOpen)
                return null;
            var key = raw;
            if (key == Keys.C) // the Amiga's CANCEL leaves Trade
            {
                IsOpen = false;
                return "";
            }
            if (key is Keys.Up or Keys.Down && Data is { } data)
            {
                var n = Choices(session, data).Count;
                _cursor = n == 0 ? 0 : (_cursor + (key == Keys.Up ? n - 1 : 1)) % n;
                continue;
            }
            if (key is Keys.Enter or Keys.O && Data is { } d0 && Choices(session, d0) is { Count: > 0 } list)
                key = list[Math.Min(_cursor, list.Count - 1)].Key;
            if (key is Keys.Escape or Keys.Back)
            {
                _cursor = 0;
                if (_slot is not null)
                    _slot = null;
                else if (_giver is not null)
                    _giver = null;
                else
                {
                    IsOpen = false;
                    return "";
                }
                continue;
            }
            var d = DialogKeys.Digit(key);
            var count = session.Party.Members.Count;
            if (_giver is null)
            {
                if (d >= 1 && d <= count)
                {
                    _giver = session.Party.Members[d - 1];
                    _cursor = 0;
                }
                continue;
            }
            if (_slot is not { } slot)
            {
                if (d >= 1 && d <= _giver.InventoryItemIds.Count && _giver.InventoryItemIds[d - 1] != 0)
                {
                    _slot = d - 1;
                    _cursor = 0;
                }
                continue;
            }
            P3TradeResult? result = null;
            if (d >= 1 && d <= count)
                result = session.TradeGive(_giver, slot, session.Party.Members[d - 1]);
            else if (d == SellChoice && session.CanTradeSell)
                result = session.TradeSell(_giver, slot);
            if (result is null)
                continue;
            _slot = null; // back to "who will trade?" (the Amiga's Trade, s116)
            _giver = null;
            _cursor = 0;
            return result switch
            {
                P3TradeResult.SameMember => UiText.S("p3town.same_member"),
                _ => session.LastMessage,
            };
        }
        return null;
    }
}

/// <summary>One text row of an Amiga list window: where it starts, its text, and whether it is yellow (a value or the pick).</summary>
public sealed record AmigaWindowRow(int X, int Y, string Text, bool Highlight);

/// <summary>An Amiga window to draw (s117): the pack's window, its rows, and the row a click picks (row index → key).</summary>
public sealed record AmigaWindowView(KeepWindowsLayout.Window Window, IReadOnlyList<AmigaWindowRow> Rows,
    IReadOnlyList<(int Row, Keys Key)> Picks);

/// <summary>A dialog that draws as the original's own window when the pack has one (III Amiga, s117).</summary>
public interface IAmigaWindowDialog
{
    /// <summary>The window now, or null to fall back to the remaster's panel.</summary>
    AmigaWindowView? View(GameSession session, GameDataRepository data, KeepWindowsLayout layout);
}

/// <summary>
/// III Amiga's Bank (window 9, FORMAT.md §8, s117): "The party is in the bank." with the party's gold and the members'
/// total in the bank, two picks and OK / CANCEL. Up / Down pick, Return or O = OK, C or Esc = CANCEL; 1 / 2 pick and OK.
/// Under the DOS bank rules (<c>p3.bank</c> = <c>dos</c>, the Amiga's own): "Withdraw all gold" closes at once, "Deposit
/// some gold" asks "How much:" in the same window with an empty field (the Amiga; DOS opens it holding the gold) and
/// Return deposits it, both with no message. Under the other rules the picks are the remaster's Withdraw / Deposit,
/// whose own dialogs follow (<see cref="Follow"/>).
/// </summary>
public sealed class AmigaBankDialog : IModalDialog, IAmigaWindowDialog
{
    private int _cursor;
    private string? _digits;

    public bool IsOpen { get; private set; }

    /// <summary>What to open after OK under the manual's bank rules: 0 = nothing, 1 = withdraw, 2 = deposit.</summary>
    public int Follow { get; private set; }

    /// <summary><see cref="Follow"/>, then 0.</summary>
    public int TakeFollow()
    {
        var f = Follow;
        Follow = 0;
        return f;
    }

    public string Help => _digits is not null
        ? UiText.S("towndlg.type_amount_back_clear_digit_enter")
        : UiText.S("game.keep_window_ok_cancel");

    public void Open()
    {
        IsOpen = true;
        _digits = null;
        Follow = 0;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) =>
        IsOpen ? [UiText.S("p3town.bank_title"), .. Picks(session)] : [];

    private static IReadOnlyList<string> Picks(GameSession session) => session.BankIsDos
        ? [UiText.S("p3town.bank_withdraw_all"), UiText.S("p3town.bank_deposit_some")]
        : [UiText.S("p3town.bank_withdraw"), UiText.S("p3town.bank_deposit")];

    public AmigaWindowView? View(GameSession session, GameDataRepository data, KeepWindowsLayout layout)
    {
        if (!IsOpen || layout.Bank is not { Text.Count: >= 4 } w)
            return null;
        var t = w.Text;
        var bank = session.Party.Members.Sum(m => (long)m.GoldInBank);
        List<AmigaWindowRow> rows =
        [
            new(t[0].X, t[0].Y, UiText.S("p3town.bank_title"), false),
            new(t[1].X, t[1].Y, UiText.S("p3town.bank_gold_carried"), false),
            new(t[1].X, t[1].Y + 8, UiText.S("p3town.bank_members_total"), false),
            new(t[2].X, t[2].Y, session.Party.GoldCarried.ToString(System.Globalization.CultureInfo.InvariantCulture), true),
            new(t[2].X, t[2].Y + 8, bank.ToString(System.Globalization.CultureInfo.InvariantCulture), true),
        ];
        if (_digits is not null)
        {
            rows.Add(new(t[3].X, t[3].Y, UiText.S("p3town.bank_how_much") + _digits + "_", false));
            return new AmigaWindowView(w, rows, []);
        }
        var picks = Picks(session);
        for (var i = 0; i < picks.Count; i++)
            rows.Add(new(t[3].X, t[3].Y + 8 * i, picks[i], i == _cursor));
        return new AmigaWindowView(w, rows, [(rows.Count - 2, Keys.D1), (rows.Count - 1, Keys.D2)]);
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (!IsOpen)
                return null;
            if (key is Keys.Escape or Keys.C)
            {
                IsOpen = false;
                return "";
            }
            if (_digits is not null)
            {
                if (key is Keys.Back or Keys.Delete)
                    _digits = _digits.Length > 0 ? _digits[..^1] : _digits;
                else if (DialogKeys.Digit(key) is >= 0 and var d && _digits.Length < 9)
                    _digits += (char)('0' + d);
                else if (key is Keys.Enter or Keys.O)
                {
                    IsOpen = false;
                    var amount = _digits.Length == 0 ? 0 : long.Parse(_digits, System.Globalization.CultureInfo.InvariantCulture);
                    session.DosBankDeposit(Math.Min(amount, session.Party.GoldCarried));
                    return ""; // the Amiga closes with no message
                }
                continue;
            }
            if (key is Keys.Up or Keys.Down)
                _cursor = 1 - _cursor;
            else if (DialogKeys.Digit(key) is 1 or 2 && DialogKeys.Digit(key) is var pick)
                _cursor = pick - 1;
            if (key is not (Keys.Enter or Keys.O) && DialogKeys.Digit(key) is not (1 or 2))
                continue;
            if (!session.BankIsDos)
            {
                IsOpen = false;
                Follow = _cursor + 1;
                return "";
            }
            if (_cursor == 0)
            {
                IsOpen = false;
                session.DosBankWithdrawAll();
                return "";
            }
            if (session.Party.GoldCarried > 0)
                _digits = ""; // the Amiga's field opens empty (FORMAT.md §8)
            else
            {
                IsOpen = false;
                return UiText.S("game.no_gold_carried");
            }
        }
        return null;
    }
}

/// <summary>
/// s157: III's manual / no-fee Bank D)EPOSIT (<see cref="SessionPromptKind.BankShares"/>) in the Amiga Bank's window 9 style
/// under AMIGA SCREENS. The Amiga has no share deposit (FORMAT.md 8.2), so the rows are the remaster's: the prompt's last
/// line ("Shares for: NAME") in the title cell, its other lines as labels, then one pick row per share count (the
/// highlight yellow, a click = that count's digit); OK / CANCEL as the other windows (<see cref="Choice"/>).
/// </summary>
public static class AmigaShareWindow
{
    /// <summary>The window for <paramref name="prompt"/>, or null: not the bank's share question, or the pack lacks window 9.</summary>
    public static AmigaWindowView? View(SessionPrompt prompt, int cursor, KeepWindowsLayout layout)
    {
        if (prompt.Kind != SessionPromptKind.BankShares || prompt.Lines.Count == 0 || layout.Bank is not { Text.Count: >= 4 } w)
            return null;
        var t = w.Text;
        var rows = new List<AmigaWindowRow> { new(t[0].X, t[0].Y, "  " + prompt.Lines[^1], false) };
        for (var i = 0; i < prompt.Lines.Count - 1; i++)
            rows.Add(new(t[1].X, t[1].Y + 8 * i, prompt.Lines[i], false));
        var top = Math.Max(t[3].Y, t[1].Y + 8 * prompt.Lines.Count);
        var picks = new List<(int, Keys)>();
        for (var i = 0; i < prompt.Options.Count; i++)
        {
            var n = prompt.Options[i];
            rows.Add(new(t[3].X, top + 8 * i, n == "1" ? UiText.F("p3town.bank_one_share", n) : UiText.F("p3town.bank_x_shares", n),
                i == cursor));
            if (int.TryParse(n, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
                    out var d) && d is >= 0 and <= 9)
                picks.Add((rows.Count - 1, Keys.D0 + d));
        }
        return new AmigaWindowView(w, rows, picks);
    }

    /// <summary>
    /// The key the prompt menu gets for <paramref name="key"/> on the share window: O = OK (Return, the highlighted count),
    /// null for C = CANCEL (the front end leaves the deposit, <see cref="SessionPrompts.CancelBankDeposit"/>); any other key
    /// unchanged (arrows, digits, Return).
    /// </summary>
    public static Keys? Choice(Keys key) => key switch
    {
        Keys.O => Keys.Enter,
        Keys.C => null,
        _ => key,
    };
}
