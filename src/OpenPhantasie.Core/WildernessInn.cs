namespace OpenPhantasie.Core;

/// <summary>
/// The independent inn on overworld tile 41 (OV2 <c>0x6700</c>, <c>town-menus.md</c> §Wilderness inn) [C].
/// The enter prompt (menu <c>0x16</c>) is the Client's; call <see cref="Stay"/> once the party has said yes.
/// </summary>
public static class WildernessInn
{
    /// <summary>Price: needs party gold &gt; 255, takes 256 (<c>sub [0x63f6],0x100</c>; manual "250 GP plus tax", D-005).</summary>
    public const int Price = 256;

    public static string CantAffordMessage => GameText.S("core.inn.you_cant_afford_it");

    /// <summary>
    /// One stay. A member who cast since the last rest rolls <c>rand0_99 &lt; 20·(@14 − @16)</c> for +1 MP max
    /// (`core.inn.gains_more_magic`; D-046 manual cap — DOS uses ≤); then MP and HP are refilled (sleepers too, not woken).
    /// DEAD members get no line and no MP roll but are refilled too, status unchanged (D-047 manual — DOS skips them). DOS then adds 1000 travel-time units (≈ 3.9 days); the remaster instead adds one age-clock unit
    /// (two weeks) to every party member, DEAD too (D-008, like the town Inn's D-004).
    /// </summary>
    /// <remarks>
    /// [~] Gold: DOS has one purse <c>[0x63f6]</c>; Core splits it into <see cref="Party.GoldCarried"/> and the found
    /// gold <see cref="LootSack.Gold"/>. The "&gt; 255" test uses their sum and the 256 comes from the carried purse
    /// first. <c>[0x64fe] := 1</c> after the stay is a dead flag [C]: four writers (OV2 <c>0x41fc</c>, <c>0x66f1</c>,
    /// <c>0x682f</c>, <c>0x696d</c>), no reader (overworld-movement.md) — not modelled.
    /// </remarks>
    /// <remarks>
    /// <paramref name="rules"/> (default: the manual) can play DOS: <c>p1.wild-inn-time</c> / <c>p2.wild-inn-time</c> (D-008 / D2-042, no age here; the session
    /// adds the 1000 travel-time units), <c>p1.inn-magic-cap</c> (D-046, <c>&lt;=</c>) and <c>p1.wild-inn-dead</c> (D-047,
    /// the dead skipped).
    /// </remarks>
    public static TownTransactionResult Stay(Party party, LootSack sack, IRandomSource rng, GameOptions? rules = null)
    {
        rules ??= GameOptions.ManualPhantasieI;
        var ages = !rules.WildernessInnTravelTime;
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(sack);
        ArgumentNullException.ThrowIfNull(rng);

        // The purse first, then found gold, then the Guild treasury when it is on (s175).
        if (!GoldPurse.TrySpend(Price, party.Treasury, TreasuryEntryKind.Inn,
                (() => party.GoldCarried, v => party.GoldCarried = v), (() => sack.Gold, v => sack.Gold = v)))
            return new TownTransactionResult { Kind = TownTransactionKind.Failed, Message = CantAffordMessage };

        var lines = new List<string> { GameText.S("core.inn.in_the_inn") };
        foreach (var m in party.Members)
        {
            if (m.Status == CharacterStatus.Dead)
            {
                // D-047 (user s37: manual "each character", like the town Inn): DOS skips the dead; the remaster refills them.
                if (!rules.IsDos(RuleOptionRegistry.WildernessInnDead))
                {
                    m.MagicCur = m.MagicMax;
                    m.HpCur = m.HpMax;
                }
                if (ages)
                    m.AgeClock += TownMenus.InnStayAgeClockUnits; // D-008: the stay's two weeks pass for the dead too
                continue;
            }

            var gains = false;
            if (m.CastSinceRest)
            {
                m.CastSinceRest = false;
                // D-046 (user s37: manual): DOS uses <=, so at gap 0 a roll of 0 passed and MP max went one past the
                // manual's "Maximum Magic Points" table. The remaster uses < (as the town Inn does) and never exceeds it.
                var roll = rng.Next(0, 100);
                var gap20 = (m.MpCeiling - m.MagicMax) * 20;
                gains = rules.InnMagicPastTable ? roll <= gap20 : roll < gap20;
                if (gains)
                    m.MagicMax++;
            }

            lines.Add(Pad(m.Name) + (gains ? GameText.S("core.inn.gains_more_magic") : GameText.S("core.inn.is_resting")));
            m.MagicCur = m.MagicMax;
            m.HpCur = m.HpMax;
            if (ages)
                m.AgeClock += TownMenus.InnStayAgeClockUnits; // D-008 (DOS: time += 1000 instead)
        }

        return new TownTransactionResult
        {
            Kind = TownTransactionKind.Ok,
            Message = string.Join("; ", lines),
            Lines = lines,
            GoldDelta = -Price,
        };
    }

    /// <summary><c>%-10.9s</c>: at most 9 characters, left-justified in 10.</summary>
    private static string Pad(string name) => (name.Length > 9 ? name[..9] : name).PadRight(10);
}
