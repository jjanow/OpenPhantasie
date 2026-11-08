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
}

/// <summary>Guild ▸ Purge of a character who is in the party (D3-040).</summary>
public enum PurgePartyRule
{
    /// <summary>Phantasie I (OV1 <c>0x2489</c>): the member is purged and a loaded elemental is unloaded.</summary>
    ClearsElemental,

    /// <summary>III's manual, handled as III's Drop: the member is purged; the elemental stays while a character is left.</summary>
    KeepsElemental,

    /// <summary>DOS III: `core.guild.cannot_purge_party_member` (Drop first.)</summary>
    Refused,
}
