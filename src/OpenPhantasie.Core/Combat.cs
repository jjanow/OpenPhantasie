namespace OpenPhantasie.Core;

/// <summary>Outcome of a single combat round.</summary>
public sealed class CombatResult
{
    public bool PartyWon { get; init; }
    public bool PartyWiped { get; init; }
    public bool EncounterOver { get; init; }
    public long RewardXp { get; init; }
    public long RewardGold { get; init; }
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The round's hits in the order they landed (<see cref="CombatHit"/>), for a front end's effects (III Amiga's spell
    /// flash, its sounds). Filled by <see cref="GameSession.ResolveCombatRound"/>; empty elsewhere.
    /// </summary>
    public IReadOnlyList<CombatHit> Hits { get; internal set; } = Array.Empty<CombatHit>();

    /// <summary>The round's line tags in order (III; I's engine, so I and II, tags its swings, wakings and casts): who each line belongs to, presentation only.</summary>
    public IReadOnlyList<CombatLineTag> LineTags { get; internal set; } = Array.Empty<CombatLineTag>();
}

/// <summary>
/// One hit of a combat round on a party member (<see cref="Member"/>) or a monster (<see cref="Monster"/>), whatever
/// its damage (a spell that only puts to sleep or weakens counts: the originals call their damage routine with 0 for it).
/// <see cref="Touch"/>: a monster's stunning or draining melee (I's riders 34 / 35). Presentation only: recording one
/// draws no random number and changes no rule. <see cref="Line"/>: the index in <see cref="CombatResult.Log"/> of the
/// first line printed after the hit landed (its "takes n damage!" line; the log's length when nothing follows), so a
/// front end can play the hit's flash just before it (III Amiga flashes inside the damage routine, FORMAT.md §8.6);
/// −1 = not known. <see cref="SpellDamage"/>: a party damage spell's amount for I Amiga's sparkle count (damage / 32 + 1,
/// h53 <c>+0xab8</c>, s133): FIREFLASH its damage, MINDBLAST / FLAMEBOLT the roll before a monster's armour and the floor;
/// −1 = none. II ST (s156) uses it for its sparkle the same way (<c>0x18b16</c>, FORMAT.md 7.3) and, on a
/// <see cref="DamageSource.PartyRock"/> hit, for the thrown rock's damage before the 1-point floor (the rock sprite).
/// </summary>
public sealed record CombatHit(DamageSource Source, Character? Member, Monster? Monster, bool Touch = false, int Line = -1,
    int SpellDamage = -1);

/// <summary>A melee swing's outcome on a round line (<see cref="CombatLineTag"/>).</summary>
public enum CombatSwing
{
    None,
    Hit,
    Miss,
}

/// <summary>
/// Who a round's log line belongs to (III s117, I s132; presentation only): line <see cref="Line"/> of <see cref="CombatResult.Log"/>
/// and every untagged line after it belong to the actor whose turn it opens (<see cref="Member"/> or <see cref="Monster"/>).
/// A <see cref="CombatSwing"/> marks a melee swing's "hits!" / "misses" line: the Amiga shows the attacker's attack frame
/// before it and plays sound 15 (hit) or 18 (miss) with it (member <c>0x1662c</c> / <c>0x16682</c>, monster
/// <c>0x17eda</c> / <c>0x17b28</c>). <see cref="Sound"/>: a sound the Amiga starts as the line prints (0 = none): III's
/// cast lines play sound 21 (<see cref="P3Fight.CastSound"/>). I (s132) tags its swings, waking lines and the party's /
/// monsters' cast lines (no sound: I Amiga's pack names each line's sound).
/// </summary>
public sealed record CombatLineTag(int Line, Character? Member, Monster? Monster, CombatSwing Swing = CombatSwing.None,
    int Sound = 0);

/// <summary>Per-character runtime fields computed by combat prep (<c>FUN_0000_3263</c>).</summary>
public sealed class CombatantPrep
{
    public int ArmorEff { get; set; }
    public int WeaponDamageEff { get; set; }
    public int WeaponHitEff { get; set; }
    public int DefenseRating { get; set; }
    public int ShieldSoak { get; set; }
    public int PackedAction { get; set; } = CombatAction.Cleared;
}

/// <summary>One rank of monsters (RANK 1, RANK 2, …).</summary>
public sealed class MonsterRank
{
    public int RankIndex { get; init; }
    public List<Monster> Members { get; } = new();

    public bool IsWiped => Members.TrueForAll(m => !m.IsAlive);
    public IEnumerable<Monster> Living => Members.Where(m => m.IsAlive);
}

/// <summary>
/// Active encounter: party + ranked monster groups + packed actions.
/// Call <see cref="Prepare"/> once at fight start (and after gear buffs).
/// </summary>
public sealed class CombatEncounter
{
    public Party Party { get; }
    public List<MonsterRank> Ranks { get; } = new();
    public CombatantPrep[] Prep { get; private set; } = Array.Empty<CombatantPrep>();

