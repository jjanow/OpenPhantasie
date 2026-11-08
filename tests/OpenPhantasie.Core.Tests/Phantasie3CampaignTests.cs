using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T4: Phantasie III as a playable campaign (<see cref="Phantasie3Campaign"/>) on the shipped
/// <c>GameData/phantasie3</c>: start in Pendragon, creation (social class, kit, first spell, skills), the wilderness
/// (places, terrain, blocked squares, the encounter roll), dungeons (cells, exits to the planes, event programs, the
/// archive altar and event gold options), training, the Mystic's score, the Astral Plane and the gods' reward.
/// </summary>
public class Phantasie3CampaignTests
{
    internal static readonly GameDataRepository Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    internal static GameSession NewGame(IDosRandomSource? rng = null, GameOptions? options = null)
    {
        var s = GameSessionFactory.Create(Data, rng ?? new SeededDosRandomSource(7));
        s.StartNewGame(options);
        return s;
    }

    internal static GameSession WithParty(IDosRandomSource? rng = null, GameOptions? options = null, int members = 4)
    {
        var s = NewGame(rng, options);
        var classes = new[] { CharacterClass.Fighter, CharacterClass.Priest, CharacterClass.Wizard, CharacterClass.Thief,
            CharacterClass.Ranger, CharacterClass.Monk };
        var races = new[] { Race.Human, Race.Dwarf, Race.Elf, Race.Halfling, Race.Gnome, Race.Human };
        s.OpenGuild();
        for (var i = 0; i < members; i++)
        {
            var c = s.CreateAndEnroll("HERO" + i, races[i], classes[i]);
            Assert.True(s.TryAddToParty(c, out var err), err);
        }
        s.LeaveGuild();
        return s;
    }

    internal static Phantasie3Campaign Campaign(GameSession s) => Assert.IsType<Phantasie3Campaign>(s.Campaign);

    [Fact]
    public void Menu_ListsIIIAsBuilt()
    {
        Assert.True(Campaigns.IsBuilt(GameId.P3));
        Assert.Equal(GameId.P3, Campaigns.For(GameId.P3).Game);
        Assert.Throws<InvalidOperationException>(() => Phantasie3Campaign.Unloaded.World);
    }

    [OriginalDataFact]
    public void Elemental_JoinsWithOneCharacter_AndAnotherCanStillJoin()
    {
        Assert.Equal(new ElementalRules(1, BlocksAdd: false, DropKeeps: true), Data.Rules.ElementalRulesOrDefault);
        var s = NewGame();
        s.OpenGuild();
        Assert.False(s.TryAddElemental(0, out var none)); // ovl2 0x3a0a: `core.guild.party_not_assembled`
        Assert.Equal("The party is not assembled.", none);
        var a = s.CreateAndEnroll("HERO0", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(a, out var err), err);
        Assert.True(s.TryAddElemental(0, out err), err);
        Assert.False(s.TryAddElemental(1, out err));
        Assert.Equal(GameText.S("core.guild.elemental_already_joined"), err); // III's table: "An elemental is in the party."
        var b = s.CreateAndEnroll("HERO1", Race.Elf, CharacterClass.Wizard);
        Assert.True(s.TryAddToParty(b, out err), err); // III: Add is not blocked; the elemental stays in slot 7
        Assert.Equal(2, s.Party.Members.Count);
        Assert.NotNull(s.Party.Elemental);
    }

    [Fact]
    public void ElementalRules_PhantasieI_SixFirst_NoAddAfter()
    {
        var party = new Party();
        Assert.False(GuildHall.TryAddElemental(party, new Character { Name = "AIR" }, out var err));
        Assert.Equal(ElementalRules.PhantasieI, party.Rules.ElementalRulesOrDefault);
        Assert.Equal(GameText.S("core.guild.elementals_only_join_six"), err);
    }

