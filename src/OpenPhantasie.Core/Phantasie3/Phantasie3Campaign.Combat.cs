namespace OpenPhantasie.Core;

/// <summary>
/// III's fights: the two-group builder (<c>make_encounter</c> root <c>0xefd8</c>), III's round loop
/// (<see cref="P3RoundResolver"/>, which also runs the monsters' give-up test at the end of each round), the encounter
/// options (<see cref="P3Encounters"/>) and the final fights' reward scene.
/// </summary>
public sealed partial class Phantasie3Campaign
{
    /// <summary>The current fight's setup (a save keeps what a win still needs: <see cref="FightSetup.FromEvent"/>).</summary>
    private sealed class FightState
    {
        public required FightSetup Setup { get; init; }
        public CombatEncounter? Encounter { get; set; }
    }

    private static readonly P3RoundResolver RoundResolver = new();

    /// <summary>III's combat round (<c>combat_round</c> root <c>0xd8c7</c>).</summary>
    public ICombatResolver CombatResolver => RoundResolver;

    private FightState? _fight;

    /// <summary>Raw monster words over 64,000 mean <c>(v − 64,000) · 1000</c> (gold and XP, <c>encounters.md</c> §4).</summary>
    public static long BigValue(long v) => v > 64000 ? (v - 64000) * 1000 : v;

    /// <summary>A group member's HP: monster byte 25, values over 200 meaning <c>100·b − 20000</c> (<c>encounters.md</c> §1).</summary>
    public static int MonsterHp(int b) => b > 200 ? 100 * b - 20000 : b;

    /// <summary>
    /// <c>make_encounter</c> (root <c>0xefd8</c>) and <c>fill_group</c> (<c>0xe3e4</c>): rank 1 = the monster, its count
    /// <c>rnd(byte 14)</c>, each member's HP from byte 25 (no roll); rank 2 when <c>rnd(100) ≤ byte 28</c>, from companion
    /// byte 29 or 30 (<c>rnd(2) &amp; 1</c>). A rank-1 byte 27 above 41 drops rank 2 and keeps at most two monsters; a
    /// rank-2 byte 27 above 41 drops rank 2. Sleeping monsters (status 2) are built asleep. Ids above 79 become 11 and 61
    /// becomes 60 (the fight routine ovl4 <c>0x1d30</c>).
    /// </summary>
    public CombatEncounter? BuildEncounter(GameSession session, int firstMonsterId, SurpriseFlags surprise)
    {
        var setup = _nextFight ?? new FightSetup(false, FightMode.Normal, 0, false);
        _nextFight = null;
        var rng = session.CampaignAccess.Rng;
        var id = firstMonsterId & 0xff;
        if (id > EncounterBuilder.LastMonsterId) id = 11;
        if (id == 61) id = 60;

        var enc = new CombatEncounter(session.Party)
        {
            Surprise = surprise,
            OpensWithoutOrders = !surprise.OpensWithPartyOptions,
            FirstMonsterId = id,
        };
        var first = session.CampaignAccess.MonsterRecord(id);
        var count = P3Dice.Rnd(rng, Math.Max(1, first.GroupMax));
        Monster[]? second = null;
        if (P3Dice.Rnd(rng, 100) <= first.NextGroupChance && first.Companions.Count >= 2)
        {
            var companion = first.Companions[P3Dice.Rnd(rng, 2) & 1];
            var tpl2 = session.CampaignAccess.MonsterRecord(companion);
            second = Group(tpl2, P3Dice.Rnd(rng, Math.Max(1, tpl2.GroupMax)), setup.MonstersAsleep);
            if (tpl2.GroupAloneByte > 41)
                second = null;
        }
        if (first.GroupAloneByte > 41)
        {
            second = null;
            count = Math.Min(count, 2);
        }
        var fight = NewFight(session, id);
        enc.CampaignState = fight;
        AddGroup(enc, first, Group(first, count, setup.MonstersAsleep));
        enc.TreasureItemIds = first.TreasureItems.ToArray();
        if (second is not null)
            AddGroup(enc, second[0], second);
        fight.StartStrength = P3RoundResolver.Strength(enc.Ranks);
        if (setup.Mode == FightMode.PartySurprise)
        {
            fight.NextRound = P3RoundMode.PartyOnly; // combat_round(2): the party's free round after the first menu
            fight.FreeFlee = true;
        }

        _fight = new FightState { Setup = setup, Encounter = enc };
        return enc;
    }

