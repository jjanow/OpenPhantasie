using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// s165: III's Damage Report (<see cref="P3DamageReport"/>, Amiga <c>0x1d4ea</c>; DOS III's box root <c>0x2623</c>) as its
/// own window over everything: every member's line in one place, none cut (before s165 the lines went to the message
/// band, whose fourth row was cut). Under AMIGA SCREENS with the pack's window it is window 6 with one OK
/// (<see cref="UiOverlays.DrawDamageReportWindow"/>); otherwise the remaster's text panel. The wait is <c>0x3ba0</c> with
/// key 0: any key (a click on OK sends O) closes it and nothing under it sees that key. A report is offered once, when
/// <see cref="GameSession.DamageReportSerial"/> moves on in the same session.
/// </summary>
public sealed class P3DamageReportBox
{
    private GameSession? _session;
    private long _seen;

    /// <summary>The report on screen; null when the box is not up.</summary>
    public P3DamageReport? Report { get; private set; }

    public bool Shown => Report is not null;

    /// <summary>
    /// Opens the box with <paramref name="session"/>'s new report, if it made one since the last call (a session it has
    /// not seen before only sets the mark). True when it opened.
    /// </summary>
    public bool Offer(GameSession? session)
    {
        if (session is null)
            return false;
        if (!ReferenceEquals(session, _session))
        {
            _session = session;
            _seen = session.DamageReportSerial;
            Report = null;
            return false;
        }
        if (session.DamageReportSerial == _seen)
            return false;
        _seen = session.DamageReportSerial;
        if (session.Game != GameId.P3 || session.DamageReport is not { } report)
            return false;
        Report = report;
        return true;
    }

    /// <summary>While up the box owns the frame's keys: any key closes it. True when it was up (the keys are used up).</summary>
    public bool HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (!Shown)
            return false;
        if (newKeys.Any())
            Report = null;
        return true;
    }

    /// <summary>Drops the box (a load, the main menu).</summary>
    public void Close() => Report = null;

    /// <summary>The remaster's panel lines: the title, then "NAME  line" a member.</summary>
    public static IReadOnlyList<string> PanelLines(P3DamageReport report) =>
        [P3DamageReport.Title, .. report.Rows.Select(r => $"{r.Name,-8}  {r.Text(report.Kind)}")];

    /// <summary>
    /// <paramref name="text"/> (a band line) with the report's noted lines taken out, blanks closed up; the report's lead
    /// line ("A trap!!!") when nothing else was there. The window holds those lines (s165).
    /// </summary>
    public static string WithoutReportLines(string text, P3DamageReport report)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        // Whole notes only, longest first, each where it starts the text or follows a blank and ends it or meets one
        // (a note inside a longer name's line is left alone).
        var left = text;
        foreach (var note in report.Notes.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct().OrderByDescending(n => n.Length))
            left = System.Text.RegularExpressions.Regex.Replace(left,
                @"(?<=^|\s)" + System.Text.RegularExpressions.Regex.Escape(note) + @"(?=$|\s)", "");
        left = System.Text.RegularExpressions.Regex.Replace(left, @"\s{2,}", " ").Trim();
        return left.Length > 0 || left == text ? left : report.Lead;
    }
}
