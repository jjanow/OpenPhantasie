namespace OpenPhantasie.Core;

/// <summary>Which class gets which starting spell on an I → II transfer (<c>p2.transfer-spells</c>, D2-002).</summary>
public enum TransferSpellRule
{
    /// <summary>The manual (p.27): Wizards keep spell #5 (FIREFLASH I), Priests spell #1 (HEALING I).</summary>
    Manual,

    /// <summary>The Atari ST (<c>UTILIT2.PRG</c> <c>0x0edc</c>): also HEALING I for Rangers and FIREFLASH I for Monks.</summary>
    St,
}

/// <summary>Which divine spells survive an I → II transfer (<c>p2.transfer-divine</c>, D2-004).</summary>
public enum TransferDivineRule
{
    /// <summary>"#55 and above" (manual p.27; what the ST keeps: slots 54–59).</summary>
    FromP27,

    /// <summary>"#58+" (manual p.14: slots 57–59 only).</summary>
    FromP14,
}

/// <summary>What happens to bank gold on an I → II transfer (<c>p2.transfer-gold</c>, D2-003).</summary>
public enum TransferGoldRule
{
    /// <summary>The ST formula <c>⌊hi/35⌋·256 + 100</c> (user s76: accepted).</summary>
    St,

    /// <summary>Candidate (pending the user): <c>min(old, formula)</c>, so no character gains gold.</summary>
    NoGain,
}

/// <summary>The rules one I → II transfer runs with (from a Phantasie II save's <see cref="GameOptions"/>).</summary>
public sealed record TransferIToIIRules(TransferSpellRule Spells, TransferDivineRule Divine, TransferGoldRule Gold)
{
    /// <summary>The ST's own transfer (every value as <c>UTILIT2.PRG</c> does it).</summary>
    public static TransferIToIIRules St { get; } = new(TransferSpellRule.St, TransferDivineRule.FromP27, TransferGoldRule.St);
}

/// <summary>
/// The Phantasie I → II character transfer (Phase 09 T3; spec <c>reverse-engineering/atari-st/phantasie2/logic-specs/
/// transfer.md</c>, byte table <c>FORMAT.md</c> §6): the ST copies I's record and changes a fixed set of fields. Pure and
/// headless; the input is never changed. Works on the shared <see cref="Character"/> model.
/// <para>
/// Not modelled (spec §2): the fight-start armor copy <c>@247</c> (combat prep rewrites it), the 36 II-only bytes
/// (282–317; zero = origin Gelnor <c>[~]</c>), and II's TOSS skill <c>@42</c> (0 from I). II's three elementals come from
/// II's own <c>TWNS.INT</c>, not from I. Checked live in Hatari (s76, <c>parity/transfer-live.md</c>): byte for byte.
/// </para>
/// </summary>
public static class TransferIToII
{
    /// <summary>I's character slots the ST converts (<c>0x0edc</c> loops 22; the elementals 22–24 are not converted).</summary>
    public const int CharacterSlots = 22;

    /// <summary>Spell flag slot of HEALING I (#1) and FIREFLASH I (#5).</summary>
    public const int HealingISlot = 0, FireflashISlot = 4;

    /// <summary>First kept spell slot: #55 (p.27, the ST) or #58 (p.14).</summary>
    public static int FirstKeptSlot(TransferDivineRule rule) => rule == TransferDivineRule.FromP14 ? 57 : 54;

    /// <summary>The bank word's top: the ST reads I's u16 word; a remaster balance above it is read as 65,535.</summary>
    public const int IBankWordMax = ushort.MaxValue;

    /// <summary>
    /// The items a transferred character arrives with, in carried-slot order (bytes 223/225/227: SM SHIELD 4, MACE 49,
    /// LEATHER 23); every I item is dropped. The wand of Nikademus the manual keeps is not an item in II (D2-001, see
    /// <see cref="HasWandOfNikademus"/>).
    /// </summary>
    public static IReadOnlyList<int> StarterItems { get; } = [StarterShield, StarterWeapon, StarterArmor];

    /// <summary>Score above which a Phantasie II character holds the wand of Nikademus.</summary>
    public const int WandScoreThreshold = 99;

    /// <summary>
    /// D2-001 (s76 research): no Phantasie II version has a wand item. II's "wand of Nikademus" is a flag derived from the
    /// character's Phantasie I score (<c>@70</c>, <see cref="Character.Score"/>, copied unchanged by the transfer): score
    /// &gt; 99. <c>PHANT.PRG</c> <c>0x3a16</c> prints "- WAND OF NIK -" on the sheet and the Mystic (<c>0xd7e4</c>) adds
    /// "heroes of Gelnor". So the transfer keeps the wand as the manual says, and needs no option.
    /// </summary>
    public static bool HasWandOfNikademus(Character c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Score > WandScoreThreshold;
    }

