namespace OpenPhantasie.Core;

/// <summary>
/// Data for the day branches of the random wilderness encounter (<c>wilderness-encounters.json</c>) [C] (s39).
/// <see cref="PairTable"/> is the 16-byte table at COM DS <c>0x1a5c</c>: the monster pair (0 = undead/devils,
/// 1 = slimes, 2 = insects, 3 = animals, 4 = humanoids, 5 = magic users) for index <c>terrain class + rand0_99 / 25</c>.
/// </summary>
public sealed record WildernessEncounterRules(IReadOnlyList<int> PairTable)
{
    public static WildernessEncounterRules Dos { get; } = new([4, 1, 2, 3, 3, 4, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1]);
}

/// <summary>The three menus of the day branches (COM menu ids <c>0xf</c>, <c>0x10</c>, <c>0x11</c>).</summary>
public enum WildernessMenu
{
    /// <summary>Menu <c>0xf</c>: "BLACK KNIGHTS !!!" — RUN / FIGHT / SURRENDER.</summary>
    BlackKnights,

    /// <summary>Menu <c>0x10</c>: "YOU SPOT SLEEPING MONSTERS!" — ATTACK / LEAVE.</summary>
    SleepingMonsters,

    /// <summary>Menu <c>0x11</c>: "%s HEARS MONSTERS" — RUN / FIGHT.</summary>
    HearsMonsters,
}

public enum WildernessAnswer
{
    Run,
    Fight,
    Surrender,
    Attack,
    Leave,
}

/// <summary>A day-branch menu waiting for the player (monster already picked, post-choice rolls not drawn yet).</summary>
/// <param name="MonsterId">0-based first monster the menu is about.</param>
/// <param name="ListenerSlot">Party slot that heard the monsters (<see cref="WildernessMenu.HearsMonsters"/> only).</param>
public sealed record WildernessPrompt(WildernessMenu Menu, int MonsterId, int? ListenerSlot, string Title);

/// <summary>
/// Result of a day-branch step. <see cref="Prompt"/> non-null = wait for an answer. Otherwise <see cref="MonsterId"/>
/// non-null = fight with the surprise globals <see cref="Globals"/>; null = no fight.
/// </summary>
public sealed record WildernessEncounterOutcome(
    int? MonsterId,
    SurpriseFlags Globals,
    IReadOnlyList<string> Messages,
    WildernessPrompt? Prompt = null,
    bool SurrenderFoundGold = false);

/// <summary>
/// Day branches of OV2 <c>0x70ed</c> (<c>0x723b..0x74f0</c>) [C] (s39; <c>overworld-movement.md</c> §Day encounter). Called
/// after <see cref="NightEncounter.TryRoll"/> drew its <c>rand0_99 &gt;= 35</c>. Monster ids are 0-based (DOS returns
/// <c>id + 1</c> in AX; 0 = no fight). <c>F</c>/<c>H</c> are the surprise globals <c>[0x649e]</c>/<c>[0x64a0]</c>; a branch
/// that writes neither leaves them stale (<paramref name="globals"/> in, unchanged out).
/// </summary>
public static class WildernessEncounter
{
    /// <summary>1-based <c>0x4d</c> = BLACK KNIGHT.</summary>
    public const int BlackKnightMonsterId = 76;

    /// <summary><c>[0x63ea] &gt; 83</c> (highest <c>@70</c> SCORE, set on leaving town): the Black Knights flee.</summary>
    public const int BlackKnightFleeScore = 83;

    public static string BlackKnightsFlee => GameText.S("core.encounter.black_knights_flee");
    public static string BlackKnightsTitle => GameText.S("core.encounter.black_knights");
    public static string SleepingTitle => GameText.S("core.encounter.sleeping_monsters");
    public static string YouSurpriseThem => GameText.S("core.encounter.you_surprise_them");
    public static string MonstersSurpriseYou => GameText.S("core.encounter.monsters_surprise_you");

    /// <summary>
    /// <c>0x723b</c>: draws the day rolls up to the first menu (or the fight / no fight).
    /// </summary>
    /// <param name="highestScore"><c>[0x63ea]</c>, the highest <c>@70</c> of the party when it last left town.</param>
    /// <param name="terrainClass"><c>[0x649a]</c> = tile / 10.</param>
    /// <param name="terrainSubcode"><c>[0x649c]</c> = tile % 10 (<c>T</c>, the 12-monster block).</param>
    public static WildernessEncounterOutcome RollDay(
        Party party,
        int highestScore,
        int terrainClass,
        int terrainSubcode,
        SurpriseFlags globals,
        IRandomSource rng,
        WildernessEncounterRules? rules = null,
        EncounterRules? encounterRules = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rng);
        rules ??= WildernessEncounterRules.Dos;
        var er = encounterRules ?? EncounterRules.PhantasieI;
        int Roll(IRandomSource r) => er.Roll100(r);
        var t = terrainSubcode;

