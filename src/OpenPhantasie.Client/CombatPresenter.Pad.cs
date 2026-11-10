using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>One entry of the pad's pick on a combat step: its label (or a spell, by Core index) and the keys it sends.</summary>
public sealed record CombatPadPick(string Label, IReadOnlyList<Keys> Keys, int SpellId = -1);

/// <summary>
/// "Pad coverage" (s182): what the pad does on the combat step waiting now (<see cref="CombatPresenter.PadStep"/>, read by
/// <see cref="CombatPad"/>). <see cref="Id"/> changes when the step does (the pick starts again). With
/// <see cref="Picks"/> the D-pad steps a pick through them and A sends the pick's keys; with none the D-pad is the arrows.
/// <see cref="PickOnLeftRight"/>: only Left / Right step the pick (Up / Down stay the step's own arrows).
/// <see cref="AKey"/>: what A sends before anything is picked. <see cref="LeftRightKey"/>: the key Left / Right send on a
/// step with no pick that has no use for them (III Amiga's spell window: M, the MORE button). <see cref="RightTriggerKey"/>:
/// the key RT sends (s183: III Amiga's spell / heal windows: C, the CANCEL button), or none. <see cref="AFirstPick"/>: A
/// with nothing picked sends the first pick (s192, pad fix C19: the DOS order steps that read only a digit, where Return
/// does nothing).
/// </summary>
public sealed record CombatPadStep(string Id, IReadOnlyList<CombatPadPick> Picks, bool PickOnLeftRight = false,
    Keys AKey = Keys.Enter, Keys LeftRightKey = Keys.None, Keys RightTriggerKey = Keys.None, bool AFirstPick = false);

public sealed partial class CombatPresenter
{
    private int _padSerial; // counts the rounds' orders, so a pick never outlives its round

