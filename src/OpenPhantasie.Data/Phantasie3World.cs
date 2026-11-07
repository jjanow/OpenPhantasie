using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Builds Core's <see cref="P3WorldData"/> from a Phantasie III data folder (Phase 09 T4): places, tile terrain, planes,
/// the wilderness encounter tables, dungeon entrances, training and skill tables (with D3-032's fixes), the class kit,
/// social classes and the hit-location table.
/// </summary>
public static class Phantasie3World
{
    /// <summary>The campaign a session of <paramref name="data"/>'s game plays (III: one bound to its world data).</summary>
    public static ICampaign CreateCampaign(this GameDataRepository data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Game == GameId.P3 ? new Phantasie3Campaign(data.BuildPhantasie3World()) : Campaigns.For(data.Game);
    }

    private static readonly IReadOnlyDictionary<string, P3Skill> SkillIds = new Dictionary<string, P3Skill>
    {
        ["attack"] = P3Skill.Attack, ["parry"] = P3Skill.Parry, ["find_item"] = P3Skill.FindItem,
        ["find_trap"] = P3Skill.FindTrap, ["disarm_trap"] = P3Skill.Disarm, ["listen"] = P3Skill.Listen,
        ["pick_lock"] = P3Skill.PickLock, ["swim"] = P3Skill.Swim,
    };

    private static CharacterClass Class(string name) =>
        Enum.TryParse<CharacterClass>(name, ignoreCase: true, out var c)
            ? c
            : throw new InvalidDataException($"rule-tables.json: unknown class '{name}'.");

    public static P3WorldData BuildPhantasie3World(this GameDataRepository data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var rt = data.Phantasie3 ?? throw new InvalidDataException("The game data has no rule-tables.json (Phantasie III).");
        var terrain = data.TileTerrain ?? throw new InvalidDataException("The game data has no tile terrain (Phantasie III).");
        var wild = rt.Wilderness ?? throw new InvalidDataException("rule-tables.json has no wilderness_encounters block.");

        var planeRegions = data.OverworldRegions.Where(r => r.Plane is not null).ToDictionary(r => r.Plane!, r => r.RegionId);
        var places = data.Places.Select(p => new P3Place(
            planeRegions.TryGetValue(p.Plane, out var region)
                ? region
                : throw new InvalidDataException($"Place {p.Name} names an unknown plane '{p.Plane}'."),
            p.X, p.Y,
            p.Kind switch
            {
                "town" => P3PlaceKind.Town,
                "dungeon" => P3PlaceKind.Dungeon,
                "inn" => P3PlaceKind.Inn,
                _ => throw new InvalidDataException($"Place {p.Name} has an unknown type '{p.Kind}'."),
            },
            p.Number, p.Name)).ToList();
        var start = places.FirstOrDefault(p => p.Kind == P3PlaceKind.Town && p.Number == Phantasie3Campaign.HomeTownId)
                    ?? throw new InvalidDataException("No square for town 1 (Pendragon).");

        P3TrainingTables Tables(bool fixedCells)
        {
            var skills = new Dictionary<P3Skill, IReadOnlyDictionary<CharacterClass, IReadOnlyList<int>>>();
            foreach (var (id, skill) in SkillIds)
            {
                var rows = rt.SkillTables[id];
                var byClass = new Dictionary<CharacterClass, IReadOnlyList<int>>();
                foreach (var (cls, row) in rows)
                {
                    var use = row;
                    if (fixedCells && rt.SkillTableFixes.TryGetValue(id, out var fix) && fix.TryGetValue(cls, out var fixedRow))
                        use = fixedRow;
                    byClass[Class(cls)] = use;
                }
                skills[skill] = byClass;
            }
            return new P3TrainingTables(
                rt.TrainingXpCurve.Skip(1).ToArray(),
                rt.TrainingGoldCurve.Skip(1).ToArray(),
                rt.HpDice.ToDictionary(kv => Class(kv.Key), kv => kv.Value),
                skills);
        }

        var coreRaces = rt.CoreRace;
        return new P3WorldData
        {
            Places = places,
            Terrain = new P3Terrain(
                terrain.Ranges.Select(r => new P3TerrainRange(r.First, r.Last, r.Terrain)).ToList(),
                terrain.NetherworldOverrides.Select(r => new P3TerrainRange(r.First, r.Last, r.Terrain)).ToList(),
                terrain.Other, terrain.BlockedFrom, terrain.NoEncounter, terrain.Names),
            Wilderness = new P3WildernessRules(wild.Chance, wild.NightBelow, wild.Kind2Below, wild.SleepingBelow,
                wild.NoticePerLevel, wild.SurpriseBelow, wild.PartySurpriseBelow, wild.RunAwayBelow, wild.AreaLevels,
                wild.TerrainOffsets),
            PlaneNames = planeRegions.ToDictionary(kv => kv.Value, kv => kv.Key),
            PlaneArrivals = data.OverworldRegions.Where(r => r.Plane is not null && r.Arrival is not null)
                .ToDictionary(r => r.RegionId, r => r.Arrival!.Value),
            Start = (start.RegionId, start.X, start.Y),
            DungeonEntranceX = rt.DungeonEntranceX,
            Training = Tables(fixedCells: true),
            TrainingAsRead = Tables(fixedCells: false),
            HitLocations = new P3HitLocationTable(rt.HitLocationRows.Select(h => h.Cumulative).ToList()).Validate(),
            TallMonsterIds = rt.TallMonsterIds.ToHashSet(),
            LimblessMonsterIds = rt.LimblessMonsterIds.ToHashSet(),
            Kit = rt.Kit,
            KitBow = rt.Classes.ToDictionary(Class, rt.KitBowFor),
            FirstSpell = rt.FirstSpell.ToDictionary(kv => Class(kv.Key), kv => kv.Value),
            SocialClassNames = rt.SocialClasses,
            SocialClassThresholds = rt.SocialClassThresholds,
            SocialClassGoldPerClass = rt.SocialClassGoldPerClass,
            SocialClassBaseRaces = rt.SocialClassBaseRaces
                .Where(coreRaces.ContainsKey)
                .Select(r => Enum.Parse<Race>(coreRaces[r], ignoreCase: true))
                .ToList(),
            SocialClassRollDie = rt.SocialClassRollDie,
            ScoreWeights = rt.ScoreWeights,
            SpellCount = data.Spells.Count,
            SpellNames = data.Spells.Select(s => s.Name).ToList(),
            ScrollItemRecords = data.ScrollItemRecords,
            ArmoryRestock = rt.ArmoryRestock,
            TownEntryDroppedItem = rt.TownEntryDroppedItem,
            Transfer = rt.Transfer,
            CreationAge = rt.CreationAge is { } ca
                ? new P3CreationAgeRule(ca.RollBase, ca.Offset,
                    ca.Factors.Where(kv => coreRaces.ContainsKey(kv.Key))
                        .ToDictionary(kv => Enum.Parse<Race>(coreRaces[kv.Key], ignoreCase: true), kv => kv.Value),
                    ca.Other)
                : null,
        };
    }
}