    /// <summary>The starter kit's ids and points (bytes 83/85/87 and 241/243/245).</summary>
    public const int StarterShield = 4, StarterWeapon = 49, StarterArmor = 23;

    public const int StarterWeaponPoints = 4, StarterShieldSoak = 4, StarterArmorSoak = 3;

    /// <summary>II's MP max and current after the transfer (low bytes 17, 19 := 5).</summary>
    public const int StartMagic = 5;

    /// <summary>
    /// II's town (byte 255 := 1). II's town numbers are 1-based (<c>FORMAT.md</c> §9: <c>@254</c> = 1 = record 0 =
    /// PIPPACOTT, II's new-game town <c>[C]</c>), unlike I's 0-based <c>@254</c>, so this is Core town 1, Pippacott.
    /// </summary>
    public const int StartTown = 1;

    /// <summary>One character, I → II (spec §2–§5).</summary>
    public static Character Convert(Character fromI, TransferIToIIRules rules)
    {
        var c = ApplyByteTable(fromI, rules, StartTown);
        if (c.RosterOrdinal > 0) // 0x128a runs on occupied records only (mode 2; mode 1 has no XP raise)
            c.Experience = Math.Max(c.Experience, LevelZeroMinimum(c));
        c.Phantasie2 = new(); // the 36 II-only bytes 282–317 := 0: origin Gelnor, quest score 0, no minions or runes
        return c;
    }

    /// <summary>
    /// The converter <c>0x0edc</c>'s per-byte table (spec sections 2–5 without the section 4 step 2 XP raise), shared by both directions:
    /// mode 2 (I → II, <see cref="Convert"/>) and mode 1 (II → I, <see cref="TransferIIToI"/>). <paramref name="town"/> is
    /// the Core town byte 255 := 1 stands for in the target game. The input is never changed.
    /// </summary>
    internal static Character ApplyByteTable(Character from, TransferIToIIRules rules, int town)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(rules);
        var c = from.Clone(); // the ST copies bytes 0–281; only the fields below change (their high bytes are kept)

        c.MagicMax = (c.MagicMax & ~0xFF) | StartMagic;
        c.MagicCur = (c.MagicCur & ~0xFF) | StartMagic;
        c.HpMax &= ~0xFF; // low byte cleared: HP 0 for any HP below 256
        c.HpCur &= ~0xFF;
        c.Level = (c.Level & ~0xFF) | ((c.Level & 0xFF) == 20 ? 20 : 0);

        c.WornShield = StarterShield;
        c.WornArmor = StarterArmor;
        c.WornWeapon = StarterWeapon;
        c.InventoryItemIds.Clear();
        c.InventoryItemIds.AddRange(StarterItems); // Score (@70) stays: it carries the wand (D2-001)
        c.WeaponDamageB = (c.WeaponDamageB & ~0xFF) | StarterWeaponPoints;
        c.ShieldSoak = (c.ShieldSoak & ~0xFF) | StarterShieldSoak;
        c.ArmorSoak = (c.ArmorSoak & ~0xFF) | StarterArmorSoak;

        c.CurrentTown = town;
        c.SpellsKnown = Spells(from, rules);
        c.GoldInBank = BankGold(from.GoldInBank, rules.Gold);
        c.Experience = Experience(from.Experience);

