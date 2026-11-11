using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The IV battle on the 7 × 7 field (<see cref="P4Battle"/>), drawn as squares until IV's art (T6.4). Keys: the options
/// G / B / A / F / L; a member's orders: the pad moves him, then M Aim, L Lunge, A Attack, T Thrust, S Slash, F Fire Bow,
/// H Throw, P Parry, C Cast, Y Fly, N Land, X Leave (on an exit). A weapon or a square spell asks for the target with a
/// cursor (pad, Enter; Back cancels); a spell list and a member pick take digits or Up / Down and Enter.
/// </summary>
public sealed partial class P4DungeonScreen
{
    private enum BattleMode { Normal, Target, Spells, Member }

    private BattleMode _mode;
    private P4ActionKind _pending;
    private int _pendingSpell;

    /// <summary>The combat spell list's page (s152, <c>_getmagic(member, 0)</c>: page 0 each time Cast is chosen).</summary>
    private int _spellPage;

    /// <summary>The target cursor's square while a weapon or a square spell waits for its target.</summary>
    public int? TargetCursor { get; private set; }

    private void ResetBattleUi()
    {
        _mode = BattleMode.Normal;
        TargetCursor = null;
        Cursor = 0;
        _spellPage = 0;
    }

    private static readonly Dictionary<Keys, P4ActionKind> ActionKeys = new()
    {
        [Keys.M] = P4ActionKind.AimBlow, [Keys.L] = P4ActionKind.Lunge, [Keys.A] = P4ActionKind.Attack,
        [Keys.T] = P4ActionKind.Thrust, [Keys.S] = P4ActionKind.Slash, [Keys.F] = P4ActionKind.FireBow,
        [Keys.H] = P4ActionKind.Throw, [Keys.P] = P4ActionKind.Parry, [Keys.C] = P4ActionKind.Cast,
        [Keys.Y] = P4ActionKind.Fly, [Keys.N] = P4ActionKind.Land, [Keys.X] = P4ActionKind.Leave,
    };

    private static readonly P4EncounterOption[] OptionOrder =
    [
        P4EncounterOption.Fight, P4EncounterOption.Greet, P4EncounterOption.Beg, P4EncounterOption.Demand,
        P4EncounterOption.Flee, P4EncounterOption.AutoCombat, P4EncounterOption.AutoRound,
    ];

    private static Keys OptionKey(P4EncounterOption o) => o switch
    {
        P4EncounterOption.AutoCombat => Keys.Q,
        P4EncounterOption.AutoRound => Keys.R,
        P4EncounterOption.Greet => Keys.G,
        P4EncounterOption.Beg => Keys.B,
        P4EncounterOption.Demand => Keys.A,
        P4EncounterOption.Fight => Keys.F,
        _ => Keys.L,
    };

    private void BattleKey(P4Battle b, Keys key)
    {
        switch (b.Prompt)
        {
            case P4BattlePrompt.Options when b.AutoCombat:
                // Q fights to the end without the options menu (_xpopt 0): each key shows the next round.
                b.ChooseOption(P4EncounterOption.AutoCombat);
                ResetBattleUi();
                break;
            case P4BattlePrompt.Options:
                foreach (var o in OptionOrder)
                    if (key == OptionKey(o))
                    {
                        b.ChooseOption(o);
                        ResetBattleUi();
                        return;
                    }
                Pick(key, OptionOrder.Length, n => b.ChooseOption(OptionOrder[n]));
                break;
            case P4BattlePrompt.Order:
                OrderKey(b, key);
                break;
            case P4BattlePrompt.Treasure when key is Keys.Y or Keys.N:
                b.TakeItem(key == Keys.Y);
                break;
            case P4BattlePrompt.Over when key is Keys.Space or Keys.Enter:
                b.Continue();
                break;
        }
    }

