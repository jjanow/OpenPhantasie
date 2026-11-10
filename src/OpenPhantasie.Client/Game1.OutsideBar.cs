using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private readonly AmigaOutsideBar _outsideBar = new();

    /// <summary>
    /// s136: III Amiga's menu bar outside the towns (<see cref="AmigaOutsideBar"/>) shows under AMIGA SCREENS on the
    /// overworld and in a dungeon with an Amiga pack, over the screen's top rows, while the status line is off (it would
    /// sit under the bar).
    /// </summary>
    private bool OutsideBarShown(GameSession s) =>
        s.Game == GameId.P3 && s.Mode is GameMode.Overworld or GameMode.Dungeon && CurrentGameSettings.OriginalScreens
        && _assets?.KeepWindows is not null && !CurrentGameSettings.StatusLines && !FullScreenOverworld(s);

    /// <summary>The bar answers the mouse and its keys only when no dialog, prompt or overlay owns them.</summary>
    private bool OutsideBarTakesInput() =>
        _session is { } s && OutsideBarShown(s) && OpenModal is null && CurrentPrompt is null && !_dosDialog.IsOpen
        && !_showInventory && !_showBestiary && !_combatUi.IsPresenting;

    /// <summary>
    /// s141: III Amiga's town / guild dispatch (<c>0xb1cc</c>) sends <c>?</c> and <c>/</c> to File › Info (<c>0x86e6</c>, the
    /// credits); the keys reach it on the street (the town bar's File › Info too, s156), under AMIGA SCREENS
    /// with the pack's credits, while no dialog, prompt, list or building owns the keys.
    /// </summary>
    private bool TownInfoTakesInput(GameSession s) =>
        s.Game == GameId.P3 && s.Mode is GameMode.Town or GameMode.Guild && CurrentGameSettings.OriginalScreens
        && _assets?.KeepWindows?.Credits is not null && OpenModal is null && CurrentPrompt is null && !_dosDialog.IsOpen
        && !_showInventory && !_showBestiary && !_combatUi.IsPresenting && _doorFlash is null && !_amigaMessage.Shown
        && (s.Mode == GameMode.Guild || _townMenu.Place == TownPlace.Menu);

    /// <summary>
    /// The bar's keys: an OK window (File › Info's credits, Options › Party Status) takes every key while up; Esc closes an
    /// open menu (and is used up); ? and / Info, P Party Status, + (numeric pad) / = / − the speed
    /// (<see cref="AmigaOutsideBar.ForKey"/>). In a III town or guild ? and / open the credits (s141,
    /// <see cref="TownInfoTakesInput"/>). True when the frame's keys were used here. The menu closes when the bar is not
    /// shown.
    /// </summary>
    /// <remarks>"Pad coverage" (s182): an OK window also closes with the pad's A or B (Return / Esc,
    /// <see cref="InputMap.MenuPadKeys"/>); the bar's menus take the pad's own buttons (<see cref="AmigaOutsideBar.HandleButton"/>:
    /// Start, D-pad, A, B), and a button the bar takes goes no further (no step, no fight).</remarks>
    private bool HandleOutsideBarKeys(GameSession s, IEnumerable<Keys> newKeys, IEnumerable<Buttons> newButtons)
    {
        var buttons = newButtons.ToList();
        if (_outsideBar.HandleWindowKeys(newKeys.Concat(InputMap.MenuPadKeys(buttons)).ToList()))
            return true;
        if (!OutsideBarShown(s))
        {
            _outsideBar.CloseMenu();
            if (TownInfoTakesInput(s) && newKeys.Any(k => AmigaOutsideBar.ForKey(k) == OutsideBarAct.Info))
            {
                _outsideBar.ShowCredits();
                return true;
            }
            return false;
        }
        if (!OutsideBarTakesInput())
            return false;
        foreach (var button in buttons)
        {
            var (taken, act) = _outsideBar.HandleButton(button, CurrentGameSettings.CombatSpeed);
            if (!taken)
                continue;
            _hitsFresh = false; // the next draw lays out the menu as it is now
            if (act != OutsideBarAct.None)
                RunOutsideBarAct(act);
            return true;
        }
        var keys = newKeys.ToList();
        if (_outsideBar.Open is not null && keys.Contains(Keys.Escape))
        {
            _outsideBar.Close();
            return true;
        }
        var used = false;
        foreach (var key in keys)
            if (AmigaOutsideBar.ForKey(key) is var act and not OutsideBarAct.None)
            {
                RunOutsideBarAct(act);
                used = true;
            }
        return used;
    }

    /// <summary>
    /// An item of the bar: the remaster's dialog for it (Cast, Examine → inspect, Use Item → use potion) or the speed; Info
    /// the Amiga's credits window 0 (s140) and Party Status its window 22, the trip summary (s141, FORMAT.md 8.9), when the
    /// pack has them (else the remaster's health dialog stands in for Party Status).
    /// </summary>
    private void RunOutsideBarAct(OutsideBarAct act)
    {
        if (_session is not { } s)
            return;
        if (act == OutsideBarAct.Info)
        {
            if (_assets?.KeepWindows?.Credits is not null)
                _outsideBar.ShowCredits();
            return;
        }
        if (AmigaOutsideBar.SpeedOf(act) is { } speed)
        {
            _settings.Game(s.Game).CombatSpeed = speed; // the Amiga's one Speed word (0x240f6), the fight's too
            _settings.Save();
            return;
        }
        string? refused = act switch
        {
            OutsideBarAct.Cast => _castDialog.Open(s),
            OutsideBarAct.Examine => _inspectDialog.Open(s),
            OutsideBarAct.UseItem => _potionDialog.Open(s),
            _ => null,
        };
        if (act == OutsideBarAct.PartyStatus)
        {
            if (_assets?.KeepWindows?.PartyStatus is not null)
            {
                _outsideBar.ShowStatus();
                return;
            }
            _healthDialog.Open();
        }
        if (refused is not null)
            SetFeedback(refused, HelpForMode(s.Mode));
        else if (OpenModal is { } opened)
            SetFeedback(_message, opened.Help);
    }
}