    /// <summary>
    /// XP earned so far, <c>[0x7512]</c>: 0 at build, <c>+0x1a</c> of every monster killed (<see cref="RecordKill"/>,
    /// OV2 <c>0x1656</c>). Paid on every way out of combat (deposited to <see cref="LootSack"/>). The dissolve path
    /// (<c>0x8c1</c>, <see cref="CombatSpells.Dissolve"/>) adds the front group's XP × (living − 1). Victory (<c>0x3fbe</c>)
    /// prints it per non-DEAD member (<c>[0x6490]</c>) — not in Core.
    /// </summary>
    public long RewardXp { get; set; }

    /// <summary>GP paid on victory, <c>[0x750e]</c>: <c>+0x2f</c> of every monster, summed at build (<c>0x3a97</c>).</summary>
    public long RewardGold { get; set; }

    /// <summary>Group 0's treasure item ids (<c>+0x35</c>/<c>+0x36</c> → <c>[0x74a6]</c>/<c>[0x74a8]</c>), dropped at the end by
    /// <see cref="TreasureDrop"/>.</summary>
    public IReadOnlyList<int> TreasureItemIds { get; set; } = Array.Empty<int>();

    /// <summary>0-based id of the encounter's first monster (DOS <c>[0x64e6]</c> − 1); −1 if unknown (older saves).</summary>
    public int FirstMonsterId { get; set; } = -1;

    /// <summary>Σ reaction modifier <c>+0x29</c> per group, <c>[0x750c]</c>; GREETINGS needs <c>rand &lt; Σ + 10</c> (<see cref="PartyOptions"/>).</summary>
    public int ReactionSum { get; set; }

    /// <summary>THREATEN factor <c>[0xc432]</c>: 15 at build, 45 once a monster run has failed (<see cref="PartyOptions"/>).</summary>
    public int ThreatenFactor { get; set; } = PartyOptions.ThreatenFactorStart;

    /// <summary><c>[0x74b6]</c>: a monster has taken damage this fight (<c>0x1597</c>); GREETINGS can't work after that.</summary>
    public bool MonsterDamaged { get; set; }

    /// <summary>Failed monster run (<c>0x3e13</c>, state 2): no monster acts in the next resolved round. Cleared by it.</summary>
    public bool MonstersLoseRound { get; set; }

    /// <summary><c>[0x74ba]</c>/<c>[0x74b4]</c> zeroed (BEG, GREET, FLEE, TELEPORTATION, CHARM): no treasure (<see cref="TreasureDrop"/>).</summary>
    public bool TreasureForfeited { get; set; }

    /// <summary><c>[0x74b2]</c>: monsters surrendered in a dungeon, so their treasure is handed over (<see cref="TreasureDrop"/>).</summary>
    public bool SurrenderTreasure { get; set; }

    // Party buffs / SLOWED / armor-reduced live only in Prep (DOS @246/@248/@250): QUICKNESS, STRENGTH and PROTECTION
    // rebuild their field from base, specials 17–24 scale the current value (combat-resolution.md §Status effects).
    // The old write-only PartySlowed / PartyWeakened / *Bonus flags had no DOS counterpart and were removed (s33).

    /// <summary><c>[0x74b0]</c>: a spell ended the fight; it stops at the next round top (<see cref="CombatSpells.EndBySpell"/>).</summary>
    public SpellCombatEnd SpellEnd { get; set; }

    /// <summary>
    /// <c>[0x6490]</c> holds the living <i>count</i> rather than the last living <i>index</i> (combat-resolution.md
    /// §`core.spell.summon_elemental`): false after the orders phase, true once a monster has taken a turn this round (melee or special)
    /// or RESURRECTION worked. Only DOS's `core.spell.summon_elemental` test reads it (D-033 <c>dos</c>); not saved, reset every round top.
    /// </summary>
    public bool LivingWordIsCount { get; set; }

    /// <summary><c>[0x74aa]</c> set by dissolve: the end of combat counts every monster as killed ("MONSTERS KILLED").</summary>
    public bool Dissolved { get; set; }

    /// <summary><c>[0x74b8]</c>: DISPELL UNDEAD has been tried on an undead front group this fight (one try per fight).</summary>
    public bool DispellUndeadTried { get; set; }

    /// <summary>
    /// Live surprise globals <c>[0x649e]</c>/<c>[0x64a0]</c> (§Surprise). A bare encounter starts at (0, 0) with no
    /// opening effect. <see cref="EncounterBuilder"/> sets the flags the encounter source chose.
    /// </summary>
    public SurpriseFlags Surprise { get; set; }

    /// <summary>
    /// OV2 <c>0x33ab</c> returned 5: the first round runs at once, with no morale check, no PARTY OPTIONS and every order
    /// cleared. One-shot: the next <see cref="CombatResolver.ResolveRound"/> consumes it.
    /// </summary>
    public bool OpensWithoutOrders { get; set; }