    /// <summary>The fight's III state; the tall flag comes from the first group's id (root <c>0x104c0</c>).</summary>
    private P3Fight NewFight(GameSession session, int firstMonsterId) => new()
    {
        HitLocations = World.HitLocations,
        TallMonster = World.TallMonsterIds.Contains(firstMonsterId),
        LimblessMonsterIds = World.LimblessMonsterIds,
        Items = session.CampaignAccess.Items,
        SpellNames = World.SpellNames,
    };

    /// <summary>Fight start (root <c>0xd55b</c>): each member's first order is the saved option, re-checked.</summary>
    public void OnCombatStarted(GameSession session, CombatEncounter encounter)
    {
        if (P3Fight.Of(encounter) is not null)
            P3CombatModule.LoadSavedOrders(encounter);
    }

    /// <summary>A fight is saved: its <see cref="P3Fight"/> state and whether a win spends the event square.</summary>
    public void OnCombatCaptured(GameSession session, CombatEncounter encounter, CombatSnapshot snapshot)
    {
        if (P3Fight.Of(encounter) is not { } fight)
            return;
        snapshot.Phantasie3 = fight.Capture(encounter);
        snapshot.Phantasie3.FromEvent = _fight is { } f && ReferenceEquals(f.Encounter, encounter) && f.Setup.FromEvent;
    }

    /// <summary>
    /// A fight loaded from a save: its <see cref="P3Fight"/> as saved (monster bodies, bow spells, fight values, this
    /// round's parries, the round state). A save without III's state (none is written outside III) gets a fresh one: the
    /// monsters whole, no spells, and the give-up test measured from the strength now.
    /// </summary>
    public void OnCombatRestored(GameSession session, CombatEncounter encounter, CombatSnapshot snapshot)
    {
        var fight = NewFight(session, RankOneId(encounter));
        fight.StartStrength = P3RoundResolver.Strength(encounter.Ranks);
        if (snapshot.Phantasie3 is { } saved)
            fight.Restore(saved, encounter);
        encounter.CampaignState = fight;
        _fight = new FightState
        {
            Setup = new FightSetup(false, FightMode.Normal, 0, snapshot.Phantasie3?.FromEvent ?? false),
            Encounter = encounter,
        };
    }

    private static Monster[] Group(Monster tpl, int count, bool asleep) =>
        Enumerable.Range(0, count).Select(_ => Combatant(tpl, asleep)).ToArray();

    private static void AddGroup(CombatEncounter enc, Monster tpl, Monster[] members)
    {
        enc.AddRank(members);
        enc.RewardGold += members.Sum(m => m.Gold);
        enc.MonsterLevel += tpl.Level;
        enc.ReactionSum += tpl.ReactionModifier;
    }

