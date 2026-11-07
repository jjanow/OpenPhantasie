namespace OpenPhantasie.Core;

/// <summary>
/// The rule constants that differ between the games of the series (Phase 09 T1.3, from the T0 feature matrix rows marked
/// "each"): gold caps, Guild size and starting gold. Each game ships its own values in
/// <c>GameData/&lt;game&gt;/rules.json</c> (loaded by <c>RulesProfileLoader</c> in OpenPhantasie.Data); a
/// <see cref="GameSession"/> gets its profile at construction and hands it to the party, sack and roster it builds.
/// <para>
/// Constants that are the same in every game (party of 6, 9 items per character, 90 sack items, level 15, the level and
/// training tables) stay in code. DOS file-format facts (the 22 GUILD.DAT records) stay in the codecs.
/// </para>
/// </summary>
public sealed record RulesProfile
{
    /// <summary>The game these rules belong to (<c>"game": "p1"</c> in rules.json).</summary>
    public required GameId Game { get; init; }

    /// <summary>
    /// Most gold the party purse holds; the excess is lost. Phantasie I: 65,000 (manual p.10, D-048; DOS clamps at 65,535).
    /// </summary>
    public required long PartyGoldCap { get; init; }

    /// <summary>
    /// Most gold one character's bank account holds; the excess is lost. Phantasie I: 65,000 (manual p.10, D-048).
    /// </summary>
    public required long BankGoldCap { get; init; }

    /// <summary>
    /// Record slots in the Guild roster. Phantasie I: 22, so original 22-character DOS saves still load (D-002).
    /// </summary>
    public required int GuildSlotCount { get; init; }

    /// <summary>Most characters Guild N)EW will fill. Phantasie I: 21 per the manual (DOS fills all 22, D-002).</summary>
    public required int GuildMaxCharacters { get; init; }

    /// <summary>Gold a new character starts with in the bank. Phantasie I: 256 (III: by social class).</summary>
    public required int StartingGold { get; init; }

    /// <summary>
    /// The worn-item slots and the item ids each takes (09 T2, <c>inventory-actions.md</c> §2). Phantasie I: shield 1–20,
    /// armor 21–40, weapon 41–100; III adds a bow slot.
    /// </summary>
    public required EquipmentLayout Equipment { get; init; }

    /// <summary>
    /// What each original platform's Guild held (T1.3 rest; <c>guild_by_platform</c> in rules.json), keyed by platform id
    /// (the <c>reverse-engineering/&lt;platform&gt;</c> folder names: <c>dos</c>, <c>amiga</c>, <c>apple2</c>, <c>c64</c>…).
    /// A platform whose numbers are unknown is absent; a value no source states is null. The remaster plays
    /// <see cref="GuildSlotCount"/> / <see cref="GuildMaxCharacters"/>; this table only records the originals, and
    /// <see cref="ForPlatform"/> applies one of them. Phantasie I: DOS 22/22, Amiga 22 slots, Apple 37/37, C64 at most 21.
    /// </summary>
    public PlatformGuildTable GuildByPlatform { get; init; } = PlatformGuildTable.Empty;

    /// <summary>
    /// The platform whose code is this game's rules source (<c>original_platform</c> in rules.json, a
    /// <see cref="GuildByPlatform"/> key): the side the Original (DOS / Atari ST) preset plays. Phantasie I: <c>dos</c>;
    /// II: <c>atari-st</c> (s71). Defaults to <c>dos</c>.
    /// </summary>
    public string OriginalPlatform { get; init; } = "dos";

    /// <summary>
    /// The Guild training XP target's per-game constants (<c>training</c> in rules.json): each class's stat sum and divisor
    /// and when the level factor resets. Null = the profile does not state them. Phantasie I: <see
    /// cref="TrainingRules.PhantasieI"/> (play uses <see cref="TownMenus.TrainingTarget"/>, which matches it); II: FIGHTER
    /// (2·STR + DEX)/33.0, PRIEST (STR + 2·INT)/36.3, factor 1.0 at level 0 (D2-012).
    /// </summary>
    public TrainingRules? Training { get; init; }

