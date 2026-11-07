using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T1.4 rest: the shared loop runs on a campaign that is not Phantasie I's. The fake below lives outside Core and
/// gives none of I's values (start town, TRANSPORTATION list, blocked text, treasure rule, Mystic, death step), so each
/// test shows the loop asks the campaign instead of carrying I's detail itself. It reaches the session only through the
/// public API and <see cref="GameSession.CampaignAccess"/>. (Its game id is p1 because only I's data exists yet.)
/// </summary>
public class MinimalCampaignTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private sealed class PlainDeath : IMemberDeathRule
    {
        public void ApplyDeath(Character member) => member.Status = CharacterStatus.Dead;
    }

    private sealed class MinimalCampaign : ICampaign
    {
        public readonly List<string> Calls = new();

        public GameId Game => GameId.P1;
        public FeatureModuleSet DefaultFeatureModules => FeatureModuleSet.PhantasieI;
        public IMemberDeathRule Death { get; } = new PlainDeath();
        public int StartTownId => 3;
        public IReadOnlyList<int> TransportTowns { get; } = [5, 2, 7];
        public bool IsUnnamedTransportTown(int townId) => townId == 7;
        public IReadOnlyDictionary<int, int> OverworldTownAliases { get; } = new Dictionary<int, int>();
        public string BlockedMessage(int regionId) => $"A WALL OF FOG {regionId}";
        public IReadOnlyList<int> FallbackMonsterPool { get; } = [0];
        public bool MayDropItem(int itemId, int? dungeonLevelId) => false;
        public bool WithholdsDungeonTreasure(int firstMonsterId) => false;

        public TownTransactionResult ConsultMystic(GameSession session) =>
            new() { Kind = TownTransactionKind.Ok, Message = "THE CRYSTAL BALL IS CLOUDY" };

        public void OnSpecialSite(GameSession session, OverworldMoveResult result) => Calls.Add(nameof(OnSpecialSite));
        public void BeginDungeonEvent(GameSession session, DungeonMoveResult move) => Calls.Add(nameof(BeginDungeonEvent));
        public void AnswerDungeonEvent(GameSession session, int choice) => Calls.Add(nameof(AnswerDungeonEvent));
        public void ResumeDungeonScript(GameSession session) => Calls.Add(nameof(ResumeDungeonScript));
        public bool IsBlackKnightSpot(int dungeonLevelId) => false;

        /// <summary>Every survived fight is a win here: the campaign sets the endgame through <see cref="CampaignAccess"/>.</summary>
        public void AfterCombatSurvived(GameSession session, CombatEncounter encounter, CombatResult result)
        {
            session.CampaignAccess.LastEndgame = EndgameOutcome.Victory;
            session.CampaignAccess.Rng.Next(0, 100); // the session's own stream is reachable
        }

        public PartyDefeatResult ResolvePartyDefeat(GameSession session)
        {
            session.CampaignAccess.ReturnToWilderness();
            return new PartyDefeatResult { Log = ["THE END"] };
        }
    }

    private static GameSession NewSession(ICampaign campaign, int seed = 4) =>
        GameSessionFactory.Create(Data, new SeededDosRandomSource(seed), campaign: campaign);

    private static Character Enroll(GameSession session, CharacterClass cls = CharacterClass.Fighter)
    {
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Plain", Race.Human, cls);
        Assert.True(session.TryAddToParty(hero, out _));
        return hero;
    }

    [OriginalDataFact]
    public void NewGame_StartsInTheCampaignsTown()
    {
        var session = NewSession(new MinimalCampaign());
        session.StartNewGame();
        Assert.Equal(3, session.CurrentTownId);
        Assert.Equal("Entered town #3.", session.LastMessage);
    }

    [OriginalDataFact]
    public void Transportation_ListsTheCampaignsTowns_WithoutTheCurrentOne()
    {
        var session = NewSession(new MinimalCampaign());
        session.EnterTown(2);
        Assert.Equal(new[] { 5, 7 }, session.TransportDestinations);
        session.EnterTown(1);
        Assert.Equal(new[] { 5, 2, 7 }, session.TransportDestinations);
    }

    [OriginalDataFact]
    public void BlockedStep_PrintsTheCampaignsText()
    {
        var session = NewSession(new MinimalCampaign());
        Enroll(session);
        session.DepartGuildToWilderness();
        session.Overworld.RegionId = 1;
        session.Overworld.X = 0;
        session.Overworld.Y = 0;
        var result = session.MoveOverworld(CardinalDirection.North); // region 0 does not exist: blocked
        Assert.Equal(OverworldMoveKind.Blocked, result.Kind);
        Assert.Equal("A WALL OF FOG 1", session.LastMessage);
    }

    [OriginalDataFact]
    public void Mystic_IsTheCampaigns()
    {
        var session = NewSession(new MinimalCampaign());
        Assert.Equal("THE CRYSTAL BALL IS CLOUDY", session.ConsultMystic().Message);
    }

    [OriginalDataFact]
    public void Party_UsesTheCampaignsDeathStep()
    {
        var campaign = new MinimalCampaign();
        var session = NewSession(campaign);
        Assert.Same(campaign.Death, session.Party.Death);
    }

    [OriginalDataFact]
    public void Overworld_HasNoneOfIsTownAliases()
    {
        var traversal = Data.CreateTraversal(new MinimalCampaign());
        Assert.Empty(traversal.TownAliases);
        Assert.Equal(Phantasie1Campaign.OlympiaTownId, Data.CreateTraversal().TownAliases[Phantasie1Campaign.OlympiaFeatureArg]);
    }

    [OriginalDataFact]
    public void Treasure_FollowsTheCampaignsDropRule()
    {
        // The same wilderness fight (same seed) under I's campaign offers its treasure; the minimal one forbids every item.
        static GameSession Fight(ICampaign campaign)
        {
            var session = NewSession(campaign, seed: 11);
            Enroll(session);
            session.DepartGuildToWilderness();
            var enc = session.BeginWildernessCombat(FirstMonsterWithTreasure(), SurpriseFlags.Normal);
            foreach (var m in enc.AllMonsters)
                m.HitPoints = 0;
            session.ResolveCombatRound();
            return session;
        }

        Assert.NotEmpty(Fight(Phantasie1Campaign.Instance).PendingTreasure);
        var minimal = Fight(new MinimalCampaign());
        Assert.Empty(minimal.PendingTreasure);
        Assert.Equal(EndgameOutcome.Victory, minimal.LastEndgame); // AfterCombatSurvived went through CampaignAccess
    }

    [OriginalDataFact]
    public void PartyDefeat_IsTheCampaignsJudgement()
    {
        var session = NewSession(new MinimalCampaign());
        var hero = Enroll(session);
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(0, SurpriseFlags.Normal);
        hero.HpCur = 0;
        hero.Status = CharacterStatus.Dead;
        session.ResolveCombatRound();
        Assert.True(session.PartyDefeatPending);
        var result = session.ResolvePartyDefeat();
        Assert.Equal(["THE END"], result.Log);
        Assert.Equal(GameMode.Overworld, session.Mode);
        Assert.Null(session.Combat);
    }

    /// <summary>A monster whose treasure ids, shifted by −1..+3, stay well inside the item table.</summary>
    private static int FirstMonsterWithTreasure() =>
        Data.Monsters.First(m => m.ToRecord().TreasureItems.Any(id => id is > 10 and < 100)).Id;
}
