using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class SessionCodecTests
{
    private static GameDataRepository Data => GameDataRepository.LoadPhantasie1();

    [OriginalDataFact]
    public void CaptureApply_RoundTripsPartyAndOverworld_FromAnyMode()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Saver", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.BankWithdraw(hero, hero.GoldInBank);
        session.DepartGuildToWilderness();
        session.MoveOverworld(CardinalDirection.East);
        session.Sack.DepositRewards(11, 22, new[] { 46 });

        // Save from overworld — no town/inn gate.
        Assert.Equal(GameMode.Overworld, session.Mode);
        var json = SessionCodec.WriteJson(session.CaptureSnapshot());

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(99));
        loaded.ApplySnapshot(SessionCodec.ReadJson(json));

        Assert.Equal(GameMode.Overworld, loaded.Mode);
        Assert.Equal(session.Overworld.RegionId, loaded.Overworld.RegionId);
        Assert.Equal(session.Overworld.X, loaded.Overworld.X);
        Assert.Equal(session.Overworld.Y, loaded.Overworld.Y);
        Assert.Equal(session.Party.GoldCarried, loaded.Party.GoldCarried);
        Assert.Single(loaded.Party.Members);
        Assert.Equal("Saver", loaded.Party.Members[0].Name);
        Assert.Equal(11, loaded.Sack.Experience);
        Assert.Equal(22, loaded.Sack.Gold);
        Assert.Contains(46, loaded.Sack.ItemIds);
    }

    [OriginalDataFact]
    public void CaptureApply_PreservesDungeonAndCombat()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Dungeon", Race.Elf, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();

        var level = Data.CreateDungeonTraversal().GetLevel(1);
        var (ex, ey) = FindWalkable(level);
        session.EnterDungeon(1, ex, ey);
        session.Dungeon!.Level!.SetBufferByte(1300, 0x42); // a live-buffer rewrite (side table)
        session.DungeonSave = new DungeonSaveSlot(3, Enumerable.Repeat((byte)7, DungeonLevel.BufferSize).ToArray());

        var goblin = Data.Monsters.First(m => m.HpBase <= 8);
        session.BeginDungeonCombat(goblin.Id);
        session.Combat!.RewardXp = 15; // as if a kill had already paid out
        session.Combat.ThreatenFactor = PartyOptions.ThreatenFactorAfterFailedRun;
        session.Combat.MonsterDamaged = true;
        session.Combat.MonstersLoseRound = true;
        session.Combat.SpellEnd = SpellCombatEnd.Ended;
        session.Combat.Dissolved = true;
        session.Combat.DispellUndeadTried = true;
        session.Overworld.TerrainClass = 3;
        foreach (var m in session.Combat!.AllMonsters)
        {
            m.Defense = 0;
            m.HitPoints = 5;
            m.StatusFlags = MonsterStatusFlags.Asleep; // SLEEP lasts across rounds (D-020)
        }

        var snap = session.CaptureSnapshot();
        Assert.Equal(GameMode.Combat, snap.Mode);
        Assert.NotNull(snap.Dungeon);
        Assert.NotNull(snap.Combat);

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(snap))); // through the JSON codec

        Assert.Equal(GameMode.Combat, loaded.Mode);
        Assert.NotNull(loaded.Dungeon);
        Assert.Equal(ex, loaded.Dungeon!.X);
        Assert.Equal(0x42, loaded.Dungeon.Level!.GetBufferByte(1300));
        Assert.NotNull(loaded.DungeonSave);
        Assert.Equal(3, loaded.DungeonSave!.DungeonId);
        Assert.All(loaded.DungeonSave.LevelBuffer, b => Assert.Equal(7, b));
        Assert.NotNull(loaded.Combat);
        Assert.Equal(15, loaded.Combat!.RewardXp);
        Assert.Equal(5, loaded.Combat.AllMonsters.First().HitPoints);
        Assert.All(loaded.Combat.AllMonsters, m => Assert.Equal(MonsterStatusFlags.Asleep, m.StatusFlags));
        Assert.Equal(PartyOptions.ThreatenFactorAfterFailedRun, loaded.Combat.ThreatenFactor);
        Assert.True(loaded.Combat.MonsterDamaged);
        Assert.True(loaded.Combat.MonstersLoseRound);
        Assert.Equal(SpellCombatEnd.Ended, loaded.Combat.SpellEnd);
        Assert.True(loaded.Combat.Dissolved);
        Assert.True(loaded.Combat.DispellUndeadTried);
        Assert.Equal(3, loaded.Overworld.TerrainClass);
    }

    [OriginalDataFact]
    public void CaptureApply_KeepsEffectiveCombatFields_AndOldSavesFallBackToPrep()
    {
        // DOS keeps @246/@248/@250 and the action table until the next prep (0x3263): buffs, SLOWED and
        // armor-reduced survive a mid-combat load.
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Buffed", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginCombat(Data.Monsters.First().Id);

        var prep = session.Combat!.Prep[0];
        var baseArmor = prep.ArmorEff;
        var baseDamage = prep.WeaponDamageEff;
        var baseHit = prep.WeaponHitEff;
        prep.ArmorEff = baseArmor + 10;           // PROTECTION / NINJA
        prep.WeaponDamageEff = baseDamage + 7;    // STRENGTH
        prep.WeaponHitEff = (baseHit * 5 + 5) / 10; // SLOWED (special 21–24 scaling)
        prep.PackedAction = CombatAction.Cleared;

        var snap = session.CaptureSnapshot();
        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(snap)));

        var got = loaded.Combat!.Prep[0];
        Assert.Equal(baseArmor + 10, got.ArmorEff);
        Assert.Equal(baseDamage + 7, got.WeaponDamageEff);
        Assert.Equal((baseHit * 5 + 5) / 10, got.WeaponHitEff);
        Assert.Equal(CombatAction.Cleared, got.PackedAction);

        // A save from before s33 has no Prep: the fields are rebuilt by prep, as before.
        snap.Combat!.Prep = null;
        var old = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        old.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(snap)));
        Assert.Equal(baseArmor, old.Combat!.Prep[0].ArmorEff);
        Assert.Equal(baseDamage, old.Combat.Prep[0].WeaponDamageEff);
        Assert.Equal(baseHit, old.Combat.Prep[0].WeaponHitEff);
    }

    [OriginalDataFact]
    public void ApplySnapshot_RefusesVersion1MidCombatSave()
    {
        // s23 (user): pre-s22 mid-combat saves carry packed-byte monster stats — refuse them; v1 saves outside combat still load.
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Old", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();

        var calm = session.CaptureSnapshot();
        calm.Version = 1;
        GameSessionFactory.Create(Data, new SeededDosRandomSource(1)).ApplySnapshot(calm);

        session.BeginCombat(Data.Monsters.First().Id);
        var fighting = session.CaptureSnapshot();
        fighting.Version = 1;
        Assert.Throws<InvalidDataException>(() =>
            GameSessionFactory.Create(Data, new SeededDosRandomSource(1)).ApplySnapshot(fighting));
    }

    [Fact]
    public void CharacterClone_CopiesInventoryAndSpells()
    {
        var ch = new Character
        {
            Name = "Clone",
            SpellsKnown = Enumerable.Repeat(1, CharacterCreation.SpellCount).ToArray(),
        };
        ch.InventoryItemIds.AddRange(new[] { 1, 2, 3 });
        var copy = ch.Clone();
        copy.InventoryItemIds.Add(99);
        copy.SpellsKnown[0] = 0;
        Assert.DoesNotContain(99, ch.InventoryItemIds);
        Assert.Equal(1, ch.SpellsKnown[0]);
    }

    private static (int X, int Y) FindWalkable(DungeonLevel level)
    {
        for (var y = 0; y < DungeonLevel.Height; y++)
        for (var x = 0; x < DungeonLevel.Width; x++)
        {
            if (level.IsWalkable(x, y))
                return (x, y);
        }

        throw new InvalidOperationException("No walkable cell.");
    }
}
