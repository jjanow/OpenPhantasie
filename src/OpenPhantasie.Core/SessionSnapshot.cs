namespace OpenPhantasie.Core;

/// <summary>
/// Full campaign snapshot for remaster save-anywhere (Phase 7).
/// Does not require town/inn — Client may write this from any mode.
/// </summary>
public sealed class SessionSnapshot
{
    /// <summary>2 (s23): monster combat fields come from the real DOS record. Version-1 saves taken mid-combat
    /// hold the old packed-byte Armor/Defense/AttackPower and are refused (user decision, s23).
    /// 3 (Phase 09 T1.2): remaster save JSON writes item, monster and dungeon ids game-prefixed (<c>p1:42</c>); the
    /// JSON codec refuses older files (no old-save migration). The snapshot itself still holds the local numbers.</summary>
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>The game this campaign belongs to (T1.2): the prefix of every id in its save file.</summary>
    public GameId Game { get; set; } = GameId.P1;
    public GameMode Mode { get; set; }
    public GameMode CombatReturnMode { get; set; }
    public int? CurrentTownId { get; set; }

    /// <summary>Guild roster record slots (the game's <see cref="RulesProfile.GuildSlotCount"/>; empty in a parked party).</summary>
    public Character?[] RosterSlots { get; set; } = Array.Empty<Character?>();
    public int[] PartySlotIndices { get; set; } = Array.Empty<int>();
    public long PartyGold { get; set; }

    /// <summary>Party slot 7: the elemental loaded at the Guild (null if none). Not in <see cref="PartySlotIndices"/>.</summary>
    public Character? Elemental { get; set; }

    /// <summary>The elemental was summoned in the saved combat (it is member 7 until the fight ends).</summary>
    public bool ElementalSummoned { get; set; }

    public int OverworldRegionId { get; set; }
    public int OverworldX { get; set; }
    public int OverworldY { get; set; }
    public int OverworldTerrainClass { get; set; }

    /// <summary>Travel-time counter <c>[0x63f2]</c> (<see cref="GameSession.TravelTime"/>); 0 in older saves.</summary>
    public int TravelTime { get; set; }

    /// <summary>Wilderness globals and region bytes (s39); null in older saves.</summary>
    public WildernessSnapshot? Wilderness { get; set; }

    public DungeonSnapshot? Dungeon { get; set; }

    /// <summary>The one-slot dungeon save (DNG.SAV + DNGX.SAV), or null when empty (s36).</summary>
    public DungeonSaveSnapshot? DungeonSave { get; set; }

    /// <summary>
    /// The other dungeons' saved maps in a game that keeps one per dungeon (III's <c>DNP&lt;letter&gt;</c>,
    /// <see cref="RulesProfile.DungeonSavePerDungeon"/>); empty otherwise. Top level only, as <see cref="DungeonSave"/>.
    /// </summary>
    public DungeonSaveSnapshot[] OtherDungeonSaves { get; set; } = Array.Empty<DungeonSaveSnapshot>();
    public LootSackSnapshot Sack { get; set; } = new();
    public CombatSnapshot? Combat { get; set; }

    /// <summary>Treasure items still waiting for their "KEEP IT?" answer (<see cref="GameSession.PendingTreasure"/>).</summary>
    public int[] PendingTreasure { get; set; } = Array.Empty<int>();

    /// <summary>Who found each <see cref="PendingTreasure"/> item (null = none named; III's fight finds name one).</summary>
    public string?[] PendingTreasureFinders { get; set; } = Array.Empty<string?>();

    /// <summary>`core.defeat.your_party_has_been_defeated` is waiting (<see cref="GameSession.PartyDefeatPending"/>; s41).</summary>
    public bool PartyDefeatPending { get; set; }

    /// <summary>
    /// P1.4: town Armory state kept between visits (<see cref="GameSession.Towns"/>). Empty in older saves — every
    /// town then starts from the data files again, as before.
    /// </summary>
    public TownState[] Towns { get; set; } = Array.Empty<TownState>();

    /// <summary>
    /// D-069 (manual: several parties explore at once): the parked parties, each holding only party-local state
    /// (<see cref="SeveralParties"/>; roster, towns, region bytes and the dungeon save slot live on this top level only).
    /// Empty in a parked entry.
    /// </summary>
    public SessionSnapshot[] OtherParties { get; set; } = Array.Empty<SessionSnapshot>();

    /// <summary>
    /// T5.0: the rules the whole save is locked to (<see cref="GameSession.Options"/>). Top level only: a parked party
    /// never carries it. Null in a save written before s73 (loads as Original (manual)).
    /// </summary>
    public GameOptionsSnapshot? Options { get; set; }
}

/// <summary>A save's rules (<see cref="GameOptions"/>) as plain ids: the preset, option id → value, enabled module ids.</summary>
public sealed class GameOptionsSnapshot
{
    public string Preset { get; set; } = OptionPreset.Manual.Id();
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
    public string[] Modules { get; set; } = Array.Empty<string>();

    public static GameOptionsSnapshot From(GameOptions options) => new()
    {
        Preset = options.Preset.Id(),
        Values = new Dictionary<string, string>(options.Values, StringComparer.Ordinal),
        Modules = options.Modules.ToArray(),
    };

