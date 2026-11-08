using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T1.4: the shared engine (<see cref="GameSession"/>) calls the campaign (<see cref="ICampaign"/>) for each game's
/// own content, and the campaign's game is the session's single "which game" source.
/// </summary>
public class CampaignSplitTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    /// <summary>Phantasie I with every hook counted, behaviour unchanged.</summary>
    private sealed class CountingCampaign(GameId game = GameId.P1) : ICampaign
    {
        private readonly ICampaign _inner = Phantasie1Campaign.Instance;
        public readonly Dictionary<string, int> Calls = new();

        private void Count(string hook) => Calls[hook] = Calls.GetValueOrDefault(hook) + 1;

        public GameId Game => game;
        public FeatureModuleSet DefaultFeatureModules => _inner.DefaultFeatureModules;
        public IMemberDeathRule Death => _inner.Death;
        public int StartTownId => _inner.StartTownId;
        public IReadOnlyList<int> TransportTowns => _inner.TransportTowns;
        public bool IsUnnamedTransportTown(int townId) => _inner.IsUnnamedTransportTown(townId);
        public IReadOnlyDictionary<int, int> OverworldTownAliases => _inner.OverworldTownAliases;
        public string BlockedMessage(int regionId) => _inner.BlockedMessage(regionId);
        public IReadOnlyList<int> FallbackMonsterPool => _inner.FallbackMonsterPool;
        public bool MayDropItem(int itemId, int? dungeonLevelId) => _inner.MayDropItem(itemId, dungeonLevelId);
        public bool WithholdsDungeonTreasure(int firstMonsterId) => _inner.WithholdsDungeonTreasure(firstMonsterId);
        public TownTransactionResult ConsultMystic(GameSession session) => _inner.ConsultMystic(session);

        public void OnSpecialSite(GameSession session, OverworldMoveResult result)
        {
            Count(nameof(OnSpecialSite));
            _inner.OnSpecialSite(session, result);
        }

        public void BeginDungeonEvent(GameSession session, DungeonMoveResult move)
        {
            Count(nameof(BeginDungeonEvent));
            _inner.BeginDungeonEvent(session, move);
        }

        public void AnswerDungeonEvent(GameSession session, int choice)
        {
            Count(nameof(AnswerDungeonEvent));
            _inner.AnswerDungeonEvent(session, choice);
        }

        public void ResumeDungeonScript(GameSession session)
        {
            Count(nameof(ResumeDungeonScript));
            _inner.ResumeDungeonScript(session);
        }

        public bool IsBlackKnightSpot(int dungeonLevelId)
        {
            Count(nameof(IsBlackKnightSpot));
            return _inner.IsBlackKnightSpot(dungeonLevelId);
        }

        public void AfterCombatSurvived(GameSession session, CombatEncounter encounter, CombatResult result)
        {
            Count(nameof(AfterCombatSurvived));
            _inner.AfterCombatSurvived(session, encounter, result);
        }

        public PartyDefeatResult ResolvePartyDefeat(GameSession session)
        {
            Count(nameof(ResolvePartyDefeat));
            return _inner.ResolvePartyDefeat(session);
        }
    }

    /// <summary>A death step that only sets DEAD (no CON loss), to see the engine route through the rule.</summary>
    private sealed class PlainDeath : IMemberDeathRule
    {
        public int Calls;

        public void ApplyDeath(Character member)
        {
            Calls++;
            member.Status = CharacterStatus.Dead;
        }
    }

    private static GameSession SessionFacingTheBlackLord(ICampaign campaign, out Character hero)
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(9), campaign: campaign);
        session.OpenGuild();
        hero = session.CreateAndEnroll("Victor", Race.Human, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.EnterDungeon(9, 15, 0);
        session.BeginDungeonCombat(Endgame.BlackLordId, SurpriseFlags.Normal);
        return session;
    }

    [OriginalDataFact]
    public void DefaultSession_IsThePhantasieICampaign_WithItsModulesAndDeathStep()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        Assert.Same(Phantasie1Campaign.Instance, session.Campaign);
        Assert.Same(Phantasie1Campaign.Instance, Campaigns.For(GameId.P1));
        Assert.Equal(GameId.P1, session.Game);
        Assert.Equal(Data.Game, session.Game);
        Assert.Equal(session.Game, session.Rules.Game);
        Assert.Same(FeatureModuleSet.PhantasieI, session.FeatureModules);
        Assert.Same(PhantasieIDeathRule.Instance, session.Party.Death);
        Assert.Same(PhantasieIDeathRule.Instance, new Party().Death);
        Assert.Equal(GameId.P1, session.CaptureSnapshot().Game);
        Assert.Equal(GameSession.BlackKnightFortressLevel, Phantasie1Campaign.BlackKnightFortressLevel);
    }

    [Fact]
    public void UnbuiltCampaigns_AreRefused()
    {
        Assert.Same(Phantasie2Campaign.Instance, Campaigns.For(GameId.P2)); // built in T3
        Assert.Throws<NotSupportedException>(() => Campaigns.For(GameId.P4));
    }

    [OriginalDataFact]
    public void DataOfAnotherGame_IsRefused()
    {
        var ex = Assert.Throws<InvalidDataException>(
            () => GameSessionFactory.Create(Data, new SeededDosRandomSource(1), campaign: new CountingCampaign(GameId.P2)));
        Assert.Contains("p1", ex.Message);
        Assert.Contains("p2", ex.Message);
    }

    [OriginalDataFact]
    public void RulesOfAnotherGame_AreRefused()
    {
        var rules = RulesProfile.PhantasieI with { Game = GameId.P3 };
        Assert.Throws<ArgumentException>(
            () => GameSessionFactory.Create(Data, new SeededDosRandomSource(1), rules: rules));
    }

    [OriginalDataFact]
    public void FeatureModulesOfAnotherGame_AreRefused()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        Assert.Throws<ArgumentException>(
            () => session.FeatureModules = FeatureModuleSet.Create(GameId.P2, new P1CombatOrdersModule()));
        Assert.Same(FeatureModuleSet.PhantasieI, session.FeatureModules);
    }

    [OriginalDataFact]
    public void Victory_GoesThroughTheCampaign()
    {
        var campaign = new CountingCampaign();
        var session = SessionFacingTheBlackLord(campaign, out _);
        Assert.Equal(1, campaign.Calls[nameof(ICampaign.IsBlackKnightSpot)]);
        foreach (var m in session.Combat!.AllMonsters)
            m.HitPoints = 0;
        session.ResolveCombatRound();

        Assert.Equal(1, campaign.Calls[nameof(ICampaign.AfterCombatSurvived)]);
        Assert.Equal(EndgameOutcome.Victory, session.LastEndgame);
        Assert.Equal(GameMode.Town, session.Mode);
        Assert.Null(session.Dungeon);
    }

    [OriginalDataFact]
    public void PartyDefeat_EngineClears_ThenTheCampaignJudges()
    {
        var campaign = new CountingCampaign();
        var session = SessionFacingTheBlackLord(campaign, out var hero);
        hero.Level = 6;
        hero.HpCur = 0;
        hero.Status = CharacterStatus.Dead;
        session.ResolveCombatRound();
        Assert.True(session.PartyDefeatPending);
        Assert.True(session.LastFightLost); // s163: II ST's sound 17 skips the next fight
        Assert.False(campaign.Calls.ContainsKey(nameof(ICampaign.AfterCombatSurvived))); // a wipe is never a win

        session.TravelTime = 5000;
        var result = session.ResolvePartyDefeat();
        Assert.Equal(1, campaign.Calls[nameof(ICampaign.ResolvePartyDefeat)]);
        Assert.Same(result, session.LastPartyDefeat);
        Assert.Equal(EndgameOutcome.PartyDefeat, session.LastEndgame);
        Assert.Equal(0, session.TravelTime);
        Assert.Null(session.Combat);
        Assert.Null(session.Dungeon);
        Assert.Equal(PartyDefeat.WakeRegion, session.Overworld.RegionId);
    }

    [Fact]
    public void DeathStep_IsThePartysRule_OnSwimmingAndTraps()
    {
        var death = new PlainDeath();
        var party = new Party(RulesProfile.PhantasieI) { Death = death };
        var swimmer = new Character
        {
            Name = "Sinker", Status = CharacterStatus.Ok, HpCur = 1, HpMax = 10,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 5),
        };
        party.Members.Add(swimmer);

        // Swim skill 0: the swim roll fails, then 1..5 damage on 1 HP.
        Assert.True(Swimming.Resolve(party, new SeededRandomSource(3), new List<string>()));
        Assert.Equal(1, death.Calls);
        Assert.Equal(CharacterStatus.Dead, swimmer.Status);
        Assert.Equal(10, swimmer.Stats.Con); // the plain rule has no CON loss

        // The same fall with I's rule loses CON per D-009 (CON 10, LUCK 5 → 2).
        var iParty = new Party();
        var iSwimmer = swimmer.Clone();
        iSwimmer.Status = CharacterStatus.Ok;
        iSwimmer.HpCur = 1;
        iParty.Members.Add(iSwimmer);
        Swimming.Resolve(iParty, new SeededRandomSource(3), new List<string>());
        Assert.Equal(CharacterStatus.Dead, iSwimmer.Status);
        Assert.Equal(10 - PhantasieIDeathRule.ConLoss(swimmer.Stats), iSwimmer.Stats.Con);
        Assert.Equal(8, iSwimmer.Stats.Con);

        // A sprung trap (0x306f → 0x124a): DEX 0 never avoids it, and the hit floors at 1 on 1 HP.
        var trapped = swimmer.Clone();
        trapped.Status = CharacterStatus.Ok;
        trapped.HpCur = 1;
        trapped.Stats = trapped.Stats with { Dex = 0 };
        var trapParty = new Party(RulesProfile.PhantasieI) { Death = death };
        trapParty.Members.Add(trapped);
        Assert.True(DungeonTraps.Spring(trapParty, 2, new SeededRandomSource(5), new List<string>(), out _));
        Assert.Equal(2, death.Calls);
        Assert.Equal(CharacterStatus.Dead, trapped.Status);
        Assert.Equal(10, trapped.Stats.Con);
    }
}
