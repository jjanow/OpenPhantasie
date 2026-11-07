namespace OpenPhantasie.Core;

/// <summary>The rolls of one night encounter (OV2 <c>0x70ed..0x71fe</c>), drawn before the fight is built.</summary>
/// <param name="MonsterId">0-based first monster (DOS <c>[0x64e6]</c> − 1 = <c>r + 12·T</c>).</param>
/// <param name="WatchSlot">Party slot on watch.</param>
/// <param name="Heard">The watcher was not DEAD and passed the listen roll: nobody sleeps.</param>
public sealed record NightEncounterRoll(int MonsterId, int WatchSlot, bool Heard);

/// <summary>
/// Night encounter [C] (<c>overworld-movement.md</c> §Night encounter; DOSBox s35 §2). It is the first branch of the
/// random wilderness encounter <c>0x70ed</c>: <c>rand0_99 &lt; 35</c> — no link to the travel-time counter. One random
/// party member is on watch; if it is alive and <c>rand0_99 &lt; listen @40</c> it hears the monsters. Otherwise every OK
/// member falls asleep except the watcher. The fight opens normally (1,1). Manual p.12 agrees.
/// </summary>
public static class NightEncounter
{
    /// <summary><c>rand0_99 &lt; 35</c> (<c>0x7104</c>).</summary>
    public const int Chance = 35;

    public static string Banner => GameText.S("core.encounter.a_night_encounter");

    /// <summary>
    /// The heard line's second half on a row of its own: a front end whose original shows "%s IS ON WATCH" / "AND HEARS
    /// THE MONSTERS" on two rows (I Amiga, requester 23 at h74 <c>+0x1b6</c>, s139) splits the line there (pack
    /// <c>map_message.splits</c>). Core's log keeps the one line.
    /// </summary>
    public static string HeardSecondRow => GameText.S("core.encounter.and_hears_the_monsters");

    /// <summary>
    /// Draws, in DOS order: the night roll; the monster (<c>r = rand0_99·5/100</c>, redrawn while 2 or 3, so
    /// <c>r ∈ {0, 1, 4}</c>; id <c>r + 12·<paramref name="terrainSubcode"/></c>); the watch slot
    /// <c>rand0_99·size/100</c>; and, only for a non-DEAD watcher, the listen roll. Returns null when the encounter is
    /// not a night one (then <see cref="WildernessEncounter.RollDay"/>, the day branches at <c>0x723b</c>).
    /// </summary>
    /// <param name="terrainSubcode"><c>[0x649c]</c> = tile % 10 of the tile the party stands on.</param>
    public static NightEncounterRoll? TryRoll(Party party, int terrainSubcode, IRandomSource rng, EncounterRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rng);
        if (party.Members.Count == 0)
            throw new InvalidOperationException("No party for a night encounter.");

        var er = rules ?? EncounterRules.PhantasieI;
        int Roll(IRandomSource r) => er.Roll100(r);
        if (Roll(rng) >= Chance)
            return null;

        int r;
        do
            r = Roll(rng) * 5 / 100;
        while (r is 2 or 3);
        var monsterId = r + 12 * terrainSubcode;

        var watch = Roll(rng) * party.Members.Count / 100;
        // II r = 100 gives w = party size: the record past the last member (encounters.md §4, live s82). Without an elemental
        // its LISTEN is 0, so nobody hears and the party sleeps; see ApplyAfterPrep for the line. With six members and a
        // Guild elemental that record is the elemental's (Watcher).
        var watcher = Watcher(party, watch);
        var heard = watcher is not null && watcher.Status != CharacterStatus.Dead && Roll(rng) < watcher.Skills.Listen;
        return new NightEncounterRoll(monsterId, watch, heard);
    }

    /// <summary>
    /// After the fight's prep (every non-DEAD member OK): if nobody heard, every OK member goes to SLEEP and the watcher,
    /// if not DEAD, is set back to OK (<c>0x7183..0x71cd</c>). Returns the message lines ("%s IS ON WATCH" [+ "AND HEARS
    /// THE MONSTERS"]); a DEAD watcher prints nothing.
    /// </summary>
    public static IReadOnlyList<string> ApplyAfterPrep(Party party, NightEncounterRoll roll)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(roll);
        var watcher = Watcher(party, roll.WatchSlot);
        if (roll.Heard && watcher is not null)
            return [GameText.F("core.encounter.x_on_watch_hears", watcher.Name)];

        foreach (var m in party.Members)
        {
            if (m.Status == CharacterStatus.Ok)
                m.Status = CharacterStatus.Sleep;
        }

        // II, r = 100 with six members: the watcher is party record 6, which the group builder marks status OK at every
        // fight build (0xa7ba) and which holds no name, so the ST prints " IS ON WATCH" with a blank name (live s82,
        // encounters.md §4). Other sizes read a record whose status is 0 (DEAD): records past the party are zeroed when the
        // party loads, and the defeat compaction 0xc14a clears the status of every record it vacates. So no line and
        // everyone sleeps (live s83, three members before the run's first fight; encounters.md §4).
        if (watcher is null && roll.WatchSlot == Party.MaxMembers && party.Members.Count == Party.MaxMembers
            && party.Elemental is null)
            return [GameText.F("core.encounter.x_on_watch", "")];
        if (watcher is null || watcher.Status == CharacterStatus.Dead)
            return [];
        watcher.Status = CharacterStatus.Ok;
        return [GameText.F("core.encounter.x_on_watch", watcher.Name)];
    }

    /// <summary>
    /// The member on watch in slot <paramref name="slot"/>, or null past the party. II's r = 100 slot is the record past the
    /// party; with six members that is record 6, where a Guild elemental lives while the party size stays 6 (the fight
    /// start raises it to 7 only when record 6 holds one: ST <c>0x83e2</c> tests <c>0x258ee</c> / <c>0x25932</c>), so the
    /// elemental itself watches: its status, its LISTEN, its name (s92, static <c>[C]</c>; not seen live).
    /// </summary>
    private static Character? Watcher(Party party, int slot)
    {
        if (slot < party.Members.Count)
            return party.Members[slot];
        if (slot == Party.MaxMembers && party.Members.Count == Party.MaxMembers && party.Elemental is { } elemental
            && !party.Members.Contains(elemental))
            return elemental;
        return null;
    }


}