    /// <summary>
    /// Monster hold <c>[0x64a0]</c> ≠ 0: ready monsters don't act. Setting it writes 1, like DISPELL UNDEAD's run (<c>0x993</c>)
    /// and FEAR (D-021). Clearing it writes 0, like a failed Flee (<c>0x3d9f</c>).
    /// </summary>
    public bool MonstersFrozen
    {
        get => Surprise.MonsterHold != 0;
        set => Surprise = Surprise with { MonsterHold = value ? 1 : 0 };
    }

    /// <summary>
    /// Encounter level <c>L = [0x7508]</c> (`core.magic.monsters_at_level_x`), used by monster readiness: Σ group level
    /// <c>+0x18</c> at build (<see cref="EncounterBuilder"/>, OV2 <c>0x3ac4</c>).
    /// </summary>
    public int MonsterLevel { get; set; }

    /// <summary>
    /// A campaign's own per-fight state, built with the encounter (III: <see cref="P3Fight"/>, the hit-location table, the
    /// monsters' body areas and the bow spells in effect). Null for I and II. Not saved.
    /// </summary>
    public object? CampaignState { get; set; }

    /// <summary>How a summoned elemental's combat fields are set (D-032: normal prep), see <see cref="ElementalPrepRule"/>.</summary>
    public ElementalPrepRule ElementalPrep { get; set; } = ElementalPrepRule.Prepared;

    /// <summary>
    /// The save's rules options this fight reads (T5.0, PLAN P3.2: D-015, D-016 …). <see cref="GameSession"/> sets its own
    /// <see cref="GameSession.Options"/> (new fights and a restored mid-fight save); a bare encounter plays Phantasie I's
    /// Original (manual) rules.
    /// </summary>
    public GameOptions Rules
    {
        get => _rules;
        set => _rules = value ?? throw new ArgumentNullException(nameof(value));
    }

    private GameOptions _rules = GameOptions.ManualPhantasieI;

    /// <summary>
    /// The feature modules this fight runs with (Phase 09 T1.6): they own the combat orders and can hook damage.
    /// <see cref="GameSession"/> sets its own set; a bare encounter falls back to <see cref="FeatureModuleSet.PhantasieI"/>.
    /// </summary>
    public FeatureModuleSet Modules
    {
        get => _modules;
        set => _modules = value ?? throw new ArgumentNullException(nameof(value));
    }

    private FeatureModuleSet _modules = FeatureModuleSet.PhantasieI;

    public CombatEncounter(Party party)
    {
        Party = party ?? throw new ArgumentNullException(nameof(party));
    }

    public MonsterRank AddRank(params Monster[] monsters)
    {
        var rank = new MonsterRank { RankIndex = Ranks.Count };
        rank.Members.AddRange(monsters);
        Ranks.Add(rank);
        return rank;
    }

    /// <summary>A monster died: add its XP (OV2 <c>0x1597</c> → <c>0x1656</c>).</summary>
    public void RecordKill(Monster monster) => RewardXp += monster.ExperienceValue;

    /// <summary>
    /// Round-end re-rank (OV2 <c>0x19d7</c>): groups sorted by living count, largest first, ties to the later group
    /// (<see cref="MonsterRank.RankIndex"/> = build order). DOS re-compacts on every monster death (<c>0x1694</c>),
    /// so the counts include this round's kills.
    /// </summary>
    public void ReRank()
    {
        var order = Ranks
            .OrderByDescending(r => r.Living.Count())
            .ThenByDescending(r => r.RankIndex)
            .ToList();
        Ranks.Clear();
        Ranks.AddRange(order);
    }

    /// <summary>Flat living monster list across all ranks (for simple tests).</summary>
    public IEnumerable<Monster> AllMonsters => Ranks.SelectMany(r => r.Members);

    private readonly List<CombatHit> _roundHits = new();

    /// <summary>
    /// Note a hit of the running round (<see cref="CombatResult.Hits"/>), landing before the next line of
    /// <paramref name="log"/> (<see cref="CombatHit.Line"/>); presentation only.
    /// </summary>
    internal void NoteHit(CombatHit hit, List<string> log) => _roundHits.Add(hit with { Line = log.Count });

    private readonly List<CombatLineTag> _roundLines = new();

    /// <summary>Tag the next line of <paramref name="log"/> (<see cref="CombatLineTag"/>); presentation only.</summary>
    internal void TagLine(List<string> log, Character? member, Monster? monster, CombatSwing swing = CombatSwing.None,
        int sound = 0) =>
        _roundLines.Add(new CombatLineTag(log.Count, member, monster, swing, sound));

    /// <summary>
    /// s158: tag the line about to be added to <paramref name="log"/> with <paramref name="sound"/>, keeping the last tag's
    /// actor (the line's side stays that of the line before it).
    /// </summary>
    internal void TagSound(List<string> log, int sound)
    {
        var last = _roundLines.Count > 0 ? _roundLines[^1] : null;
        _roundLines.Add(new CombatLineTag(log.Count, last?.Member, last?.Monster, CombatSwing.None, sound));
    }

