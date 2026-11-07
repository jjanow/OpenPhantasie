using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Builds a <see cref="GameSession"/> from loaded game tables. The session's game is its campaign's
/// (<see cref="GameSession.Game"/>, Phase 09 T1.4); the data folder and the rules profile must be of the same game.
/// </summary>
public static class GameSessionFactory
{
    public static GameSession Create(
        GameDataRepository data,
        IDosRandomSource? rng = null,
        ICombatResolver? combat = null,
        RulesProfile? rules = null,
        ICampaign? campaign = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        campaign ??= data.CreateCampaign();
        if (data.Game != campaign.Game)
            throw new InvalidDataException(
                $"The game data is {data.Game.Prefix()}, but the campaign is {campaign.Game.Prefix()}.");
        rng ??= new SeededDosRandomSource(Environment.TickCount);

        var monsterIds = data.Monsters
            .Where(m => m.HpBase > 0 && m.HpBase <= 20) // stub pool: only the debug Fight key (Begin*Combat() with no id) uses it; game paths pass the DOS monster
            .Select(m => m.Id)
            .Take(16)
            .ToList();
        if (monsterIds.Count == 0)
            monsterIds = data.Monsters.Select(m => m.Id).Take(8).ToList();

        return new GameSession(
            overworld: data.CreateTraversal(campaign),
            dungeons: data.CreateDungeonTraversal(),
            items: data.CreateItemCatalog(),
            townFactory: data.CreateTownState,
            monsterRecord: id => data.GetMonster(id).ToRecord(),
            creation: data.CreationTables,
            rng: rng,
            rules: rules ?? data.Rules,
            combat: combat,
            wildernessMonsterIds: monsterIds,
            elementalTemplates: data.Elementals.Select(e => e.ToCharacter()).ToList(),
            wildernessEncounters: data.WildernessEncounters,
            campaign: campaign);
    }

    /// <summary>Import Phase 3 guild.json snapshots into a live roster.</summary>
    public static void SeedRosterFromFixture(GameSession session, GameDataRepository data)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(data);

        foreach (var snap in data.GuildRoster.Characters)
        {
            if (snap.Slot < 0 || snap.Slot >= session.Roster.Slots.Length)
                continue;

            var ch = SnapshotToCharacter(snap, data.CreationTables);
            session.Roster.Slots[snap.Slot] = ch;
        }
    }

    /// <summary>Shipped guild.json records are DOS characters: lifespan from the D-003 band (user s17).</summary>
    public static Character SnapshotToCharacter(GuildCharacterSnapshot snap, CharacterCreationTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (!Enum.TryParse<Race>(snap.Race, ignoreCase: true, out var race))
            race = Race.Human;
        if (!Enum.TryParse<Race>(snap.BirthRace, ignoreCase: true, out var birthRace))
            birthRace = race;
        if (!Enum.TryParse<CharacterClass>(snap.Class, ignoreCase: true, out var cls))
            cls = CharacterClass.Fighter;
        if (!Enum.TryParse<CharacterStatus>(snap.Status, ignoreCase: true, out var status))
            status = CharacterStatus.Ok;

        var ch = new Character
        {
            Name = snap.Name,
            Race = race,
            BirthRace = birthRace,
            Class = cls,
            Status = status,
            Level = snap.Level,
            Stats = new CharacterStats(
                snap.Strength, snap.Intelligence, snap.Dexterity,
                snap.Constitution, snap.Charisma, snap.Luck),
            MagicCur = snap.MagicCur,
            MagicMax = snap.MagicMax,
            MpCeiling = snap.MpCeiling,
            Word12 = snap.Word12,
            CastSinceRest = snap.CastSinceRest,
            CurrentTown = GuildCodec.TownFromDos(snap.CurrentTown), // guild.json keeps the raw DOS @254
            HasAged = snap.HasAged,
            AgeClock = snap.AgeClock,
            HpMax = snap.HpMax,
            HpCur = snap.HpCur,
            GoldInBank = snap.GoldInBank,
            Experience = snap.Experience,
            WeaponDamageA = snap.WeaponDamageA ?? snap.ScrollOrWeaponHit ?? 0,
            WornShield = snap.WornShield,
            WornArmor = snap.WornArmor,
            WornWeapon = snap.WornWeapon,
            Score = snap.Score,
            Skills = snap.Skills is null
                ? new CharacterSkills(0, 0, 0, 0, 0, 0, 0, 0)
                : new CharacterSkills(
                    snap.Skills.Attack, snap.Skills.Parry, snap.Skills.FindItem,
                    snap.Skills.SpotTrap, snap.Skills.DisarmTrap, snap.Skills.Listen,
                    snap.Skills.PickLock, snap.Skills.Swim),
            SpellsKnown = new int[CharacterCreation.SpellCount],
        };
        ch.Lifespan = CharacterCreation.LifespanForImported(ch, tables);
        ch.SkillBonuses = snap.SkillBonuses is { Length: CharacterSkills.RecordSlotCount } b
            ? (int[])b.Clone()
            : CharacterCreation.SkillBonusSeeds(ch.Stats);

        if (snap.Inventory is not null)
        {
            ch.InventoryItemIds.AddRange(snap.Inventory);
        }
        else
        {
            // Pre-s19 extracts carry no item list: keep the worn items at least.
            if (ch.WornShield != 0) ch.InventoryItemIds.Add(ch.WornShield);
            if (ch.WornWeapon != 0) ch.InventoryItemIds.Add(ch.WornWeapon);
            if (ch.WornArmor != 0) ch.InventoryItemIds.Add(ch.WornArmor);
        }
        CombatGear.ApplyWornItemPoints(ch);
        return ch;
    }
}
