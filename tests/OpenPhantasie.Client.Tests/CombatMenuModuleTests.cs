using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>Phase 09 T1.6 rest: the combat order menu is built from the fight's <see cref="FeatureModuleSet.CombatChoices"/>.</summary>
public class CombatMenuModuleTests
{
    /// <summary>A placeholder module that adds one order (a value outside I's six) and packs it as 777.</summary>
    private sealed class ExtraOrderModule : ICombatActionModule
    {
        public const CombatChoice Extra = (CombatChoice)99; // no label: "ORDER 99"
        public const int Packed = 777;

        public FeatureModuleDescriptor Descriptor { get; } =
            new("test.extra-order", GameId.P1, Implemented: true, "test", "test");
        public IReadOnlyList<CombatChoice> Choices { get; } = [Extra];
        public int Chosen { get; private set; }

        public bool TryChoose(in CombatOrderRequest request, IRandomSource rng, out int packedAction)
        {
            packedAction = Packed;
            if (request.Choice != Extra) return false;
            Chosen++;
            return true;
        }

        public bool TryResolve(CombatEncounter encounter, int memberIndex, int packedAction, IRandomSource rng,
            List<string> log) => packedAction == Packed;
    }

    private static (GameSession, CombatPresenter) AtOrders(Action<GameSession>? configure = null)
    {
        var session = ClientSessions.Fight();
        configure?.Invoke(session);
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(session);
        while (ui.OptionCursor != CombatText.DefaultPartyOptionIndex)
            ui.HandleKey(session, Keys.Down);
        ui.HandleKey(session, Keys.Enter); // FIGHT
        Assert.True(ui.AwaitingOrders);
        return (session, ui);
    }

    [OriginalDataFact]
    public void PhantasieI_Menu_IsTodaysSixOrders()
    {
        var (s, ui) = AtOrders();
        Assert.Equal(FeatureModuleSet.PhantasieI.CombatChoices, CombatPresenter.MenuChoices(s));
        var name = s.Party.Members[ui.OrderMember].Name;
        Assert.Equal(
            $"{name} ({ui.OrderLabelFor(s, ui.OrderMember)}): 1 THRUST 2 ATTACK 3 SLASH 4 LUNGE 5 CAST 6 PARRY  Enter keep",
            ui.LastLine);
    }

    [OriginalDataFact]
    public void ModuleOrder_AppearsInTheMenu_AndItsDigitChoosesIt()
    {
        var extra = new ExtraOrderModule();
        var (s, ui) = AtOrders(session =>
            session.Combat!.Modules = FeatureModuleSet.Create(GameId.P1, new P1CombatOrdersModule(), extra));

        Assert.Equal(7, CombatPresenter.MenuChoices(s).Count);
        Assert.EndsWith("6 PARRY 7 ORDER 99  Enter keep", ui.LastLine);
        Assert.Contains(PanelMouse.Hotkeys(ui.LastLine, commandList: true), h => h.Key == Keys.D7);

        var member = ui.OrderMember;
        ui.HandleKey(s, Keys.D7);
        Assert.Equal(1, extra.Chosen);
        Assert.Equal(ExtraOrderModule.Packed, s.Combat!.Prep[member].PackedAction);
    }

    [OriginalDataFact]
    public void TossRock_InI_AsksForARank_AndRepeatsClampedToTheLivingRanks()
    {
        var (s, ui) = AtOrders(session =>
            session.Combat!.Modules = FeatureModuleSet.ForGame(GameId.P1, enable: ["p2.rock-throwing"]));
        Assert.EndsWith("6 PARRY 7 TOSS  Enter keep", ui.LastLine);
        var member = ui.OrderMember;
        ui.HandleKey(s, Keys.D7); // TOSS asks "WHICH RANK?" for any class (II pick_rank 0x0f14c)
        Assert.Equal(member, ui.OrderMember);
        ui.HandleKey(s, Keys.D1);
        Assert.Equal(CombatAction.Toss(0), s.Combat!.Prep[member].PackedAction);

        // A repeated toss keeps TOSS and clamps its rank to the living ranks (live s78: rank 3 → 2 → 1).
        Assert.Equal(new RepeatOrder(CombatChoice.Toss, 1, -1, 0),
            CombatText.RepeatFor(CombatAction.Toss(2), CharacterClass.Wizard, livingRanks: 2));
        Assert.Equal("TOSS AT RANK 2", CombatText.OrderLabel(new RepeatOrder(CombatChoice.Toss, 1, -1, 0)));
    }

    [OriginalDataFact]
    public void DigitPastTheMenu_IsIgnored()
    {
        var (s, ui) = AtOrders();
        var member = ui.OrderMember;
        ui.HandleKey(s, Keys.D7); // I has six orders
        Assert.Equal(member, ui.OrderMember);
    }
}