    /// <summary>
    /// III only: tag a monster's " dies!" line about to be added to <paramref name="log"/> with sound 39
    /// (<see cref="P3Sounds.Dies"/>; Amiga <c>0x18610</c> inside <c>damage_monster</c> <c>0x1833e</c>, FORMAT.md 7.2; a
    /// member's death is <c>damage_character</c>, no 39). Call just before the line is added.
    /// </summary>
    internal void TagDies(List<string> log)
    {
        if (!Modules.IsEnabled("p3.combat-actions"))
            return;
        var last = _roundLines.Count > 0 ? _roundLines[^1] : null; // the line keeps the last tag's actor (the killer)
        _roundLines.Add(new CombatLineTag(log.Count, last?.Member, last?.Monster, CombatSwing.None, P3Sounds.Dies));
    }

    /// <summary>The line tags noted since the last call, in order; the list starts afresh.</summary>
    public IReadOnlyList<CombatLineTag> TakeRoundLines()
    {
        var tags = _roundLines.ToArray();
        _roundLines.Clear();
        return tags;
    }

    /// <summary>The last noted hit, on <paramref name="member"/>, was a stunning / draining touch.</summary>
    internal void MarkTouch(Character member)
    {
        if (_roundHits.Count > 0 && ReferenceEquals(_roundHits[^1].Member, member))
            _roundHits[^1] = _roundHits[^1] with { Touch = true };
    }

    /// <summary>The hits noted since the last call, in order; the list starts afresh.</summary>
    public IReadOnlyList<CombatHit> TakeRoundHits()
    {
        var hits = _roundHits.ToArray();
        _roundHits.Clear();
        return hits;
    }

    public bool MonstersWiped => Ranks.Count == 0 || Ranks.TrueForAll(r => r.IsWiped);

    /// <summary>No non-DEAD member left (OV2 <c>0x43ae</c> counts SLEEP members as alive).</summary>
    public bool PartyWiped => Party.Members.TrueForAll(m => m.Status == CharacterStatus.Dead);

    /// <summary>
    /// Combat prep (OV2 <c>0x3263</c>): armor_eff = @244, weapon_dmg = @28 + @240, weapon_hit = attack
    /// skill @30, defense rating → @12 (written to the record, like DOS). Every non-DEAD member is set
    /// to OK, so sleep left over from an earlier fight ends here (OV2 <c>0x32fb</c>); pass
    /// <paramref name="fightStart"/> = false to recompute without that (save restore, lazy re-prep).
    /// </summary>
    public void Prepare(bool fightStart = true)
    {
        var members = Party.Members;
        Prep = new CombatantPrep[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            var ch = members[i];
            if (ReferenceEquals(ch, Party.Elemental))
            {
                Prep[i] = CombatSpells.ElementalPrep(ch, ElementalPrep); // only after a summon (save restore)
                continue;
            }
            if (fightStart && ch.Status == CharacterStatus.Sleep) // DOS: every non-DEAD → OK; I / II have no other state.
                ch.Status = CharacterStatus.Ok;                       // III's Out / Down come from the body and stay
            Prep[i] = PrepFor(ch);
        }
    }

    /// <summary>One member's prep (OV2 <c>0x327c..0x32f7</c>); writes the defense rating into <c>@12</c> like DOS.</summary>
    internal static CombatantPrep PrepFor(Character ch)
    {
        var defense = CombatMath.ComputeDefenseRating(
            ch.Stats.Dex, ch.ArmorSoak, ch.Skills.Parry, ch.ShieldSoak);
        ch.Word12 = defense;
        return new CombatantPrep
        {
            ArmorEff = ch.ArmorSoak,
            WeaponDamageEff = ch.WeaponDamageA + ch.WeaponDamageB,
            WeaponHitEff = ch.Skills.Attack,
            ShieldSoak = ch.ShieldSoak,
            DefenseRating = defense,
            PackedAction = CombatAction.Cleared,
        };
    }

    /// <summary>
    /// `core.spell.summon_elemental` success (OV2 <c>0x8db</c>): the elemental becomes member 7 with no action this round
    /// (its action word is 4 from the build, <c>0x339e</c>). Prep of the others is kept.
    /// </summary>
    public void AddSummonedMember(Character elemental)
    {
        ArgumentNullException.ThrowIfNull(elemental);
        EnsurePrep();
        Party.Members.Add(elemental);
        Prep = [.. Prep, CombatSpells.ElementalPrep(elemental, ElementalPrep)];
    }

    /// <summary>
    /// End of combat (OV2 <c>0x3fbb</c> → <c>0x3eff</c>, after the survivor count): every non-DEAD member is set to OK,
    /// so nobody leaves a fight asleep. The AWAKEN spell (<c>0x9bf</c>) runs the same routine mid-combat.
    /// Returns how many members were asleep.
    /// </summary>
    public static int WakeParty(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        var woken = 0;
        foreach (var m in party.Members)
        {
            if (m.Status != CharacterStatus.Sleep) // I / II: every non-DEAD member is OK or asleep; III's Out / Down stay
                continue;
            woken++;
            m.Status = CharacterStatus.Ok;
        }
        return woken;
    }

