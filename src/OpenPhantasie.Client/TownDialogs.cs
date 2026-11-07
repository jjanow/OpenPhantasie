using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>Digit helpers shared by the key-driven dialogs.</summary>
internal static class DialogKeys
{
    /// <summary>1..9 for a digit key, 0 for D0/NumPad0, −1 otherwise.</summary>
    public static int Digit(Keys key) => key switch
    {
        >= Keys.D0 and <= Keys.D9 => key - Keys.D0,
        >= Keys.NumPad0 and <= Keys.NumPad9 => key - Keys.NumPad0,
        _ => -1,
    };
}

/// <summary>
/// The one member-list cursor DOS keeps across "WHO WILL WITHDRAW?", "WHO WILL SHOP?" and "WHO WILL USE?" (s58 DOSBox: the
/// Armory opened on the last Bank row, Use item on the last Armory row; town-menus.md §Bank). The remaster's current
/// shopper is this row. In memory only.
/// </summary>
public sealed class MemberCursor
{
    public int Row { get; set; }

    /// <summary>The row kept inside a party of <paramref name="count"/> members.</summary>
    public int Clamp(int count) => Row = Math.Clamp(Row, 0, Math.Max(0, count - 1));

    /// <summary>Up/Down move the row (wrapping, like the DOS menus <c>[~]</c>); returns true if the key was one of them.</summary>
    public bool Move(Keys key, int count)
    {
        if (key is not (Keys.Up or Keys.Down) || count == 0)
            return false;
        Row = (Clamp(count) + (key == Keys.Down ? 1 : count - 1)) % count;
        return true;
    }
}

/// <summary>
/// Bank W)ITHDRAW (OV1 <c>0x2944</c> loop, town-menus.md §Bank) [C]: "GOLD CARRIED %u" / "WHO WILL WITHDRAW?" on the shared
/// <see cref="MemberCursor"/> row (Up/Down + Return, or the digit; Esc leaves), then the amount field (<c>0x35df</c> →
/// field editor <c>0x3629</c>, s58 static + DOSBox): it opens holding the member's whole bank balance and Return takes out
/// the value shown (<see cref="GameSession.BankWithdraw"/>). A digit shifts in from the right; one that would put the
/// value above the balance or 65535 is refused (DOS beeps; with the save's gold cap lifted, <c>p1.gold-cap</c>, only the
/// balance limits it, <see cref="MaxAmount"/>). Backspace/Delete shifts right with a 0 in front. DOS has no
/// Esc case there, so Esc is ignored. After Return — 0 included — the loop goes back to "WHO WILL WITHDRAW?" (s58 DOSBox).
/// III under AMIGA SCREENS (s157, <see cref="AmigaKeys"/>): drawn in the Bank's window 9 style (<see cref="View"/>), O = OK
/// (Return), C or Esc = CANCEL (the picker leaves; the amount field goes back to the picker, nothing taken out).
/// </summary>
public sealed class WithdrawDialog : IModalDialog, IAmigaWindowDialog
{
    public static string WhoTitle => UiText.S("towndlg.who_will_withdraw"); // OV1 0x5de8

    /// <summary>
    /// The amount field's limit besides the balance: 65535 (DOS text compare with "65535", OV1 <c>0x58c5</c>) under a gold
    /// cap; the lifted cap (D-048 option) has none, so a balance above 65,535 can be taken out.
    /// </summary>
    public static long MaxAmount(GameSession session) =>
        session.Options.GoldCap == GoldCapRule.Lifted ? GoldCaps.Lifted : GoldCaps.Dos;

    private readonly MemberCursor _members;
    private bool _open;
    private Character? _member;
    private int _amount;

    public WithdrawDialog(MemberCursor? members = null) => _members = members ?? new MemberCursor();

    public bool IsOpen => _open;

    /// <summary>True while the member picker is shown (no amount field open).</summary>
    public bool Picking => _open && _member is null;

    /// <summary>
    /// True while the dialog shows as III Amiga's window (set by the front end before keys): O = OK, C / Esc = CANCEL. Off
    /// (I, II, the remaster's panel) the keys are DOS's.
    /// </summary>
    public bool AmigaKeys { get; set; }

    public string Help => AmigaKeys
        ? Picking ? UiText.S("p3town.bank_window_pick_help") : UiText.S("p3town.bank_window_amount_help")
        : Picking ? UiText.S("towndlg.arrows_enter_or_1_6_choose") : UiText.S("towndlg.type_amount_back_clear_digit_enter");

