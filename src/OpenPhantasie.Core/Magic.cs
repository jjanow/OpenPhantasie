namespace OpenPhantasie.Core;

/// <summary>
/// Spell indices matching <c>spells.json</c> / <c>magic-spells.md</c> binary order.
/// </summary>
public static class SpellId
{
    public const int HealingI = 0;
    public const int HealingIi = 1;
    public const int HealingIv = 3;
    public const int FireflashI = 4;
    public const int FireflashIv = 7;
    public const int QuicknessI = 8;
    public const int QuicknessIv = 11;
    public const int StrengthI = 12;
    public const int StrengthIi = 13;
    public const int StrengthIv = 15;
    public const int ProtectionI = 16;
    public const int ProtectionIv = 19;
    public const int ConfusionI = 20;
    public const int ConfusionIv = 23;
    public const int WeaknessI = 24;
    public const int WeaknessIv = 27;
    public const int BindingI = 28;
    public const int BindingIv = 31;
    public const int MindblastI = 32;
    public const int MindblastIv = 35;
    public const int FlameboltI = 36;
    public const int FlameboltIv = 39;
    public const int Charm = 40;
    public const int Sleep = 41;
    public const int Teleportation = 42;
    public const int Resurrection = 43;
    public const int NinjaIi = 44;
    public const int Fear = 45;
    public const int Dissolve = 46;
    public const int SummonElemental = 47;
    public const int DispellUndead = 48;
    public const int NinjaI = 49;
    public const int Awaken = 50;
    public const int MonsterEval = 51;
    public const int Vision = 52;
    public const int Transportation = 53;
    public const int DivineI = 54;

    public const int Count = CharacterCreation.SpellCount;

    /// <summary>DOS spell slots (name table DS <c>0x174d</c>, flag words <c>@96 + 2·slot</c>): 60, of which 0–53 and 57 are named.</summary>
    public const int DosSlotCount = 60;

    /// <summary>[C] DIVINE I's DOS slot (slots 54–56 are empty): known flag <c>@210</c>, MP cost <c>57 % 4 + 1</c> = 2 (s41).</summary>
    public const int DivineIDosSlot = 57;

    /// <summary>
    /// [C] (s41, <c>magic-spells.md</c> §DIVINE I) DOS slot of a remaster spell index. The DOS spell lists, costs and
    /// known-flags are indexed by slot; remaster indices 0–53 are the same slots, DIVINE I (54) is slot 57.
    /// </summary>
    public static int DosSlot(int spellIndex)
    {
        if (spellIndex == DivineIi)
            return DivineIiSlot;
        if ((uint)spellIndex >= Count)
            throw new ArgumentOutOfRangeException(nameof(spellIndex));
        return spellIndex == DivineI ? DivineIDosSlot : spellIndex;
    }

    /// <summary>Phantasie II's DIVINE II (#57): remaster index 55 (II's <c>spells.json</c>), ST slot 56, so it costs 1 MP.</summary>
    public const int DivineIi = 55;

    /// <summary>[C] (ST <c>magic-divine.md</c> §1) DIVINE II's spell slot.</summary>
    public const int DivineIiSlot = 56;

    /// <summary>Remaster index of a DOS slot, or -1 for an unnamed slot (54–56, 58, 59).</summary>
    public static int FromDosSlot(int slot) => slot switch
    {
        >= 0 and < DivineI => slot,
        DivineIDosSlot => DivineI,
        _ => -1,
    };
}

/// <summary>
/// A game's spell layout (T4, shared): how many spells it has (remaster indices <c>0 … Count − 1</c>) and each index's DOS
/// spell slot, which sets the MP cost <c>(slot % 4) + 1</c>. Phantasie I has 55 (DIVINE I, index 54, is slot 57); II adds
/// DIVINE II (index 55, slot 56); III's 57 sit in their own slots (index = slot: #55 / #56 Accuracy / Party Accuracy are
/// slots 54 / 55, 3 / 4 MP; <c>FORMAT.md</c> §4.2). Game-neutral: a later game adds its layout here or builds one from
/// its data's <c>dos_slot</c> column (<see cref="FromDosSlots"/>).
/// </summary>
public sealed class SpellSlots
{
    private readonly int[] _slots;

