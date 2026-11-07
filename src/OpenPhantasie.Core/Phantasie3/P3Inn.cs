namespace OpenPhantasie.Core;

/// <summary>
/// III's inn (ovl2 <c>0x347d</c>, s136; <c>body-areas.md</c> section 4 "The inn routine"), town and wilderness alike:
/// each member in party order "is resting."; a Dead one " in peace." and nothing else; a living one ages one stay, gets
/// the magic ceiling of their class and level (the manual's Maximum Magic Points table, <c>DS:0xafb</c>, levels above 15
/// read 15), may gain a magic point (<c>20 · (ceiling − max) &gt; rnd(100)</c>, "Magic increases!"), and has magic and HP
/// set to their maximum. The body areas heal by the caller's rule (town: every Injured / Broken area; wilderness: 2 cure
/// points). Who may gain is D3-069 (DECIDED s137: the manual): a member who cast a spell outside town (a fight, the
/// wilderness or a dungeon) since the last stay, <see cref="Character.CastSinceRest"/>; DOS instead rolls for every
/// living member on the first stay after a fight.
/// </summary>
public static class P3Inn
{
    /// <summary>The highest level the ceiling table holds (<c>min(level, 15)</c>).</summary>
    public const int CeilingLevels = 15;

    /// <summary>Runs the stay over the party; returns each member's line.</summary>
    internal static IReadOnlyList<string> Stay(GameSession session)
    {
        var tables = session.CampaignAccess.CreationTables;
        var rng = session.CampaignAccess.Rng;
        var lines = new List<string>();
        foreach (var m in session.Party.Members)
        {
            if (m.Status == CharacterStatus.Dead)
            {
                lines.Add(GameText.F("core.inn.x_is_resting_x", m.Name, GameText.S("core.inn.in_peace")).TrimEnd());
                continue;
            }
            m.AgeClock += TownMenus.InnStayAgeClockUnits;
            m.MpCeiling = Ceiling(tables, m);
            var raised = false;
            if (m.CastSinceRest && m.MagicMax < m.MpCeiling // D3-069 manual (DOS: "no stay since the last fight")
                && 20 * (m.MpCeiling - m.MagicMax) > P3Dice.Rnd(rng, 100))
            {
                m.MagicMax++;
                raised = true;
            }
            m.CastSinceRest = false;
            m.MagicCur = m.MagicMax;
            m.HpCur = m.HpMax;
            lines.Add(GameText.F("core.inn.x_is_resting_x", m.Name,
                raised ? GameText.S("core.inn.magic_increases") : "").TrimEnd());
        }
        return lines;
    }

    /// <summary>The magic ceiling of <paramref name="m"/>'s class at their level (<c>DS:0xafb[15·class + min(level, 15)]</c>).</summary>
    public static int Ceiling(CharacterCreationTables tables, Character m)
    {
        var row = tables.MpCeilingByClass[(int)m.Class];
        return row[Math.Clamp(m.Level, 1, Math.Min(CeilingLevels, row.Count)) - 1];
    }
}
