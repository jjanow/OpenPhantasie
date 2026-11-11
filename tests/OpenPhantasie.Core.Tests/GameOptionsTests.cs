using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>T5.0 (<c>09-startup-options.md</c>): rules options, the four presets and locking them per save.</summary>
public class GameOptionsTests
{
    // Two test options: A differs between manual and DOS, B between manual and Remaster.
    private static readonly RuleOptionDescriptor A = new("test.a", GameId.P1, ["a0", "a1"], Manual: "a0", Dos: "a1", Remaster: "a0");
    private static readonly RuleOptionDescriptor B = new("test.b", GameId.P1, ["b0", "b1"], Manual: "b0", Dos: "b0", Remaster: "b1");
    private static readonly IReadOnlyList<RuleOptionDescriptor> TestOptions = [A, B];

    private static GameSession NewSession() =>
        GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(5));

    [Fact]
    public void Presets_give_each_option_its_value()
    {
        Assert.Equal(new[] { "a0", "b0" }, Values(GameOptions.ForPreset(GameId.P1, OptionPreset.Manual, TestOptions)));
        Assert.Equal(new[] { "a1", "b0" }, Values(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos, TestOptions)));
        Assert.Equal(new[] { "a0", "b1" }, Values(GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster, TestOptions)));
        Assert.Equal(new[] { "a0", "b0" }, Values(GameOptions.ForPreset(GameId.P1, OptionPreset.Custom, TestOptions)));
    }

    [Fact]
    public void Changing_an_option_moves_to_the_matching_preset_or_custom()
    {
        var manual = GameOptions.ForPreset(GameId.P1, OptionPreset.Manual, TestOptions);
        Assert.Equal(OptionPreset.Dos, manual.With("test.a", "a1").Preset);
        Assert.Equal(OptionPreset.Remaster, manual.With("test.b", "b1").Preset);
        Assert.Equal(OptionPreset.Custom, manual.With("test.a", "a1").With("test.b", "b1").Preset);
        Assert.Equal(OptionPreset.Manual, manual.With("test.a", "a1").With("test.a", "a0").Preset);
    }

    [Fact]
    public void A_preset_is_kept_while_its_values_still_match()
    {
        // Every value the same as both manual and DOS: picking DOS stays DOS.
        var same = new RuleOptionDescriptor("test.same", GameId.P1, ["x", "y"], "x", "x", "y");
        var dos = GameOptions.ForPreset(GameId.P1, OptionPreset.Dos, [same]);
        Assert.Equal(OptionPreset.Dos, dos.With("test.same", "x").Preset);
        Assert.True(dos.Matches(OptionPreset.Manual));
    }

    [Fact]
    public void Picking_custom_keeps_the_values()
    {
        var custom = GameOptions.ForPreset(GameId.P1, OptionPreset.Dos, TestOptions).WithPreset(OptionPreset.Custom);
        Assert.Equal(OptionPreset.Custom, custom.Preset);
        Assert.Equal(new[] { "a1", "b0" }, Values(custom));
        Assert.Equal(new[] { "a0", "b1" }, Values(custom.WithPreset(OptionPreset.Remaster)));
    }

    [Fact]
    public void Bad_values_and_placeholder_modules_are_refused()
    {
        var o = GameOptions.ForPreset(GameId.P1, OptionPreset.Manual, TestOptions);
        Assert.Throws<ArgumentException>(() => o.With("test.a", "zz"));
        Assert.Throws<ArgumentException>(() => o.With("test.none", "a0"));
        Assert.Throws<NotSupportedException>(() => o.WithModule(FeatureModuleRegistry.Aim.Id, true));
    }

    [Fact]
    public void Native_combat_cannot_be_switched_off()
    {
        Assert.Throws<NotSupportedException>(() => GameOptions.ForPreset(GameId.P1)
            .WithModule(FeatureModuleRegistry.P1CombatOrders.Id, false));
        Assert.Throws<NotSupportedException>(() => GameOptions.ForPreset(GameId.P3)
            .WithModule(FeatureModuleRegistry.P3CombatActions.Id, false));
    }

    [Theory]
    [InlineData(GameId.P1, "p1.combat-orders")]
    [InlineData(GameId.P2, "p1.combat-orders")]
    [InlineData(GameId.P3, "p3.combat-actions")]
    public void Empty_saved_modules_retain_native_combat(GameId game, string native)
    {
        var rules = GameOptions.Restore(game, "custom", null, []);
        Assert.Contains(native, rules.Modules);
        Assert.DoesNotContain("p2.rock-throwing", rules.Modules); // optional, including in II
        Assert.NotEmpty(rules.CreateModules().CombatChoices);
    }

    [Fact]
    public void Restore_is_lenient()
    {
        var r = GameOptions.Restore(GameId.P1, "nonsense",
            new Dictionary<string, string> { ["test.a"] = "a1", ["test.b"] = "zz", ["test.gone"] = "q" },
            ["p1.combat-orders", "p3.aim", "no.such"], TestOptions);
        Assert.Equal(new[] { "a1", "b0" }, Values(r));
        Assert.Equal(OptionPreset.Dos, r.Preset); // "manual" no longer fits a1 → re-matched
        Assert.Equal(new[] { "p1.combat-orders" }, r.Modules);

        var custom = GameOptions.Restore(GameId.P1, "custom", new Dictionary<string, string>(), null, TestOptions);
        Assert.Equal(OptionPreset.Custom, custom.Preset); // a stored Custom stays Custom
        Assert.Equal(new[] { "p1.combat-orders" }, custom.Modules); // no module list → the game's own
    }

    [Fact]
    public void Phantasie_I_ships_elemental_prep_from_D032()
    {
        Assert.Equal(["p1.elemental-prep", "p1.stash-access", "p1.armory-restock", "p1.gold-cap", "p1.rename-party",
            "p1.stunning-blow", "p1.armor-reduced", "p1.payout-remainder",
            "p1.attack-gate", "p1.lunge", "p1.binding", "p1.confusion-iv", "p1.fear", "p1.dispell-undead", "p1.spell-level",
            "p1.fireflash-damage", "p1.fireflash-target", "p1.failed-run", "p1.town-inn-time", "p1.hit-dice",
            "p1.wild-inn-time", "p1.inn-magic-cap", "p1.wild-inn-dead", "p1.pool-cap", "p1.path-time", "p1.monster-eval",
            "p1.vision", "p1.add-aging", "p1.cast-flag", "p1.dungeon-map-no",
            "p1.halfling-dex", "p1.guild-size", "p1.lifespans", "p1.monk-sleep",
            "p1.town-healing", "p1.beg-mercy", "p1.sack-size", "p1.sleep", "p1.charm", "p1.teleport", "p1.wild-treasure",
            "p1.summon-party", "p1.monster-spell-target", "p1.combat-shares", "p1.continue",
            "p1.cast-list", "p1.transport-list", "p1.combat-spell-list", "p1.orders-reset",
            "elemental-load", "elemental-drop", "elemental-purge", "shared-aging", "modern-inventory", "treasury", "guild-limit"],
            RuleOptionRegistry.For(GameId.P1).Select(d => d.Id));
        Assert.Equal(ElementalPrepRule.Prepared, GameOptions.ForPreset(GameId.P1).ElementalPrep);
        Assert.Equal(ElementalPrepRule.Dos, GameOptions.ForPreset(GameId.P1, OptionPreset.Dos).ElementalPrep);
        Assert.Equal(ElementalPrepRule.Prepared, GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster).ElementalPrep);
    }

    [OriginalDataFact]
    public void A_new_game_locks_its_rules_into_the_save()
    {
        var session = NewSession();
        Assert.Equal(OptionPreset.Manual, session.Options.Preset);
        session.StartNewGame(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
        Assert.Equal(ElementalPrepRule.Dos, session.ElementalPrep);
        Assert.True(session.FeatureModules.IsEnabled("p1.combat-orders"));

        var json = SessionCodec.WriteJson(session.CaptureSnapshot());
        var other = NewSession();
        other.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal(OptionPreset.Dos, other.Options.Preset);
        Assert.Equal(ElementalPrepRule.Dos, other.ElementalPrep);
    }

    [OriginalDataFact]
    public void A_save_without_options_plays_original_manual()
    {
        var session = NewSession();
        session.StartNewGame(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
        var snap = session.CaptureSnapshot();
        snap.Options = null;
        var json = SessionCodec.WriteJson(snap);
        Assert.DoesNotContain("\"options\"", json, StringComparison.OrdinalIgnoreCase);

        session.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal(OptionPreset.Manual, session.Options.Preset);
        Assert.Equal(ElementalPrepRule.Prepared, session.ElementalPrep);
    }

    [Fact]
    public void Parked_parties_never_carry_options_and_continue_keeps_the_files()
    {
        var file = new SessionSnapshot
        {
            RosterSlots = new Character?[22],
            Options = GameOptionsSnapshot.From(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos)),
        };
        Assert.Null(SeveralParties.Park(file).Options);

        file.RosterSlots[0] = new Character { Name = "ANNA" };
        var next = SeveralParties.Select(file, 1, 22, out _, out var error);
        Assert.Equal("", error);
        Assert.Equal("dos", next!.Options!.Preset);
    }

    private static string[] Values(GameOptions o) => o.Options.Select(d => o.Values[d.Id]).ToArray();
}
