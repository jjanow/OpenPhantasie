namespace OpenPhantasie.Core;

/// <summary>
/// Old age — runs on town entry [C] (OV1 <c>0x1954</c>, town-menus.md §Aging). Thresholds use the
/// remaster lifespan unit (age-clock units, DIFFERENCES.md D-003): decline once past 70 % of the
/// lifespan, die past 100 %. DOS compares <c>@264 &gt;&gt; 8</c> with <c>@252</c> instead.
/// </summary>
public static class Aging
{
    /// <summary>One DOS lifespan step <c>@252</c> = <c>@264 &gt;&gt; 8</c> = 256 age-clock units (≈ 9.85 years).</summary>
    public const int DosLifespanStep = 256;

    /// <summary>
    /// [C] OV1 <c>0x1954</c>: past 70 % of the lifespan a character grows older (once). Also the shared aging model's point
    /// (QoL <c>shared-aging</c>, <see cref="SharedAgingModel"/>), so IV compares the same way under it.
    /// </summary>
    public static bool IsPastDecline(int age, int span) => age > span * 7 / 10;

    /// <summary>[C] OV1 <c>0x1954</c>: past the lifespan a character dies (the shared model's rule too).</summary>
    public static bool IsPastLifespan(int age, int span) => age > span;

    /// <summary>
    /// Town-entry pass over the active party. Returns the "has aged" / "has died" lines.
    /// [C] (s36) First every party member's age clock — DEAD members too, DOS adds to all 7 slots — gains
    /// <see cref="TravelClock.AgeUnits"/> of <paramref name="travelTime"/> (<c>(time &gt;&gt; 8) / 7</c>, OV1 <c>0x1995</c>);
    /// the caller then resets its counter (OV1 <c>0x19d0</c>). The party-gold split (OV1 <c>0x167d</c>) stays Core's
    /// equal split (D-037). The loaded elemental leaves here (OV1 <c>0x19d3</c>, s33).
    /// [C]+[D✓] (s49) When slot 7 holds an elemental, DOS lowers the party size <c>[0x648e]</c> before the aging loop
    /// (OV1 <c>0x19dc</c>, loop to <c>[0x648e]</c> at <c>0x19eb</c>), so the last adventurer skips aging on that visit
    /// and ages on the next entry. The <c>@254</c> write (<c>0x1b44</c>) loops all 7 slots and is unaffected.
    /// </summary>
    /// <param name="dosLifespans">
    /// D-003 <c>dos</c> (<c>p1.lifespans</c>): compare in DOS's steps, <c>@264 &gt;&gt; 8</c> with <c>@252</c> (the lifespan
    /// / <see cref="DosLifespanStep"/>), as OV1 <c>0x1954</c> does.
    /// </param>
    /// <param name="ageUnits">The age-clock units this entry adds; null = I's <see cref="TravelClock.AgeUnits"/>.</param>
    /// <param name="ageTheDead">I / II: DEAD members age too (DOS adds to all 7 slots); III skips them (ovl3 <c>0x65a</c>).</param>
    public static IReadOnlyList<string> ApplyTownEntry(
        Party party, int townId, int travelTime = 0, bool dosLifespans = false, int? ageUnits = null, bool ageTheDead = true)
    {
        ArgumentNullException.ThrowIfNull(party);
        var travelAge = ageUnits ?? TravelClock.AgeUnits(travelTime);
        foreach (var m in party.Members)
            if (ageTheDead || m.Status != CharacterStatus.Dead)
                m.AgeClock += travelAge; // @264 += (time >> 8) / 7, OV1 0x19bb
        var skipped = party.Elemental is not null ? party.Adventurers.LastOrDefault() : null; // OV1 0x19dc
        party.DismissElemental();
        party.Elemental = null; // [C] OV1 0x19d3: slot 7 cleared on town entry. II plays I's here: the ST's town entry is [?] (s172)
        var lines = new List<string>();
        foreach (var m in party.Members)
        {
            if (ReferenceEquals(m, skipped))
                continue;
            if (m.Lifespan <= 0)
                continue; // unknown lifespan (record without @252) — never age it out
            var (age, span) = dosLifespans
                ? (m.AgeClock >> 8, m.Lifespan / DosLifespanStep) // years = @264 >> 8 vs @252 (town-menus.md §Aging)
                : (m.AgeClock, m.Lifespan);
            if (!IsPastDecline(age, span))
                continue;
            if (m.Status == CharacterStatus.Dead)
                continue;

            int k;
            if (IsPastLifespan(age, span))
            {
                lines.Add(GameText.F("core.aging.x_has_died", m.Name));
                m.Status = CharacterStatus.Dead;
                k = 0;
            }
            else if (!m.HasAged)
            {
                lines.Add(GameText.F("core.aging.x_has_aged", m.Name));
                m.HasAged = true;
                k = 4;
            }
            else
            {
                continue; // DOS multiplies by 5/5 here — no change
            }

            var s = m.Stats;
            m.Stats = new CharacterStats(
                s.Str * k / 5, s.Int * k / 5, s.Dex * k / 5, s.Con * k / 5, s.Cha * k / 5, s.Luck * k / 5);
            m.Word12 = m.Word12 * k / 5;
            // [~] DOS recomputes derived fields via OV1 0x2fdb (worn-item pick); Core's nearest equivalent.
            CombatGear.ApplyWornItemPoints(m);
        }

        foreach (var m in party.Members)
            m.CurrentTown = townId; // @254, all party slots (OV1 0x1b57)
        return lines;
    }
}
