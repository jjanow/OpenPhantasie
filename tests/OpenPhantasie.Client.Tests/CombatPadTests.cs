using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// "Pad coverage" (s182): a fight in I, II and III, each layout, played with the pad alone (<see cref="CombatPad"/> over
/// <see cref="CombatPresenter.PadStep"/>), as Game1 feeds it: the pad's keys after the keyboard's, the rest of the frame
/// dropped once the phase changes.
/// </summary>
public class CombatPadTests
{
    private static readonly GameDataRepository P2Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private static CombatFieldLayout P1Layout() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.CombatField!;

    /// <summary>One frame of pad presses: the keys they send go to the presenter until one changes the phase.</summary>
    private static IReadOnlyList<Keys> Press(CombatPad pad, CombatPresenter ui, GameSession s, params Buttons[] buttons)
    {
        var keys = pad.KeysFor(ui.PadStep(s), buttons);
        Assert.DoesNotContain(keys, k => k is Keys.Escape or Keys.PageUp or Keys.PageDown); // B, LB, RB are the game's own
        foreach (var key in keys)
        {
            if (ui.HandleKey(s, key))
                break;
        }
        return keys;
    }

    /// <summary>The D-pad presses that land the pick on the first entry <paramref name="match"/> accepts, then A.</summary>
    private static Buttons[] PickThenA(CombatPadStep step, Func<CombatPadPick, bool> match)
    {
        var index = step.Picks.ToList().FindIndex(p => match(p));
        Assert.True(index >= 0, "no such pick: " + string.Join(", ", step.Picks.Select(p => p.Label)));
        var move = step.PickOnLeftRight ? Buttons.DPadRight : Buttons.DPadDown;
        return [.. Enumerable.Repeat(move, index + 1), Buttons.A];
    }

