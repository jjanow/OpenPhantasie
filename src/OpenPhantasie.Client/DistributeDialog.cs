using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The DOS item hand-out screen (OV1 <c>0x2bed</c>, <c>town-menus.md</c> §Distribute) over
/// <see cref="GameSession.ItemDistribution"/>: "NAME CLASS WPN ARM SHL #", the SELL row (carried gold) and the SACK row
/// (found items), one row per member, then the pooled items with "&lt;====" on the current one. The DOS menu cursor moves
/// over SELL, SACK and the members and starts on member 1; Enter gives. The manual's keys work too: 1–6 a member, 7 sell.
/// There is no cancel (DOS asks again until every item is placed); a refused pick (full member / sack) beeps in DOS.
/// A game with its own worn columns (<see cref="GameSession.HandOutWornPoints"/>; III's ovl2 <c>0x0de6</c> "SH AR BW WP")
/// shows those points in place of WPN ARM SHL.
/// </summary>
public sealed class DistributeDialog : IModalDialog
{
    public static string Header => UiText.S("distribute.name_class_wpn_arm_shl");

    /// <summary>The header row for <paramref name="session"/>'s game: I's, or the worn-point columns' (III: SH AR BW WP).</summary>
    public static string HeaderFor(GameSession session) =>
        session.Campaign.HandOutWornColumns(session) is null ? Header : UiText.S("distribute.name_class_sh_ar_bw_wp");

    private static string[] ClassNames => [UiText.S("distribute.thief"), UiText.S("distribute.monk"), UiText.S("distribute.ranger"), UiText.S("distribute.fighter"), UiText.S("distribute.priest"), UiText.S("distribute.wizard")];

    /// <summary>Menu row: 0 SELL, 1 SACK, 2.. members (DOS <c>[0x1674]</c>, starts at 2).</summary>
    public int Cursor { get; private set; } = 2;

    private string _note = "";

    public bool IsOpen { get; private set; }

    public string Help => UiText.S("distribute.up_down_enter_give_1_6");

    public string? Open(GameSession session, bool foundOnly = false)
    {
        if (session.BeginItemDistribution(foundOnly) is not { IsDone: false })
            return UiText.S("distribute.nothing_to_distribute");
        IsOpen = true;
        Cursor = 2;
        _note = "";
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        var party = session.Party;
        string Mark(int row) => row == Cursor ? ">" : " ";
        var lines = new List<string>
        {
            HeaderFor(session),
            $"{Mark(0)}{"SELL",-22}{party.GoldCarried,10}",
            $"{Mark(1)}{"SACK",-31}{session.Sack.ItemCount}",
        };
        for (var i = 0; i < party.Members.Count; i++)
        {
            var m = party.Members[i];
            var name = m.Name.Length > 9 ? m.Name[..9] : m.Name;
            var cls = (uint)m.Class < ClassNames.Length ? ClassNames[(int)m.Class][..3] : "?";
            var worn = session.HandOutWornPoints(m) is { } points
                ? string.Concat(points.Select(p => $"{p,4}"))
                : $"{m.WeaponDamageB,4}{m.ArmorSoak,4}{m.ShieldSoak,4}";
            lines.Add($"{Mark(i + 2)}{i + 1,-2}{name,-11}{cls,-4}{worn}{CarriedItems.Count(m),4}");
        }
        lines.Add(_note.Length > 0 ? _note : "---------------------------------");
        // Every item still to hand out (s99): the panel scrolls when the list runs past the screen.
        var pool = session.ItemDistribution?.Remaining ?? [];
        lines.AddRange(pool.Select((id, k) =>
            ItemDistribution.ItemLine(id, data.Items.FirstOrDefault(it => it.Id == id)?.Name ?? $"#{id}", party, session.Gear)
            + (k == 0 ? " <====" : "")));
        return lines;
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        var rows = session.Party.Members.Count + 2;
        foreach (var key in newKeys)
        {
            DistributeTarget? pick = null;
            if (key == Keys.Up)
                Cursor = Math.Max(0, Cursor - 1);
            else if (key == Keys.Down)
                Cursor = Math.Min(rows - 1, Cursor + 1);
            else if (key is Keys.Enter or Keys.Space)
                pick = new DistributeTarget(Cursor - 2);
            else if (DialogKeys.Digit(key) is var d and >= 1)
                pick = d == 7 ? DistributeTarget.Sell
                    : d <= session.Party.Members.Count ? DistributeTarget.Member(d - 1) : null;
            if (pick is not { } target)
                continue;
            var ok = session.GiveDistributedItem(target);
            _note = ok ? "" : session.LastMessage; // DOS beeps and asks again
            if (session.ItemDistribution is null)
            {
                IsOpen = false;
                return UiText.S("distribute.done");
            }
            return null;
        }
        return null;
    }
}
