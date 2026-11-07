using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Wilderness / dungeon Options → Health (OV2 <c>0x450c</c>, called from the Options menu <c>0x14</c> at <c>0x5d39</c>) [C]
/// (s49 static): "HEALTH REPORT:" then one line per party slot <c>&lt; [0x648e]</c>, format DS <c>0xbd7d</c>
/// <c>"%-10.9s%-6.5sHP%4d/%-4dMP%3d/%-3d"</c> = name, status name (COM table: DEAD / OK / SLEEP), HP <c>@22/@20</c>,
/// MP <c>@18/@16</c>; any key closes it. Matches the s43 capture ("name status HP cur/max MP cur/max"). The combat
/// PARTY OPTIONS "Party health" is a different routine (<see cref="CombatText.HealthReport"/>).
/// </summary>
public sealed class HealthDialog : IModalDialog
{
    public bool IsOpen { get; private set; }

    public string Help => UiText.S("health.any_key_to_continue");

    public void Open() => IsOpen = true;

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        if (!newKeys.Any())
            return null;
        IsOpen = false;
        return "";
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) => Report(session);

    public static IReadOnlyList<string> Report(GameSession session) =>
        [CombatText.HealthReportTitle, .. session.Party.Members.Select(Line)];

    public static string Line(Character ch)
    {
        var name = ch.Name.Length > 9 ? ch.Name[..9] : ch.Name;
        var status = ch.Status switch
        {
            CharacterStatus.Dead => UiText.S("health.dead"),
            CharacterStatus.Sleep => UiText.S("health.sleep"),
            _ => UiText.S("health.okay"), // D-064: the manual's party table (DOS "OK")
        };
        return UiText.F("health.x_x_hp_x_x_mp", name, status, ch.HpCur, ch.HpMax, ch.MagicCur, ch.MagicMax);
    }
}