    private SpellSlots(int[] slots) => _slots = slots;

    /// <summary>The largest spell count any layout may have (packing bound; the DOS name tables have 60 slots).</summary>
    public const int MaxCount = SpellId.DosSlotCount;

    /// <summary>Number of spells (indices <c>0 … Count − 1</c>).</summary>
    public int Count => _slots.Length;

    /// <summary>Phantasie I: 0–53 in their slots, DIVINE I (54) in slot 57.</summary>
    public static SpellSlots PhantasieI { get; } =
        new([.. Enumerable.Range(0, SpellId.DivineI), SpellId.DivineIDosSlot]);

    /// <summary>Phantasie II: I's, then DIVINE II (55) in slot 56 (ST <c>magic-divine.md</c> §1).</summary>
    public static SpellSlots PhantasieII { get; } =
        new([.. Enumerable.Range(0, SpellId.DivineI), SpellId.DivineIDosSlot, SpellId.DivineIiSlot]);

    /// <summary>Phantasie III: 57 spells, index = slot (#53 empty, #57 Divine Aid; <c>FORMAT.md</c> §4.2).</summary>
    public static SpellSlots PhantasieIII { get; } = new([.. Enumerable.Range(0, 57)]);

    /// <summary>A layout from a game's data (<c>spells.json</c> <c>dos_slot</c>, in index order).</summary>
    public static SpellSlots FromDosSlots(IReadOnlyList<int> dosSlots)
    {
        ArgumentNullException.ThrowIfNull(dosSlots);
        if (dosSlots.Count > MaxCount)
            throw new ArgumentOutOfRangeException(nameof(dosSlots), $"At most {MaxCount} spells.");
        return new([.. dosSlots]);
    }

    /// <summary>The layout of <paramref name="game"/>; Phantasie I's for a game without one of its own (IV has its own magic).</summary>
    public static SpellSlots For(GameId game) => game switch
    {
        GameId.P2 => PhantasieII,
        GameId.P3 => PhantasieIII,
        _ => PhantasieI,
    };

    /// <summary>The layout of the game <paramref name="rules"/> play (Phantasie I's when null).</summary>
    public static SpellSlots For(GameOptions? rules) => For(rules?.Game ?? GameId.P1);

    /// <summary>True when <paramref name="spellIndex"/> is one of the game's spells.</summary>
    public bool Contains(int spellIndex) => (uint)spellIndex < (uint)_slots.Length;

    /// <summary>The DOS slot of a spell index.</summary>
    public int DosSlot(int spellIndex) =>
        Contains(spellIndex) ? _slots[spellIndex] : throw new ArgumentOutOfRangeException(nameof(spellIndex));

    /// <summary><c>(slot % 4) + 1</c>.</summary>
    public int MagicPointCost(int spellIndex) => (DosSlot(spellIndex) % 4) + 1;
}

/// <summary>Outcome of casting a spell or using a potion.</summary>
public sealed class MagicResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int MpSpent { get; init; }
    public int HpRestored { get; init; }
    public int MpRestored { get; init; }
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();

    public static MagicResult Fail(string message) =>
        new() { Success = false, Message = message, Log = new[] { message } };

    public static MagicResult Ok(string message, int mpSpent = 0, int hpRestored = 0, int mpRestored = 0,
        IReadOnlyList<string>? log = null) =>
        new()
        {
            Success = true,
            Message = message,
            MpSpent = mpSpent,
            HpRestored = hpRestored,
            MpRestored = mpRestored,
            Log = log ?? new[] { message },
        };
}

