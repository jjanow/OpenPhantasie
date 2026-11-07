using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// End-of-combat treasure, OV2 <c>0x40e4..0x41f6</c> / <c>0x421d</c> (combat-resolution.md §Outputs / rewards), with the
/// remaster's D-031 (wilderness always drops, manual) and the SCROLL 17 guard on the shifted item (user, s34).
/// </summary>
public class TreasureDropTests
{
    private const int ItemCount = 181;

    /// <summary>Hands out exactly the scripted 0..99 rolls; any extra draw fails the test (pins DOS draw order).</summary>
    private sealed class StrictRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public StrictRandomSource(params int[] values) => _values = new Queue<int>(values);

        public int Remaining => _values.Count;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.NotEmpty(_values);
            return _values.Dequeue();
        }
    }

    private static Character Member(string name, int findItem, CharacterStatus status = CharacterStatus.Ok) => new()
    {
        Name = name, Class = CharacterClass.Thief, Status = status, HpCur = 10, HpMax = 10,
        Skills = new CharacterSkills(0, 0, findItem, 0, 0, 0, 0, 0),
    };

    private static CombatEncounter Encounter(int[] treasure, int level = 2, int firstMonster = 10, params Character[] members)
    {
        var party = new Party();
        foreach (var m in members.Length > 0 ? members : new[] { Member("A", 0) })
            party.Members.Add(m);
        var enc = new CombatEncounter(party) { TreasureItemIds = treasure, MonsterLevel = level, FirstMonsterId = firstMonster };
        enc.AddRank(new Monster { Name = "M", HitPoints = 1 });
        return enc;
    }

    [Theory]
    [InlineData(0, 0, new[] { 53, 63 })]    // rand/20 = 0 → id + 3
    [InlineData(99, 99, new[] { 49, 59 })]  // rand/20 = 4 → id − 1
    [InlineData(40, 60, new[] { 51, 60 })]
    public void Wilderness_AlwaysDrops_NoPresenceRoll_D031(int shiftA, int shiftB, int[] expected)
    {
        // DOS would draw rand ≤ 2·L + 20 first; the remaster draws only the two shift rolls.
        var rng = new StrictRandomSource(shiftA, shiftB);
        var offers = TreasureDrop.Roll(Encounter([50, 60]), dungeonLevelId: null, ItemCount, rng, new List<string>());
        Assert.Equal(expected, offers);
        Assert.Equal(0, rng.Remaining);
    }

    [Fact]
    public void Forfeited_OrNoIds_NothingAndNoRolls()
    {
        var enc = Encounter([50, 60]);
        enc.TreasureForfeited = true; // BEG / GREET / FLEE / TELEPORTATION / CHARM
        Assert.Empty(TreasureDrop.Roll(enc, null, ItemCount, new StrictRandomSource(), new List<string>()));
        Assert.Empty(TreasureDrop.Roll(Encounter([0, 0]), null, ItemCount, new StrictRandomSource(), new List<string>()));
    }

    [Fact]
    public void OneIdZero_StillDrawsBothShiftRolls()
    {
        var rng = new StrictRandomSource(0, 0);
        Assert.Equal(new[] { 138 }, TreasureDrop.Roll(Encounter([0, 135]), null, ItemCount, rng, new List<string>()));
        Assert.Equal(0, rng.Remaining);
    }

    [Theory]
    [InlineData(20, null, false)]  // FIRE ELEMENTAL 135 + 3 − 1 = 137 in the wilderness: blocked (DOS gave it)
    [InlineData(39, null, false)]
    [InlineData(20, 4, false)]     // another dungeon level
    [InlineData(20, 9, true)]      // Fortress of the Black Knights: allowed
    public void WinScroll_GuardTestsTheShiftedItem(int shiftRoll, int? dungeonLevel, bool dropped)
    {
        Assert.Equal(dropped ? 137 : null,
            TreasureDrop.Drop(135, shift: true, dungeonLevel, ItemCount, new StrictRandomSource(shiftRoll)));
    }

    [Fact]
    public void ScriptDrop_NoShift_WinScrollOnlyInFortress()
    {
        Assert.Null(TreasureDrop.Drop(137, shift: false, 3, ItemCount, new StrictRandomSource()));
        Assert.Equal(137, TreasureDrop.Drop(137, shift: false, 9, ItemCount, new StrictRandomSource()));
    }

    [Theory]
    [InlineData(1, 99, null)]   // id 1 shifts to 0 — an empty slot [~]
    [InlineData(178, 0, null)]  // 181 is past the item table [~]
    [InlineData(178, 20, 180)]
    public void Drop_ShiftedOutsideTheItemTable_IsNothing(int id, int roll, int? expected)
    {
        Assert.Equal(expected, TreasureDrop.Drop(id, shift: true, null, ItemCount, new StrictRandomSource(roll)));
    }

    [OriginalDataFact]
    public void Dungeon_PresenceRoll_ThenFirstNonDeadFinder()
    {
        // Presence: rand ≤ 2·L + 50 = 54. Finders in party order, DEAD skipped, stop at the first success.
        var dead = Member("DEAD", 99, CharacterStatus.Dead);
        var miss = Member("MISS", 10);
        var spot = Member("SPOT", 40);
        var late = Member("LATE", 99);
        var rng = new StrictRandomSource(54, 11, 40, 0, 0); // presence, MISS fails, SPOT finds, two shift rolls
        var log = new List<string>();
        var offers = TreasureDrop.Roll(Encounter([50, 60], 2, 10, dead, miss, spot, late), 3, ItemCount, rng, log);
        Assert.Equal(new[] { 53, 63 }, offers);
        Assert.Equal(new[] { "SPOT SPOTS AN ITEM" }, log);
        Assert.Equal(0, rng.Remaining);
    }

    [Fact]
    public void Dungeon_PresenceFails_OrNobodyFinds_Nothing()
    {
        Assert.Empty(TreasureDrop.Roll(Encounter([50, 60], 2), 3, ItemCount, new StrictRandomSource(55), new List<string>()));
        var rng = new StrictRandomSource(0, 1);
        Assert.Empty(TreasureDrop.Roll(Encounter([50, 60], 2, 10, Member("A", 0)), 3, ItemCount, rng, new List<string>()));
        Assert.Equal(0, rng.Remaining);
    }

    [Fact]
    public void Dungeon_Surrender_AlwaysDrops_EvenIfNobodyFinds()
    {
        var enc = Encounter([50, 60], 2, 10, Member("A", 0));
        enc.SurrenderTreasure = true; // [0x74b2]
        var rng = new StrictRandomSource(99, 50, 0, 0); // presence 99 ≤ 45 + 54 → yes, finder fails, two shifts
        Assert.Equal(new[] { 53, 63 }, TreasureDrop.Roll(enc, 3, ItemCount, rng, new List<string>()));
        Assert.Equal(0, rng.Remaining);
    }

    [Fact]
    public void Dungeon_BlackLord_NoTreasure_NoRolls()
    {
        var enc = Encounter([50, 60], 21, TreasureDrop.BlackLordId);
        Assert.Empty(TreasureDrop.Roll(enc, 10, ItemCount, new StrictRandomSource(), new List<string>()));
    }

    [Fact]
    public void Dungeon_NoDropRule_IsTheCampaigns()
    {
        Assert.True(Phantasie1Campaign.Instance.WithholdsDungeonTreasure(Phantasie1Campaign.BlackLordId));
        Assert.False(Phantasie1Campaign.Instance.WithholdsDungeonTreasure(Phantasie1Campaign.BlackLordId - 1));

        // A campaign that withholds nothing: the Black Lord's fight rolls like any other (same draws as the surrender case).
        var lord = Encounter([50, 60], 2, Phantasie1Campaign.BlackLordId, Member("A", 0));
        lord.SurrenderTreasure = true;
        var rng = new StrictRandomSource(99, 50, 0, 0);
        Assert.Equal(new[] { 53, 63 }, TreasureDrop.Roll(lord, 3, ItemCount, rng, new List<string>(), withholds: _ => false));
        Assert.Equal(0, rng.Remaining);

        // One that withholds every fight: no rolls at all.
        Assert.Empty(TreasureDrop.Roll(
            Encounter([50, 60], 21), 10, ItemCount, new StrictRandomSource(), new List<string>(), withholds: _ => true));
    }

    // --- Session: KEEP IT? -------------------------------------------------------------

    private static GameSession WildernessFightEndingBySpell(out GameDataRepository data)
    {
        data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Porter", Race.Human, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);
        session.Combat!.TreasureItemIds = [50, 60];
        session.Combat.SpellEnd = SpellCombatEnd.Ended; // e.g. a FEAR run: treasure kept
        return session;
    }

    [OriginalDataFact]
    public void Session_OffersEachItem_KeepNo_AndSaveLoadKeepsThePrompt()
    {
        var session = WildernessFightEndingBySpell(out var data);
        Assert.Equal(MoraleOutcome.EndedBySpell, session.StartCombatRound());
        Assert.Equal(2, session.PendingTreasure.Count);
        var first = session.PendingTreasure[0];
        var second = session.PendingTreasure[1];
        Assert.InRange(first, 49, 53);
        Assert.InRange(second, 59, 63);
        Assert.Equal($"YOU FIND {data.Items.First(i => i.Id == first).Name}. KEEP IT?", session.TreasurePrompt);

        Assert.True(session.AnswerTreasure(keep: true));
        Assert.Equal(new[] { first }, session.Sack.ItemIds);

        var loaded = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot())));
        Assert.Equal(new[] { second }, loaded.PendingTreasure);

        Assert.False(loaded.AnswerTreasure(keep: false));
        Assert.Empty(loaded.PendingTreasure);
        Assert.Null(loaded.TreasurePrompt);
        Assert.Equal(new[] { first }, loaded.Sack.ItemIds);
        Assert.Throws<InvalidOperationException>(() => loaded.AnswerTreasure(true));
    }

    [OriginalDataFact]
    public void Session_FullSack_LosesTheItem()
    {
        var session = WildernessFightEndingBySpell(out _);
        for (var i = 0; i < LootSack.MaxItems; i++)
            session.Sack.ItemIds.Add(1);
        session.StartCombatRound();
        Assert.False(session.AnswerTreasure(keep: true));
        Assert.Contains("Sack is full!!!", session.Log);
        Assert.Equal(LootSack.MaxItems, session.Sack.ItemIds.Count);
    }

    [OriginalDataFact]
    public void Session_Teleport_ForfeitsTreasure()
    {
        var session = WildernessFightEndingBySpell(out _);
        session.Combat!.SpellEnd = SpellCombatEnd.Teleported;
        session.StartCombatRound();
        Assert.Empty(session.PendingTreasure);
    }
}