        // I's DOS GUILD.DAT carry describes an I record from before the transfer; it is not written back through it.
        c.Phantasie1.DosRecord = null;
        c.Phantasie1.DosRecordLifespan = null;
        return c;
    }

    /// <summary>
    /// I's roster, I → II: the first <see cref="CharacterSlots"/> slots are converted (an empty slot stays empty); the
    /// elemental slots (a DOS GUILD.DAT's records 22–24) are left out.
    /// <para>
    /// <paramref name="guildUnlimited"/> (QoL <c>guild-limit</c> = <c>none</c>, <see cref="GameOptions.GuildUnlimited"/>):
    /// the input is the remaster's <see cref="GuildRoster.Slots"/>, which may hold characters past slot 22 and never holds
    /// the elementals (they are templates), so every slot is converted and the result keeps the input's length (at least
    /// <see cref="CharacterSlots"/>).
    /// </para>
    /// </summary>
    public static Character?[] ConvertRoster(IReadOnlyList<Character?> iSlots, TransferIToIIRules rules,
        bool guildUnlimited = false)
    {
        ArgumentNullException.ThrowIfNull(iSlots);
        var count = guildUnlimited ? Math.Max(CharacterSlots, iSlots.Count) : CharacterSlots;
        var slots = new Character?[count];
        for (var i = 0; i < Math.Min(count, iSlots.Count); i++)
            slots[i] = iSlots[i] is { } c ? Convert(c, rules) : null;
        return slots;
    }

    /// <summary>
    /// Spell flags (spec §3): #1–#54 cleared; the divine slots from <see cref="FirstKeptSlot"/> kept; HEALING I / FIREFLASH I
    /// set to 1 for the classes <paramref name="rules"/> names. Works on the remaster's indices through their DOS slots.
    /// </summary>
    public static int[] Spells(Character fromI, TransferIToIIRules rules)
    {
        ArgumentNullException.ThrowIfNull(fromI);
        var first = FirstKeptSlot(rules.Divine);
        var spells = new int[fromI.SpellsKnown.Length];
        for (var i = 0; i < spells.Length; i++)
            if (SpellId.DosSlot(i) >= first)
                spells[i] = fromI.SpellsKnown[i];

        var st = rules.Spells == TransferSpellRule.St;
        if (fromI.Class == CharacterClass.Priest || (st && fromI.Class == CharacterClass.Ranger))
            spells[SpellId.FromDosSlot(HealingISlot)] = 1;
        if (fromI.Class == CharacterClass.Wizard || (st && fromI.Class == CharacterClass.Monk))
            spells[SpellId.FromDosSlot(FireflashISlot)] = 1;
        return spells;
    }

    /// <summary>Bank gold (spec §5): <c>⌊hi/35⌋·256 + 100</c> from the u16 word's high byte; <c>no-gain</c> caps it at the old balance.</summary>
    public static int BankGold(int iBank, TransferGoldRule rule)
    {
        var word = Math.Clamp(iBank, 0, IBankWordMax);
        var formula = (word >> 8) / 35 * 256 + 100;
        return rule == TransferGoldRule.NoGain ? Math.Min(iBank, formula) : formula;
    }

    /// <summary>Experience step 1 (spec §4): 256 · g, g from the old XP's tier.</summary>
    public static long Experience(long iXp) => 256L * XpTier(iXp);

    /// <summary>
    /// The tier g of spec §4 (bytes 266–269: only byte 268 = g is kept). The ST reads byte 267 = <c>(XP &gt;&gt; 16) &amp; 0xFF</c>
    /// as b and, for b = 0, byte 268 ≥ 20 (XP mod 65,536 ≥ 5,120); byte 266 is never read, so XP wraps at 2^24 (live s76:
    /// 16,847,216 → b 1 → 512). A remaster XP outside I's s32 word is read as the word would hold it (clamped to 0..2^31−1).
    /// </summary>
    public static int XpTier(long xp)
    {
        var word = (uint)Math.Clamp(xp, 0, int.MaxValue);
        var b = (word >> 16) & 0xFF;
        if (b == 0)
            return ((word >> 8) & 0xFF) >= 20 ? 1 : 0;
        return b switch
        {
            1 => 2,
            <= 5 => 3,
            <= 12 => 4,
            <= 24 => 5,
            <= 40 => 6,
            <= 70 => 7,
            <= 100 => 8,
            <= 150 => 9,
            <= 220 => 10,
            _ => 11,
        };
    }

    /// <summary>
    /// Experience step 2 (<c>0x128a</c>, live s76): I's training formula at level 0, <c>trunc(200 / (sum/mult + 1e-5))</c>, with
    /// II's sums for FIGHTER and PRIEST (spec §4). XP is raised to it when lower. Computed in doubles; the ST uses FFP
    /// singles. Both gave the same integers on every live input; a threshold within float rounding of an integer could
    /// differ <c>[~]</c>.
    /// </summary>
    public static long LevelZeroMinimum(Character c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var s = c.Stats;
        var (sum, mult) = c.Class switch
        {
            CharacterClass.Thief => (s.Str + 2 * s.Dex, 19.8),
            CharacterClass.Monk => (s.Str + s.Int + s.Dex, 49.5),
            CharacterClass.Ranger => (s.Str + s.Int + s.Cha, 52.8),
            CharacterClass.Fighter => (2 * s.Str + s.Dex, 33.0),
            CharacterClass.Priest => (s.Str + 2 * s.Int, 36.3),
            CharacterClass.Wizard => (2 * s.Int + s.Luck, 42.9),
            _ => (200, 1.0), // other class bytes: threshold 0
        };
        return (long)Math.Truncate(200.0 / (sum / mult + 1e-5));
    }
}