    /// <summary>Rebuild leniently (<see cref="GameOptions.Restore"/>).</summary>
    public GameOptions ToOptions(GameId game) => GameOptions.Restore(game, Preset, Values, Modules);
}

/// <summary>
/// Overworld state DOS keeps between steps (s39): <c>[0x6496]</c>, <c>[0x63ea]</c>, the surprise globals, a pending
/// day-encounter menu, and the region bytes (DOS writes <c>OUT*.DAT</c> back: spent x7 tiles and fog).
/// </summary>
public sealed class WildernessSnapshot
{
    public bool TileClassFightDone { get; set; }
    public int HighestScore { get; set; }

    /// <summary><c>[0x63ec]</c>: average party level at the last town exit (<see cref="GameSession.TownExitAverageLevel"/>); null in older saves.</summary>
    public int? TownExitAverageLevel { get; set; }
    public bool SurpriseFirstRound { get; set; }
    public int MonsterHold { get; set; }

    /// <summary>DOS's living list <c>[0x64a2]</c> (<see cref="Party.DosLiving"/>, D-033 <c>dos</c>); null in older saves.</summary>
    public int?[]? DosLivingEntries { get; set; }

    /// <summary>Its word <c>[0x6490]</c>.</summary>
    public int DosLivingWord { get; set; }
    public WildernessMenu? PromptMenu { get; set; }
    public int PromptMonsterId { get; set; }
    public int? PromptListenerSlot { get; set; }
    public string? PromptTitle { get; set; }

    /// <summary>The entry question's place name (III's "&lt;name&gt; Will you enter?", <see cref="OverworldEntryPrompt.Name"/>); null = I's text.</summary>
    public string? EntryName { get; set; }

    /// <summary>The dungeon entry is at its second question, `core.p3.load_saved_dungeon` (<see cref="OverworldEntryPrompt.AsksLoad"/>).</summary>
    public bool EntryAsksLoad { get; set; }

    /// <summary>A pending "A TOWN. ENTER?" / `core.overworld.a_dungeon_enter` (<see cref="GameSession.EntryPrompt"/>); null = none.</summary>
    public OverworldEntryKind? EntryKind { get; set; }
    public int EntryId { get; set; }
    public int EntryDestX { get; set; }
    public int EntryDestY { get; set; }

    /// <summary>Raw 520-byte tile grid per region id (fog-encoded, spent fixed encounters included).</summary>
    public Dictionary<int, byte[]>? RegionTiles { get; set; }

    /// <summary>A field-by-field copy (the region bytes are shared, not copied).</summary>
    public WildernessSnapshot ShallowCopy() => (WildernessSnapshot)MemberwiseClone();
}

public sealed class DungeonSnapshot
{
    public int LevelId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public DungeonFacing Facing { get; set; }

    /// <summary>The live 2500-byte level buffer (s36; replaces ClearedEventKeys). Null in older saves.</summary>
    public byte[]? LevelBuffer { get; set; }

    /// <summary>`core.prompt.an_exit_take_it` was waiting for an answer.</summary>
    public bool ExitPending { get; set; }

    /// <summary>Previous cell (<c>[0x751e]/[0x7520]</c>, the step-back target). Null in pre-s39 saves → the current cell.</summary>
    public int? PrevX { get; set; }
    public int? PrevY { get; set; }

    /// <summary>Wandering-encounter chance <c>[0x1a5a]</c>. Null in pre-s39 saves → 7 (entry value).</summary>
    public int? WanderingChance { get; set; }

    /// <summary>Last message-cell message shown (<c>[0x64f4]</c>). Null in pre-s39 saves → 0.</summary>
    public int? LastMessageId { get; set; }

    /// <summary>"%s SPOTS A TRAP" waiting for D)ISARM / L)EAVE (s41: modal prompts survive a save).</summary>
    public DungeonTrapPromptSnapshot? TrapPrompt { get; set; }

    /// <summary>"%s `core.dungeon.x_hears_monsters_go_back`": the listener's party index; null = no lock prompt.</summary>
    public int? LockListener { get; set; }
    public string? LockMessage { get; set; }

    /// <summary>A scripted-event menu waiting for its answer: the cell's feature byte and the message id.</summary>
    public int? EventFeature { get; set; }
    public int? EventMessageId { get; set; }

    /// <summary>
    /// The waiting question's own lines and choices (s95): a game's synthetic prompts (III's door / trap questions) are not
    /// level messages, and a menu may be padded past its text's choices. Null in older saves = the level message.
    /// </summary>
    public string[]? EventLines { get; set; }
    public string[]? EventOptions { get; set; }

    /// <summary>An event script stopped at op 6's KEEP IT? (s58): the rest runs after the answer. Null = none.</summary>
    public DungeonScriptResume? ScriptResume { get; set; }

    /// <summary>III's event-text page (<see cref="GameSession.DungeonPage"/>): its rows, null = none; and whether it waits for a key.</summary>
    public string[]? PageLines { get; set; }
    public bool PageWaits { get; set; }
}

