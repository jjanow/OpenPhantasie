namespace OpenPhantasie.Core;

/// <summary>What ended the campaign's last big moment (no game-over exists in DOS either way).</summary>
public enum EndgameOutcome
{
    /// <summary>State 7 → OV1 <c>0x4d5</c>: the Black Lord is dead; Zeus' victory screen.</summary>
    Victory,

    /// <summary>State 6 → OV1 <c>0x3bc</c>: party defeat, the Great Demon's judgement.</summary>
    PartyDefeat,
}

/// <summary>
/// A reward scene to show after a final fight (Phantasie III's gods' reward: Amiga root <c>0x1ee82</c>, DOS ovl4
/// <c>0x0000</c>, <c>endgame.md</c> §3): <see cref="IntroLines"/> on a cleared screen, then the god's picture
/// (<see cref="Good"/>: Zeus, else Pluto) with the party (<see cref="PartyRaces"/>, the elemental left out) under it, then
/// <see cref="SpeechLines"/> on a text page.
/// </summary>
public sealed record RewardScene(bool Good, IReadOnlyList<Race> PartyRaces, IReadOnlyList<string> IntroLines,
    IReadOnlyList<string> SpeechLines)
{
    /// <summary>Every line in order: the intro, then the speech.</summary>
    public IReadOnlyList<string> Lines => [.. IntroLines, .. SpeechLines];
}

/// <summary>One party slot on I's victory screen: its race portrait, and whether DIVINE I was granted (status OK).</summary>
public sealed record VictorySlot(Race Race, bool Granted);

/// <summary>
/// Phantasie I's victory screen (DOS OV1 <c>0x4d5</c>, Amiga h32 <c>+0x6d0</c>; <c>endgame.md</c> §3): Zeus' picture,
/// <see cref="SpeechLines"/>, then a key. <see cref="Slots"/>: the party in slot order, the summoned elemental left out
/// (DOS copies at most 6 records). The front end shows it once after the Black Lord's fight; the game goes on in Pelnor.
/// </summary>
public sealed record VictoryScene(IReadOnlyList<VictorySlot> Slots, IReadOnlyList<string> SpeechLines)
{
    /// <summary>A page shown before the picture until a key (II ST: Filmon's ten intro lines, <c>0x231fc</c>); I has none.</summary>
    public IReadOnlyList<string> IntroPage { get; init; } = [];

    /// <summary>Bottom-line lines after the fade-in, before anyone is presented (II: "Filmon speaks:", "Presenting …").</summary>
    public IReadOnlyList<string> HeaderLines { get; init; } = [];

    /// <summary>
    /// One title per presented member, in grant order (II: "%s - the %s from %s" for records 0…n−1, n = OK members,
    /// <c>endgame.md</c> §7.1); empty when members are not named (I).
    /// </summary>
    public IReadOnlyList<string> Titles { get; init; } = [];

    /// <summary>A page shown after the speech until a key (II: Filmon's 21 closing lines, <c>0x23244</c>); I has none.</summary>
    public IReadOnlyList<string> ClosingPage { get; init; } = [];

    /// <summary>Every line in the order the screen shows them (the prompt box shows these without a pack scene).</summary>
    public IReadOnlyList<string> Lines => [.. IntroPage, .. HeaderLines, .. Titles, .. SpeechLines, .. ClosingPage];
}

/// <summary>The win (endgame.md §1, §3 [C]).</summary>
public static class Endgame
{
    /// <summary>BLACK LORD, 0-based monster id 72 (<c>[0x64e6] == 0x49</c>).</summary>
    public const int BlackLordId = Phantasie1Campaign.BlackLordId;

    /// <summary>Victory text, OV1 <c>0x5913..0x5a09</c> (table <c>0x5905</c>), rows 17..23.</summary>
    public static IReadOnlyList<string> ZeusSpeech =>
    [
        GameText.S("core.endgame.zeus_1"),
        GameText.S("core.endgame.zeus_2"),
        GameText.S("core.endgame.zeus_3"),
        GameText.S("core.endgame.zeus_4"),
        GameText.S("core.endgame.zeus_5"),
        GameText.S("core.endgame.zeus_6"),
        GameText.S("core.endgame.zeus_7"),
    ];

    /// <summary>
    /// End of combat OV2 <c>0x4202..0x4214</c>: state 7 iff the first monster is the BLACK LORD and the "all monsters killed"
    /// flag <c>[0x74aa]</c> is set — by the living recount reaching 0 or by DISSOLVE (<c>0x087b</c>). Escapes (TELEPORTATION,
    /// flee, BEG) leave it 0. The party-wipe test runs first, so a mutual kill is a defeat.
    /// </summary>
    public static bool IsBlackLordKilled(CombatEncounter encounter, CombatResult result)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(result);
        if (result.PartyWiped || encounter.FirstMonsterId != BlackLordId)
            return false;
        return encounter.Dissolved || encounter.AllMonsters.All(m => !m.IsAlive);
    }

    /// <summary>
    /// Zeus' victory screen OV1 <c>0x4d5</c>: the GUILD.DAT written there is the roster <b>before</b> DIVINE I is granted
    /// (quirk [C]: it is set on OK party members after the party → roster copy), returned as clones. Then every OK member
    /// learns DIVINE I. The caller moves the party to town 1; the game goes on.
    /// </summary>
    public static IReadOnlyList<Character?> GrantVictory(Party party, GuildRoster roster)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(roster);
        // Remaster record (user s72, not DOS data): every adventurer in the fight slew the Black Lord. Recorded before the
        // write so the saved roster keeps it.
        var blackLord = new Deed(Deed.Slew, new ContentId(GameId.P1, BlackLordId));
        foreach (var m in party.Adventurers)
            m.RecordDeed(blackLord);
        var guildWrite = roster.Slots.Select(c => c?.Clone()).ToArray();
        foreach (var m in party.Adventurers) // n := partySize == 7 ? 6 : partySize — the summon slot is skipped
        {
            if (m.Status == CharacterStatus.Ok)
                m.SpellsKnown[SpellId.DivineI] = 1; // DOS @210 = slot 57; remaster index 54 (data-tables.md, P0.6)
        }
        return guildWrite;
    }
}
