namespace OpenPhantasie.Core;

/// <summary>
/// One <c>DATM</c> monster type (70-byte record, <c>FORMAT.md</c> §6.4; <c>GameData/phantasie4/monsters.json</c>): the
/// fields the 7 × 7 battle reads.
/// </summary>
public sealed record P4MonsterType(
    int Index,
    string Name,
    int GroupMax,
    int AttackValue,
    int DamageValue,
    int Armour,
    int Defence,
    int Level,
    long Experience,
    int MagicResistance,
    int HpBase,
    int Nice,
    long Gold,
    int SpellAbility,
    IReadOnlyList<int> SpellIds,
    int NextGroupChance,
    IReadOnlyList<int> Companions,
    int Movement,
    IReadOnlyList<int> TreasureItems,
    bool Flier,
    int BodyShape,
    bool BigHitter,
    bool Undead);

/// <summary>Equipment slots by item id range (<c>_equip</c>, <c>items.json</c> kind).</summary>
public enum P4ItemKind { Other, Shield, Armour, Weapon, Bow, Thrown }

/// <summary>How an item's requirement b2 is compared (<c>_cal_user</c>): Str, 2 Str + Dex, or Str + Dex.</summary>
public enum P4StrCheck { Str, Str2PlusDex, StrPlusDex }

/// <summary>An item's combat fields (<c>_idat</c> b1 points, b2 requirement, b3 armour movement %).</summary>
public sealed record P4ItemStats(int Id, P4ItemKind Kind, int Points, int MinStr, P4StrCheck Check, int MovePct);

/// <summary>One <c>_e_obstacle(kind, count, passable)</c> call: count = rand(Die) + Add.</summary>
public sealed record P4ObstacleRoll(int Kind, int Die, int Add, bool Passable);

/// <summary>
/// The outdoor first monster type of a field (<c>_e_on_*</c>): rand(Die) − 1 + AreaLevelTimes × area level + Add; then with
/// rand(100) &lt; OverrideBelow it becomes rand(OverrideDie) − 1 + OverrideAdd.
/// </summary>
public sealed record P4MonsterPick(int Die, int Add, int AreaLevelTimes, int OverrideBelow, int OverrideDie, int OverrideAdd);

/// <summary>A battlefield (<c>_e_on_*</c>): the outdoor terrain types that use it, its obstacles and the monster pick.</summary>
public sealed record P4Battlefield(
    string Name, IReadOnlyList<int> TerrainTypes, IReadOnlyList<P4ObstacleRoll> Obstacles, P4MonsterPick Monster);

/// <summary>The field's fixed tables (<c>rule-tables.json</c> <c>combat.battle</c>, <c>combat.md</c> §1, §7, §8.1).</summary>
public sealed record P4BattleTables(
    IReadOnlyList<P4Battlefield> Fields,
    int ObstacleSquareDie,
    int ObstacleSquareAdd,
    IReadOnlyList<int> PlacementColumns,
    IReadOnlyList<int> EdgeSquares,
    IReadOnlyList<(int Square, int Edge)> SideExits,
    int MaxGroups,
    int WakeMonsterBelow,
    int MonsterFleeBelowPercent,
    IReadOnlyDictionary<int, int> CriticalByItem,
    IReadOnlyList<int> BowClassBonus,
    IReadOnlyList<int> ThrownClassBonus,
    IReadOnlyList<int> Protection,
    IReadOnlyList<int> Speed,
    IReadOnlyList<int> Strength,
    IReadOnlyList<int> NinjaStride,
    int TreasureDungeonExtraBelow,
    IReadOnlyList<int> TreasureExtraItems,
    IReadOnlyDictionary<int, int>? EventMonsterFlags = null)
{
    /// <summary>
    /// <c>_monevf</c>: the story flag an event fight sets when won (first <c>DATM</c> type → flag), or null for an ordinary
    /// fight. Its monsters never flee (<c>_b_chkflee</c>, <c>_evmon</c> ≥ 0) [C].
    /// </summary>
    public int? EventFlag(int firstType) =>
        EventMonsterFlags is not null && EventMonsterFlags.TryGetValue(firstType, out var f) ? f : null;

    /// <summary>The field outdoor terrain type <paramref name="type"/> uses, or null (no <c>_e_on_*</c> call).</summary>
    public P4Battlefield? FieldFor(int type) => Fields.FirstOrDefault(f => f.TerrainTypes.Contains(type));
}

/// <summary>
/// One <c>ELMS</c> record (<c>elementals.json</c>, <c>FORMAT.md</c> §6.2): what Summon Elemental loads into party slot 6.
/// Weapon / shield / armour values are stored in the record (+6B, +B6, +B7; it wears no items); +3B is its melee bonus.
/// </summary>
public sealed record P4ElementalTemplate(
    string Name, int Level, int HpMax, int MpMax, IReadOnlyList<int> Abilities, int Attack, int Parry, int WeaponValue,
    int MeleeBonus, int ShieldValue, int ArmourValue, int Movement, IReadOnlyList<int> KnownSpells,
    IReadOnlyList<int> Strategy);

/// <summary>Everything a IV battle reads from the data besides <see cref="P4Tables"/>: monsters, items, field tables, elementals.</summary>
public sealed record P4BattleData(
    IReadOnlyList<P4MonsterType> Monsters, IReadOnlyDictionary<int, P4ItemStats> Items, P4BattleTables Tables,
    IReadOnlyList<P4ElementalTemplate>? Elementals = null)
{
    public P4ItemStats? Item(int id) => Items.TryGetValue(id, out var s) ? s : null;
}