    private void OrderKey(P4Battle b, Keys key)
    {
        var m = b.Current!;
        switch (_mode)
        {
            case BattleMode.Target:
                if (DirectionOf(key) is int d && P4Battle.Neighbour(TargetCursor!.Value, d) is int to)
                    TargetCursor = to;
                else if (key == Keys.Back)
                    ResetBattleUi();
                else if (key is Keys.Enter or Keys.Space)
                {
                    var order = _pending == P4ActionKind.Cast
                        ? new P4Order(P4ActionKind.Cast, TargetCursor!.Value, _pendingSpell)
                        : new P4Order(_pending, TargetCursor!.Value);
                    if (b.Order(order))
                        ResetBattleUi();
                    else
                        Note = UiText.S("p4.battle.refused");
                }
                return;
            case BattleMode.Spells:
                if (key == Keys.Back)
                {
                    ResetBattleUi();
                    return;
                }
                // s152: the page's castable spells, then the category pages and Quit (_getmagic(member, 0)'s head lines).
                Pick(key, b.CastableSpells(_spellPage).Count + b.SpellPages.Count + 1, n =>
                {
                    switch (b.SpellListChoice(_spellPage, n))
                    {
                        case ({ } spell, _, _):
                            ChooseSpell(b, spell);
                            break;
                        case (_, { } page, _):
                            _spellPage = page;
                            BoxScroll.ForgetFollow(); // s160: the new page's cursor line into view
                            break;
                        default:
                            ResetBattleUi();
                            break;
                    }
                });
                return;
            case BattleMode.Member:
                if (key == Keys.Back)
                {
                    ResetBattleUi();
                    return;
                }
                Pick(key, b.Members.Count, n =>
                {
                    if (!b.Order(new P4Order(P4ActionKind.Cast, n, _pendingSpell)))
                        Note = UiText.S("p4.battle.refused");
                    ResetBattleUi();
                });
                return;
        }
        if (key == Keys.Q)
        {
            // M auto move (Figure 15); M is Aim Blow in the order keys, so the remaster puts it on Q.
            b.AutoMove();
            ResetBattleUi();
            return;
        }
        if (DirectionOf(key) is int dir)
        {
            if (!b.Step(dir))
                Note = UiText.S("p4.battle.cannot_step");
            return;
        }
        if (!ActionKeys.TryGetValue(key, out var kind))
            return;
        if (!b.Offered().Contains(kind))
        {
            Note = UiText.S("p4.battle.refused");
            return;
        }
        switch (kind)
        {
            case P4ActionKind.AimBlow or P4ActionKind.Lunge or P4ActionKind.Attack or P4ActionKind.Thrust or P4ActionKind.Slash
                or P4ActionKind.FireBow or P4ActionKind.Throw:
                _pending = kind;
                _mode = BattleMode.Target;
                TargetCursor = b.Monsters.Where(x => x.OnField)
                    .OrderBy(x => P4Battle.Range2(m.Square, x.Square)).Select(x => (int?)x.Square).FirstOrDefault() ?? m.Square;
                break;
            case P4ActionKind.Cast:
                // s152: _getmagic(member, 0) opens on page 0 even with nothing castable (letters only; 6 quits).
                _mode = BattleMode.Spells;
                Cursor = 0;
                _spellPage = 0;
                BoxScroll.ForgetFollow(); // s160: the narrow column scrolls to the list's cursor line at once
                break;
            default:
                if (!b.Order(new P4Order(kind)))
                    Note = UiText.S("p4.battle.refused");
                break;
        }
    }

    private void ChooseSpell(P4Battle b, int spell)
    {
        _pendingSpell = spell;
        _pending = P4ActionKind.Cast;
        switch (b.TargetOf(spell))
        {
            case P4Battle.SpellTarget.Member:
                _mode = BattleMode.Member;
                Cursor = 0;
                break;
            case P4Battle.SpellTarget.Square:
                _mode = BattleMode.Target;
                var m = b.Current!;
                TargetCursor = b.Monsters.Where(x => x.OnField)
                    .OrderBy(x => P4Battle.Range2(m.Square, x.Square)).Select(x => (int?)x.Square).FirstOrDefault() ?? m.Square;
                break;
            default:
                if (!b.Order(new P4Order(P4ActionKind.Cast, Spell: spell)))
                    Note = UiText.S("p4.battle.refused");
                ResetBattleUi();
                break;
        }
    }

    // --- What is shown ------------------------------------------------------------------------------------------------

    private static string ActionName(P4ActionKind k) => UiText.S("p4.battle.action." + k.ToString().ToLowerInvariant());

    /// <summary>The battle's string ids built at run time: actions, options, ends and field names (string-table test).</summary>
    public static IEnumerable<string> BattleStringKeys() =>
        Enum.GetValues<P4ActionKind>().Select(k => "p4.battle.action." + k.ToString().ToLowerInvariant())
            .Concat(OptionOrder.Select(o => "p4.battle.option." + o.ToString().ToLowerInvariant()))
            .Concat(Enum.GetValues<P4BattleEnd>().Select(e => "p4.battle.end." + e.ToString().ToLowerInvariant()))
            .Concat(new[] { "dungeon", "ship", "desert", "hill", "forest", "grass", "mist", "nether" }
                .Select(f => "p4.battle.field." + f));

