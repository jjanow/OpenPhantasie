namespace OpenPhantasie.Core;

/// <summary>
/// A game's own Guild rules for its elementals (T1 profile flag; <c>elementals</c> in rules.json).
/// Phantasie I (OV1 <c>0x2786</c> / <c>0x1fe8</c>, manual "will only serve a party of six"): the party must have six
/// members, and no one can be added while it is loaded. Phantasie II (Atari ST <c>PHANT.PRG</c>, static <c>[C]</c> s172,
/// <c>atari-st/phantasie2/logic-specs/towns.md</c> section 10): no member count at Add, characters join beside it, Drop and
/// Purge of a character never touch it, and the Drop list holds it. Phantasie III (ovl2 <c>0x3a0a</c>, manual "does not
/// count as a member"; <c>phantasie3/logic-specs/elementals.md</c> section 2): one character is enough, and a character
/// added later goes in front of it (the elemental stays last).
/// </summary>
/// <param name="MinMembers">Characters the party needs before Guild Add lets an elemental join (I: 6, II: 0, III: 1).</param>
/// <param name="BlocksAdd">A loaded elemental stops Guild ▸ Add (I: yes, II and III: no).</param>
/// <param name="DropKeeps">
/// Guild ▸ Drop of a character keeps a loaded elemental (III, ovl2 <c>0x3c92</c> [C] s97: the records after the dropped
/// one move up, so the elemental stays last; II: the Drop clears only the character's slot). False = I's: every Drop
/// clears party slot 7 (OV1 <c>0x22d1</c>).
/// </param>
/// <param name="KeepsAlone">
/// With <see cref="DropKeeps"/>: the elemental stays even when no character is left (II, ST Drop <c>0xd57c</c> and the
/// compaction <c>0xc14a</c> never touch record 6). False = III's: unloaded when the last character goes (<c>0x3e0b</c>).
/// <c>[?]</c> What the ST does when such a party leaves the Guild or town is not read; the remaster's town Exit refuses a
/// party with no character (<see cref="GameSession.LeaveTown"/>).
/// </param>
/// <param name="DropsElemental">
/// Guild ▸ Drop lists the loaded elemental after the characters and can unload it by itself (II: the ST Drop list is the
/// seven party records, <c>0x5da</c> with 7; III: DOS ovl2 <c>0x3c92</c> clears the flag). False = I's Drop list.
/// </param>
/// <param name="Purge">
/// Guild ▸ Purge of a character in the party, the game's own rule (the every-game <c>elemental-purge</c> = <c>game</c>):
/// I clears the elemental (OV1 <c>0x2489</c>); II keeps it (ST <c>0xd68a</c> clears party records 0–5 only); III's manual
/// purges as Drop (D3-040).
/// </param>
/// <param name="PurgeOriginal">
/// <see cref="Purge"/> under the Original preset when it differs (III: DOS refuses, string 448); null = <see cref="Purge"/>.
/// </param>
/// <param name="SummonMinMembers">
/// Living characters Summon Elemental needs in a fight; null = <see cref="MinMembers"/>. II: 6 (ST <c>0x83e2</c>), though
/// its Guild Add needs none.
/// </param>
public sealed record ElementalRules(
    int MinMembers,
    bool BlocksAdd,
    bool DropKeeps = false,
    bool KeepsAlone = false,
    bool DropsElemental = false,
    ElementalPurge Purge = ElementalPurge.Clears,
    ElementalPurge? PurgeOriginal = null,
    int? SummonMinMembers = null)
{
    /// <summary>Phantasie I's: six members first; nobody joins after it; Drop and Purge unload it.</summary>
    public static ElementalRules PhantasieI { get; } = new(Party.MaxMembers, BlocksAdd: true);

    /// <summary>
    /// Phantasie III's (<c>phantasie3/rules.json</c> <c>elementals</c>, pinned equal by a test): one character first,
    /// characters join in front of it, a Drop keeps it while a character is left, Drop can unload it by itself, Purge is
    /// the manual's as-Drop (D3-040) and DOS's refusal under the Original preset. Used by the cross-game options
    /// (<see cref="RuleOptionRegistry.ElementalLoad"/>, <see cref="RuleOptionRegistry.ElementalDrop"/>) in the other games.
    /// </summary>
    public static ElementalRules PhantasieIII { get; } = new(1, BlocksAdd: false, DropKeeps: true, DropsElemental: true,
        Purge: ElementalPurge.AsDrop, PurgeOriginal: ElementalPurge.Refused);

    /// <summary>Living characters Summon Elemental needs (<see cref="SummonMinMembers"/>, else <see cref="MinMembers"/>).</summary>
    public int SummonMembers => SummonMinMembers ?? MinMembers;
}