    /// <summary>One group member from the record: HP, gold and XP decoded from III's raw words.</summary>
    private static Monster Combatant(Monster t, bool asleep) => new()
    {
        Id = t.Id,
        Name = t.Name,
        CombatRating = t.CombatRating,
        HitPoints = Math.Max(1, MonsterHp(t.HpBase)),
        Level = t.Level,
        ExperienceValue = BigValue(t.ExperienceValue),
        Gold = BigValue(t.Gold),
        SpecialChance = t.SpecialChance,
        Specials = t.Specials,
        Armor = t.Armor,
        DamageField = t.DamageField,
        Defense = t.Defense,
        AttackPower = t.AttackPower,
        HpBase = t.HpBase,
        GroupSizeFactor = t.GroupSizeFactor,
        NextGroupChance = t.NextGroupChance,
        Companions = t.Companions,
        TreasureItems = t.TreasureItems,
        ReactionModifier = t.ReactionModifier,
        SpriteWidth = t.SpriteWidth,
        SpriteHeight = t.SpriteHeight,
        Undead = t.Undead,
        NeverFlees = t.NeverFlees,
        GroupMax = t.GroupMax,
        GroupAloneByte = t.GroupAloneByte,
        StatusFlags = asleep ? MonsterStatusFlags.Asleep : MonsterStatusFlags.None,
    };

    private static int Strength(CombatEncounter enc) => P3RoundResolver.Strength(enc.Ranks);

    private static int StartStrength(CombatEncounter enc) => P3Fight.Of(enc)?.StartStrength ?? Strength(enc);

    private static MonsterRank? RankOne(CombatEncounter enc) => enc.Ranks.FirstOrDefault(r => r.RankIndex == 0);

    private static int RankOneId(CombatEncounter enc) =>
        RankOne(enc)?.Members.FirstOrDefault()?.Id ?? enc.FirstMonsterId;

    private static int RankOneReaction(CombatEncounter enc) =>
        RankOne(enc)?.Members.FirstOrDefault()?.ReactionModifier ?? 0;

    /// <summary>
    /// No check at the top of a round: III's give-up test (root <c>0xdb96…0xdc22</c>) runs at the end of every round, inside
    /// <see cref="P3RoundResolver"/>.
    /// </summary>
    public MoraleOutcome? MoraleCheck(GameSession session, CombatEncounter encounter, List<string> log) =>
        MoraleOutcome.Stand;

    /// <summary>
    /// The Encounter menu (<c>encounters.md</c> §4): Greet, Accept Surrender (Threaten), Beg for Mercy and Flee by III's
    /// tests; a refusal prints "No Response..." and the round goes on with orders; a failed Flee loses the round.
    /// </summary>
    public PartyOptionResult? ChoosePartyOption(GameSession session, CombatEncounter encounter, PartyOption option)
    {
        var rules = session.Options;
        var rng = session.CampaignAccess.Rng;
        var id = RankOneId(encounter);
        var reaction = RankOneReaction(encounter);
        var log = new List<string>();
        switch (option)
        {
            case PartyOption.Fight:
                return new PartyOptionResult { Outcome = PartyOptionOutcome.GiveOrders, Log = log };
            case PartyOption.Greetings:
                if (!P3Encounters.Greet(id, reaction, rng, rules, greetBlockedFlag: encounter.MonsterDamaged))
                    return Refused(log);
                log.Add(GameText.S("core.p3.greetings"));
                encounter.TreasureForfeited = true;
                return Over(encounter, gold: 0, log);
            case PartyOption.Threaten:
                if (!P3Encounters.Surrenders(Strength(encounter), StartStrength(encounter), id, rng, rules))
                    return Refused(log);
                log.Add(GameText.S("core.p3.monsters_surrender"));
                return Over(encounter, encounter.RewardGold, log);
            case PartyOption.BegMercy:
                if (!P3Encounters.BegForMercy(id, reaction, rng, rules))
                    return Refused(log);
                log.Add(GameText.S("core.p3.they_take_your_gold"));
                session.Sack.Gold = 0;
                session.Party.GoldCarried = 0; // III's one treasury: all of it
                if (P3Encounters.MercyTakeItems(session.Sack, rng, rules) > 0)
                    log.Add(GameText.S("core.p3.they_take_new_items"));
                encounter.TreasureForfeited = true;
                return Over(encounter, gold: 0, log);
            case PartyOption.Flee:
            {
                var rankOne = RankOne(encounter);
                var context = new P3FleeContext(
                    RankOneExists: rankOne is not null,
                    RankOneAllDisabled: rankOne is not null && !rankOne.Members.Any(m =>
                        m.IsAlive && !m.StatusFlags.HasFlag(MonsterStatusFlags.Asleep)),
                    AnyMemberAble: session.Party.Members.Any(m => m.Status is CharacterStatus.Ok or CharacterStatus.Down),
                    FreeFleeFlag: P3Fight.Of(encounter)?.FreeFlee ?? false);
                if (P3Encounters.Flee(context, id, reaction, Strength(encounter), rng, rules))
                {
                    session.PlaySound(P3Sounds.Stop); // 0x19c66: the driver's stop before the line (s158)
                    log.Add(GameText.S("core.p3.party_gets_away"));
                    encounter.TreasureForfeited = true;
                    return Over(encounter, gold: 0, log);
                }
                log.Add(GameText.S("core.p3.you_dont_get_away"));
                if (P3Fight.Of(encounter) is { } fight)
                    fight.NextRound = P3RoundMode.MonstersOnly; // "free shots": the actions stay for the next round
                return new PartyOptionResult { Outcome = PartyOptionOutcome.LoseRound, Log = log };
            }
            default:
                return null;
        }
    }

