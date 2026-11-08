using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Printout — town Option entry 6 and Inn menu <c>0x0d</c> entry 2, both OV1 <c>0x46dd</c> [C] (s56 static). DOS checks the
/// printer (<c>0xe41f</c>; nothing happens when it is not ready) and prints, for every party member: two blank lines,
/// <c>"%10s LEVEL:%2d%11s%11s%6s"</c> (name, level <c>@88</c>, race <c>@256</c>, class <c>@220</c>, status <c>@24</c>),
/// <c>"SPELLS:"</c> and one line per known spell (<c>@96 + 2i == 1</c>), then <c>"POTIONS:"</c> and one line per carried
/// item with id 101..120 (<c>@222..@238</c>). The manual says nothing about it; the remaster has no printer, so the
/// lines go to a text file instead (manual-silent detail, handled automatically).
/// </summary>
public static class PartyPrintout
{
    public static IReadOnlyList<string> Lines(Party party, GameDataRepository data)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(data);
        var lines = new List<string>();
        foreach (var m in party.Members)
        {
            lines.Add("");
            lines.Add("");
            lines.Add(string.Format(UiText.S("printout.x_level_x_x_x_x"),
                m.Name, m.Level,
                InspectDialog.Named(InspectDialog.RaceNames, (int)m.Race),
                InspectDialog.Named(InspectDialog.ClassNames, (int)m.Class),
                InspectDialog.Named(InspectDialog.StatusNames, (int)m.Status)));
            lines.Add(UiText.S("printout.spells"));
            for (var i = 0; i < m.SpellsKnown.Length; i++)
                if (m.SpellsKnown[i] == 1)
                    lines.Add(data.Spells.FirstOrDefault(s => s.Id == i)?.Name ?? $"#{i}");
            lines.Add(UiText.S("printout.potions"));
            foreach (var id in m.InventoryItemIds.Where(id => id is > 100 and < 121))
                lines.Add(data.Items.FirstOrDefault(it => it.Id == id)?.Name ?? $"#{id}");
        }
        return lines;
    }

    /// <summary>Writes the printout beside the save file; returns the path.</summary>
    public static string WriteFile(Party party, GameDataRepository data) => WriteLines(Lines(party, data));

    /// <summary>
    /// Writes <paramref name="lines"/> as a printout beside the save file (s163: also a scroll read in I Amiga's town,
    /// h29 <c>+0x24e</c>'s YES); returns the path.
    /// </summary>
    public static string WriteLines(IEnumerable<string> lines)
    {
        var dir = ClientSettings.DataDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"printout-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllLines(path, lines);
        return path;
    }
}
