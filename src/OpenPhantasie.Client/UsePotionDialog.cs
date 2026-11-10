using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Options → Use potion (OV2 <c>0x43e2</c>, wilderness / dungeon Options menu <c>0x14</c>; manual p.13 U)SE POTION; town
/// U)SE ITEM for potions) [C] (s49 static): `potion.who_will_use_potion` (member picker <c>0x5a0a</c> mode 1 — OK members), the
/// member's potions (<c>0x5b5f</c>), "ON WHOM:" (picker mode 4), then <see cref="Magic.UsePotion"/>. OV2 calls <c>0x43e2</c>
/// only from the Options menu: there is no potion order in combat (DOS orders bar THRUST…PARRY; manual p.10 options 1–7).
/// Picker mode 4 offers every party slot below <c>[0x648e]</c> whatever its status, dead and asleep included [C] (the combat
/// HEALING "ON WHOM :" uses the same mode); so does the ON WHOM step here.
/// </summary>
public sealed class UsePotionDialog : IModalDialog, IPadWheel
{
    /// <summary>Pad fix B7 (s188): the pad wheel on each step's numbers (<see cref="IPadWheel"/>).</summary>
    public string? PadWheel(GameSession session) => _step switch
    {
        Step.User => DialogPad.Picks(Users(session).Count),
        Step.Potion => DialogPad.Picks(_slots.Count),
        Step.Target => DialogPad.Picks(session.Party.Members.Count),
        _ => null,
    };

    private enum Step { Closed, User, Potion, Target }

    private Step _step = Step.Closed;
    private Character? _user;
    private int _slot;
    private List<int> _slots = [];

    public bool IsOpen => _step != Step.Closed;

    public string Help => _step == Step.Closed ? "" : UiText.S("potion.1_9_choose_esc_cancel");

    /// <summary>Members picker mode 1 offers (status OK).</summary>
    public static IReadOnlyList<int> Users(GameSession session) =>
        Enumerable.Range(0, session.Party.Members.Count)
            .Where(i => session.Party.Members[i].Status == CharacterStatus.Ok)
            .ToList();

    /// <summary>Inventory slots holding a HEALING (101–110) or MAGIC (111–120) potion.</summary>
    public static List<int> PotionSlots(Character ch) =>
        Enumerable.Range(0, ch.InventoryItemIds.Count).Where(i => Magic.IsPotion(ch.InventoryItemIds[i])).ToList();

    public string? Open(GameSession session)
    {
        if (session.Mode is GameMode.Combat or GameMode.Title || Users(session).Count == 0)
            return UiText.S("potion.no_one_can_use_a_potion");
        _step = Step.User;
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) => _step switch
    {
        Step.User => [UiText.S("potion.who_will_use_potion"), .. Users(session).Select((m, i) => $"{i + 1} {session.Party.Members[m].Name}")],
        Step.Potion => [$"{_user!.Name}:", .. _slots.Take(9).Select((s, i) =>
            $"{i + 1} {data.Items.FirstOrDefault(it => it.Id == _user.InventoryItemIds[s])?.Name ?? "#" + _user.InventoryItemIds[s]}")],
        Step.Target => [UiText.S("potion.on_whom"), .. session.Party.Members.Select((m, i) => UiText.F("potion.x_x_x_x_mp_x", i + 1, m.Name, m.HpCur, m.HpMax, m.MagicCur, m.MagicMax))],
        _ => [],
    };

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (key is Keys.Escape or Keys.Back)
            {
                _step = Step.Closed;
                return UiText.S("potion.cancelled");
            }
            var digit = key switch
            {
                >= Keys.D1 and <= Keys.D9 => key - Keys.D0,
                >= Keys.NumPad1 and <= Keys.NumPad9 => key - Keys.NumPad0,
                _ => 0,
            };
            if (digit == 0)
                continue;
            switch (_step)
            {
                case Step.User when digit <= Users(session).Count:
                    _user = session.Party.Members[Users(session)[digit - 1]];
                    _slots = PotionSlots(_user);
                    if (_slots.Count == 0)
                    {
                        _step = Step.Closed;
                        return UiText.F("potion.x_has_no_potions", _user.Name);
                    }
                    _step = Step.Potion;
                    return null;
                case Step.Potion when digit <= Math.Min(9, _slots.Count):
                    _slot = _slots[digit - 1];
                    _step = Step.Target;
                    return null;
                case Step.Target when digit <= session.Party.Members.Count:
                    _step = Step.Closed;
                    return Magic.UsePotion(_user!, session.Party.Members[digit - 1], _slot).Message;
            }
        }
        return null;
    }
}