    private static PartyOptionResult Refused(List<string> log)
    {
        log.Add(GameText.S("core.p3.no_response"));
        return new PartyOptionResult { Outcome = PartyOptionOutcome.GiveOrders, Log = log };
    }

    private static PartyOptionResult Over(CombatEncounter encounter, long gold, List<string> log)
    {
        var end = new CombatResult
        {
            EncounterOver = true, PartyWon = false, RewardXp = encounter.RewardXp, RewardGold = gold, Log = log,
        };
        return new PartyOptionResult { Outcome = PartyOptionOutcome.CombatOver, End = end, Log = log };
    }

    /// <summary>
    /// Treasure after a fight (root <c>0xee93</c>, <c>encounters.md</c> §4): items only, the lead group's two treasure ids,
    /// each found by the first member in party order who is not Dead and makes a Find Item roll (<c>rnd(100) ≤</c> skill);
    /// item b falls back to a's finder. No presence roll, no shift. Nothing after Greet, Mercy, Flee or Teleportation
    /// (outcome 0 or the greet flag), which all forfeit the treasure.
    /// </summary>
    public IReadOnlyList<TreasureOffer>? RollTreasure(GameSession session, CombatEncounter encounter, List<string> log)
    {
        var offers = new List<TreasureOffer>();
        if (encounter.TreasureForfeited)
            return offers;
        var items = session.CampaignAccess.Items;
        foreach (var (id, finder) in FindTreasure(session.Party, encounter.TreasureItemIds, session.CampaignAccess.Rng))
        {
            if (items.Get(id) is null)
                continue;
            offers.Add(new TreasureOffer(id, finder.Name)); // root 0xf2eb asks "<name> finds <item>" / "Will the party keep it?"
        }
        return offers;
    }

    /// <summary>
    /// The finds of root <c>0xee93</c>: items a and b (0 = none) each get a finder from root <c>0xe53a(6, −1)</c>, the first
    /// member in party order who is not Dead (Out, Asleep and Down find too) and rolls <c>rnd(100) ≤</c> Find Item; b
    /// falls back to a's finder. Rolls are drawn for a first, then b.
    /// </summary>
    public static IReadOnlyList<(int ItemId, Character Finder)> FindTreasure(
        Party party, IReadOnlyList<int> treasureIds, IRandomSource rng)
    {
        var a = treasureIds.Count > 0 ? treasureIds[0] : 0;
        var b = treasureIds.Count > 1 ? treasureIds[1] : 0;
        var fa = a != 0 ? Finder(party, rng) : null;
        var fb = b != 0 ? Finder(party, rng) : null;
        if (fa is not null && fb is null)
            fb = fa;
        var found = new List<(int, Character)>();
        if (a > 0 && fa is not null)
            found.Add((a, fa));
        if (b > 0 && fb is not null)
            found.Add((b, fb));
        return found;
    }

