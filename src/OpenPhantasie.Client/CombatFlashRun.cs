using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The spell flashes of one combat round (Phantasie III Amiga, the pack's <see cref="CombatFlashLayout"/>; FORMAT.md
/// §2.4), one after another in the order the hits landed: the Amiga flashes a target inside its damage routine, when the
/// routine's flash flag is set (<see cref="Flashes"/>). No MonoGame types: tests drive it with VBL counts.
/// </summary>
public sealed class CombatFlashRun
{
    /// <summary>
    /// One flash: on <see cref="Member"/>'s portrait or <see cref="Monster"/>'s picture, its top-left at
    /// (<see cref="X"/>, <see cref="Y"/>) from the picture's top-left, starting <see cref="StartVbl"/> VBLs into the run.
    /// <see cref="Line"/>: the round log line its hit came before (<see cref="CombatHit.Line"/>; −1 = not known), where the
    /// Amiga's combat field plays it (<see cref="CombatFieldRun"/>, FORMAT.md §8.6).
    /// </summary>
    public sealed record Flash(Character? Member, Monster? Monster, int X, int Y, int StartVbl, int Line = -1);

    private CombatFlashRun(CombatFlashLayout layout, IReadOnlyList<Flash> flashes)
    {
        Layout = layout;
        Items = flashes;
    }

    public CombatFlashLayout Layout { get; }

    public IReadOnlyList<Flash> Items { get; }

    /// <summary>VBLs the whole run lasts.</summary>
    public int LengthVbl => Items.Count * Layout.FlashVbl;

    /// <summary>
    /// Whether the Amiga flashes the target of <paramref name="hit"/>: <c>damage_monster</c> <c>0x1833e</c> for the party's
    /// spells (a swing, a shot or a tossed rock passes 0), <c>damage_character</c> <c>0x17e16</c> for the monsters'
    /// specials (spells, breath, Sleep, the lowered armour / attack) and their stunning / draining touches (a plain swing
    /// passes 0).
    /// </summary>
    public static bool Flashes(CombatHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        return hit.Monster is not null
            ? hit.Source == DamageSource.PartySpell
            : hit.Member is not null && (hit.Source == DamageSource.MonsterSpecial || hit.Touch);
    }

    /// <summary>
    /// The run for <paramref name="hits"/>, or null when none flashes or there is no layout. <paramref name="slotOf"/>: a
    /// member's party slot (−1 = not in the party: skipped); <paramref name="rankOf"/>: its combat row word (III: Back 0,
    /// Middle 1, Front 2); <paramref name="giant"/>: the monster's group has the giant flag; <paramref name="next"/>:
    /// <c>next(n)</c> = 0..n − 1, the sparkle's own jitter (never the game's random numbers).
    /// </summary>
    public static CombatFlashRun? Plan(IReadOnlyList<CombatHit> hits, CombatFlashLayout? layout, Func<Character, int> slotOf,
        Func<Character, int> rankOf, Func<Monster, bool> giant, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(slotOf);
        ArgumentNullException.ThrowIfNull(rankOf);
        ArgumentNullException.ThrowIfNull(giant);
        ArgumentNullException.ThrowIfNull(next);
        if (layout is null)
            return null;
        var flashes = new List<Flash>();
        foreach (var hit in hits)
        {
            if (!Flashes(hit))
                continue;
            var start = flashes.Count * layout.FlashVbl;
            if (hit.Monster is { } monster)
            {
                var (x, y) = layout.MonsterOffset(giant(monster), next);
                flashes.Add(new Flash(null, monster, x, y, start, hit.Line));
            }
            else if (hit.Member is { } member && slotOf(member) is var slot && slot >= 0)
            {
                var (x, y) = layout.PartyOffset(slot, rankOf(member), next);
                flashes.Add(new Flash(member, null, x, y, start, hit.Line));
            }
        }
        return flashes.Count == 0 ? null : new CombatFlashRun(layout, flashes);
    }

    /// <summary>The flash shown <paramref name="vbl"/> VBLs into the run and how many of its frames are drawn, or null.</summary>
    public (Flash Flash, int Frames)? At(double vbl)
    {
        if (vbl < 0 || vbl >= LengthVbl)
            return null;
        var flash = Items[Math.Min(Items.Count - 1, (int)(vbl / Layout.FlashVbl))];
        return (flash, FramesAt(vbl - flash.StartVbl));
    }

    /// <summary>How many frames of a flash are drawn <paramref name="vbl"/> VBLs after it starts (0 before, all after).</summary>
    public int FramesAt(double vbl) =>
        vbl < 0 ? 0 : Math.Min(Layout.Pictures.Count, (int)(vbl / Layout.FrameVbl) + 1);

    /// <summary>Milliseconds one flash lasts (<see cref="CombatFlashLayout.FlashVbl"/> at the layout's VBL rate).</summary>
    public double FlashMs => Layout.FlashVbl * 1000.0 / Layout.VblHz;

    /// <summary>(VBL, sound) of every flash's sound (before frame <see cref="CombatFlashLayout.SoundFrame"/>); empty without one.</summary>
    public IReadOnlyList<(int Frame, int Sound)> SoundPlays =>
        Layout.Sound is int sound
            ? [.. Items.Select(f => (f.StartVbl + Layout.SoundFrame * Layout.FrameVbl, sound))]
            : [];
}
