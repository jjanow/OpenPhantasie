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

    /// <summary>True while the inn's rows are up (the SPACE wait after the stay).</summary>
    private bool InnWindowUp => InTownScene && SceneName == "p4.inn" && W.InnRows is not null && Extras?.Inn is not null;

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
        InnText(inn, W.InnRows ?? [], Game.Party.Members.Count, s => PanelText(s.Label, s.Text), t => PackWidth(t, k));

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

    private void DrawBankWindow(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k)
    {
        if (!BankWindowUp)
            return;
        var bank = Extras!.Bank!;
        var view = W.BankWindow!;
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
            var b = bank.Buttons;
            for (var n = 0; n < b.Count; n++)
            {
                var digit = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var y = b.Y + b.Step * n;
                // _wprint("  "), _wputc('0' + n), _wprint("  ") on colour-4 cells (_bgcol 4): as wide as the advances.
                Box(new PanelRect(b.X, y, 2 * PackWidth(b.Pad, k) + PackWidth(digit, k), b.Height), b.Colour, Color.SteelBlue);
                PackText(batch, pixel, origin + new Vector2((b.X + PackWidth(b.Pad, k)) * k, y * k), digit,
                    Pal(bank.TextColour, Color.Black), k);
                // _setbtn(-1, win, x, y, 24, 16, '0' + n): a click gives the share, as the digit key does here.
                if (W.Prompt == P4TownPrompt.Shares)
                    UiOverlays.Hits?.Add(new HitRegion(new Rectangle((int)(origin.X + b.X * k), (int)(origin.Y + y * k),
                        (int)Math.Ceiling(b.Width * k), (int)Math.Ceiling(b.Height * k)), new P4PanelIconTag("_deposit", n))
                    { Click = [Microsoft.Xna.Framework.Input.Keys.D0 + n] });
            }
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
