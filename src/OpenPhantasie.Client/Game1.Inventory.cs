using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The shell's hooks for the paperdoll + stash screen (<see cref="InventoryScreen"/>, QoL
/// <see cref="GameSettings.InventoryScreen"/>). Open, it is the inventory overlay (<c>_showInventory</c>), so every guard
/// that keeps prompts, dialogs, walks and gameplay keys away from the V overlay applies to it too.
/// </summary>
public partial class Game1
{
    private readonly InventoryScreen _inventoryScreen = new();

    private bool InventoryScreenOpen => _showInventory && _inventoryScreen.IsOpen;

    /// <summary>V with the option on: open the screen on the town's current shopper.</summary>
    private void OpenInventoryScreen(GameSession s, GamePadState pad)
    {
        _inventoryScreen.Open(s, _data!, _members.Clamp(Math.Max(1, s.Party.Members.Count)));
        if (!_inventoryScreen.IsOpen)
            return; // no party: nothing to show
        _showInventory = true;
        _showBestiary = false;
        _inventoryCompare = false;
        _inventoryScreen.UsingPad = pad.Buttons.X == ButtonState.Pressed; // opened with the pad's X: show the button hints
        SetFeedback(_inventoryScreen.ReadOnlyReason ?? "", _inventoryScreen.Help);
    }

    private void CloseInventoryScreen()
    {
        _inventoryScreen.Close();
        _showInventory = false;
        SetFeedback("", HelpForMode(_session!.Mode));
    }

    /// <summary>
    /// The screen owns every key and pad button while open (V / pad X closes it; Esc / pad B puts back what is held, then
    /// closes).
    /// </summary>
    private void HandleInventoryScreenKeys(KeyboardState keys, GamePadState pad)
    {
        if (_input.Pressed(GameAction.ToggleInventory, keys, pad)) // V or the pad's X
        {
            CloseInventoryScreen();
            return;
        }
        var toggle = _settings.GetKey(GameAction.ToggleInventory);
        foreach (var key in _input.NewKeys(keys))
        {
            if (key == toggle)
            {
                CloseInventoryScreen();
                return;
            }
            var line = _inventoryScreen.HandleKey(key);
            if (!_inventoryScreen.IsOpen)
            {
                CloseInventoryScreen();
                return;
            }
            InventoryFeedback(line);
        }
        foreach (var button in _input.NewButtons(pad))
        {
            var line = _inventoryScreen.HandleButton(button);
            if (!_inventoryScreen.IsOpen)
            {
                CloseInventoryScreen();
                return;
            }
            InventoryFeedback(line);
        }
    }

    private void InventoryFeedback(string? line)
    {
        if (_inventoryScreen.IsOpen)
            SetFeedback(line ?? _message, _inventoryScreen.Help);
    }

    /// <summary>Drag and drop on the screen: a drag picks up what is under its start and drops it where it is let go.</summary>
    private void InventoryDrag()
    {
        if (!InventoryScreenOpen)
            return;
        if (_mouse.DragStarted && _mouse.DragOrigin is { } from && _inventoryScreen.Held is null
            && _hits.At(from)?.Tag is InventorySpot spot)
        {
            InventoryFeedback(_inventoryScreen.PickUp(spot));
            _mouse.DragPayload = _inventoryScreen.Held;
        }
        if (_mouse.DragEnded is var (_, to) && _mouse.DragPayload is InventorySpot source)
        {
            InventoryFeedback(_hits.At(to)?.Tag is InventorySpot target and not (StashFilterSpot or StashSortSpot)
                ? _inventoryScreen.Drop(source, target)
                : _inventoryScreen.PutBack());
            _mouse.DragPayload = null;
            _hitsFresh = false;
        }
    }

    /// <summary>Hover moves the cursor (during a drag, a member tab shows that member); a click picks up or places (Enter).</summary>
    private void InventoryPoint(InventorySpot spot, bool clicked)
    {
        if (!InventoryScreenOpen)
            return;
        if (clicked)
            InventoryFeedback(_inventoryScreen.Click(spot));
        else if (_mouse.Dragging)
            _inventoryScreen.DragOver(spot);
        else
            _inventoryScreen.Point(spot);
    }
}