    /// <summary>DOS keeps the XP pool <c>[0x63f4]</c> in tens, capped at <c>0xffff</c> (OV2 <c>0x40b2</c>) → 655,350 XP;
    /// Core's pool holds whole XP (D-053) under the same cap.</summary>
    public const long XpPoolCap = 0xffff * 10L;

    /// <summary>
    /// End-of-combat survivor count <c>[0x6490]</c> (OV2 <c>0x3fa9</c> = <c>0x43ae() + 1</c>): every non-DEAD
    /// adventurer, sleepers included (<c>@24 ≠ 0</c>); a summoned elemental has already left (<c>0x3f2e</c>).
    /// </summary>
    public static int Survivors(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return party.Adventurers.Count(m => m.Status != CharacterStatus.Dead);
    }

    public void SetAction(int memberIndex, int packedAction)
    {
        EnsurePrep();
        if ((uint)memberIndex >= (uint)Prep.Length)
            throw new ArgumentOutOfRangeException(nameof(memberIndex));
        Prep[memberIndex].PackedAction = packedAction;
    }

    public void EnsurePrep()
    {
        if (Prep.Length != Party.Members.Count)
            Prepare(fightStart: false);
    }
}

/// <summary>Combat round resolver.</summary>
public interface ICombatResolver
{
    CombatResult ResolveRound(CombatEncounter encounter, IRandomSource rng);
}

/// <summary>
/// Implements turn resolution from <c>combat-resolution.md</c> + M7 spells. Turn order is the DOS round scheduler
/// (OV2 <c>0x00e4</c>, §Turn order): three passes p = 1, 3, 5; in each, every party member that rolls ready acts
/// (party order), then every ready monster (rank order). Nobody acts twice in a round.
/// </summary>
public sealed class CombatResolver : ICombatResolver
{
    public CombatResult ResolveRound(CombatEncounter encounter, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(rng);
        encounter.EnsurePrep();

        var log = new List<string>();
        var party = encounter.Party;

        // Round top (0x3b84): a spell cast last round ended the fight; no morale check, no orders.
        if (encounter.SpellEnd != SpellCombatEnd.None)
            return CombatSpells.EndBySpell(encounter);

        // Round-start compaction (0x3b7e, [0x74ae] = 1): every monster state 2 (done) → 3 (ready); state 1 (asleep) stays.
        // A failed monster run (0x3e13) then puts every monster in state 2 for this round.
        foreach (var mon in encounter.AllMonsters)
            mon.DoneThisRound = encounter.MonstersLoseRound;
        encounter.MonstersLoseRound = false;
        encounter.LivingWordIsCount = false; // the orders phase (0x6a2a) stores the last living index

        // Surprise (0xee..0x11f): a fight that skipped PARTY OPTIONS (0x33ab → 5) has no orders. With the first-round flag set
        // and no hold, the party loses the round. The flag is cleared at every round top. Its saved copy releases the hold
        // after pass 1 (0x10f3).
        if (encounter.OpensWithoutOrders)
        {
            ClearOrders(encounter.Prep);
            encounter.OpensWithoutOrders = false;
        }
        var firstRound = encounter.Surprise.FirstRound;
        if (firstRound && encounter.Surprise.MonsterHold == 0)
            ClearOrders(encounter.Prep);
        encounter.Surprise = encounter.Surprise with { FirstRound = false };

        foreach (var pass in CombatMath.RoundPasses)
        {
            // --- Party block ---
            for (var i = 0; i < party.Members.Count; i++)
            {
                if (encounter.MonstersWiped)
                    break;

                var ch = party.Members[i];
                if (ch.Status == CharacterStatus.Dead)
                    continue;
                if (ch.Status == CharacterStatus.Sleep)
                {
                    if (Roll0To99(rng) <= 16 && ch.HpCur >= 1)
                    {
                        ch.Status = CharacterStatus.Ok;
                        encounter.TagLine(log, ch, null);
                        log.Add(GameText.F("core.combat.x_awakens", ch.Name));
                    }
                    continue;
                }

                // PARRY (3) and no action (4) never roll, so PARRY stays armed for the whole round.
                // Read Prep live: `core.spell.summon_elemental` can append a member mid-round.
                var action = encounter.Prep[i].PackedAction;
                if (action <= CombatAction.Cleared || ch.HpCur <= 0)
                    continue;
                if (!CombatMath.PartyActsInPass(Roll0To99(rng), ch.Level, pass))
                    continue;

                PartyMemberActs(encounter, i, action, rng, log);
                encounter.Prep[i].PackedAction = CombatAction.Cleared;

                // TELEPORTATION / CHARM end the fight at once (D-022, D-023); kill XP from earlier hits is kept.
                if (encounter.SpellEnd.EndsAtOnce())
                    return CombatSpells.EndBySpell(encounter, log);
            }

            // Other spells that set [0x74b0] don't stop the round: everyone after the caster still acts (s31).
            if (encounter.MonstersWiped)
                return Victory(encounter, log);

            // --- Monster block --- (groups are compacted on every death, 0x1694: k/n count the living)
            for (var g = 0; g < encounter.Ranks.Count; g++)
            {
                var group = encounter.Ranks[g].Living.ToList();
                for (var k = 0; k < group.Count; k++)
                {
                    if (encounter.PartyWiped)
                        break;

                    var mon = group[k];
                    if (!mon.IsAlive || mon.DoneThisRound)
                        continue;
                    if ((mon.StatusFlags & MonsterStatusFlags.Asleep) != 0)
                    {
                        if (Roll0To99(rng) < 15)
                        {
                            mon.StatusFlags &= ~MonsterStatusFlags.Asleep;
                            mon.DoneThisRound = true;
                            encounter.TagLine(log, null, mon);
                            log.Add(GameText.F("core.combat.x_awakens", mon.Name));
                        }
                        continue;
                    }
                    // [0x64a0] != 0 (DISPELL UNDEAD's run): ready monsters don't roll (0xce7).
                    if (encounter.MonstersFrozen)
                        continue;
                    if (!CombatMath.MonsterActsInPass(Roll0To99(rng), pass, encounter.MonsterLevel))
                        continue;

                    // Special-ability gate (0xd30): rand + 1 ≤ m[+0x1e]·(2g + 2)/(ranks + 2) → special (0x16a9).
                    // Specials end the turn even in the third rank; ids 34/35 fall through to melee with a rider.
                    var rider = MonsterMeleeRider.None;
                    if (MonsterSpecials.GatePasses(Roll0To99(rng), mon.SpecialChance, g, encounter.Ranks.Count)
                        && mon.Specials.Count == 3)
                    {
                        mon.DoneThisRound = true;
                        encounter.LivingWordIsCount = true; // 0x1065
                        var fallThrough = MonsterSpecials.Use(encounter, encounter.Ranks[g], mon, rng, log);
                        if (fallThrough is null || g == 2)
                            continue;
                        rider = fallThrough.Value;
                    }

                    // The third rank never melees (0xd68); it stays ready and re-rolls next pass.
                    if (g == 2)
                        continue;

                    mon.DoneThisRound = true;
                    encounter.LivingWordIsCount = true; // melee start 0xd93
                    if (!encounter.Modules.ResolveMonsterMelee(encounter, encounter.Ranks[g], mon, rider, rng, log))
                        MonsterMelee(encounter, mon, k, group.Count, rider, rng, log);
                }
            }

            if (encounter.PartyWiped)
            {
                return new CombatResult
                {
                    PartyWon = false,
                    PartyWiped = true,
                    EncounterOver = true,
                    Log = log,
                };
            }

            // 0x10f3: in a first round the hold (unless 2) ends after pass 1. Normal fights: monsters act from pass 3.
            if (firstRound && encounter.Surprise.MonsterHold != 2)
            {
                encounter.MonstersFrozen = false;
                firstRound = false;
            }
        }

        // Round end (0x1115): hold 2 → 1, else 0. "Monsters surprised" are held in rounds 1 and 2.
        encounter.Surprise = encounter.Surprise with { MonsterHold = encounter.Surprise.MonsterHold == 2 ? 1 : 0 };
        encounter.ReRank();
        return new CombatResult
        {
            EncounterOver = false,
            Log = log,
        };
    }

