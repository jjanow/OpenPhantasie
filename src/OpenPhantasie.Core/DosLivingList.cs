namespace OpenPhantasie.Core;

/// <summary>
/// DOS's non-DEAD member list <c>[0x64a2]</c> and its word <c>[0x6490]</c> (<c>combat-resolution.md</c> §SUMMON
/// ELEMENTAL, "The living list and its word"). Read only under D-033's <c>dos</c> values: <c>p1.summon-party</c> (the
/// Summon Elemental test <c>0x8cc</c> and its list write) and <c>p1.monster-spell-target</c> (the target of monster
/// specials 1–4, <c>0x1731</c>). Always kept up to date, whatever the options, and kept for the whole session, like
/// DOS's RAM, because an entry past the current count keeps whatever an earlier rebuild wrote.
/// </summary>
/// <remarks>
/// <para>Writers [C] (OV2 bytes): <c>0x43ae</c> lists the party slots <c>0..[0x648e]−1</c> with status ≠ DEAD
/// into entries <c>0, 1, …</c> and returns the last index (−1 for none); entries past it are left alone. The orders phase
/// (<c>0x6a32</c>) stores that index in the word; each monster turn (melee start <c>0xd93</c>, after a hit <c>0xf3a</c>,
/// after a miss <c>0xfed</c>, after a special <c>0x1065</c>) and the end of combat (<c>0x3fa9</c>) store index + 1 (the
/// count). RESURRECTION (<c>0x11d4</c>, combat and field) rebuilds the list but only adds 1 to the word, so the word keeps
/// its meaning. A death (<c>0x12f4</c>) subtracts 1. Summon Elemental (<c>0x8e1</c>) adds 1 and writes slot 6 at the new
/// word. Out of combat the step handler (<c>0x432a</c>), dungeon event damage (<c>0x26d0</c>), traps (<c>0x31e1</c>) and
/// swimming (<c>0x695c</c>) rebuild the list without touching the word. OV1 and the COM never touch either.</para>
/// <para>[?] The list and word at start-up: both are in the COM's BSS (DS <c>0x64a2</c> lies past the unpacked image),
/// so their first value is whatever memory held. Core starts with no entries and word 0; an entry never written gives no
/// target.</para>
/// </remarks>
public sealed class DosLivingList
{
    /// <summary>Party slots 0..6 (slot 6 = the elemental).</summary>
    public const int Capacity = Party.MaxMembers + 1;

    /// <summary>The elemental's party slot (index 6).</summary>
    public const int ElementalSlot = Party.MaxMembers;

    private readonly int?[] _entries = new int?[Capacity];

    /// <summary><c>[0x6490]</c>: the last living index after the orders phase, the living count after a monster turn.</summary>
    public int Word { get; set; }

    /// <summary>The list entries (party slot numbers; null = never written).</summary>
    public IReadOnlyList<int?> Entries => _entries;

    /// <summary>Entry <paramref name="index"/>, or null when it is outside the list or was never written.</summary>
    public int? Entry(int index) => (uint)index < Capacity ? _entries[index] : null;

    /// <summary>
    /// <c>0x43ae</c> over the party slots <paramref name="slots"/> (<c>[0x648e]</c> of them): every non-DEAD slot in
    /// order. Returns the last index written, −1 when nobody is listed. Entries past it keep their old values.
    /// </summary>
    public int Rebuild(IReadOnlyList<Character> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var last = -1;
        for (var i = 0; i < slots.Count && last + 1 < Capacity; i++)
        {
            if (slots[i].Status == CharacterStatus.Dead)
                continue;
            _entries[++last] = i;
        }
        return last;
    }

    /// <summary>The orders phase (<c>0x6a32</c>): rebuild, word := last index.</summary>
    public void OrdersPhase(IReadOnlyList<Character> slots) => Word = Rebuild(slots);

    /// <summary>A monster turn (<c>0xd93</c>, <c>0xf3a</c>, <c>0xfed</c>, <c>0x1065</c>) or the end of combat (<c>0x3fa9</c>): word := count.</summary>
    public void Recount(IReadOnlyList<Character> slots) => Word = Rebuild(slots) + 1;

    /// <summary>RESURRECTION worked (<c>0x11d4</c>): rebuild, then word + 1 (the rebuild's result is dropped).</summary>
    public void Resurrected(IReadOnlyList<Character> slots)
    {
        Rebuild(slots);
        Word++;
    }

    /// <summary>A party member died (<c>0x12cf</c> → <c>0x12f4</c>): word − 1.</summary>
    public void MemberDied() => Word--;

    /// <summary>
    /// The Summon Elemental cast (<c>0x8cc</c>): with word 5 and an elemental loaded, word + 1 and the entry at the new
    /// word := slot 6. Returns whether the test passed (the caller sets party size 7).
    /// </summary>
    public bool TrySummon(bool elementalLoaded)
    {
        if (Word != Party.MaxMembers - 1 || !elementalLoaded)
            return false;
        Word++;
        _entries[Word] = ElementalSlot;
        return true;
    }

    /// <summary>Restores a saved list (a remaster save; DOS saves don't hold it).</summary>
    public void Restore(IReadOnlyList<int?>? entries, int word)
    {
        Array.Clear(_entries);
        if (entries is not null)
        {
            for (var i = 0; i < Math.Min(entries.Count, Capacity); i++)
                _entries[i] = entries[i];
        }
        Word = word;
    }
}
