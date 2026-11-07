namespace OpenPhantasie.Core;

/// <summary>The Great Demon's judgement on one dead member (OV1 <c>0x3bc</c>, endgame.md §4 [C]).</summary>
public enum DefeatFate
{
    /// <summary>OV1 <c>0x1ef</c>: removed from the party and the roster.</summary>
    Destroyed,

    /// <summary>OV1 <c>0x18e</c>: revived (status OK, HP max, bank <c>@262 &amp;= 0xff</c>).</summary>
    Resurrected,

    /// <summary>OV1 <c>0x113</c>: race UNDEAD (15), revived, level 20, <c>@240 := 15</c>.</summary>
    Undead,
}

public sealed class PartyDefeatResult
{
    /// <summary>Each member's fate, in party order (the summoned slot 7 is removed first and gets none).</summary>
    public IReadOnlyList<(Character Member, DefeatFate Fate)> Fates { get; init; } = Array.Empty<(Character, DefeatFate)>();

    /// <summary>At least one member came back (OV1 returns 2): the party wakes on the overworld at OUT1 (20,5).</summary>
    public bool AnyRevived => Fates.Any(f => f.Fate != DefeatFate.Destroyed);

    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Phantasie II's 5th defeat of the run (ST counter <c>0x231fa</c>, <c>0x12de4</c>: increment, then <c>== 5</c>) [C] (live
    /// s82): as the door opens, a sign "Sorry, we've run out of Demons" shows behind it before the Great Demon appears.
    /// Cosmetic; always false in Phantasie I.
    /// </summary>
    public bool DemonsRanOut { get; init; }
}

/// <summary>
/// Party defeat — state 6 → OV1 <c>0x5ee</c> → <c>0x3bc</c> (endgame.md §4 [C]; D-025 NOT A CONFLICT, DOS rule).
/// "YOUR PARTY HAS BEEN DEFEATED", the Astral Plane, and the Great Demon's judgement of each (dead) member.
/// </summary>
public static class PartyDefeat
{
    public const int UndeadLevel = 20;
    public const int UndeadWeaponPoints = 15;

    /// <summary>Where survivors wake: region OUT1, (20, 5) — the OUT1 "SMALL TEMPLE" feature (<c>[0x63e4]</c> = x / <c>[0x63e6]</c> = y [C], OV2 <c>0x5eaa</c>).</summary>
    public const int WakeRegion = 1;
    public const int WakeX = 20;
    public const int WakeY = 5;

    /// <summary>
    /// Judge every member and clear the party's loot. RNG draws in DOS order: none when CON = 0; one <c>rand</c> for the
    /// resurrect test only when level &gt; 2; one more for UNDEAD when that test is reached. <c>rand</c> = 0..32767.
    /// </summary>
    /// <param name="keepFoundItems">Phantasie II (ST <c>0x12678</c>): only the XP pool and the found gold are cleared.</param>
    public static PartyDefeatResult Resolve(Party party, GuildRoster roster, LootSack sack, IDosRandomSource rng,
        bool keepFoundItems = false)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(sack);
        ArgumentNullException.ThrowIfNull(rng);

        var log = new List<string>
        {
            GameText.S("core.defeat.your_party_has_been_defeated"),
            GameText.S("core.defeat.arrived_at_astral_plane"),
            GameText.S("core.defeat.great_demon_speaks"),
        };

        // partySize 7 -> 6, then slot 7 removed (0x1bae(0x624c)): the summoned/loaded elemental is gone.
        party.DismissElemental();
        party.Elemental = null;

        var fates = new List<(Character, DefeatFate)>();
        foreach (var m in party.Members.ToList())
        {
            var fate = Judge(m, rng);
            fates.Add((m, fate));
            switch (fate)
            {
                case DefeatFate.Resurrected:
                    Revive(m);
                    log.Add(GameText.F("core.defeat.x_resurrected", m.Name));
                    break;
                case DefeatFate.Undead:
                    m.Race = Race.Undead; // @256 := 15; @216 (BirthRace) keeps the birth race
                    Revive(m);
                    log.Add(GameText.F("core.defeat.x_status_undead", m.Name));
                    m.Level = UndeadLevel;
                    m.WeaponDamageB = UndeadWeaponPoints; // @240 := 15
                    break;
                default:
                    log.Add(GameText.F("core.defeat.x_destroyed", m.Name));
                    party.Members.Remove(m); // 0x1bae; the free-slot list bookkeeping is [~]
                    for (var i = 0; i < roster.Slots.Length; i++)
                        if (ReferenceEquals(roster.Slots[i], m))
                            roster.Slots[i] = null;
                    m.InParty = false;
                    break;
            }
        }

        // [0x63f4] XP pool, [0x63f6] found gold, found items [0x63fa..] := 0. [0x63f2] (travel time) is cleared by the
        // session (GameSession.ResolvePartyDefeat).
        if (keepFoundItems)
        {
            sack.Experience = 0;
            sack.Gold = 0;
        }
        else
            sack.Clear();
        return new PartyDefeatResult { Fates = fates, Log = log };
    }

    /// <summary>One member's fate (OV1 <c>0x40d..0x437</c>).</summary>
    public static DefeatFate Judge(Character m, IDosRandomSource rng)
    {
        if (m.Stats.Con == 0)
            return DefeatFate.Destroyed;
        var level = m.Level;
        if (level > 2 && rng.NextDosWord() / 0x2000 < level - 2)
            return DefeatFate.Resurrected;
        if (rng.NextDosWord() / 0x2aab < level)
            return DefeatFate.Undead;
        return DefeatFate.Destroyed;
    }

    /// <summary>Revive helper OV1 <c>0x16b</c>: status OK, bank <c>@262 &amp;= 0xff</c>, HP := max.</summary>
    private static void Revive(Character m)
    {
        m.Status = CharacterStatus.Ok;
        m.GoldInBank &= 0xff;
        m.HpCur = m.HpMax;
    }
}
