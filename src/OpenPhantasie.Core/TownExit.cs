namespace OpenPhantasie.Core;

/// <summary>
/// Party set-up when leaving town [C] (OV1 <c>0x3249</c>, town menu Exit = case 3 of the table at OV1 <c>0x208f</c>;
/// save-file-format.md @28). For every party slot, DEAD included:
/// <c>@28 = (3·level·|7 − 2·class| + 11·STR) / 66</c> and <c>@260 = 2·slot + 12</c>. The same pass also
/// computes the average level <c>[0x63ec]</c> (<see cref="GameSession.TownExitAverageLevel"/>: the next town entry's
/// Armory restock, <see cref="ArmoryRestock"/>; combat surrender reads it live) and the highest SCORE <c>[0x63ea]</c>
/// (<see cref="GameSession.HighestScore"/>; sole reader OV2 <c>0x7246</c>, the Black Knights flee test, s41 xref). DOS refuses to leave with an empty party
/// (`core.town.you_must_assemble_a_group`).
/// </summary>
public static class TownExit
{
    /// <summary>OV1 string at file <c>0x5eb2</c>, printed when Exit is chosen with no party members.</summary>
    public static string EmptyPartyMessage => GameText.S("core.town.you_must_assemble_a_group");

    public static void Apply(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        for (var slot = 0; slot < party.Members.Count; slot++)
        {
            var m = party.Members[slot];
            m.WeaponDamageA = WeaponDamageA(m.Level, m.Class, m.Stats.Str);
            m.Phantasie1.PartyOrderStamp = 2 * slot + 12;
        }
    }

    /// <summary><c>(3·level·|7 − 2·class| + 11·STR) / 66</c>, class ids Thief 0 … Wizard 5 (factor 7/5/3/1/1/3).</summary>
    public static int WeaponDamageA(int level, CharacterClass cls, int strength) =>
        (3 * level * Math.Abs(7 - 2 * (int)cls) + 11 * strength) / 66;
}
