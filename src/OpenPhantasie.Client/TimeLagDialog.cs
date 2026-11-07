using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The manual's wilderness T)IME LAG (D-073): "Choose a number between 0 and 9 (0 is the fastest, 9 the slowest). You
/// will also be asked if you want sound: answer Y or N." A digit sets the lag, then Y / N sets the sound; Esc leaves both
/// unchanged. DOS has a fast / normal / slow Speed menu instead (not built, D-073).
/// </summary>
public sealed class TimeLagDialog : IModalDialog
{
    public static string LagQuestion => UiText.S("timelag.time_lag_0_9");
    public static string SoundQuestion => UiText.S("timelag.sound_y_n");

    /// <summary>
    /// Whether <b>T</b> opens this dialog: in the wilderness and the dungeon, unless the game's D-073 QoL switch
    /// (<see cref="GameSettings.TimeLagInOptions"/>) moved the time lag into the Options tab.
    /// </summary>
    public static bool IsCommand(GameMode mode, GameSettings game) =>
        mode is GameMode.Overworld or GameMode.Dungeon && !game.TimeLagInOptions;

    private int? _lag;

    public bool IsOpen { get; private set; }

    private (int Lag, bool Sound)? _chosen;

    /// <summary>The lag and sound Y / N finished the dialog with (once), for the caller to apply and save.</summary>
    public (int Lag, bool Sound)? TakeChosen()
    {
        var chosen = _chosen;
        _chosen = null;
        return chosen;
    }

    public string Help => _lag is null ? UiText.S("timelag.0_fastest_9_slowest_esc_cancel") : UiText.S("timelag.y_sound_on_n_sound_off");

    public void Open()
    {
        IsOpen = true;
        _lag = null;
        _chosen = null;
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (key == Keys.Escape)
            {
                IsOpen = false;
                return "";
            }
            if (_lag is null)
            {
                if (Digit(key) is int d)
                {
                    _lag = d;
                    return "";
                }
            }
            else if (key is Keys.Y or Keys.N)
            {
                _chosen = (_lag.Value, key == Keys.Y);
                IsOpen = false;
                return UiText.F("timelag.time_lag_x_sound_x", _lag.Value, (key == Keys.Y ? UiText.S("timelag.on") : UiText.S("timelag.off")));
            }
        }
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) =>
        _lag is null ? [LagQuestion] : [$"{LagQuestion} {_lag}", SoundQuestion];

    private static int? Digit(Keys key) => key switch
    {
        >= Keys.D0 and <= Keys.D9 => key - Keys.D0,
        >= Keys.NumPad0 and <= Keys.NumPad9 => key - Keys.NumPad0,
        _ => null,
    };
}