        if (er.DayBlackKnights && Roll(rng) > 91) // II: no Black Knights roll at all (encounters.md §4)
        {
            if (highestScore > BlackKnightFleeScore)
                return new(null, globals, [BlackKnightsFlee]);
            return new(null, globals, [],
                new WildernessPrompt(WildernessMenu.BlackKnights, BlackKnightMonsterId, null, BlackKnightsTitle));
        }

        int monster;
        if (Roll(rng) < 25)
        {
            monster = Roll(rng) / 10 + 12 * t + 2;                       // 0x72d2, [0x6498] := 4
        }
        else
        {
            var coin = Roll(rng) * 2 / 100;                              // 0x72fc
            var index = Roll(rng) / 25 + terrainClass;                   // 0x730a
            if ((uint)index >= (uint)rules.PairTable.Count)
                throw new InvalidOperationException($"Terrain class {terrainClass} has no encounter pair (index {index}).");
            monster = coin + 2 * rules.PairTable[index] + 12 * t;        // [0x6498] := 5
        }

        if (Roll(rng) < 31)                                               // 0x733d ([bp-6] is always 1)
            return new(null, globals, [],
                new WildernessPrompt(WildernessMenu.SleepingMonsters, monster, null, SleepingTitle));

        // 0x7389: listener roll for every member (rand drawn before the status test); the last success listens.
        var listener = -1;
        for (var i = 0; i < party.Members.Count; i++)
        {
            var roll = Roll(rng) * 222 / 100;
            var m = party.Members[i];
            if (roll <= m.Skills.Listen && m.Status != CharacterStatus.Dead)
                listener = i;
        }

        var threshold = 6 * t + 20;
        if (listener >= 0)
        {
            var heard = GameText.F("core.encounter.x_hears_monsters", party.Members[listener].Name);
            if (Roll(rng) < threshold)
                return new(monster, SurpriseFlags.Normal, [heard]);     // 0x741b → 0x7473
            return new(null, globals, [],
                new WildernessPrompt(WildernessMenu.HearsMonsters, monster, listener, heard));
        }

        if (Roll(rng) >= threshold)                                       // 0x74b0
            return new(monster, SurpriseFlags.Normal, []);
        var r = Roll(rng);
        if (r >= 50)
            return new(monster, SurpriseFlags.PartySurprised, [MonstersSurpriseYou]);

        // [C quirk] 0x74ec → 0x74f0: falls off the end, AX = r (0..49) is taken as the 1-based monster id; no flag write.
        return new(r == 0 ? null : r - 1, globals, []);
    }

    /// <summary>Resolves a <see cref="WildernessPrompt"/>; the post-choice rolls are drawn here, as in DOS.</summary>
    public static WildernessEncounterOutcome Answer(
        WildernessPrompt prompt, WildernessAnswer answer, SurpriseFlags globals, IRandomSource rng,
        EncounterRules? encounterRules = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(rng);
        var er = encounterRules ?? EncounterRules.PhantasieI;
        int Roll(IRandomSource r) => er.Roll100(r);
        switch (prompt.Menu)
        {
            case WildernessMenu.BlackKnights:
            {
                var f0 = globals with { FirstRound = false };                // 0x7284: F := 0 before the switch
                return answer switch
                {
                    WildernessAnswer.Run => Roll(rng) < 44
                        ? new(null, f0, [])
                        : new(prompt.MonsterId, SurpriseFlags.Normal, []),
                    WildernessAnswer.Fight => new(prompt.MonsterId, SurpriseFlags.Stalled, []),
                    // [C] 0x72ad: found gold [0x63f6] := 0, no fight. Carried gold untouched.
                    WildernessAnswer.Surrender => new(null, f0, [], SurrenderFoundGold: true),
                    _ => throw BadAnswer(prompt, answer),
                };
            }
            case WildernessMenu.SleepingMonsters:
                return answer switch
                {
                    WildernessAnswer.Attack => new(prompt.MonsterId, SurpriseFlags.MonstersSurprised, []),
                    WildernessAnswer.Leave => new(null, globals, []),
                    _ => throw BadAnswer(prompt, answer),
                };
            case WildernessMenu.HearsMonsters:
                return answer switch
                {
                    WildernessAnswer.Fight => Roll(rng) < 30
                        ? new(prompt.MonsterId, SurpriseFlags.Normal, [])
                        : new(prompt.MonsterId, SurpriseFlags.MonstersSurprised, [YouSurpriseThem]),
                    WildernessAnswer.Run => Roll(rng) < 80
                        ? new(null, globals, [])
                        : new(prompt.MonsterId, SurpriseFlags.Normal, []),
                    _ => throw BadAnswer(prompt, answer),
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(prompt));
        }
    }

    private static ArgumentException BadAnswer(WildernessPrompt prompt, WildernessAnswer answer) =>
        new($"{answer} is not an option of the {prompt.Menu} menu.", nameof(answer));


}
