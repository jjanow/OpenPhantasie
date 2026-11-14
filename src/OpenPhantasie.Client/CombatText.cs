using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>A member's order as the action chooser would repeat it (OV2 <c>0x6bc5</c> mode 0).</summary>
public readonly record struct RepeatOrder(CombatChoice Choice, int RankIndex, int SpellIndex, int HealTargetIndex);

/// <summary>
/// Pure combat presentation helpers (no MonoGame types): the DOS rank list, the PARTY OPTIONS labels and the
/// order repeat rule. Texts come from <c>combat-resolution.md</c>.
/// </summary>
public static class CombatText
{
    /// <summary>
    /// PARTY OPTIONS in DOS menu order (menu <c>0x18</c>, labels COM DS <c>0xd4ff</c>: "Beg mercy", "Threaten", "Greetings",
    /// "FIGHT", "Flee", "Show names", "Party health"), the first five in the same order as the manual's C64 supplement.
    /// The last two have no <see cref="PartyOption"/>: they open an information box and return to the menu
    /// (<see cref="ShowNamesIndex"/>, <see cref="PartyHealthIndex"/>; s49 static decode of OV2 <c>0x3e6f</c> / <c>0x45a0</c>).
    /// </summary>
    public static IReadOnlyList<(PartyOption? Option, string Label)> PartyOptionMenu =>
    [
        (PartyOption.BegMercy, UiText.S("combattext.beg_mercy")),
        (PartyOption.Threaten, UiText.S("combattext.threaten")),
        (PartyOption.Greetings, UiText.S("combattext.greetings")),
        (PartyOption.Fight, UiText.S("combattext.fight")),
        (PartyOption.Flee, UiText.S("combattext.flee")),
        (null, UiText.S("combattext.show_names")),
        (null, UiText.S("combattext.party_health")),
    ];

    public const int ShowNamesIndex = 5;
    public const int PartyHealthIndex = 6;

    /// <summary>
    /// The box a Show names / Party health entry opens (empty for a real option). Neither uses up the round: DOS waits
    /// for a key (COM <c>0xe3c4</c>) and returns to the menu.
    /// </summary>
    public static IReadOnlyList<string> InfoBox(int menuIndex, GameSession session) => menuIndex switch
    {
        ShowNamesIndex => RankLines(session.Combat),
        PartyHealthIndex => HealthReport(session),
        _ => [],
    };

    /// <summary>"HEALTH REPORT:" (OV2 <c>0x45a0</c>, string DS <c>0xbda2</c>).</summary>
    public static string HealthReportTitle => UiText.S("combattext.health_report");

    /// <summary>
    /// Party health, OV2 <c>0x45a0</c> [C] (s49 static): title "HEALTH REPORT:", then one line per party slot up to the
    /// party size <c>[0x648e]</c> (a summoned elemental included), format DS <c>0xbdb1</c>
    /// <c>"%-10.9s%c HP%4d/%-4dA:%d MP%3d/%d"</c> = name, status letter (table DS <c>0xbd9f</c> "D S": DEAD 'D', OK ' ',
    /// asleep 'S'), HP <c>@22</c>/<c>@20</c>, A = <c>@250 / 75 + 1</c> (most SLASH swings), MP <c>@18</c>/<c>@16</c>.
    /// </summary>
    public static IReadOnlyList<string> HealthReport(GameSession session)
    {
        var lines = new List<string> { HealthReportTitle };
        var members = session.Party.Members;
        var prep = session.Combat?.Prep;
        for (var i = 0; i < members.Count; i++)
        {
            var ch = members[i];
            var hitEff = prep is not null && i < prep.Length ? prep[i].WeaponHitEff : ch.Skills.Attack; // @250 = @30 at prep
            lines.Add(HealthLine(ch, hitEff));
        }
        return lines;
    }

    /// <summary>One HEALTH REPORT line, <c>"%-10.9s%c HP%4d/%-4dA:%d MP%3d/%d"</c>.</summary>
    public static string HealthLine(Character ch, int weaponHitEff)
    {
        var name = ch.Name.Length > 9 ? ch.Name[..9] : ch.Name;
        var status = ch.Status switch
        {
            CharacterStatus.Dead => 'D',
            CharacterStatus.Sleep => 'S',
            _ => ' ',
        };
        return UiText.F("combattext.x_x_hp_x_x_a", name, status, ch.HpCur, ch.HpMax, weaponHitEff / 75 + 1, ch.MagicCur, ch.MagicMax);
    }

    /// <summary>
    /// Which entry the menu highlights the first time it opens; after that it reopens on the last choice (DOS menus
    /// remember, s45). DOS's first-ever highlight of menu <c>0x18</c> is FIGHT, index 3 (table word DS <c>0x1a4</c>) [C].
    /// </summary>
    public static int DefaultPartyOptionIndex => 3;