    /// <summary>Every order := 4 (no action), as OV2 <c>0x3389</c> / <c>0x114</c> write <c>0x74fa[i]</c>.</summary>
    private static void ClearOrders(CombatantPrep[] prep)
    {
        foreach (var p in prep)
            p.PackedAction = CombatAction.Cleared;
    }

    /// <summary>One party member's action, resolved by the module that owns it (I: melee or a cast; else nothing).</summary>
    private static void PartyMemberActs(CombatEncounter encounter, int i, int action, IRandomSource rng, List<string> log) =>
        encounter.Modules.ResolvePartyAction(encounter, i, action, rng, log);

    /// <summary>Party melee (packed <c>swings·10 + rank</c>, <c>combat-resolution.md</c>): Phantasie I's melee resolution.</summary>
    public static void ResolvePartyMelee(
        CombatEncounter encounter, int i, int action, IRandomSource rng, List<string> log)
    {
        var ch = encounter.Party.Members[i];
        var prep = encounter.Prep;

        var (swings, rankIndex) = CombatAction.Decode(action);
        if (rankIndex >= encounter.Ranks.Count)
        {
            return; // [D✓] s65: a melee order at a rank that no longer exists does nothing, with no text
        }

        var rank = encounter.Ranks[rankIndex];
        for (var s = 0; s < swings; s++)
        {
            if (rank.IsWiped)
                break;

            var target = PickLiving(rank, rng);
            if (target is null)
                break;

            var chance = CombatMath.PartyMeleeHitChance(
                prep[i].WeaponHitEff, swings, target.Defense);
            var hitRoll = Roll0To99(rng);
            if (!CombatMath.HitsStrict(hitRoll, chance))
            {
                encounter.TagLine(log, ch, null, CombatSwing.Miss); // presentation only (I Amiga: the attack frame)
                log.Add(GameText.F("core.combat.x_misses", ch.Name)); // [D✓] s65: DOS names no target
                continue;
            }

            encounter.TagLine(log, ch, null, CombatSwing.Hit);
            log.Add(GameText.F("core.combat.x_hits", ch.Name));
            var dmgRoll = Roll0To99(rng);
            var raw = CombatMath.PartyToMonsterDamage(
                dmgRoll, prep[i].WeaponDamageEff, target.Armor, action);
            var dmg = CombatMath.ApplyDamageFloor(raw);
            ApplyMonsterDamage(encounter, target, dmg, DamageSource.PartyMelee, rng, log);
        }
    }

