using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// QoL <c>shared-aging</c> (<c>docs/plan/13-aging-normalised.md</c>) in the Options tab: one row in the QUALITY OF LIFE
/// section, THE GAME'S OWN for new games under the default preset, SHARED in a locked Remaster save.
/// </summary>
public class SharedAgingClientTests
{
    private static string Row(IEnumerable<string> rows) =>
        rows.Select(t => t.Trim()).Single(t => t.StartsWith(UiText.S("option.shared-aging"), StringComparison.Ordinal));

    [OriginalDataFact]
    public void Options_ShowTheRowUnderQualityOfLife_WithThePresetsValue()
    {
        var o = new OptionsScreen(new ClientSettings(), _ => [], g => g == GameId.P1);
        o.Open(null);
        o.HandleKeys([Keys.Right]); // tab I
        var rows = o.RowTexts().Select(t => t.Trim()).ToList();
        var row = Row(rows);
        Assert.True(rows.IndexOf(row) > rows.IndexOf(UiText.S("options.quality_of_life")));
        Assert.EndsWith(UiText.S("option.shared-aging.off"), row);

        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(7));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster));
        var locked = new OptionsScreen(new ClientSettings(), _ => [], g => g == GameId.P1);
        locked.Open(s);
        Assert.EndsWith(UiText.S("option.shared-aging.on"), Row(locked.RowTexts()));
    }
}
