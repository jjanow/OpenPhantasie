namespace OpenPhantasie.Core;

/// <summary>
/// One Phantasie III fight's own state (<see cref="CombatEncounter.CampaignState"/>, built by
/// <see cref="Phantasie3Campaign.BuildEncounter"/>): the hit-location odds, the "tall monster" flag (root <c>0x104c0</c>),
/// every monster's six body areas (group record <c>+0x4c + 10k + 2m</c>) and the bow spells in effect, which last the
/// rest of the fight (<c>bows.md</c> §5), and the round loop's own state (<see cref="P3RoundResolver"/>: wake chance, the
/// next round's mode, the free Flee, the per-fight attack % and armour points the monster spells lower). Saved with the
/// fight (<see cref="Capture"/> / <see cref="Restore"/>, <see cref="P3FightSnapshot"/>, s95).
/// </summary>
public sealed class P3Fight
{
    private readonly Dictionary<Monster, P3BodyAreas> _bodies = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Character, P3BowSpell> _bowSpells = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Character> _accuracy = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Character, int> _attack = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Character, int> _armour = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Monster, P3BodyAreas> _statusBodies = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Character> _parried = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Character> _parryBlock = new(ReferenceEqualityComparer.Instance);

    /// <summary>Wake chance <c>FAR:0xb628</c> at fight start (<c>combat-round.md</c> §4).</summary>
    public const int WakeChanceStart = 15;

    /// <summary>Wake chance <c>FAR:0xb628</c>: 15 at fight start, +15 after every round that leaves the fight on, at most 100.</summary>
    public int WakeChance { get; set; } = WakeChanceStart;

    /// <summary>How the next round runs (<c>combat_round(mode)</c>): normal, monsters only (failed Flee) or party only.</summary>
    public P3RoundMode NextRound { get; set; }

    /// <summary><c>FAR:0xb5c2</c>: the party surprised the monsters, so the first menu's Flee is free; cleared by the first round.</summary>
    public bool FreeFlee { get; set; }

    /// <summary>
    /// <c>FAR:0xb5c0</c>: Awaken (#51, <c>0xca27</c>) was cast this round; at its end every Asleep party member wakes
    /// (<c>0xdd85</c>).
    /// </summary>
    public bool AwakenAtRoundEnd { get; set; }

    /// <summary>Monster strength at fight start (<c>FAR:0x9816</c>), for the give-up test at the end of each round.</summary>
    public int StartStrength { get; set; }

    public required P3HitLocationTable HitLocations { get; init; }

    /// <summary>The first group is a tall monster: every hit on a monster uses row 4.</summary>
    public bool TallMonster { get; init; }

    /// <summary>Monsters whose injury lines say "'s body" (0-based ids).</summary>
    public IReadOnlySet<int> LimblessMonsterIds { get; init; } = new HashSet<int>();

    /// <summary>The items' point values.</summary>
    public required IItemCatalog Items { get; init; }

    /// <summary>The party's Fireflash numbers (D3-075, <see cref="P3World.Fireflash"/>).</summary>
    public P3FireflashTable Fireflash { get; init; } = P3FireflashTable.Default;

    /// <summary>The spell names by 0-based index (<see cref="P3World.SpellNames"/>).</summary>
    public IReadOnlyList<string> SpellNames { get; init; } = [];

    /// <summary>
    /// A party member's cast line. DOS III's <c>party_casts</c> (root <c>0xbf9a</c>) prints name + " casts " + master string
    /// <c>331 + spell</c> and plays sound 21 [C]; the Amiga prints the same text ("elrod  casts Mindblast IV", §8.6). A
    /// spell without a name falls back to I's "X CASTS A SPELL".
    /// </summary>
    public static string CastLine(CombatEncounter encounter, Character caster, int spellIndex) =>
        Of(encounter) is { } fight && (uint)spellIndex < (uint)fight.SpellNames.Count
            && fight.SpellNames[spellIndex] is { Length: > 0 } spell
            ? GameText.F("core.p3.x_casts_x", caster.Name, spell)
            : GameText.F("core.combat.x_casts_a_spell", caster.Name);

    /// <summary>
    /// The sound of a III cast line (s127, Amiga sound table <c>0x23e1a</c> [C] static): the party's cast line
    /// (<c>0x1620a</c>, as DOS's <c>party_casts</c>) and the monsters' spell lines (<c>0x1673e</c>: Fireflash, Quickness,
    /// Strength, Protection, Mindblast, Fireball, Sleep, and the lowered armour / attack lines) start sound 21 as the line
    /// prints. Breath and the stunning / draining touches play none.
    /// </summary>
    public const int CastSound = 21;

    /// <summary>
    /// Add <paramref name="caster"/>'s cast line (<see cref="CastLine"/>) to <paramref name="log"/>; in a III fight it is
    /// tagged with <see cref="CastSound"/>, in another game with the caster alone.
    /// </summary>
    public static void AddCastLine(CombatEncounter encounter, Character caster, int spellIndex, List<string> log)
    {
        if (Of(encounter) is not null)
            encounter.TagLine(log, caster, null, sound: CastSound);
        else
            encounter.TagLine(log, caster, null); // I / II: the caster, no sound of Core's (I Amiga: its flash, s132)
        log.Add(CastLine(encounter, caster, spellIndex));
    }

