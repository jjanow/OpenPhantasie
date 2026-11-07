using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// M8 — automated start-to-finish playthrough exercising guild → town →
/// overworld → combat → loot distribute → dungeon entry against Core rules.
/// </summary>
public class PlaythroughParityTests
{
    private static GameDataRepository Data => GameDataRepository.LoadPhantasie1();
    private static CharacterCreationTables Tables => Data.CreationTables;

    [OriginalDataFact]
    public void FullLoop_CreateParty_Town_WildernessFight_Distribute_EnterDungeon()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(2026));
        session.OpenGuild();

        var fighter = session.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        var priest = session.CreateAndEnroll("Healer", Race.Elf, CharacterClass.Priest);
        Assert.True(session.TryAddToParty(fighter, out _));
        Assert.True(session.TryAddToParty(priest, out _));
        Assert.Equal(2, session.Party.Members.Count);

        // Withdraw starting gold and visit Pelnor (town id 1).
        Assert.Equal(TownTransactionKind.Ok,
            session.BankWithdraw(fighter, RulesProfile.PhantasieI.StartingGold).Kind);
        Assert.Equal(TownTransactionKind.Ok,
            session.BankWithdraw(priest, RulesProfile.PhantasieI.StartingGold).Kind);
        Assert.Equal(512, session.Party.GoldCarried);

        session.EnterTown(1);
        Assert.Equal(GameMode.Town, session.Mode);
        Assert.NotNull(session.CurrentTown);

        // Buy a dagger if stocked (item 46); ignore failure if not.
        var goldBeforeBuy = session.Party.GoldCarried;
        var buy = session.Buy(fighter, 46);
        if (buy.Kind == TownTransactionKind.Ok)
            Assert.Contains(46, fighter.InventoryItemIds);

        var mystic = session.ConsultMystic();
        Assert.Equal(TownTransactionKind.Ok, mystic.Kind);
        // base = fighter.Level(1) + priest.Level(1) = 2; score = (2+1)/3 = 1.
        Assert.Equal(1, fighter.Score);
        Assert.Equal(1, priest.Score);

        session.RestAtInn();
        session.LeaveTown();
        Assert.Equal(GameMode.Overworld, session.Mode);
        Assert.Equal(OverworldState.NewGameRegionId, session.Overworld.RegionId);

        // Scripted low-HP fight (goblin-class) so auto-melee finishes quickly.
        var goblin = Data.Monsters.First(m => m.HpBase <= 8);
        var enc = session.BeginWildernessCombat(goblin.Id);
        // Gold is summed at build; XP comes per kill (OV2 0x3a97 / 0x1656).
        var expectedGold = enc.AllMonsters.Sum(m => m.Gold);
        var expectedXp = enc.AllMonsters.Sum(m => m.ExperienceValue);
        Assert.Equal(expectedGold, enc.RewardGold);
        Assert.Equal(0, enc.RewardXp);
        // Soften defense so thrusts connect under seeded RNG.
        foreach (var m in session.Combat!.AllMonsters)
        {
            m.Defense = 0;
            m.Armor = 0;
            m.AttackPower = 0; // the build may field several SKELETON groups; keep the scripted fight one-sided
            m.HitPoints = Math.Min(m.HitPoints ?? 5, 5);
        }

        foreach (var ch in session.Party.Members)
        {
            ch.Skills = ch.Skills with { Attack = Math.Max(ch.Skills.Attack, 40) };
            ch.WeaponDamageA = Math.Max(ch.WeaponDamageA, 20);
        }

        session.Combat.Prepare();
        var fight = session.FightToEnd();
        Assert.True(fight.EncounterOver);
        Assert.False(fight.PartyWiped);
        // Won outright, or the survivors ran (morale check, OV2 0x3b98): full gold either way, XP only for kills.
        if (!fight.PartyWon)
        {
            Assert.Contains("MONSTERS RUN", session.Log);
            expectedXp = fight.RewardXp;
        }
        // End of combat (D-053, manual): the whole XP and gold go into the sack; the split waits for town.
        Assert.Equal(expectedXp, session.Sack.Experience);
        Assert.Equal(expectedGold, session.Sack.Gold);
        Assert.Equal(GameMode.Overworld, session.Mode);

        var goldBeforeDist = session.Party.GoldCarried;
        session.EnterTown(1);
        var dist = session.DistributeLoot();
        Assert.Equal(TownTransactionKind.Ok, dist.Kind);
        Assert.True(session.Sack.IsEmpty);
        // Remainder goes to the first survivor (TownMenus.Distribute).
        Assert.Equal(expectedXp / 2 + expectedXp % 2, fighter.Experience);
        Assert.Equal(expectedXp / 2, priest.Experience);
        Assert.Equal(goldBeforeDist + expectedGold, session.Party.GoldCarried);
        _ = goldBeforeBuy; // buy may have spent gold; distribute assertion uses pre-dist purse

        session.LeaveTown();

        // Walk from new-game spawn toward the known OUT1 dungeon at (9,8).
        // Spawn is (20,5); move west/north-ish — use absolute path via state set
        // for the entrance step (parity of EnterDungeon itself is what matters).
        session.Overworld.X = 8;
        session.Overworld.Y = 8;
        var step = session.MoveOverworld(CardinalDirection.East);
        Assert.Equal(OverworldMoveKind.EnterDungeon, step.Kind);
        session.AnswerOverworldEntry(enter: true); // "A DUNGEON. ENTER?"
        Assert.Equal(GameMode.Dungeon, session.Mode);
        Assert.NotNull(session.Dungeon);
        Assert.Equal(1, session.Dungeon!.LevelId);

        // One dungeon step that doesn't have to change mode.
        session.TurnDungeonRight();
        var dmove = session.MoveDungeonForward();
        Assert.True(
            dmove.Kind is DungeonMoveKind.Moved
                or DungeonMoveKind.BlockedWall
                or DungeonMoveKind.TriggerTrap
                or DungeonMoveKind.TriggerMessage
                or DungeonMoveKind.TriggerLock
                or DungeonMoveKind.TriggerStairs
                or DungeonMoveKind.TriggerCombat
                or DungeonMoveKind.TriggerEncounter
                or DungeonMoveKind.ScriptedEvent
                or DungeonMoveKind.ExitDungeon);

        // Guild JSON save round-trip for the roster.
        var json = GuildCodec.WriteJson(session.Roster.Occupied);
        var reloaded = GuildCodec.ReadJson(json);
        Assert.Equal(2, reloaded.Count);
        Assert.Contains(reloaded, c => c.Name == "Hero" && c.Experience == fighter.Experience);
    }

    [OriginalDataFact]
    public void Experience_S32RoundTrip_SupportsValuesAbove65535()
    {
        var ch = CharacterCreation.Create(
            new CharacterCreationRequest
            {
                Name = "veteran",
                Race = Race.Human,
                Class = CharacterClass.Fighter,
            },
            Data.CreationTables,
            new SeededDosRandomSource(1));
        ch.Experience = 100_000;

        var bytes = GuildCodec.WriteBinary([ch]);
        var loaded = GuildCodec.ReadBinary(bytes, Tables)[0]!;
        Assert.Equal(100_000, loaded.Experience);
    }

    [OriginalDataFact]
    public void Quirks_Q1_GoldCappedAtManual65000_D048()
    {
        // Q1 FIX undone (D-048, user s40): the manual's 65,000 cap; DOS's is 65,535. Excess lost in both.
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        var hero = session.CreateAndEnroll("Rich", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.EnterTown(1);
        hero.GoldInBank = 60_000;
        session.Party.GoldCarried = 10_000;
        var dep = session.BankDeposit([1]); // D-049: share split
        Assert.Equal(TownTransactionKind.Ok, dep.Kind);
        Assert.Equal(0, session.Party.GoldCarried);
        Assert.Equal(RulesProfile.PhantasieI.BankGoldCap, hero.GoldInBank);
    }
}
