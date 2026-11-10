using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The Guild treasury's window (P3 QoL, <c>docs/plan/11-guild-treasury.md</c> section 5): under the <c>treasury</c> option
/// the town Bank opens this instead of its accounts (I–III; every route: the Bank list, the withdraw key, III's Amiga bank).
/// It shows <see cref="TreasuryReport.Lines"/>: the title, the balance, then the ledger newest first by trip. No deposit,
/// withdraw or shares. Up / Down / PgUp / PgDn / Home / End scroll a ledger longer than the page; any other key closes.
/// Drawn in III's window 9 under AMIGA SCREENS (<see cref="IAmigaWindowDialog"/>), else in the remaster's text panel.
/// </summary>
public sealed class TreasuryDialog : IModalDialog, IPagedDialog, IAmigaWindowDialog
{
    /// <summary>Report lines a page shows under the title (the text panel's 13 rows less the title).</summary>
    public const int PageRows = UiOverlays.PromptPanelRows;

    private int _offset;
    private int _total;

    public bool IsOpen { get; private set; }

    public string Help => UiText.S("treasury.help");

    public void Open()
    {
        IsOpen = true;
        _offset = 0;
    }

    public ListPage? Page => IsOpen ? ListPage.Of(1, PageRows, _offset, _total) : null;

    /// <summary>The report's lines after the title (what scrolls).</summary>
    private List<string> Body(GameSession session)
    {
        var body = TreasuryReport.Lines(session.Treasury, 0).Skip(1).ToList();
        _total = body.Count;
        _offset = Math.Clamp(_offset, 0, Math.Max(0, _total - PageRows));
        return body;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) =>
        IsOpen ? [GameText.S("core.treasury.title"), .. Body(session).Skip(_offset).Take(PageRows)] : [];

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (!IsOpen)
                return null;
            var max = Math.Max(0, Body(session).Count - PageRows);
            int? to = key switch
            {
                Keys.Up => _offset - 1,
                Keys.Down => _offset + 1,
                Keys.PageUp => _offset - (PageRows - 1),
                Keys.PageDown => _offset + PageRows - 1,
                Keys.Home => 0,
                Keys.End => max,
                _ => null,
            };
            if (to is { } offset)
            {
                _offset = Math.Clamp(offset, 0, max);
                continue;
            }
            IsOpen = false;
            return "";
        }
        return null;
    }

    /// <summary>
    /// III Amiga: the Bank's window 9 with the title in its title cell and a page of the report from its first row. The
    /// report is the remaster's, not a list of the original's, so its rows step the font's line height (the Amiga's 8-px
    /// grid left the 7-px glyphs touching, s179); the window grows down to hold them, as any long list.
    /// </summary>
    public AmigaWindowView? View(GameSession session, GameDataRepository data, KeepWindowsLayout layout)
    {
        if (!IsOpen || layout.Bank is not { Text.Count: >= 2 } w)
            return null;
        var t = w.Text;
        var rows = new List<AmigaWindowRow> { new(t[0].X, t[0].Y, GameText.S("core.treasury.title"), false) };
        var page = Body(session).Skip(_offset).Take(PageRows).ToList();
        for (var i = 0; i < page.Count; i++)
            rows.Add(new(t[1].X, t[1].Y + PixelFont.LineHeight * i, page[i], false)); // the remaster's report: its own line height (s180)
        return new AmigaWindowView(w, rows, []);
    }
}