    /// <summary>
    /// The wilderness terrain-class rules (<c>terrain</c> in rules.json, <see cref="TerrainRules"/>): I's fixed fight and
    /// pool, II's terrain fights and lava. Null = the profile does not state them (the traversal then plays I's).
    /// </summary>
    public TerrainRules? Terrain { get; init; }

    /// <summary>
    /// The Toss Rock numbers (<c>toss_rock</c> in rules.json, <see cref="TossRockRules"/>): stated by Phantasie II, the
    /// module's home game. Null elsewhere.
    /// </summary>
    public TossRockRules? TossRock { get; init; }

    /// <summary>
    /// The encounter builder and encounter-roll rules (<c>encounters</c> in rules.json, <see cref="EncounterRules"/>).
    /// Null = the profile does not state them, and play uses Phantasie I's DOS rules.
    /// </summary>
    public EncounterRules? Encounters { get; init; }

    /// <summary>
    /// When the Guild lets an elemental join (<c>elementals</c> in rules.json, <see cref="ElementalRules"/>). Null = I's
    /// (<see cref="ElementalRules.PhantasieI"/>: six members first, no Add after it).
    /// </summary>
    public ElementalRules? Elementals { get; init; }

    /// <summary>
    /// The game keeps one saved map per dungeon (<c>dungeon_save_per_dungeon</c> in rules.json): III writes
    /// <c>DNP&lt;letter&gt;</c> / <c>DNG&lt;letter&gt;</c> for each (<c>dungeon-events.md</c> §1). False = I's / II's one slot
    /// (<c>DNG.SAV</c>, whichever dungeon was saved last).
    /// </summary>
    public bool DungeonSavePerDungeon { get; init; }

    /// <summary>
    /// Guild ▸ Drop pays the dropped character a share of the party's gold into his bank account (<c>drop_gold_share</c>
    /// in rules.json): <c>⌊party gold / characters⌋</c>, the divisor counted before the Drop, the elemental not counted
    /// (III, DOS ovl2 <c>0x3c92</c> and Amiga <c>0xd448</c> [C] s98; Apple II's Drop moves no gold; the manual is silent).
    /// False = I's / II's Drop, which leaves the purse alone (<see cref="GuildHall.DropGoldShare"/>).
    /// </summary>
    public bool DropGoldShare { get; init; }

    /// <summary>The elemental rules play uses: <see cref="Elementals"/>, or Phantasie I's.</summary>
    public ElementalRules ElementalRulesOrDefault => Elementals ?? ElementalRules.PhantasieI;

    /// <summary>The encounter rules play uses: <see cref="Encounters"/>, or Phantasie I's.</summary>
    public EncounterRules EncounterRulesOrDefault => Encounters ?? EncounterRules.PhantasieI;

    /// <summary>
    /// This profile with <paramref name="platform"/>'s Guild size in place of the remaster's; a value that platform does
    /// not state keeps the remaster's. An unknown platform returns this profile unchanged.
    /// </summary>
    public RulesProfile ForPlatform(string platform)
    {
        if (!GuildByPlatform.TryGetValue(platform, out var size)) return this;
        return (this with
        {
            GuildSlotCount = size.SlotCount ?? GuildSlotCount,
            GuildMaxCharacters = size.MaxCharacters ?? Math.Min(GuildMaxCharacters, size.SlotCount ?? GuildSlotCount),
        }).Validate();
    }

    /// <summary>
    /// Phantasie I's values, for code paths built without a session (tests, the parameterless <see cref="Party"/>,
    /// <see cref="GuildRoster"/> and <see cref="LootSack"/>). The shipped <c>GameData/phantasie1/rules.json</c> holds the
    /// same numbers; a test pins the two together so they cannot drift.
    /// </summary>
    public static RulesProfile PhantasieI { get; } = new()
    {
        Game = GameId.P1,
        PartyGoldCap = 65_000,
        BankGoldCap = 65_000,
        GuildSlotCount = 22,
        GuildMaxCharacters = 21,
        StartingGold = 256,
        Equipment = EquipmentLayout.PhantasieI,
        GuildByPlatform = new PlatformGuildTable(new Dictionary<string, PlatformGuildSize>
        {
            ["dos"] = new(SlotCount: 22, MaxCharacters: 22),
            ["amiga"] = new(SlotCount: 22, MaxCharacters: null),
            ["apple2"] = new(SlotCount: 37, MaxCharacters: 37),
            ["c64"] = new(SlotCount: null, MaxCharacters: 21),
        }),
        Training = TrainingRules.PhantasieI,
        Terrain = TerrainRules.PhantasieI,
    };

