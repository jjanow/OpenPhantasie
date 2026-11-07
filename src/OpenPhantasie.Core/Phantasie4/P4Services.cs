namespace OpenPhantasie.Core;

/// <summary>
/// One IV item as the shop, the distribution and the menus read it (<c>items.json</c>, <c>_idat</c>, <c>_itm</c>).
/// <paramref name="Icon"/> = <c>_idat</c> b0: the item lists draw glyph b0 + 0x20 of P4.X's font 2, a 32 × 32 icon
/// (FORMAT.md section 6.9 "Item lists"; the X68000 pack's <c>p4.item.N</c>).
/// </summary>
public sealed record P4ItemInfo(
    int Id, string Name, int Value, string Kind, P4ItemKind Slot, int Points, int MinStr, P4StrCheck Check, int Icon = 0);

/// <summary>IV's 157 items by id (0 = nothing): name, price (<c>_idat</c> u16 @4), kind and the combat fields.</summary>
public sealed class P4ItemCatalog
{
    private readonly Dictionary<int, P4ItemInfo> _items;

    public P4ItemCatalog(IEnumerable<P4ItemInfo> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToDictionary(i => i.Id);
    }

    public P4ItemInfo? this[int id] => _items.TryGetValue(id, out var i) ? i : null;

    public string Name(int id) => this[id]?.Name ?? $"#{id}";

    public int Value(int id) => this[id]?.Value ?? 0;

    /// <summary>The item's icon glyph (<c>_idat</c> b0, 0–49), or null for no item (id 0) or an unknown id.</summary>
    public int? Icon(int id) => id != 0 && this[id] is { } i ? i.Icon : null;

    /// <summary>Ids 1–156 (the shop's range, <c>_itemlist</c> wraps after 0x9C).</summary>
    public const int FirstShopItem = 1, LastShopItem = 156;
}

/// <summary>The five equipment slots <c>_equip</c> fills (record +64–68): shield, armour, weapon, bow, thrown.</summary>
public sealed record P4Equipped(int Shield, int Armour, int Weapon, int Bow, int Thrown);

/// <summary>
/// <c>_cal_user</c> and <c>_equip</c> (towns.md §6.1, combat.md §1) outside combat: who can use an item and what a member
/// wears. Item points follow D4-018 (<see cref="P4Town.ItemPoints"/>). <c>P4Battle</c> runs its own copy at combat start.
/// </summary>
public static class P4Equipment
{
    /// <summary>
    /// <c>_cal_user</c> / the shop's "N-U" test: ids 1–27 need b2 ≤ Str, 28–63 b2 ≤ 2 Str + Dex, 64–84 b2 ≤ Str + Dex; every
    /// other item can be used.
    /// </summary>
    public static bool CanUse(P4ItemInfo? item, P4Member m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (item is null || item.Slot == P4ItemKind.Other)
            return true;
        var (str, dex) = (m.Abilities[0], m.Abilities[2]);
        var have = item.Check switch
        {
            P4StrCheck.Str2PlusDex => 2 * str + dex,
            P4StrCheck.StrPlusDex => str + dex,
            _ => str,
        };
        return item.MinStr <= have;
    }

    /// <summary><c>_equip</c>: per slot the carried item with the most points the member can use (first one on a tie).</summary>
    public static P4Equipped Best(GameOptions o, P4TownTables t, P4ItemCatalog items, P4Member m)
    {
        int shield = 0, armour = 0, weapon = 0, bow = 0, thrown = 0;
        int sp = 0, ap = 0, wp = 0, bp = 0, tp = 0;
        foreach (var id in m.Items)
        {
            if (items[id] is not { Slot: not P4ItemKind.Other } it || !CanUse(it, m))
                continue;
            var p = P4Town.ItemPoints(o, t, id, it.Points);
            switch (it.Slot)
            {
                case P4ItemKind.Shield when p > sp: (shield, sp) = (id, p); break;
                case P4ItemKind.Armour when p > ap: (armour, ap) = (id, p); break;
                case P4ItemKind.Weapon when p > wp: (weapon, wp) = (id, p); break;
                case P4ItemKind.Bow when p > bp: (bow, bp) = (id, p); break;
                case P4ItemKind.Thrown when thrown == 0 || p > tp: (thrown, tp) = (id, p); break;
            }
        }
        return new P4Equipped(shield, armour, weapon, bow, thrown);
    }
}

/// <summary>
/// One level-up (<c>_calpow(record, L)</c> with L ≥ 1, <c>training.md</c> §4, §4.1): <see cref="Begin"/> pays the level-up gold,
/// the three skill picks go through <see cref="SkillAfter"/> / <see cref="Pick"/>, and <see cref="Finish"/> rolls the HP, sets
/// the magic level, opens the row's spells and writes the level (D4-060). L is the level before the level-up.
/// </summary>
public static class P4LevelUp
{
    /// <summary>The level-up gold into the account (social^social × L × 30; D4-031 cap).</summary>
    public static void Begin(GameOptions o, P4Tables t, P4Member m)
    {
        ArgumentNullException.ThrowIfNull(m);
        m.Account = P4Training.LevelUpGold(o, t.Training, t.Towns, m.Social, m.Level, m.Account);
    }

    /// <summary>The value a pick of <paramref name="skill"/> gives now (<c>_calsk</c> with the refreshed bonus).</summary>
    public static int SkillAfter(P4Tables t, P4Member m, P4Skill skill) =>
        P4Training.SkillValue(t.Training, m.Class, skill, m.Level, m.Skill(skill),
            P4Training.SkillBonus(skill, m.Abilities, m.Race));

