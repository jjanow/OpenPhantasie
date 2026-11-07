using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s154: the texts of I Amiga's requester-21 reports (Amiga <c>FORMAT.md</c> "Requester 21 on the map") where they are the
/// Amiga's own rather than Core's (DOS's): the titles (<see cref="MapMessageWindow.Titles"/>, leading blanks kept), the map's
/// Health report (h50 <c>+0x420</c>) and the fight's Party health (h44 <c>+0x1478</c>). No MonoGame types.
/// </summary>
public static class P1MapReports
{
    /// <summary>The UI string ids the pack names for these windows (for the string table's checks).</summary>
    public static IEnumerable<string> StringKeys(MapMessageWindow? window) =>
        window is null ? [] : window.Titles.Values.Concat(window.RowTexts.Values)
            .Concat(new[] { window.HealthTitle, window.HealthRow }.OfType<string>())
            .Append("p1field.health_row");

    /// <summary>
    /// <paramref name="lines"/> (a report as Core logged it: the title, then the rows) with the title the original prints
    /// in its place when the window lists one (<see cref="MapMessageWindow.Titles"/>); the rows as logged.
    /// </summary>
    public static IReadOnlyList<string> Report(MapMessageWindow window, IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
            return lines;
        var title = lines[0].Trim();
        IReadOnlyList<string> rows = [.. lines.Skip(1).Select(r => Row(window, r))];
        foreach (var (coreId, uiId) in window.Titles)
            if (GameText.S(coreId).Trim() == title)
                return [UiText.S(uiId), .. rows];
        return [lines[0], .. rows];
    }

    /// <summary>
    /// s156: <paramref name="row"/> in the original's text when the window lists its Core format (<see cref="MapMessageWindow.RowTexts"/>):
    /// a Core text <c>"{0,-N}rest"</c> (or <c>"{0,-N:.M}rest"</c>, a cut name) whose row ends in <c>rest</c> gives the name back (its first N cells, trimmed).
    /// </summary>
    public static string Row(MapMessageWindow window, string row)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(row);
        foreach (var (coreId, uiId) in window.RowTexts)
        {
            var m = System.Text.RegularExpressions.Regex.Match(GameText.S(coreId), @"^\{0,-(\d+)(?::\.\d+)?\}(.*)$");
            if (!m.Success)
                continue;
            var n = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (row.Length >= n && row[n..] == m.Groups[2].Value)
                return UiText.F(uiId, row[..n].TrimEnd());
        }
        return row;
    }

    /// <summary>
    /// The map's Health report as I Amiga shows it (h50 <c>+0x420…+0x54a</c>): the title, then one row per member,
    /// <c>" %-10.9s%-6.5sHP%4d/%-4dMP%4d/%-3d "</c> (h51 <c>+0x8b</c>; status DEAD / OK / SLEEP, h12 <c>+0xb78</c>). Null when the
    /// pack does not name its texts.
    /// </summary>
    public static IReadOnlyList<string>? MapHealth(MapMessageWindow window, Party party)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(party);
        if (window.HealthTitle is not { } title || window.HealthRow is not { } row)
            return null;
        return [UiText.S(title), .. party.Members.Select(m =>
            UiText.F(row, Cut(m.Name, 9), Cut(Status(m), 5), m.HpCur, m.HpMax, m.MagicCur, m.MagicMax))];
    }

    /// <summary>
    /// Party options › Party health as I Amiga shows it (h44 <c>+0x1478</c>): "HEALTH REPORT:" (h45 <c>+0x107</c>), then one row
    /// per party slot, <c>"%-10.9s%c HP%4d/%-4dA:%-3.2dMP%4d/%-3d "</c> (h45 <c>+0x116</c>; status letter from h45 <c>+0x104</c>
    /// "D S": DEAD 'D', OK ' ', asleep 'S'; A = <c>@250 / 75 + 1</c>). The program's printf (h137 → h167) reads a precision but
    /// uses it only for <c>%s</c> and the floats, so <c>%-3.2d</c> prints as <c>%-3d</c> (s156 static [C]; the s128 shot showed
    /// the same for the orders rows' <c>%-4.3d</c>). Null when the pack does not name its title.
    /// </summary>
    public static IReadOnlyList<string>? FightHealth(MapMessageWindow window, GameSession session)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(session);
        if (window.HealthTitle is not { } title)
            return null;
        var members = session.Party.Members;
        var prep = session.Combat?.Prep;
        var rows = new List<string> { UiText.S(title) };
        for (var i = 0; i < members.Count; i++) // slot order, a summoned elemental included (h4 +0)
        {
            var m = members[i];
            var hitEff = prep is not null && i < prep.Length ? prep[i].WeaponHitEff : m.Skills.Attack;
            var letter = m.Status switch { CharacterStatus.Dead => 'D', CharacterStatus.Sleep => 'S', _ => ' ' };
            rows.Add(UiText.F("p1field.health_row", Cut(m.Name, 9), letter, m.HpCur, m.HpMax, hitEff / 75 + 1, m.MagicCur,
                m.MagicMax));
        }
        return rows;
    }

    private static string Status(Character m) => m.Status switch
    {
        CharacterStatus.Dead => UiText.S("p1field.status.dead"),
        CharacterStatus.Sleep => UiText.S("p1field.status.sleep"),
        _ => UiText.S("p1field.status.ok"),
    };

    private static string Cut(string s, int n) => s.Length > n ? s[..n] : s;
}