    private List<MenuLine> BattleLines(P4Battle b)
    {
        var lines = new List<MenuLine>
        {
            new(UiText.F("p4.battle.title_x_y", UiText.S("p4.battle.field." + b.Field), b.Round + 1), MenuLineStyle.Title),
            new(string.Join(" ", b.Monsters.Where(x => x.OnField).Select(x => $"{x.Letter}:{x.Name}")), MenuLineStyle.Dim),
        };
        switch (b.Prompt)
        {
            case P4BattlePrompt.Options when b.AutoCombat:
                lines.Add(new(PadS("p4.battle.auto_continue"), MenuLineStyle.Header));
                break;
            case P4BattlePrompt.Options:
                for (var i = 0; i < OptionOrder.Length; i++)
                    lines.Add(Option(i, UiText.S("p4.battle.option." + OptionOrder[i].ToString().ToLowerInvariant())));
                break;
            case P4BattlePrompt.Order:
            {
                var m = b.Current!;
                lines.Add(new(UiText.F("p4.battle.orders_x_y", m.Member.Name, m.MoveLeft), MenuLineStyle.Header));
                switch (_mode)
                {
                    case BattleMode.Target:
                        lines.Add(new(UiText.F("p4.battle.target_x", _pending == P4ActionKind.Cast
                            ? UiText.F("p4.battle.spell_x", _pendingSpell) : ActionName(_pending)), MenuLineStyle.Selected));
                        break;
                    case BattleMode.Spells:
                    {
                        // s152: the page's castable spells by name, then the pages and Quit (the list's head lines).
                        var spells = b.CastableSpells(_spellPage);
                        for (var i = 0; i < spells.Count; i++)
                            lines.Add(Option(i, $"{i + 1} " + SpellName(spells[i])));
                        var heads = b.SpellPageHeads;
                        for (var i = 0; i <= b.SpellPages.Count; i++)
                        {
                            var n = spells.Count + i;
                            var text = i < heads.Count ? HeadText(GameText.S(heads[i]))
                                : i < b.SpellPages.Count ? UiText.F("p4.battle.spell_page_x", i + 1) : UiText.S("p4.battle.spell_quit");
                            lines.Add(Option(n, (n < 9 ? $"{n + 1} " : "  ") + text));
                        }
                        break;
                    }
                    case BattleMode.Member:
                        lines.Add(new(UiText.S("p4.dungeon.who"), MenuLineStyle.Header));
                        for (var i = 0; i < b.Members.Count; i++)
                            lines.Add(Option(i, $"{i + 1} {b.Members[i].Member.Name}"));
                        break;
                    default:
                        // s182: the pad's order (X / Y pick it, A gives it) in brackets once the pad has picked one.
                        // s191 (pad fix D26): and Q auto move, bracketed, while the pad has picked it.
                        lines.Add(new(string.Join(" ", b.Offered().Select(k => !_padAutoMove && k == _padAction
                                ? $"[{ActionName(k)}]" : ActionName(k)))
                            + (_padAutoMove ? $" [{UiText.S("p4.battle.action.auto_move")}]" : ""), MenuLineStyle.Normal));
                        break;
                }
                break;
            }
            case P4BattlePrompt.Treasure:
                lines.Add(new(UiText.S("p4.outdoor.yes_no"), MenuLineStyle.Selected));
                break;
            case P4BattlePrompt.Over:
                lines.Add(new(UiText.S("p4.battle.end." + b.End.ToString().ToLowerInvariant()), MenuLineStyle.Header));
                break;
        }
        foreach (var l in b.Prompt == P4BattlePrompt.Treasure ? b.TreasureLines : b.Log.TakeLast(10))
            lines.Add(new(l, MenuLineStyle.Dim));
        return lines;
    }

    /// <summary>
    /// A head line's text for the text column without the original's own key ("1 :Non-combat" → "Non-combat"): the column
    /// numbers its lines with the remaster's digits, which differ (the page lines follow the page's spells).
    /// </summary>
    public static string HeadText(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        var colon = label.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && label[..colon].Trim() is { Length: > 0 } key && key.All(char.IsAsciiDigit)
            ? label[(colon + 1)..].TrimStart() : label;
    }

