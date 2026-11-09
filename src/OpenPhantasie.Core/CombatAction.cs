namespace OpenPhantasie.Core;

/// <summary>
/// Packed combat action word at <c>0x74fa[char]</c> (<c>combat-resolution.md</c> [C]).
/// Encoding: <c>value = swings * 10 + target_rank_index</c> (except PARRY = 3).
/// Casts: HEALING uses <c>1000*tier + target</c>; other spells use
/// <c>0x68 + (spellIndex - 3)</c> (OV2 LAB_0000_01fb).
/// </summary>
public static class CombatAction
{
    public const int Parry = 3;

    /// <summary>Cleared / idle action after PARRY soak or end of member turn.</summary>
    public const int Cleared = 4;

    /// <summary>OV2 subtracts this from packed casts (≥105) to get cast_id.</summary>
    public const int CastBase = 0x68;

    public static int Encode(int swings, int rankIndex)
    {
        if (swings < 0) throw new ArgumentOutOfRangeException(nameof(swings));
        if (rankIndex is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(rankIndex));
        return swings * 10 + rankIndex;
    }

    public static int Thrust(int rankIndex = 0) => Encode(1, rankIndex);
    public static int Lunge() => Encode(1, 1); // rank 2 (index 1); fighters
    public static int Attack(int rankIndex = 0) => Encode(2, rankIndex);
    public static int Slash(int swings, int rankIndex = 0) => Encode(swings, rankIndex);

    /// <summary>
    /// SLASH swing count from weapon-hit effective <c>@250</c>: 4 at ≥ 225, else 3. DOS (OV2 <c>0x6cf3</c>) uses
    /// <c>@250/75 + 1</c> but only from 150 up, dropping to ATTACK/THRUST below 75/150; the remaster follows the
    /// manual (ATTACK always 2, SLASH always 3–4), keeping the DOS count wherever DOS allows a SLASH (D-011).
    /// </summary>
    public static int SlashSwings(int weaponHitEff) => weaponHitEff >= 225 ? 4 : 3;

    /// <summary>SLASH packed with the D-011 swing count for this member's <c>@250</c>.</summary>
    public static int SlashFor(int weaponHitEff, int rankIndex = 0) =>
        Encode(SlashSwings(weaponHitEff), rankIndex);

    /// <summary>
    /// Manual: only fighters may LUNGE (D-012; DOS OV2 <c>0x6d2e</c> also lets thieves). Anyone else gets DOS's
    /// fallback, a THRUST at rank 1.
    /// </summary>
    public static bool CanLunge(CharacterClass cls, bool thievesToo = false) =>
        cls == CharacterClass.Fighter || (thievesToo && cls == CharacterClass.Thief);

    /// <summary>LUNGE, or DOS's rank-1 THRUST for a class that can't; <paramref name="thievesToo"/> = DOS (D-012).</summary>
    public static int LungeFor(CharacterClass cls, bool thievesToo = false) =>
        CanLunge(cls, thievesToo) ? Lunge() : Thrust(0);

    /// <summary>DOS ATTACK (OV2 <c>0x6ccd</c>, D-011 <c>dos</c>): THRUST below <c>@250</c> 75.</summary>
    public static int DosAttackFor(int weaponHitEff, int rankIndex = 0) =>
        weaponHitEff < 75 ? Thrust(rankIndex) : Attack(rankIndex);

    /// <summary>DOS SLASH (OV2 <c>0x6cf3</c>, D-011 <c>dos</c>): ATTACK (or THRUST) below 150, else <c>@250/75 + 1</c> swings.</summary>
    public static int DosSlashFor(int weaponHitEff, int rankIndex = 0) =>
        weaponHitEff < 150 ? DosAttackFor(weaponHitEff, rankIndex) : Encode(weaponHitEff / 75 + 1, rankIndex);

    /// <summary>
    /// Pack HEALING I–IV: <c>1000 * (spellIndex+1) + targetMemberIndex</c>.
    /// </summary>
    public static int CastHealing(int spellIndex0To3, int targetMemberIndex = 0)
    {
        if (spellIndex0To3 is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(spellIndex0To3));
        if (targetMemberIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(targetMemberIndex));
        return (spellIndex0To3 + 1) * 1000 + targetMemberIndex;
    }

    /// <summary>
    /// Pack a non-HEALING spell (index ≥4): <c>0x68 + (spellIndex - 3)</c>.
    /// </summary>
    public static int CastSpell(int spellIndex)
    {
        if (spellIndex is >= 0 and <= 3)
            throw new ArgumentException("Use CastHealing for HEALING I–IV.", nameof(spellIndex));
        if ((uint)spellIndex >= SpellSlots.MaxCount)
            throw new ArgumentOutOfRangeException(nameof(spellIndex));
        return CastBase + (spellIndex - 3);
    }