    /// <summary>The III state of <paramref name="encounter"/>; null for a fight of another game.</summary>
    public static P3Fight? Of(CombatEncounter encounter) => encounter.CampaignState as P3Fight;

    /// <summary>An item's points (<c>DS:0x113c + id</c>); 0 for no item.</summary>
    public int Points(int itemId) => itemId > 0 ? Items.Get(itemId)?.Points ?? 0 : 0;

    public P3BodyAreas BodyOf(Monster monster) =>
        _bodies.TryGetValue(monster, out var b) ? b : _bodies[monster] = new P3BodyAreas();

    /// <summary>
    /// A monster's status (root <c>0xec1a</c>): dead at 0 HP, else from its body; Out is its "Unconscious". After
    /// compaction moved it, the body it had before still decides until <see cref="RefreshStatus"/> (DOS moves the status
    /// word and recomputes it only on a hit or a wake).
    /// </summary>
    public P3Status StatusOf(Monster monster) =>
        !monster.IsAlive
            ? P3Status.Dead
            : (_statusBodies.GetValueOrDefault(monster) ?? BodyOf(monster)).Status(monster.HitPoints ?? 0,
                monster.StatusFlags.HasFlag(MonsterStatusFlags.Asleep) ? P3Status.Asleep : P3Status.Okay);

    /// <summary>A hit or a wake: the monster's status follows the body in its slot again.</summary>
    public void RefreshStatus(Monster monster) => _statusBodies.Remove(monster);

    /// <summary>The Arrow Flame in effect on the member's bow (None when none was cast this fight).</summary>
    public P3BowSpell BowSpellOf(Character member) => _bowSpells.GetValueOrDefault(member);

    /// <summary>Accuracy / Party Accuracy set the member's bow % to 250 and it still holds (no Arrow Flame since).</summary>
    public bool HasAccuracy(Character member) => _accuracy.Contains(member);

    /// <summary>
    /// A bow spell lands on <paramref name="member"/>. DOS (<c>p3.bow-spells</c> = <c>dos</c>): Arrow Flame (root
    /// <c>0xc3cc</c>) writes both bow words, the % and the damage (so it also ends Accuracy's 250); Accuracy (root
    /// <c>0xca34</c> / <c>0xca46</c>) writes only the % word <c>FAR:0xdd9c + 2·member</c> = 250, so an Arrow Flame's damage
    /// stays. Manual (D3-039, user s96): the spells only "raise" / "increase", so none lowers what an earlier one gave:
    /// Accuracy stays with a later Arrow Flame and Arrow Flame I does not replace II.
    /// </summary>
    public void SetBowSpell(Character member, P3BowSpell spell, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (spell == P3BowSpell.Accuracy)
        {
            _accuracy.Add(member);
            return;
        }
        if (rules.P3DosBowSpells())
        {
            _accuracy.Remove(member);
            _bowSpells[member] = spell;
        }
        else if (spell > BowSpellOf(member))
            _bowSpells[member] = spell;
    }

    /// <summary>
    /// The member's Fire Bow % and damage range this fight (<see cref="P3Combat.BowStats"/>): Accuracy sets the % to 250
    /// (DOS) or to at least 250 (manual, D3-039).
    /// </summary>
    public P3BowStats BowStatsOf(Character member, GameOptions rules)
    {
        var bow = P3Combat.BowStats(Points(member.WornBow), member.P3.FireBow, BowSpellOf(member), rules);
        if (!HasAccuracy(member)) return bow;
        return bow with
        {
            Percent = rules.P3DosBowSpells() ? P3Combat.AccuracyPercent : Math.Max(P3Combat.AccuracyPercent, bow.Percent),
        };
    }

    /// <summary>
    /// A member's attack % this fight (<c>FAR:0xfd10 + 2i</c>, set once per fight from Attack by root <c>0xd730</c>); the
    /// "slowed" monster spells scale it.
    /// </summary>
    public int AttackOf(Character member) => _attack.TryGetValue(member, out var a) ? a : member.Skills.Attack;

    public void SetAttack(Character member, int value) => _attack[member] = value;

    /// <summary>
    /// A member's armour points this fight (<c>FAR:0xb59a + 2i</c> = the worn armour's points, at least 1, root
    /// <c>0xd730</c>): breath soaks them; "armour reduced" scales them.
    /// </summary>
    public int ArmourOf(Character member) =>
        _armour.TryGetValue(member, out var a) ? a : Math.Max(1, Points(member.WornArmor));

    public void SetArmour(Character member, int value) => _armour[member] = value;

