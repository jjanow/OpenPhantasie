namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie I's combat orders as a feature module (<see cref="FeatureModuleRegistry.P1CombatOrders"/>). It hands every
/// order to the existing DOS-checked code unchanged: packing is <see cref="CombatOrders.Choose"/>, melee is
/// <see cref="CombatResolver.ResolvePartyMelee"/>, casts are <see cref="SpellCaster.ResolveCombatCast"/>. No extra RNG
/// calls, no extra text.
/// </summary>
public sealed class P1CombatOrdersModule : ICombatActionModule
{
    private static readonly CombatChoice[] MenuOrder =
    [
        CombatChoice.Thrust,
        CombatChoice.Attack,
        CombatChoice.Slash,
        CombatChoice.Lunge,
        CombatChoice.Cast,
        CombatChoice.Parry,
    ];

    public FeatureModuleDescriptor Descriptor => FeatureModuleRegistry.P1CombatOrders;

    public IReadOnlyList<CombatChoice> Choices => MenuOrder;

    public bool TryChoose(in CombatOrderRequest request, IRandomSource rng, out int packedAction)
    {
        if (Array.IndexOf(MenuOrder, request.Choice) < 0)
        {
            packedAction = CombatAction.Cleared;
            return false;
        }
        packedAction = CombatOrders.Choose(
            request.Member, request.Prep, request.Choice, rng,
            request.RankIndex, request.SpellIndex, request.HealTargetIndex, request.Rules);
        return true;
    }

    public bool TryResolve(
        CombatEncounter encounter, int memberIndex, int packedAction, IRandomSource rng, List<string> log)
    {
        // Only packed values that decode to a spell: SpellCaster does nothing with the rest, so leaving them unclaimed keeps
        // I's behaviour and frees them for other modules.
        if (CombatAction.TryDecodeCast(packedAction, out _, out _))
        {
            SpellCaster.ResolveCombatCast(encounter, memberIndex, packedAction, rng, log);
            return true;
        }
        if (CombatAction.IsMelee(packedAction))
        {
            CombatResolver.ResolvePartyMelee(encounter, memberIndex, packedAction, rng, log);
            return true;
        }
        return false;
    }
}
