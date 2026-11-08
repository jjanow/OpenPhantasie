namespace OpenPhantasie.Core;

public enum TownTransactionKind
{
    Ok,
    Failed,
}

public sealed class TownTransactionResult
{
    public required TownTransactionKind Kind { get; init; }
    public string Message { get; init; } = "";
    public long GoldDelta { get; init; }
    public int? ItemId { get; init; }
    public bool Unusable { get; init; }

    /// <summary>
    /// s154: the lines <see cref="Message"/> joins, when the original shows them as rows of a window (the wilderness inn's
    /// "IN THE INN:" report); empty otherwise.
    /// </summary>
    public IReadOnlyList<string> Lines { get; init; } = [];
}

/// <summary>
/// Town facility transactions (OV1.OVR) — pure Core mutations.
/// No Temple: DOS towns have none (town-menus.md §Temple); resurrection is a spell.
/// </summary>
public static class TownMenus
{
    // --- Bank ----------------------------------------------------------------

    public static TownTransactionResult Withdraw(Party party, Character member, long amount)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);
        if (amount <= 0)
            return Fail(GameText.S("core.bank.amount_must_be_positive"));
        if (!party.Members.Contains(member))
            return Fail(GameText.S("core.guild.not_in_party"));
        if (member.GoldInBank < amount)
            return Fail(GameText.S("core.bank.insufficient_bank_gold"));

        // DOS 0x29c4..0x29e6: the full amount leaves the account, then the purse is clamped — the excess is lost (D-048).
        member.GoldInBank = (int)(member.GoldInBank - amount);
        var lost = party.AddGold(amount);
        return Ok(GameText.F("core.bank.withdrew_x_gp", amount), goldDelta: amount - lost);
    }

    /// <summary>
    /// [C] DOS Bank→Deposit (OV1 <c>0x2919</c> choice 1 → <c>0x167d</c>, the same share split as town entry):
    /// share = floor(carried / Σshares) (Σ 0 → 1); member i's bank <c>@262</c> += shares[i] · share; the remainder
    /// is dropped and the carried purse is zeroed. Manual agrees ("split the party's gold among the individual
    /// members … how many shares each party member gets (1, 2, or 3)"). Shares: 1..3 for a living member, 0..3
    /// for a dead one (s35 prompt; the manual names only 1..3). D-049 (user s40): the Bank's only Deposit, as the manual says.
    /// Each account is clamped at the manual's 65,000 and the excess lost (D-048; DOS: 65,535, COM 0x2e5d). DOS also splits the XP pool here; Core's XP pool
    /// lives in <see cref="LootSack"/> and is paid out on town entry (D-037), so only gold is split.
    /// </summary>
    public static TownTransactionResult DepositShares(Party party, IReadOnlyList<int> shares) =>
        DepositShares(party, shares, (carried, total, s) => s * (carried / total), (carried, total) => carried / total);

    /// <summary>
    /// The share deposit with another game's maths: <paramref name="accountGets"/>(party gold, total shares, member's
    /// shares) is what one account receives, <paramref name="shareValue"/>(party gold, total shares) the value shown; the
    /// rest of the party gold is lost. Same checks and messages as Phantasie I's.
    /// </summary>
    public static TownTransactionResult DepositShares(
        Party party, IReadOnlyList<int> shares, Func<long, int, int, long> accountGets, Func<long, int, long> shareValue)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(shares);
        if (shares.Count != party.Members.Count)
            return Fail(GameText.S("core.bank.one_share_per_member"));
        for (var i = 0; i < shares.Count; i++)
        {
            var min = party.Members[i].Status == CharacterStatus.Dead ? 0 : 1;
            if (shares[i] < min || shares[i] > 3)
                return Fail(GameText.S("core.bank.shares_1_2_3"));
        }

        if (party.GoldCarried <= 0)
            return Fail(GameText.S("core.bank.no_gold_carried")); // DOS 0x167d: empty purse (and pool) → beep, no prompt

        var total = shares.Sum();
        if (total == 0)
            total = 1;
        var carried = party.GoldCarried;
        var share = shareValue(carried, total);
        var deposited = new List<string>();
        for (var i = 0; i < shares.Count; i++)
        {
            var m = party.Members[i];
            var gets = accountGets(carried, total, shares[i]);
            var lost = m.AddBankGold(gets, party.BankGoldCap);
            deposited.Add($"{m.Name} {gets - lost}");
        }
        party.GoldCarried = 0;
        // Manual: "Then the number of GPs deposited into each account is displayed."
        return Ok(GameText.F("core.bank.each_share_x_deposited_x", share, string.Join(", ", deposited)), goldDelta: -carried);
    }

    // --- Armory --------------------------------------------------------------

    public static int ListBuyPrice(ItemCatalogEntry item, TownState town) =>
        item.Value is { } v
            ? (int)((long)v * town.PriceIndex / 100) // III (FORMAT.md §4.1) [~]
            : ItemRules.BuyPrice(item.ValueFactor, town.PriceIndex);

    /// <summary>OV1 Armory message (string imm <c>0xc143</c>) for a shopper whose 9 item slots are full.</summary>
    public static string NinthItemMessage => GameText.S("core.armory.ninth_item");

    public static TownTransactionResult Buy(
        Party party,
        Character shopper,
        TownState town,
        IItemCatalog catalog,
        int itemId)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(shopper);
        ArgumentNullException.ThrowIfNull(town);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!party.Members.Contains(shopper))
            return Fail(GameText.S("core.guild.not_in_party"));
        // [C] Armory OV1 0x1c16: a shopper already holding 9 items is turned away (0x3d14(1)); empty slots are free.
        if (CarriedItems.IsFull(shopper))
            return Fail(NinthItemMessage);
        // [C] the Armory list and its pick (OV1 0x3e2d / 0x3ee8) start at id 1, so id 0 (NOTHING; PHANTASIA's restock
        // list is all zeros) is never for sale.
        if (itemId < TownState.FirstListedItemId || town.GetStock(itemId) <= 0)
            return Fail(GameText.S("core.armory.empty_sorry"));

        var item = catalog.Get(itemId);
        if (item is null)
            return Fail(GameText.S("core.armory.unknown_item"));

        // [C] OV1 charges the full list price. The pass-2 59-GP record delta is
        // the per-member settlement of a 177-GP shared-purse purchase by 3 members.
        var price = ListBuyPrice(item, town);
        if (party.GoldCarried < price)
            return Fail(GameText.S("core.armory.not_enough_gold"));

        party.GoldCarried -= price;
        town.SetStock(itemId, town.GetStock(itemId) - 1);
        CarriedItems.Put(shopper, itemId); // [C] first empty slot (OV1 0x1c18..0x1c2e, 0x1cef)

        AutoEquipIfBetter(shopper, itemId);

        var unusable = ItemRules.IsEquipment(itemId) && !ItemRules.IsUsable(itemId, shopper.Stats);
        return new TownTransactionResult
        {
            Kind = TownTransactionKind.Ok,
            Message = unusable ? GameText.S("core.armory.unusable") : GameText.F("core.armory.bought_x_for_x_gp", item.Name, price),
            GoldDelta = -price,
            ItemId = itemId,
            Unusable = unusable,
        };
    }

    // --- Inn -----------------------------------------------------------------

    /// <summary>
    /// Town Inn rest [C] (OV1 <c>0x2a0d</c>, town-menus.md §Town Inn rest). Free. A non-dead
    /// member who cast since the last rest rolls once for +1 MP max: <c>rand/327 &lt;
    /// (ceiling − max)·20</c>. Then every member — DEAD too, status unchanged, sleepers not
    /// woken — gets HP and MP refilled. Remaster: the stay lasts two weeks, so every member's
    /// age clock gains 1 (DIFFERENCES.md D-004; DOS doesn't age anyone here). Aging effects are
    /// applied on the next town entry, as in DOS.
    /// </summary>
    /// <remarks><paramref name="dosNoTime"/> = DOS (<c>p1.town-inn-time</c>, D-004; II <c>p2.town-inn-time</c>, D2-032): the
    /// stay takes no time. <paramref name="skipDead"/> = the ST (<c>p2.inn-dead</c>, D2-031): DEAD members are not refilled.</remarks>
    public static TownTransactionResult Rest(Party party, IDosRandomSource rng, bool dosNoTime = false, bool skipDead = false)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rng);

        var lines = new List<string>();
        foreach (var m in party.Members)
        {
            var raised = false;
            if (m.Status != CharacterStatus.Dead && m.CastSinceRest)
            {
                m.CastSinceRest = false;
                if (rng.NextDosWord() / 327 < (m.MpCeiling - m.MagicMax) * 20)
                {
                    m.MagicMax++;
                    raised = true;
                }
            }

            var tail = m.Status != CharacterStatus.Ok ? GameText.S("core.inn.in_peace") : raised ? GameText.S("core.inn.magic_increases") : "";
            lines.Add(GameText.F("core.inn.x_is_resting_x", m.Name, tail).TrimEnd());
            if (!(skipDead && m.Status == CharacterStatus.Dead)) // II st (D2-031, 0xe664): the dead keep their HP / MP
            {
                m.MagicCur = m.MagicMax;
                m.HpCur = m.HpMax;
            }
            if (!dosNoTime)
                m.AgeClock += InnStayAgeClockUnits; // D-004
        }

        return Ok(string.Join("; ", lines)); // one line: the Client HUD font has no line breaks
    }

    /// <summary>Length of one inn stay (town or wilderness) in age-clock units: two weeks (D-004, D-008).</summary>
    public const int InnStayAgeClockUnits = 1;

    /// <summary>
    /// Divide sack XP/gold/items among living survivors.
    /// Q2 FIX: remainder gold/XP goes to the first survivor instead of being discarded. <paramref name="dropRemainder"/> =
    /// DOS (<c>p1.payout-remainder</c> = <c>dropped</c>): the remainders are lost, as DOS loses every share remainder.
    /// </summary>
    public static TownTransactionResult Distribute(Party party, LootSack sack, bool items = true, bool dropRemainder = false)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(sack);

        var survivors = party.LivingMembers.ToList();
        if (survivors.Count == 0)
            return Fail(GameText.S("core.inn.no_survivors"));

        var n = survivors.Count;
        var xpShare = sack.Experience / n;
        var xpRem = dropRemainder ? 0 : sack.Experience % n;
        var goldShare = sack.Gold / n;
        var goldRem = dropRemainder ? 0 : sack.Gold % n;

        for (var i = 0; i < n; i++)
        {
            var xp = xpShare + (i == 0 ? xpRem : 0);
            var gold = goldShare + (i == 0 ? goldRem : 0);
            survivors[i].Experience = checked(survivors[i].Experience + xp);
            party.AddGold(gold); // D-048: over 65,000 is lost
        }

        // Round-robin items to survivors, skipping anyone with 9 items. DOS gives each found item to the member
        // the player picks and asks again when that member is full (give-item OV1 0x2e03, 9-slot check 0x2f01);
        // items nobody can take stay in the sack.
        var leftover = new List<int>();
        var next = 0;
        if (!items)
            leftover.AddRange(sack.Items); // kept for the hand-out screen (ItemDistribution)
        foreach (var id in items ? sack.Items : [])
        {
            var taker = -1;
            for (var k = 0; k < n && taker < 0; k++)
            {
                var c = (next + k) % n;
                if (!CarriedItems.IsFull(survivors[c])) taker = c;
            }
            if (taker < 0)
            {
                leftover.Add(id);
                continue;
            }
            CarriedItems.Put(survivors[taker], id);
            next = (taker + 1) % n;
        }

        var msg =
            GameText.F("core.inn.each_survivor_gets_x_xp_x_gp", xpShare, goldShare) +
            (xpRem + goldRem > 0 ? GameText.S("core.inn.remainder_to_first_survivor") : ".") +
            (items && leftover.Count > 0 ? GameText.F("core.inn.x_items_left_in_sack", leftover.Count) : "");
        sack.Clear();
        sack.ItemIds.AddRange(leftover);
        return Ok(msg, goldDelta: goldShare * n + goldRem);
    }

    // --- Mystic --------------------------------------------------------------

    // items.json indices 141-149 = "RING A".."RING I", 150 = "WAND" — the nine
    // rings + wand collectibles. OV1 0x20bb..0x225a (Mystic handler; reached
    // only via the OV1+0x746b runtime jump table, so Ghidra drops it as
    // "unreachable" — read directly with capstone CS_MODE_16 instead, same
    // technique as the training-formula blocks) walks each member's carried
    // inventory (record @0xde, up to 9 slots) and, for any id in [141,150],
    // sets one of ten party-wide "distinct capability" flags (flag index =
    // id-141). [C, byte-verified disassembly].
    private const int MysticRingIdFirst = 141;
    private const int MysticWandId = 150;

    /// <summary>
    /// SCORE = floor((base + 1) / 3), where
    /// base = sum of the levels (+0x58) of party members whose status word
    /// (+0x18, GUILD @24) is 1 = OK — OV1 0x2100 <c>cmp [bx+0x18],1; jne skip</c>.
    /// DEAD and SLEEP members are skipped entirely; UNDEAD is a race with
    /// status OK, so undead members count (PLAN H3, s10)
    /// + 18 per distinct ring/wand id (141..150) held in those same members'
    /// carried inventories
    /// + 66 more if the WAND (150) specifically is held (double-counted: once
    /// as one of the 18-point flags, once again as its own +66 bonus).
    /// [C, byte-verified disassembly of OV1 0x20bb..0x225a]. Score is written
    /// per-member only if it exceeds that member's stored value (matches the
    /// runtime write-back-if-greater at record +0x46) — never decreases.
    /// Readiness-tier thresholds (score &gt; 9/20/39/59/83/99, byte-verified)
    /// are [C]; the seven OV1 strings (0x5edb..0x5f63) attach to the tiers in
    /// string-table order [C]+[D✓] (decoded with the table, OV1 0x3327, s49).
    /// </summary>
    public static TownTransactionResult ConsultMystic(Party party) =>
        ConsultMystic(party, Phantasie1Campaign.HasTheTestRunes);

    /// <summary>
    /// <see cref="ConsultMystic(Party)"/> with the campaign's tier-4 check: <paramref name="readyForTheTests"/> gets the
    /// counted members (I: the first four runes, <see cref="Phantasie1Campaign.HasTheTestRunes"/>).
    /// </summary>
    public static TownTransactionResult ConsultMystic(Party party, Func<IReadOnlyList<Character>, bool> readyForTheTests)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(readyForTheTests);

        var counted = party.Members
            .Where(m => m.Status == CharacterStatus.Ok)
            .ToList();
        var heldIds = counted.SelectMany(m => m.InventoryItemIds).ToHashSet();
        var distinctCapabilities = heldIds.Count(id => id is >= MysticRingIdFirst and <= MysticWandId);
        var hasWand = heldIds.Contains(MysticWandId);

        var baseValue = counted.Sum(m => m.Level)
            + 18 * distinctCapabilities
            + (hasWand ? 66 : 0);
        var score = (baseValue + 1) / 3;

        foreach (var m in party.Members)
            m.Score = Math.Max(m.Score, score);

        // 0x3327(score, tier, allFourRunes): table at OV1 file 0x5ecd; tiers 2–4 add "The Gods." — at tier 4 with the
        // first four rune words (@72..@78, OR'd across counted members) all set, `core.mystic.the_gods_and_tests` [C]+[D✓] s49.
        var tier = (score > 9 ? 1 : 0) + (score > 20 ? 1 : 0) + (score > 39 ? 1 : 0) + (score > 59 ? 1 : 0)
            + (score > 83 ? 1 : 0) + (score > 99 ? 1 : 0);
        var allFourRunes = readyForTheTests(counted);
        string[] table =
        [
            GameText.S("core.mystic.tier_novice"), GameText.S("core.mystic.tier_experienced"),
            GameText.S("core.mystic.tier_not_yet_ready"), GameText.S("core.mystic.tier_almost_ready"),
            GameText.S("core.mystic.tier_ready"), GameText.S("core.mystic.tier_heroes"), GameText.S("core.mystic.tier_supreme"),
        ];
        var text = GameText.F("core.mystic.your_score_is_x_you_are_x", score, table[tier]);
        if (tier is >= 2 and <= 4)
            text += tier == 4 && allFourRunes ? GameText.S("core.mystic.the_gods_and_tests") : GameText.S("core.mystic.the_gods");
        return Ok(text);
    }

    // --- Guild training / spells --------------------------------------------

    // Training (OV1 0x1160..0x148d), fully decoded s39 — town-menus.md §Guild "Training chain" [C].
    // The FPU-emulator helpers (OV1 near target T → COM image T − 0xac24) are: f6ce int→acc, f734 long→acc,
    // f11e/f0ef inline double → acc/second, f020/f019 load float → acc/second, f08c store acc as float,
    // f267 add, f260 sub, f506 mul, f3e1 div, f1fc swap, f1dd/f1d4 push acc/second, f1e6/f1f1 pop, f765 acc→long;
    // OV1 0x51f3 = pow(x, y). Every constant below is the exact inline double (bit pattern) DOS loads.
    private const ulong ThiefMultBits   = 0x4033CCCCCCCCCCC0; // 19.8 − 13 ulp, OV1 0x11fa
    private const ulong MonkMultBits    = 0x4048C00000000000; // 49.5 exact,    OV1 0x121f
    private const ulong RangerMultBits  = 0x404A666666666660; // 52.8 − 6 ulp,  OV1 0x1244
    private const ulong FighterMultBits = 0x4030800000000000; // 16.5 exact,    OV1 0x1266
    private const ulong PriestMultBits  = 0x4032199999999980; // 18.1 − 26 ulp, OV1 0x1287
    private const ulong WizardMultBits  = 0x4045733333333320; // 42.9 − 19 ulp, OV1 0x12ab
    private const ulong LevelFactorBits = 0x3FFCCCCCCCCCCCC0; // 1.8,   OV1 0x12df / 0x1304
    private const ulong MinusPoint8Bits = 0xBFE9999999999980; // −0.8,  OV1 0x1345
    private const ulong ThousandBits    = 0x408F400000000000; // 1000,  OV1 0x1353
    private const ulong EpsilonBits     = 0x3EE4F8B588E368C0; // 1e−5,  OV1 0x136d
    private const ulong GoldPowerBits   = 0x4001999999999980; // 2.2,   OV1 0x13a4

    private static double D(ulong bits) => BitConverter.UInt64BitsToDouble(bits);

    /// <summary>The six stored training multipliers as DOS holds them (index = class @220).</summary>
    public static IReadOnlyList<double> TrainingMultipliers { get; } =
    [
        D(ThiefMultBits), D(MonkMultBits), D(RangerMultBits), D(FighterMultBits), D(PriestMultBits), D(WizardMultBits),
    ];

    // Class→stat sum, [C] (s11): dispatch table at OV1 file 0x12be (CS delta 0x53dc), handlers A..F.
    // Enum order matches CharacterClass (Thief=0, Monk=1, Ranger=2, Fighter=3, Priest=4, Wizard=5).
    private static readonly Func<CharacterStats, int>[] ClassStatSums =
    [
        s => s.Dex * 2 + s.Str,      // A Thief   OV1 0x11ea
        s => s.Int + s.Dex + s.Str,  // B Monk    OV1 0x120e
        s => s.Int + s.Cha + s.Str,  // C Ranger  OV1 0x1233
        s => s.Dex + s.Str,          // D Fighter OV1 0x1258
        s => s.Int + s.Str,          // E Priest  OV1 0x1279
        s => s.Int * 2 + s.Luck,     // F Wizard  OV1 0x129a
    ];

    /// <summary>
    /// [C] OV1 0x11ea..0x1381 — the raw training target X (a double) before the XP comparison:
    /// <code>
    /// s = (float)(statSum / mult)                       // [bp-4]
    /// F = (float)1.8; repeat level−1 times: F = (float)(F · 1.8)   // = 1.8^level
    /// if XP == 0: F = 1.0                               // fresh characters pay 0.2 × the level-1 target
    /// X = (F − 0.8) · 1000 / (s + 1e−5)
    /// </code>
    /// Reproduces all 13 live captures (s5 XP 40, s35 XP 0/141/181). The 1e−5 term (not the stored-double ulps)
    /// is what gives Thief 439 and Ranger 1599.
    /// </summary>
    public static double TrainingTarget(int classIdx, CharacterStats stats, int level, long experience)
    {
        var s = (float)(ClassStatSums[classIdx](stats) / TrainingMultipliers[classIdx]);
        var f = (float)D(LevelFactorBits);
        for (var l = 1; l < level; l++)
            f = (float)(f * D(LevelFactorBits));
        if (experience == 0)
            f = 1.0f;
        var a = (f + D(MinusPoint8Bits)) * D(ThousandBits);
        return a / (s + D(EpsilonBits));
    }

    /// <summary>
    /// [C] OV1 0x1387..0x139e + 0x3b35 — XP still missing ("You need N more `core.guild.you_need_x_more_xp`"); 0 = may train.
    /// DOS stores <c>(float)(XP − X)</c>, truncates it to a long and trains when that is ≥ 0.
    /// </summary>
    public static long TrainingXpShortfall(int classIdx, CharacterStats stats, int level, long experience)
    {
        var v = (float)(experience - TrainingTarget(classIdx, stats, level, experience));
        var n = (long)v; // f765 truncates toward zero
        return n < 0 ? -n : 0;
    }

    /// <summary>
    /// XP total needed for the next level when XP &gt; 0 (= floor of the target). For a character with XP 0 DOS
    /// asks only 0.2 × the level-1 value — use the <paramref name="experience"/> overload for that.
    /// </summary>
    public static long TrainingXpRequired_ForClass(int classIdx, CharacterStats stats, int level) =>
        TrainingXpRequired_ForClass(classIdx, stats, level, experience: 1);

    /// <summary>XP total that ends the shortfall for this XP value (XP 0 uses DOS's reset factor).</summary>
    public static long TrainingXpRequired_ForClass(int classIdx, CharacterStats stats, int level, long experience) =>
        (long)Math.Floor(TrainingTarget(classIdx, stats, level, experience));

    /// <summary>[C] OV1 0x1160..0x13c4 — XP threshold for the next level-up, using the member's current XP.</summary>
    public static long TrainingXpRequired(Character member) =>
        TrainingXpRequired_ForClass((int)member.Class, member.Stats, member.Level, member.Experience);

    /// <summary>[C] XP the member still needs (0 = can train).</summary>
    public static long TrainingXpShortfall(Character member) =>
        TrainingXpShortfall((int)member.Class, member.Stats, member.Level, member.Experience);

    /// <summary>
    /// XP still missing by <paramref name="rules"/> (a game's <see cref="RulesProfile.Training"/>). Phantasie I's values (or
    /// null) take the bit-exact DOS chain above; any other game's take <see cref="TrainingRules.Shortfall"/> in doubles.
    /// </summary>
    public static long TrainingXpShortfall(Character member, TrainingRules? rules) =>
        IsDosChain(rules) ? TrainingXpShortfall(member) : rules!.Shortfall(member.Class, member.Stats, member.Level, member.Experience);

    /// <summary>The training price by <paramref name="rules"/> (I's values or null: the DOS formula below).</summary>
    public static long TrainingGoldCost(Character member, TownState town, TrainingRules? rules)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(town);
        return IsDosChain(rules) ? TrainingGoldCost(member, town) : rules!.GoldCost(member.Level, member.Stats.Cha, town.PriceIndex);
    }

    /// <summary>True for Phantasie I's training values, which play through the exact DOS float chain.</summary>
    private static bool IsDosChain(TrainingRules? rules) => rules is null || rules.Equals(TrainingRules.PhantasieI);

    /// <summary>
    /// [C] OV1 <c>0x119b..0x11a7</c>: training refuses only a DEAD member (<c>@24 == 0</c>) or level ≥ 15 — a SLEEPing
    /// member trains (manual silent on status, so DOS rules; s49). II's <c>guild_train 0x0cdde</c> refuses the same two.
    /// </summary>
    public static bool CanTrain(Character member, out string reason) => CanTrain(member, out reason, rules: null);

    /// <inheritdoc cref="CanTrain(Character, out string)"/>
    public static bool CanTrain(Character member, out string reason, TrainingRules? rules)
    {
        if (member.Status == CharacterStatus.Dead)
        {
            reason = GameText.S("core.guild.no_one_wise_enough"); // [D✓] s41 DOSBox (both cases)
            return false;
        }

        if (member.Level >= 15)
        {
            reason = GameText.S("core.guild.no_one_wise_enough"); // [D✓] s41 DOSBox (both cases)
            return false;
        }

        var need = TrainingXpShortfall(member, rules);
        if (need > 0)
        {
            reason = GameText.F("core.guild.you_need_x_more_xp", need);
            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>[C] OV1 0x13a1..0x13c4 — gold base = (long)pow(level, 2.2); level = current level @88.</summary>
    public static long TrainingGoldBase(int level) => (long)Math.Pow(level, D(GoldPowerBits));

    /// <summary>
    /// [C] OV1 0x13a1..0x1451 — <c>min(15000, (long)pow(level, 2.2) · max(20 − CHA, 0)² · price_index / 100)</c>.
    /// The base is NOT the XP threshold (pre-s39 reading); XP never enters the price.
    /// </summary>
    public static long TrainingGoldCost(Character member, TownState town)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(town);
        var chaFactor = Math.Max(20 - member.Stats.Cha, 0);
        return Math.Min(15000, TrainingGoldBase(member.Level) * (chaFactor * chaFactor) * town.PriceIndex / 100);
    }

    /// <summary>
    /// Guild Training for one member. <paramref name="rules"/> = the game's <see cref="RulesProfile.Training"/> (null =
    /// Phantasie I's); <paramref name="costHiddenWhileShort"/> = the original's box that shows no cost while XP is short
    /// (<c>p2.training-cost</c> = <c>st</c>, D2-013; the manual's D-050 shows it); <paramref name="toss"/> = grow TOSS.
    /// </summary>
    public static TownTransactionResult Train(
        Party party,
        Character member,
        TownState town,
        CharacterCreationTables tables,
        IDosRandomSource rng,
        bool dosHitDice = false,
        TrainingRules? rules = null,
        bool costHiddenWhileShort = false,
        TossRockRules? toss = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(town);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(rng);
        if (!party.Members.Contains(member))
            return Fail(GameText.S("core.guild.train_not_in_party"));
        var cost = TrainingGoldCost(member, town, rules);
        if (!CanTrain(member, out var reason, rules))
        {
            // D-050 (manual): "you will be told how many experience points are required and how much the training will
            // cost" — DOS (0x3b35) and the ST (confirm_cost 0x017e4, D2-013) show the cost only once the XP is there.
            if (member.Status != CharacterStatus.Dead && member.Level < 15 && !costHiddenWhileShort)
                reason += GameText.F("core.guild.it_will_cost_x_gold", cost);
            return Fail(reason);
        }

        if (party.GoldCarried < cost)
            return Fail(GameText.S("core.guild.train_cant_afford")); // [D✓] s41 DOSBox; nothing taken

        party.GoldCarried -= cost;
        LevelUp.Apply(member, member.Level, tables, rng, dosHitDice, rules, toss); // [C] OV1 0x1484 → 0x9ee
        return Ok(GameText.S("core.guild.you_are_trained"), goldDelta: -cost);
    }

    /// <summary>
    /// [C] OV1 0x154d..0x165c charges this fee for a spell marked available
    /// (SpellsKnown[id] == 2). The fee is independent of the spell id; the
    /// <c>spellId % 4 + 1</c> rule is MP spent when casting, not the learn fee.
    /// </summary>
    public static int SpellLearningGoldCost(Character member, TownState town)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(town);

        var denominator = (long)member.Stats.Cha * member.Stats.Int * (9 - (int)member.Class);
        if (denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(member), "Spell-learning attributes must be positive.");

        return checked((int)((long)town.PriceIndex * member.Level * 90 / denominator));
    }

    /// <summary>
    /// [C] OV1 0x14cf..0x15ab (s63, DOSBox-checked): "You can learn %d more spells." The cap is
    /// <c>trunc(level·INT·1.3/90·class + 3·(class == FIGHTER) + 1)</c> (class number THIEF 0 … WIZARD 5, x87 order kept);
    /// the count is <c>min(spells marked 2, cap − known + 1)</c>, so a character can end up knowing cap + 1. A DEAD member
    /// gets −1. Zero or less → `core.guild.learn_no_more`.
    /// </summary>
    public static int SpellLearnCount(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (member.Status == CharacterStatus.Dead)
            return -1;
        var cap = (int)Math.Truncate(member.Level * (double)member.Stats.Int * 1.3 / 90 * (int)member.Class
            + 3 * (member.Class == CharacterClass.Fighter ? 1 : 0) + 1.0);
        int known = 0, marked = 0;
        for (var id = 0; id < Math.Min(60, member.SpellsKnown.Length); id++)
        {
            if (member.SpellsKnown[id] == 1) known++;
            else if (member.SpellsKnown[id] == 2) marked++;
        }
        return Math.Min(marked, cap - known + 1);
    }

    /// <summary>
    /// [C] OV1 0x1618..0x162d: rolled once each time a member is picked at "WHO WILL LEARN?", before the list opens
    /// (also when the count is 0 or the pick is cancelled): <c>rand / 1638 &lt; INT</c> learns, otherwise "You've failed".
    /// </summary>
    public static bool SpellLearnRoll(Character member, IDosRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(rng);
        return rng.NextDosWord() / 1638 < member.Stats.Int;
    }

    /// <summary>
    /// [C] OV1 0x3455 / 0x1648..0x1672 (s63 DOSBox): fee > carried gold → `core.guild.learn_cant_afford` (nothing taken). Otherwise the
    /// fee is taken and age +2 even on a failed <paramref name="learned"/> roll (<see cref="SpellLearnRoll"/>); only a success
    /// marks the spell known.
    /// </summary>
    public static TownTransactionResult LearnSpell(
        Party party,
        Character member,
        TownState town,
        int spellId,
        bool learned = true)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(town);
        if (!party.Members.Contains(member))
            return Fail(GameText.S("core.guild.learn_not_in_party"));
        if ((uint)spellId >= (uint)member.SpellsKnown.Length)
            return Fail(GameText.S("core.magic.unknown_spell"));
        if (member.SpellsKnown[spellId] != 2 || SpellLearnCount(member) <= 0)
            return Fail(GameText.S("core.guild.learn_no_more"));

        var cost = SpellLearningGoldCost(member, town);
        if (party.GoldCarried < cost)
            return Fail(GameText.S("core.guild.learn_cant_afford"));

        party.GoldCarried -= cost;
        member.AgeClock += 2; // [C] OV1 0x165f: learning takes four weeks (manual silent), paid on a fail too
        if (!learned)
            return Ok(GameText.S("core.guild.learn_failed"), goldDelta: -cost);
        member.SpellsKnown[spellId] = 1;
        return Ok(GameText.S("core.guild.learn_learned"), goldDelta: -cost);
    }

    // --- helpers -------------------------------------------------------------

    private static void AutoEquipIfBetter(Character ch, int itemId)
    {
        if (!ItemRules.IsUsable(itemId, ch.Stats)) return;
        var (points, _, _) = ItemRules.CombatGate(itemId);
        if (ItemRules.IsShield(itemId))
        {
            var cur = ItemRules.CombatGate(ch.WornShield).Points;
            if (points >= cur) ch.WornShield = itemId;
        }
        else if (ItemRules.IsArmor(itemId))
        {
            var cur = ItemRules.CombatGate(ch.WornArmor).Points;
            if (points >= cur) ch.WornArmor = itemId;
        }
        else if (ItemRules.IsWeapon(itemId))
        {
            var cur = ItemRules.CombatGate(ch.WornWeapon).Points;
            if (points >= cur) ch.WornWeapon = itemId;
        }
        CombatGear.ApplyWornItemPoints(ch);
    }

    private static TownTransactionResult Ok(string message, long goldDelta = 0, int? itemId = null) =>
        new()
        {
            Kind = TownTransactionKind.Ok,
            Message = message,
            GoldDelta = goldDelta,
            ItemId = itemId,
        };

    private static TownTransactionResult Fail(string message) =>
        new() { Kind = TownTransactionKind.Failed, Message = message };
}