    [OriginalDataFact]
    public void NewGame_StartsInPendragon_OnItsScandorSquare()
    {
        var s = NewGame();
        Assert.Equal(GameId.P3, s.Game);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(1, s.CurrentTownId);
        Assert.Equal("Pendragon", s.CurrentTown!.Name);
        Assert.Equal((0, 24, 37), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.Equal(57, Data.Spells.Count);
    }

    [OriginalDataFact]
    public void Creation_GivesSocialClassKitFirstSpellSkillsAndBody()
    {
        var s = WithParty(members: 4);
        var w = Campaign(s).World;
        foreach (var m in s.Party.Members)
        {
            var p3 = m.Phantasie3!;
            Assert.InRange(p3.SocialClass, 0, 3);
            Assert.Equal(P3PartyRank.Front, p3.Rank);
            Assert.Equal(Phantasie3Origin.Scandor, p3.Origin);
            Assert.Equal(P3Training.StartingGold(p3.SocialClass), m.GoldInBank);
            Assert.Equal(57, m.SpellsKnown.Length);
            Assert.Equal(4, m.WornShield);
            Assert.Equal(22, m.WornArmor);
            Assert.Equal(64, m.WornWeapon);
            Assert.Equal(m.Class == CharacterClass.Thief ? 44 : 41, m.WornBow);
            Assert.Equal([4, 22, m.WornBow, 64], m.InventoryItemIds);
            Assert.True(m.HpMax >= 1);
            Assert.True(P3BodyAreas.Of(m).AllOkay);
            Assert.InRange(m.Skills.Attack, 1, 250);
            Assert.InRange(p3.FireBow, 1, 250);
            var first = w.FirstSpell[m.Class];
            if (first > 0)
                Assert.Equal(1, m.SpellsKnown[first - 1]);
        }
        Assert.Equal(1, s.Party.Members.Single(m => m.Class == CharacterClass.Priest).SpellsKnown[0]); // #1 Healing I
        Assert.Equal(1, s.Party.Members.Single(m => m.Class == CharacterClass.Wizard).SpellsKnown[4]); // #5 Fireflash I
    }

    [OriginalDataFact]
    public void LeavingTown_PutsThePartyOnPendragonsSquare_AndWalking_Works()
    {
        var s = WithParty();
        Assert.True(s.LeaveTown());
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal((0, 24, 37), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.Equal(50, s.CurrentOverworldRegion.GridWidth);
        Assert.Equal(75, s.CurrentOverworldRegion.GridHeight);
        // Two steps south is the Pendragon archives (25, 39): east then south twice.
        var r1 = s.MoveOverworld(CardinalDirection.East);
        Assert.NotEqual(OverworldMoveKind.Blocked, r1.Kind);
    }

    [OriginalDataFact]
    public void MapEdges_AreBlocked_OceanOnScandor_RiverStyxOnThePlanes()
    {
        var s = WithParty();
        s.LeaveTown();
        s.Overworld.X = 1;
        s.Overworld.Y = 37;
        var r = s.MoveOverworld(CardinalDirection.West);
        Assert.Equal(OverworldMoveKind.Blocked, r.Kind);
        Assert.Equal((1, 37), (s.Overworld.X, s.Overworld.Y));
        Assert.Equal("Ocean", s.LastMessage);

        s.Overworld.RegionId = Phantasie3Campaign.Netherworld;
        s.Overworld.X = 6;
        s.Overworld.Y = 3;
        Assert.NotEqual(OverworldMoveKind.Blocked, s.MoveOverworld(CardinalDirection.North).Kind); // (6, 2): Black Path
        Assert.Equal(OverworldMoveKind.Blocked, s.MoveOverworld(CardinalDirection.North).Kind); // (6, 1): tile 248
        Assert.Equal("River Styx", s.LastMessage);
    }

    [OriginalDataFact]
    public void Places_AskWillYouEnter_ByName()
    {
        var s = WithParty();
        s.LeaveTown();
        s.Overworld.X = 25;
        s.Overworld.Y = 38;
        var r = s.MoveOverworld(CardinalDirection.South);
        Assert.Equal(OverworldMoveKind.EnterDungeon, r.Kind);
        Assert.Equal("The Pendragon town archives Will you enter?", s.EntryPrompt!.Text);
        Assert.Equal((5, 11, 0), (s.EntryPrompt.Id, s.EntryPrompt.DestX, s.EntryPrompt.DestY));
        s.AnswerOverworldEntry(false);
        s.Overworld.X = 24;
        s.Overworld.Y = 36;
        Assert.Equal(OverworldMoveKind.EnterTown, s.MoveOverworld(CardinalDirection.South).Kind);
        Assert.Equal("Pendragon Will you enter?", s.EntryPrompt!.Text);
        s.AnswerOverworldEntry(true);
        Assert.Equal(1, s.CurrentTownId);
    }

    [OriginalDataFact]
    public void WildernessTables_AreaLevel_AndTheMonsterPick()
    {
        var w = Campaign(NewGame()).World.Wilderness;
        Assert.Equal([3, 4, 5, 4, 3, 2, 2, 3, 2, 3, 2, 2, 0, 3, 3, 2, 1, 1, 1, 2, 3, 2, 1, 1, 2], w.AreaLevels);
        Assert.Equal(0, w.AreaLevel(24, 37)); // Pendragon's section (2, 2)
        Assert.Equal(3, w.AreaLevel(0, 0));
        Assert.Equal((8, 38, 26, 37), (w.Chance, w.NightBelow, w.Kind2Below, w.SleepingBelow));
        // Kind 1 (night): 12L + {0, 1, 4}; rnd(3) = 3 gives 4.
        Assert.Equal(12 * 2 + 4, w.PickMonster(1, 2, 0, new Fixed(2)));
        // Kind 3: 12L + offsets[t + rnd(4)]; meadows (0) with rnd(4) = 1 → offsets[1] = 4.
        Assert.Equal(4, w.PickMonster(3, 0, 0, new Fixed(0)));
        // Out of range ids become 11; 61 becomes 60.
        Assert.Equal(11, w.PickMonster(2, 6, 0, new Fixed(9)));
        Assert.Equal(60, w.PickMonster(3, 5, 0, new Fixed(1))); // 60 + offsets[0 + 2] = 61 → 60
    }

    /// <summary>Always returns <c>minInclusive + offset</c> (clamped): <c>Rnd(n)</c> = offset + 1.</summary>
    private sealed class Fixed(int offset) : IDosRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => Math.Min(minInclusive + offset, maxExclusive - 1);

        public int NextDosWord() => offset;
    }

    [OriginalDataFact]
    public void WildernessInn_Costs250_AndRefusesAPoorParty()
    {
        var s = WithParty();
        s.LeaveTown();
        s.Overworld.X = 24; // The Mystic Inn (24, 20)
        s.Overworld.Y = 21;
        Assert.Equal(OverworldMoveKind.WildernessInn, s.MoveOverworld(CardinalDirection.North).Kind);
        s.Party.GoldCarried = 100;
        Assert.Equal(TownTransactionKind.Failed, s.StayAtWildernessInn().Kind);
        s.Party.GoldCarried = 1000;
        s.Party.Members[0].HpCur = 1;
        Assert.Equal(TownTransactionKind.Ok, s.StayAtWildernessInn().Kind);
        Assert.Equal(750, s.Party.GoldCarried);
        Assert.Equal(s.Party.Members[0].HpMax, s.Party.Members[0].HpCur);
    }

    [OriginalDataFact]
    public void D3032_SkillTables_IsValuesByDefault_DosAsReadAsOption()
    {
        var w = Campaign(NewGame()).World;
        Assert.Equal(250, w.Training.SkillTargets[P3Skill.Attack][CharacterClass.Ranger][14]);
        Assert.Equal(25, w.TrainingAsRead.SkillTargets[P3Skill.Attack][CharacterClass.Ranger][14]);
        Assert.Equal(55, w.Training.SkillTargets[P3Skill.PickLock][CharacterClass.Thief][2]);
        Assert.Equal(35, w.TrainingAsRead.SkillTargets[P3Skill.PickLock][CharacterClass.Thief][2]);
        Assert.Equal(110, w.Training.SkillTargets[P3Skill.Parry][CharacterClass.Monk][14]);
        Assert.Equal(40, w.TrainingAsRead.SkillTargets[P3Skill.Parry][CharacterClass.Monk][14]);
        Assert.Same(w.Training, w.TrainingFor(GameOptions.ForPreset(GameId.P3)));
        Assert.Same(w.TrainingAsRead, w.TrainingFor(GameOptions.ForPreset(GameId.P3, OptionPreset.Dos)));
    }

    [OriginalDataFact]
    public void FinalBosses_AreBuiltAlone_WithTheirBigWords()
    {
        var s = WithParty();
        s.LeaveTown();
        var enc = s.BeginWildernessCombat(Phantasie3Campaign.NikademusB);
        var boss = Assert.Single(Assert.Single(enc.Ranks).Members);
        Assert.Equal(100 * 209 - 20000, boss.HitPoints); // byte 25 = 209 → 900 HP
        Assert.Equal((64300 - 64000) * 1000, boss.ExperienceValue);
        Assert.Equal((64099 - 64000) * 1000, enc.RewardGold);
        Assert.Equal(5, Phantasie3Campaign.MonsterHp(5));
    }

    /// <summary>s158 (Amiga <c>0x19c66</c>, FORMAT.md 7.2): the party getting away plays the driver's stop 33 first.</summary>
    [OriginalDataFact]
    public void EncounterOptions_PartyGetsAway_PlaysTheStop()
    {
        var rng = new Switchable(3);
        var s = WithParty(rng);
        s.LeaveTown();
        s.BeginWildernessCombat(0); // a Skeleton
        rng.Low = true;
        var before = s.SoundSequence;
        var flee = s.ChoosePartyOption(PartyOption.Flee);
        Assert.Equal(PartyOptionOutcome.CombatOver, flee.Outcome);
        Assert.Contains("The party gets away!", s.Log);
        Assert.Equal([P3Sounds.Stop], s.SoundsSince(before));
    }

    [OriginalDataFact]
    public void EncounterOptions_FleeFromAFinalBoss_IsBlocked_MercyTakesAllGoldAndNewItems()
    {
        var s = WithParty();
        s.LeaveTown();
        s.BeginWildernessCombat(Phantasie3Campaign.LordWood);
        var flee = s.ChoosePartyOption(PartyOption.Flee);
        Assert.Equal(PartyOptionOutcome.LoseRound, flee.Outcome);
        Assert.Contains("You don't get away...", flee.Log);

        Assert.DoesNotContain(P3Sounds.Stop, s.SoundsSince(0)); // `core.p3.you_dont_get_away` plays nothing

        var rng = new Switchable(3);
        var t = WithParty(rng);
        t.LeaveTown();
        t.Party.GoldCarried = 500;
        t.Sack.Gold = 40;
        t.Sack.ItemIds.Add(101);
        t.BeginWildernessCombat(0); // a Skeleton: reaction −10, Mercy at rnd(100) < 50
        rng.Low = true; // every rnd(n) = 1
        var mercy = t.ChoosePartyOption(PartyOption.BegMercy);
        Assert.Equal(PartyOptionOutcome.CombatOver, mercy.Outcome);
        Assert.Contains("They take your gold!", t.Log);
        Assert.Equal(0, t.Party.GoldCarried + t.Sack.Gold);
        Assert.DoesNotContain(101, t.Sack.ItemIds); // D3-024: rnd(100) = 1 ≤ 50 takes the new item
        Assert.Contains("They take some of your new items!", t.Log);
        Assert.NotEqual(GameMode.Combat, t.Mode);
    }

    /// <summary>A seeded source that can be switched to always give the lowest value (<c>rnd(n)</c> = 1).</summary>
    internal sealed class Switchable(int seed) : IDosRandomSource
    {
        private readonly SeededDosRandomSource _inner = new(seed);

        public bool Low { get; set; }

        public int Next(int minInclusive, int maxExclusive) => Low ? minInclusive : _inner.Next(minInclusive, maxExclusive);

        public int NextDosWord() => Low ? 0 : _inner.NextDosWord();
    }

    [OriginalDataFact]
    public void TownReentry_RestocksWeaponsAndPotions_DosKeepsTheStock()
    {
        foreach (var dos in new[] { false, true })
        {
            var s = WithParty(options: dos ? GameOptions.ForPreset(GameId.P3).With("p3.armory-restock", "dos") : null);
            var before = s.CurrentTown!.Stock.ToArray();
            Assert.Contains(before.Skip(1).Take(100), n => n > 0); // shields, armour, bows or weapons in Pendragon
            s.LeaveTown();
            s.Overworld.Y = 36;
            s.MoveOverworld(CardinalDirection.South);
            s.AnswerOverworldEntry(true);
            Assert.Equal(1, s.CurrentTownId);
            var after = s.CurrentTown!.Stock;
            if (dos)
                Assert.Equal(before, after);
            else
            {
                Assert.NotEqual(before, after); // D3-033: Apple II's churn
                Assert.Equal(before.Skip(111), after.Skip(111)); // magic potions, scrolls and treasures never wiped …
                Assert.Equal(0, after[172]); // … and the Light Key is never stocked
            }
        }
    }

    [OriginalDataFact]
    public void TravelTime_CountsThirdsOfADay_TownEntryAgesPer14Days()
    {
        var s = WithParty();
        s.LeaveTown();
        Assert.Equal(0, s.TravelTime);
        s.MoveOverworld(CardinalDirection.North); // leaving Pendragon's square: ⅓ day
        Assert.Equal(1, s.TravelTime);
        var ages = s.Party.Members.Select(m => m.AgeClock).ToArray();
        s.TravelTime = 3 * 14 * 2 + 2; // 28 days out
        s.Party.Members[0].Status = CharacterStatus.Dead;
        s.Overworld.Y = 36;
        s.Overworld.X = 24;
        s.MoveOverworld(CardinalDirection.South);
        s.AnswerOverworldEntry(true);
        Assert.Equal(0, s.TravelTime);
        Assert.Equal(ages[0], s.Party.Members[0].AgeClock); // the dead do not age (ovl3 0x65a)
        Assert.Equal(ages[1] + 2, s.Party.Members[1].AgeClock);
    }

    [OriginalDataFact]
    public void Transportation_ListsTheScandorTowns()
    {
        var s = NewGame();
        Assert.Equal(Enumerable.Range(1, 14), s.Campaign.TransportTowns); // D3-035: never to the plane towns 15–17
        s.EnterTown(16); // Dark: the party stands on the Dark plane's town square
        Assert.Equal((Phantasie3Campaign.DarkPlane, 4, 1), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    [OriginalDataFact]
    public void IIIsOwnSpells_AreRefused_HealingCuresTheBody()
    {
        var s = WithParty();
        var priest = s.Party.Members.Single(m => m.Class == CharacterClass.Priest);
        priest.MagicCur = priest.MagicMax = 20;
        priest.SpellsKnown[12] = 1; // #13 Arrow Flame I
        var arrow = s.CastSpell(priest, 12);
        Assert.False(arrow.Success);
        Assert.Equal(20, priest.MagicCur);

        var fighter = s.Party.Members[0];
        var body = P3BodyAreas.Of(fighter);
        body[P3BodyArea.LeftLeg] = P3AreaState.Broken;
        fighter.BodyCondition = body.ToCondition();
        fighter.Status = CharacterStatus.Down;
        priest.SpellsKnown[SpellId.HealingIi] = 1;
        var heal = s.CastSpell(priest, SpellId.HealingIi, fighter); // tier II: 2 cure points
        Assert.True(heal.Success);
        Assert.True(P3BodyAreas.Of(fighter).AllOkay);
        Assert.Equal(CharacterStatus.Ok, fighter.Status);
    }

    [OriginalDataFact]
    public void TheMystic_RanksByTheScore()
    {
        var s = WithParty();
        Assert.Equal("You are novice adventurers", s.ConsultMystic().Message);
        foreach (var m in s.Party.Members)
            m.P3.QuestBits = 0xff; // 116 + level / 6
        Assert.Equal("You are expert adventurers", s.ConsultMystic().Message);
        s.Party.Members[0].P3.Phantasie1Score = 120;
        Assert.Equal("You are expert adventurers and heroes of Gelnor", s.ConsultMystic().Message);
    }
}
