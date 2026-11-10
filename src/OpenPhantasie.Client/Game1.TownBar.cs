using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private readonly AmigaTownBar _townBar = new();
    private Keys? _townBarKey; // a town key an item stands for, sent at the start of the next frame
    private bool _guildFromBar; // s158: a Guild item of the bar is running; the party goes back to the street after it

    /// <summary>
    /// s156: III Amiga's town bar (<see cref="AmigaTownBar"/>) replaces the remaster's DOS row on the town street under
    /// AMIGA SCREENS with a pack that has it (<c>keep_windows.menu_bars.town</c>); null otherwise.
    /// </summary>
    private CombatFieldMenuBar? TownBarPlaces(GameSession s) =>
        s.Game == GameId.P3 && s.Mode == GameMode.Town && CurrentGameSettings.OriginalScreens
        && _assets?.KeepWindows?.MenuBars.GetValueOrDefault("town") is { } bar ? bar : null;

    /// <summary>The bar answers the mouse and the arrows only on the street with nothing else up (as the town's ? / /).</summary>
    private bool TownBarTakesInput(GameSession s) =>
        TownBarPlaces(s) is not null && TownInfoTakesInput(s) && _townMenu.Place == TownPlace.Menu
        && !_outsideBar.CreditsShown && !_outsideBar.StatusShown && !_confirm.Shown;

    /// <summary>The town bar's palette: the map's (w4 holds it word for word), the dim towns' <c>town_dim</c> (0x78ee).</summary>
    private string TownBarPalette(GameSession s) =>
        _assets is { } a && a.IsDimTown(s.CurrentTownId) && a.TownScreen.DimPalette is { } dim ? dim : UiOverlays.MapPalette;

    /// <summary>
    /// The town bar's keys (the remaster's: arrows, Return / Space, Esc on an open menu, <see cref="AmigaTownBar.HandleKey"/>);
    /// its letters take its items (D3-074, <see cref="AmigaTownBar.ActionForKey"/>; with COMMON TOWN KEYS the set's letters
    /// go on to the town's own, the manual's); other keys go on to the town. True when the frame's keys were used here.
    /// </summary>
    private bool HandleTownBarKeys(GameSession s, IEnumerable<Keys> newKeys)
    {
        ReturnFromBarGuild(s);
        if (_townBarKey is { } pending) // Armoury / Bank: the town's own key, read by this frame's town input
        {
            _townBarKey = null;
            _input.Inject([pending]);
            return false;
        }
        if (TownBarPlaces(s) is not { } bar || !TownBarTakesInput(s))
        {
            _townBar.Close();
            return false;
        }
        var used = false;
        foreach (var key in newKeys)
        {
            // D3-074: the bar's letters (0x2189c) are its items' keys, whether a menu is pulled down or not; with COMMON TOWN
            // KEYS on, the set's letters go on to the town's own (the manual's) instead.
            if (AmigaTownBar.ActionForKey(key) is { Length: > 0 } keyed && bar.Menus.Any(m => m.Actions.Contains(keyed))
                && !(CurrentGameSettings.CommonTownKeys && CommonTownKeySet.Contains(key)))
            {
                _townBar.Close();
                RunTownBarAction(s, keyed);
                used = true;
                continue;
            }
            var (taken, action) = _townBar.HandleKey(bar, key);
            used |= taken;
            if (action.Length > 0)
            {
                RunTownBarAction(s, action);
                break;
            }
        }
        return used;
    }

    /// <summary>
    /// A town bar item (the pack's action): the remaster's own command for it. File: Info the credits (window 0), Load the
    /// remaster's load, Save its save. Options: Armoury and Bank as their town keys (A / B: the Armoury screen, the Amiga's
    /// Bank window), Inn / Mystic through their doors, Exit leaves, Cast Spell / Distribute / Trade Item (when the rules
    /// allow trading) / Use Item / View Party (the remaster's inspect). Guild: the party goes into the Guild (its door) and
    /// the Guild's own dialog for the item opens there (Examine: the remaster's inspect).
    /// </summary>
    private void RunTownBarAction(GameSession s, string action)
    {
        var shopper = s.Party.Members.Count > 0 ? s.Party.Members[_members.Clamp(s.Party.Members.Count)] : null;
        void Go(TownChoice choice)
        {
            if (!StartDoorFlash(TownDoor(choice), () => RunTownChoice(s, choice, shopper)))
                RunTownChoice(s, choice, shopper);
        }
        switch (action)
        {
            case "info":
                if (_assets?.KeepWindows?.Credits is not null)
                    _outsideBar.ShowCredits();
                return;
            case "load": // s158: the Amiga asks first (0xb1f0, window 1 with 36); s160: through P3ConfirmQuestion
                LoadOrAsk(s);
                return;
            case "save": // 0xba9a: the same question as the street's S
                RunTownChoice(s, TownChoice.SaveGame, shopper);
                return;
            case "armoury": _townBarKey = Keys.A; return;
            case "bank": _townBarKey = Keys.B; return;
            case "inn": Go(TownChoice.Inn); return;
            case "mystic": Go(TownChoice.Mystic); return;
            case "exit": Go(TownChoice.Exit); return;
            case "cast": Go(TownChoice.CastSpell); return;
            case "distribute": Go(TownChoice.Distribute); return;
            case "trade" when s.CanTrade: Go(TownChoice.Trade); return;
            case "use": Go(TownChoice.UseItem); return;
            case "view": Go(TownChoice.ShowParty); return;
            case "add" or "drop" or "examine" or "new" or "purge" or "rename" or "learn" or "training":
                void Enter()
                {
                    RunTownChoice(s, TownChoice.Guild, shopper);
                    RunGuildBarAction(s, action);
                }
                if (!StartDoorFlash(TownDoor(TownChoice.Guild), Enter))
                    Enter();
                return;
        }
    }

    /// <summary>The pack's window 1 with the OK / CANCEL art (the dungeon keep window's box), or null.</summary>
    private KeepWindowsLayout.Window? ConfirmWindowLayout() =>
        _assets?.KeepWindows?.Dungeon is { } w && _assets.Get(PictureNames.Button("ok")) is not null
        && _assets.Get(PictureNames.Button("cancel")) is not null ? w : null;

    /// <summary>A Guild item of the town bar, once the party is in the Guild: the Guild key's dialog.</summary>
    private void RunGuildBarAction(GameSession s, string action)
    {
        if (s.Mode != GameMode.Guild)
            return;
        var refused = action switch
        {
            "add" => _guildAddDialog.Open(s),
            "drop" => _guildDropDialog.Open(s),
            "new" => _guildNewDialog.Open(s),
            "purge" => _guildPickDialog.OpenPurge(s),
            "rename" => _guildPickDialog.OpenRename(s),
            "examine" => _guildPickDialog.OpenExamine(s, member => _inspectDialog.OpenSheet(member)), // s158: any roster member
            "learn" => _guildWhoDialog.Open(s, GuildPartyPickDialog.LearnTitle, (ss, m) => _learnDialog.Open(ss, m) ?? ""),
            "training" => _guildWhoDialog.OpenTraining(s),
            _ => null,
        };
        _guildFromBar = true; // s158: the Amiga runs the item from the street (0xb1cc) and stays there
        if (refused is not null)
        {
            SetFeedback(refused, HelpForMode(s.Mode));
            _messageBand.Restart();
            OfferAmigaMessage(refused);
        }
        else if (OpenModal is { } opened)
            SetFeedback(_message, opened.Help);
    }

    /// <summary>
    /// s158: once a Guild item taken from the town bar is over (its dialog closed, any window-7 refusal answered), the party
    /// leaves the remaster's Guild screen for the street, where the Amiga's town dispatch (<c>0xb1cc</c>) never left; the
    /// item's last line stays on the band. A Guild entered any other way is left alone.
    /// </summary>
    private void ReturnFromBarGuild(GameSession s)
    {
        if (!_guildFromBar)
            return;
        if (s.Mode != GameMode.Guild)
        {
            _guildFromBar = false;
            return;
        }
        if (OpenModal is not null || CurrentPrompt is not null || _amigaMessage.Shown || _doorFlash is not null)
            return;
        _guildFromBar = false;
        s.LeaveGuild();
        RefreshTownStock();
        SetFeedback(_message, HelpForMode(s.Mode));
    }

    /// <summary>The mouse on the town bar (<see cref="UiOverlays.DrawTownMenuBar"/>'s regions).</summary>
    private void TownBarMouse(object? tag, bool clicked)
    {
        if (_session is not { } s || TownBarPlaces(s) is not { } bar || !TownBarTakesInput(s))
            return;
        switch (tag)
        {
            case TownMenuTitleTag title when clicked:
                _townBar.Toggle(bar, title.Menu);
                break;
            case TownMenuTitleTag:
                _townBar.Hover(bar, -1);
                break;
            case TownMenuItemTag item:
                _townBar.Hover(bar, item.Item);
                if (clicked)
                {
                    var action = _townBar.Take(bar, item.Item);
                    if (action.Length > 0)
                        RunTownBarAction(s, action);
                }
                break;
            case TownMenuBackdropTag:
                if (clicked)
                    _townBar.Close();
                else
                    _townBar.Hover(bar, -1);
                break;
        }
    }
}