    /// <summary>
    /// "Pad coverage" (s182): the pad on the combat step that waits for a key now, or null when none does. The menus that
    /// move with the arrows (PARTY OPTIONS, I / III Amiga's field menus, DOS III's rank menu, III Amiga's rank / spell /
    /// heal windows) take the D-pad as arrows and A as Return; the steps that take a digit or a letter (the order menu,
    /// the rank, the spell number, the heal target, the party rank, the Amiga order screens' members and buttons) get a
    /// pick of what that step accepts, each sent as the keys the keyboard types for it. The original screens' keys are
    /// unchanged: the pad only sends them.
    /// </summary>
    public CombatPadStep? PadStep(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Combat is null || !AwaitingInput)
            return null;
        if (Phase == CombatPhase.PartyOptions)
            return new CombatPadStep("options", []);
        var id = $"{_padSerial}|{_amiga}|{_p1Orders}|{_orderMember}|{_step}|{P1SpellFirst}|{_amigaFirst}|{_spellDigits}|{CastNotice.Length > 0}";
        if (_p1ToneThen is not null || CastNotice.Length > 0 && _amiga)
            return new CombatPadStep(id, []); // the tone takes no key; III Amiga's message box closes on any key
        var members = session.Party.Members;
        if (_amiga)
            return AmigaPadStep(session, id);
        if (_p1Orders)
        {
            if (_orderMember < 0)
                return new CombatPadStep(id, // s192 (pad fix C17): FIGHT (gadget 1) last, as III Amiga's Fight!
                [
                    .. P1OrderRows(session).Select((m, k) => new CombatPadPick(members[m].Name, [Keys.D1 + k])),
                    new CombatPadPick(UiText.S("p1field.item.fight"), [Keys.F]),
                ]);
            if (_step == OrderStep.Menu)
                return new CombatPadStep(id, MenuPicks(session));
            if (_step == OrderStep.SpellNumber && P1SpellLists)
                return new CombatPadStep(id,
                    [.. P1SpellPage(session).Select((spell, k) => new CombatPadPick("", [Keys.D1 + k], spell)), ExitPick()],
                    PickOnLeftRight: true); // Up / Down: the page's scroll gadgets
            if (_step == OrderStep.HealTarget && P1SpellLists)
                return new CombatPadStep(id,
                    [.. members.Take(Math.Min(members.Count, P1Layout!.SpellList!.Target.Rows))
                        .Select((m, k) => new CombatPadPick(m.Name, [Keys.D1 + k])), ExitPick()]);
        }
        if (_orderMember < 0)
            return new CombatPadStep(id, []);
        return _step switch
        {
            OrderStep.Menu => new CombatPadStep(id, MenuPicks(session)),
            OrderStep.Rank when AsksDosRankMenu(session) => new CombatPadStep(id, []), // its own lit word: arrows, Return
            // s192 (pad fix C19): these steps read a digit only, so A with nothing picked takes the first pick
            OrderStep.Rank => new CombatPadStep(id,
                [.. Enumerable.Range(1, LivingRanks(session)).Select(r => new CombatPadPick(UiText.F("pad.combat.rank_x", r), [Keys.D0 + r]))],
                AFirstPick: true),
            OrderStep.PartyRank => new CombatPadStep(id,
            [
                new(UiText.S("pad.combat.front"), [Keys.D1]), new(UiText.S("pad.combat.middle"), [Keys.D2]),
                new(UiText.S("pad.combat.back"), [Keys.D3]),
            ], AFirstPick: true),
            OrderStep.SpellNumber when _spellDigits.Length == 0 => new CombatPadStep(id,
                [.. SpellChoices(session).Select(spell =>
                    new CombatPadPick("", [.. (spell + 1).ToString().Select(c => Keys.D0 + (c - '0')), Keys.Enter], spell))],
                PickOnLeftRight: true, AFirstPick: true), // Up / Down scroll the list (no cursor)
            OrderStep.HealTarget => new CombatPadStep(id,
                [.. members.Take(9).Select((m, k) => new CombatPadPick(m.Name, [Keys.D1 + k]))], AFirstPick: true),
            _ => new CombatPadStep(id, []), // digits typed by the keyboard: A is Return
        };
    }

    private static CombatPadPick ExitPick() => new(UiText.S("p1field.exit"), [Keys.E]);

    /// <summary>The order menu by its digits (the DOS menu, I Amiga's requester 24 buttons): A unpicked keeps the order.</summary>
    private static IReadOnlyList<CombatPadPick> MenuPicks(GameSession session) =>
        [.. MenuChoices(session).Select((c, i) => new CombatPadPick(CombatText.ChoiceLabel(c), [Keys.D1 + i]))];

    /// <summary>III Amiga's orders screen (s118): the member boxes and Fight!, then the picked member's buttons.</summary>
    private CombatPadStep AmigaPadStep(GameSession session, string id)
    {
        var members = session.Party.Members;
        var fight = new CombatPadPick(UiText.S("p3combat.list.F"), [Keys.F]);
        if (_orderMember < 0)
        {
            var picks = Enumerable.Range(0, Math.Min(members.Count, MaxMembers)).Where(i => AmigaPickable(members[i]))
                .Select(i => new CombatPadPick(members[i].Name, [Keys.D1 + i])).Append(fight).ToList();
            return new CombatPadStep(id, picks, AKey: Keys.F); // A unpicked: Fight! (as I Amiga's Return is its FIGHT)
        }
        return _step switch
        {
            OrderStep.Menu => new CombatPadStep(id,
            [
                .. AmigaOptionKeys.Where(o => MenuChoices(session).Contains(o.Choice))
                    .Select(o => new CombatPadPick(UiText.S("p3combat.list." + o.Key), [o.Key])),
                .. new[] { Keys.D1, Keys.D2, Keys.D3 }.Select(k => new CombatPadPick(UiText.S("p3combat.list." + (k - Keys.D0)), [k])),
                fight,
            ]),
            // its cursor: Up / Down, Return; MORE; s183: RT = CANCEL (C: the order becomes Thrust)
            OrderStep.SpellNumber => new CombatPadStep(id, [], LeftRightKey: Keys.M, RightTriggerKey: Keys.C),
            OrderStep.HealTarget => new CombatPadStep(id, [], RightTriggerKey: Keys.C), // its cursor; RT = CANCEL (Thrust)
            _ => new CombatPadStep(id, []), // the rank window (A = OK, B = CANCEL)
        };
    }

    /// <summary>
    /// s183 (Pad leftovers): the pad's B on a DOS-style order list (not the Amiga order screens) goes back as Backspace
    /// does (<see cref="RestartOrderList"/>: the list again from its first member, orders given so far kept) once there is
    /// somewhere to go back to: past the first member's order menu. On that menu B opens the game menu as before.
    /// </summary>
    public bool PadBackRestartsOrders(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Combat is null || !AwaitingOrders || _amiga || _p1Orders || _orderMember < 0 || _p1ToneThen is not null)
            return false;
        return _step != OrderStep.Menu || _orderMember != NextOrderable(session, -1);
    }
}