    /// <summary>
    /// Monster melee (OV2 <c>0xd73</c>): <c>attack/82</c> swings, each aimed by slot (<c>0xdd4</c>). A special's
    /// <paramref name="rider"/> (ids 34/35) follows every hit unless that hit wiped the party (<c>0xf3a</c>).
    /// </summary>
    private static void MonsterMelee(
        CombatEncounter encounter, Monster mon, int slot, int groupCount, MonsterMeleeRider rider,
        IRandomSource rng, List<string> log)
    {
        var party = encounter.Party;
        var prep = encounter.Prep;
        var swings = CombatMath.MonsterSwingCount(mon.AttackPower);
        for (var s = 0; s < swings; s++)
        {
            var targetIndex = CombatMath.MonsterTargetSlot(Roll0To99(rng), slot, groupCount, party.Members);
            if (targetIndex < 0)
                break;

            var target = party.Members[targetIndex];
            var metric = CombatMath.MonsterMeleeMetric(
                mon.AttackPower, swings, prep[targetIndex].DefenseRating);
            var hitRoll = Roll0To99(rng);
            if (!CombatMath.MonsterMeleeHits(hitRoll, metric))
            {
                encounter.TagLine(log, null, mon, CombatSwing.Miss);
                log.Add(GameText.F("core.combat.x_misses", mon.Name)); // [D✓] s65: "VIPER HITS / priesto TAKES 8 DAMAGE"
                continue;
            }

            encounter.TagLine(log, null, mon, CombatSwing.Hit);
            log.Add(GameText.F("core.combat.x_hits", mon.Name));
            var dmgRoll = Roll0To99(rng);
            var isParrying = CombatAction.IsParry(prep[targetIndex].PackedAction);
            var raw = CombatMath.MonsterToPartyDamage(
                dmgRoll,
                mon.DamageField,
                prep[targetIndex].ArmorEff,
                isParrying,
                prep[targetIndex].ShieldSoak,
                out var parryConsumed);
            if (parryConsumed)
                prep[targetIndex].PackedAction = CombatAction.Cleared;

            var dmg = CombatMath.ApplyDamageFloor(raw);
            ApplyPartyDamage(encounter, target, dmg, DamageSource.MonsterMelee, rng, log);
            if (encounter.PartyWiped)
                return;
            MonsterSpecials.ApplyRider(rider, target, prep[targetIndex], log, stunTheDead: encounter.Rules.DosStunningBlow);
        }
    }

    /// <summary>All monsters dead (<c>0xc8b</c> → <c>0x3f29</c>); a TELEPORTATION cast this round still zeroes the gold.</summary>
    internal static CombatResult Victory(CombatEncounter encounter, List<string> log)
    {
        var xp = encounter.RewardXp;
        var gold = CombatSpells.GoldAtEnd(encounter);
        if (encounter.SpellEnd.PaysNoGold())
            encounter.TreasureForfeited = true;
        log.Add(GameText.S("core.combat.monsters_killed"));
        if (!encounter.Rules.SurvivorShares) // D-053 dos: DOS prints only the shares
            log.Add(GameText.F("core.combat.x_xp_and_x_gp", xp, gold));
        return new CombatResult
        {
            PartyWon = true,
            EncounterOver = true,
            RewardXp = xp,
            RewardGold = gold,
            Log = log,
        };
    }

    /// <summary>
    /// A combat hit on a monster, through the encounter's damage modules (<see cref="IDamageModule"/>; Phantasie I has
    /// none, so the base step below runs alone and unchanged).
    /// </summary>
    internal static void ApplyMonsterDamage(
        CombatEncounter encounter, Monster target, int dmg, DamageSource source, IRandomSource rng, List<string> log,
        int? spellIndex = null, int spellDamage = -1)
    {
        encounter.NoteHit(new CombatHit(source, null, target, SpellDamage: spellDamage), log);
        var modules = encounter.Modules;
        if (!modules.HasDamageModules)
        {
            ApplyMonsterDamage(encounter, target, dmg, log);
            return;
        }
        var hit = new DamageHit
        {
            Encounter = encounter, Party = encounter.Party, Source = source, Rng = rng, Monster = target, Amount = dmg,
            SpellIndex = spellIndex,
        };
        dmg = modules.BeforeDamage(hit, log);
        ApplyMonsterDamage(encounter, target, dmg, log);
        modules.AfterDamage(hit, !target.IsAlive, log);
    }