/// <summary>
/// Magic-point costs, HEALING / potion formulas, and cast eligibility from
/// <c>magic-spells.md</c>. Combat effect dispatch lives in <see cref="SpellCaster"/>.
/// </summary>
public static class Magic
{
    /// <summary>
    /// [C] OV2.OVR <c>0x47da</c> / COM picker <c>0x160e</c>: <c>(slot % 4) + 1</c> on the DOS slot — the remaster index
    /// for every spell but DIVINE I (slot 57 → 2 MP, s41).
    /// </summary>
    public static int MagicPointCost(int spellIndex) => (SpellId.DosSlot(spellIndex) % 4) + 1;

    /// <summary>The MP cost of <paramref name="spellIndex"/> in the layout <paramref name="slots"/> (null: I's / II's).</summary>
    public static int MagicPointCost(int spellIndex, SpellSlots? slots) =>
        slots is null ? MagicPointCost(spellIndex) : slots.MagicPointCost(spellIndex);

    public static bool IsCasterClass(CharacterClass cls) =>
        cls is CharacterClass.Wizard or CharacterClass.Priest
            or CharacterClass.Monk or CharacterClass.Ranger;

    public static bool KnowsSpell(Character ch, int spellIndex) =>
        (uint)spellIndex < (uint)ch.SpellsKnown.Length && ch.SpellsKnown[spellIndex] == 1;

    public static bool CanAfford(Character ch, int spellIndex, SpellSlots? slots = null) =>
        ch.MagicCur >= MagicPointCost(spellIndex, slots);

    /// <summary>
    /// HEALING I–IV amount before clamp. [~] RNG treated as 0..99 (<c>FUN_0000_655b</c>).
    /// OV2 floors a zero roll result to 1.
    /// </summary>
    public static int HealingAmount(int tier1To4, int rng0To99)
    {
        if (tier1To4 is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(tier1To4));
        var heal = ((6 * tier1To4 * tier1To4 + 4) * rng0To99) / 100;
        return heal == 0 ? 1 : heal;
    }

    /// <summary>
    /// D-010 <c>dos</c> (<c>p1.town-healing</c>): DOS's town HEALING [C] (OV1 <c>0x261a</c>), <c>rand / (32767 / c)</c> with
    /// <c>c = 6·tier² + 4</c> and no floor: 0–10 / 0–28 / 0–58 / 0–100 (the top value only on the last few words).
    /// </summary>
    public static int TownHealingAmountDos(int tier1To4, int dosWord)
    {
        if (tier1To4 is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(tier1To4));
        return dosWord / (32767 / (6 * tier1To4 * tier1To4 + 4));
    }