    /// <summary>
    /// III Amiga look (s157): the Bank's window 9 (box, frame, OK / CANCEL) with the remaster's own rows — the Amiga has no
    /// per-member withdraw (FORMAT.md 8.2). Picker: "Who will withdraw?", the party's gold, one row per member (name, its
    /// bank gold in the value colour; the cursor row yellow; a click picks). Amount: the member's bank gold, the party's
    /// gold and "How much:" with the field.
    /// </summary>
    public AmigaWindowView? View(GameSession session, GameDataRepository data, KeepWindowsLayout layout)
    {
        if (!_open || layout.Bank is not { Text.Count: >= 4 } w)
            return null;
        var t = w.Text;
        static string N(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var rows = new List<AmigaWindowRow>();
        var picks = new List<(int, Keys)>();
        if (_member is null)
        {
            rows.Add(new(t[0].X, t[0].Y, UiText.S("p3town.bank_who_withdraws"), false));
            rows.Add(new(t[1].X, t[1].Y, UiText.S("p3town.bank_gold_carried"), false));
            rows.Add(new(t[2].X, t[2].Y, N(session.Party.GoldCarried), true));
            var row = _members.Clamp(session.Party.Members.Count);
            for (var i = 0; i < session.Party.Members.Count; i++)
            {
                var m = session.Party.Members[i];
                var y = t[1].Y + 16 + 8 * i;
                rows.Add(new(t[2].X, y, N(m.GoldInBank), true));
                rows.Add(new(t[1].X, y, $"{i + 1} {m.Name}", i == row));
                picks.Add((rows.Count - 1, Keys.D0 + i + 1));
            }
            return new AmigaWindowView(w, rows, picks);
        }
        rows.Add(new(t[0].X, t[0].Y, UiText.F("p3town.bank_account_x", _member.Name), false));
        rows.Add(new(t[1].X, t[1].Y, UiText.S("p3town.bank_gold_in_bank"), false));
        rows.Add(new(t[2].X, t[2].Y, N(_member.GoldInBank), true));
        rows.Add(new(t[1].X, t[1].Y + 8, UiText.S("p3town.bank_gold_carried"), false));
        rows.Add(new(t[2].X, t[2].Y + 8, N(session.Party.GoldCarried), true));
        rows.Add(new(t[3].X, t[3].Y, UiText.S("p3town.bank_how_much") + " " + N(_amount) + "_", false));
        return new AmigaWindowView(w, rows, picks);
    }

    /// <summary>The value in the amount field.</summary>
    public int Amount => _amount;

    /// <summary>Bank Withdraw: start at "WHO WILL WITHDRAW?".</summary>
    public void Open()
    {
        _open = true;
        _member = null;
    }

    /// <summary>Straight to <paramref name="member"/>'s amount field (as if picked at "WHO WILL WITHDRAW?").</summary>
    public void Open(Character member)
    {
        _open = true;
        _member = member;
        _amount = member.GoldInBank;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        if (!_open)
            return [];
        if (_member is null)
        {
            var row = _members.Clamp(session.Party.Members.Count);
            return [UiText.F("towndlg.gold_carried_x", session.Party.GoldCarried), WhoTitle,
                .. session.Party.Members.Select((m, i) => $"{(i == row ? ">" : " ")}{i + 1} {m.Name}  {m.GoldInBank}")];
        }
        return [UiText.F("towndlg.x_gold_in_bank_x", _member.Name, _member.GoldInBank), UiText.F("towndlg.how_much_to_withdraw_x", _amount)];
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var raw in newKeys)
        {
            if (!_open)
                return null;
            var key = AmigaKeys && raw == Keys.O ? Keys.Enter : raw; // the Amiga window's OK
            if (AmigaKeys && key is Keys.C or Keys.Escape && _member is not null)
            {
                _member = null; // CANCEL: back to "Who will withdraw?", nothing taken out
                return "";
            }
            if (_member is null)
            {
                var count = session.Party.Members.Count;
                if (key is Keys.Escape or Keys.Back || AmigaKeys && key == Keys.C)
                {
                    _open = false; // Esc at the picker is the only way out (0x2944)
                    return "";
                }
                if (_members.Move(key, count))
                    continue;
                var pick = key == Keys.Enter && count > 0 ? _members.Clamp(count) + 1 : DialogKeys.Digit(key);
                if (pick >= 1 && pick <= count)
                {
                    _members.Row = pick - 1;
                    Open(session.Party.Members[pick - 1]);
                }
                continue;
            }
            if (key is Keys.Back or Keys.Delete)
            {
                _amount /= 10;
                continue;
            }
            var d = DialogKeys.Digit(key);
            if (d >= 0)
            {
                var max = MaxAmount(session);
                // DOS's field is five digits wide (the leading digit drops off); the lifted cap's field is as wide as the cap.
                var shifted = max > GoldCaps.Dos ? _amount * 10L + d : (_amount * 10L + d) % 100_000;
                if (shifted <= _member.GoldInBank && shifted <= max)
                    _amount = (int)shifted; // else DOS beeps and undoes the digit
                continue;
            }
            if (key == Keys.Enter)
            {
                var member = _member;
                _member = null; // back to "WHO WILL WITHDRAW?" (s58 DOSBox), whatever the amount
                return _amount == 0 ? UiText.S("towndlg.nothing_withdrawn") : session.BankWithdraw(member, _amount).Message;
            }
        }
        return null;
    }
}