    /// <summary>
    /// First packed value of Phantasie II's Toss Rock (<see cref="P2RockThrowingModule"/>): <c>TossBase + rank index</c>.
    /// The ST packs <c>−rank</c>; the remaster keeps a positive block clear of I's melee (10–99), casts (105+) and HEALING
    /// (1000–4999) so I's round loop, which skips every order ≤ <see cref="Cleared"/>, runs it.
    /// </summary>
    public const int TossBase = 5000;

    /// <summary>Highest rank index a toss can pack (ten ranks, as melee).</summary>
    public const int MaxTossRank = 9;

    /// <summary>Toss Rock at <paramref name="rankIndex"/> (0-based).</summary>
    public static int Toss(int rankIndex)
    {
        if (rankIndex is < 0 or > MaxTossRank) throw new ArgumentOutOfRangeException(nameof(rankIndex));
        return TossBase + rankIndex;
    }

    /// <summary>True for a packed Toss Rock order, with its 0-based rank.</summary>
    public static bool TryDecodeToss(int packed, out int rankIndex)
    {
        rankIndex = packed - TossBase;
        if (rankIndex is >= 0 and <= MaxTossRank) return true;
        rankIndex = 0;
        return false;
    }

    public static bool IsParry(int packed) => packed == Parry;

    public static bool IsMelee(int packed) =>
        packed is >= 10 and <= 99;

    public static bool IsCastOrSpecial(int packed) => packed >= 105;

    public static bool IsHealingCast(int packed) => packed >= 1000;

    /// <summary>
    /// Decode a cast packed word into 0-based spell index + optional HEALING target.
    /// </summary>
    public static bool TryDecodeCast(int packed, out int spellIndex, out int healTargetIndex)
    {
        healTargetIndex = 0;
        spellIndex = -1;
        if (packed < 105)
            return false;

        if (IsHealingCast(packed))
        {
            var tier = packed / 1000;
            if (tier is < 1 or > 4)
                return false;
            spellIndex = tier - 1;
            healTargetIndex = packed % 1000;
            return true;
        }

        var castId = packed - CastBase;
        spellIndex = castId + 3;
        return spellIndex is >= 4 and < SpellSlots.MaxCount; // any game's layout (I: 55, II: 56, III: 57)
    }

    public static (int Swings, int RankIndex) Decode(int packed)
    {
        if (IsParry(packed) || packed == Cleared)
            return (0, 0);
        return (packed / 10, packed % 10);
    }
}

/// <summary>
/// The combat orders: Phantasie I's six (OV2 action chooser <c>0x6bc5</c> switch types), then the orders other games'
/// modules add (appended only, so I's values never move).
/// </summary>
public enum CombatChoice
{
    Thrust,
    Attack,
    Slash,
    Lunge,
    Cast,
    Parry,

    /// <summary>Phantasie II's "7) Toss Rock" (<see cref="P2RockThrowingModule"/>): any member, any rank.</summary>
    Toss,

    /// <summary>Phantasie III's Fire Bow (<see cref="P3CombatModule"/>).</summary>
    FireBow,

    /// <summary>Phantasie III's Aim Blow (<see cref="P3CombatModule"/>).</summary>
    Aim,

    /// <summary>
    /// Phantasie III's "Change Rank" (string 498, root <c>0xfb5f</c>): not an order; the menu asks Front / Middle / Back
    /// (<see cref="GameSession.ChangeCombatRank"/>) and the member then picks an order.
    /// </summary>
    ChangeRank,
}