    /// <summary>Base HP step for a monster hit (OV2 <c>0x1597</c>).</summary>
    private static void ApplyMonsterDamage(CombatEncounter encounter, Monster target, int dmg, List<string> log)
    {
        var hp = target.HitPoints ?? 0;
        hp -= dmg;
        target.HitPoints = hp;
        encounter.MonsterDamaged = true; // [0x74b6] := 1 at 0x15a5
        log.Add(GameText.F("core.combat.x_takes_x_damage", target.Name, dmg));
        if (hp <= 0)
        {
            target.HitPoints = 0;
            encounter.RecordKill(target);
            encounter.TagDies(log);
            log.Add(GameText.F("core.combat.x_dies", target.Name));
        }
    }

    /// <summary>
    /// A combat hit on a party member, through the encounter's damage modules (<see cref="IDamageModule"/>; Phantasie I
    /// has none, so it is exactly <see cref="ApplyPartyDamage(Party, Character, int, List{string})"/>).
    /// </summary>
    internal static void ApplyPartyDamage(
        CombatEncounter encounter, Character target, int dmg, DamageSource source, IRandomSource rng, List<string> log)
    {
        encounter.NoteHit(new CombatHit(source, target, null), log);
        var modules = encounter.Modules;
        if (!modules.HasDamageModules)
        {
            ApplyPartyDamage(encounter.Party, target, dmg, log);
            return;
        }
        var hit = new DamageHit
        {
            Encounter = encounter, Party = encounter.Party, Source = source, Rng = rng, Member = target, Amount = dmg,
        };
        dmg = modules.BeforeDamage(hit, log);
        ApplyPartyDamage(encounter.Party, target, dmg, log);
        modules.AfterDamage(hit, target.Status == CharacterStatus.Dead, log);
    }

    /// <summary>
    /// A hit on a party member outside combat (traps, dungeon scripts, swimming), through the party's damage modules
    /// (<see cref="Party.Modules"/>; Phantasie I has none, so it is exactly the base step). The base step's own lines go
    /// nowhere: these callers print their own DOS report. Module lines go to <paramref name="log"/>. Returns the HP
    /// actually taken off (the amount after the modules).
    /// </summary>
    internal static int ApplyPartyDamage(
        Party party, Character target, int dmg, DamageSource source, IRandomSource rng, List<string> log,
        int? trapLevel = null)
    {
        var modules = party.Modules;
        if (!modules.HasDamageModules)
        {
            ApplyPartyDamage(party, target, dmg, new List<string>());
            return dmg;
        }
        var hit = new DamageHit
        {
            Party = party, Source = source, Rng = rng, Member = target, Amount = dmg, TrapLevel = trapLevel,
        };
        dmg = modules.BeforeDamage(hit, log);
        ApplyPartyDamage(party, target, dmg, new List<string>());
        modules.AfterDamage(hit, target.Status == CharacterStatus.Dead, log);
        return dmg;
    }

    /// <summary>
    /// Base HP step for a hit on a party member (OV2 <c>0x124a</c>): HP down and, at 0, the campaign's death step
    /// (<see cref="Party.Death"/>; I: DEAD and the D-009 CON loss). No module hooks run here: hits go through the
    /// hooked overloads above, in combat and out of it (traps, swimming, dungeon scripts).
    /// </summary>
    internal static void ApplyPartyDamage(Party party, Character target, int dmg, List<string> log)
    {
        target.HpCur -= dmg;
        log.Add(GameText.F("core.combat.x_takes_x_damage", target.Name, dmg));
        if (target.HpCur <= 0)
        {
            target.HpCur = 0;
            party.Death.ApplyDeath(target);
            log.Add(GameText.F("core.combat.x_dies", target.Name));
        }
    }

    /// <summary>Phantasie I's CON loss on dying (D-009): <see cref="PhantasieIDeathRule.ConLoss"/>.</summary>
    public static int DeathConLoss(CharacterStats stats) => PhantasieIDeathRule.ConLoss(stats);

    private static Monster? PickLiving(MonsterRank rank, IRandomSource rng)
    {
        var living = rank.Members.Where(m => m.IsAlive).ToList();
        if (living.Count == 0) return null;
        if (living.Count == 1) return living[0];
        return living[rng.Next(0, living.Count)];
    }

    /// <summary>
    /// <c>FUN_0000_655b</c> result 0..99. Uses <see cref="IRandomSource.Next"/>(0,100)
    /// so tests can inject a seeded or scripted source; a DOS LCG wrapper can
    /// implement the same interface later.
    /// </summary>
    private static int Roll0To99(IRandomSource rng) => rng.Next(0, 100);
}

/// <summary>Placeholder kept for older call sites; prefer <see cref="CombatResolver"/>.</summary>
[Obsolete("Use CombatResolver — M6 implemented combat-resolution.md formulas.")]
public sealed class StubCombatResolver : ICombatResolver
{
    private readonly CombatResolver _inner = new();

    public CombatResult ResolveRound(CombatEncounter encounter, IRandomSource rng) =>
        _inner.ResolveRound(encounter, rng);
}
