using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using Xunit;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// Pad fix B8 (s189): each wheel step's own line (<c>&lt;help id&gt;.wheel</c>, <see cref="DialogPad.WheelSuffix"/>) in
/// place of the generic wheel line, naming the pick and what X / Y / A / B do there; a new step starts the wheel again.
/// </summary>
public sealed class PadWheelHelpTests
{
    /// <summary>The help ids of every wheel step (<see cref="IPadWheel"/> dialogs and the typed names).</summary>
    public static readonly string[] WheelHelpIds =
    [
        "cast.1_9_choose_esc_cancel", "cast.1_9_choose_up_down_more", "potion.1_9_choose_esc_cancel",
        "timelag.0_fastest_9_slowest_esc_cancel", "guild.1_6_drop_esc_done", "guild.1_6_choose_esc_done", "p3train.help",
        "towndlg.1_9_learn_up_down_more", "lineage.kind_help", "lineage.race_help", "member.1_6_race_esc_cancel",
        "member.1_6_class_esc_back", "member.type_name_enter_accept_esc_cancel", "member.type_name_enter_accept_esc_back",
        "inspect.1_7_inspect_esc_done", "towndlg.type_amount_back_clear_digit_enter", "p3town.bank_window_amount_help",
        "towndlg.type_amount_deposit", "useitem.1_9_choose_esc_cancel",
    ];

    public static TheoryData<GameId> Games => new() { GameId.P1, GameId.P2, GameId.P3 };

    [Theory]
    [MemberData(nameof(Games))]
    public void EveryWheelStep_HasAWheelLine_WithOnePick_BesideItsPadTwin(GameId game)
    {
        var table = UiText.For(game);
        foreach (var id in WheelHelpIds)
        {
            Assert.True(table.Contains(id + PadWording.Suffix), id);
            var wheel = table.Get(id + DialogPad.WheelSuffix);
            Assert.Contains("{0}", wheel);
            Assert.DoesNotContain("{1}", wheel);
            Assert.Equal(wheel, DialogPad.WheelTextFor(table, table.Get(id)));
        }
        foreach (var (id, _) in table.Strings.Where(p => p.Key.EndsWith(DialogPad.WheelSuffix, StringComparison.Ordinal)))
            Assert.Contains(id[..^DialogPad.WheelSuffix.Length], WheelHelpIds);
    }

    [Fact]
    public void WheelLine_ShowsAtOnce_WithThePickAndItsName()
    {
        var help = UiText.S("cast.1_9_choose_esc_cancel");
        var pad = new DialogPad();
        var d = new object();
        pad.KeysFor(d, [], "123", stepHelp: help);
        Assert.Equal("Left/Right 1 ANN  X choose  B cancel",
            pad.Help(d, "123", false, false, help, pickName: p => p == 1 ? "ANN" : "BOB"));
        pad.KeysFor(d, [Buttons.DPadRight], "123", stepHelp: help);
        Assert.Equal("Left/Right 2 BOB  X choose  B cancel",
            pad.Help(d, "123", false, false, help, pickName: p => p == 1 ? "ANN" : "BOB"));
        Assert.Equal("Left/Right 2  X choose  B cancel", pad.Help(d, "123", false, false, help)); // no name: the number
    }

    [Fact]
    public void WheelLine_NamesTheSpace_AndShowsLetters()
    {
        var help = UiText.S("member.type_name_enter_accept_esc_cancel");
        var pad = new DialogPad();
        var d = new object();
        pad.KeysFor(d, [], NameEntry.Alphabet, stepHelp: help);
        Assert.StartsWith("Left/Right A  X type", pad.Help(d, NameEntry.Alphabet, false, false, help));
        pad.KeysFor(d, [Buttons.DPadLeft], NameEntry.Alphabet, stepHelp: help);
        Assert.StartsWith($"Left/Right {UiText.S("typing.space")}  X type", pad.Help(d, NameEntry.Alphabet, false, false, help));
    }

    [Fact]
    public void ANewStep_SharingTheHelpText_StartsTheWheelAgain()
    {
        var help = UiText.S("cast.1_9_choose_esc_cancel");
        var pad = new DialogPad();
        var d = new object();
        Assert.Equal([Keys.D2], pad.KeysFor(d, [Buttons.DPadRight, Buttons.X], "123", stepHelp: help, stepName: "Caster"));
        Assert.Equal([Keys.D1], pad.KeysFor(d, [Buttons.X], "123", stepHelp: help, stepName: "Target"));
        Assert.Equal([Keys.D2], pad.KeysFor(d, [Buttons.DPadRight, Buttons.X], "123", stepHelp: help, stepName: "Target"));
        Assert.Equal([Keys.D2], pad.KeysFor(d, [Buttons.X], "123", stepHelp: help, stepName: "Target")); // same step: kept
    }

    [OriginalDataFact]
    public void Cast_WheelLines_NameTheCaster_TheSpell_ThenTheHealTarget_FromOne()
    {
        var s = ClientSessions.Overworld(); // Fighto (1) + Priesto (2)
        var priest = s.Party.Members[1];
        priest.SpellsKnown[SpellId.HealingI] = 1;
        var d = new CastDialog();
        d.Open(s);
        var pad = new DialogPad();
        string? Help() => pad.Help(d, d.PadWheel(s), false, false, d.Help, true, d.PadStep,
            p => d.PadPickName(s, ClientSessions.Data, p));
        IReadOnlyList<Keys> Press(params Buttons[] b) => pad.KeysFor(d, b, d.PadWheel(s), stepHelp: d.Help, stepName: d.PadStep);
        Press();
        Assert.Equal($"Left/Right 1 {s.Party.Members[0].Name}  X choose  B cancel", Help());
        d.HandleKeys(Press(Buttons.DPadRight, Buttons.X), s); // Priesto casts
        Press();
        Assert.StartsWith($"Left/Right 1 {ClientSessions.Data.GetSpell(SpellId.HealingI).Name}", Help());
        d.HandleKeys(Press(Buttons.X), s); // HEALING I: heal whom
        Press();
        Assert.Equal($"Left/Right 1 {s.Party.Members[0].Name}  X choose  B cancel", Help()); // starts at 1, not at the caster's 2
    }

    [Fact]
    public void Rename_HasItsOwnLine_BGoesBack()
    {
        Assert.EndsWith("B back", UiText.S("member.type_name_enter_accept_esc_back.wheel"));
        Assert.EndsWith("B cancel", UiText.S("member.type_name_enter_accept_esc_cancel.wheel"));
        Assert.DoesNotContain("B ", UiText.S("towndlg.type_amount_back_clear_digit_enter.wheel")); // DOS withdraw: B does nothing
    }
}
