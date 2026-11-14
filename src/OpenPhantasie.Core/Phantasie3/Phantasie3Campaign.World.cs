namespace OpenPhantasie.Core;

/// <summary>III's wilderness: the step, places, terrain, swimming, lava and the random encounter (ovl3, root <c>0xde54</c>).</summary>
public sealed partial class Phantasie3Campaign
{
    /// <summary>How the next fight opens (III's combat mode): 0 normal, 1 the monsters surprise, 2 the party surprises.</summary>
    internal enum FightMode
    {
        Normal = 0,
        MonstersSurprise = 1,
        PartySurprise = 2,
    }

    /// <summary>What the next fight build needs that the shared loop does not carry (consumed by <see cref="BuildEncounter"/>).</summary>
    private sealed record FightSetup(bool MonstersAsleep, FightMode Mode, int ClearFlag, bool FromEvent);

    private FightSetup? _nextFight;

    private static SurpriseFlags SurpriseFor(FightMode mode) =>
        mode == FightMode.MonstersSurprise ? SurpriseFlags.PartySurprised : SurpriseFlags.Normal;

    /// <summary>Start a wilderness fight with III's opening.</summary>
    private void StartWildernessFight(GameSession session, int monsterId, FightMode mode, bool asleep = false)
    {
        _nextFight = new FightSetup(asleep, mode, ClearFlag: 0, FromEvent: false);
        session.BeginWildernessCombat(monsterId, SurpriseFor(mode));
    }

    /// <summary>
    /// One wilderness step (ovl3 <c>0x88</c> loop, <c>planes.md</c>): a map edge or a tile ≥ 242 is blocked ("Ocean" /
    /// "River Styx", the party stays); otherwise the party moves, the terrain name shows when it changes, water swims,
    /// Netherworld lava hurts, a place square asks "Will you enter?" and any other square on land may start an encounter.
    /// Travel time per step is not decoded for III <c>[?]</c>: the clock does not move.
    /// </summary>
    public OverworldMoveResult? MoveOverworld(GameSession session, CardinalDirection direction)
    {
        var w = World;
        var state = session.Overworld;
        var map = session.CurrentOverworldRegion;
        var (dx, dy) = direction switch
        {
            CardinalDirection.North => (0, -1),
            CardinalDirection.South => (0, 1),
            CardinalDirection.East => (1, 0),
            _ => (-1, 0),
        };
        int nx = state.X + dx, ny = state.Y + dy;
        if (map.InBounds(state.X, state.Y)) // the square being left, charged blocked or not (root 0x14a9)
            session.TravelTime += P3TravelTime.StepCost(w.Terrain.TerrainOf(map.GetRaw(state.X, state.Y),
                w.IsNetherworld(state.RegionId)));
        var edge = nx <= 0 || ny <= 0 || nx >= map.GridWidth - 1 || ny >= map.GridHeight - 1;
        var tile = map.InBounds(nx, ny) ? map.GetRaw(nx, ny) : 255;
        if (edge || w.Terrain.IsBlocked(tile))
        {
            session.Note(BlockedMessage(state.RegionId));
            return new OverworldMoveResult
            {
                Kind = OverworldMoveKind.Blocked, RegionId = state.RegionId, X = state.X, Y = state.Y, Tile = tile,
            };
        }

        state.X = nx;
        state.Y = ny;
        var terrain = w.Terrain.TerrainOf(tile, w.IsNetherworld(state.RegionId));
        if (terrain != state.TerrainClass && w.Terrain.NameOf(terrain) is { Length: > 0 } name)
            session.Note(name);
        state.TerrainClass = terrain;

        var kind = OverworldMoveKind.Moved;
        if (terrain == P3Terrain.Water)
        {
            kind = OverworldMoveKind.Swim;
            session.CampaignAccess.Swim(); // root 0x2426(0, 1, …): each member rolls Swim [~]
        }
        else if (terrain == P3Terrain.Lava)
        {
            kind = OverworldMoveKind.Lava;
            session.Note(GameText.S("core.p3.lava_burns"));
            if (DamageParty(session, 13, P3DamageReportKind.Escape, lava: true)) // root 0x2426(13, 1, 3) / Amiga 0xa676
                session.CampaignAccess.PartyDefeatPending = true;
        }

        OverworldMoveResult Result(OverworldMoveKind k, int? town = null, int? dungeon = null, int? destX = null) => new()
        {
            Kind = k, RegionId = state.RegionId, X = state.X, Y = state.Y, Tile = tile, TownId = town, DungeonId = dungeon,
            DestX = destX, DestY = destX is null ? null : 0,
        };

        if (session.PartyDefeatPending)
            return Result(kind);

        if (w.PlaceAt(state.RegionId, nx, ny) is { } place)
        {
            switch (place.Kind)
            {
                case P3PlaceKind.Town:
                    Ask(session, new OverworldEntryPrompt(OverworldEntryKind.Town, place.Number, 0, 0) { Name = place.Name });
                    return Result(OverworldMoveKind.EnterTown, town: place.Number);
                case P3PlaceKind.Dungeon:
                    var x = w.DungeonEntranceX[place.Number - 1];
                    Ask(session, new OverworldEntryPrompt(OverworldEntryKind.Dungeon, place.Number, x, 0) { Name = place.Name });
                    return Result(OverworldMoveKind.EnterDungeon, dungeon: place.Number, destX: x);
                default:
                    session.Note(place.Name);
                    return Result(OverworldMoveKind.WildernessInn);
            }
        }

        if (!w.Terrain.NoEncounter.Contains(terrain))
            RollWildernessEncounter(session, terrain);
        return Result(kind);
    }

