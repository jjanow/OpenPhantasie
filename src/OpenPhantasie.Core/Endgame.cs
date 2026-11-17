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
/// (DOS copies at most 6 records). The front end shows it once after the Black Lord's fight; the game goes on (on
/// Olympus or in Pelnor: <c>p1.victory</c>, D-090).
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

    /// <summary>s224: <see cref="Titles"/>' parts (the member's name, the title word, the homeland), for screens that print
    /// them on rows of their own (II C64's <c>WIN</c>: name / "THE … ONE" / "FROM …"); empty when <see cref="Titles"/> is.</summary>
    public IReadOnlyList<VictoryTitle> TitleParts { get; init; } = [];

    /// <summary>A page shown after the speech until a key (II: Filmon's 21 closing lines, <c>0x23244</c>); I has none.</summary>
    public IReadOnlyList<string> ClosingPage { get; init; } = [];

    /// <summary>Every line in the order the screen shows them (the prompt box shows these without a pack scene).</summary>
    public IReadOnlyList<string> Lines => [.. IntroPage, .. HeaderLines, .. Titles, .. SpeechLines, .. ClosingPage];

    /// <summary>
    /// s226 (D-090): I's victory under <c>p1.victory</c> = <c>apple</c>: the party goes to Olympus, not Pelnor. A screen
    /// that names the destination words it so (the C64's closing page); the others don't change.
    /// </summary>
    public bool ToOlympus { get; init; }
}