    /// <summary>A spell's name from the IV names (the number when they are not loaded).</summary>
    private string SpellName(int spell) => Game.Services?.Names.Spells is { Count: > 0 } names
        ? P4Names.At(names, spell - 1) : UiText.F("p4.battle.spell_x", spell);

    /// <summary>The combat spell list up now (s152): the member's orders, Cast chosen, the list on its page.</summary>
    private P4ListWindow? BattleSpellList => Game.Battle is { Prompt: P4BattlePrompt.Order } b && _mode == BattleMode.Spells
        ? b.SpellList(_spellPage, Game.Services?.Names.Spells ?? []) : null;

    private string BattleHelp(P4Battle b) => b.Prompt switch
    {
        P4BattlePrompt.Options when b.AutoCombat => UiText.S("p4.battle.help_next_round"), // s193 (E28)
        P4BattlePrompt.Options => UiText.S("p4.battle.help_options"),
        P4BattlePrompt.Order => _mode switch
        {
            BattleMode.Target => UiText.S("p4.battle.help_target"),
            BattleMode.Spells or BattleMode.Member => UiText.S("p4.battle.help_pick"),
            _ when b.AutoCombat => UiText.S("p4.battle.help_next_round"),
            _ => UiText.S("p4.battle.help_orders"),
        },
        P4BattlePrompt.Treasure => UiText.S("p4.outdoor.help_confirm"),
        _ => UiText.S("p4.dungeon.help_wait"),
    };

    private static List<string> BattlePartyLines(P4Battle b) =>
        [.. b.Members.Select(m => UiText.F("p4.battle.member_x_y_z_w_v", m.Slot + 1, m.Member.Name, m.Member.Hp, m.Member.HpMax,
            m.MoveLeft) + " " + (m.Gone ? UiText.S("p4.battle.gone") : !m.Member.Alive ? UiText.S("p4.dungeon.dead")
            : m.State switch
            {
                0 => m.Member.Condition == P4Member.Asleep ? UiText.S("p4.dungeon.asleep") : UiText.S("p4.battle.unconscious"),
                2 => UiText.S("p4.battle.down"),
                4 => UiText.S("p4.battle.out"),
                _ => ActionName(m.Order.Kind),
            }))];

    private static readonly Color FieldFloor = new(60, 80, 60), DungeonFloor = new(50, 54, 66), Rock = new(22, 22, 28),
        Brush = new(80, 100, 70), Exit = new(220, 190, 90), Foe = new(210, 80, 70), Cursor1 = new(250, 240, 120);

    private void DrawBattle(SpriteBatch batch, Texture2D pixel, Rectangle area, P4Battle b)
    {
        const int cell = 16;
        var x0 = area.X + (area.Width - cell * P4Battle.Size) / 2;
        var y0 = area.Y + (area.Height - cell * P4Battle.Size) / 2;
        var current = b.Current;
        for (var sq = 0; sq < P4Battle.Squares; sq++)
        {
            var r = new Rectangle(x0 + P4Battle.Col(sq) * cell, y0 + P4Battle.Row(sq) * cell, cell, cell);
            var floor = b.Obstacle(sq) < 0 ? (b.Outdoors ? FieldFloor : DungeonFloor) : b.Walkable(sq) ? Brush : Rock;
            Fill(batch, pixel, new Rectangle(r.X + 1, r.Y + 1, cell - 2, cell - 2), floor);
            if (b.Exits.Contains(sq))
                Outline(batch, pixel, r, Exit);
            if (b.MemberAt(sq) is { } m)
            {
                Fill(batch, pixel, new Rectangle(r.X + 3, r.Y + 3, cell - 6, cell - 6), m.Flying ? Selected : Party);
                PixelFont.Draw(batch, pixel, r.X + 5, r.Y + 4, (m.Slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Back);
                if (m == current)
                    Outline(batch, pixel, r, Color.White);
            }
            else if (b.MonsterAt(sq) is { } mon)
            {
                Fill(batch, pixel, new Rectangle(r.X + 3, r.Y + 3, cell - 6, cell - 6), Foe);
                PixelFont.Draw(batch, pixel, r.X + 5, r.Y + 4, mon.Letter.ToString(), Back);
            }
            if (TargetCursor == sq)
                Outline(batch, pixel, new Rectangle(r.X + 1, r.Y + 1, cell - 2, cell - 2), Cursor1);
        }
    }
}