    private static void Ask(GameSession session, OverworldEntryPrompt prompt)
    {
        session.CampaignAccess.EntryPrompt = prompt;
        session.Note(prompt.Text);
    }

    /// <summary>
    /// The party damage routine (root <c>0x2426(die, add, kind)</c>, <c>planes.md</c> section 5, s208 <c>[C]</c> static; Amiga
    /// <c>0x1d24a</c> the same): every member with HP &gt; 0 rolls <c>d = rnd(die) + add − 1</c> (<c>rnd</c> = 1…n, n &lt; 1
    /// taken as 1), so a sprung trap (<paramref name="add"/> 0) does 0…die − 1 and the event ops (add 1) 1…die.
    /// <paramref name="lava"/> (kind 3): d = 0 when the member's LCK ≥ the roll, else 27 − roll. A 0 gets the kind's unhurt
    /// line. True when nobody is left alive.
    /// </summary>
    public static bool DamageParty(GameSession session, int die, P3DamageReportKind kind = P3DamageReportKind.Trap,
        int add = 1, bool lava = false)
    {
        session.PlaySound(P3Sounds.Loss); // the Damage Report window plays 42 whatever the die (Amiga 0x1d516)
        var lead = session.LastMessage;
        var mark = session.LogSequence;
        var rng = session.CampaignAccess.Rng;
        var rows = new List<P3DamageReportRow>();
        foreach (var m in session.Party.Members)
        {
            // s165: every member has a row in the report (Amiga 0x1d4ea): one already at 0 HP "is dead."
            if (m.HpCur <= 0 || m.Status == CharacterStatus.Dead)
            {
                rows.Add(new P3DamageReportRow(m.Name, P3DamageOutcome.AlreadyDead, 0));
                continue;
            }
            var d = Math.Max(0, P3Dice.Rnd(rng, die) + add - 1);
            if (lava)
                d = m.Stats.Luck >= d ? 0 : 27 - d; // ovl3 0x547 → 0x2426 kind 3: LCK (+0x14) against the roll
            if (d == 0)
            {
                rows.Add(new P3DamageReportRow(m.Name, P3DamageOutcome.Unhurt, 0)); // "avoids the trap." / "escapes unscathed."
                continue;
            }
            session.Note(GameText.F("core.p3.damage_report_x_x", m.Name, d));
            if (m.HpCur <= d)
            {
                m.HpCur = 0;
                m.Status = CharacterStatus.Dead;
                session.Note(GameText.F("core.p3.x_dies", m.Name));
                rows.Add(new P3DamageReportRow(m.Name, P3DamageOutcome.Died, d));
            }
            else
            {
                m.HpCur -= d;
                rows.Add(new P3DamageReportRow(m.Name, P3DamageOutcome.Damaged, d));
            }
        }
        session.Report(new P3DamageReport(kind, rows, session.LogSince(mark), lead));
        return session.Party.Members.Count > 0 && session.Party.Members.All(m => m.Status == CharacterStatus.Dead);
    }

    /// <summary>A random member who is not Dead (root <c>0x1124</c>) <c>[~]</c>; null for an empty party.</summary>
    private static Character? RandomLiving(GameSession session)
    {
        var living = session.Party.Members.Where(m => m.Status != CharacterStatus.Dead).ToList();
        return living.Count == 0 ? null : living[P3Dice.Rnd(session.CampaignAccess.Rng, living.Count) - 1];
    }