    private static bool IsThrust(CombatPadPick p) => p.Label.Equals("thrust", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A pad-only player: Fight at the options, Thrust for every member (the Amiga screens: each member still on PARRY is
    /// picked, then Fight!), the first rank / target, A elsewhere; the round plays out between. True when the fight ended.
    /// </summary>
    private static bool PlayToTheEnd(GameSession s, CombatPresenter ui)
    {
        var pad = new CombatPad();
        for (var i = 0; i < 2000; i++)
        {
            if (!ui.IsPresenting && s.Mode != GameMode.Combat)
                return true;
            if (!ui.AwaitingInput)
            {
                pad.KeysFor(null, []);
                ui.Update(s, 10_000, fastForward: false);
                continue;
            }
            var step = ui.PadStep(s)!;
            Buttons[] buttons;
            if (step.Picks.Count == 0)
                buttons = [Buttons.A];
            else if (step.Picks.Any(IsThrust))
                buttons = PickThenA(step, IsThrust);
            else if (ui.AmigaOrdersShown && ui.OrderMember < 0 || ui.P1OrdersShown && ui.P1PickedMember < 0)
            {
                var parry = s.Party.Members.Select((m, k) => (m, k))
                    .FirstOrDefault(p => p.k < 7 && ui.OrderLabelFor(s, p.k).StartsWith("PARRY", StringComparison.Ordinal)
                        && step.Picks.Any(x => x.Label == p.m.Name));
                buttons = parry.m is { } member ? PickThenA(step, p => p.Label == member.Name) : [Buttons.A]; // A: Fight!
            }
            else
                buttons = PickThenA(step, _ => true);
            Press(pad, ui, s, buttons);
        }
        return false;
    }

    private static GameSession P3Fight(int members = 4)
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(7));
        s.StartNewGame(null);
        s.OpenGuild();
        var classes = new[] { CharacterClass.Fighter, CharacterClass.Priest, CharacterClass.Wizard, CharacterClass.Thief };
        for (var i = 0; i < members; i++)
            Assert.True(s.TryAddToParty(s.CreateAndEnroll("HERO" + i, Race.Human, classes[i]), out var err), err);
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0, SurpriseFlags.Normal);
        return s;
    }

    private static GameSession P2Fight()
    {
        var s = GameSessionFactory.Create(P2Data, new SeededDosRandomSource(7));
        s.StartNewGame(null);
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("HERO0", CharacterClass.Fighter), ("HERO1", CharacterClass.Thief) })
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(name, Race.Human, cls), out var err), err);
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0, SurpriseFlags.Normal);
        return s;
    }

    [OriginalDataFact]
    public void P1_Dos_AWholeFight_ByPadAlone()
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        Assert.True(PlayToTheEnd(s, ui));
    }

    [OriginalDataFact]
    public void P1_Amiga_AWholeFight_ByPadAlone()
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings()) { P1Layout = P1Layout(), AmigaP1Field = () => true };
        ui.Start(s);
        Assert.True(ui.P1FieldMenuShown);
        Assert.True(PlayToTheEnd(s, ui));
    }

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void P2_AWholeFight_ByPadAlone(bool stLines)
    {
        var s = P2Fight();
        var table = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!.StCombatLines!;
        var ui = new CombatPresenter(new ClientSettings()) { P2Lines = stLines ? () => table : null, P2SoundSeconds = _ => 0 };
        ui.Start(s);
        Assert.True(PlayToTheEnd(s, ui));
    }

    [OriginalDataFact]
    public void P3_Dos_AWholeFight_ByPadAlone()
    {
        var s = P3Fight();
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        Assert.True(PlayToTheEnd(s, ui));
    }

    [OriginalDataFact]
    public void P3_Amiga_AWholeFight_ByPadAlone()
    {
        var s = P3Fight();
        var ui = new CombatPresenter(new ClientSettings()) { AmigaField = () => true, AmigaOrders = () => true };
        ui.Start(s);
        Assert.True(ui.AmigaFieldMenuShown);
        Assert.True(PlayToTheEnd(s, ui));
    }

    [OriginalDataFact]
    public void P3_AmigaOrders_DosOrders_OffTheField_ByPadAlone()
    {
        var s = P3Fight();
        var ui = new CombatPresenter(new ClientSettings()) { AmigaOrders = () => true };
        ui.Start(s);
        Assert.True(PlayToTheEnd(s, ui));
    }

    [OriginalDataFact]
    public void PartyOptions_DPadMovesTheMenu_AChoosesIt()
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        var pad = new CombatPad();
        var at = ui.OptionCursor;
        Press(pad, ui, s, Buttons.DPadDown);
        Assert.Equal((at + 1) % CombatText.PartyOptionMenu.Count, ui.OptionCursor);
        Press(pad, ui, s, Buttons.LeftThumbstickUp);
        Assert.Equal(at, ui.OptionCursor);
        Press(pad, ui, s, Buttons.A); // FIGHT (the default)
        Assert.Equal(CombatPhase.Orders, ui.Phase);
    }

    [OriginalDataFact]
    public void OrderMenu_AUnpicked_KeepsTheOrder_APicked_SendsItsDigit()
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        var pad = new CombatPad();
        Press(pad, ui, s, Buttons.A);
        Assert.Equal(0, ui.OrderMember);
        Assert.Equal([Keys.Enter], Press(pad, ui, s, Buttons.A)); // keep PARRY
        Assert.Equal(1, ui.OrderMember);
        Press(pad, ui, s, Buttons.DPadDown, Buttons.DPadDown); // 2) ATTACK
        Assert.Equal(UiText.F("pad.event_pick_x_x", 2, CombatText.ChoiceLabel(CombatPresenter.MenuChoices(s)[1])),
            pad.Help(ui.PadStep(s)));
        Assert.Equal([Keys.D2], Press(pad, ui, s, Buttons.A));
        Assert.StartsWith(CombatText.ChoiceLabel(CombatChoice.Attack), ui.OrderLabelFor(s, 1));
        Assert.Null(pad.Help(ui.PadStep(s)));
    }

    [OriginalDataFact]
    public void Dos_Cast_SpellByLeftRight_UpDownScroll_ThenTheHealTarget()
    {
        var s = ClientSessions.Fight();
        var priest = s.Party.Members[1];
        Array.Clear(priest.SpellsKnown);
        priest.SpellsKnown[0] = 1; // Healing I
        priest.SpellsKnown[4] = 1;
        priest.MagicCur = 10;
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        var pad = new CombatPad();
        Press(pad, ui, s, Buttons.A); // FIGHT
        Press(pad, ui, s, Buttons.A); // member 1 keeps PARRY
        var cast = CombatPresenter.MenuChoices(s).ToList().IndexOf(CombatChoice.Cast);
        Press(pad, ui, s, PickThenA(ui.PadStep(s)!, p => p.Label == CombatText.ChoiceLabel(CombatChoice.Cast)));
        Assert.True(cast >= 0);
        var step = ui.PadStep(s)!;
        Assert.True(step.PickOnLeftRight);
        Assert.Equal(ui.SpellChoices(s), step.Picks.Select(p => p.SpellId));
        Assert.Equal([Keys.Down], Press(pad, ui, s, Buttons.DPadDown)); // the list's own scroll
        Press(pad, ui, s, Buttons.DPadRight);
        Assert.Equal(UiText.F("pad.event_pick_x_x", 1, "#1 HEAL"), pad.Help(ui.PadStep(s), _ => "HEAL"));
        Assert.Equal([Keys.D1, Keys.Enter], Press(pad, ui, s, Buttons.A));
        Assert.True(ui.AsksHealTarget);
        Assert.Equal(s.Party.Members.Select(m => m.Name), ui.PadStep(s)!.Picks.Select(p => p.Label));
        Press(pad, ui, s, Buttons.DPadDown, Buttons.A); // the fighter
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
    }

    [OriginalDataFact]
    public void P3_Dos_RankMenu_IsArrowsAndA_PartyRank_IsAPick()
    {
        var s = P3Fight();
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        var pad = new CombatPad();
        Press(pad, ui, s, Buttons.A); // FIGHT
        var choices = CombatPresenter.MenuChoices(s);
        if (choices.Contains(CombatChoice.ChangeRank))
        {
            Press(pad, ui, s, PickThenA(ui.PadStep(s)!, p => p.Label == CombatText.ChoiceLabel(CombatChoice.ChangeRank)));
            var picks = ui.PadStep(s)!.Picks;
            Assert.Equal([UiText.S("pad.combat.front"), UiText.S("pad.combat.middle"), UiText.S("pad.combat.back")],
                picks.Select(p => p.Label));
            Press(pad, ui, s, Buttons.DPadUp, Buttons.A); // BACK (wraps to the last)
            Assert.Equal(0, ui.OrderMember); // the member still picks an order
        }
        if (ui.AsksDosRank(s) || s.Combat!.Ranks.Count(r => !r.IsWiped) != 2)
            return;
        // Thieves are asked; walk to the Thief (member 4) keeping the others' orders.
        while (ui.OrderMember is >= 0 and < 3)
            Press(pad, ui, s, Buttons.A);
        Press(pad, ui, s, PickThenA(ui.PadStep(s)!, IsThrust));
        Assert.True(ui.AsksDosRank(s));
        Assert.Empty(ui.PadStep(s)!.Picks);
        Assert.Equal([Keys.Right], Press(pad, ui, s, Buttons.DPadRight));
        Press(pad, ui, s, Buttons.A); // Two
        Assert.Contains("2", ui.OrderLabelFor(s, 3));
    }

    [OriginalDataFact]
    public void P3_Amiga_MemberPick_Buttons_MoveRow_AndFight()
    {
        var s = P3Fight();
        var ui = new CombatPresenter(new ClientSettings()) { AmigaOrders = () => true };
        ui.Start(s);
        var pad = new CombatPad();
        Press(pad, ui, s, Buttons.A); // FIGHT at the DOS options
        Assert.True(ui.AmigaOrdersShown);
        var step = ui.PadStep(s)!;
        Assert.Equal(Keys.F, step.AKey);
        Assert.Equal(UiText.S("p3combat.list.F"), step.Picks[^1].Label);
        Press(pad, ui, s, Buttons.DPadDown, Buttons.A); // HERO0
        Assert.Equal(0, ui.OrderMember);
        var labels = ui.PadStep(s)!.Picks.Select(p => p.Label).ToList();
        Assert.Contains(UiText.S("p3combat.list.3"), labels);
        Press(pad, ui, s, PickThenA(ui.PadStep(s)!, p => p.Label == UiText.S("p3combat.list.3"))); // Move Front
        Assert.Equal(-1, ui.OrderMember);
        Press(pad, ui, s, Buttons.DPadDown, Buttons.A);
        Assert.Equal([Keys.A], Press(pad, ui, s, PickThenA(ui.PadStep(s)!, p => p.Label == UiText.S("p3combat.list.A")))); // Attack
        if (ui.AsksRankWindow)
            Press(pad, ui, s, Buttons.A); // OK = rank 1
        Assert.Equal(-1, ui.OrderMember);
        Assert.False(ui.OrderLabelFor(s, 0).StartsWith("PARRY", StringComparison.Ordinal));
        Press(pad, ui, s, Buttons.A); // Fight!
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
    }

    [OriginalDataFact]
    public void P3_Amiga_SpellWindow_UpDownArrows_RightIsMore()
    {
        var s = P3Fight();
        var wizard = s.Party.Members[2];
        for (var i = 0; i < wizard.SpellsKnown.Length; i++)
            wizard.SpellsKnown[i] = 1;
        wizard.MagicCur = 50;
        var ui = new CombatPresenter(new ClientSettings()) { AmigaOrders = () => true };
        ui.Start(s);
        var pad = new CombatPad();
        Press(pad, ui, s, Buttons.A);
        Press(pad, ui, s, PickThenA(ui.PadStep(s)!, p => p.Label == wizard.Name));
        Press(pad, ui, s, PickThenA(ui.PadStep(s)!, p => p.Label == UiText.S("p3combat.list.C")));
        if (!ui.AsksAmigaSpell)
            return; // refused before the list (no magic): nothing to page
        var first = ui.AmigaSpellPage(s)[0];
        Assert.Equal([Keys.Down], Press(pad, ui, s, Buttons.DPadDown));
        Assert.Equal(1, ui.AmigaListCursor);
        Assert.Equal([Keys.M], Press(pad, ui, s, Buttons.DPadRight));
        if (ui.AmigaSpellList(s).Count > CombatPresenter.AmigaSpellRows)
            Assert.NotEqual(first, ui.AmigaSpellPage(s)[0]);
    }

    [OriginalDataFact]
    public void P1_Amiga_SpellList_LeftRightPick_UpDownPage_Exit()
    {
        var s = ClientSessions.Fight();
        var layout = P1Layout() with
        {
            SpellList = P1Layout().SpellList! with { Spells = P1Layout().SpellList!.Spells with { Tone = null } },
        };
        var ui = new CombatPresenter(new ClientSettings()) { P1Layout = layout, AmigaP1Field = () => true };
        ui.Start(s);
        var priest = s.Party.Members[1];
        Array.Clear(priest.SpellsKnown);
        foreach (var id in new[] { 4, 5, 6, 7, 8, 9, 10, 11 })
            priest.SpellsKnown[id] = 1;
        priest.MagicCur = 20;
        var pad = new CombatPad();
        Press(pad, ui, s, Buttons.A); // Party Options down, on Fight
        Press(pad, ui, s, Buttons.A); // Fight
        Assert.True(ui.P1OrdersShown);
        Press(pad, ui, s, PickThenA(ui.PadStep(s)!, p => p.Label == priest.Name));
        Assert.True(ui.P1AsksOrder);
        Press(pad, ui, s, PickThenA(ui.PadStep(s)!, p => p.Label == CombatText.ChoiceLabel(CombatChoice.Cast)));
        Assert.True(ui.P1AsksSpell);
        var step = ui.PadStep(s)!;
        Assert.True(step.PickOnLeftRight);
        Assert.Equal(UiText.S("p1field.exit"), step.Picks[^1].Label);
        Assert.Equal([Keys.Down], Press(pad, ui, s, Buttons.DPadDown)); // the next page
        Assert.True(ui.P1SpellCanScrollUp);
        Assert.Equal([Keys.Up], Press(pad, ui, s, Buttons.DPadUp));
        Assert.Equal([Keys.E], Press(pad, ui, s, Buttons.DPadLeft, Buttons.A)); // wraps to EXIT: PARRY
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.StartsWith("PARRY", ui.OrderLabelFor(s, 1));
    }

    [Fact]
    public void GameButtons_SendNothing_AndNoStepGetsEscOrPaging()
    {
        var pad = new CombatPad();
        var step = new CombatPadStep("x", [new("ONE", [Keys.D1])]);
        Assert.Empty(pad.KeysFor(step, [Buttons.B, Buttons.X, Buttons.Y, Buttons.LeftShoulder, Buttons.RightShoulder,
            Buttons.Back, Buttons.Start, Buttons.LeftTrigger, Buttons.RightTrigger]));
        // s190 (pad fix list C12): only the right stick pages, the log and the rank box (LB / RB stay Save / Load).
        Assert.Equal([Keys.PageUp], pad.KeysFor(step, [Buttons.RightThumbstickUp]));
        Assert.Equal([Keys.Enter], pad.KeysFor(step, [Buttons.A]));
        Assert.Equal([Keys.D1], pad.KeysFor(step, [Buttons.DPadLeft, Buttons.A]));
    }

    [Fact]
    public void Mark_IsTheTopmostRegionClickingThePicksKeys()
    {
        var hits = new HitMap();
        hits.Add(new HitRegion(new Microsoft.Xna.Framework.Rectangle(0, 0, 10, 8)) { Click = [Keys.D2] });
        hits.Add(new HitRegion(new Microsoft.Xna.Framework.Rectangle(0, 20, 10, 8)) { Click = [Keys.D1, Keys.Enter] });
        hits.Add(new HitRegion(new Microsoft.Xna.Framework.Rectangle(0, 40, 10, 8)) { Click = [Keys.D2] });
        var pad = new CombatPad();
        var step = new CombatPadStep("s", [new("ONE", [Keys.D1, Keys.Enter]), new("TWO", [Keys.D2]), new("SIX", [Keys.D6])]);
        Assert.Null(CombatPad.MarkFor(pad.Picked(step), hits)); // nothing picked: no mark
        pad.KeysFor(step, [Buttons.DPadDown]);
        Assert.Equal(20, CombatPad.MarkFor(pad.Picked(step), hits)!.Value.Y);
        pad.KeysFor(step, [Buttons.DPadDown]);
        Assert.Equal(40, CombatPad.MarkFor(pad.Picked(step), hits)!.Value.Y);
        pad.KeysFor(step, [Buttons.DPadDown]);
        Assert.Null(CombatPad.MarkFor(pad.Picked(step), hits)); // not drawn: the help line only
        Assert.Null(pad.Picked(step with { Id = "t" }));
    }

    [Fact]
    public void ANewStep_StartsThePickAgain()
    {
        var pad = new CombatPad();
        var one = new CombatPadStep("a", [new("ONE", [Keys.D1]), new("TWO", [Keys.D2])]);
        pad.KeysFor(one, [Buttons.DPadDown, Buttons.DPadDown]);
        Assert.Equal(2, pad.Pick);
        Assert.Equal([Keys.Enter], pad.KeysFor(one with { Id = "b" }, [Buttons.A]));
        pad.KeysFor(one, [Buttons.DPadDown]);
        pad.KeysFor(null, []);
        Assert.Equal(0, pad.Pick);
    }
}
