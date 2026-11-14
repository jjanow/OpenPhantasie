using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>III Amiga's combat orders screen (s118, FORMAT.md §8.3; <c>0x188e4</c> / <c>0x18c48</c>).</summary>
public partial class AmigaOrdersScreenTests
{
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    [OriginalDataFact]
    public void EmptyStoredModules_CanGiveTheFirstFightOrder()
    {
        var (session, ui) = AtOrders(amiga: false, GameOptions.Restore(GameId.P3, "custom", null, []));
        Assert.True(ui.AwaitingOrders);
        ui.HandleKey(session, Keys.T);
        Assert.NotNull(session.Combat);
    }

    /// <summary>A III party of four (Fighter, Priest, Wizard, Thief) in a normal fight, the presenter at the orders.</summary>
    private static (GameSession, CombatPresenter) AtOrders(bool amiga = true, GameOptions? options = null)
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(7));
        s.StartNewGame(options);
        s.OpenGuild();
        var classes = new[] { CharacterClass.Fighter, CharacterClass.Priest, CharacterClass.Wizard, CharacterClass.Thief };
        for (var i = 0; i < classes.Length; i++)
        {
            var c = s.CreateAndEnroll("HERO" + i, Race.Human, classes[i]);
            Assert.True(s.TryAddToParty(c, out var err), err);
        }
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0, SurpriseFlags.Normal);
        var ui = new CombatPresenter(new ClientSettings()) { AmigaOrders = () => amiga };
        ui.Start(s);
        while (ui.OptionCursor != CombatText.DefaultPartyOptionIndex)
            ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.Enter); // FIGHT
        Assert.Equal(CombatPhase.Orders, ui.Phase);
        return (s, ui);
    }

    /// <summary>The same party at the round's top (PARTY OPTIONS) with the Amiga field on.</summary>
    private static (GameSession, CombatPresenter) AtFieldMenu()
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(7));
        s.StartNewGame(null);
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("HERO0", CharacterClass.Fighter), ("HERO1", CharacterClass.Priest) })
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(name, Race.Human, cls), out var err), err);
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0, SurpriseFlags.Normal);
        var ui = new CombatPresenter(new ClientSettings()) { AmigaField = () => true, AmigaOrders = () => true };
        ui.Start(s);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        return (s, ui);
    }

    [OriginalDataFact]
    public void FieldMenu_IsUp_AtTheRoundTop_Closed()
    {
        var (_, ui) = AtFieldMenu();
        Assert.True(ui.AmigaFieldMenuShown);
        Assert.Null(ui.FieldMenuOpen);
    }

    [OriginalDataFact]
    public void FieldMenu_F_GoesToTheOrdersScreen()
    {
        var (s, ui) = AtFieldMenu();
        ui.HandleKey(s, Keys.F);
        Assert.Equal(CombatPhase.Orders, ui.Phase);
        Assert.True(ui.AmigaOrdersShown);
        Assert.False(ui.AmigaFieldMenuShown);
    }

    [OriginalDataFact]
    public void FieldMenu_Arrows_PullOptionsDown_ReturnTakesTheItem()
    {
        var (s, ui) = AtFieldMenu();
        ui.HandleKey(s, Keys.Down);
        Assert.Equal(CombatPresenter.AmigaOptionsMenu, ui.FieldMenuOpen);
        Assert.Equal(0, ui.FieldMenuItem); // F) Fight
        ui.HandleKey(s, Keys.Up);
        Assert.Equal(6, ui.FieldMenuItem); // wraps to H) Party health
        ui.HandleKey(s, Keys.Escape);
        Assert.Null(ui.FieldMenuOpen);
        ui.HandleKey(s, Keys.Enter);
        ui.HandleKey(s, Keys.Enter); // F) Fight
        Assert.Equal(CombatPhase.Orders, ui.Phase);
    }

    [OriginalDataFact]
    public void FieldMenu_H_ShowsHealth_AndComesBack()
    {
        var (s, ui) = AtFieldMenu();
        ui.HandleKey(s, Keys.H);
        Assert.NotEmpty(ui.InfoBox);
        ui.HandleKey(s, Keys.Space);
        Assert.Empty(ui.InfoBox);
        Assert.True(ui.AmigaFieldMenuShown);
    }

    [OriginalDataFact]
    public void FieldMenu_Speed_SetsAndReports()
    {
        var (s, ui) = AtFieldMenu();
        AmigaCombatSpeed? told = null;
        ui.FieldSpeedChosen = sp => told = sp;
        ui.HandleKey(s, Keys.OemMinus);
        Assert.Equal(AmigaCombatSpeed.Slow, ui.FieldSpeed);
        Assert.Equal(AmigaCombatSpeed.Slow, told);
        ui.HandleKey(s, Keys.Add);
        Assert.Equal(AmigaCombatSpeed.Fast, ui.FieldSpeed);
        ui.OpenFieldMenu(2);
        ui.HoverFieldMenuItem(1);
        ui.HandleKey(s, Keys.Enter); // =) Normal
        Assert.Equal(AmigaCombatSpeed.Normal, ui.FieldSpeed);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
    }

    private static IReadOnlyList<string> FieldTitles() => CombatPresenter.AmigaFieldMenus.Select(m => UiText.S(m.TitleId)).ToList();

    private static Rectangle FieldBox(int menu) => UiOverlays.FieldMenuBox(UiOverlays.FieldMenuColumn(FieldTitles(), menu),
        CombatPresenter.AmigaFieldMenus[menu].Items.Select(i => UiText.S(i.LabelId)).ToList());

    [OriginalDataFact]
    public void FieldMenu_Places_AreTheCodes0x12d6() // s120: columns 0 / 6 / 15, titles at (column + 1) · 8
    {
        var titles = FieldTitles();
        Assert.Equal([0, 6, 15], Enumerable.Range(0, 3).Select(m => UiOverlays.FieldMenuColumn(titles, m)));
        Assert.Equal([8, 56, 128], Enumerable.Range(0, 3).Select(m => UiOverlays.FieldMenuTitleX(UiOverlays.FieldMenuColumn(titles, m))));
        Assert.Equal(new Rectangle(0, 1, 48, 10), UiOverlays.FieldMenuTitleSpan(0, "File")); // 0x2b1c(column, 1, length + 2, 10)
        Assert.Equal(new Rectangle(48, 1, 72, 10), UiOverlays.FieldMenuTitleSpan(6, "Options"));
        Assert.Equal(new Rectangle(0, 12, 80, 16), FieldBox(0));    // "?) Info " 8 + 1 → 10 cells
        Assert.Equal(new Rectangle(48, 12, 144, 64), FieldBox(1));  // "`p3field.item.B` " 17 + 1 → 18
        Assert.Equal(new Rectangle(112, 12, 96, 32), FieldBox(2));  // column 15 → 14; "=)  Normal " 11 + 1 → 12
        Assert.Equal(new Rectangle(48, 32, 144, 8), UiOverlays.FieldMenuItemRow(FieldBox(1), 2)); // text row 2 + i
    }

    [Fact]
    public void FieldMenu_Box_IsTheTownsMeasure() // the town's list 0x238a4: Options (96, 12)–(223, 107)
    {
        var column = UiOverlays.FieldMenuColumn(["File", "Guild", "Options"], 2);
        Assert.Equal(112, UiOverlays.FieldMenuTitleX(column)); // measured s116: Guild 56, Options 112
        var box = UiOverlays.FieldMenuBox(column, ["A) Armoury", "B) Bank", "I) Inn", "M) Mystic", "X) Exit", "------------- ", "C) Cast Spell", "D) Distribute", "G) Trade Item", "U) Use Item", "V) View Party"]);
        Assert.Equal((96, 12, 128, 96), (box.X, box.Y, box.Width, box.Height));
    }

    [OriginalDataFact]
    public void FieldMenu_Speed_TicksWithAStar() // 0x870e: '*' over character 3
    {
        Assert.Equal("+) *Fast", UiOverlays.FieldSpeedLabel(UiText.S("p3field.item.fast"), true));
        Assert.Equal("=)  Normal ", UiOverlays.FieldSpeedLabel(UiText.S("p3field.item.normal"), false));
    }

    [OriginalDataTheory]
    [InlineData(Keys.M, AmigaFieldWindow.MonsterNames)]
    [InlineData(Keys.H, AmigaFieldWindow.PartyHealth)]
    public void FieldWindow_OpensOnTheField_AnyKeyClosesIt_AndIsUsedUp(Keys key, AmigaFieldWindow window)
    {
        var (s, ui) = AtFieldMenu();
        ui.AmigaFieldWindows = () => true;
        ui.HandleKey(s, key);
        Assert.Equal(window, ui.FieldWindow);
        Assert.Empty(ui.InfoBox);
        ui.HandleKey(s, Keys.F); // 0x1e8c4(1) takes the key: no fight
        Assert.Equal(AmigaFieldWindow.None, ui.FieldWindow);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        Assert.True(ui.AmigaFieldMenuShown);
    }

    [OriginalDataFact]
    public void FieldWindow_MonsterNames_ListsRank2First()
    {
        var (s, _) = AtFieldMenu();
        var rows = UiOverlays.MonsterNamesRows(s.Combat!).ToList();
        Assert.NotEmpty(rows);
        Assert.Equal(rows.OrderByDescending(r => r.Rank).ToList(), rows);
        foreach (var (rank, count, name) in rows)
        {
            var r = s.Combat!.Ranks.Single(x => x.RankIndex == rank - 1);
            Assert.Equal(r.Living.Count(), count);
            Assert.Equal(r.Living.First().Name, name);
        }
    }

    [OriginalDataFact]
    public void FieldWindow_PartyHealth_LeavesTheElementalOut()
    {
        var (s, _) = AtFieldMenu();
        Assert.Equal(s.Party.Members.Where(m => !ReferenceEquals(m, s.Party.Elemental)), UiOverlays.PartyHealthMembers(s.Party));
        Assert.Equal(10, UiText.S("p3field.health_labels").Split('\n').Length); // 0x23178: Name: … Rt Leg:
        Assert.Equal([0, 0, 1, 2, 0], new[] { 5, 9, 10, 999, 1000 }.Select(UiOverlays.AmigaRightShift));
    }

    [OriginalDataFact]
    public void FieldWindows_AreInThePack() // s120: 0x1a264(1) window 23 moved down; 0xadac window 24
    {
        var windows = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")!.KeepWindows!;
        var m = windows.MonsterNames!;
        Assert.Equal([16, 128, 300, 191], m.Box);
        Assert.Equal(("combat", 2, 7, 1), (m.Palette, m.Frame[0], m.Frame[1], m.Colours[0]));
        Assert.Equal((56, 144), m.Text[0]);                               // cell (7, 18)
        Assert.Equal([(56, 168), (96, 168), (104, 168), (120, 168), (136, 168)], m.Text.Skip(1)); // rank 1: row 21, columns 7 / 12 / 13 / 15 / 17
        var h = windows.PartyHealth!;
        Assert.Equal([24, 16, 286, 199], h.Box);
        Assert.Equal([1, 4, 15, 15, 14, 13, 12], h.Colours);
        Assert.Equal([(32, 24), (32, 112), (96, 24), (160, 24), (224, 24), (96, 112), (160, 112), (224, 112)], h.Text);
    }

    private static string Line(GameSession s, CombatPresenter ui, int i) =>
        UiOverlays.OrdersMemberLine(s.Party.Members[i], ui.PackedOrder(i), P3Data);

    [OriginalDataFact]
    public void Screen_ShowsEveryOrderSet_NobodyAsked_UntilAMemberIsPicked()
    {
        var (s, ui) = AtOrders();
        Assert.True(ui.AmigaOrdersShown);
        Assert.True(ui.AwaitingOrders);
        Assert.Equal(-1, ui.OrderMember);
        ui.HandleKey(s, Keys.D1);
        Assert.Equal(0, ui.OrderMember);
        ui.HandleKey(s, Keys.T);
        Assert.Equal(-1, ui.OrderMember); // back to the boxes (0x18c20)
        Assert.Equal("Thrusts (R1)", Line(s, ui, 0));
        ui.HandleKey(s, Keys.F);
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
        Assert.False(ui.AmigaOrdersShown);
    }

    [OriginalDataFact]
    public void OptionLetters_AreTheAmigasTable() // Core's rules still apply (a level-1 Fighter's Attack / Slash are Thrusts)
    {
        var (s, ui) = AtOrders();
        foreach (var (key, label) in new[] { (Keys.P, "Parries"), (Keys.T, "Thrusts (R1)"), (Keys.P, "Parries") })
        {
            ui.HandleKey(s, Keys.D1);
            ui.HandleKey(s, key);
            Assert.Equal(label, Line(s, ui, 0));
        }
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.Q); // a letter not in the table: Thrust, the Amiga's default (0x18afa)
        Assert.Equal("Thrusts (R1)", Line(s, ui, 0));
    }

    [OriginalDataFact]
    public void Move_ChangesTheRowAtOnce_AndKeepsTheOrder()
    {
        var (s, ui) = AtOrders();
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.P);
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.D1); // '1' = Move Back (0x18ae2 → 8)
        Assert.Equal(P3PartyRank.Back, s.Party.Members[0].Phantasie3!.Rank);
        Assert.Equal(-1, ui.OrderMember);
        Assert.Equal("Parries", Line(s, ui, 0));
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.D3); // '3' = Move Front
        Assert.Equal(P3PartyRank.Front, s.Party.Members[0].Phantasie3!.Rank);
    }

    [OriginalDataFact]
    public void ReturnOrBackspace_LeaveThePickWithNoChange()
    {
        var (s, ui) = AtOrders();
        var before = ui.PackedOrder(1);
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.Enter);
        Assert.Equal(-1, ui.OrderMember);
        Assert.Equal(before, ui.PackedOrder(1));
        Assert.Equal(CombatPhase.Orders, ui.Phase);
    }

    [OriginalDataFact]
    public void Cast_AsksTheSpell_ThenComesBackToTheBoxes()
    {
        var (s, ui) = AtOrders();
        ui.HandleKey(s, Keys.D3); // the Wizard
        ui.HandleKey(s, Keys.C);
        Assert.NotEmpty(ui.SpellChoices(s));
        ui.HandleKey(s, Keys.Back);
        Assert.Equal(-1, ui.OrderMember);
        Assert.Empty(ui.SpellChoices(s));
    }

    /// <summary>The Priest (member 2) knowing <paramref name="spells"/> (Core indices) only, with MP to spare.</summary>
    private static void Teach(GameSession s, params int[] spells)
    {
        var priest = s.Party.Members[1];
        Array.Clear(priest.SpellsKnown);
        foreach (var id in spells)
            priest.SpellsKnown[id] = 1;
        priest.MagicMax = priest.MagicCur = 50;
    }

    [OriginalDataFact]
    public void SpellWindow_ListsKnownSpells_HighestFirst_CursorWraps_OkTakesIt()
    {
        var (s, ui) = AtOrders();
        Teach(s, 0, 4, 5);
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.C);
        Assert.True(ui.AsksAmigaSpell);
        Assert.Equal([5, 4, 0], ui.AmigaSpellPage(s)); // 0xd08c sorts highest first
        Assert.Equal(0, ui.AmigaListCursor);
        ui.HandleKey(s, Keys.Up); // wraps to the bottom row
        Assert.Equal(2, ui.AmigaListCursor);
        ui.HandleKey(s, Keys.D2); // '2' = down, wraps to the top
        Assert.Equal(0, ui.AmigaListCursor);
        ui.AmigaPickRow(s, 1); // a press on a row only moves the cursor (0x17a6)
        Assert.True(ui.AsksAmigaSpell);
        ui.HandleKey(s, Keys.O);
        Assert.Equal(-1, ui.OrderMember);
        Assert.Equal(P3Data.GetSpell(4).Name, Line(s, ui, 1));
    }

    [OriginalDataFact]
    public void SpellWindow_Cancel_SetsThrust_EscKeepsTheOrder()
    {
        var (s, ui) = AtOrders();
        Teach(s, 4);
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.P);
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.Escape);
        Assert.Equal(-1, ui.OrderMember);
        Assert.Equal("Parries", Line(s, ui, 1));
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.C); // CANCEL (0x19656)
        Assert.Equal(-1, ui.OrderMember);
        Assert.Equal("Thrusts (R1)", Line(s, ui, 1));
    }

    [OriginalDataFact]
    public void SpellWindow_Manual_ListsOnlyCombatAndEverywhereSpells_D063()
    {
        var (s, ui) = AtOrders();
        Teach(s, 53, 4);
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.C);
        Assert.Equal([4], ui.AmigaSpellPage(s)); // Transportation (N) is left off
    }

    [OriginalDataFact]
    public void SpellWindow_More_ShowsTheNext12_ThenTheFirstAgain()
    {
        var (s, ui) = AtOrders();
        Teach(s, [.. Enumerable.Range(0, 14)]);
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.C);
        Assert.Equal(12, ui.AmigaSpellPage(s).Count);
        Assert.Equal(13, ui.AmigaSpellPage(s)[0]);
        ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.M);
        Assert.Equal([1, 0], ui.AmigaSpellPage(s));
        Assert.Equal(0, ui.AmigaListCursor);
        ui.HandleKey(s, Keys.M);
        Assert.Equal(13, ui.AmigaSpellPage(s)[0]);
    }

    [OriginalDataFact]
    public void Healing_OpensTheHealWindow_TheCursorPicksTheTarget()
    {
        var (s, ui) = AtOrders();
        Teach(s, 0); // Healing I
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.Enter);
        Assert.True(ui.AsksAmigaHeal);
        ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.Enter);
        Assert.Equal(-1, ui.OrderMember);
        Assert.True(CombatAction.TryDecodeCast(ui.PackedOrder(1), out var spell, out var target));
        Assert.Equal((0, 2), (spell, target));
    }

    [OriginalDataFact]
    public void Pack_HasTheSpellAndHealWindows()
    {
        var windows = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")!.KeepWindows!;
        Assert.Equal([56, 24, 252, 176], windows.Spell!.Box); // window 11 (0x23464)
        Assert.Equal((192, 151), windows.Spell.More);
        Assert.Equal([8, 44, 312, 160], windows.Heal!.Box); // window 6 (0x23360)
        Assert.Equal((16, 72), windows.Heal.Text[1]);
    }

    [OriginalDataFact]
    public void WithoutTheSwitch_TheRemastersListAsksEachMemberInTurn()
    {
        var (_, ui) = AtOrders(amiga: false);
        Assert.False(ui.AmigaOrdersShown);
        Assert.Equal(0, ui.OrderMember);
    }

    [Theory]
    [InlineData(P3Action.Attack, 1, "p3combat.order.0")]
    [InlineData(P3Action.Attack, 2, "p3combat.order.11")]
    [InlineData(P3Action.FireBow, 2, "p3combat.order.15")]
    [InlineData(P3Action.Thrust, 2, "p3combat.order.14")]
    [InlineData(P3Action.Aim, 1, "p3combat.order.6")]
    [InlineData(P3Action.Parry, 1, "p3combat.order.5")]
    public void OrderLabel_IsTheActionsIndex_Plus11ForRank2(P3Action action, int rank, string id) =>
        Assert.Equal(id, UiOverlays.OrderLabelId(P3CombatModule.Pack(new P3ActionChoice(action, rank)), out _));

    [Fact]
    public void OrderLabel_NoOrderYet_IsParries() =>
        Assert.Equal("p3combat.order.5", UiOverlays.OrderLabelId(CombatAction.Cleared, out _));

    [Fact]
    public void Layout_IsTheCodes()
    {
        var box = UiOverlays.OrdersMemberBox(1); // 0xac48(6, 22, 194, 33)
        Assert.Equal((6, 22, 194, 33), (box.X, box.Y, box.Right - 1, box.Bottom - 1));
        Assert.Equal((6, 166, 73, 177), (UiOverlays.OrdersFightBox.X, UiOverlays.OrdersFightBox.Y,
            UiOverlays.OrdersFightBox.Right - 1, UiOverlays.OrdersFightBox.Bottom - 1));
        Assert.Equal(3, UiOverlays.OrdersMemberRow(1));
        Assert.Equal((23, 24), (UiOverlays.OrdersGroupRow(1), UiOverlays.OrdersGroupRow(0)));
        Assert.Equal((25, 9), UiOverlays.OrdersRosterCell(1));
        Assert.Equal((33, 1), UiOverlays.OrdersRosterCell(3));
        Assert.Equal(new Point(216, 32), UiOverlays.OrdersFigureAt(0));
        Assert.Equal(new Point(280, 160), UiOverlays.OrdersFigureAt(5));
        Assert.Equal(12, UiOverlays.OrdersList.Count);
    }

    [OriginalDataFact]
    public void Pack_HasTheRankWindow()
    {
        var amiga = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga");
        var rank = amiga!.KeepWindows!.Rank!;
        Assert.Equal([24, 72, 296, 136], rank.Box);
        Assert.Equal((80, 111), rank.Ok);
        Assert.Equal((184, 111), rank.Cancel);
    }

    // ---- D3-075: Fireflash asks the rank under p3.fireflash = dos ---------------------------------------------------------

    private static GameOptions DosFireflash => GameOptions.ForPreset(GameId.P3).With("p3.fireflash", "dos");

    [OriginalDataTheory]
    [InlineData(Keys.Y, 0)]
    [InlineData(Keys.N, 1)]
    public void Fireflash_DosOption_OwnList_AsksTheRank_AndPacksIt(Keys answer, int rankIndex)
    {
        var (s, ui) = AtOrders(amiga: false, DosFireflash);
        Assert.Equal(2, s.Combat!.Ranks.Count(r => !r.IsWiped)); // this seed meets two groups
        Teach(s, SpellId.FireflashI + 1);
        ui.HandleKey(s, Keys.Enter); // the Fighter keeps his order
        ui.HandleKey(s, Keys.D8); // Cast Spell
        ui.HandleKey(s, Keys.D6); // #6 Fireflash II
        ui.HandleKey(s, Keys.Enter);
        Assert.True(ui.AsksDosRank(s)); // root 0xfc41: Fireflash picks a rank when rank 2 exists
        ui.HandleKey(s, answer);
        Assert.False(ui.AsksDosRank(s));
        Assert.Equal(CombatAction.CastSpellAtRank(SpellId.FireflashI + 1, rankIndex), s.Combat.Prep[1].PackedAction);
    }

    [OriginalDataFact]
    public void Fireflash_Manual_OwnList_AsksNoRank()
    {
        var (s, ui) = AtOrders(amiga: false);
        Teach(s, SpellId.FireflashI + 1);
        ui.HandleKey(s, Keys.Enter);
        ui.HandleKey(s, Keys.D8);
        ui.HandleKey(s, Keys.D6);
        ui.HandleKey(s, Keys.Enter);
        Assert.False(ui.AsksDosRank(s));
        Assert.Equal(CombatAction.CastSpell(SpellId.FireflashI + 1), s.Combat!.Prep[1].PackedAction);
    }

    [OriginalDataFact]
    public void Fireflash_DosOption_AmigaScreen_AsksInTheRankWindow()
    {
        var (s, ui) = AtOrders(options: DosFireflash);
        Teach(s, SpellId.FireflashI);
        ui.HandleKey(s, Keys.D2); // the Priest
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.O); // OK on Fireflash I
        Assert.True(ui.AsksRankWindow);
        ui.HandleKey(s, Keys.C); // CANCEL = rank 2 (0x19878)
        Assert.Equal(-1, ui.OrderMember);
        Assert.Equal(CombatAction.CastSpellAtRank(SpellId.FireflashI, 1), ui.PackedOrder(1));

        // Another spell asks no rank.
        Teach(s, SpellId.QuicknessI);
        ui.HandleKey(s, Keys.D2);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.O);
        Assert.False(ui.AsksRankWindow);
        Assert.Equal(CombatAction.CastSpell(SpellId.QuicknessI), ui.PackedOrder(1));
    }

    [Fact]
    public void RepeatFor_ARankTwoFireflash_KeepsItsRank_WhileTwoRanksLive()
    {
        var packed = CombatAction.CastSpellAtRank(SpellId.FireflashI, 1);
        var two = CombatText.RepeatFor(packed, CharacterClass.Wizard, livingRanks: 2);
        Assert.Equal((CombatChoice.Cast, 1, SpellId.FireflashI), (two.Choice, two.RankIndex, two.SpellIndex));
        Assert.Equal(0, CombatText.RepeatFor(packed, CharacterClass.Wizard, livingRanks: 1).RankIndex);
        Assert.Equal(0, CombatText.RepeatFor(CombatAction.CastSpell(SpellId.FireflashI), CharacterClass.Wizard, 2).RankIndex);
    }
}
