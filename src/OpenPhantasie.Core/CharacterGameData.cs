namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie I-only character fields (Phase 09 T1.1): the runes and the magical-pool product the Phantasie I campaign
/// reads, plus the DOS GUILD.DAT round-trip words the remaster keeps but never uses. Other games leave this at its
/// empty defaults. Byte offsets live in the Phantasie I converter (<c>GuildCodec</c>), not here.
/// </summary>
public sealed class Phantasie1CharacterData
{
    /// <summary>The five runes (AIR, EARTH, FIRE, WATER, GOD).</summary>
    public const int RuneCount = 5;

    /// <summary>Rune flags [C] (endgame.md §2.2): 0/1, index <see cref="Rune"/>.</summary>
    public int[] Runes { get; set; } = new int[RuneCount];

    /// <summary>
    /// Magical-pool product (script field id 55) [C] (s39). Creation writes 1 (OV1 <c>0x0cbe</c>); the wilderness pool
    /// (OV2 <c>0x656b</c>) skips a member whose value is divisible by <c>k = 2·(tile % 10) + 1</c> and otherwise
    /// multiplies it by <c>k</c> (16-bit), so each pool kind works once per character. DNG5 event 20 tests
    /// <c>≥ 100</c>. No other reader or writer in COM / OV1 / OV2.
    /// </summary>
    public int Word90 { get; set; }

    /// <summary>
    /// Unused DOS word [C]: no reader or writer in COM/OV1/OV2; the only path would be script field id 19, which nothing
    /// passes. 0 in every fixture. Kept for round-trip.
    /// </summary>
    public int Word26 { get; set; }

    /// <summary>
    /// Per-record party-order stamp [C structure]. Zero until the character joins an active party; town Exit
    /// (OV1 0x32eb) writes <c>2·slot + 12</c> (fixtures: 12, 14, … 22 for positions 1..6). Write-only in DOS: nothing
    /// reads it, and it does not change with game time (it is not the age clock). Kept for round-trip.
    /// </summary>
    public int PartyOrderStamp { get; set; }

    /// <summary>
    /// P1.4 raw-record carry: the 282-byte GUILD.DAT record this character was imported from (null for remaster-made
    /// characters). Opaque to Core; the DOS exporter starts from these bytes so unknown/stale bytes (name padding, odd
    /// status/flag values, empty spell slots) round-trip unchanged.
    /// </summary>
    public byte[]? DosRecord { get; set; }

    /// <summary>
    /// <see cref="Character.Lifespan"/> as it was right after the DOS import (D-003 band). While unchanged, the exporter
    /// writes the imported DOS lifespan word back instead of converting the remaster lifespan (P1.4 decision 4).
    /// </summary>
    public int? DosRecordLifespan { get; set; }

    /// <summary>Script field id of the first rune (<c>43 + </c><see cref="Rune"/>, OV2 field map <c>0x1ba5</c>).</summary>
    public const int FirstRuneField = 43;

    /// <summary>Script field id of <see cref="Word90"/>, the magical-pool product.</summary>
    public const int PoolProductField = 55;

    /// <summary>Script field id of <see cref="Word26"/> (read-only: nothing writes it).</summary>
    public const int Word26Field = 19;

    /// <summary>
    /// Read one of I's own words by event-script field id (OV2 <c>0x1ba5</c>): 19 = <see cref="Word26"/>, 43..47 = the
    /// runes, 55 = <see cref="Word90"/>. False for any other id (the shared fields are the script's).
    /// </summary>
    public bool TryGetScriptField(int id, out int value)
    {
        switch (id)
        {
            case Word26Field: value = Word26; return true;
            case >= FirstRuneField and < FirstRuneField + RuneCount: value = Runes[id - FirstRuneField]; return true;
            case PoolProductField: value = Word90; return true;
            default: value = 0; return false;
        }
    }

    /// <summary>Write a rune (43..47) or the pool product (55) by script field id; false (no write) for any other id.</summary>
    public bool TrySetScriptField(int id, int value)
    {
        switch (id)
        {
            case >= FirstRuneField and < FirstRuneField + RuneCount: Runes[id - FirstRuneField] = value; return true;
            case PoolProductField: Word90 = value; return true;
            default: return false;
        }
    }

    /// <summary>Deep copy (arrays and the carried record are not shared).</summary>
    public Phantasie1CharacterData Clone() => new()
    {
        Runes = (int[])Runes.Clone(),
        Word90 = Word90,
        Word26 = Word26,
        PartyOrderStamp = PartyOrderStamp,
        DosRecord = DosRecord is null ? null : (byte[])DosRecord.Clone(),
        DosRecordLifespan = DosRecordLifespan,
    };
}