    /// <summary>
    /// The first member (party order, not Dead or Out) whose <paramref name="skill"/> passes <c>rnd(100) ≤ skill</c> (root
    /// <c>0xe53a</c>); members after the first success do not roll. Null when nobody passes.
    /// </summary>
    internal static Character? SkillCheck(GameSession session, P3Skill skill)
    {
        var rng = session.CampaignAccess.Rng;
        foreach (var m in session.Party.Members)
        {
            if (m.Status is CharacterStatus.Dead or CharacterStatus.Out)
                continue;
            if (P3Dice.Rnd(rng, 100) <= SkillOf(m, skill))
                return m;
        }
        return null;
    }

    /// <summary>
    /// Root <c>0xde54</c> (s83, <c>encounters.md</c> §7): <c>rnd(100) &lt; 8</c> starts one. L = the area level. Night
    /// (<c>rnd(100) &lt; 38</c>): the party sleeps, a random member watches and wakes it on a Listen roll. Else the monster
    /// is kind 2 (<c>rnd(100) &lt; 26</c>) or kind 3; sleeping monsters (<c>rnd(100) &lt; 37</c>) ask whether to attack;
    /// otherwise a Listen check and the monsters' notice (<c>rnd(100) &lt; 8L</c>) decide the surprise or a "Fight? (or try to
    /// run)" question.
    /// </summary>
    private void RollWildernessEncounter(GameSession session, int terrain)
    {
        var rules = World.Wilderness;
        var rng = session.CampaignAccess.Rng;
        if (session.Party.Members.Count == 0 || P3Dice.Rnd(rng, 100) >= rules.Chance)
            return;
        session.PlaySound(P3Sounds.Encounter); // Amiga 0x14a28: every encounter, before the night / surprise rolls
        var state = session.Overworld;
        var level = rules.AreaLevel(state.X, state.Y);
        if (P3Dice.Rnd(rng, 100) < rules.NightBelow)
        {
            var id = rules.PickMonster(1, level, terrain, rng);
            StartWildernessFight(session, id, FightMode.Normal);
            NightWatch(session);
            return;
        }
        var monster = rules.PickMonster(P3Dice.Rnd(rng, 100) < rules.Kind2Below ? 2 : 3, level, terrain, rng);
        if (P3Dice.Rnd(rng, 100) < rules.SleepingBelow)
        {
            var spotter = RandomLiving(session);
            if (spotter is null)
                return;
            AskWilderness(session, WildernessMenu.SleepingMonsters, monster, spotter,
                GameText.F("core.p3.x_spots_sleeping_monsters", spotter.Name));
            return;
        }
        var heard = SkillCheck(session, P3Skill.Listen) is not null;
        var noticed = P3Dice.Rnd(rng, 100) < rules.NoticePerLevel * level;
        if (noticed && !heard)
        {
            if (P3Dice.Rnd(rng, 100) < rules.SurpriseBelow)
            {
                session.Note(GameText.S("core.p3.monsters_surprise_you"));
                StartWildernessFight(session, monster, FightMode.MonstersSurprise);
            }
            else
                StartWildernessFight(session, monster, FightMode.Normal);
            return;
        }
        if (heard && !noticed && RandomLiving(session) is { } asker)
        {
            AskWilderness(session, WildernessMenu.HearsMonsters, monster, asker,
                GameText.F("core.p3.x_hears_monsters_fight", asker.Name));
            return;
        }
        StartWildernessFight(session, monster, FightMode.Normal);
    }

    private static void AskWilderness(GameSession session, WildernessMenu menu, int monster, Character asker, string title)
    {
        session.CampaignAccess.WildernessPrompt =
            new WildernessPrompt(menu, monster, session.Party.Members.IndexOf(asker), title);
        session.Note(title);
    }

