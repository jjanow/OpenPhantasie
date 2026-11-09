using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s163: what the inn, the bank and the ending's message pages draw in their windows (FORMAT.md 6.9 "Building windows
/// under the picture"): <c>_inn</c>'s rows in a sunken box under INN.PKH, the bank's account table / deposit / withdraw
/// tables under BANK.PKH with the party's gold, and <c>_endmsg</c>'s untitled white window 33 over the ending's pictures.
/// These windows are 352 rows (the original's) round a 180-row picture; the lines they hold are no longer printed in the
/// text column. English rows that would pass the box wrap and push the later rows (and the window) down.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The titled windows whose grey under the picture holds the original's messages (pack key → scene).</summary>
    private static readonly HashSet<string> TallWindows = ["_inn", "_bank", "_banquet", "_arathamus"];

    private bool InTownScene => Game.LoopBuilt && Game.Battle is null && Game.Place == P4Place.Town;

    /// <summary>The inn's picture (the town inn's scene; s165: an outdoor inn's too).</summary>
    public const string InnScene = "p4.inn";

    /// <summary>The inn's rows now: the town inn's, or (s165) an outdoor inn's (<c>_oinn</c> → <c>_inn</c>).</summary>
    private IReadOnlyList<P4InnRow>? InnRowsNow => !Game.LoopBuilt || Game.Battle is not null ? null
        : Game.Place == P4Place.Town ? W.InnRows : Game.Place == P4Place.Outdoors && !MenuOpen ? O.InnRows : null;

    /// <summary>True while the inn's rows are up (the SPACE wait after the stay, in town or at an outdoor inn).</summary>
    private bool InnWindowUp => SceneName == InnScene && InnRowsNow is not null && Extras?.Inn is not null;

    /// <summary>True while the bank is open with its window data.</summary>
    private bool BankWindowUp => InTownScene && SceneName == "p4.bank" && W.BankWindow is not null && Extras?.Bank is not null;

    /// <summary>True while an ending page waits in <c>_endmsg</c>'s window.</summary>
    private bool EndMessageUp => InTownScene && W.EndingBlock is not null && Extras?.EndMessage is { } m
        && SceneName is { } s && m.SceneY.ContainsKey(s);

    /// <summary>True when the town's message lines are in a window under the picture, not the text column.</summary>
    private bool MessagesInWindow => InnWindowUp || BankWindowUp || EndMessageUp;

    // --- The inn ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The inn's rows (pack pixels in its window): member m's row at (text x, text y + step · m), the name, "slept" and the
    /// MP line after it, wrapped short of the box's right edge (8 in, as the left); the box's height (pad + step · party
    /// size, taller by the wrapped rows).
    /// </summary>
    public static (List<(int X, int Y, string Text)> Rows, int BoxHeight) InnText(InnWindow inn, IReadOnlyList<P4InnRow> rows,
        int partySize, Func<TrainingHallSegment, string> text, Func<string, int> width)
    {
        ArgumentNullException.ThrowIfNull(inn);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(text);
        var right = inn.BoxX + inn.BoxWidth - (inn.TextX - inn.BoxX);
        var pieces = rows.Select(r => ((int?)inn.TextX, (int?)(inn.TextY + inn.RowStep * r.Member),
            r.Name + text(inn.Slept) + (r.MpUp ? text(inn.MpUp) : ""), 0, inn.TextColour, 0));
        var flowed = FlowText(pieces, inn.TextX, inn.TextY, right, inn.RowStep, (t, _, _) => width(t));
        var last = flowed.Select(r => r.Y).DefaultIfEmpty(inn.TextY).Max();
        var lastMember = inn.TextY + inn.RowStep * Math.Max(0, partySize - 1);
        var height = inn.BoxPad + inn.RowStep * Math.Max(1, partySize) + Math.Max(0, last - lastMember);
        return ([.. flowed.Select(r => (r.X, r.Y, r.Text))], height);
    }

    private (List<(int X, int Y, string Text)> Rows, int BoxHeight) InnRows(InnWindow inn, float k) =>
        InnText(inn, InnRowsNow ?? [], Game.Party.Members.Count, s => PanelText(s.Label, s.Text), t => PackWidth(t, k));

    private void DrawInnWindow(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k)
    {
        if (!InnWindowUp)
            return;
        var inn = Extras!.Inn!;
        var (rows, height) = InnRows(inn, k);
        DrawSunken(batch, pixel, origin, new PanelRect(inn.BoxX, inn.BoxY, inn.BoxWidth, height), k);
        var ink = Pal(inn.TextColour, Color.Black);
        foreach (var (x, y, t) in rows)
            PackText(batch, pixel, origin + new Vector2(x * k, y * k), t, ink, k);
    }

    // --- The bank --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The bank window's pieces now (pack pixels in its window): the band and heading of a deposit / withdraw, the table's
    /// headers and rows, the gold or per-share line. <paramref name="shares"/> are the deposit's shares while the prompt is
    /// open (the Client's), <paramref name="typed"/> the amount typed for the withdraw row asking.
    /// </summary>
    public static List<(int X, int Y, string Text, bool Centred, int Colour)> BankText(BankWindow bank, P4BankWindow view,
        IReadOnlyList<P4Member> members, long gold, IReadOnlyList<int>? shares, string? typed,
        Func<TrainingHallSegment, string> text, Func<string, string, string> label)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(label);
        var list = new List<(int, int, string, bool, int)>();
        var ink = bank.TextColour;
        var table = TableOf(bank, view.View);
        if (view.View != P4BankView.Accounts)
        {
            var heading = view.View == P4BankView.Deposit ? bank.DepositHeading : bank.WithdrawHeading;
            list.Add((heading.X ?? 0, heading.Y ?? 0, text(heading), true, heading.Colour ?? 15));
        }
        foreach (var h in table.Headers)
            list.Add((h.X ?? 0, h.Y ?? 0, text(h), false, h.Colour ?? ink));
        int Col(string name) => table.Columns.GetValueOrDefault(name);
        string Amount(long n) => PaddedAmount(n, table.Digits);
        switch (view.View)
        {
            case P4BankView.Accounts:
                for (var i = 0; i < members.Count; i++)
                {
                    var y = table.RowY + table.RowStep * i;
                    list.Add((Col("name"), y, members[i].Name, false, ink));
                    list.Add((Col("balance"), y, Amount(members[i].Account), false, ink));
                }
                break;
            case P4BankView.Deposit:
                for (var i = 0; i < members.Count; i++)
                {
                    // s165: _deposit asks member by member (0x4A8E): his name comes up at his turn, the share at x 196 once
                    // given (_wloc places it; no cursor is drawn); later members' rows are still empty.
                    if (view.Rows.Count == 0 && shares is not null && i > shares.Count)
                        break;
                    var y = table.RowY + table.RowStep * i;
                    list.Add((Col("name"), y, members[i].Name, false, ink));
                    var share = view.Rows.Count > i ? view.Rows[i].Share : shares is { } s && s.Count > i ? s[i] : null;
                    if (share is int n)
                        list.Add((Col("share"), y, n.ToString(System.Globalization.CultureInfo.InvariantCulture), false, ink));
                }
                break;
            case P4BankView.Withdraw:
                for (var i = 0; i < view.Rows.Count; i++)
                {
                    var r = view.Rows[i];
                    var y = table.RowY + table.RowStep * r.Member;
                    list.Add((Col("name"), y, r.Name, false, ink));
                    list.Add((Col("balance"), y, Amount(r.Account), false, ink));
                    list.Add((Col("limit"), y, Amount(r.Limit), false, ink));
                    if (r.Amount is long a)
                        list.Add((Col("amount"), y, Amount(a), false, ink));
                    else if (i == view.Rows.Count - 1 && r.Limit > 0 && typed is not null)
                        list.Add((Col("amount"), y, typed, false, ink)); // _numin types at the bank window's cursor
                }
                break;
        }
        if (view.View == P4BankView.Deposit && view.PerShare is long per)
            list.Add((bank.PerShare.X ?? 0, bank.PerShare.Y ?? 0,
                text(bank.PerShare) + PaddedAmount(per, bank.PerDigits) + text(bank.PerUnit), false, ink));
        else
        {
            var g = bank.Gold;
            list.Add((g.X, g.Y, label(g.Label, g.LabelText) + PaddedAmount(gold, g.Digits) + label(g.Unit, g.UnitText), false, ink));
        }
        return list;
    }

    private static BankTable TableOf(BankWindow bank, P4BankView view) => view switch
    {
        P4BankView.Deposit => bank.Deposit,
        P4BankView.Withdraw => bank.Withdraw,
        _ => bank.Accounts,
    };

    // --- The Guild treasury (11-guild-treasury.md 5) ------------------------------------------------------------------------

    /// <summary>The treasury's sunken box in the bank's window: the grey under the picture, 8 in at the sides, 12 at the
    /// top and bottom.</summary>
    public static PanelRect TreasuryFrame(BankWindow bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        var c = bank.Clear;
        return new PanelRect(c.X + 8, c.Y + 12, c.Width - 16, c.Height - 24);
    }

    /// <summary>The rows the treasury's box shows at once (the accounts table's row step).</summary>
    public static int TreasuryVisibleRows(BankWindow bank) =>
        Math.Max(1, (TreasuryFrame(bank).Height - 4) / Math.Max(1, bank.Accounts.RowStep));

    /// <summary>
    /// The treasury's rows (<see cref="TreasuryReport.Lines"/>) in the bank's window: every line, wrapped short of the box's
    /// right edge (nothing is dropped; the box scrolls when they pass it). Row n is at (x, frame y + 2 + step · n).
    /// </summary>
    public static List<(int X, string Text)> TreasuryRows(BankWindow bank, IReadOnlyList<string> lines,
        Func<string, int> width)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(width);
        var frame = TreasuryFrame(bank);
        var x = frame.X + 8;
        var step = Math.Max(1, bank.Accounts.RowStep);
        var pieces = new List<(int? X, int? Y, string Text, int Font, int Colour, int Style)>();
        for (var i = 0; i < lines.Count; i++)
            pieces.Add((x, i * step, lines[i], 0, bank.TextColour, 0));
        // An empty line keeps its row (FlowText adds no row for it).
        var rows = new List<(int X, string Text)>();
        var flowed = FlowText(pieces, x, 0, frame.X + frame.Width - 8 - ScrollBox.BarWidth, step, (t, _, _) => width(t));
        var byRow = flowed.GroupBy(r => r.Y / step).ToDictionary(g => g.Key, g => g.First());
        var last = Math.Max(lines.Count - 1, byRow.Keys.DefaultIfEmpty(0).Max());
        for (var n = 0; n <= last; n++)
            rows.Add(byRow.TryGetValue(n, out var r) ? (r.X, r.Text) : (x, ""));
        return rows;
    }

    /// <summary>The treasury box's scroll (wheel and bar) while it is up.</summary>
    public ScrollBox TreasuryScroll { get; } = new();

    private P4BankWindow? _treasuryShown;

    private void DrawTreasuryWindow(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k, BankWindow bank)
    {
        if (!ReferenceEquals(W.BankWindow, _treasuryShown))
        {
            _treasuryShown = W.BankWindow;
            TreasuryScroll.Home(); // each visit opens at the newest entries
        }
        var frame = TreasuryFrame(bank);
        DrawSunken(batch, pixel, origin, frame, k);
        var rows = TreasuryRows(bank, W.TreasuryLines ?? [], t => PackWidth(t, k));
        var visible = TreasuryVisibleRows(bank);
        TreasuryScroll.Set(rows.Count, visible);
        var area = new Rectangle((int)(origin.X + frame.X * k), (int)(origin.Y + frame.Y * k), (int)Math.Ceiling(frame.Width * k),
            (int)Math.Ceiling(frame.Height * k));
        UiOverlays.HitScrollArea(TreasuryScroll, area);
        var ink = Pal(bank.TextColour, Color.Black);
        var step = Math.Max(1, bank.Accounts.RowStep);
        for (var i = 0; i < visible && TreasuryScroll.Offset + i < rows.Count; i++)
        {
            var (x, t) = rows[TreasuryScroll.Offset + i];
            PackText(batch, pixel, origin + new Vector2(x * k, (frame.Y + 2 + step * i) * k), t, ink, k);
        }
        UiOverlays.DrawScrollBar(batch, pixel, TreasuryScroll, area);
    }

    private void DrawBankWindow(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k)
    {
        if (!BankWindowUp)
            return;
        var bank = Extras!.Bank!;
        var view = W.BankWindow!;
        if (view.View == P4BankView.Treasury)
        {
            batch.Draw(pixel, origin + new Vector2(bank.Clear.X * k, bank.Clear.Y * k), null, Pal(bank.ClearColour, Color.LightGray),
                0f, Vector2.Zero, new Vector2(bank.Clear.Width * k, bank.Clear.Height * k), SpriteEffects.None, 0f);
            DrawTreasuryWindow(batch, pixel, origin, k, bank);
            return;
        }
        void Box(PanelRect r, int colour, Color fallback) =>
            batch.Draw(pixel, origin + new Vector2(r.X * k, r.Y * k), null, Pal(colour, fallback), 0f, Vector2.Zero,
                new Vector2(r.Width * k, r.Height * k), SpriteEffects.None, 0f);
        Box(bank.Clear, bank.ClearColour, Color.LightGray);
        if (view.View != P4BankView.Accounts)
            Box(bank.Band, bank.BandColour, Color.Black);
        var table = TableOf(bank, view.View);
        DrawSunken(batch, pixel, origin, table.Frame, k);
        Box(table.Line, table.LineColour, Color.Black);
        if (view.View == P4BankView.Deposit)
        {
            DrawShareButtons(batch, pixel, origin, k, bank.Buttons, bank.TextColour, "_deposit");
            if (view.PerShare is not null)
                Box(bank.ClearPer, bank.ClearColour, Color.LightGray);
        }
        var typed = W.Prompt == P4TownPrompt.Number ? _typed : null;
        foreach (var (x, y, t, centred, colour) in BankText(bank, view, Game.Party.Members, Game.Party.Gold,
                     W.Prompt == P4TownPrompt.Shares ? _shares : null, typed, s => PanelText(s.Label, s.Text), PanelText))
        {
            var left = centred ? x - (PackWidth(t, k) >> 1) : x;
            PackText(batch, pixel, origin + new Vector2(left * k, y * k), t, Pal(colour, Color.Black), k);
        }
    }

    /// <summary>
    /// The share buttons " n " (n = 0–3) of <c>_deposit</c> and <c>_gatebank</c>: <c>_wprint("  ")</c>, <c>_wputc('0' + n)</c>,
    /// <c>_wprint("  ")</c> on colour cells as wide as the advances. While the shares are asked a click gives the share
    /// (<c>_setbtn(-1, win, x, y, 24, 16, '0' + n)</c>), as the digit key does; the "0" button is off for a member in good
    /// health (its enable field, s165).
    /// </summary>
    private void DrawShareButtons(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k, BankShareButtons b, int ink,
        string tag)
    {
        var asking = W.Prompt == P4TownPrompt.Shares;
        var members = Game.Party.Members;
        var zeroOn = asking && _shares.Length < members.Count && members[_shares.Length].Condition != P4Member.Okay;
        for (var n = 0; n < b.Count; n++)
        {
            var digit = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var y = b.Y + b.Step * n;
            var cell = new PanelRect(b.X, y, 2 * PackWidth(b.Pad, k) + PackWidth(digit, k), b.Height);
            batch.Draw(pixel, origin + new Vector2(cell.X * k, cell.Y * k), null, Pal(b.Colour, Color.SteelBlue), 0f, Vector2.Zero,
                new Vector2(cell.Width * k, cell.Height * k), SpriteEffects.None, 0f);
            PackText(batch, pixel, origin + new Vector2((b.X + PackWidth(b.Pad, k)) * k, y * k), digit, Pal(ink, Color.Black), k);
            if (asking && (n > 0 || zeroOn))
                UiOverlays.Hits?.Add(new HitRegion(new Rectangle((int)(origin.X + b.X * k), (int)(origin.Y + y * k),
                    (int)Math.Ceiling(b.Width * k), (int)Math.Ceiling(b.Height * k)), new P4PanelIconTag(tag, n))
                { Click = [Microsoft.Xna.Framework.Input.Keys.D0 + n] });
        }
    }

    // --- The experience share-out (_gatebank) --------------------------------------------------------------------------

    /// <summary>The titled window key of <c>_gatebank</c>'s share-out (the pack's <c>titled</c>).</summary>
    public const string GateBankWindowName = "_gatebank";

    /// <summary>
    /// s165: <c>_gatebank</c>'s pieces (pack pixels in its window, FORMAT.md 6.9): the pool line, then member m's row at
    /// (name x, row y + step · m), shown once his share is asked (<paramref name="shares"/>: the shares given so far, null
    /// = every row), the share digit at x 164, and after the shares (<see cref="P4GateShare.Gains"/>) the experience given
    /// in its place, padded to 8 digits.
    /// </summary>
    public static List<(int X, int Y, string Text)> GateBankText(GateBankWindow gate, P4GateShare share,
        IReadOnlyList<P4Member> members, IReadOnlyList<int>? shares, Func<TrainingHallSegment, string> text)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(share);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(text);
        var list = new List<(int, int, string)>
        {
            (gate.Pool.X ?? 0, gate.Pool.Y ?? 0, text(gate.Pool) + PaddedAmount(share.Pool, gate.PoolDigits) + text(gate.PoolUnit)),
        };
        for (var i = 0; i < members.Count; i++)
        {
            if (share.Gains is null && shares is not null && i > shares.Count)
                break;
            var y = gate.RowY + gate.RowStep * i;
            list.Add((gate.NameX, y, members[i].Name));
            if (share.Gains is { } gains && i < gains.Count)
                list.Add((gate.ShareX, y, PaddedAmount(gains[i], gate.GainDigits)));
            else if (shares is not null && i < shares.Count)
                list.Add((gate.ShareX, y, shares[i].ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        return list;
    }

    /// <summary>The share-out's window is up: the entry's experience shares or their wait, in town, the pack loaded.</summary>
    private bool GateBankUp => InTownScene && W.GateShare is not null && Extras?.GateBank is not null
        && Extras.Titled?.ContainsKey(GateBankWindowName) == true && Assets?.PackPixelsPerLayoutPixel is > 0;

    /// <summary>
    /// s165: <c>_gatebank</c>'s titled window (312 × 160) at the original's place, 100 rows below the other windows' top
    /// ((160, 132) against their (160, 32)); the screen above it stays clear (<c>_clrtdraw</c>).
    /// </summary>
    private bool DrawGateBank(SpriteBatch batch, Texture2D pixel)
    {
        if (!GateBankUp)
            return false;
        var gate = Extras!.GateBank!;
        var window = Extras.Titled![GateBankWindowName];
        var drop = Math.Max(0, window.Window.Y - TitledWindowTop);
        var (w, h) = TitledArea(window.Window.Width, window.Window.Height);
        var k = 1f / Assets!.PackPixelsPerLayoutPixel;
        var shares = W.Prompt == P4TownPrompt.Shares ? _shares : null;
        return DrawBeside(batch, pixel, w, drop + h, (origin, scale) =>
        {
            var top = origin + new Vector2(0, drop * scale);
            DrawTitledFrame(batch, pixel, top, window.Window.Width + 2, window.Window.Height,
                PanelText(window.Title, window.TitleText), scale);
            var at = top + new Vector2(1, PanelTitleRows) * scale;
            batch.Draw(pixel, at, null, Pal(14, Color.LightGray), 0f, Vector2.Zero,
                new Vector2(window.Window.Width * scale, window.Window.Height * scale), SpriteEffects.None, 0f);
            DrawSunken(batch, pixel, at, gate.Frame, scale);
            DrawShareButtons(batch, pixel, at, scale, gate.Buttons, gate.TextColour, GateBankWindowName);
            foreach (var (x, y, t) in GateBankText(gate, W.GateShare!, Game.Party.Members, shares, s => PanelText(s.Label, s.Text)))
                PackText(batch, pixel, at + new Vector2(x * scale, y * scale), t, Pal(gate.TextColour, Color.Black), scale);
        }, 0, (int)Math.Floor((drop + h) * k));
    }

    /// <summary>The y of the titled windows the remaster puts at the picture's place (<c>_wopen(…, 160, 32, …)</c>).</summary>
    private const int TitledWindowTop = 32;

    // --- The ending's message window ---------------------------------------------------------------------------------------

    /// <summary>
    /// <c>_endmsg</c>'s window for <paramref name="block"/> relative to scene <paramref name="scene"/>'s picture (pack pixels):
    /// its top, its height (52, taller by wrapped rows) and the page's rows, wrapped short of the right edge.
    /// </summary>
    public static (int Y, int Height, List<(int X, int Y, string Text)> Rows) EndMessageText(EndMessageWindow end,
        string scene, int block, IReadOnlyList<string> lines, Func<string, int> width)
    {
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(lines);
        var y = end.YFor(block) - end.SceneY.GetValueOrDefault(scene);
        var right = end.Window.Width - end.TextX;
        var pieces = lines.Select((l, i) => ((int?)end.TextX, (int?)(end.TextY + end.LineStep * i), l, 0, end.TextColour, 0));
        var rows = FlowText(pieces, end.TextX, end.TextY, right, end.LineStep, (t, _, _) => width(t));
        var last = rows.Select(r => r.Y).DefaultIfEmpty(end.TextY).Max();
        var height = Math.Max(end.Window.Height, last + end.LineStep + (end.Window.Height - end.TextY - end.LineStep * end.Lines));
        return (y, height, [.. rows.Select(r => (r.X, r.Y, r.Text))]);
    }

    private (int Y, int Height, List<(int X, int Y, string Text)> Rows) EndMessageRows(float k) =>
        EndMessageText(Extras!.EndMessage!, SceneName!, W.EndingBlock ?? 0,
            [.. W.Box.Skip(W.Box.Count > 0 && W.Box[0].Length == 0 ? 1 : 0)], t => PackWidth(t, k)); // Line's leading "\n"

    /// <summary>The untitled white window (<c>_wwind</c> flag 1, title ""): fill 15, a black frame, a black shadow right and below.</summary>
    private void DrawEndMessage(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k)
    {
        if (!EndMessageUp)
            return;
        var end = Extras!.EndMessage!;
        var (y, h, rows) = EndMessageRows(k);
        var w = end.Window.Width;
        var black = Pal(0, Color.Black);
        void Box(float bx, float by, float bw, float bh, Color c) =>
            batch.Draw(pixel, origin + new Vector2(bx * k, by * k), null, c, 0f, Vector2.Zero, new Vector2(bw * k, bh * k),
                SpriteEffects.None, 0f);
        Box(-1, y - 1, w + 2, h + 2, black);
        Box(0, y, w, h, Pal(end.Fill, Color.White));
        Box(w + 1, y + 1, 1, h + 1, black);
        Box(1, y + h + 1, w + 1, 1, black);
        var ink = Pal(end.TextColour, Color.Black);
        foreach (var (x, ry, t) in rows)
            PackText(batch, pixel, origin + new Vector2(x * k, (y + ry) * k), t, ink, k);
    }

    // --- Shared ------------------------------------------------------------------------------------------------------------

    /// <summary><c>_whole</c>'s sunken frame: two 1-pixel rings outside <paramref name="r"/>, colour 1 top / left, 15 bottom / right.</summary>
    private void DrawSunken(SpriteBatch batch, Texture2D pixel, Vector2 origin, PanelRect r, float k)
    {
        void Box(float x, float y, float w, float h, Color c) =>
            batch.Draw(pixel, origin + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k),
                SpriteEffects.None, 0f);
        Color dark = Pal(1, Color.DimGray), light = Pal(15, Color.White);
        for (var n = 1; n <= NoYesFrame; n++)
        {
            Box(r.X - n, r.Y - n, r.Width + 2 * n, 1, dark);
            Box(r.X - n, r.Y - n, 1, r.Height + 2 * n, dark);
            Box(r.X - n, r.Y + r.Height + n - 1, r.Width + 2 * n, 1, light);
            Box(r.X + r.Width + n - 1, r.Y - n, 1, r.Height + 2 * n, light);
        }
    }

    /// <summary>
    /// The height (pack pixels) of the titled window <paramref name="key"/> round a <paramref name="pictureHeight"/>-row
    /// picture: the original's (352 for the inn, the bank, the banquet, the cell; else the picture's), taller when the
    /// inn's box or the ending's window would pass its bottom.
    /// </summary>
    private int TitledHeight(string key, TitledWindow window, int pictureHeight, float k)
    {
        if (!TallWindows.Contains(key) || window.Window.Height <= pictureHeight)
            return pictureHeight;
        var height = window.Window.Height;
        if (InnWindowUp)
        {
            var inn = Extras!.Inn!;
            height = Math.Max(height, inn.BoxY + InnRows(inn, k).BoxHeight + NoYesFrame + 2);
        }
        if (EndMessageUp)
        {
            var (y, h, _) = EndMessageRows(k);
            height = Math.Max(height, y + h + 2);
        }
        return height;
    }

    /// <summary>What a tall titled window draws under and over its picture: the window grey under it, the building's rows.</summary>
    private void DrawUnderPicture(SpriteBatch batch, Texture2D pixel, Vector2 origin, int width, int pictureHeight, int height,
        float k)
    {
        if (height > pictureHeight)
            batch.Draw(pixel, origin + new Vector2(0, pictureHeight * k), null, Pal(14, Color.LightGray), 0f, Vector2.Zero,
                new Vector2(width * k, (height - pictureHeight) * k), SpriteEffects.None, 0f);
    }

    private void DrawBuildingWindows(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k)
    {
        DrawInnWindow(batch, pixel, origin, k);
        DrawBankWindow(batch, pixel, origin, k);
        DrawEndMessage(batch, pixel, origin, k);
    }
}