/// <summary>A game's own Guild ▸ Purge rule for a party member with an elemental loaded (rules.json <c>purge</c>).</summary>
public enum ElementalPurge
{
    /// <summary><c>clears</c>: purged, the elemental is unloaded (I, OV1 <c>0x2489</c>).</summary>
    Clears,

    /// <summary><c>as-drop</c>: purged, the elemental does what this save's Drop does (III's manual, D3-040).</summary>
    AsDrop,

    /// <summary><c>keeps</c>: purged, the elemental stays, even with no character left (II, ST <c>0xd68a</c>).</summary>
    Keeps,

    /// <summary><c>refused</c>: a party member is not purged; Drop first (DOS III, string 448).</summary>
    Refused,
}

/// <summary>
/// The Guild's elemental rules a save plays by (PLAN P3 QoL, user D3-040, s172): the game's own
/// (<see cref="RulesProfile.Elementals"/>) under the every-game options' <c>game</c> value, else the original rule the
/// option names.
/// </summary>
public static class ElementalGuildRules
{
    /// <summary>
    /// <paramref name="own"/> (the game's rules.json block) with Add (<see cref="RuleOptionRegistry.ElementalLoad"/>) and Drop
    /// (<see cref="RuleOptionRegistry.ElementalDrop"/>) replaced as <paramref name="options"/> say. The Drop list
    /// (<see cref="ElementalRules.DropsElemental"/>), the own Purge rule and SUMMON's count stay the game's own.
    /// </summary>
    public static ElementalRules Elementals(this GameOptions options, ElementalRules own)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(own);
        var load = options.Is(RuleOptionRegistry.ElementalLoad, "phantasie-i") ? ElementalRules.PhantasieI
            : options.Is(RuleOptionRegistry.ElementalLoad, "phantasie-iii") ? ElementalRules.PhantasieIII
            : own;
        var drop = options.Is(RuleOptionRegistry.ElementalDrop, "phantasie-i") ? ElementalRules.PhantasieI
            : options.Is(RuleOptionRegistry.ElementalDrop, "phantasie-iii") ? ElementalRules.PhantasieIII
            : own;
        return own with
        {
            MinMembers = load.MinMembers,
            BlocksAdd = load.BlocksAdd,
            DropKeeps = drop.DropKeeps,
            KeepsAlone = drop.KeepsAlone,
        };
    }

    /// <summary>
    /// What Guild ▸ Purge does to a party member (<see cref="RuleOptionRegistry.ElementalPurge"/>). Under <c>game</c>: the
    /// game's own rule (<see cref="ElementalRules.Purge"/>), and under the Original preset its
    /// <see cref="ElementalRules.PurgeOriginal"/> (III: manual and Remaster as-Drop, DOS refused; a Custom save reads the
    /// manual's side). <c>as-drop</c> follows <paramref name="effective"/>'s Drop.
    /// </summary>
    public static PurgePartyRule PurgeRule(this GameOptions options, ElementalRules effective)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(effective);
        var rule = options.Is(RuleOptionRegistry.ElementalPurge, "phantasie-i") ? ElementalPurge.Clears
            : options.Is(RuleOptionRegistry.ElementalPurge, "as-drop") ? ElementalPurge.AsDrop
            : options.Is(RuleOptionRegistry.ElementalPurge, "refused") ? ElementalPurge.Refused
            : options.Preset == OptionPreset.Dos ? effective.PurgeOriginal ?? effective.Purge
            : effective.Purge;
        return rule switch
        {
            ElementalPurge.Refused => PurgePartyRule.Refused,
            ElementalPurge.Keeps => PurgePartyRule.KeepsAlways,
            ElementalPurge.AsDrop when effective.DropKeeps => effective.KeepsAlone ? PurgePartyRule.KeepsAlways : PurgePartyRule.KeepsElemental,
            _ => PurgePartyRule.ClearsElemental,
        };
    }
}

/// <summary>
/// Guild ▸ Purge of a character who is in the party (D3-040). No rule removes an elemental from the game: Purge refuses an
/// elemental itself (<see cref="GuildHall.PurgeRefusal"/>), and these rules only decide whether the party copy is unloaded.
/// </summary>
public enum PurgePartyRule
{
    /// <summary>Phantasie I (OV1 <c>0x2489</c>): the member is purged and a loaded elemental is unloaded.</summary>
    ClearsElemental,

    /// <summary>III's manual, handled as III's Drop: the member is purged; the elemental stays while a character is left.</summary>
    KeepsElemental,

    /// <summary>II (ST <c>0xd68a</c>): the member is purged; the elemental stays, even when no character is left.</summary>
    KeepsAlways,

    /// <summary>DOS III: `core.guild.cannot_purge_party_member` (Drop first.)</summary>
    Refused,
}