    /// <summary>
    /// The DOS rank list, <c>"RANK %1d : %1d  %s(S)"</c> (OV2 <c>0x7990</c>; also the PARTY OPTIONS "Show names" box,
    /// OV2 <c>0x3e6f</c>, which prints it for each of the <c>[0x6492]</c> ranks in rank order): one line per rank that still has living
    /// monsters, in the current rank order (after <see cref="CombatEncounter.ReRank"/>), with the living count and the
    /// group's monster name; in III also each living member's status letter (K / S / D / U, or O under DOS rules). Line n
    /// is the rank a thief picks with digit n.
    /// </summary>
    public static IReadOnlyList<string> RankLines(CombatEncounter? encounter)
    {
        if (encounter is null)
            return Array.Empty<string>();
        var lines = new List<string>();
        var fight = P3Fight.Of(encounter);
        foreach (var rank in encounter.Ranks.Where(r => !r.IsWiped))
        {
            var line = UiText.F("combattext.rank_x_x_x_s", lines.Count + 1, rank.Living.Count(), rank.Members[0].Name);
            if (fight is not null) // III: one status letter per living member, "2 GNOME(S) S U" (D3-021)
                line += " " + string.Join(" ", rank.Living.Select(m =>
                    P3Encounters.StatusLetter(fight.StatusOf(m), encounter.Rules)));
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Repeat order from the previous packed action (mode 0 of the chooser, <c>0x6bc5</c>): &lt;10 → PARRY, 10/12 → THRUST,
    /// 11 → LUNGE, 20–22 → ATTACK, 30–90 → SLASH, else a spell. The rank is kept, clamped to the living rank count.
    /// A fight starts with every action 4, so everyone's first order is PARRY (s35 DOSBox capture).
    /// </summary>
    /// <remarks>
    /// Remaster detail: Core packs a thief's THRUST at rank 2 as 11 too. In DOS a thief's LUNGE is also 11, so both
    /// repeat the same way; under D-012 (LUNGE for fighters only) a thief's LUNGE would become THRUST rank 1, so for a thief
    /// 11 repeats as THRUST at rank 2 (the manual: the order "remains unchanged until you change it").
    /// A spell is recast without asking [C] (OV2 repeat path <c>0x6bc5</c>/<c>0x6d7b</c>): DOS re-checks that it is still known
    /// (<c>0x6e22</c>) and that MP is enough (<c>0x6e52</c>), and on a fail beeps and takes the PARRY path; a HEALING cast keeps
    /// its target. Core's <see cref="CombatOrders.Choose"/> makes the same checks and PARRY fallback; the remaster has no
    /// sound yet, so the beep is not played.
    /// </remarks>
    public static RepeatOrder RepeatFor(int previousPacked, CharacterClass cls, int livingRanks)
    {
        if (previousPacked < 10)
            return new RepeatOrder(CombatChoice.Parry, 0, -1, 0);

        // III: the stored action and its rank repeat as they are (Core re-runs the menu rules on them).
        if (P3CombatModule.TryUnpack(previousPacked, out var p3))
            return new RepeatOrder(P3Choice(p3.Action), Math.Clamp(p3.Rank - 1, 0, Math.Max(0, livingRanks - 1)), -1, 0);

        // II Toss Rock: repeated with its rank clamped to the living ranks (seen live, toss-training-live.md).
        if (CombatAction.TryDecodeToss(previousPacked, out var tossRank))
            return new RepeatOrder(CombatChoice.Toss, Math.Clamp(tossRank, 0, Math.Max(0, livingRanks - 1)), -1, 0);

        if (CombatAction.IsCastOrSpecial(previousPacked))
        {
            // III's Fireflash at rank 2 (D3-075 dos) repeats at rank 2 while two ranks live.
            return CombatAction.TryDecodeCast(previousPacked, out var spell, out var target, out var castRank)
                ? new RepeatOrder(CombatChoice.Cast, Math.Clamp(castRank, 0, Math.Max(0, livingRanks - 1)), spell, target)
                : new RepeatOrder(CombatChoice.Parry, 0, -1, 0); // unreachable from DOS orders: menu 0x1a has no specials
        }

        var swings = previousPacked / 10;
        var rank = Math.Clamp(previousPacked % 10, 0, Math.Max(0, livingRanks - 1));
        if (previousPacked == 11 && cls != CharacterClass.Thief)
            return new RepeatOrder(CombatChoice.Lunge, 0, -1, 0);
        var choice = swings switch
        {
            1 => CombatChoice.Thrust,
            2 => CombatChoice.Attack,
            _ => CombatChoice.Slash,
        };
        return new RepeatOrder(choice, rank, -1, 0);
    }

    private static CombatChoice P3Choice(P3Action action) => action switch
    {
        P3Action.Attack => CombatChoice.Attack,
        P3Action.Lunge => CombatChoice.Lunge,
        P3Action.Slash => CombatChoice.Slash,
        P3Action.FireBow => CombatChoice.FireBow,
        P3Action.Aim => CombatChoice.Aim,
        P3Action.Parry => CombatChoice.Parry,
        _ => CombatChoice.Thrust,
    };

    /// <summary>
    /// The order menu line for <paramref name="choices"/>, numbered from 1: "1 THRUST 2 ATTACK … 6 PARRY" for Phantasie I.
    /// Keeps the "digit WORD" shape the mouse reads (<see cref="PanelMouse.Hotkeys"/>).
    /// </summary>
    public static string OrderMenu(IReadOnlyList<CombatChoice> choices) =>
        string.Join(" ", choices.Select((c, i) => $"{i + 1} {ChoiceLabel(c)}"));

    /// <summary>
    /// A menu order's name. An order no table names yet (a module from another game) shows "ORDER n".
    /// </summary>
    public static string ChoiceLabel(CombatChoice choice) => choice switch
    {
        CombatChoice.Thrust => UiText.S("combattext.thrust"),
        CombatChoice.Attack => UiText.S("combattext.attack"),
        CombatChoice.Slash => UiText.S("combattext.slash"),
        CombatChoice.Lunge => UiText.S("combattext.lunge"),
        CombatChoice.Cast => UiText.S("combattext.cast"),
        CombatChoice.Parry => UiText.S("combattext.parry"),
        CombatChoice.Toss => UiText.S("combattext.toss"),
        CombatChoice.FireBow => UiText.S("combattext.fire_bow"),
        CombatChoice.Aim => UiText.S("combattext.aim"),
        CombatChoice.ChangeRank => UiText.S("combattext.change_rank"),
        _ => UiText.F("combattext.order_x", (int)choice),
    };

    /// <summary>Order label for the orders list, e.g. "PARRY", "THRUST R2", "SPELL #5".</summary>
    public static string OrderLabel(RepeatOrder order) => order.Choice switch
    {
        CombatChoice.Thrust => UiText.S("combattext.thrust") + RankSuffix(order.RankIndex),
        CombatChoice.Attack => UiText.S("combattext.attack") + RankSuffix(order.RankIndex),
        CombatChoice.Slash => UiText.S("combattext.slash") + RankSuffix(order.RankIndex),
        CombatChoice.Lunge => UiText.S("combattext.lunge"),
        CombatChoice.Cast => UiText.F("combattext.spell_x", order.SpellIndex + 1) + RankSuffix(order.RankIndex), // III Fireflash R2 (D3-075)
        CombatChoice.Toss => UiText.S("combattext.toss") + UiText.F("combattext.at_rank_x", order.RankIndex + 1), // ST "TOSS AT RANK n"
        CombatChoice.FireBow => UiText.S("combattext.fire_bow") + RankSuffix(order.RankIndex),
        CombatChoice.Aim => UiText.S("combattext.aim"),
        _ => UiText.S("combattext.parry"),
    };

    /// <summary>
    /// The spells <paramref name="ch"/> may pick at "WHICH SPELL?" in combat (D-035 / manual p.12–13: combat spells only in
    /// encounters, non-combat spells only outside them, everywhere spells anywhere): known spells that are combat-only or
    /// everywhere (HEALING I–IV, RESURRECTION). Non-combat spells (MONSTER EVAL., VISION, TRANSPORTATION) and DIVINE I are left
    /// off. Spells the caster can't afford stay listed — DOS refuses them at pick time and the chooser takes the PARRY path.
    /// </summary>
    public static IReadOnlyList<int> CombatSpellChoices(Character ch, GameOptions? rules = null) =>
        Enumerable.Range(0, SpellSlots.For(rules).Count) // the game's spells (III: 57)
            .Where(id => id < ch.SpellsKnown.Length && Magic.KnowsSpell(ch, id) && IsCombatCastable(id, rules))
            .ToList();

    /// <summary>Combat-only (C/A, C/P) or everywhere (E) spell; any spell under D-063 <c>dos</c> (<see cref="SpellCaster.IsCombatCastable"/>).</summary>
    public static bool IsCombatCastable(int spellIndex, GameOptions? rules = null) => SpellCaster.IsCombatCastable(spellIndex, rules);

    private static string RankSuffix(int rankIndex) => rankIndex > 0 ? UiText.F("combattext.at_rank_x", rankIndex + 1) : ""; // [D✓] s65
}
