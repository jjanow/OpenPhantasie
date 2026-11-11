using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

public partial class Game1
{
    /// <summary>
    /// s185 (Pad leftovers): the full-screen overworld's band (F2's map-only layout, <see cref="UiOverlays.DrawMapHud"/>):
    /// the message, and the help by <see cref="MapOnlyBand"/>.
    /// </summary>
    private void DrawMapOnlyHud(GameSession s, bool messageShows)
    {
        if (_spriteBatch is null || _pixel is null)
            return;
        var mapHelp = UiText.S("game.map_only_help");
        var (message, help) = MapOnlyBand(BandMessage, messageShows, _help, HelpForMode(s.Mode), mapHelp,
            CurrentGameSettings.StatusLines || PadHelpShown(mapHelp));
        UiOverlays.DrawMapHud(_spriteBatch, _pixel, message, UiOverlays.TreasuryGoldLine(s), help);
    }

    /// <summary>
    /// The full-screen overworld band's message and help. The help follows the sidebar layout's rule: a dialog's own help
    /// always (under the message: the dialog is up); the map's own help with STATUS LINES on, or in the pad's words while
    /// the pad is the input (D-077: the manual's screen has no help line, so not for the keyboard with the status line
    /// off). The map's own help is the map-only one (<c>game.map_only_help</c>: no party sidebar to scroll, F2 brings it
    /// back), put in at draw time, so F2 needs no new help set. The map fills the screen from row 15, so the map's own
    /// help keeps the band to its one row: it takes the row while there is no message, or once the message has shown
    /// its few seconds (<paramref name="messageShows"/>, as the town picture's band, s98).
    /// </summary>
    /// <param name="help">The help set now (<c>_help</c>).</param>
    /// <param name="modeHelp">The overworld's own help (<c>HelpForMode</c>): <paramref name="help"/> equal to it is no dialog's.</param>
    /// <param name="mapHelp">The map-only layout's own help, shown in its place.</param>
    /// <param name="showMapHelp">STATUS LINES on, or the pad is the input and <paramref name="mapHelp"/> has pad words.</param>
    public static (string Message, string Help) MapOnlyBand(string message, bool messageShows, string help, string modeHelp,
        string mapHelp, bool showMapHelp)
    {
        if (help != modeHelp)
            return (message, help);
        if (!showMapHelp)
            return (message, "");
        return message.Length > 0 && messageShows ? (message, "") : ("", mapHelp);
    }
}