/// <summary>
/// Action chooser (OV2 <c>0x6bc5</c>, <c>combat-resolution.md</c> §Action chooser) with the manual decisions D-011
/// (ATTACK 2 / SLASH 3–4, no skill gate) and D-012 (LUNGE fighters only). Only thieves pick a rank (DOS
/// <c>pick_rank</c> <c>0x6f94</c>; manual: thieves may strike any rank); everyone else strikes rank 1.
/// </summary>
public static class CombatOrders
{
    /// <summary>
    /// Packed action for <paramref name="choice"/>. PARRY rolls at order time like DOS (<c>0x6f2b</c>):
    /// <c>rand ≤ parry @32</c> → PARRY, else no action this round. An unusable spell falls back to that PARRY path.
    /// <paramref name="rules"/> (default: the manual) can play DOS's skill gate (<c>p1.attack-gate</c>, D-011) and thieves'
    /// LUNGE (<c>p1.lunge</c>, D-012).
    /// </summary>
    public static int Choose(
        Character ch,
        CombatantPrep prep,
        CombatChoice choice,
        IRandomSource rng,
        int rankIndex = 0,
        int spellIndex = -1,
        int healTargetIndex = 0,
        GameOptions? rules = null)
    {
        rules ??= GameOptions.ManualPhantasieI;
        var gate = rules.IsDos(RuleOptionRegistry.AttackGate);
        ArgumentNullException.ThrowIfNull(ch);
        ArgumentNullException.ThrowIfNull(prep);
        ArgumentNullException.ThrowIfNull(rng);
        var rank = ch.Class == CharacterClass.Thief ? Math.Clamp(rankIndex, 0, 9) : 0;

        switch (choice)
        {
            case CombatChoice.Thrust:
                return CombatAction.Thrust(rank);
            case CombatChoice.Attack:
                return gate ? CombatAction.DosAttackFor(prep.WeaponHitEff, rank) : CombatAction.Attack(rank);
            case CombatChoice.Slash:
                return gate ? CombatAction.DosSlashFor(prep.WeaponHitEff, rank) : CombatAction.SlashFor(prep.WeaponHitEff, rank);
            case CombatChoice.Lunge:
                return CombatAction.LungeFor(ch.Class, thievesToo: rules.IsDos(RuleOptionRegistry.Lunge));
            case CombatChoice.Cast:
                var slots = SpellSlots.For(rules); // the game's spell layout (III: 57, Accuracy past I's 55)
                if (slots.Contains(spellIndex)
                    && Magic.KnowsSpell(ch, spellIndex) && Magic.CanAfford(ch, spellIndex, slots))
                {
                    return spellIndex <= 3
                        ? CombatAction.CastHealing(spellIndex, healTargetIndex)
                        : CombatAction.CastSpell(spellIndex);
                }
                return ParryRoll(ch, rng);
            case CombatChoice.Parry:
                return ParryRoll(ch, rng);
            default:
                throw new ArgumentOutOfRangeException(nameof(choice));
        }
    }

    /// <summary>OV2 <c>0x6f2b</c>: PARRY only if the 0..99 roll is ≤ the parry skill, else no action.</summary>
    public static int ParryRoll(Character ch, IRandomSource rng) =>
        rng.Next(0, 100) <= ch.Skills.Parry ? CombatAction.Parry : CombatAction.Cleared;
}

/// <summary>
/// Worn-item fill from OV1 <c>0x2fdb</c> (record pointer = <c>0x5bb0 + i·0x11a</c>). The selector
/// writes weapon points to GUILD @240 (damage B), the shield id to @242 (= its points for every
/// shield) and armor points to @244. It does not write damage A (@28); it is written on leaving town (<see cref="TownExit"/>).
/// </summary>
public static class CombatGear
{
    /// <summary>
    /// Writes the worn items' points into @240 / @242 / @244. <paramref name="points"/> is the game's point value by item id
    /// (<see cref="ICampaign.ItemPoints"/>; III its table); null = I's <see cref="ItemRules.CombatGate"/>.
    /// </summary>
    public static void ApplyWornItemPoints(Character ch, Func<int, int>? points = null)
    {
        ArgumentNullException.ThrowIfNull(ch);
        points ??= static id => ItemRules.CombatGate(id).Points;
        ch.ShieldSoak = ch.WornShield > 0 ? points(ch.WornShield) : 0;
        ch.ArmorSoak = ch.WornArmor > 0 ? points(ch.WornArmor) : 0;
        ch.WeaponDamageB = ch.WornWeapon > 0 ? points(ch.WornWeapon) : 0;
    }

    /// <summary>
    /// The selector itself (OV1 <c>0x2fdb</c>): from the nine carried items, the usable (<see cref="ItemRules.IsUsable"/>)
    /// shield, armor and weapon with the most points (the first of equals, slot order) become the worn ones; a kind with
    /// none usable is worn empty. Then <see cref="ApplyWornItemPoints"/>.
    /// </summary>
    public static void SelectWornItems(Character ch)
    {
        ArgumentNullException.ThrowIfNull(ch);
        int Best(Func<int, bool> kind)
        {
            var best = 0;
            foreach (var id in ch.InventoryItemIds)
                if (kind(id) && ItemRules.IsUsable(id, ch.Stats)
                    && (best == 0 || ItemRules.CombatGate(id).Points > ItemRules.CombatGate(best).Points))
                    best = id;
            return best;
        }
        ch.WornShield = Best(ItemRules.IsShield);
        ch.WornArmor = Best(ItemRules.IsArmor);
        ch.WornWeapon = Best(ItemRules.IsWeapon);
        ApplyWornItemPoints(ch);
    }
}
