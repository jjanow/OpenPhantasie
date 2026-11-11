using OpenPhantasie.Core;
using OpenPhantasie.Data;
using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

public sealed partial class P4DungeonScreen
{
    private bool _unifiedGuildShortcut;
    public bool UnifiedTownReady => Game.Options.UnifiedTownMenu && OnTownStreet && !MainMenuAsked
        && !DoorOpen && !MysticBusy && !WelcomeUp;

    public UnifiedTownContext UnifiedTownContext => new(GameId.P4, Game.Party.Members.Count, Trade: true,
        Hall: W.Site?.Type == 0, Shipwright: W.Site?.Type == 1);

    /// <summary>Existing IV service flows; the shared shell handles preferences, sound and saves.</summary>
    public void RunUnifiedTownCommand(UnifiedTownCommand command)
    {
        if (!UnifiedTownReady || UnifiedTownContext.Refusal(command) is not null)
            return;
        P4Building? building = command switch
        {
            UnifiedTownCommand.Guild => P4Building.Guild, UnifiedTownCommand.Armory => P4Building.Armory,
            UnifiedTownCommand.Bank => P4Building.Bank, UnifiedTownCommand.Inn => P4Building.Inn,
            UnifiedTownCommand.Mystic => P4Building.Mystic, UnifiedTownCommand.Hall => P4Building.HallOfGlory,
            UnifiedTownCommand.Shipwright => P4Building.Shipwright, UnifiedTownCommand.Leave => P4Building.Leave,
            _ => null,
        };
        Cursor = 0;
        if (building is { } door)
            EnterFromStreet(door);
        else switch (command)
        {
            case UnifiedTownCommand.Training or UnifiedTownCommand.Learn:
                W.Enter(P4Building.Guild);
                W.Choose(command == UnifiedTownCommand.Training ? 1 : 2);
                _unifiedGuildShortcut = true;
                break;
            case UnifiedTownCommand.Trade: W.Command(P4TownCommand.Trade); break;
            case UnifiedTownCommand.Distribute: W.Command(P4TownCommand.Distribute); break;
            case UnifiedTownCommand.Inspect: Game.OpenMenu(P4MenuCommand.Profile); break;
            case UnifiedTownCommand.Use: Game.OpenMenu(P4MenuCommand.Use); break;
            case UnifiedTownCommand.Cast: Game.OpenMenu(P4MenuCommand.Cast); break;
            case UnifiedTownCommand.Resurrection or UnifiedTownCommand.Transportation:
                Game.OpenMenu(P4MenuCommand.Cast, TownMenuData.Spells(GameId.P4, command.ToString().ToLowerInvariant()));
                break;
            case UnifiedTownCommand.GameMenu: OpenSystem(); break;
        }
        Game.Follow();
    }

    private void ReturnFromUnifiedTownService()
    {
        if (!_unifiedGuildShortcut || W.IconMenu != "_guildmenu" || W.Prompt != P4TownPrompt.Pick)
            return;
        _unifiedGuildShortcut = false;
        W.Choose(3);
        Cursor = 0;
        Game.Follow();
    }
}
