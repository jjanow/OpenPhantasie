namespace OpenPhantasie.Core;

/// <summary>
/// Primary attributes for a character. Phase 3 GUILD layout uses Luck, not Wisdom.
/// </summary>
public record CharacterStats(int Str, int Int, int Dex, int Con, int Cha, int Luck);

/// <summary>
/// Character class ids matching PHANTASI.COM name table order.
/// </summary>
public enum CharacterClass : byte
{
    Thief = 0,
    Monk = 1,
    Ranger = 2,
    Fighter = 3,
    Priest = 4,
    Wizard = 5,
}

/// <summary>
/// Race ids 0–14 matching PHANTASI.COM name table order.
/// </summary>
public enum Race : byte
{
    Human = 0,
    Dwarf = 1,
    Elf = 2,
    Halfling = 3,
    Gnome = 4,
    Pixie = 5,
    Sprite = 6,
    Ogre = 7,
    Gnoll = 8,
    Troll = 9,
    Kobold = 10,
    Orc = 11,
    Goblin = 12,
    LizardMan = 13, // DOS name: LIZ.MAN
    Minotaur = 14,
    /// <summary>
    /// Not creatable: DOS race index 15 (race table PHANTASI.COM 0x6d77). A character
    /// turns undead via OV1 0x113, which sets the displayed race to 15 and leaves the
    /// status OK — so UNDEAD is a race, not a status (PLAN H3/H4, s10).
    /// </summary>
    Undead = 15,
}

/// <summary>
/// Character status. Values are the DOS status-word values [C] (status table PHANTASI.COM 0x6dfe;
/// DOSBox s10); the DOS converter (GuildCodec) maps them. DOS has no other status:
/// UNDEAD is a <see cref="Race"/>, and "DESTROYED" (OV1 0x1ef) removes the character
/// rather than storing a status. SLEEP never outlives a fight: the end of combat (OV2 <c>0x3fbb</c> → <c>0x3eff</c>)
/// and the next prep (<c>0x3263</c>) set every non-DEAD member to OK [C] (s31).
/// </summary>
public enum CharacterStatus : byte
{
    Dead = 0,
    Ok = 1,
    Sleep = 2,

    /// <summary>
    /// Phantasie III only (record <c>+0x2c</c> = 3, <c>body-areas.md</c> §3): head or torso Broken. The member cannot act,
    /// train or learn spells. I and II never set it.
    /// </summary>
    Out = 3,

    /// <summary>
    /// Phantasie III only (record <c>+0x2c</c> = 4): a leg Broken or Gone. The member still fights (melee −40, no bow).
    /// I and II never set it.
    /// </summary>
    Down = 4,
}

/// <summary>
/// Eight skills printed on the character sheet.
/// </summary>
public record CharacterSkills(
    int Attack,
    int Parry,
    int FindItem,
    int SpotTrap,
    int DisarmTrap,
    int Listen,
    int PickLock,
    int Swim)
{
    /// <summary>Skill slots in record order, including the two unused slots (between Listen/PickLock and PickLock/Swim).</summary>
    public const int RecordSlotCount = 10;

    /// <summary>The ten record-order skill slots; the two unused slots read as 0.</summary>
    public int[] ToRecordSlots() =>
        [Attack, Parry, FindItem, SpotTrap, DisarmTrap, Listen, 0, PickLock, 0, Swim];

    public static CharacterSkills FromRecordSlots(IReadOnlyList<int> v) =>
        new(v[0], v[1], v[2], v[3], v[4], v[5], v[7], v[9]);
}
