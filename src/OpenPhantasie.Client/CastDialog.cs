using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Option → Cast spell outside combat (<see cref="GameSession.CastSpell"/>): "WHO WILL CAST SPELL:" → spell → HEALING target.
/// Lists only the caster's known spells that can be cast here (D-035, manual: rejected before any MP is spent).
/// </summary>
public sealed class CastDialog : IModalDialog, IPagedDialog
{
    private enum Step { Closed, Caster, Spell, Target }

    private const int PageSize = 9;

    private Step _step = Step.Closed;
    private Character? _caster;
    private int _spell;
    private List<int> _spells = [];
    private int _scroll;

    public bool IsOpen => _step != Step.Closed;

    public static CastLocation? LocationFor(GameMode mode) => mode switch
    {
        GameMode.Town => CastLocation.Town,
        GameMode.Overworld => CastLocation.Field,
        GameMode.Dungeon => CastLocation.Dungeon,
        _ => null,
    };

    public string? Open(GameSession session)
    {
        if (LocationFor(session.Mode) is null || session.Party.Members.Count == 0)
            return UiText.S("cast.no_spells_can_be_cast_here");
        _step = Step.Caster;
        _caster = null;
        _scroll = 0;
        return null;
    }

    public ListPage? Page => _step == Step.Spell ? ListPage.Of(1, PageSize, _scroll, _spells.Count) : null;

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) => _step switch
    {
        Step.Caster => [UiText.S("cast.who_will_cast_spell"), .. session.Party.Members.Select((m, i) =>
            UiText.F("cast.x_x_mp_x_x", i + 1, m.Name, m.MagicCur, m.MagicMax))],
        Step.Spell => [UiText.F("cast.x_casts", _caster!.Name), .. _spells.Skip(_scroll).Take(PageSize).Select((id, i) =>
            UiText.F("cast.x_x_x_mp", i + 1, data.GetSpell(id).Name, Magic.MagicPointCost(id, SpellSlots.For(session.Options))))],
        Step.Target => [UiText.S("cast.heal_whom"), .. session.Party.Members.Select((m, i) => $"{i + 1} {m.Name}  {m.HpCur}/{m.HpMax}")],
        _ => [],
    };

    public string Help => _step switch
    {
        Step.Spell when _spells.Count > PageSize => UiText.S("cast.1_9_choose_up_down_more"),
        Step.Closed => "",
        _ => UiText.S("cast.1_9_choose_esc_cancel"),
    };

    /// <summary>Handle this frame's new keys; returns a HUD line when the dialog closes.</summary>
    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (key is Keys.Escape or Keys.Back)
            {
                _step = Step.Closed;
                return UiText.S("cast.cancelled");
            }
            if (_step == Step.Spell && key is Keys.Up or Keys.Down)
            {
                _scroll = key == Keys.Up
                    ? Math.Max(0, _scroll - 1)
                    : Math.Min(Math.Max(0, _spells.Count - PageSize), _scroll + 1);
                continue;
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
                case Step.Caster when digit <= session.Party.Members.Count:
                    _caster = session.Party.Members[digit - 1];
                    var here = LocationFor(session.Mode)!.Value;
                    _spells = SpellCaster.OutOfCombatChoices(_caster, here, session.Options).ToList();
                    if (_spells.Count == 0)
                    {
                        _step = Step.Closed;
                        // s158: no spell known at all has its own line (III: S443 "This character knows no spells.", DOS
                        // root 0x8d8a / Amiga 0x84e0 / 0xb8aa); known spells none of which is castable here keep the other.
                        return Array.IndexOf(_caster.SpellsKnown, 1) < 0
                            ? UiText.F("cast.x_knows_no_spells", _caster.Name)
                            : UiText.F("cast.x_knows_no_spell_that_can", _caster.Name);
                    }
                    _scroll = 0;
                    _step = Step.Spell;
                    return null;
                case Step.Spell when _scroll + digit - 1 < _spells.Count:
                    _spell = _spells[_scroll + digit - 1];
                    if (_spell <= SpellId.HealingIv)
                    {
                        _step = Step.Target;
                        return null;
                    }
                    return Cast(session, null);
                case Step.Target when digit <= session.Party.Members.Count:
                    return Cast(session, session.Party.Members[digit - 1]);
            }
        }
        return null;
    }

    private string Cast(GameSession session, Character? target)
    {
        _step = Step.Closed;
        return session.CastSpell(_caster!, _spell, target).Message; // extra log lines go to the session log
    }
}