    /// <summary>
    /// Compaction (root <c>0xd23e</c>) moves HP, magic and status but leaves the body areas by slot: after
    /// <paramref name="compacted"/> (the same monsters, living first), slot s keeps the body that was in slot s. A monster
    /// that changed slot keeps its old status until its next hit or wake (<see cref="StatusOf"/>).
    /// </summary>
    public void KeepBodiesBySlot(IReadOnlyList<Monster> before, IReadOnlyList<Monster> compacted)
    {
        var bodies = before.Select(BodyOf).ToArray();
        for (var s = 0; s < compacted.Count && s < bodies.Length; s++)
        {
            var m = compacted[s];
            if (!ReferenceEquals(_bodies.GetValueOrDefault(m), bodies[s]) && !_statusBodies.ContainsKey(m))
                _statusBodies[m] = BodyOf(m);
            _bodies[m] = bodies[s];
        }
    }

    // ---- Parry (D3-036, manual) -----------------------------------------------------------------------------------------

    /// <summary>A new round: last round's parries are over.</summary>
    public void ClearParries()
    {
        _parried.Clear();
        _parryBlock.Clear();
    }

    /// <summary>The member's Parry roll succeeded this round: the shield counts against melee and blocks one hit.</summary>
    public void SetParried(Character member)
    {
        _parried.Add(member);
        _parryBlock.Add(member);
    }

    public bool Parried(Character member) => _parried.Contains(member);

    /// <summary>Uses up this round's hit reduction; false when it is spent or the member did not parry.</summary>
    public bool UseParryBlock(Character member) => _parryBlock.Remove(member);

    // ---- Save / load ---------------------------------------------------------------------------------------------------

    /// <summary>The fight's state for a save: monsters in rank order, members in party order.</summary>
    public P3FightSnapshot Capture(CombatEncounter encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        var monsters = encounter.Ranks.SelectMany(r => r.Members).ToList();
        var members = encounter.Party.Members;
        List<int> Indices(ICollection<Character> set) =>
            Enumerable.Range(0, members.Count).Where(i => set.Contains(members[i])).ToList();
        return new P3FightSnapshot
        {
            MonsterBodies = monsters.Select(m => BodyOf(m).ToStates()).ToList(),
            MonsterStatusBodies = _statusBodies.Count == 0
                ? null
                : monsters.Select(m => _statusBodies.GetValueOrDefault(m)?.ToStates()).ToList(),
            BowSpells = members.Select(BowSpellOf).ToList(),
            Accuracy = Indices(_accuracy),
            Attack = members.Select(m => _attack.TryGetValue(m, out var a) ? a : (int?)null).ToList(),
            Armour = members.Select(m => _armour.TryGetValue(m, out var a) ? a : (int?)null).ToList(),
            Parried = Indices(_parried),
            ParryBlock = Indices(_parryBlock),
            WakeChance = WakeChance,
            NextRound = NextRound,
            FreeFlee = FreeFlee,
            AwakenAtRoundEnd = AwakenAtRoundEnd,
            StartStrength = StartStrength,
        };
    }

    /// <summary>Put a saved state back on the restored <paramref name="encounter"/>; entries past its monsters / members are ignored.</summary>
    public void Restore(P3FightSnapshot snap, CombatEncounter encounter)
    {
        ArgumentNullException.ThrowIfNull(snap);
        ArgumentNullException.ThrowIfNull(encounter);
        var monsters = encounter.Ranks.SelectMany(r => r.Members).ToList();
        var members = encounter.Party.Members;
        for (var i = 0; i < monsters.Count; i++)
        {
            if (i < snap.MonsterBodies.Count)
                _bodies[monsters[i]] = P3BodyAreas.FromStates(snap.MonsterBodies[i]);
            if (snap.MonsterStatusBodies is { } sb && i < sb.Count && sb[i] is { } status)
                _statusBodies[monsters[i]] = P3BodyAreas.FromStates(status);
        }
        for (var i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (i < snap.BowSpells.Count && snap.BowSpells[i] != P3BowSpell.None)
                _bowSpells[m] = snap.BowSpells[i];
            if (snap.Accuracy.Contains(i)) _accuracy.Add(m);
            if (i < snap.Attack.Count && snap.Attack[i] is { } a) _attack[m] = a;
            if (i < snap.Armour.Count && snap.Armour[i] is { } r) _armour[m] = r;
            if (snap.Parried.Contains(i)) _parried.Add(m);
            if (snap.ParryBlock.Contains(i)) _parryBlock.Add(m);
        }
        WakeChance = snap.WakeChance;
        NextRound = snap.NextRound;
        FreeFlee = snap.FreeFlee;
        AwakenAtRoundEnd = snap.AwakenAtRoundEnd;
        StartStrength = snap.StartStrength;
    }

    /// <summary>A character's III status (record <c>+0x2c</c> uses the same numbers).</summary>
    public static P3Status StatusOf(Character member) => (P3Status)(int)member.Status;
}

/// <summary>The <c>mode</c> of III's <c>combat_round</c> (root <c>0xd8c7</c>).</summary>
public enum P3RoundMode
{
    /// <summary>Party and monsters act.</summary>
    Normal = 0,

    /// <summary>Only the monsters act: a monster ambush's free round, or a failed Flee's "free shots".</summary>
    MonstersOnly = 1,

    /// <summary>Only the party acts: the first round after the party surprised the monsters.</summary>
    PartyOnly = 2,
}