/// <summary>
/// Guild S)PELLS for the picked member (DOS "WHO WILL LEARN?", OV1 <c>0x1490..0x1677</c>, s63 DOSBox): picking a member rolls
/// the learn check (<see cref="GameSession.RollSpellLearn"/>), then shows "You can learn %d more spells." / "The cost is %d GP."
/// and the spells marked available (<c>SpellsKnown == 2</c>; none once <see cref="TownMenus.SpellLearnCount"/> ≤ 0). One pick
/// per opening through <see cref="TownMenus.LearnSpell"/>, then back to "WHO WILL LEARN?". The list is numbered after the
/// manual's "POSSIBLE SPELLS:" (D-074, decided: manual; DOS shows names under "Available spells:").
/// </summary>
public sealed class LearnSpellDialog : IModalDialog, IPagedDialog
{
    public ListPage? Page { get; private set; }

    private const int PageSize = 9;
    private Character? _member;
    private int _scroll;
    private bool _learned;

    public bool IsOpen => _member is not null;

    public string Help => UiText.S("towndlg.1_9_learn_up_down_more");

    public static IReadOnlyList<int> Learnable(Character ch) =>
        TownMenus.SpellLearnCount(ch) <= 0 ? []
            : Enumerable.Range(0, Math.Min(ch.SpellsKnown.Length, SpellId.Count)).Where(id => ch.SpellsKnown[id] == 2).ToList();

    public string? Open(GameSession session, Character member)
    {
        if (session.CurrentTown is null)
            return UiText.S("towndlg.no_guild_here");
        _learned = session.RollSpellLearn(member); // OV1 0x1618: rolled before the list, whatever follows
        _member = member;
        _scroll = 0;
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        if (_member is null || session.CurrentTown is not { } town)
            return [];
        var fee = TownMenus.SpellLearningGoldCost(_member, town);
        Page = ListPage.Of(3, PageSize, _scroll, Learnable(_member).Count);
        return [UiText.F("towndlg.you_can_learn_x_more_spells", Math.Max(0, TownMenus.SpellLearnCount(_member))), UiText.F("towndlg.the_cost_is_x_gp", fee), UiText.S("towndlg.possible_spells"),
            .. Learnable(_member).Skip(_scroll).Take(PageSize).Select((id, i) => $"{i + 1} #{id + 1} {data.GetSpell(id).Name}")];
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (_member is null)
                return null;
            if (key is Keys.Escape or Keys.Back)
            {
                _member = null;
                return "";
            }
            var list = Learnable(_member);
            if (key is Keys.Up or Keys.Down)
            {
                _scroll = key == Keys.Up ? Math.Max(0, _scroll - 1) : Math.Min(Math.Max(0, list.Count - PageSize), _scroll + 1);
                continue;
            }
            var d = DialogKeys.Digit(key);
            if (d >= 1 && _scroll + d - 1 < list.Count && session.CurrentTown is { } town)
            {
                var result = session.LearnSpell(_member, list[_scroll + d - 1], _learned);
                _member = null; // DOS: one pick, then back to "WHO WILL LEARN?"
                return result.Message;
            }
        }
        return null;
    }
}