    /// <summary>Apply HEALING to a target; returns HP actually gained.</summary>
    public static int ApplyHealing(Character target, int healAmount)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (healAmount < 0) healAmount = 0;
        var room = Math.Max(0, target.HpMax - target.HpCur);
        var gained = Math.Min(room, healAmount);
        target.HpCur += gained;
        return gained;
    }

    /// <summary>[C] HEALING potion ids 101..110 → <c>n²</c> HP.</summary>
    public static int PotionHealingAmount(int itemId)
    {
        if (itemId is < 101 or > 110)
            throw new ArgumentOutOfRangeException(nameof(itemId), "Not a HEALING potion.");
        var n = itemId - 100;
        return n * n;
    }

    /// <summary>[C] MAGIC potion ids 111..120 → <c>3n</c> MP.</summary>
    public static int PotionMagicAmount(int itemId)
    {
        if (itemId is < 111 or > 120)
            throw new ArgumentOutOfRangeException(nameof(itemId), "Not a MAGIC potion.");
        return 3 * (itemId - 110);
    }

    public static bool IsHealingPotion(int itemId) => itemId is >= 101 and <= 110;
    public static bool IsMagicPotion(int itemId) => itemId is >= 111 and <= 120;
    public static bool IsPotion(int itemId) => IsHealingPotion(itemId) || IsMagicPotion(itemId);

    /// <summary>
    /// Consume one inventory potion on <paramref name="target"/> (OV2 <c>FUN_0000_43e2</c>).
    /// </summary>
    public static MagicResult UsePotion(Character owner, Character target, int inventoryIndex)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(target);
        if ((uint)inventoryIndex >= (uint)owner.InventoryItemIds.Count)
            return MagicResult.Fail(GameText.S("core.magic.no_item_in_slot"));

        var itemId = owner.InventoryItemIds[inventoryIndex];
        if (!IsPotion(itemId))
            return MagicResult.Fail(GameText.S("core.magic.not_a_potion"));

        CarriedItems.ClearSlot(owner, inventoryIndex); // [C] slot := 0 in place (OV2 0x446b, OV1 0x31ac)

        if (IsHealingPotion(itemId))
        {
            var amount = PotionHealingAmount(itemId);
            var gained = ApplyHealing(target, amount);
            return MagicResult.Ok(
                GameText.F("core.magic.x_drinks_healing_x_x", target.Name, itemId - 100, gained),
                hpRestored: gained);
        }

        var restore = PotionMagicAmount(itemId);
        var room = Math.Max(0, target.MagicMax - target.MagicCur);
        var gainedMp = Math.Min(room, restore);
        target.MagicCur += gainedMp;
        return MagicResult.Ok(
            GameText.F("core.magic.x_drinks_magic_x_x", target.Name, itemId - 110, gainedMp),
            mpRestored: gainedMp);
    }

    /// <summary>
    /// Deduct MP for a known, affordable cast. Returns cost or -1 on failure. <paramref name="slots"/> is the game's spell
    /// layout (null: Phantasie I's 55).
    /// </summary>
    public static int TrySpendMp(Character caster, int spellIndex, out string reason, SpellSlots? slots = null)
    {
        ArgumentNullException.ThrowIfNull(caster);
        if (!(slots ?? SpellSlots.PhantasieI).Contains(spellIndex))
        {
            reason = GameText.S("core.magic.unknown_spell");
            return -1;
        }

        if (caster.Status != CharacterStatus.Ok || caster.HpCur <= 0)
        {
            reason = GameText.S("core.magic.caster_unable");
            return -1;
        }

        if (!IsCasterClass(caster.Class) && caster.MagicMax <= 0)
        {
            reason = GameText.S("core.magic.not_a_spellcaster");
            return -1;
        }

        if (!KnowsSpell(caster, spellIndex))
        {
            reason = GameText.S("core.magic.spell_not_known");
            return -1;
        }

        var cost = MagicPointCost(spellIndex, slots);
        if (caster.MagicCur < cost)
        {
            reason = GameText.S("core.magic.not_enough_mp");
            return -1;
        }

        caster.MagicCur -= cost;
        reason = "";
        return cost;
    }

    // --- Tier coefficient tables recovered from OV2 cast dispatch [C] ---------

    /// <summary>FIREFLASH I–IV DOS damage scale params (cast_id 1–4). Kept for reference; the remaster uses
    /// <see cref="FireflashManualMax"/> (D-029).</summary>
    public static readonly int[] FireflashParams = { 9, 0x1d, 0x3a, 100 };

    /// <summary>QUICKNESS I–IV percent bonuses to weapon-hit.</summary>
    public static readonly int[] QuicknessPercent = { 10, 0x19, 0x2d, 0x46 };

    /// <summary>STRENGTH I–IV flat weapon-damage bonuses.</summary>
    public static readonly int[] StrengthFlat = { 2, 4, 7, 0xb };

    /// <summary>PROTECTION I–IV flat armor-soak bonuses.</summary>
    public static readonly int[] ProtectionFlat = { 1, 3, 6, 10 };

    /// <summary>CONFUSION I–IV scale percents (special-ability gate × p / 100). DOS IV = 5; the remaster uses 0
    /// so CONFUSION IV stops specials completely, as the manual says (D-014, user decision s23).</summary>
    public static readonly int[] ConfusionPercent = { 0x50, 0x3c, 0x28, 0 };

    /// <summary>DOS's CONFUSION I–IV percents (OV2 <c>0x1403</c>): IV = 5 (<c>p1.confusion-iv</c> = <c>dos</c>, D-014).</summary>
    public static readonly int[] ConfusionPercentDos = { 0x50, 0x3c, 0x28, 5 };

    /// <summary>WEAKNESS I–IV scale percents (monster attack power).</summary>
    public static readonly int[] WeaknessPercent = { 0x50, 0x3c, 0x28, 0x14 };

    /// <summary>BINDING I–IV scale percents (monster defense).</summary>
    public static readonly int[] BindingPercent = { 0x55, 0x46, 0x37, 0x2d };

    /// <summary>D-013 (user, s23): BINDING always acts as if |defense| were at least this, so it always helps.</summary>
    public const int BindingMinimumDefense = 20;

    /// <summary>
    /// BINDING applied to a monster defense (D-013, manual: "easier to hit"). Positive defense keeps the DOS
    /// scaling (OV2 0x1429 <c>defense · pct / 100</c>); negative defense moves further down by the same share of
    /// its size. Either way the cut is at least <c>20 · (100 − pct) / 100</c> (3 / 6 / 9 / 11), so defense 0 drops too.
    /// </summary>
    public static int BoundDefense(int defense, int pct)
    {
        var minimumCut = BindingMinimumDefense * (100 - pct) / 100;
        if (defense > 0)
            return Math.Min(defense * pct / 100, defense - minimumCut);
        return defense - Math.Max(-defense * (100 - pct) / 100, minimumCut);
    }

    /// <summary>MINDBLAST I–IV damage scale (no armor).</summary>
    public static readonly int[] MindblastParams = { 5, 0xb, 0x29, 0x51 };

    /// <summary>FLAMEBOLT I–IV damage scale (armor-resisted).</summary>
    public static readonly int[] FlameboltParams = { 7, 0xf, 0x1f, 0x42 };

    /// <summary>FIREFLASH I–IV top damage at full INT — manual (PDF p.19, printed 15): 1–10 / 1–30 / 1–60 / 1–100.</summary>
    public static readonly int[] FireflashManualMax = { 10, 30, 60, 100 };

    /// <summary>INT at which FIREFLASH reaches the manual's top damage (the Human ceiling; higher INT gives no more).</summary>
    public const int FireflashFullInt = 18;

    /// <summary>
    /// FIREFLASH damage — <b>remaster design (user, D-029), not DOS</b>: the manual's ranges, scaled by the caster's INT
    /// <c>@2</c>: <c>1 + rng · (max − 1) · min(INT, 18) / (99 · 18)</c>. INT 18+ gives exactly the manual range; lower INT
    /// shrinks the top (INT 13: 1–7 / 1–21 / 1–43 / 1–72); minimum is always 1. DOS OV2 <c>0x321..0x35d</c> is
    /// <c>rng · param · INT / 2000 + 1</c> with params <see cref="FireflashParams"/>, which needs INT 20 (above every race
    /// ceiling) to reach the manual's numbers. See <c>magic-spells.md</c> §Effect coefficients.
    /// </summary>
    public static int FireflashDamage(int rng0To99, int tier, int intelligence)
    {
        var max = FireflashManualMax[tier];
        var intel = Math.Clamp(intelligence, 0, FireflashFullInt);
        return 1 + rng0To99 * (max - 1) * intel / (99 * FireflashFullInt);
    }

    /// <summary>
    /// DOS FIREFLASH damage (OV2 <c>0x321..0x35d</c>, <c>p1.fireflash-damage</c> = <c>dos</c>, D-029):
    /// <c>rng · param · INT / 2000 + 1</c> with <see cref="FireflashParams"/>.
    /// </summary>
    public static int FireflashDamageDos(int rng0To99, int tier, int intelligence) =>
        rng0To99 * FireflashParams[tier] * intelligence / 2000 + 1;

    /// <summary>AoE spell damage sample: <c>(rng * param) / 100</c>.</summary>
    public static int ScaledSpellDamage(int rng0To99, int param) =>
        (rng0To99 * param) / 100;

    public static int TierIndex(int spellIndex, int firstOfLine) =>
        spellIndex - firstOfLine;
}