    public static void Pick(P4Tables t, P4Member m, P4Skill skill) => m.Skills[(int)skill] = SkillAfter(t, m, skill);

    /// <summary>HP rolls, magic level, the learnable spells of the row (state ≠ 1 → 2) and the new level.</summary>
    public static void Finish(GameOptions o, P4Tables t, IRandomSource rng, P4Member m)
    {
        var l = m.Level;
        m.HpMax = P4Training.NewMaxHp(o, t.Training, rng, m.Class, l, m.Abilities[3], m.HpMax);
        m.MagicLevel = P4Training.MagicLevelAfter(t.Training, m.Class, l);
        foreach (var s in P4Training.SpellsGranted(o, t.Training, m.Class, l, t.Services))
        {
            if (s is >= 1 and <= P4Member.SpellCount && m.Spells[s - 1] != 1)
                m.Spells[s - 1] = 2;
        }
        m.Level = P4Training.LevelAfter(o, t.Service, l);
    }
}

/// <summary>Leaving a town (<c>_exittown</c>, towns.md §2 Leaving) and the inn's MP growth (training.md §4.2).</summary>
public static class P4TownRules
{
    /// <summary>
    /// <c>_par</c>+0A = the highest Score; each member's +3B = trunc(trunc(Str / 6) + |class − 3.5| × level / 11); <c>_par</c>+0B =
    /// trunc(Σ level / size + 0.5). The re-equip is <see cref="P4Equipment.Best"/> (computed where it is read).
    /// </summary>
    public static void Leave(P4Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        party.MaxScore = party.Members.Count == 0 ? 0 : party.Members.Max(m => m.Score);
        party.GoldSinceTown = 0; // D4-062: Party info counts from here
        foreach (var m in party.Members)
            m.MeleeBonus = MeleeBonus(m);
        party.AverageLevel = party.Members.Count == 0
            ? 0 : (int)Math.Truncate(party.Members.Sum(m => m.Level) / (double)party.Members.Count + 0.5);
    }

    /// <summary>Record +3B: trunc(trunc(Str / 6) + |class − 3.5| × level / 11).</summary>
    public static int MeleeBonus(P4Member m) =>
        (int)Math.Truncate(m.Abilities[0] / 6 + Math.Abs(m.Class - 3.5) * m.Level / 11);

    /// <summary>
    /// The inn's MP growth (<c>_inn</c>): a member whose +72 flag is set (a spell cast in combat) has it cleared and gains
    /// 1 max MP if rand(100) ≤ (magic level − max MP) × 20. True when MP went up.
    /// </summary>
    public static bool InnMpGrowth(P4ServiceTables s, IRandomSource rng, P4Member m)
    {
        if (!m.MpGrowth)
            return false;
        m.MpGrowth = false;
        if (P4Dice.Rand(rng, 100) > (m.MagicLevel - m.MpMax) * s.MpGrowthPercentPerPoint)
            return false;
        m.MpMax = Math.Min(255, m.MpMax + 1);
        return true;
    }

    /// <summary>The spells <c>_getmagic</c> offers at <paramref name="place"/> (D4-061: the manual's lists or the code's).</summary>
    public static IReadOnlyList<int> CastList(GameOptions o, P4ServiceTables s, P4CastPlace place) =>
        (o.P4Code(P4RuleOptions.CastPlaces) ? s.SpellPlacesCode : s.SpellPlacesManual)[place];

    /// <summary>
    /// The ending a Pendragon entry runs (<c>_ending</c>, ending.md §1): the first of f0 … f4 (story flags 0x38 …) whose next
    /// flag is clear, f4 alone; null = no ending flag set. Returns the index 0–4 (scores 125 … 225).
    /// </summary>
    public static int? Ending(P4ServiceTables s, P4Party party)
    {
        var f = Enumerable.Range(0, 5).Select(i => party.StoryFlags[s.EndingFirstFlag + i] != 0).ToArray();
        for (var i = 0; i < 4; i++)
        {
            if (f[i] && !f[i + 1])
                return i;
        }
        return f[4] ? 4 : null;
    }

    /// <summary>
    /// <c>_endset</c> + the ending's own changes (ending.md §1–§2): average level A = trunc(Σ level / size + 0.5) into
    /// <c>_par</c>+0B; each member's Score = (ending score + A) as a byte; HP / MP full; Charisma at least 20; Okay; wounds
    /// cleared; social class Noble (175) or Lord (200, 225); endings 125–175 set f0–f3.
    /// </summary>
    public static int ApplyEnding(P4ServiceTables s, P4Party party, int ending)
    {
        var score = s.EndingScores[ending];
        var a = party.Members.Count == 0
            ? 0 : (int)Math.Truncate(party.Members.Sum(m => m.Level) / (double)party.Members.Count + 0.5);
        party.AverageLevel = a;
        foreach (var m in party.Members)
        {
            m.Score = (score + a) & 0xFF;
            m.Hp = m.HpMax;
            m.Mp = m.MpMax;
            m.Abilities[4] = Math.Max(m.Abilities[4], s.EndingMinCharisma);
            m.Condition = P4Member.Okay;
            Array.Clear(m.Wounds);
            if (score == s.EndingNobleAt)
                m.Social = s.EndingNoble;
            else if (score >= s.EndingLordFrom)
                m.Social = s.EndingLord;
        }
        if (ending < 3)
        {
            for (var i = 0; i < 4; i++)
                party.StoryFlags[s.EndingFirstFlag + i] = 1;
        }
        return score;
    }
}
