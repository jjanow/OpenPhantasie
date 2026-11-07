using Microsoft.Xna.Framework;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

public partial class Game1
{
    // s154: I Amiga's "LEAVING %s" (h29 +0x168, after the town loop h5 +0xde returns): h80 +0x818(text, 4) clears the town
    // line and prints it centred in z6font, pen 4 (EE0), the name in its 10-character field (x 106 for PELNOR); then the
    // optional auto-save and the overworld's load, with no wait of its own. DOS OV1 0x483e prints the same text at column
    // 11 of row 24 (not drawn here: the DOS path keeps the remaster's screens). Under AMIGA SCREENS with a pack that names
    // the line's colour, the town picture holds with the line for the pack's filmed time before the map shows.
    private GameMode? _leavingLastMode;
    private int _leavingLastTown;
    private string? _leavingLastName;
    private string? _leavingLine;
    private int _leavingTown;
    private double _leavingFrom;
    private double _leavingUntil;

    /// <summary>
    /// Notes a town (or Guild) → overworld change since the last call and, under AMIGA SCREENS with I's pack, starts the
    /// leaving line's hold. Called from Update and Draw, so the map is never drawn before the line.
    /// </summary>
    private void NoteLeaving(GameSession s)
    {
        var mode = s.Mode;
        if (_leavingLastMode is GameMode.Town or GameMode.Guild && mode == GameMode.Overworld && _leavingLastName is { } name
            && s.Game == GameId.P1 && CurrentGameSettings.OriginalScreens
            && _assets?.TownScreen is { LeavingColour: not null, LeavingSeconds: > 0 } layout)
        {
            _leavingLine = UiOverlays.TownLeavingLine(layout, name);
            _leavingTown = _leavingLastTown;
            _leavingFrom = _updateSeconds;
            _leavingUntil = _updateSeconds + layout.LeavingSeconds + layout.LeavingBlackSeconds;
        }
        _leavingLastMode = mode;
        if (mode is GameMode.Town or GameMode.Guild)
        {
            _leavingLastTown = s.CurrentTownId ?? _leavingLastTown;
            _leavingLastName = s.CurrentTown?.Name;
        }
    }

    /// <summary>True while the leaving line holds the screen (keys and clicks wait: the Amiga is loading the map).</summary>
    private bool LeavingShown => _leavingLine is not null && _updateSeconds < _leavingUntil;

    /// <summary>s154: forgets the hold and the last mode (a load, the main menu).</summary>
    private void ResetLeaving(GameSession? s)
    {
        _leavingLine = null;
        _leavingLastMode = s?.Mode;
        _leavingLastName = s?.CurrentTown?.Name;
        _leavingLastTown = s?.CurrentTownId ?? 0;
    }

    /// <summary>
    /// The town picture with the leaving line over everything (no bar, no name: the line was cleared first); then (s156) the
    /// whole screen black for the pack's <see cref="OpenPhantasie.Data.TownScreenLayout.LeavingBlackSeconds"/> while the Amiga
    /// loads the map.
    /// </summary>
    private void DrawLeaving()
    {
        if (!LeavingShown || _assets?.TownScreen is not { LeavingColour: { } colour } layout)
            return;
        UiOverlays.Hits?.Clear();
        if (layout.LeavingAt(_updateSeconds - _leavingFrom) == OpenPhantasie.Data.LeavingPhase.Black)
        {
            _spriteBatch!.Draw(_pixel!, new Rectangle(0, 0, VirtualScreen.LayoutWidth, VirtualScreen.LayoutHeight), Color.Black);
            return;
        }
        UiOverlays.DrawTownLeaving(_spriteBatch!, _pixel!, _assets, layout, _leavingTown, _leavingLine!,
            VirtualScreen.ParseColor(colour));
    }
}
