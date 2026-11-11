using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private readonly UnifiedTownMenu _unifiedTown = new();
    private string? _unifiedTownNote;

    private bool UnifiedTownShown => !_options.IsOpen && !_slotScreen.IsOpen && (_p4 is { UnifiedTownReady: true }
        || _p4 is null && _session is { Mode: GameMode.Town, Options.UnifiedTownMenu: true } && _townMenu.Place == TownPlace.Menu
            && OpenModal is null && CurrentPrompt is null && !_dosDialog.IsOpen && !_showInventory && !_showBestiary
            && !_combatUi.IsPresenting && _doorFlash is null && !_amigaMessage.Shown && !_damageReport.Shown
            && !P1HeldRequesterUp && !DosPagesUp && _p2Alert.Current is null && !_outsideBar.CreditsShown && !_outsideBar.StatusShown && !LeavingShown);

    private UnifiedTownContext UnifiedTownContext => _p4?.UnifiedTownContext
        ?? new(_session!.Game, _session.Party.Members.Count, Trade: _session.CanTrade || _session.Options.ModernInventory,
            AutoSaveRequired: _session.Options.AutoSaveAlways);

    private bool HandleUnifiedTownInput(KeyboardState keys, GamePadState pad)
    {
        if (!UnifiedTownShown)
            return false;
        _townBar.Close();
        if (_p4 is null && _input.Pressed(GameAction.ToggleInventory, keys, pad))
        {
            HandleGlobalInput(keys, pad);
            return true;
        }
        foreach (var key in _input.NewKeys(keys).Concat(UnifiedTownMenu.PadKeys(_input.NewButtons(pad))))
        {
            _unifiedTownNote = null;
            if (_unifiedTown.HandleKey(key) is not { } command)
                continue;
            if (UnifiedTownContext.Refusal(command) is { } refusal)
                _unifiedTownNote = refusal;
            else
                RunUnifiedTownCommand(command, pad);
            break; // a service never also reads the input that opened it
        }
        return true;
    }

    private void RunUnifiedTownCommand(UnifiedTownCommand command, GamePadState pad)
    {
        if (_p4 is { } p4)
        {
            switch (command)
            {
                case UnifiedTownCommand.Save or UnifiedTownCommand.Load:
                    if (p4.HandleKeys([command == UnifiedTownCommand.Save ? Keys.F5 : Keys.F9]) is { } request)
                        RunPhantasie4Request(request);
                    break;
                case UnifiedTownCommand.Options: _options.OpenFor(p4.Game.Options); break;
                case UnifiedTownCommand.Sound:
                    _settings.Sound = !_settings.Sound;
                    _settings.Save();
                    _unifiedTownNote = UiText.S(_settings.Sound ? "game.sound_on" : "game.sound_off");
                    break;
                default: p4.RunUnifiedTownCommand(command); break;
            }
            return;
        }
        var s = _session!;
        var shopper = s.Party.Members.Count == 0 ? null : s.Party.Members[_members.Clamp(s.Party.Members.Count)];
        switch (command)
        {
            case UnifiedTownCommand.Armory:
                _townMenu.HandleKey(Keys.A);
                break;
            case UnifiedTownCommand.Bank when s.TreasuryOn:
                OpenTreasury();
                break;
            case UnifiedTownCommand.Bank:
                _townMenu.DosBank = s.BankIsDos;
                _townMenu.HandleKey(Keys.B);
                break;
            case UnifiedTownCommand.Training or UnifiedTownCommand.Learn:
                s.OpenGuild();
                RunGuildBarAction(s, command == UnifiedTownCommand.Training ? "training" : "learn");
                break;
            case UnifiedTownCommand.Resurrection or UnifiedTownCommand.Transportation:
                var refusal = _castDialog.Open(s, OpenPhantasie.Data.TownMenuData.Spells(s.Game, command.ToString().ToLowerInvariant()));
                SetFeedback(refusal ?? _message, _castDialog.Help);
                break;
            case UnifiedTownCommand.Load: LoadOrAsk(s); break;
            case UnifiedTownCommand.Options: _options.Open(s); break;
            case UnifiedTownCommand.GameMenu: OpenGameMenu(); break;
            case UnifiedTownCommand.Trade when !s.CanTrade && s.Options.ModernInventory:
                OpenInventoryScreen(s, pad);
                break;
            default:
                var choice = command switch
                {
                    UnifiedTownCommand.Guild => TownChoice.Guild, UnifiedTownCommand.Inn => TownChoice.Inn,
                    UnifiedTownCommand.Mystic => TownChoice.Mystic, UnifiedTownCommand.Leave => TownChoice.Exit,
                    UnifiedTownCommand.Inspect => TownChoice.ShowParty, UnifiedTownCommand.Trade => TownChoice.Trade,
                    UnifiedTownCommand.Distribute => TownChoice.Distribute, UnifiedTownCommand.Use => TownChoice.UseItem,
                    UnifiedTownCommand.Cast => TownChoice.CastSpell, UnifiedTownCommand.Save => TownChoice.SaveGame,
                    UnifiedTownCommand.AutoSave => TownChoice.ToggleAutoSave, UnifiedTownCommand.Sound => TownChoice.ToggleSound,
                    UnifiedTownCommand.Printout => TownChoice.Printout,
                    _ => throw new ArgumentOutOfRangeException(nameof(command)),
                };
                RunTownChoice(s, choice, shopper);
                if (command == UnifiedTownCommand.Inn)
                    _townMenu.Reset(); // the same shared street after a rest, rather than I's Inn submenu
                if (command is UnifiedTownCommand.Inn or UnifiedTownCommand.Sound or UnifiedTownCommand.AutoSave)
                    _unifiedTownNote = _message;
                break;
        }
    }

    private void DrawUnifiedTown()
    {
        if (!UnifiedTownShown || _spriteBatch is null || _pixel is null)
            return;
        var town = _p4 is { } p4 ? p4.Game.Town.Site?.Name ?? UiText.F("ui.town_x", p4.Game.Town.Town)
            : _session?.CurrentTown?.Name ?? "";
        var gold = _p4 is { } fourth ? fourth.Game.Party.SpendableGold(fourth.Game.Options) : _session!.SpendableGold;
        _unifiedTown.Draw(_spriteBatch, _pixel, town, UnifiedTownContext.Refusal, _hits, _unifiedTownNote,
            UiText.F("unifiedtown.status", gold, UnifiedTownContext.Members));
    }
}