/// <summary>Where a Phantasie II character comes from (<c>@284</c>, ST <c>endgame.md</c> §9). Text only: no rule reads it.</summary>
public enum Phantasie2Origin
{
    /// <summary>0: transferred from Phantasie I.</summary>
    Gelnor = 0,

    /// <summary>1: made in a Ferronrah town (1–6).</summary>
    Ferronrah = 1,

    /// <summary>2: made in a Netherworld town (7–10).</summary>
    Netherworld = 2,
}

/// <summary>
/// The 36 bytes Phantasie II adds to I's record (ST <c>GUILD.DAT</c> @282–317, <c>endgame.md</c> §1): the quest SCORE, the
/// origin, which of Pluto's eight minions were killed with this member in the party, and the eight beast runes. Null on a
/// character that never played II.
/// </summary>
public sealed class Phantasie2CharacterData
{
    public const int MinionCount = 8;

    /// <summary><c>@282</c>: quest progress 0 → ≤ 100 (Mystic) → 200 (orb destroyed) → 250 (wand of Filmon used).</summary>
    public int QuestScore { get; set; }

    /// <summary><c>@284</c> ORIGIN.</summary>
    public Phantasie2Origin Origin { get; set; }

    /// <summary><c>@286 + 2(k − 1)</c>: minion k (1..8, index k − 1) was killed while this member was in the party.</summary>
    public bool[] MinionKilled { get; set; } = new bool[MinionCount];

    /// <summary><c>@302 + 2(k − 1)</c>: beast rune k (index k − 1), gained by EXAMINE-ing amulet k.</summary>
    public bool[] BeastRunes { get; set; } = new bool[MinionCount];

    public Phantasie2CharacterData Clone() => new()
    {
        QuestScore = QuestScore,
        Origin = Origin,
        MinionKilled = (bool[])MinionKilled.Clone(),
        BeastRunes = (bool[])BeastRunes.Clone(),
    };
}

/// <summary>
/// Optional parentage slot for games whose characters have parents (Phantasie IV's two ancestors). In I–III it is filled
/// only by the T5 lineage option (<see cref="LineageRules"/>, <c>ancestry.md</c> section 13): ancestor 1, then ancestor 2.
/// References only; abilities are read once, at creation.
/// </summary>
/// <param name="Parents">Parent references in source order; may be empty.</param>
public sealed record Lineage(IReadOnlyList<ParentRef> Parents);

/// <summary>
/// One parent reference. Ids are game-scoped <see cref="ContentId"/> strings of the ancestor's own game (<c>p4:0</c> =
/// IV's race 0, Human; <c>p1:3</c> = I's class 3, Fighter), not Phantasie I's <see cref="Race"/> /
/// <see cref="CharacterClass"/> enums. Null = unknown.
/// </summary>
/// <param name="Name">The ancestor's name for a person from the Hall list; null for a new ancestor (IV keeps none).</param>
public sealed record ParentRef(string? RaceId, string? ClassId, string? Name = null);

/// <summary>
/// Optional body-area condition for games with per-area injury (Phantasie III). The area set and the state set are
/// still open questions (4 vs 6 areas; state names), so both are data ids, not enums. No display text lives here.
/// Phantasie I never fills it.
/// </summary>
/// <param name="Areas">Body-area id → condition-state id.</param>
public sealed record BodyCondition(IReadOnlyDictionary<string, string> Areas);

/// <summary>
/// One deed in a character's record (Phase 09, user s72): what the character took part in, as data ids only. Phantasie
/// IV's Hall of Glory builds biography lines from such deeds ("fought …", "freed … from the curse"); the mapping onto
/// those lines and their wording waits for IV's record layout (T6.2/T6.3). No display text lives here.
/// </summary>
/// <param name="KindId">What happened, e.g. <see cref="Deed.Slew"/>.</param>
/// <param name="Subject">Who or what it happened to (e.g. a monster id); null = none.</param>
public sealed record Deed(string KindId, ContentId? Subject)
{
    /// <summary>Was in the party that killed <see cref="Subject"/> (I: the Black Lord).</summary>
    public const string Slew = "slew";

    /// <summary>
    /// Was in the party that destroyed <see cref="Subject"/> (II: Nikademus's orb, written by II's ending, Filmon's ceremony;
    /// remaster record, s149). IV's Hall reads it as "completed Phantasie II" (<c>ancestry.md</c> section 8.4).
    /// </summary>
    public const string Destroyed = "destroyed";
}
