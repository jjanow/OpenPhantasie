using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Phantasie I converter between DOS GUILD.DAT (22 × 282-byte records, save-file-format.md) and the remaster's
/// game-neutral <see cref="Character"/>. All GUILD byte offsets live here (<see cref="Field"/>); Core knows none of
/// them (Phase 09 T1.1). Phantasie I-only words go to <see cref="Character.Phantasie1"/>; the slots other games use
/// (<see cref="Character.Lineage"/>, <see cref="Character.BodyCondition"/>) are left null on import and never written.
/// Remaster primary save format is JSON (<see cref="WriteJson"/>); binary is for importing original DOS saves and
/// fixture round-trips.
/// </summary>
public static class GuildCodec
{
    /// <summary>One GUILD.DAT character record: 282 bytes (save-file-format.md). Owned here, not by Core (T1.1).</summary>
    public const int RecordSize = 282;
    /// <summary>GUILD.DAT holds 22 records (a DOS format fact; the remaster roster size is <see cref="RulesProfile.GuildSlotCount"/>).</summary>
    public const int SlotCount = 22;
    public const int FileSize = RecordSize * SlotCount;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new ContentIdJsonConverter() },
    };

    /// <summary>
    /// Read a DOS GUILD.DAT. <paramref name="tables"/> supplies the D-003 lifespan bands that
    /// replace each record's DOS lifespan <c>@252</c> (user decision s17).
    /// </summary>
    public static IReadOnlyList<Character?> ReadBinary(byte[] data, CharacterCreationTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (data.Length < FileSize)
            throw new InvalidDataException($"GUILD.DAT expected {FileSize} bytes, got {data.Length}.");

        var slots = new Character?[SlotCount];
        for (var i = 0; i < SlotCount; i++)
        {
            var slice = data.AsSpan(i * RecordSize, RecordSize);
            slots[i] = ParseRecord(slice, tables);
        }

        return slots;
    }

    public static IReadOnlyList<Character?> ReadBinaryFile(string path, CharacterCreationTables tables) =>
        ReadBinary(File.ReadAllBytes(path), tables);

    public static byte[] WriteBinary(IReadOnlyList<Character?> slots, ICollection<DosSaveWarning>? warnings = null)
    {
        if (slots.Count > SlotCount)
            throw new ArgumentException($"At most {SlotCount} guild slots.", nameof(slots));

        var data = new byte[FileSize];
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] is { } ch)
                WriteRecord(ch, data.AsSpan(i * RecordSize, RecordSize), warnings, i + 1);
        }

        return data;
    }

    public static void WriteBinaryFile(string path, IReadOnlyList<Character?> slots) =>
        File.WriteAllBytes(path, WriteBinary(slots));

    /// <summary>
    /// Remaster character JSON. Item ids (worn and carried) are written game-prefixed (<c>"p1:42"</c>, T1.2) for
    /// <paramref name="game"/>; the spell flag words stay positional (index = spell number of that game).
    /// </summary>
    public static string WriteJson(IEnumerable<Character> characters, GameId game = GameId.P1) =>
        JsonSerializer.Serialize(characters.Select(c => ToDto(c, game)).ToList(), JsonOptions);

    /// <summary>Reads <see cref="WriteJson"/> output; an item id of another game than <paramref name="game"/> is refused.</summary>
    /// <exception cref="InvalidDataException">An id names another game.</exception>
    public static IReadOnlyList<Character> ReadJson(string json, GameId game = GameId.P1)
    {
        var list = JsonSerializer.Deserialize<List<GuildJsonDto>>(json, JsonOptions)
                   ?? throw new InvalidDataException("Invalid guild JSON.");
        return list.Select(d => FromDto(d, game)).ToList();
    }

    /// <summary>[C] Record offset of a spell's known-flag word: <c>@96 + 2·DOS slot</c> (DIVINE I → <c>@210</c>, s41).</summary>
    public static int SpellFlagOffset(int spellIndex) => Field.SpellFlags + 2 * SpellId.DosSlot(spellIndex);

    public static Character? ParseRecord(ReadOnlySpan<byte> r, CharacterCreationTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (r.Length < RecordSize)
            throw new ArgumentException("Record too short.", nameof(r));

        var name = DecodeName(r.Slice(Field.Name, Field.NameLength));
        if (string.IsNullOrEmpty(name))
            return null;
        // [C] @92 ≠ 0 marks an occupied slot (OV1 0x0C59). A DESTROYED member's record keeps its name and stats; the next
        // town entry (OV1 0x197e → 0x1bae) zeroes only @218/@24/@92, so that slot is free (endgame.md §DESTROYED).
        if (ReadU16(r, Field.RosterOrdinal) == 0)
            return null;

        // [C] (s41) flag words are indexed by DOS slot: @96 + 2·slot, DIVINE I = slot 57 (@210); empty slots 54–56/58/59
        // stay in the raw record carry.
        var spells = new int[CharacterCreation.SpellCount];
        for (var i = 0; i < spells.Length; i++)
            spells[i] = ReadU16(r, SpellFlagOffset(i));

        var character = new Character
        {
            Name = name,
            Race = (Race)ReadU16(r, Field.Race),
            BirthRace = (Race)r[Field.BirthRace],
            Status = DecodeStatus(ReadU16(r, Field.Status)),
            Class = (CharacterClass)r[Field.Class],
            Level = ReadU16(r, Field.Level),
            InParty = ReadU16(r, Field.InParty) != 0,
            Stats = new CharacterStats(
                ReadU16(r, Field.Str), ReadU16(r, Field.Int), ReadU16(r, Field.Dex),
                ReadU16(r, Field.Con), ReadU16(r, Field.Cha), ReadU16(r, Field.Luck)),
            Word12 = ReadU16(r, Field.Defense),
            // s11 relabel (save-file-format.md): @14 = MP ceiling, @16 = MP max, @18 = MP cur.
            MpCeiling = ReadU16(r, Field.MpCeiling),
            MagicMax = ReadU16(r, Field.MpMax),
            MagicCur = ReadU16(r, Field.MpCur),
            HpMax = ReadU16(r, Field.HpMax),
            HpCur = ReadU16(r, Field.HpCur),
            WeaponDamageA = ReadU16(r, Field.WeaponDamageA),
            Skills = new CharacterSkills(
                ReadU16(r, Field.Attack), ReadU16(r, Field.Parry), ReadU16(r, Field.FindItem), ReadU16(r, Field.SpotTrap),
                ReadU16(r, Field.DisarmTrap), ReadU16(r, Field.Listen), ReadU16(r, Field.PickLock), ReadU16(r, Field.Swim)),
            Score = ReadU16(r, Field.Score),
            RosterOrdinal = ReadU16(r, Field.RosterOrdinal),
            WornShield = ReadU16(r, Field.WornShield),
            WornArmor = ReadU16(r, Field.WornArmor),
            WornWeapon = ReadU16(r, Field.WornWeapon),
            WeaponDamageB = ReadU16(r, Field.WeaponDamageB),
            ShieldSoak = ReadU16(r, Field.ShieldSoak),
            ArmorSoak = ReadU16(r, Field.ArmorSoak),
            GoldInBank = ReadU16(r, Field.BankGold),
            CastSinceRest = ReadU16(r, Field.CastSinceRest) != 0,
            SkillBonuses = ReadSkillBonuses(r),
            CurrentTown = TownFromDos(ReadU16(r, Field.Town)),
            HasAged = ReadU16(r, Field.HasAged) != 0,
            AgeClock = ReadU16(r, Field.AgeClock),
            Experience = ReadI32(r, Field.Experience),
            SpellsKnown = spells,
            Phantasie1 =
            {
                Runes = ReadRunes(r),
                Word90 = ReadU16(r, Field.Word90),
                Word26 = ReadU16(r, Field.Word26),
                PartyOrderStamp = ReadU16(r, Field.PartyOrderStamp),
            },
            // Lineage and BodyCondition stay null: Phantasie I has neither.
        };

        // DOS @252 is not kept: imported characters get the remaster band (D-003, user s17).
        character.Lifespan = CharacterCreation.LifespanForImported(character, tables);
        // P1.4 raw-record carry: the exporter overlays known fields on these bytes.
        character.Phantasie1.DosRecord = r.Slice(0, RecordSize).ToArray();
        character.Phantasie1.DosRecordLifespan = character.Lifespan;

        // Carried items @222..@238, 9 slots, worn items included (P0.5 s19).
        for (var off = Field.Items; off < Field.Items + 2 * Character.InventorySlots; off += 2)
        {
            character.InventoryItemIds.Add(ReadU16(r, off)); // 0 = empty slot, kept in place (s58)
        }
        CarriedItems.TrimTrailingEmpty(character.InventoryItemIds);

        return character;
    }

    /// <summary>
    /// Write one 282-byte record. P1.4: starts from the carried import bytes (<see cref="Phantasie1CharacterData.DosRecord"/>, zeros
    /// for a remaster-made character) and writes the known fields over them, so unknown bytes survive. Fields Core
    /// decodes lossily (name padding, status/flag words, inventory holes, <c>@252</c>) keep their carried bytes while
    /// the decoded value is unchanged. Values that don't fit their DOS field are clamped and added to
    /// <paramref name="warnings"/>.
    /// </summary>
    /// <param name="slotNumber">1-based GUILD.DAT slot, written to <c>@92</c> when the character has no roster ordinal.</param>
    public static void WriteRecord(Character ch, Span<byte> r, ICollection<DosSaveWarning>? warnings = null, int slotNumber = 1)
    {
        ArgumentNullException.ThrowIfNull(ch);
        var p1 = ch.Phantasie1;
        var raw = p1.DosRecord is { Length: RecordSize } carried ? carried : null;
        if (raw is null)
            r.Clear();
        else
            raw.CopyTo(r);

        var who = string.IsNullOrEmpty(ch.Name) ? GameText.S("core.dossave.unnamed") : ch.Name;
        void U16(Span<byte> rec, int off, long value, string field) =>
            DosFieldWriter.U16(rec, off, value, "GUILD.DAT", $"{who} {field}", warnings);

        U16(r, Field.Str, ch.Stats.Str, "@0 STR");
        U16(r, Field.Int, ch.Stats.Int, "@2 INT");
        U16(r, Field.Dex, ch.Stats.Dex, "@4 DEX");
        U16(r, Field.Con, ch.Stats.Con, "@6 CON");
        U16(r, Field.Cha, ch.Stats.Cha, "@8 CHA");
        U16(r, Field.Luck, ch.Stats.Luck, "@10 luck");
        U16(r, Field.Defense, ch.Word12, "@12 defense");
        U16(r, Field.MpCeiling, ch.MpCeiling, "@14 MP ceiling");
        U16(r, Field.MpMax, ch.MagicMax, "@16 MP max");
        U16(r, Field.MpCur, ch.MagicCur, "@18 MP");
        U16(r, Field.HpMax, ch.HpMax, "@20 HP max");
        U16(r, Field.HpCur, ch.HpCur, "@22 HP");
        if (raw is null || DecodeStatus(ReadU16(r, Field.Status)) != ch.Status)
            WriteU16(r, Field.Status, EncodeStatus(ch.Status));
        U16(r, Field.Word26, p1.Word26, "@26");
        U16(r, Field.WeaponDamageA, ch.WeaponDamageA, "@28 weapon damage A");
        U16(r, Field.Attack, ch.Skills.Attack, "@30 attack");
        U16(r, Field.Parry, ch.Skills.Parry, "@32 parry");
        U16(r, Field.FindItem, ch.Skills.FindItem, "@34 find item");
        U16(r, Field.SpotTrap, ch.Skills.SpotTrap, "@36 spot trap");
        U16(r, Field.DisarmTrap, ch.Skills.DisarmTrap, "@38 disarm trap");
        U16(r, Field.Listen, ch.Skills.Listen, "@40 listen");
        U16(r, Field.PickLock, ch.Skills.PickLock, "@44 pick lock");
        U16(r, Field.Swim, ch.Skills.Swim, "@48 swim");
        for (var i = 0; i < CharacterSkills.RecordSlotCount && i < ch.SkillBonuses.Length; i++)
            U16(r, Field.SkillBonuses + 2 * i, ch.SkillBonuses[i], $"@{Field.SkillBonuses + 2 * i} skill bonus");
        U16(r, Field.Score, ch.Score, "@70 score");
        for (var k = 0; k < Phantasie1CharacterData.RuneCount && k < p1.Runes.Length; k++)
            U16(r, Field.Runes + 2 * k, p1.Runes[k], $"@{Field.Runes + 2 * k} rune");
        U16(r, Field.Word90, p1.Word90, "@90");
        // @92 must be non-zero for DOS to see the slot (OV1 0x0C59) and is the 1-based slot id the DESTROYED free list
        // hands to 0x1bae; a character made in the remaster has none yet, so it gets its slot number.
        U16(r, Field.RosterOrdinal, ch.RosterOrdinal != 0 ? ch.RosterOrdinal : slotNumber, "@92 roster ordinal");
        U16(r, Field.WornShield, ch.WornShield, "@82 worn shield");
        U16(r, Field.WornArmor, ch.WornArmor, "@84 worn armor");
        U16(r, Field.WornWeapon, ch.WornWeapon, "@86 worn weapon");
        U16(r, Field.Level, ch.Level, "@88 level");
        WriteFlag(r, Field.CastSinceRest, ch.CastSinceRest, raw is not null);
        U16(r, Field.WeaponDamageB, ch.WeaponDamageB, "@240 weapon points");
        U16(r, Field.ShieldSoak, ch.ShieldSoak, "@242 shield");
        U16(r, Field.ArmorSoak, ch.ArmorSoak, "@244 armor");

        for (var i = 0; i < ch.SpellsKnown.Length && i < CharacterCreation.SpellCount; i++)
            U16(r, SpellFlagOffset(i), ch.SpellsKnown[i], $"spell {i}");

        r[Field.BirthRace] = (byte)ch.BirthRace;
        WriteFlag(r, Field.InParty, ch.InParty, raw is not null);
        r[Field.Class] = (byte)ch.Class;
        // @256 is the race the game displays (sheet/Inspect, PHANTASI.COM 0x2f03) and
        // the creation duplicate-race guard reads (OV1 0x0C5F). Equal to @216 until
        // the character turns undead, when OV1 0x113 sets it to 15 = UNDEAD [C].
        if (raw is null || (Race)ReadU16(r, Field.Race) != ch.Race)
            WriteU16(r, Field.Race, (byte)ch.Race);

        WriteInventory(ch, r, warnings);

        // @252: the imported DOS value goes back while the D-003 lifespan is unchanged (P1.4); otherwise the
        // remaster lifespan in DOS steps (lossy: DOS compares @264 >> 8 with it).
        if (raw is null || p1.DosRecordLifespan != ch.Lifespan)
            U16(r, Field.Lifespan, LifespanToDos(ch.Lifespan), "@252 lifespan");
        U16(r, Field.Town, TownToDos(ch.CurrentTown), "@254 town");
        WriteFlag(r, Field.HasAged, ch.HasAged, raw is not null);
        U16(r, Field.PartyOrderStamp, p1.PartyOrderStamp, "@260 party stamp");
        U16(r, Field.BankGold, ch.GoldInBank, "@262 gold in bank");
        // u16 in DOS: a remaster elf (~78 000 units, D-003) can't be stored — clamped and listed (P1.4 decision 7).
        U16(r, Field.AgeClock, ch.AgeClock, "@264 age clock");
        if (ch.Experience is < int.MinValue or > int.MaxValue)
            warnings?.Add(new DosSaveWarning(DosSaveWarningKind.Clamp, "GUILD.DAT",
                GameText.F("core.dossave.x_experience_x_32_bits", who, ch.Experience)));
        WriteI32(r, Field.Experience, ch.Experience);

        if (raw is null || DecodeName(r.Slice(Field.Name, Field.NameLength)) != ch.Name)
        {
            if (ch.Name.Length > Field.NameLength)
                warnings?.Add(new DosSaveWarning(DosSaveWarningKind.Clamp, "GUILD.DAT",
                    GameText.F("core.dossave.name_x_longer_than_x_cut_to_x", ch.Name, Field.NameLength, ch.Name[..Field.NameLength])));
            var nameBytes = Encoding.ASCII.GetBytes(ch.Name.PadRight(Field.NameLength).Substring(0, Field.NameLength));
            nameBytes.CopyTo(r.Slice(Field.Name, Field.NameLength));
        }
    }

    /// <summary>Boolean word: a carried nonzero value (e.g. <c>@218 = 3</c>) is kept while the flag is unchanged.</summary>
    private static void WriteFlag(Span<byte> r, int offset, bool value, bool carried)
    {
        if (carried && (ReadU16(r, offset) != 0) == value)
            return;
        WriteU16(r, offset, value ? 1 : 0);
    }

    /// <summary>
    /// Carried items <c>@222..@238</c>: slot i = list entry i (0 = empty), so a drunk potion's gap round-trips (s58).
    /// </summary>
    private static void WriteInventory(Character ch, Span<byte> r, ICollection<DosSaveWarning>? warnings)
    {
        if (ch.InventoryItemIds.Count > Character.InventorySlots)
            warnings?.Add(new DosSaveWarning(DosSaveWarningKind.Clamp, "GUILD.DAT",
                GameText.F("core.dossave.x_carries_x_items_dos_holds_x", ch.Name, ch.InventoryItemIds.Count, Character.InventorySlots)));
        for (var i = 0; i < Character.InventorySlots; i++)
        {
            var id = i < ch.InventoryItemIds.Count ? ch.InventoryItemIds[i] : 0;
            DosFieldWriter.U16(r, Field.Items + i * 2, id, "GUILD.DAT", GameText.F("core.dossave.x_item_slot_x", ch.Name, i), warnings);
        }
    }

    private static int[] ReadRunes(ReadOnlySpan<byte> r)
    {
        var runes = new int[Phantasie1CharacterData.RuneCount];
        for (var k = 0; k < runes.Length; k++)
            runes[k] = ReadU16(r, Field.Runes + 2 * k);
        return runes;
    }

    /// <summary>
    /// GUILD.DAT record byte offsets [C] (save-file-format.md; character fields' provenance is on
    /// <see cref="Character"/> / <see cref="Phantasie1CharacterData"/>). All words are little-endian u16 unless noted.
    /// Bytes not listed (<c>@42</c>, <c>@46</c>, empty spell slots, <c>@246..@251</c> fight-start copies, name padding)
    /// are kept only through the raw-record carry.
    /// </summary>
    public static class Field
    {
        public const int Str = 0, Int = 2, Dex = 4, Con = 6, Cha = 8, Luck = 10;
        /// <summary>Defense rating (<see cref="Character.Word12"/>).</summary>
        public const int Defense = 12;
        public const int MpCeiling = 14, MpMax = 16, MpCur = 18, HpMax = 20, HpCur = 22;
        /// <summary>Status word: 0 = DEAD, 1 = OK, 2 = SLEEP.</summary>
        public const int Status = 24;
        /// <summary>Unused word (<see cref="Phantasie1CharacterData.Word26"/>).</summary>
        public const int Word26 = 26;
        public const int WeaponDamageA = 28;
        /// <summary>Skill words @30..@48 in record order; @42 and @46 are unused.</summary>
        public const int Attack = 30, Parry = 32, FindItem = 34, SpotTrap = 36, DisarmTrap = 38, Listen = 40,
            PickLock = 44, Swim = 48;
        /// <summary>Ten skill-bonus words @50..@68, same order as the skill words.</summary>
        public const int SkillBonuses = 50;
        public const int Score = 70;
        /// <summary>Five rune flag words @72..@80 (<see cref="Rune"/> order).</summary>
        public const int Runes = 72;
        public const int WornShield = 82, WornArmor = 84, WornWeapon = 86, Level = 88;
        /// <summary>Magical-pool product (<see cref="Phantasie1CharacterData.Word90"/>).</summary>
        public const int Word90 = 90;
        /// <summary>Roster ordinal; 0 = empty slot.</summary>
        public const int RosterOrdinal = 92;
        public const int CastSinceRest = 94;
        /// <summary>Spell known-flag words, <c>@96 + 2·DOS slot</c> (<see cref="SpellFlagOffset"/>).</summary>
        public const int SpellFlags = 0x60;
        /// <summary>Byte: race chosen at creation.</summary>
        public const int BirthRace = 216;
        public const int InParty = 218;
        /// <summary>Byte: class.</summary>
        public const int Class = 220;
        /// <summary>Nine carried-item words @222..@238.</summary>
        public const int Items = 222;
        public const int WeaponDamageB = 240, ShieldSoak = 242, ArmorSoak = 244;
        /// <summary>DOS lifespan in 256-unit steps (<see cref="DosLifespanStep"/>).</summary>
        public const int Lifespan = 252;
        /// <summary>0-based town index (<see cref="TownFromDos"/>).</summary>
        public const int Town = 254;
        /// <summary>Displayed race (15 = UNDEAD).</summary>
        public const int Race = 256;
        public const int HasAged = 258, PartyOrderStamp = 260, BankGold = 262, AgeClock = 264;
        /// <summary>Signed 32-bit experience @266..@269.</summary>
        public const int Experience = 266;
        /// <summary>ASCII name, space/NUL padded.</summary>
        public const int Name = 270, NameLength = 12;
    }

    private static string DecodeName(ReadOnlySpan<byte> bytes) =>
        Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');

    /// <summary>
    /// <c>@254</c> holds the 0-based town record index [C] (s39, s56: OV1 <c>0x1b57</c> stores the town-entry argument,
    /// the endgame restart enters with <c>[0x64ee] = 0</c>, every fixture party in Pelnor holds 0). Core town ids are 1-based
    /// (Pelnor = 1, <see cref="Aging.ApplyTownEntry"/>), so the codec converts at the boundary.
    /// </summary>
    public static int TownFromDos(int word) => word + 1;

    /// <summary>Core town id → <c>@254</c>; 0 (no town set) is written as 0 = Pelnor.</summary>
    public static int TownToDos(int townId) => Math.Max(0, townId - 1);

    /// <summary>
    /// DOS <c>@252</c> unit: one step = <c>@264 &gt;&gt; 8</c> = 256 age-clock units. Only used on write;
    /// on read the DOS value is replaced by the D-003 band (<see cref="CharacterCreation.LifespanForImported"/>).
    /// </summary>
    public const int DosLifespanStep = 256;

    /// <summary>Core lifespan → DOS <c>@252</c> steps (lossy; used only when the lifespan changed since import).</summary>
    public static int LifespanToDos(int lifespan) =>
        Math.Min(0xffff, lifespan / DosLifespanStep);

    private static int[] ReadSkillBonuses(ReadOnlySpan<byte> r)
    {
        var bonuses = new int[CharacterSkills.RecordSlotCount];
        for (var i = 0; i < bonuses.Length; i++)
            bonuses[i] = ReadU16(r, Field.SkillBonuses + 2 * i);
        return bonuses;
    }

    /// <summary>
    /// GUILD @24 status word. DOS names 0 = DEAD, 1 = OK, 2 = SLEEP [C]
    /// (PHANTASI.COM 0x6dfe table, DOSBox s10). DOS only ever writes 0/1/2 — every write
    /// is a constant (e.g. OV1 0x1bbf and 0x2475 store 0) [C]; other values come only from
    /// hand-edited files and are read as OK.
    /// </summary>
    private static CharacterStatus DecodeStatus(int status) =>
        status is >= 0 and <= 2 ? (CharacterStatus)status : CharacterStatus.Ok;

    private static int EncodeStatus(CharacterStatus status) => (int)status;

    private static int ReadU16(ReadOnlySpan<byte> r, int offset) =>
        r[offset] | (r[offset + 1] << 8);

    private static long ReadI32(ReadOnlySpan<byte> r, int offset) =>
        unchecked((int)(uint)(r[offset]
            | (r[offset + 1] << 8)
            | (r[offset + 2] << 16)
            | (r[offset + 3] << 24)));

    private static void WriteU16(Span<byte> r, int offset, int value)
    {
        r[offset] = (byte)(value & 0xff);
        r[offset + 1] = (byte)((value >> 8) & 0xff);
    }

    private static void WriteI32(Span<byte> r, int offset, long value)
    {
        var v = (int)Math.Clamp(value, int.MinValue, int.MaxValue);
        r[offset] = (byte)(v & 0xff);
        r[offset + 1] = (byte)((v >> 8) & 0xff);
        r[offset + 2] = (byte)((v >> 16) & 0xff);
        r[offset + 3] = (byte)((v >> 24) & 0xff);
    }

    private static GuildJsonDto ToDto(Character ch, GameId game) => new()
    {
        Name = ch.Name,
        Race = ch.Race.ToString(),
        BirthRace = ch.BirthRace.ToString(),
        Class = ch.Class.ToString(),
        Status = ch.Status.ToString(),
        Level = ch.Level,
        Strength = ch.Stats.Str,
        Intelligence = ch.Stats.Int,
        Dexterity = ch.Stats.Dex,
        Constitution = ch.Stats.Con,
        Charisma = ch.Stats.Cha,
        Luck = ch.Stats.Luck,
        MpCeiling = ch.MpCeiling,
        MagicCur = ch.MagicCur,
        MagicMax = ch.MagicMax,
        HpCur = ch.HpCur,
        HpMax = ch.HpMax,
        GoldInBank = ch.GoldInBank,
        Experience = ch.Experience,
        Score = ch.Score,
        WeaponDamage28 = ch.WeaponDamageA,
        WeaponDamageB = ch.WeaponDamageB,
        ShieldSoak = ch.ShieldSoak,
        ArmorSoak = ch.ArmorSoak,
        WornShield = new ContentId(game, ch.WornShield),
        WornArmor = new ContentId(game, ch.WornArmor),
        WornWeapon = new ContentId(game, ch.WornWeapon),
        WornBow = ch.WornBow == 0 ? null : new ContentId(game, ch.WornBow),
        WornThrown = ch.WornThrown == 0 ? null : new ContentId(game, ch.WornThrown),
        RosterOrdinal = ch.RosterOrdinal,
        InParty = ch.InParty,
        AgeClock = ch.AgeClock,
        Lifespan = ch.Lifespan,
        CurrentTown = ch.CurrentTown,
        HasAged = ch.HasAged,
        CastSinceRest = ch.CastSinceRest,
        Word12 = ch.Word12,
        SkillBonuses = ch.SkillBonuses.ToArray(),
        Skills = new GuildSkillsDto
        {
            Attack = ch.Skills.Attack,
            Parry = ch.Skills.Parry,
            FindItem = ch.Skills.FindItem,
            SpotTrap = ch.Skills.SpotTrap,
            DisarmTrap = ch.Skills.DisarmTrap,
            Listen = ch.Skills.Listen,
            PickLock = ch.Skills.PickLock,
            Swim = ch.Skills.Swim,
        },
        TossRock = ch.TossRock,
        TossRockBonus = ch.TossRockBonus,
        SpellsKnown = ch.SpellsKnown.ToArray(),
        InventoryItemIds = ch.InventoryItemIds.Select(i => new ContentId(game, i)).ToList(), // 0 = NOTHING / empty slot
        Phantasie1 = new Phantasie1Dto
        {
            Runes = (int[])ch.Phantasie1.Runes.Clone(),
            Word90 = ch.Phantasie1.Word90,
            Word26 = ch.Phantasie1.Word26,
            PartyOrderStamp = ch.Phantasie1.PartyOrderStamp,
            DosRecord = ch.Phantasie1.DosRecord is null ? null : (byte[])ch.Phantasie1.DosRecord.Clone(),
            DosRecordLifespan = ch.Phantasie1.DosRecordLifespan,
        },
        Phantasie2 = ch.Phantasie2 is not { } p2
            ? null
            : new Phantasie2Dto
            {
                QuestScore = p2.QuestScore,
                Origin = p2.Origin.ToString().ToLowerInvariant(),
                MinionKilled = (bool[])p2.MinionKilled.Clone(),
                BeastRunes = (bool[])p2.BeastRunes.Clone(),
            },
        Phantasie3 = ch.Phantasie3 is not { } p3
            ? null
            : new Phantasie3Dto
            {
                SocialClass = p3.SocialClass,
                Rank = (int)p3.Rank,
                Origin = p3.Origin.ToString().ToLowerInvariant(),
                QuestBits = p3.QuestBits,
                DeedBits = p3.DeedBits,
                Phantasie1Score = p3.Phantasie1Score,
                Phantasie2Score = p3.Phantasie2Score,
                DivineAid = p3.DivineAid,
                FireBow = p3.FireBow,
                SavedAction = p3.SavedAction,
                SavedCast = p3.SavedCast,
                ExperienceShares = p3.ExperienceShares,
                PlaneTownsVisited = p3.PlaneTownsVisited == 0 ? null : p3.PlaneTownsVisited,
            },
        Lineage = ch.Lineage is null
            ? null
            : new LineageDto
            {
                Parents = ch.Lineage.Parents.Select(p => new ParentDto { RaceId = p.RaceId, ClassId = p.ClassId, Name = p.Name }).ToList(),
            },
        BodyCondition = ch.BodyCondition is null ? null : new Dictionary<string, string>(ch.BodyCondition.Areas),
        Deeds = ch.Deeds?.Select(x => new DeedDto { Kind = x.KindId, Subject = x.Subject }).ToList(),
    };

    private static Character FromDto(GuildJsonDto d, GameId game)
    {
        if (!Enum.TryParse<Race>(d.Race, ignoreCase: true, out var race))
            race = Race.Human;
        if (!Enum.TryParse<CharacterClass>(d.Class, ignoreCase: true, out var cls))
            cls = CharacterClass.Fighter;
        if (!Enum.TryParse<Race>(d.BirthRace, ignoreCase: true, out var birthRace))
            birthRace = race;
        if (!Enum.TryParse<CharacterStatus>(d.Status, ignoreCase: true, out var status))
            status = CharacterStatus.Ok;

        var ch = new Character
        {
            Name = d.Name ?? "",
            Race = race,
            BirthRace = birthRace,
            Class = cls,
            Status = status,
            Level = d.Level,
            Stats = new CharacterStats(
                d.Strength, d.Intelligence, d.Dexterity,
                d.Constitution, d.Charisma, d.Luck),
            // combatRating = pre-s17 name of the same @14 word.
            MpCeiling = d.MpCeiling ?? d.CombatRating ?? 0,
            MagicCur = d.MagicCur,
            MagicMax = d.MagicMax,
            HpCur = d.HpCur,
            HpMax = d.HpMax,
            GoldInBank = d.GoldInBank,
            Experience = d.Experience,
            Score = d.Score,
            // Saves before s19 named @28 "ScrollCountOrWeaponHit". The old @28 value was mostly weapon points ± scroll
            // buys, so it is dropped: @28 = 0 (user decision s19).
            WeaponDamageA = d.WeaponDamage28 ?? 0,
            WeaponDamageB = d.WeaponDamageB,
            ShieldSoak = d.ShieldSoak,
            ArmorSoak = d.ArmorSoak,
            WornShield = d.WornShield?.LocalIn(game) ?? 0,
            WornArmor = d.WornArmor?.LocalIn(game) ?? 0,
            WornWeapon = d.WornWeapon?.LocalIn(game) ?? 0,
            WornBow = d.WornBow?.LocalIn(game) ?? 0,
            WornThrown = d.WornThrown?.LocalIn(game) ?? 0,
            RosterOrdinal = d.RosterOrdinal,
            InParty = d.InParty,
            AgeClock = d.AgeClock ?? d.BirthStamp ?? 0,
            Lifespan = d.Lifespan,
            CurrentTown = d.CurrentTown,
            HasAged = d.HasAged,
            CastSinceRest = d.CastSinceRest,
            Word12 = d.Word12,
            Skills = new CharacterSkills(
                d.Skills?.Attack ?? 0, d.Skills?.Parry ?? 0, d.Skills?.FindItem ?? 0,
                d.Skills?.SpotTrap ?? 0, d.Skills?.DisarmTrap ?? 0, d.Skills?.Listen ?? 0,
                d.Skills?.PickLock ?? 0, d.Skills?.Swim ?? 0),
            SpellsKnown = d.SpellsKnown is { Length: > 0 }
                ? d.SpellsKnown
                : new int[CharacterCreation.SpellCount],
            TossRock = d.TossRock,
            TossRockBonus = d.TossRockBonus,
        };
        ch.SkillBonuses = d.SkillBonuses is { Length: CharacterSkills.RecordSlotCount }
            ? d.SkillBonuses
            : CharacterCreation.SkillBonusSeeds(ch.Stats); // older saves: creation formula

        if (d.InventoryItemIds is not null)
            ch.InventoryItemIds.AddRange(d.InventoryItemIds.Select(i => i.LocalIn(game)));
        if (d.Phantasie1 is { } p1)
        {
            ch.Phantasie1.Runes = p1.Runes is { Length: Phantasie1CharacterData.RuneCount } runes
                ? (int[])runes.Clone()
                : new int[Phantasie1CharacterData.RuneCount];
            ch.Phantasie1.Word90 = p1.Word90;
            ch.Phantasie1.Word26 = p1.Word26;
            ch.Phantasie1.PartyOrderStamp = p1.PartyOrderStamp;
            if (p1.DosRecord is { Length: RecordSize } rec)
            {
                ch.Phantasie1.DosRecord = rec;
                ch.Phantasie1.DosRecordLifespan = p1.DosRecordLifespan;
            }
        }
        // Pre-s19 saves named @26 "WeaponDamageA" (no WeaponDamage28 key).
        if (d.WeaponDamage28 is null && d.WeaponDamageA is { } oldWord26)
            ch.Phantasie1.Word26 = oldWord26;
        if (d.Phantasie2 is { } p2d)
        {
            static bool[] Flags(bool[]? f) =>
                f is { Length: Phantasie2CharacterData.MinionCount } ? (bool[])f.Clone() : new bool[Phantasie2CharacterData.MinionCount];
            ch.Phantasie2 = new Phantasie2CharacterData
            {
                QuestScore = p2d.QuestScore,
                Origin = Enum.TryParse<Phantasie2Origin>(p2d.Origin, ignoreCase: true, out var o) ? o : Phantasie2Origin.Gelnor,
                MinionKilled = Flags(p2d.MinionKilled),
                BeastRunes = Flags(p2d.BeastRunes),
            };
        }
        if (d.Phantasie3 is { } p3d)
        {
            ch.Phantasie3 = new Phantasie3CharacterData
            {
                SocialClass = p3d.SocialClass,
                Rank = (P3PartyRank)Math.Clamp(p3d.Rank, 0, 2),
                Origin = Enum.TryParse<Phantasie3Origin>(p3d.Origin, ignoreCase: true, out var o3) ? o3 : Phantasie3Origin.Scandor,
                QuestBits = p3d.QuestBits,
                DeedBits = p3d.DeedBits,
                Phantasie1Score = p3d.Phantasie1Score,
                Phantasie2Score = p3d.Phantasie2Score,
                DivineAid = p3d.DivineAid,
                FireBow = p3d.FireBow,
                SavedAction = p3d.SavedAction ?? P3CombatModule.NewCharacterAction,
                SavedCast = p3d.SavedCast,
                ExperienceShares = p3d.ExperienceShares ?? 1,
                PlaneTownsVisited = p3d.PlaneTownsVisited ?? 0,
            };
        }
        if (d.Lineage is { } lineage)
            ch.Lineage = new Lineage((lineage.Parents ?? []).Select(p => new ParentRef(p.RaceId, p.ClassId, p.Name)).ToList());
        if (d.BodyCondition is { } body)
            ch.BodyCondition = new BodyCondition(new Dictionary<string, string>(body));
        if (d.Deeds is { } deeds)
            ch.Deeds = deeds.Select(x => new Deed(x.Kind ?? string.Empty, x.Subject)).ToList();
        return ch;
    }

    private sealed class GuildJsonDto
    {
        public string? Name { get; set; }
        public string? Race { get; set; }
        public string? BirthRace { get; set; }
        public string? Class { get; set; }
        public string? Status { get; set; }
        public int Level { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Charisma { get; set; }
        public int Luck { get; set; }
        public int? MpCeiling { get; set; }
        /// <summary>Old name of <see cref="MpCeiling"/> — read only, for saves written before s17.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CombatRating { get; set; }
        public int MagicCur { get; set; }
        public int MagicMax { get; set; }
        public int HpCur { get; set; }
        public int HpMax { get; set; }
        public int GoldInBank { get; set; }
        public long Experience { get; set; }
        public int Score { get; set; }
        /// <summary>GUILD @28 weapon damage A (<see cref="Character.WeaponDamageA"/>).</summary>
        public int? WeaponDamage28 { get; set; }
        /// <summary>Pre-s19 name of @28 — still recognised, but its value is discarded on load (user s19).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ScrollCountOrWeaponHit { get; set; }
        /// <summary>Pre-s19 name of @26 — read only.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? WeaponDamageA { get; set; }
        public int WeaponDamageB { get; set; }
        public int ShieldSoak { get; set; }
        public int ArmorSoak { get; set; }
        public ContentId? WornShield { get; set; }
        public ContentId? WornArmor { get; set; }
        public ContentId? WornWeapon { get; set; }
        /// <summary>Phantasie III's bow slot (T4); left out while empty, so I / II saves are unchanged.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ContentId? WornBow { get; set; }
        /// <summary>Phantasie IV's thrown-weapon slot (T6); left out while empty, so I–III saves are unchanged.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ContentId? WornThrown { get; set; }
        public int RosterOrdinal { get; set; }
        public bool InParty { get; set; }
        public int? AgeClock { get; set; }
        /// <summary>Old name of <see cref="AgeClock"/> — read only, for saves written before s17.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BirthStamp { get; set; }
        public int Lifespan { get; set; }
        public int CurrentTown { get; set; }
        public bool HasAged { get; set; }
        public bool CastSinceRest { get; set; }
        public int Word12 { get; set; }
        public int[]? SkillBonuses { get; set; }
        public GuildSkillsDto? Skills { get; set; }

        /// <summary>II's TOSS skill and its bonus (<see cref="Character.TossRock"/>); written only when not 0.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TossRock { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TossRockBonus { get; set; }
        public int[]? SpellsKnown { get; set; }
        public List<ContentId>? InventoryItemIds { get; set; }

        /// <summary>Phantasie I-only fields (<see cref="Phantasie1CharacterData"/>).</summary>
        public Phantasie1Dto? Phantasie1 { get; set; }

        /// <summary>Phantasie II's added fields (<see cref="Phantasie2CharacterData"/>); written only when set.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Phantasie2Dto? Phantasie2 { get; set; }

        /// <summary>Phantasie III's added fields (<see cref="Phantasie3CharacterData"/>); written only when set.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Phantasie3Dto? Phantasie3 { get; set; }

        /// <summary>Phantasie IV-style parentage; written only when set (never in Phantasie I).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LineageDto? Lineage { get; set; }

        /// <summary>Phantasie III-style body-area id → state id; written only when set (never in Phantasie I).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? BodyCondition { get; set; }

        /// <summary>The deeds record (user s72); written only when set.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DeedDto>? Deeds { get; set; }
    }

    private sealed class Phantasie1Dto
    {
        /// <summary>Runes @72..@80 (s36).</summary>
        public int[]? Runes { get; set; }
        public int Word90 { get; set; }
        public int Word26 { get; set; }
        public int PartyOrderStamp { get; set; }

        /// <summary>P1.4 carried GUILD.DAT record (base64); absent for remaster-made characters.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public byte[]? DosRecord { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DosRecordLifespan { get; set; }
    }

    private sealed class Phantasie2Dto
    {
        public int QuestScore { get; set; }
        public string? Origin { get; set; }
        public bool[]? MinionKilled { get; set; }
        public bool[]? BeastRunes { get; set; }
    }

    private sealed class Phantasie3Dto
    {
        public int SocialClass { get; set; }
        public int Rank { get; set; }
        public string? Origin { get; set; }
        public int QuestBits { get; set; }
        public int DeedBits { get; set; }
        public int Phantasie1Score { get; set; }
        public int Phantasie2Score { get; set; }
        public bool DivineAid { get; set; }
        public int FireBow { get; set; }
        public int? SavedAction { get; set; }
        public int? SavedCast { get; set; }
        public int? ExperienceShares { get; set; }

        /// <summary>D3-035 QoL: plane towns 15–17 entered (bit 0 = 15); null = none.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? PlaneTownsVisited { get; set; }
    }

    private sealed class LineageDto
    {
        public List<ParentDto>? Parents { get; set; }
    }

    private sealed class ParentDto
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RaceId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ClassId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }
    }

    private sealed class DeedDto
    {
        public string? Kind { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ContentId? Subject { get; set; }
    }

    private sealed class GuildSkillsDto
    {
        public int Attack { get; set; }
        public int Parry { get; set; }
        public int FindItem { get; set; }
        public int SpotTrap { get; set; }
        public int DisarmTrap { get; set; }
        public int Listen { get; set; }
        public int PickLock { get; set; }
        public int Swim { get; set; }
    }
}