/// <summary>s224: one presented member's title in parts (II: <c>"%s - the %s from %s"</c>'s three values).</summary>
public sealed record VictoryTitle(string Name, string Word, string Origin);

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
    /// D-090 <c>apple</c>: the Apple II's <c>FLASH?</c> line 140 puts items 177, 178, 179 (DOS's DIAMOND B / C / D; the
    /// Apple's own names) in the party's found-item list (<c>SA + 1269 / 1268 / 1267</c>, slots 59 / 58 / 57 of 60), handed
    /// out in the next town like any find.
    /// </summary>
    public static IReadOnlyList<int> VictoryGifts { get; } = [177, 178, 179];

    /// <summary>D-090 <c>apple</c>: <c>FLASH?</c> 170 region byte <c>SA + 1191</c> := 19 (OUT19, south of OLYMPIA's OUT18).</summary>
    public const int OlympusRegion = 19;

    /// <summary>
    /// D-090 <c>apple</c>: <c>FLASH?</c> 170 cell (CX, CY) = (13, 18), which is DOS / Core (x, y) = (25 − CY, CX) = (7, 13)
    /// (Apple FORMAT.md section 3.6; the same turn takes CEMETARY 1550's (5, 5) on OUT1 to DOS's wake cell (20, 5)).
    /// </summary>
    public const int OlympusX = 7;

    /// <summary>D-090 <c>apple</c>: see <see cref="OlympusX"/>.</summary>
    public const int OlympusY = 13;

    /// <summary>
    /// D-090 <c>apple</c>: put <see cref="VictoryGifts"/> in <paramref name="sack"/>, each in the first free slot. The
    /// Apple writes its last three slots whatever they hold; with a full sack the remaster does the same with its last
    /// slots (the items there are lost) `[~]` (Core's sack is 90 or DOS's 60 slots with holes, not the Apple's list).
    /// </summary>
    public static void GiveVictoryGifts(LootSack sack)
    {
        ArgumentNullException.ThrowIfNull(sack);
        var placed = new HashSet<int>(); // slots a gift took in this call: never overwritten by the next gift
        foreach (var id in VictoryGifts)
        {
            var before = sack.ItemIds.ToArray();
            if (sack.TryAdd(id))
            {
                var slot = Enumerable.Range(0, sack.ItemIds.Count)
                    .First(i => i >= before.Length || before[i] != sack.ItemIds[i]);
                placed.Add(slot);
                continue;
            }
            var last = Enumerable.Range(0, sack.ItemIds.Count).Reverse().FirstOrDefault(i => !placed.Contains(i), -1);
            if (last < 0)
                return;
            sack.ItemIds[last] = id;
            placed.Add(last);
        }
    }

    /// <summary>D-090 <c>apple</c>: <c>FLASH?</c> 150 sets record byte 42 (the score, DOS <c>@70</c>) to 100 on every member: tier 6,
    /// "SUPREME BEINGS" (Apple TOWN 4656).</summary>
    public const int AppleVictoryScore = 100;

    /// <summary>D-090 <c>apple</c>: <c>FLASH?</c> 170's XP pool (<c>SA + 1197..1198</c> := 65,535, in tens): 655,350 XP, Core's
    /// whole-XP sack (D-053), which is also its pool cap (<see cref="CombatEncounter.XpPoolCap"/>).</summary>
    public const long AppleVictoryXpPool = 65535L * 10;

    /// <summary>D-090 <c>apple</c>: <c>FLASH?</c> 170's gold pool (<c>SA + 1199..1200</c> := 65,535).</summary>
    public const long AppleVictoryGoldPool = 65535;

    /// <summary>
    /// D-090 <c>apple</c>: <c>FLASH?</c> 170 sets the trip's pools (set, not added): the sack's XP := 655,350 and its gold :=
    /// 65,535, held to the sack's gold cap (D-048: found gold is carried, so the purse cap holds it, 65,000 by default) as
    /// any found gold. They are shared out at the next town as after any trip. Days out (<c>1194..1195</c> := 65,535 there)
    /// are not changed (user s227).
    /// </summary>
    public static void SetAppleVictoryPools(LootSack sack)
    {
        ArgumentNullException.ThrowIfNull(sack);
        sack.Experience = AppleVictoryXpPool;
        sack.Gold = Math.Min(AppleVictoryGoldPool, sack.GoldCap);
    }

    /// <summary>D-090: <paramref name="m"/> knows the Apple's #60 (the spell array grown to I's layout when shorter).</summary>
    public static void LearnAppleDivine(Character m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.SpellsKnown.Length <= SpellId.AppleDivine)
        {
            var grown = m.SpellsKnown;
            Array.Resize(ref grown, SpellId.AppleDivine + 1);
            m.SpellsKnown = grown;
        }
        m.SpellsKnown[SpellId.AppleDivine] = 1;
    }

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
    /// learns DIVINE I. The caller places the party (D-090: Olympus, or town 1 under <c>dos</c>); the game goes on.
    /// </summary>
    public static IReadOnlyList<Character?> GrantVictory(Party party, GuildRoster roster) =>
        GrantVictory(party, roster, apple: false);

    /// <summary>
    /// <see cref="GrantVictory(Party, GuildRoster)"/> with D-090's choice: <paramref name="apple"/> (<c>p1.victory</c> =
    /// <c>apple</c>, user s227) gives the Apple's <c>FLASH?</c> 150 instead of DIVINE I: every adventurer, dead included,
    /// gets score (<c>@70</c>, the Apple's byte 42) := <see cref="AppleVictoryScore"/> and learns the Apple's spell #60
    /// (<see cref="SpellId.AppleDivine"/>; the Apple's NOT toggle is not copied, so a second win never takes it away).
    /// Both after the roster write, as DOS's DIVINE I.
    /// </summary>
    public static IReadOnlyList<Character?> GrantVictory(Party party, GuildRoster roster, bool apple)
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
            if (apple)
            {
                m.Score = AppleVictoryScore; // FLASH? 150, PM = 0 TO PO, no status test
                LearnAppleDivine(m);
                continue;
            }
            if (m.Status == CharacterStatus.Ok)
                m.SpellsKnown[SpellId.DivineI] = 1; // DOS @210 = slot 57; remaster index 54 (data-tables.md, P0.6)
        }
        return guildWrite;
    }
}
