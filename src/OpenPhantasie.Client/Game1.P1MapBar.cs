using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private readonly P1MapMenuBar _p1MapBar = new();
    private Keys? _p1MapBarKey; // a pad-picked item's command key, sent at the start of the next frame

    /// <summary>
    /// s155: I Amiga's map screen top and bottom (pack <c>map_message.menu_bar</c> / <c>line</c>): under AMIGA SCREENS with
    /// I's pack, on the map-only layout (F2: the overworld's whole region at (4, 15), the dungeon's glyphs at (10, 16)) the
    /// Amiga's menu bar takes the top rows and the remaster's message goes on the Amiga's message line (TopEdge 192). Not
    /// with the status line on (it would sit under the bar), nor on the layout with the party sidebar (the remaster's own).
    /// </summary>
    private MapMessageLayout? P1MapScreen(GameSession s) =>
        s.Game == GameId.P1 && CurrentGameSettings.OriginalScreens && !CurrentGameSettings.StatusLines && _settings.MapOnly
        && _assets?.Pack?.MapMessage is { MenuBar: not null, Line: not null } layout
        && (FullScreenOverworld(s) || ViewMode(s) == GameMode.Dungeon && s.Dungeon is not null && _assets.Maps is not null)
            ? layout : null;

    /// <summary>The bar answers the mouse and Esc only while the map waits: no box, report, question, dialog or overlay.</summary>
    private bool P1MapBarTakesInput(GameSession s) =>
        P1MapScreen(s) is not null && s.Mode is GameMode.Overworld or GameMode.Dungeon && _mapRun is null
        && CurrentPrompt is null && !P1HeldRequesterUp && OpenModal is null && !_dosDialog.IsOpen && !_combatUi.IsPresenting
        && !_showInventory && !_showBestiary && !DosPagesUp && !_p1Holds.Busy(_updateSeconds);

    /// <summary>
    /// s193 (pad fix E29): the pad's one-row hint for I Amiga's map line (A fight, Start the menu strip, RT + A the command
    /// list, the right stick back to the party view), or null. Only while the pad is the input, the map waits with its own
    /// help (no dialog's), no menu is pulled down and no fresh message holds the line (<paramref name="messageHolds"/>):
    /// the line is the message's own row, so the hint takes it in turn and covers nothing of the map.
    /// </summary>
    private string? P1MapPadHint(GameSession s, bool messageHolds) =>
        PadWording.Active && !messageHolds && _help == HelpForMode(s.Mode) && _p1MapBar.Open is null && P1MapBarTakesInput(s)
            ? UiText.S("game.p1map_pad_hint") : null;

    /// <summary>
    /// A key closes an open map menu (Esc is used up, any other key goes on); the menu closes by itself when the bar stops
    /// taking input.
    /// </summary>
    /// <remarks>"Pad coverage" (s182): the strip takes the pad's own buttons (<see cref="P1MapMenuBar.HandleButton"/>: Start
    /// pulls Options down, D-pad, A, B), and a button it takes goes no further (no step, no fight). An item's command key
    /// is sent at the start of the next frame (<see cref="_p1MapBarKey"/>): this frame ends here.</remarks>
    private bool HandleP1MapBarKeys(GameSession s, IEnumerable<Keys> newKeys, IEnumerable<Buttons> newButtons)
    {
        if (_p1MapBarKey is { } pending)
        {
            _p1MapBarKey = null;
            _input.Inject([pending]);
            return false;
        }
        if (P1MapScreen(s)?.MenuBar is { } padBar && P1MapBarTakesInput(s))
            foreach (var button in newButtons)
            {
                var (taken, action) = _p1MapBar.HandleButton(padBar, button, s.Mode != GameMode.Dungeon);
                if (!taken)
                    continue;
                _hitsFresh = false; // the next draw lays out the strip as it is now
                if (action.Length > 0)
                    RunP1MapBarAction(s, action, nextFrame: true);
                return true;
            }
        if (_p1MapBar.Open is null)
            return false;
        if (!P1MapBarTakesInput(s))
        {
            _p1MapBar.Close();
            return false;
        }
        var keys = newKeys.ToList();
        if (keys.Count == 0)
            return false;
        // The Amiga's strip exists only while the button is held, so nothing else runs under it: any key closes the
        // remaster's open menu; Esc is used up, another key goes on to its command.
        _p1MapBar.Close();
        return keys.Contains(Keys.Escape);
    }

    /// <summary>
    /// A map menu item: the pack's action runs the remaster's command through its bound key (so a remapped key still
    /// works): Inspect, Health (requester 21 on I Amiga), Cast Spell, Use potions, Look (the dungeon only: ghosted and inert
    /// outdoors); Speed sets the game's COMBAT SPEED (the Amiga's one Speed word, the fight's too). About Phantasie shows
    /// requester 27 with the logo (s156).
    /// </summary>
    /// <param name="nextFrame">The command key goes in at the start of the next frame (a pad pick, whose frame ends at the
    /// strip), not this one (a click: the mouse runs before the frame's keys are read).</param>
    private void RunP1MapBarAction(GameSession s, string action, bool nextFrame = false)
    {
        if (P1MapMenuBar.SpeedOf(action) is { } speed)
        {
            _p1SpeedPicked = true;
            _settings.Game(s.Game).CombatSpeed = speed;
            _combatUi.FieldSpeed = speed;
            _settings.Save();
            return;
        }
        if (action == "about" && P1Requesters is { About: not null })
        {
            _p1About = true; // s156: requester 27 with the logo (h80 +0xcb4)
            return;
        }
        GameAction? command = action switch
        {
            "inspect" => GameAction.Inspect,
            "health" => GameAction.Health,
            "cast" => GameAction.CastSpell,
            "use_potion" => GameAction.UsePotion,
            "look" when s.Mode == GameMode.Dungeon => GameAction.Look,
            _ => null,
        };
        if (command is not { } c)
            return;
        if (nextFrame)
            _p1MapBarKey = _settings.GetKey(c);
        else
            _input.Inject([_settings.GetKey(c)]);
    }

    /// <summary>The mouse on the bar (<see cref="UiOverlays.DrawP1MapMenuBar"/>'s regions).</summary>
    private void P1MapBarMouse(object? tag, bool clicked)
    {
        if (_session is not { } s || P1MapScreen(s)?.MenuBar is not { } bar || !P1MapBarTakesInput(s))
            return;
        switch (tag)
        {
            case P1MapMenuTitleTag title:
                if (clicked)
                    _p1MapBar.Toggle(bar, title.Menu);
                else
                    _p1MapBar.Hover(bar, -1); // off the items: none lit
                break;
            case P1MapMenuItemTag item:
                if (_p1MapBar.Open is { } menu && P1MapMenuBar.Ghosted(bar, menu, item.Item, s.Mode != GameMode.Dungeon))
                {
                    _p1MapBar.Hover(bar, -1); // a ghosted item neither lights nor runs; a click on it closes the
                    if (clicked)              // strip with no pick (the remaster's reading, not filmed)
                        _p1MapBar.Close();
                    break;
                }
                _p1MapBar.Hover(bar, item.Item);
                if (clicked)
                    RunP1MapBarAction(s, _p1MapBar.Take(bar, item.Item));
                break;
            case P1MapMenuBackdropTag:
                if (clicked)
                    _p1MapBar.Close();
                else
                    _p1MapBar.Hover(bar, -1);
                break;
        }
    }
}
