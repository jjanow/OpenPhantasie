using OpenPhantasie.Client;

namespace OpenPhantasie.Client.Tests;

public class ClientSettingsTests
{
    [Fact]
    public void NewSettings_StartInTheMiddleOfTheTimeLagScale() =>
        Assert.Equal(5, new ClientSettings().TimeLag);

    /// <summary>D-073: lag 0 shows a round as soon as the orders are in; the pause grows with each step up to 9.</summary>
    [Fact]
    public void RoundDelay_IsZeroAtLag0_AndGrowsToLag9_D073()
    {
        Assert.Equal(0, ClientSettings.RoundDelayMs(0));
        for (var lag = 1; lag <= 9; lag++)
            Assert.True(ClientSettings.RoundDelayMs(lag) > ClientSettings.RoundDelayMs(lag - 1));
        Assert.True(ClientSettings.RoundDelayMs(9) >= 1000);
    }

    [Fact]
    public void SavedTimeLag_IsClamped() =>
        Assert.Equal(9, ClientSettings.FromJson("""{ "timeLag": 42 }""")!.TimeLag);

    /// <summary>Settings saved before D-073 (combatSpeed, the F binding) load without losing the rest.</summary>
    [Fact]
    public void StaleSpeedFields_DoNotResetTheOtherSettings()
    {
        var s = ClientSettings.FromJson("""
            { "integerScale": 4, "combatSpeed": "Instant", "dosSaveFolder": "/x",
              "keyBindings": { "ToggleCombatSpeed": "F", "Save": "F1" } }
            """)!;
        Assert.Equal(4, s.IntegerScale);
        Assert.Equal("/x", s.DosSaveFolder);
        Assert.Equal("F1", s.KeyBindings["Save"]);
        Assert.Equal(ClientSettings.DefaultTimeLag, s.TimeLag);
    }
}