    private static Character? Finder(Party party, IRandomSource rng) =>
        party.Members.FirstOrDefault(m => m.Status != CharacterStatus.Dead && P3Dice.Rnd(rng, 100) <= m.Skills.FindItem);

    /// <summary>
    /// After a fight the party survived: an event fight (op 5) that was not left by Greet / Mercy / Flee leaves 0x8a on the
    /// square; then the final fights (<c>endgame.md</c> §1): rank 1 = Nikademus (77 / 78) with its first member dead → the
    /// good reward, Lord Wood (79) → the evil one.
    /// </summary>
    public void AfterCombatSurvived(GameSession session, CombatEncounter encounter, CombatResult result)
    {
        var f = _fight is { } current && ReferenceEquals(current.Encounter, encounter) ? current : null;
        _fight = null;
        if (P3Fight.Of(encounter) is not null)
            P3CombatModule.SaveOrders(encounter);
        if (result.PartyWiped)
            return;
        if (f?.Setup.FromEvent == true && !encounter.TreasureForfeited && session.Dungeon is { } d)
            session.CampaignAccess.CurrentDungeonLevel.Set(d.X, d.Y, SpentFightCell);
        if (RankOne(encounter) is not { Members.Count: > 0 } rankOne || rankOne.Members[0].IsAlive)
            return;
        switch (rankOne.Members[0].Id)
        {
            case NikademusA or NikademusB:
                Reward(session, good: true);
                break;
            case LordWood:
                Reward(session, good: false);
                break;
        }
    }

    /// <summary>
    /// The reward scene (ovl4 <c>0x0000</c>, <c>endgame.md</c> §3): the god speaks; every member is healed (body areas,
    /// Okay), becomes Superhero (5, score 250) or Darklord (4, score 200), rank Front; the party is teleported to Pendragon
    /// on Scandor and the game goes on.
    /// </summary>
    public void Reward(GameSession session, bool good)
    {
        var access = session.CampaignAccess;
        string[] intro = [GameText.S("core.p3.reward_noises"), GameText.S("core.p3.reward_intensity")];
        string[] speech =
        [
            GameText.F("core.p3.reward_god_x", GameText.S(good ? "core.p3.god_zeus" : "core.p3.god_pluto")),
            GameText.F("core.p3.reward_cause_x_title_x", GameText.S(good ? "core.p3.cause_good" : "core.p3.cause_evil"),
                GameText.S(good ? "core.p3.title_superheroes" : "core.p3.title_darklords")),
            GameText.S("core.p3.reward_farewell"),
            GameText.S("core.p3.reward_teleport"),
        ];
        foreach (var line in intro.Concat(speech))
            session.Note(line);
        // The scene (Amiga 0x1ee82): the party's pictures under the god, the elemental left out (slot 6 = -1, 0x1efa8).
        access.PendingRewardScene = new RewardScene(good,
            session.Party.Members.Where(m => !ReferenceEquals(m, session.Party.Elemental)).Select(m => m.Race).ToList(),
            intro, speech);
        foreach (var m in session.Party.Members)
        {
            m.BodyCondition = new P3BodyAreas().ToCondition();
            m.Status = CharacterStatus.Ok;
            m.P3.SocialClass = good ? Phantasie3CharacterData.Superhero : Phantasie3CharacterData.Darklord;
            m.Score = good ? SuperheroScore : DarklordScore;
            m.P3.Rank = P3PartyRank.Front;
        }
        access.LastEndgame = EndgameOutcome.Victory;
        access.ClearDungeonState();
        MoveToTownSquare(session, HomeTownId);
        session.EnterTown(HomeTownId);
    }
}