public sealed class DungeonTrapPromptSnapshot
{
    public int TrapKind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int SpotterIndex { get; set; }
    public string Message { get; set; } = "";
    public int[] Disarmers { get; set; } = Array.Empty<int>();
}

public sealed class DungeonSaveSnapshot
{
    public int DungeonId { get; set; }
    public byte[] LevelBuffer { get; set; } = Array.Empty<byte>();
    public byte[]? ScreenImage { get; set; }
}

public sealed class LootSackSnapshot
{
    public long Experience { get; set; }
    public long Gold { get; set; }
    public int[] ItemIds { get; set; } = Array.Empty<int>();

    /// <summary><see cref="LootSack.TownExitItemIds"/>.</summary>
    public int[]? TownExitItemIds { get; set; }
}

public sealed class CombatSnapshot
{
    public long RewardXp { get; set; }
    public long RewardGold { get; set; }

    /// <summary>Encounter level <c>L</c>; null in saves from before the encounter builder (recomputed per rank).</summary>
    public int? MonsterLevel { get; set; }

    public int[] TreasureItemIds { get; set; } = Array.Empty<int>();

    /// <summary>First monster id (<see cref="CombatEncounter.FirstMonsterId"/>); null in older saves.</summary>
    public int? FirstMonsterId { get; set; }

    public int ReactionSum { get; set; }

    /// <summary>THREATEN factor <c>[0xc432]</c>; null in older saves = 15.</summary>
    public int? ThreatenFactor { get; set; }

    /// <summary><c>[0x74b6]</c>: a monster has been damaged this fight.</summary>
    public bool MonsterDamaged { get; set; }

    /// <summary>A failed monster run is pending: no monster acts in the next round.</summary>
    public bool MonstersLoseRound { get; set; }

    /// <summary><c>[0x74b0]</c>: a spell ended the fight; it stops at the next round top.</summary>
    public SpellCombatEnd SpellEnd { get; set; }

    /// <summary><c>[0x74aa]</c> from dissolve: "MONSTERS KILLED" at the end.</summary>
    public bool Dissolved { get; set; }

    /// <summary><c>[0x74b8]</c>: DISPELL UNDEAD already tried on an undead front group.</summary>
    public bool DispellUndeadTried { get; set; }

    /// <summary>Surprise first-round flag <c>[0x649e]</c> (§Surprise); false in older saves.</summary>
    public bool SurpriseFirstRound { get; set; }

    /// <summary>Monster hold <c>[0x64a0]</c> (0/1/2); 0 in older saves (between rounds it was always 0 before s33).</summary>
    public int MonsterHold { get; set; }

    /// <summary>The next round is a surprise opening with no PARTY OPTIONS or orders; false in older saves.</summary>
    public bool OpensWithoutOrders { get; set; }

    /// <summary>A failed Flee: the next round skips the orders phase (<see cref="CombatEncounter.OrdersSkipped"/>, D-033 <c>dos</c>); false in older saves.</summary>
    public bool OrdersSkipped { get; set; }

    /// <summary>The morale check (OV2 <c>0x3b70</c>) already ran this round (<see cref="GameSession.CombatMoraleCheckedThisRound"/>);
    /// false in older saves.</summary>
    public bool MoraleCheckedThisRound { get; set; }

    /// <summary>A PARTY OPTIONS choice was already taken this round (<see cref="GameSession.CombatOptionChosenThisRound"/>);
    /// false in older saves.</summary>
    public bool OptionChosenThisRound { get; set; }

    /// <summary>Monsters in current rank order.</summary>
    public List<CombatMonsterSnapshot> Monsters { get; set; } = new();

    /// <summary>
    /// Per-member effective combat fields (<see cref="CombatEncounter.Prep"/>, party order). DOS keeps <c>@246/@248/@250</c>
    /// and the action table <c>[0x74fa]</c> until the next prep <c>0x3263</c>, so QUICKNESS / STRENGTH / PROTECTION / NINJA,
    /// SLOWED and the other special 17–24 scalings survive a mid-combat load. Null in saves from before s33 = rebuilt by prep.
    /// </summary>
    public List<CombatPrepSnapshot>? Prep { get; set; }

    /// <summary>A Phantasie III fight's own state (<see cref="P3Fight"/>); null in other games.</summary>
    public P3FightSnapshot? Phantasie3 { get; set; }
}

public sealed class CombatPrepSnapshot
{
    public int ArmorEff { get; set; }
    public int WeaponDamageEff { get; set; }
    public int WeaponHitEff { get; set; }
    public int PackedAction { get; set; }
}

public sealed class CombatMonsterSnapshot
{
    public int Id { get; set; }

    /// <summary>Build-order group (<see cref="MonsterRank.RankIndex"/>); null in older saves = one rank per monster.</summary>
    public int? Group { get; set; }

    public int HitPoints { get; set; }
    public int Armor { get; set; }
    public int Defense { get; set; }
    public int AttackPower { get; set; }

    /// <summary><see cref="MonsterStatusFlags"/>; SLEEP lasts across rounds (D-020), so a mid-combat save keeps it.</summary>
    public MonsterStatusFlags StatusFlags { get; set; }
}