    /// <summary>Throws <see cref="ArgumentException"/> when a value is out of range.</summary>
    public RulesProfile Validate()
    {
        if (PartyGoldCap <= 0) throw new ArgumentException($"{nameof(PartyGoldCap)} must be positive.");
        if (BankGoldCap <= 0 || BankGoldCap > int.MaxValue)
            throw new ArgumentException($"{nameof(BankGoldCap)} must be 1..{int.MaxValue}.");
        if (GuildSlotCount <= 0) throw new ArgumentException($"{nameof(GuildSlotCount)} must be positive.");
        if (GuildMaxCharacters <= 0 || GuildMaxCharacters > GuildSlotCount)
            throw new ArgumentException($"{nameof(GuildMaxCharacters)} must be 1..{nameof(GuildSlotCount)}.");
        if (StartingGold < 0) throw new ArgumentException($"{nameof(StartingGold)} must not be negative.");
        ArgumentNullException.ThrowIfNull(Equipment);
        Equipment.Validate();
        Training?.Validate();
        Terrain?.Validate();
        TossRock?.Validate();
        Encounters?.Validate();
        ArgumentNullException.ThrowIfNull(GuildByPlatform);
        foreach (var (platform, size) in GuildByPlatform.Entries)
        {
            if (string.IsNullOrWhiteSpace(platform))
                throw new ArgumentException($"{nameof(GuildByPlatform)}: empty platform id.");
            if (size.SlotCount is null && size.MaxCharacters is null)
                throw new ArgumentException($"{nameof(GuildByPlatform)}[{platform}] states no Guild size.");
            if (size.SlotCount <= 0)
                throw new ArgumentException($"{nameof(GuildByPlatform)}[{platform}]: slots must be positive.");
            var slots = size.SlotCount ?? GuildSlotCount;
            if (size.MaxCharacters <= 0 || size.MaxCharacters > slots)
                throw new ArgumentException($"{nameof(GuildByPlatform)}[{platform}]: max characters must be 1..{slots}.");
        }
        return this;
    }
}

/// <summary>
/// One original platform's Guild size (<see cref="RulesProfile.GuildByPlatform"/>): record slots and the most characters
/// N)EW fills. Null = no source states it <c>[?]</c>.
/// </summary>
public sealed record PlatformGuildSize(int? SlotCount, int? MaxCharacters);


/// <summary>
/// The per-platform Guild sizes of a <see cref="RulesProfile"/>, keyed by platform id. Read-only, and equal by content
/// (any key order) so the record's value equality, and the test pinning rules.json to <see cref="RulesProfile.PhantasieI"/>,
/// see it.
/// </summary>
public sealed class PlatformGuildTable : IEquatable<PlatformGuildTable>
{
    public static PlatformGuildTable Empty { get; } = new(new Dictionary<string, PlatformGuildSize>());

    private readonly IReadOnlyDictionary<string, PlatformGuildSize> _entries;

    public PlatformGuildTable(IEnumerable<KeyValuePair<string, PlatformGuildSize>> entries) =>
        _entries = new Dictionary<string, PlatformGuildSize>(entries, StringComparer.Ordinal).AsReadOnly();

    public IReadOnlyDictionary<string, PlatformGuildSize> Entries => _entries;

    public int Count => _entries.Count;

    public IEnumerable<string> Platforms => _entries.Keys;

    public PlatformGuildSize this[string platform] => _entries[platform];

    public bool TryGetValue(string platform, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out PlatformGuildSize size) =>
        _entries.TryGetValue(platform, out size);

    public bool Equals(PlatformGuildTable? other) =>
        other is not null
        && Count == other.Count
        && _entries.All(kv => other._entries.TryGetValue(kv.Key, out var o) && o == kv.Value);

    public override bool Equals(object? obj) => Equals(obj as PlatformGuildTable);

    public override int GetHashCode() => Count;
}
