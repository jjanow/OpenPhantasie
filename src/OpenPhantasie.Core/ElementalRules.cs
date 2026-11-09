namespace OpenPhantasie.Core;

/// <summary>
/// When the Guild lets an elemental join the party (T1 profile flag; <c>elementals</c> in rules.json).
/// Phantasie I (OV1 <c>0x2786</c> / <c>0x1fe8</c>, manual "will only serve a party of six"): the party must have six
/// members, and no one can be added while it is loaded. Phantasie III (ovl2 <c>0x3a0a</c>, manual "does not count as a
/// member"; <c>phantasie3/logic-specs/elementals.md</c> §2): one character is enough, and a character added later goes in
/// front of it (the elemental stays last).
/// </summary>
/// <param name="MinMembers">Characters the party needs before an elemental may join (I: 6, III: 1).</param>
/// <param name="BlocksAdd">A loaded elemental stops Guild ▸ Add (I: yes, III: no).</param>
/// <param name="DropKeeps">
/// Guild ▸ Drop of a character keeps a loaded elemental while a character is left (III, ovl2 <c>0x3c92</c> [C] s97: the
/// records after the dropped one move up, so the elemental stays last; the elemental flag is cleared only when no
/// character remains, <c>0x3e0b</c>). False = I's: every Drop clears party slot 7 (OV1 <c>0x22d1</c>).
/// </param>
public sealed record ElementalRules(int MinMembers, bool BlocksAdd, bool DropKeeps = false)
{
    /// <summary>Phantasie I's: six members first; nobody joins after it.</summary>
    public static ElementalRules PhantasieI { get; } = new(Party.MaxMembers, BlocksAdd: true);

    /// <summary>
    /// Phantasie III's (<c>phantasie3/rules.json</c> <c>elementals</c>, pinned equal by a test): one character first,
    /// characters join in front of it, a Drop keeps it while a character is left. Used by the cross-game options
    /// (<see cref="RuleOptionRegistry.ElementalLoad"/>, <see cref="RuleOptionRegistry.ElementalDrop"/>) in the other games.
    /// </summary>
    public static ElementalRules PhantasieIII { get; } = new(1, BlocksAdd: false, DropKeeps: true);
}

/// <summary>
/// The Guild's elemental rules a save plays by (PLAN P3 QoL, user D3-040): the game's own (<see cref="RulesProfile.Elementals"/>,
/// D3-040's <c>p3.purge</c>) under the every-game options' <c>game</c> value, else the original rule the option names.
/// </summary>
public static class ElementalGuildRules
{
    /// <summary>
    /// <paramref name="own"/> (the game's rules.json block) with Add (<see cref="RuleOptionRegistry.ElementalLoad"/>) and Drop
    /// (<see cref="RuleOptionRegistry.ElementalDrop"/>) replaced as <paramref name="options"/> say.
    /// </summary>
    public static ElementalRules Elementals(this GameOptions options, ElementalRules own)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(own);
        var load = options.Is(RuleOptionRegistry.ElementalLoad, "phantasie-i") ? ElementalRules.PhantasieI
            : options.Is(RuleOptionRegistry.ElementalLoad, "phantasie-iii") ? ElementalRules.PhantasieIII
            : own;
        var dropKeeps = options.Is(RuleOptionRegistry.ElementalDrop, "phantasie-i") ? ElementalRules.PhantasieI.DropKeeps
            : options.Is(RuleOptionRegistry.ElementalDrop, "phantasie-iii") ? ElementalRules.PhantasieIII.DropKeeps
            : own.DropKeeps;
        return new ElementalRules(load.MinMembers, load.BlocksAdd, dropKeeps);
    }

    /// <summary>
    /// What Guild ▸ Purge does to a party member (<see cref="RuleOptionRegistry.ElementalPurge"/>): under <c>game</c> III's
    /// <c>p3.purge</c> (D3-040) and I's rule elsewhere; <c>as-drop</c> follows <paramref name="effective"/>'s Drop.
    /// </summary>
    public static PurgePartyRule PurgeRule(this GameOptions options, ElementalRules effective)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(effective);
        if (options.Is(RuleOptionRegistry.ElementalPurge, "phantasie-i"))
            return PurgePartyRule.ClearsElemental;
        if (options.Is(RuleOptionRegistry.ElementalPurge, "as-drop"))
            return effective.DropKeeps ? PurgePartyRule.KeepsElemental : PurgePartyRule.ClearsElemental;
        if (options.Is(RuleOptionRegistry.ElementalPurge, "refused"))
            return PurgePartyRule.Refused;
        return options.P3PurgeRule();
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

    /// <summary>DOS III: `core.guild.cannot_purge_party_member` (Drop first.)</summary>
    Refused,
}
