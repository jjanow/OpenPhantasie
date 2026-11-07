namespace OpenPhantasie.Data;

/// <summary>
/// Schema notes for character persistence (GUILD / SACK / DNG*.SAV).
/// Runtime characters live in the game-neutral <see cref="OpenPhantasie.Core.Character"/> (Phase 09 T1.1); the GUILD
/// byte layout lives in <see cref="GuildCodec"/> (<see cref="GuildCodec.Field"/>), Phantasie I-only words in
/// <see cref="OpenPhantasie.Core.Phantasie1CharacterData"/>. This type documents importer gaps rather than inventing DOS layouts.
/// </summary>
public static class CharacterSchemaNotes
{
    public const int GuildRecordSize = GuildCodec.RecordSize;
    public const int GuildSlotCount = GuildCodec.SlotCount;

    /*
     * Phase 3 carry-forward — M8 + Phase 8 P0.1 resolutions + remaining gaps:
     *
     * [C] (s36) DNG.SAV = id + map-screen image, DNGX.SAV = the 2500-byte level buffer (save-file-format.md);
     *     DungeonSaveCodec + Core DungeonSaveSlot. No party position is saved.
     *
     * Dungeon walkable grid is 33×38 (1254 B) [C] — see dungeon-representation.md
     *
     * [C] (s35) SACK.DAT = 60 × u16 item ids (the found-item list 0x63fa), 0 = empty; no XP/gold
     *     field — the pool lives in RAM and is split on town entry (save-file-format.md §Loot sack).
     *     SackCodec reads/writes it; Core keeps the semantic LootSack (XP/GP/items, D-018). Town-entry
     *     payout = Core's equal split, Q2 FIX remainder to the first survivor (D-037).
     *
     * [C] Armory buying has no CHA discount. OV1 FUN_0000_1d5b computes and the
     *     buy path deducts the full V²*price_index/500+1 price from the shared
     *     purse. The three pass-2 @262 values each fell 256→197 because they are
     *     persisted per-member shares: (3*256-177)/3, not because 59 was charged.
     *
     * [C] Guild training XP threshold: floor(mult*1000/statSum) at level 1,
     *     class-index→formula for Thief/Fighter/Priest/Wizard confirmed via
     *     live capture; Monk/Ranger [C] from the dispatch table (s11). Full
     *     float chain [C] (s39, OV1 0x12d7..0x13c4): X = (1.8^level - 0.8)*1000 /
     *     ((float)(statSum/mult) + 1e-5), 1.8^level -> 1.0 when XP is 0. Gold [C]:
     *     min(15000, (long)pow(level, 2.2) * max(20-CHA, 0)^2 * price_index / 100),
     *     OV1 0x13a1..0x1487 (town-menus.md §Guild). Mystic SCORE formula [C] (town-menus.md).
     *     Inn rest/MP raise, aging (on town entry) and level-up gains are [C]
     *     in town-menus.md (s11) and in Core since s17 (PLAN P1.2).
     *     There is no Temple town service in DOS (session 9, town-menus.md
     *     §Temple; manual agrees) — resurrection is the RESURRECTION spell.
     *
     * [C] Spell-learning fee (OV1 0x154d..0x165c) is independent of spell id:
     *     town.price_index * level * 90 / (CHA * INT * (9-class_id)), using DOS
     *     integer division. This replaces the old spellId%4+1 placeholder; that
     *     expression is the separate MP cast cost.
     *
     * [C] s19 combat fields (OV2 party base 0x5bb0, checked against fixtures): @12 defense
     *     rating (combat prep writes it; Character.Word12), @28 weapon damage A
     *     (Character.WeaponDamageA; writer [C] s28: town Exit OV1 0x3249, TownExit), @240 weapon B, @242 shield, @244 armor,
     *     @246/@248/@250 effective armor/damage/hit (hit = attack skill @30).
     *     The old "@28 = scroll count + weapon hit" dual-use was a base-0x5bb2 artefact.
     *
     * [C] @24 status word — 0=DEAD, 1=OK, 2=SLEEP (PHANTASI.COM table 0x6dfe; DOSBox
     *     s10). Codec maps it onto CharacterStatus explicitly (numeric values differ).
     * [C] @88 level (sheet LEVEL; Training rejects >= 15). @24 was mislabelled level.
     * [C] @218 in-party flag (Guild Add = 1, Drop = 0) — Character.InParty, set by GuildHall since s49. Not a status.
     * [C] @254 current town, @264 += 4 on every Guild Add (OV1 0x2845..0x284c, s49).
     * [C] @256 displayed race = Character.Race; 15 = Race.Undead (OV1 0x113).
     *     @216 = Character.BirthRace (keeps the living race). PLAN H4 (s10).
     *
     * [C] @92 roster ordinal / occupied marker (1..6), @256 race echo (== @216),
     *     @260 party-order stamp (2*@92+10, static; not the @264 age clock) —
     *     all now r/w by GuildCodec with round-trip tests (Phase 8 P0.1).
     * [C] s11 relabel, wired s17: @14 MP ceiling (Character.MpCeiling, was CombatRating),
     *     @16 MP max / @18 MP cur (were swapped), @50..@68 skill bonuses, @94 cast flag,
     *     @252 lifespan (×256 into age-clock units), @254 current town, @258 has-aged,
     *     @264 age clock (Character.AgeClock, was BirthStamp). @12 = defense rating (s19).
     *
     * [C] Q4 strong-race duplicate guard confirmed in OV1 0x0C28 (RANDOM path only);
     *     PRESERVE (quirks-and-bugs.md).
     *
     * [C] GUILD @262 = the member's bank account (sheet GOLD IN BANK, PHANTASI.COM 0x2f94);
     *     Withdraw moves it to the shared purse, the share split deposits into it (s35 DOSBox).
     *     The old "carried-gold share, not the bank" note is superseded; GoldInBank is right.
     *
     * [C] Worn-item fill — OV1 0x2fdb writes weapon points → @240, shield id → @242,
     *     armor points → @244 (CombatGear). @28 (damage A) is written by town Exit (s28).
     *
     * [C] Monster record (s22) — 80 × 0x39 at DS:0x1a6c of the unpacked (EXEPACK)
     *     PHANTASI.COM; armor +0x14, defense +0x2d, attack power +0x37, CV +0x25,
     *     level +0x18, HP base +0x23 (extract_monsters.py, MonsterDefinition).
     *
     * [C remaster] Experience width — Character.Experience is long; GUILD binary
     *     reads/writes s32 at @266..269 (sheet %ld). JSON saves are unbounded.
     *
     * [C] hl/mg potions, scrolls, rings, treasures are not stored: the equipment screen
     *     (COM 0x2cf6) counts them from the 9 carried items @222..@238 by id range
     *     (Character.HealingPotions etc., s19). Runes are flag words @72..@80 [C] (s31: AIR, EARTH,
     *     FIRE, WATER, GOD), Phantasie1CharacterData.Runes. @90 [C] (s39, Phantasie1CharacterData.Word90) = magical-pool product (creation 1; ×3/×5/×7 per pool kind).
     *
     * Confirmed baselines to lean on:
     *   GUILD 282×22 (skills + @70 score + worn @82/@84/@86 + @24 status/@88 level + @92/@218/@256/@260)
     *   spells_known u16[60] @0x60 by DOS slot (0/1/2); DIVINE I = slot 57 @210 (s41)
     *   TWNS stock/prices + creation tables
     *   combat to-hit/damage
     *   potion HP/MP restore formulas (magic-spells.md [C])
     *   spell MP cost = (index % 4) + 1 (magic-spells.md [C])
     *   dungeon walkable grid 33×38 (dungeon-representation.md [C])
     *   character creation formulas (character-creation.md [C])
     */
}
