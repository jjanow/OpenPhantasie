using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Audio;

/// <summary>
/// s157: a title tune that plays on after its splash (<see cref="TitleTune.PlaysOn"/>; III Amiga's <c>D/M0</c>, which the
/// VBL driver keeps playing over the Initial Menu, FORMAT.md 7.1). The rules for keeping it and for cutting it, kept out
/// of <see cref="Game1"/> so they can be tested headless.
/// </summary>
public static class TitleTuneCarry
{
    /// <summary>Whether the splash's tune goes on when the splash ends: the pack asks for it and it still sounds.</summary>
    public static bool CarriesOn(TitleTune? tune, bool playing) => tune?.PlaysOn == true && playing;

    /// <summary>
    /// Whether the carried tune stops this frame: it has ended (<paramref name="finished"/>); the player left the game it
    /// belongs to (<paramref name="session"/> null = back in the main menu, or another game started); the game's TITLE
    /// TUNE switch was turned off; or the game started a sound effect (<paramref name="effectStarted"/>: the Amiga's every
    /// effect start <c>0xb126</c> → <c>0x1f52</c> resets all three voices, music included, FORMAT.md 7.2; s158: the Client
    /// passes it at the effect's own time (<see cref="CutAtMs"/>), with the Sound switch off too). A new game or
    /// a load in the same game does not stop it (nothing on the Amiga's way from the Initial Menu into the game does).
    /// </summary>
    public static bool Stops(bool finished, GameId tuneGame, GameId? session, bool titleTuneOn, bool effectStarted) =>
        finished || session != tuneGame || !titleTuneOn || effectStarted;

    /// <summary>
    /// s158: when a run of driver starts cuts the carried tune: at its first start, <paramref name="plays"/>' earliest VBL at
    /// <paramref name="vblHz"/> after <paramref name="nowMs"/> (ms), not when the run is planned (a fight's set-up does
    /// not stop the driver on the Amiga; the round's first hit, miss, cast, " dies!" or flee stop 33 does). Null = no start
    /// (nothing cuts it). Kept whatever the Sound switch says: the Amiga has none (D3-066), so its music stops there.
    /// </summary>
    public static double? CutAtMs(double nowMs, IEnumerable<(int Frame, int Sound)> plays, double vblHz)
    {
        ArgumentNullException.ThrowIfNull(plays);
        var first = plays.Select(p => (int?)p.Frame).Min();
        return first is int f ? nowMs + Math.Max(0, f) * 1000.0 / vblHz : null;
    }

    /// <summary>The earlier of two pending cut times (null = none).</summary>
    public static double? Earlier(double? a, double? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);
}