    /// <summary>
    /// The night watch (root <c>0xee36</c>): every member not Dead or Out falls asleep, a random one is on watch and wakes the
    /// party when <c>Listen &gt; rnd(100)</c> ("… is on watch and hears them!"), else the party stays asleep.
    /// </summary>
    private static void NightWatch(GameSession session)
    {
        var party = session.Party;
        foreach (var m in party.Members)
            if (m.Status is not (CharacterStatus.Dead or CharacterStatus.Out))
                m.Status = CharacterStatus.Sleep;
        var watcher = RandomLiving(session);
        if (watcher is null)
            return;
        if (watcher.Skills.Listen > P3Dice.Rnd(session.CampaignAccess.Rng, 100))
        {
            foreach (var m in party.Members)
                if (m.Status == CharacterStatus.Sleep)
                    m.Status = CharacterStatus.Ok;
            session.Note(GameText.F("core.p3.x_is_on_watch_and_hears_them", watcher.Name));
        }
        else
            session.Note(GameText.F("core.p3.x_is_on_watch", watcher.Name));
    }

    /// <summary>
    /// The two questions of <see cref="RollWildernessEncounter"/>: sleeping monsters — Attack builds them asleep and the
    /// party may flee freely, Leave ends it; heard monsters — Fight surprises them on <c>rnd(100) &lt; 31</c>, Run gets away
    /// on <c>rnd(100) &lt; 81</c> and otherwise fights.
    /// </summary>
    public bool AnswerWildernessPrompt(GameSession session, WildernessPrompt prompt, WildernessAnswer answer)
    {
        var rules = World.Wilderness;
        var rng = session.CampaignAccess.Rng;
        session.CampaignAccess.WildernessPrompt = null;
        switch (prompt.Menu)
        {
            case WildernessMenu.SleepingMonsters:
                if (answer is WildernessAnswer.Attack or WildernessAnswer.Fight)
                    StartWildernessFight(session, prompt.MonsterId, FightMode.PartySurprise, asleep: true);
                return true;
            case WildernessMenu.HearsMonsters:
                if (answer is WildernessAnswer.Fight or WildernessAnswer.Attack)
                {
                    if (P3Dice.Rnd(rng, 100) < rules.PartySurpriseBelow)
                    {
                        session.Note(GameText.S("core.p3.you_surprise_the_monsters"));
                        StartWildernessFight(session, prompt.MonsterId, FightMode.PartySurprise);
                    }
                    else
                        StartWildernessFight(session, prompt.MonsterId, FightMode.Normal);
                }
                else if (P3Dice.Rnd(rng, 100) >= rules.RunAwayBelow)
                    StartWildernessFight(session, prompt.MonsterId, FightMode.Normal);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// An independent inn (ovl3 <c>0x8cc</c>): 250 gold from the party (`core.p3.cannot_afford_stay`), then the
    /// inn (body areas as every inn: 1 Break or 2 Injuries; HP and magic as the shared wilderness inn [~]).
    /// </summary>
    public TownTransactionResult? StayAtWildernessInn(GameSession session)
    {
        const long price = 250;
        var party = session.Party;
        var o = session.Overworld;
        if (World.PlaceAt(o.RegionId, o.X, o.Y) is not { Kind: P3PlaceKind.Inn } inn)
            throw new InvalidOperationException("The party is not at an inn.");
        if (PartyGold(session) + (party.Treasury?.Balance ?? 0) < price)
            return Failed(GameText.S("core.p3.cannot_afford_stay"));
        var lines = P3Inn.Stay(session); // ovl2 0x347d with the in-town flag clear (s136)
        HealBodiesAtInn(session, inTown: false);
        // A spend: found gold first, then carried (TakeGold's order), then the Guild treasury when it is on (s175).
        // TakeGold itself stays for the losses (events), which only ever reach trip gold.
        GoldPurse.TrySpend(price, party.Treasury, TreasuryEntryKind.Inn,
            (() => session.Sack.Gold, v => session.Sack.Gold = v), (() => party.GoldCarried, v => party.GoldCarried = v));
        return new TownTransactionResult
        {
            Kind = TownTransactionKind.Ok, GoldDelta = -price,
            Message = string.Join("; ", [GameText.F("core.p3.inn_stay", inn.Name), .. lines]),
        };
    }

    /// <summary>Take gold from the party (found gold first, then carried); returns what was taken.</summary>
    internal static long TakeGold(GameSession session, long amount)
    {
        var sack = session.Sack;
        var fromSack = Math.Min(sack.Gold, amount);
        sack.Gold -= fromSack;
        var fromParty = Math.Min(session.Party.GoldCarried, amount - fromSack);
        session.Party.GoldCarried -= fromParty;
        return fromSack + fromParty;
    }

    /// <summary>The party's gold: found gold plus carried (III has one treasury).</summary>
    internal static long PartyGold(GameSession session) => session.Sack.Gold + session.Party.GoldCarried;
}
