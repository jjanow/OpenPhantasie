using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>What the IV screen asks <see cref="Game1"/> to do (saves and leaving go through the shell).</summary>
public enum P4ScreenRequest
{
    Save,
    Load,
    MainMenu,

    /// <summary>s158: <c>_acend(1)</c>, Back to the title screen: IV's title splash with its <c>_disk(0)</c> menu.</summary>
    Title,
}

/// <summary>
/// Phantasie IV's game screen (T6.3): one <see cref="Phantasie4Campaign"/> in whichever loop runs — location map, outdoors
/// or town — and <see cref="Phantasie4Campaign.Follow"/> after every key. Dungeon keys answer whatever
/// <see cref="P4Dungeon.Prompt"/> waits for: the eight move buttons on the numeric pad (<c>_bdng</c>; arrows for the four
/// straight ones, Home / PgUp / End / PgDn for the diagonals), E Examine, Space / Enter for the message box, digits or the
/// cursor for menus and the member pick, typed text for "Letter&gt;" / "Number&gt;". Outdoors the same pad moves (<c>_omove</c>
/// 0–7), M toggles the map, Y / N answer questions; in town digits or the cursor pick a door and 0–3 set the shares. A shell
/// state like <see cref="MainMenuScreen"/>, not a <see cref="GameMode"/>: IV has no <see cref="GameSession"/>.
/// <para>
/// In town the X68000 pack's pictures show beside the text (s107, <see cref="SceneName"/>: the street, the buildings, the
/// Hall). On a location map the pack's overhead view (s108, <c>P4DungeonScreen.View3D.cs</c>), in a fight the pack's
/// field and figures (<c>P4DungeonScreen.BattleArt.cs</c>); the 7 × 7 fight (<see cref="Phantasie4Campaign.Battle"/>) keys
/// are in <c>P4DungeonScreen.Battle.cs</c>. Outdoors, and without a pack, a placeholder look: coloured squares. A campaign
/// made without battle data keeps the old preview: Enter skips the fight.
/// </para>
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>Squares drawn each side of the party.</summary>
    public const int ViewRadius = 6;

    private string _typed = "";

    public P4DungeonScreen(Phantasie4Campaign game)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        Game.Town.HallTransferSource = ReadGameRoster;
        Game.Follow();
    }

    /// <summary>
    /// T5, the Hall's transfer: <paramref name="game"/>'s Guild roster from that game's save (<c>savegame-p3.json</c>, and with
    /// <c>p4.transfer-games</c> = <c>all</c> also <c>savegame-p1.json</c> / <c>-p2.json</c>); null when there is none or it
    /// cannot be read (the original's "not III's player disk").
    /// </summary>
    public static IReadOnlyList<Character?>? ReadGameRoster(GameId game)
    {
        var path = ClientSettings.SavePathFor(game);
        if (!File.Exists(path))
            return null;
        try
        {
            return OpenPhantasie.Data.TransferSource.ReadRoster(path, game);
        }
        catch (Exception) // an old, damaged or other game's save is no roster; it must not take the shell down
        {
            return null;
        }
    }

    public Phantasie4Campaign Game { get; private set; }

    /// <summary>Menu line or member under the cursor (0-based).</summary>
    public int Cursor { get; private set; }

    /// <summary>Puts the cursor on list line <paramref name="line"/> (a Command panel icon whose line has no digit key; Return then picks it).</summary>
    public void AimAt(int line) => Cursor = Math.Max(0, line);

    /// <summary>A shell message (saved, load failed) shown instead of the help line until the next key.</summary>
    public string? Note { get; set; }

    /// <summary>The text or number typed so far.</summary>
    public string Typed => _typed;

    private P4Dungeon D => Game.Dungeon;

    /// <summary>A loaded save replaces the game on screen.</summary>
    public void Replace(Phantasie4Campaign game)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        Game.Town.HallTransferSource = ReadGameRoster;
        Cursor = 0;
        _typed = "";
        ResetBattleUi();
        Game.Follow();
    }

    /// <summary>The numeric-pad digit of each move button (<c>_bdng</c> order: button = digit − 1).</summary>
    public static int? ButtonOf(Keys key) => key switch
    {
        Keys.NumPad1 or Keys.End => 0,
        Keys.NumPad2 or Keys.Down => 1,
        Keys.NumPad3 or Keys.PageDown => 2,
        Keys.NumPad4 or Keys.Left => 3,
        Keys.NumPad6 or Keys.Right => 5,
        Keys.NumPad7 or Keys.Home => 6,
        Keys.NumPad8 or Keys.Up => 7,
        Keys.NumPad9 or Keys.PageUp => 8,
        _ => null,
    };

    /// <summary>Handles this frame's new keys; returns what the shell must do, or null.</summary>
    public P4ScreenRequest? HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (DoorOpen || MysticBusy || WelcomeUp)
            return null; // _animdoor's busy wait, the Mystic's counted loops and the street's load read no keys
        foreach (var key in newKeys)
        {
            Note = null;
            switch (key)
            {
                case Keys.F5 when Game.Battle is not null:
                    Note = UiText.S("p4.battle.no_save");
                    continue;
                case Keys.F5 when Game.SaveRefusedMidEvent:
                    Note = UiText.S("p4.dungeon.no_save_event");
                    continue;
                case Keys.F5 when !Game.CanSave:
                    Note = UiText.S("p4.town.no_save_inside");
                    continue;
                case Keys.F5:
                    return P4ScreenRequest.Save;
                case Keys.F9:
                    return P4ScreenRequest.Load;
                case Keys.Escape:
                    return P4ScreenRequest.MainMenu;
            }
            if (!Game.LoopBuilt)
                continue;
            // s158: the final screen's save question (_endstory's _confirm): Y / O saves (_disk(1): the shell's save, while
            // the prompt is still the final screen's), then on either answer the second beep and _acgamend(1).
            if (_system is null && Game.Battle is null && Game.Place == P4Place.Town && W.Prompt == P4TownPrompt.Ending
                && W.EndingStep == P4EndingStep.Question)
            {
                if (key is not (Keys.Y or Keys.O or Keys.N or Keys.C))
                    continue;
                W.AnswerEnding();
                OpenSelectGameFinal();
                if (key is Keys.Y or Keys.O)
                    return P4ScreenRequest.Save;
                continue;
            }
            // s158: "The party has been wiped out.": _deadmsg waits for any key (_mgetc), then _acgamend(1).
            if (_system is null && Game.Battle is null && Game.PartyLost)
            {
                OpenSelectGameFinal();
                continue;
            }
            if (_system is not null && Game.Battle is null)
            {
                if (SystemKey(key) is { } request)
                    return request;
                continue;
            }
            if (Game.Battle is { } battle)
            {
                BattleKey(battle, key);
                Game.Follow();
                if (Game.Battle is null)
                    ResetBattleUi();
                continue;
            }
            if (MenuOpen)
            {
                MenusKey(key);
                Game.Follow();
                continue;
            }
            switch (Game.Place)
            {
                case P4Place.Outdoors:
                    OutdoorKey(key);
                    break;
                case P4Place.Town:
                    TownKey(key);
                    break;
                default:
                    DungeonKey(key);
                    break;
            }
            Game.Follow();
        }
        return null;
    }

    private P4OutdoorLoop O => Game.Outdoor;

    private P4TownLoop W => Game.Town;

    /// <summary>The shares being entered for the town's share-out (one per member, 0–3).</summary>
    public IReadOnlyList<int> Shares => _shares;

    private int[] _shares = [];

    /// <summary>The outdoor direction (<c>_omove</c> 0–7) of a key: the dungeon's pad buttons without the middle one.</summary>
    public static int? DirectionOf(Keys key) => ButtonOf(key) is int b ? (b > 4 ? b - 1 : b) : null;

    private void OutdoorKey(Keys key)
    {
        switch (O.Prompt)
        {
            case P4OutdoorPrompt.Action:
                if (DirectionOf(key) is int d)
                    O.Move(d);
                else if (key == Keys.M)
                    O.ToggleMap();
                else
                    CommandKey(key);
                break;
            case P4OutdoorPrompt.Wait when key is Keys.Space or Keys.Enter:
                O.Continue();
                break;
            case P4OutdoorPrompt.Confirm when key is Keys.Y or Keys.N:
                O.Answer(key == Keys.Y);
                break;
            case P4OutdoorPrompt.Fight when key == Keys.Enter:
                // Preview only: the 7 × 7 field is not built, so the fight ends as if won, with no spoils and no losses.
                O.FightOver(P4FightOutcome.Won);
                break;
        }
    }

    private void TownKey(Keys key)
    {
        if (TownScreenKey(key))
            return;
        switch (W.Prompt)
        {
            case P4TownPrompt.Menu:
                if (!TownCommandKey(key))
                    Pick(key, W.Buildings.Count, n => EnterFromStreet(W.Buildings[n]));
                break;
            case P4TownPrompt.Wait when W.TreasuryLines is not null && key is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown:
                TreasuryScroll.ScrollBy(key switch
                {
                    Keys.Up => -1,
                    Keys.Down => 1,
                    Keys.PageUp => -TreasuryScroll.Visible,
                    _ => TreasuryScroll.Visible,
                });
                break;
            case P4TownPrompt.Wait when key == Keys.L && W.CanLeaveHistory:
                W.LeaveHistory(); // _hhist's L: Done
                break;
            case P4TownPrompt.Wait when key is Keys.Space or Keys.Enter:
                W.Continue();
                break;
            case P4TownPrompt.Confirm when key is Keys.Y or Keys.N:
                W.Answer(key == Keys.Y);
                break;
            case P4TownPrompt.Shares:
                ShareKey(key);
                break;
        }
    }

    /// <summary>
    /// s165: the shares as the original asks them (<c>_deposit</c> 0x4A8E, <c>_gatebank</c> 0x4FEC): member by member in
    /// party order, one key (or share button) each; the share is written on the member's row (x 196 / 164; <c>_wloc</c>
    /// only places the text, no cursor is drawn) and the next member's row comes up; after the last member the shares go
    /// in. The "0" button is off for a member in good health (its enable field = condition ≠ Okay), so 0 does nothing
    /// for him. Returns the shares given so far, and whether they are complete.
    /// </summary>
    public static (List<int> Shares, bool Done) GiveShare(IReadOnlyList<int> given, IReadOnlyList<P4Member> members, int share)
    {
        ArgumentNullException.ThrowIfNull(given);
        ArgumentNullException.ThrowIfNull(members);
        var list = given.Take(members.Count).ToList();
        if (list.Count < members.Count && share is >= 0 and <= 3
            && (share > 0 || members[list.Count].Condition != P4Member.Okay))
            list.Add(share);
        return (list, list.Count == members.Count && members.Count > 0);
    }

    /// <summary>The text column's line for member <paramref name="i"/> while the shares are asked: his share once given,
    /// "?" while it is his turn, nothing after.</summary>
    private string ShareLine(int i) =>
        $"{Game.Party.Members[i].Name,-10} {(i < _shares.Length ? _shares[i].ToString(System.Globalization.CultureInfo.InvariantCulture) : i == _shares.Length ? "?" : "")}";

    private void ShareKey(Keys key)
    {
        int? share = key is >= Keys.D0 and <= Keys.D3 ? key - Keys.D0
            : key is >= Keys.NumPad0 and <= Keys.NumPad3 ? key - Keys.NumPad0 : null;
        if (share is not int s)
            return;
        var (list, done) = GiveShare(_shares, Game.Party.Members, s);
        _shares = [.. list];
        Cursor = Math.Min(list.Count, Math.Max(0, Game.Party.Members.Count - 1));
        if (!done)
            return;
        W.EnterShares(_shares);
        _shares = [];
        Cursor = 0;
    }

    private void DungeonKey(Keys key)
    {
        {
            switch (D.Prompt)
            {
                case P4DungeonPrompt.Action:
                    if (ButtonOf(key) is int b)
                        D.Move(b);
                    else if (key == Keys.E)
                        D.Act(P4DungeonCommand.Examine);
                    else
                        CommandKey(key);
                    break;
                case P4DungeonPrompt.Wait when key is Keys.Space or Keys.Enter:
                    D.Continue();
                    break;
                case P4DungeonPrompt.Menu:
                    Pick(key, D.MenuOptions.Count, n => D.Choose(n + 1));
                    break;
                case P4DungeonPrompt.Who:
                    Pick(key, Game.Party.Members.Count, D.Choose);
                    break;
                case P4DungeonPrompt.Letters or P4DungeonPrompt.Number:
                    Type(key);
                    break;
                case P4DungeonPrompt.Fight when key == Keys.Enter:
                    // Preview only: the 7 × 7 field is not built, so the fight is left unresolved (no rewards, no losses).
                    D.FightOver(partyLost: false);
                    break;
            }
        }
    }

    private void Pick(Keys key, int count, Action<int> choose)
    {
        if (key is >= Keys.D1 and <= Keys.D9 && key - Keys.D1 < count)
            Choose(key - Keys.D1);
        else if (key is >= Keys.NumPad1 and <= Keys.NumPad9 && key - Keys.NumPad1 < count)
            Choose(key - Keys.NumPad1);
        else if (key == Keys.Up)
            Cursor = (Cursor + count - 1) % count;
        else if (key == Keys.Down)
            Cursor = (Cursor + 1) % count;
        else if (key is Keys.Enter or Keys.Space)
            Choose(Cursor);

        void Choose(int n)
        {
            Cursor = 0;
            choose(n);
        }
    }

    private void Type(Keys key)
    {
        if (key == Keys.Back && _typed.Length > 0)
            _typed = _typed[..^1];
        else if (key == Keys.Enter)
        {
            var text = _typed;
            _typed = "";
            if (D.Prompt == P4DungeonPrompt.Letters)
                D.EnterText(text);
            else
                D.EnterNumber(int.TryParse(text, out var n) ? n : 0);
        }
    }

    /// <summary>A typed character (the window's text input): letters for "Letter&gt;", digits for "Number&gt;".</summary>
    public void OnTextInput(char c)
    {
        if (!Game.LoopBuilt || TownTextInput(c) || Game.Place != P4Place.Dungeon)
            return;
        if (D.Prompt == P4DungeonPrompt.Letters && _typed.Length < 10 && c >= ' ' && c < 0x7f)
            _typed += c;
        else if (D.Prompt == P4DungeonPrompt.Number && _typed.Length < 5 && char.IsAsciiDigit(c))
            _typed += c;
    }

    // --- What is shown ------------------------------------------------------------------------------------------------

    /// <summary>The message box: title, the dungeon's lines, then the prompt's own lines.</summary>
    public IReadOnlyList<MenuLine> BoxLines()
    {
        var lines = new List<MenuLine>();
        if (Game.Battle is { } battle)
            return BattleLines(battle);
        if (!Game.LoopBuilt)
        {
            var at = Game.Location;
            lines.Add(new(UiText.S("p4.dungeon.left_title"), MenuLineStyle.Title));
            lines.Add(new(at.Place == P4Place.Town
                ? UiText.F("p4.dungeon.left_town_x", at.Map)
                : UiText.F("p4.dungeon.left_outdoors_x_y_z", at.Map, at.X, at.Y), MenuLineStyle.Normal));
            lines.Add(new(UiText.S("p4.dungeon.loop_not_built"), MenuLineStyle.Dim));
            return lines;
        }
        if (MenuOpen)
            return MenuLines();
        if (_system is not null)
            return SystemLines();
        if (Game.Place == P4Place.Outdoors)
            return OutdoorLines();
        if (Game.Place == P4Place.Town && MysticBusy)
            // _mystic: nothing to read during the gaze; the score and verdict come with the bubble, the key help with the wait.
            return _mystic!.BubbleUp ? [.. W.Box.Where(l => l.Length > 0).Select(l => new MenuLine(l, MenuLineStyle.Normal))] : [];
        if (Game.Place == P4Place.Town)
            return W.InBuilding || W.Prompt is P4TownPrompt.Pick or P4TownPrompt.Who or P4TownPrompt.Number
                or P4TownPrompt.Text ? TownScreenLines() : TownLines();
        if (D.BoxTitle is { Length: > 0 } title)
            lines.Add(new(title, MenuLineStyle.Title));
        foreach (var l in D.Box)
            foreach (var part in l.Split('\n'))
                lines.Add(new(part, MenuLineStyle.Normal));
        switch (D.Prompt)
        {
            case P4DungeonPrompt.Menu:
                for (var i = 0; i < D.MenuOptions.Count; i++)
                    lines.Add(Option(i, $"{i + 1} {D.MenuOptions[i]}"));
                break;
            case P4DungeonPrompt.Who:
                lines.Add(new(UiText.S("p4.dungeon.who"), MenuLineStyle.Header));
                for (var i = 0; i < Game.Party.Members.Count; i++)
                    lines.Add(Option(i, $"{i + 1} {Game.Party.Members[i].Name}"));
                break;
            case P4DungeonPrompt.Letters when !TypedInBand: // s151: else in the colour-14 band (DrawEventSlot)
                lines.Add(new(UiText.F("p4.dungeon.letters_x", _typed), MenuLineStyle.Selected));
                break;
            case P4DungeonPrompt.Number when !TypedInBand:
                lines.Add(new(UiText.F("p4.dungeon.number_x", _typed), MenuLineStyle.Selected));
                break;
            case P4DungeonPrompt.Fight:
                lines.Add(new(UiText.F("p4.dungeon.fight_x", D.PendingFight?.Encounter ?? 0), MenuLineStyle.Header));
                lines.Add(new(UiText.S("p4.dungeon.combat_not_built"), MenuLineStyle.Dim));
                break;
        }
        return lines;
    }

    private List<MenuLine> OutdoorLines()
    {
        var lines = new List<MenuLine>
        {
            new(UiText.F("p4.outdoor.title_x_y_z_w", O.Map, O.X, O.Y, Game.Party.DaysOut), MenuLineStyle.Title),
        };
        if (Game.Party.Ship == P4ShipState.Aboard)
            lines.Add(new(UiText.S("p4.outdoor.aboard"), MenuLineStyle.Dim));
        if (!InnWindowUp) // s165: the outdoor inn's rows are in _inn's window under its picture
            foreach (var l in O.Box)
                lines.Add(new(l, MenuLineStyle.Normal));
        switch (O.Prompt)
        {
            case P4OutdoorPrompt.Confirm:
                lines.Add(new(UiText.S("p4.outdoor.yes_no"), MenuLineStyle.Selected));
                break;
            case P4OutdoorPrompt.Fight:
                lines.Add(new(UiText.S("p4.outdoor.fight"), MenuLineStyle.Header));
                lines.Add(new(UiText.S("p4.outdoor.combat_not_built"), MenuLineStyle.Dim));
                break;
        }
        return lines;
    }

    /// <summary>The buildings' names (remaster text; the original shows the doors as pictures).</summary>
    public static string BuildingName(P4Building b) => UiText.S(BuildingKey(b));

    /// <summary>The string id of a building's name: <c>p4.town.&lt;building&gt;</c>.</summary>
    public static string BuildingKey(P4Building b) => "p4.town." + b.ToString().ToLowerInvariant();

    private List<MenuLine> TownLines()
    {
        var lines = new List<MenuLine> { new(W.Site?.Name ?? "", MenuLineStyle.Title) };
        lines.Add(new(TownGoldLine(), MenuLineStyle.Dim));
        // s151: the shipwright's lines are in his window under the picture; s163: the inn's, the bank's and the ending
        // pages' in theirs.
        if (!ShipwrightWindowUp && !MessagesInWindow)
            foreach (var l in W.Box)
                lines.Add(new(l, MenuLineStyle.Normal));
        switch (W.Prompt)
        {
            case P4TownPrompt.Menu:
                for (var i = 0; i < W.Buildings.Count; i++)
                {
                    var b = W.Buildings[i];
                    var text = $"{i + 1} {BuildingName(b)}" + (P4TownLoop.IsBuilt(b) ? "" : " " + UiText.S("p4.town.not_built_mark"));
                    lines.Add(Option(i, text));
                }
                break;
            case P4TownPrompt.Shares:
                lines.Add(new(W.SharesFor == P4ShareKind.Gold
                    ? UiText.F("p4.town.share_gold_x", W.ShareAmount)
                    : UiText.F("p4.town.share_xp_x", W.ShareAmount), MenuLineStyle.Header));
                var n = Game.Party.Members.Count;
                for (var i = 0; i < n; i++)
                    lines.Add(Option(i, ShareLine(i)));
                break;
            case P4TownPrompt.Confirm:
                lines.Add(new(UiText.S("p4.outdoor.yes_no"), MenuLineStyle.Selected));
                break;
            case P4TownPrompt.Ending when W.EndingStep == P4EndingStep.Question:
                lines.Add(new(UiText.S("p4.outdoor.yes_no"), MenuLineStyle.Selected)); // s158: _endstory's _confirm
                break;
            case P4TownPrompt.Ending when W.EndingStep == P4EndingStep.None:
                lines.Add(new(Game.Services is null ? UiText.S("p4.town.ending_not_built") : UiText.S("p4.town.game_over"),
                    MenuLineStyle.Dim));
                break;
        }
        return lines;
    }

    private MenuLine Option(int i, string text) =>
        i == Cursor ? new(">" + text, MenuLineStyle.Selected) : new(" " + text, MenuLineStyle.Normal);

    /// <summary>The bottom help line for the current prompt.</summary>
    public string Help => Game.Battle is { } b ? BattleHelp(b)
        : DoorOpen || MysticBusy || WelcomeUp ? "" // no keys are read: no help to give
        : MenuOpen ? M!.Prompt switch
        {
            P4MenuPrompt.Wait => UiText.S("p4.dungeon.help_wait"),
            P4MenuPrompt.Confirm => UiText.S("p4.outdoor.help_confirm"),
            _ => UiText.S("p4.town.help_screen"),
        }
        : !Game.LoopBuilt
        ? UiText.S("p4.dungeon.help_left")
        : _system is not null ? SystemHelp
        : Game.Place == P4Place.Outdoors ? O.Prompt switch
        {
            P4OutdoorPrompt.Action => UiText.S("p4.outdoor.help_move"),
            P4OutdoorPrompt.Wait => UiText.S("p4.dungeon.help_wait"),
            P4OutdoorPrompt.Confirm => UiText.S("p4.outdoor.help_confirm"),
            P4OutdoorPrompt.Fight => UiText.S("p4.dungeon.help_fight"),
            _ => UiText.S("p4.dungeon.help_left"),
        }
        : Game.Place == P4Place.Town ? W.Prompt switch
        {
            P4TownPrompt.Menu => UiText.S("p4.town.help_menu"),
            P4TownPrompt.Pick or P4TownPrompt.Who => UiText.S("p4.town.help_screen"),
            P4TownPrompt.Number or P4TownPrompt.Text => UiText.S("p4.dungeon.help_type"),
            P4TownPrompt.Wait when W.CanLeaveHistory => UiText.S("p4.hall.help_history"),
            P4TownPrompt.Wait => UiText.S("p4.dungeon.help_wait"),
            P4TownPrompt.Confirm => UiText.S("p4.outdoor.help_confirm"),
            P4TownPrompt.Shares => UiText.S("p4.town.help_shares_each"),
            P4TownPrompt.Ending when W.EndingStep == P4EndingStep.Question => UiText.S("p4.town.help_save_question"),
            _ => UiText.S("p4.dungeon.help_left"),
        }
        : D.Prompt switch
        {
            P4DungeonPrompt.Action => UiText.S("p4.dungeon.help_move"),
            P4DungeonPrompt.Wait => UiText.S("p4.dungeon.help_wait"),
            P4DungeonPrompt.Menu or P4DungeonPrompt.Who => UiText.S("p4.dungeon.help_pick"),
            P4DungeonPrompt.Letters or P4DungeonPrompt.Number => UiText.S("p4.dungeon.help_type"),
            P4DungeonPrompt.Fight => UiText.S("p4.dungeon.help_fight"),
            _ => UiText.S("p4.dungeon.help_left"),
        };

    /// <summary>
    /// One line per member: name, HP, MP, and the condition when not Okay. Under the Guild treasury the outdoor / dungeon
    /// panel adds the gold line (spendable gold and the trip gold, <see cref="TreasuryReport.GoldLine"/>).
    /// </summary>
    public IReadOnlyList<string> PartyLines() => Game.Battle is { } b ? BattlePartyLines(b) :
        [.. Game.Party.Members.Select(m => UiText.F("p4.dungeon.member_x_y_z_w_v", m.Name, m.Hp, m.HpMax, m.Mp, m.MpMax)
            + m.Condition switch
            {
                P4Member.Okay => "",
                P4Member.Dead => " " + UiText.S("p4.dungeon.dead"),
                P4Member.Asleep => " " + UiText.S("p4.dungeon.asleep"),
                var n => " " + UiText.F("p4.dungeon.condition_x", n), // the other conditions' names come with their loops
            }),
            .. Game.Options.TreasuryOn && Game.Place != P4Place.Town
                ? [TreasuryReport.GoldLine(Game.Party.SpendableGold(Game.Options), Game.Party.Gold)] : Array.Empty<string>()];

    /// <summary>
    /// The town lines' gold: the purse (the original's), or under the Guild treasury the spendable gold
    /// (<see cref="TreasuryReport.GoldLine"/>; in town the purse is swept, so no trip gold shows).
    /// </summary>
    private string TownGoldLine() => Game.Options.TreasuryOn
        ? TreasuryReport.GoldLine(Game.Party.SpendableGold(Game.Options), 0)
        : UiText.F("p4.town.gold_x", Game.Party.Gold);

    // --- Drawing (320 × 200 layout) -----------------------------------------------------------------------------------

    private const int Cell = 9;
    private static readonly Color Back = new(10, 14, 24), Panel = new(16, 22, 36), Edge = new(90, 110, 150);
    private static readonly Color Floor = new(40, 46, 60), Blocked = new(24, 26, 32), Water = new(40, 70, 130);
    private static readonly Color Wall = new(200, 200, 210), Door = new(220, 170, 60), Sealed = new(190, 70, 60);
    private static readonly Color Party = new(120, 200, 140), Title = new(220, 200, 120), Text = new(190, 200, 220);
    private static readonly Color Dim = new(130, 145, 170), Selected = new(120, 200, 140);

    /// <summary>IV's graphics pack (T6.4), or null: without it (or a scene it lacks) the screen draws its placeholder look.</summary>
    public AssetCatalog? Assets { get; set; }

    /// <summary>
    /// The pack scene the original shows now (<see cref="P4TownLoop.Scene"/>; <c>FORMAT.md</c> §6.9), or null: only in town,
    /// outside fights and the party menus (<c>_detail</c> and the other menus clear the picture). s140: outdoors the view
    /// stays up under a party menu (<c>_inspect</c> skips <c>_clrtdraw</c> there and <c>_ocast</c> redraws it with
    /// <c>_opts1</c>), so the menu's panel has its strip; where the original covers it with its own window (U's item
    /// window, P's and the profile's window 35) the remaster still shows the view `[~]`.
    /// </summary>
    public string? SceneName => Game.Battle is not null || !Game.LoopBuilt ? null
        : MenuOpen ? (M!.ReadingScroll ? "p4.scroll" : Game.Place == P4Place.Outdoors ? OutdoorScene : null)
        // s165: an outdoor inn (_oinn → _inn) opens INN.PKH's window over the map through _inn's SPACE wait.
        : Game.Place == P4Place.Outdoors ? (O.InnRows is not null ? InnScene : OutdoorScene)
        : Game.Place != P4Place.Town ? null
        : W.Scene switch
        {
            P4TownScene.Street when W.Site is { } site => $"p4.street.{site.Type}",
            P4TownScene.Guild => "p4.guild",
            P4TownScene.Inn => InnScene,
            P4TownScene.Bank => "p4.bank",
            P4TownScene.Shipwright => "p4.shipwright",
            P4TownScene.Mystic => "p4.mystic",
            P4TownScene.Hall => "p4.hall",
            P4TownScene.HallHistory => $"p4.hall.history.{W.HallHistoryPage}",
            P4TownScene.EndingStreet => "p4.ending.street",
            P4TownScene.EndingBanquet => "p4.ending.banquet",
            P4TownScene.EndingCell => "p4.ending.cell",
            _ => null,
        };

    /// <summary>
    /// s162: the titled window (the pack's <c>titled</c> key, P4.X's function) the original opens round scene
    /// <paramref name="scene"/>'s picture (FORMAT.md 6.9 "Titled building windows"), or null: the street and the ending's
    /// street are untitled (<c>_tw_open</c>), the shipwright has its own entry, the outdoors none.
    /// </summary>
    public static string? TitledWindowName(string? scene) => scene switch
    {
        "p4.guild" => "_guildmenu",
        InnScene => "_inn",
        "p4.bank" => "_bank",
        "p4.mystic" => "_mystic",
        HallScene => "_hall",
        "p4.ending.banquet" => "_banquet",
        "p4.ending.cell" => "_arathamus",
        { } s when s.StartsWith("p4.hall.history.", StringComparison.Ordinal) => "_hall", // _hhist draws in the Hall's window
        _ => null,
    };

    /// <summary>The outdoor view's name: <see cref="OutdoorOverview"/> with M, else <see cref="OutdoorCloseUp"/>.</summary>
    private string OutdoorScene => O.MapOverview ? OutdoorOverview : OutdoorCloseUp;

    public const string OutdoorCloseUp = "p4.outdoor.map", OutdoorOverview = "p4.outdoor.overview";

    /// <summary>The pack's picture of outdoor map <paramref name="map"/> (OT01–23, 104 × 104: 4 pack pixels a square).</summary>
    public static string OutdoorPicture(int map) => $"p4.outdoor.{map}";

    /// <summary>Pack pixels per outdoor square: 8 in the close-up (the picture drawn at 2×, <c>_scdisd2</c>), 4 on the overview.</summary>
    private int OutdoorSquare => O.MapOverview ? 4 : 8;

    /// <summary>
    /// The outdoor map as P4.X draws it (<c>_omap_load</c>, <c>FORMAT.md</c> §6.9): the close-up is the map's picture at 2×
    /// (208 × 208), the overview the block's nine pictures at 1× (312 × 312). Null without the pictures.
    /// </summary>
    private LoadedScene? OutdoorSceneParts()
    {
        if (Assets is null || O.MapData is null)
            return null;
        if (!O.MapOverview)
            return Assets.ComposeScene([(OutdoorPicture(O.Map), 0, 0, 2)]);
        var maps = O.OverviewMaps;
        // D4-072 centred: a cell past the world's edge (−1) has no picture and stays black.
        return Assets.ComposeScene(maps.Select((m, k) => (m, k)).Where(c => c.m >= 0)
            .Select(c => (OutdoorPicture(c.m), c.k % 3 * 104, c.k / 3 * 104, 1)), 3 * 104, 3 * 104);
    }

    /// <summary>Top-left of map <paramref name="map"/>'s square (x, y) in the outdoor scene (pack pixels), or null when not in view.</summary>
    private Point? OutdoorSquareAt(int map, int x, int y)
    {
        var s = OutdoorSquare;
        if (!O.MapOverview)
            return map == O.Map ? new Point(x * s, y * s) : null;
        var k = O.OverviewCellOf(map);
        return k < 0 ? null : new Point(k % 3 * 104 + x * s, k / 3 * 104 + y * s);
    }

    /// <summary>
    /// The marks over the outdoor picture (<c>_omark</c>): the party a black square of one map square with a white one 1 pixel
    /// inside (<c>_fbox</c> colours 0 and 15) [C]; its exact offset on the picture [~]. Then the ship (<c>_oship_mark</c>,
    /// s135 [C]), only while the party is on the ship's map: <c>_xbox(8, …)</c> XORs every pixel of the ship's square with 8,
    /// i.e. the picture's own colours with palette index bit 3 flipped (<see cref="ShipMarkColours"/>); aboard, the ship's
    /// square is the party's, so the party square turns colours 8 and 7 (0 ^ 8, 15 ^ 8).
    /// </summary>
    private IEnumerable<(Rectangle Box, Color Color)> OutdoorMarks()
    {
        var s = OutdoorSquare;
        var p = Game.Party;
        var black = Assets?.PaletteColour("pkh", 0) ?? Color.Black;
        var white = Assets?.PaletteColour("pkh", 15) ?? Color.White;
        var aboard = p.Ship == P4ShipState.Aboard;
        if (OutdoorSquareAt(O.Map, O.X, O.Y) is { } at)
        {
            yield return (new Rectangle(at.X, at.Y, s, s), aboard ? XorColour(black) : black);
            yield return (new Rectangle(at.X + 1, at.Y + 1, s - 2, s - 2), aboard ? XorColour(white) : white);
        }
        if (p.Ship == P4ShipState.Moored && p.ShipMap == O.Map && OutdoorSquareAt(p.ShipMap, p.ShipX, p.ShipY) is { } ship
            && Assets?.Get(OutdoorPicture(p.ShipMap)) is { } picture)
        {
            // The picture has 4 pixels a square (104 × 104); the close-up draws it at 2×.
            var source = new Rectangle(p.ShipX * 4, p.ShipY * 4, 4, 4);
            if (picture.Bounds.Contains(source))
            {
                if (_shipMark is not { } cached || cached.Picture != picture || cached.Source != source)
                {
                    // The whole picture once (the plain GetData AssetCatalog uses), then the square's 4 × 4 pixels.
                    var all = new Color[picture.Width * picture.Height];
                    picture.GetData(all);
                    var under = new Color[16];
                    for (var i = 0; i < 16; i++)
                        under[i] = all[(source.Y + i / 4) * picture.Width + source.X + i % 4];
                    _shipMark = cached = (picture, source, ShipMarkColours(under, PkhPalette()));
                }
                var marked = cached.Colours;
                var px = s / 4;
                for (var i = 0; i < marked.Length; i++)
                    yield return (new Rectangle(ship.X + i % 4 * px, ship.Y + i / 4 * px, px, px), marked[i]);
            }
        }
    }

    private (Texture2D Picture, Rectangle Source, Color[] Colours)? _shipMark;

    private Color XorColour(Color c) => ShipMarkColours([c], PkhPalette())[0];

    private Color[] PkhPalette() =>
        [.. Enumerable.Range(0, 16).Select(i => Assets?.PaletteColour("pkh", i) ?? Color.Transparent)];

    /// <summary>
    /// <c>_xbox2</c>'s XOR 8 on picture colours: each colour that is palette entry i becomes entry i ^ 8; any other colour
    /// (not the picture's palette) is left as it is.
    /// </summary>
    public static Color[] ShipMarkColours(IReadOnlyList<Color> pixels, IReadOnlyList<Color> palette)
    {
        var result = new Color[pixels.Count];
        for (var i = 0; i < pixels.Count; i++)
        {
            var c = pixels[i];
            var index = -1;
            for (var j = 0; j < palette.Count && index < 0; j++)
                if (palette[j].R == c.R && palette[j].G == c.G && palette[j].B == c.B && palette[j].A != 0)
                    index = j;
            result[i] = index >= 0 && (index ^ 8) < palette.Count ? palette[index ^ 8] : c;
        }
        return result;
    }

    public void Draw(SpriteBatch batch, Texture2D pixel)
    {
        Fill(batch, pixel, new Rectangle(0, 0, 320, 200), Back);
        if (DrawBlackedOut(batch, pixel)) // s162: _deadmsg's _xblack, its window, _xblack before _acgamend(1)
            return;
        // s156: _trang's own window 32 in the picture's place (the training hall has no picture: _trang's _clrtdraw), a
        // level-up's _sklup window in its lower part.
        // s165: _mage's window 32 is drawn the same way from the pack's "mage" (no level-up window in it).
        if (CoreTrainingWindow is { } hallUp && Assets?.PackPixelsPerLayoutPixel is > 0
            && (hallUp.Hall == P4TrainingWindow.MagicHall ? Extras?.MagicHall : Extras?.TrainingHall) is { } hall
            && (hallUp.Hall == P4TrainingWindow.MagicHall ? null : Extras!.SkillUp) is var hallSkills
            && TrainingHallArea(hall, hallUp, hallSkills, 1f / Assets.PackPixelsPerLayoutPixel) is var (hw, hh)
            && DrawTitledBeside(batch, pixel, hw, hh, PanelText(hall.Title, hall.TitleText),
                (origin, k) => DrawTrainingWindow(batch, pixel, hall, hallUp, CoreSkillWindow, hallSkills, origin, k)))
            return;
        // s152: _sklup's window 32 in the picture's place (the training hall has no picture: _trang's _clrtdraw).
        if (CoreSkillWindow is { } skills && Assets?.PackPixelsPerLayoutPixel is > 0 && Extras?.SkillUp is { } sk
            && SkillUpArea(sk) is var (skw, skh) && DrawBeside(batch, pixel, skw, skh,
                (origin, k) => DrawSkillWindow(batch, pixel, sk, skills, origin, k)))
            return;
        if (DrawFinalScreen(batch, pixel)) // s162: _endstory's own titled window
            return;
        if (DrawGateBank(batch, pixel)) // s165: _gatebank's share-out window while the entry's shares are asked / its wait
            return;
        if (DrawTownGate(batch, pixel)) // s162: _townent's Town Gate window while the aging wait is up
            return;
        if (DrawItemWindow(batch, pixel)) // s163: the Armory's, Share out's, Trade's, Use's and Identify's item windows
            return;
        if (SceneName is { } name)
        {
            var outdoor = name is OutdoorCloseUp or OutdoorOverview;
            if ((outdoor ? OutdoorSceneParts() : Assets?.GetScene(name)) is { } scene
                && DrawScene(batch, pixel, scene, outdoor ? OutdoorMarks() : []))
                return;
        }
        if (Game.Battle is { } fight ? DrawBattleArt(batch, pixel, fight)
            : Game.Place == P4Place.Dungeon && D.HasMap && DrawView3D(batch, pixel))
            return;
        // s138: a town screen with no picture (the original's _clrtdraw clears it: the roster, creation, training, the
        // Armory, Trade, Share out, the entry's waits) keeps the Command panel's strip; the text takes the picture's room.
        if (Game.Battle is null && Game.Place == P4Place.Town && PanelPlace && Assets?.Pack?.CommandPanels is not null
            && DrawBeside(batch, pixel, 0, 0, (_, _) => { }))
            return;
        var size = (2 * ViewRadius + 1) * Cell;
        var map = new Rectangle(4, 4, size, size);
        Fill(batch, pixel, map, Panel);
        if (Game.Battle is { } battle)
            DrawBattle(batch, pixel, map, battle);
        else if (Game.LoopBuilt && Game.Place == P4Place.Outdoors)
            DrawOutdoor(batch, pixel, map);
        else if (Game.Place == P4Place.Dungeon && D.HasMap)
            DrawMap(batch, pixel, map);

        var note = HelpLines(Note ?? Help, 51);
        var noteTop = 189 - (note.Count - 1) * PixelFont.LineHeight;
        var party = new Rectangle(4, map.Bottom + 4, 312, noteTop - 3 - map.Bottom - 4);

        // Every line whole (s99, user: never lose content to fit): the box's lines wrap and scroll (the selected line kept
        // in view; the wheel or the bar look further), the party lines wrap and scroll, the note takes the lines it needs.
        // The box ends above the party panel (s114: a fixed 150-pixel box ran under it, so a long shop list sat on its border).
        DrawBox(batch, pixel, BoxArea(map.Right, party.Y));
        Fill(batch, pixel, party, Panel);
        var partyCols = (party.Width - 8) / PixelFont.Advance;
        DrawRows(batch, pixel, party,
            [.. PartyLines().SelectMany(l => UiOverlays.Lines(l, partyCols)).Select(l => new BoxRow(l, Text))], PartyScroll,
            topPad: 2);
        for (var i = 0; i < note.Count; i++)
            PixelFont.Draw(batch, pixel, 6, noteTop + i * PixelFont.LineHeight, note[i], Note is null ? Dim : Title);
    }

    /// <summary>The plain screen's message box: right of the map panel, ending 4 pixels above the party panel at <paramref name="partyTop"/>.</summary>
    public static Rectangle BoxArea(int mapRight, int partyTop) =>
        new(mapRight + 4, 4, 320 - mapRight - 8, partyTop - 4 - 4);

    /// <summary>The box's lines in <paramref name="box"/>, wrapped, the selected line kept in view.</summary>
    private void DrawBox(SpriteBatch batch, Texture2D pixel, Rectangle box)
    {
        Fill(batch, pixel, box, Panel);
        Outline(batch, pixel, box, Edge);
        var rows = new List<BoxRow>();
        foreach (var line in BoxLines())
        {
            var color = line.Style switch
            {
                MenuLineStyle.Title or MenuLineStyle.Header => Title,
                MenuLineStyle.Selected => Selected,
                MenuLineStyle.Dim => Dim,
                _ => Text,
            };
            AddBoxRows(rows, line, color, box.Width - 8, VisibleRows(box.Height, 4));
        }
        // s136: the typed prompt's cursor bar after its text (the prompt is the box's last selected row).
        var cursor = CursorOn ? rows.FindLastIndex(r => r.Selected) : -1;
        DrawRows(batch, pixel, box, rows, BoxScroll, topPad: 4, follow: rows.FindIndex(r => r.Selected), cursor: cursor,
            cursorWidth: CursorWidth);
    }

    /// <summary>
    /// The picture screen (T6.4): the scene at the top left, drawn 1:1 in pack pixels (a 640 × 400 pack: half a layout
    /// pixel each), and the text in the column beside it — the box on top, the party lines and the key help under it (each
    /// wraps and scrolls; nothing is cut). False when the scene would not fit the layout (then the plain screen is drawn).
    /// </summary>
    private bool DrawScene(SpriteBatch batch, Texture2D pixel, LoadedScene scene,
        IEnumerable<(Rectangle Box, Color Color)> marks)
    {
        // s158: the shipwright's window is titled (_wopen(-1, 160, 32, 312, 352, 1, "船　大　工")): its bar and frame.
        if (ShipwrightWindowUp && Extras?.Shipwright is { } ship)
            return DrawTitledBeside(batch, pixel, scene.Width,
                Math.Max(scene.Height, ShipwrightHeight(ship, 1f / Assets!.PackPixelsPerLayoutPixel)),
                PanelText(ship.Title, ship.TitleText), (origin, k) => DrawSceneAt(batch, pixel, scene, marks, origin, k));
        // s162: the other buildings' and the ending's titled windows (_wopen(…, 1, title)): the bar and frame round the
        // picture. s163: the inn's, the bank's, the banquet's and the cell's are the original's 352 rows round a 180-row
        // picture; their grey holds the inn's rows, the bank's tables and the ending's message window
        // (P4DungeonScreen.Buildings.cs), no longer printed in the text column.
        if (TitledWindowName(SceneName) is { } key && Extras?.Titled?.GetValueOrDefault(key) is { } titled)
        {
            var height = TitledHeight(key, titled, scene.Height, 1f / Assets!.PackPixelsPerLayoutPixel);
            return DrawTitledBeside(batch, pixel, scene.Width, height, PanelText(titled.Title, titled.TitleText), (origin, k) =>
            {
                DrawUnderPicture(batch, pixel, origin, scene.Width, scene.Height, height, k);
                DrawSceneAt(batch, pixel, scene, marks, origin, k);
            });
        }
        return DrawBeside(batch, pixel, scene.Width, scene.Height,
            (origin, k) => DrawSceneAt(batch, pixel, scene, marks, origin, k));
    }

    /// <summary><see cref="DrawScene"/>'s picture (and what goes over it) with its top-left at <paramref name="origin"/>.</summary>
    private void DrawSceneAt(SpriteBatch batch, Texture2D pixel, LoadedScene scene,
        IEnumerable<(Rectangle Box, Color Color)> marks, Vector2 origin, float k)
    {
        if (WelcomeUp && BannerScene)
        {
            if (PleaseWaitUp && Assets?.Pack?.PictureAnimations?.Welcome?.PleaseWait is { } wait)
                DrawPleaseWait(batch, pixel, origin, k, scene.Width, scene.Height, wait); // s156: before "Welcome!"
            else
                DrawWelcome(batch, pixel, origin, k, scene.Width, scene.Height); // s151: _tload, before the picture
            return;
        }
        // Black under the parts: a D4-072 centred overview leaves cells past the world's edge empty.
        batch.Draw(pixel, origin, null, Color.Black, 0f, Vector2.Zero, new Vector2(scene.Width * k, scene.Height * k),
            SpriteEffects.None, 0f);
        foreach (var (texture, x, y, scale) in scene.Parts)
            batch.Draw(texture, origin + new Vector2(x * k, y * k), null, Color.White, 0f, Vector2.Zero, k * scale,
                SpriteEffects.None, 0f);
        if (SceneName == HallScene)
            DrawHallFigures(batch, origin, k); // s151: _hpix, the imports on the stage
        DrawAnimation(batch, origin, k, pixel); // s110: the frames _anima copied, the open door
        // s141: the shipwright's YES / NO box in its window, whose origin is the picture's (FORMAT.md 6.9 _noyes);
        // s151: in the window's grey under the picture, with the offer, the answer and the gold.
        if (ShipwrightWindowUp)
            DrawShipwrightWindow(batch, pixel, origin, k);
        else if (PanelName == NoYesName && Assets?.Pack?.CommandPanels?.NoYes is { } noYes)
            DrawNoYes(batch, pixel, noYes, origin, k);
        DrawBuildingWindows(batch, pixel, origin, k); // s163: the inn's rows, the bank's tables, _endmsg's window
        // Marks in pack pixels (the outdoor party square): scaled pixels, so a 1-pixel inset stays 1 pack pixel.
        foreach (var (box, color) in marks)
            batch.Draw(pixel, origin + new Vector2(box.X * k, box.Y * k), null, color, 0f, Vector2.Zero,
                new Vector2(box.Width * k, box.Height * k), SpriteEffects.None, 0f);
    }

    /// <summary>
    /// <see cref="DrawScene"/>'s layout for any picture <paramref name="width"/> × <paramref name="height"/> pack pixels:
    /// <paramref name="drawPicture"/> gets the top-left in layout pixels and the layout pixels per pack pixel; the text
    /// column goes beside it. False when the picture would not fit.
    /// </summary>
    private bool DrawBeside(SpriteBatch batch, Texture2D pixel, int width, int height, Action<Vector2, float> drawPicture) =>
        DrawBeside(batch, pixel, width, height, drawPicture, 4,
            (int)Math.Ceiling(height / (float)Assets!.PackPixelsPerLayoutPixel));

    /// <summary>
    /// A titled window (<c>_wopen</c> flag 1: the training hall's, the shipwright's) <paramref name="width"/> ×
    /// <paramref name="height"/> pack pixels in the picture's place (s158): <see cref="DrawTitledFrame"/>'s bar and frame
    /// round it; <paramref name="drawWindow"/> gets the window's own top-left. The bar takes the screen's top margin (the
    /// frame's top row at layout row 0; the original's bar reaches up to row 11 of 400), so the frame ends 4 layout rows
    /// lower than the bar alone would put it and the s157 key help still fits under it: the frame's 375 rows are 187.5
    /// layout rows, and the half row of the bottom shadow is left out of the layout's picture rect (the help keeps a
    /// 1.5-row gap). When even that does not fit (a translation's extra rows), the window is drawn untitled as before `[~]`.
    /// </summary>
    private bool DrawTitledBeside(SpriteBatch batch, Texture2D pixel, int width, int height, string title,
        Action<Vector2, float> drawWindow)
    {
        var k = 1f / Assets!.PackPixelsPerLayoutPixel;
        var (w, h) = TitledArea(width, height);
        return (title.Length > 0 && DrawBeside(batch, pixel, w, h, (origin, scale) =>
            {
                DrawTitledFrame(batch, pixel, origin, width + 2, height, title, scale);
                drawWindow(origin + new Vector2(1, PanelTitleRows) * scale, scale);
            }, 0, (int)Math.Floor(h * k)))
            || DrawBeside(batch, pixel, width, height, drawWindow);
    }

    /// <summary>A titled window's area in pack pixels (s158, <c>_wwind</c>): the frame and shadow columns, the bar, the
    /// frame and shadow rows.</summary>
    public static (int Width, int Height) TitledArea(int width, int height) => (width + 3, height + PanelTitleRows + 2);

    private bool DrawBeside(SpriteBatch batch, Texture2D pixel, int width, int height, Action<Vector2, float> drawPicture,
        int top, int layoutHeight)
    {
        var k = 1f / Assets!.PackPixelsPerLayoutPixel;
        var picture = new Rectangle(4, top, (int)Math.Ceiling(width * k), layoutHeight);
        if (picture.Bottom > 196 || picture.Right > 200)
            return false;
        drawPicture(new Vector2(picture.X, picture.Y), k);

        // s136: the original's Command panel at the right edge (FORMAT.md 6.9). Its strip is kept for the whole place (the
        // street, outdoors, a location map), so the text column does not change width with each prompt; it stays empty
        // while no read panel is up (the original's wait buttons are not read).
        var right = 316;
        var stripLeft = right;
        OpenPhantasie.Data.CommandPanel? row = null;
        if (Assets.Pack?.CommandPanels is { } overSet && PanelOverField && CurrentPanel is { Row: null } over)
        {
            // D4 found-item question (user s142, option a): the panel covers the field's right side; the text column
            // keeps its width. The message window moves left of it (FindItMessageSpan).
            DrawPanel(batch, pixel, overSet, over, new Vector2(picture.Right - (float)Math.Ceiling(PanelSize(overSet, k).X), 4), k);
        }
        else if (Assets.Pack?.CommandPanels is { } panels && (PanelPlace || FightListUp))
        {
            // s160 (user s159): in a fight the combat spell list (_getmagic(member, 0)) opens here too, right of the field
            // at the panel's place (the original's x 496); the text column gives way to it while it is open (it narrows to
            // the field's side, the key help and the party lines go under the field) and comes back when it closes.
            right = stripLeft = 316 - (int)Math.Ceiling(PanelSize(panels, k).X);
            if (CurrentPanel is { } panel)
            {
                if (panel.Row is null)
                    DrawPanel(batch, pixel, panels, panel, new Vector2(right, 4), k);
                else
                    row = panel; // s141: drawn after the box, at the sheet's lower right (RowAt)
            }
            if (CurrentList is { } shown) // s151: the spell list, Transport's towns, creation's lists
                DrawList(batch, pixel, panels, shown.List, shown.Layout, new Vector2(right, ListFrameTop(shown.Layout, k)), k);
            right -= 4;
        }
        var left = width == 0 ? 4 : picture.Right + 4;
        // s157: the key help (and the party lines when they fit) under the picture where there is room, not in the column.
        int? stripBottom = null;
        if (Assets.Pack?.CommandPanels is { } strip && (PanelPlace || FightListUp))
            stripBottom = StripBottom(PanelSize(strip, k).Y, CurrentList?.Layout, strip.Window.Height, k);
        var noteText = Note ?? Help;
        var partyLines = PartyLines();
        var layout = LayOutColumn(picture, left, right, stripBottom, c => HelpLines(noteText, c).Count,
            c => partyLines.Sum(l => UiOverlays.Lines(l, c).Count));
        var note = HelpLines(noteText, layout.NoteCols);
        var noteTop = layout.NoteTop;
        var party = layout.Party;
        var partyRows = partyLines.SelectMany(l => UiOverlays.Lines(l, layout.PartyCols)).ToList();
        var box = layout.Box;
        // s151: _ewait's bar / the typed prompts' band under the text (the box keeps its lines: it scrolls).
        var slot = EventSlotHeight(k);
        box.Height -= slot;
        DrawBox(batch, pixel, box);
        if (slot > 0)
            DrawEventSlot(batch, pixel, new Rectangle(box.X, box.Bottom, box.Width, slot), k);
        if (row is { Row: { } r } && Assets.Pack?.CommandPanels is { } set)
        {
            var rowWidth = RowLayout(row, set.IconSize, i => RowLabelWidth(i, k)).Width;
            DrawPanel(batch, pixel, set, row, RowAt(r, rowWidth, k, 316, box.Bottom, stripLeft, 4), k);
        }
        Fill(batch, pixel, party, Panel);
        DrawRows(batch, pixel, party, [.. partyRows.Select(l => new BoxRow(l, Text))], PartyScroll, topPad: 2);
        for (var i = 0; i < note.Count; i++)
            PixelFont.Draw(batch, pixel, layout.NoteX, noteTop + i * PixelFont.LineHeight, note[i], Note is null ? Dim : Title);
        return true;
    }

    /// <summary>
    /// Where <see cref="DrawBeside"/>'s text goes (layout pixels): the box, the party lines (<see cref="PartyCols"/>
    /// columns), and the key help or shell note from (<see cref="NoteX"/>, <see cref="NoteTop"/>) in <see cref="NoteCols"/>
    /// columns.
    /// </summary>
    public readonly record struct ColumnLayout(Rectangle Box, Rectangle Party, int PartyCols, int NoteX, int NoteTop, int NoteCols);

    /// <summary>
    /// The key help (or a shell note) wrapped to <paramref name="cols"/> columns (s157): help lines part their key-and-word
    /// groups with two spaces ("C spell  U use"), so a wrapped line breaks between groups, never between a key and its word,
    /// and keeps the two spaces; a group wider than the line wraps by words (<see cref="UiOverlays.Lines"/>).
    /// </summary>
    public static IReadOnlyList<string> HelpLines(string text, int cols)
    {
        text ??= "";
        if (PixelFont.Measure(text) <= cols * PixelFont.Advance)
            return [text];
        var lines = new List<string>();
        var line = "";
        foreach (var group in text.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Length > 0 && PixelFont.Measure(line + "  " + group) <= cols * PixelFont.Advance)
            {
                line += "  " + group;
                continue;
            }
            if (line.Length > 0)
                lines.Add(line);
            line = "";
            if (PixelFont.Measure(group) <= cols * PixelFont.Advance)
                line = group;
            else
                lines.AddRange(UiOverlays.Lines(group, cols));
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    /// <summary>Layout pixel the lowest text row's glyphs may reach (the screen's 4-pixel margin).</summary>
    private const int TextBottom = 196;

    /// <summary>
    /// <see cref="DrawBeside"/>'s text layout for a picture at <paramref name="picture"/> and the text column from
    /// <paramref name="left"/> to <paramref name="right"/>; <paramref name="stripBottom"/> is the Command strip's lowest
    /// drawn row where the strip is kept (null elsewhere). <paramref name="noteRows"/> / <paramref name="partyRows"/> give the
    /// wrapped row counts at a column count.
    /// <para>
    /// s157 (user: "if you can make it look better then do it"): the column beside a 320-pixel picture and the strip is
    /// about 11 text columns, so the key help wrapped to 5–7 rows (breaking inside a word) and the party lines scrolled.
    /// Where the strip is kept, the key help goes under the picture and the strip, the screen's full width (two rows for
    /// every help line), or else the width left of the strip; the column's text then reaches down to it, and the party
    /// panel may take half the column (was 6 rows). Under a picture with room for them (a building's 320 × 180 picture),
    /// the party lines also go under the picture, one row a member, and the box takes the column's whole height. Where
    /// the help does not fit under the picture (a tall list window, no strip: the fight), the old column layout stays.
    /// Nothing is dropped either way: every area wraps and scrolls as before.
    /// </para>
    /// </summary>
    public static ColumnLayout LayOutColumn(Rectangle picture, int left, int right, int? stripBottom, Func<int, int> noteRows,
        Func<int, int> partyRows)
    {
        ArgumentNullException.ThrowIfNull(noteRows);
        ArgumentNullException.ThrowIfNull(partyRows);
        var lh = PixelFont.LineHeight;
        var columnWidth = right - left;
        var cols = (columnWidth - 8) / PixelFont.Advance;
        // The top of a band of `rows` text rows ending at TextBottom, when it clears `above` by 2 pixels.
        int? BandTop(int rows, int above)
        {
            var top = TextBottom - PixelFont.GlyphHeight - (Math.Max(1, rows) - 1) * lh;
            return top >= above + 2 ? top : null;
        }
        if (stripBottom is { } sb)
        {
            (int X, int Top, int Cols)? band = null;
            var fullCols = (316 - 4 - 2) / PixelFont.Advance;
            var leftCols = (right - 4 - 2) / PixelFont.Advance;
            if (BandTop(noteRows(fullCols), Math.Max(picture.Bottom, sb)) is { } full)
                band = (4, full, fullCols);
            else if (BandTop(noteRows(leftCols), picture.Bottom) is { } part)
                band = (4, part, leftCols);
            if (band is { } b)
            {
                var bottom = b.Top - 3; // the column's text ends 3 pixels above the help, as before
                var underCols = (right - 4 - 8) / PixelFont.Advance;
                var underHeight = partyRows(underCols) * lh + 4;
                if (bottom - underHeight >= picture.Bottom + 4)
                {
                    var under = new Rectangle(4, bottom - underHeight, right - 4, underHeight);
                    return new(new Rectangle(left, 4, columnWidth, under.Y - 4 - 4), under, underCols, b.X, b.Top, b.Cols);
                }
                var cap = Math.Max(6, ((bottom - 4) / 2 - 4) / lh);
                var height = Math.Min(partyRows(cols), cap) * lh + 4;
                var beside = new Rectangle(left, bottom - height, columnWidth, height);
                return new(new Rectangle(left, 4, columnWidth, beside.Y - 4 - 4), beside, cols, b.X, b.Top, b.Cols);
            }
        }
        var noteCols = (columnWidth - 2) / PixelFont.Advance;
        var noteTop = TextBottom - noteRows(noteCols) * lh;
        var partyHeight = Math.Min(partyRows(cols) * lh + 4, 6 * lh + 4);
        var party = new Rectangle(left, noteTop - 3 - partyHeight, columnWidth, partyHeight);
        return new(new Rectangle(left, 4, columnWidth, party.Y - 4 - 4), party, cols, left + 1, noteTop, noteCols);
    }

    /// <summary>The menu box's rows when they run past it.</summary>
    public ScrollBox BoxScroll { get; } = new();

    /// <summary>The party lines when they run past their panel.</summary>
    public ScrollBox PartyScroll { get; } = new();

    /// <summary>The rows <see cref="DrawRows"/> shows in an area <paramref name="height"/> pixels high.</summary>
    public static int VisibleRows(int height, int topPad) =>
        Math.Max(1, (height - topPad - 2 - PixelFont.GlyphHeight) / PixelFont.LineHeight + 1);

    /// <summary>
    /// <paramref name="rows"/> from the top of <paramref name="area"/> (9 pixels apart), scrolled by <paramref name="scroll"/>.
    /// s151: an icon row's block (<see cref="BoxRow.Block"/>) is drawn whole or not at all, so its icon never crosses the
    /// area's edge; following a row inside a block brings the whole block into view.
    /// </summary>
    private void DrawRows(SpriteBatch batch, Texture2D pixel, Rectangle area,
        IReadOnlyList<BoxRow> rows, ScrollBox scroll, int topPad, int follow = -1, int cursor = -1, int cursorWidth = 1)
    {
        var visible = VisibleRows(area.Height, topPad);
        scroll.Set(rows.Count, visible);
        if (follow >= 0)
        {
            var owner = rows[follow].Owner;
            if (owner >= 0)
                scroll.Follow(owner, owner + rows[owner].Block - 1);
            else
            {
                // s160: a selected line wrapped over several rows (the narrow column beside the combat spell list) is
                // brought into view whole, not only its first row.
                var last = follow;
                while (last + 1 < rows.Count && rows[last + 1].Selected && rows[last + 1].Owner < 0)
                    last++;
                scroll.Follow(follow, last);
            }
        }
        UiOverlays.HitScrollArea(scroll, area);
        for (var i = 0; i < visible && scroll.Offset + i < rows.Count; i++)
        {
            var row = rows[scroll.Offset + i];
            var y = area.Y + topPad + i * PixelFont.LineHeight;
            if (row.Owner >= 0)
            {
                if (row.Owner < scroll.Offset || row.Owner + rows[row.Owner].Block > scroll.Offset + visible)
                    continue; // part of a block cut by the scroll: shown when scrolled into view
                if (row.Icon is { } icon)
                {
                    // s158: Share out's row 0 lit across the row (_wxbox XOR 15: the window grey becomes colour 1).
                    if (row.Lit)
                        Fill(batch, pixel, new Rectangle(area.X + 4, y, area.Width - 8, IconCell(icon).Y),
                            Assets!.PaletteColour("pkh", 14 ^ ShareOutXor) ?? Color.DimGray);
                    DrawItemIcon(batch, pixel, icon, new Vector2(area.X + 4, y), row.Lit);
                }
            }
            PixelFont.Draw(batch, pixel, area.X + 4 + row.Indent, y + row.Dy, row.Text,
                row.Lit ? Assets?.PaletteColour("pkh", ShareOutXor) ?? Color.White : row.Color);
            if (scroll.Offset + i == cursor) // a 1-pixel bar, one text row high, after the text (events.md 4.1)
                Fill(batch, pixel, new Rectangle(area.X + 4 + row.Indent + PixelFont.Measure(row.Text), y + row.Dy, cursorWidth,
                    PixelFont.GlyphHeight), row.Color);
        }
        UiOverlays.DrawScrollBar(batch, pixel, scroll, new Rectangle(area.X, area.Y + 1, area.Width - 1, area.Height - 2));
    }

    private void DrawMap(SpriteBatch batch, Texture2D pixel, Rectangle area)
    {
        var m = D.Map;
        for (var dy = -ViewRadius; dy <= ViewRadius; dy++)
            for (var dx = -ViewRadius; dx <= ViewRadius; dx++)
            {
                int x = D.X + dx, y = D.Y + dy;
                if (x is < 0 or >= P4DungeonFile.Size || y is < 0 or >= P4DungeonFile.Size)
                    continue;
                var r = new Rectangle(area.X + (dx + ViewRadius) * Cell, area.Y + (dy + ViewRadius) * Cell, Cell, Cell);
                var floor = m.Floor(x, y);
                Fill(batch, pixel, new Rectangle(r.X + 1, r.Y + 1, Cell - 1, Cell - 1),
                    P4DungeonMap.IsWater(floor) ? Water : IsBlocked(m, x, y) ? Blocked : Floor);
                Side(m.Move(x, y, 0), new Rectangle(r.X, r.Y, Cell + 1, 1));
                Side(m.Move(x, y, 3), new Rectangle(r.X, r.Y, 1, Cell + 1));
                if (dx == ViewRadius || x == P4DungeonFile.Size - 1)
                    Side(m.Move(x, y, 1), new Rectangle(r.Right, r.Y, 1, Cell + 1));
                if (dy == ViewRadius || y == P4DungeonFile.Size - 1)
                    Side(m.Move(x, y, 2), new Rectangle(r.X, r.Bottom, Cell + 1, 1));
            }

        // The party in the middle, with a tick on the side it faces.
        var c = new Rectangle(area.X + ViewRadius * Cell + 3, area.Y + ViewRadius * Cell + 3, 3, 3);
        Fill(batch, pixel, c, Party);
        var tick = D.Dir switch
        {
            0 => new Rectangle(c.X + 1, c.Y - 2, 1, 2),
            1 => new Rectangle(c.Right, c.Y + 1, 2, 1),
            2 => new Rectangle(c.X + 1, c.Bottom, 1, 2),
            _ => new Rectangle(c.X - 2, c.Y + 1, 2, 1),
        };
        Fill(batch, pixel, tick, Party);

        void Side(int code, Rectangle line)
        {
            var color = code switch
            {
                P4DungeonMap.Wall or P4DungeonMap.Edge => (Color?)Wall,
                P4DungeonMap.LockedDoor => Door,
                P4DungeonMap.SealedDoor => Sealed,
                _ => null,
            };
            if (color is { } col)
                Fill(batch, pixel, line, col);
        }
    }

    /// <summary>Placeholder colours by outdoor terrain type 0–19 (no picture pack yet, T6.4).</summary>
    private static readonly Color[] TerrainColors =
    [
        new(40, 70, 150), new(70, 110, 190), new(190, 170, 100), new(130, 120, 70), new(110, 100, 100),
        new(30, 100, 40), new(80, 160, 70), new(200, 70, 30), new(150, 150, 170), new(120, 30, 30),
        new(80, 20, 20), new(70, 60, 80), new(90, 80, 100), new(200, 220, 255), new(220, 200, 120),
        new(220, 140, 60), new(160, 90, 160), new(80, 200, 220), new(230, 230, 230), new(20, 30, 70),
    ];

    private static readonly Color ShipColor = new(240, 220, 160);

    private void DrawOutdoor(SpriteBatch batch, Texture2D pixel, Rectangle area)
    {
        if (O.MapData is not { } m)
            return;
        for (var dy = -ViewRadius; dy <= ViewRadius; dy++)
            for (var dx = -ViewRadius; dx <= ViewRadius; dx++)
            {
                int x = O.X + dx, y = O.Y + dy;
                if (x is < 0 or >= P4OutdoorMap.Size || y is < 0 or >= P4OutdoorMap.Size)
                    continue;
                var r = new Rectangle(area.X + (dx + ViewRadius) * Cell, area.Y + (dy + ViewRadius) * Cell, Cell, Cell);
                var type = P4OutdoorMap.TypeOf(m.Tile(x, y));
                Fill(batch, pixel, r, TerrainColors[Math.Clamp(type, 0, TerrainColors.Length - 1)]);
                var p = Game.Party;
                if (p.Ship == P4ShipState.Moored && p.ShipMap == O.Map && p.ShipX == x && p.ShipY == y)
                    Fill(batch, pixel, new Rectangle(r.X + 2, r.Y + 3, Cell - 4, 3), ShipColor);
            }
        var c = new Rectangle(area.X + ViewRadius * Cell + 2, area.Y + ViewRadius * Cell + 2, 5, 5);
        Fill(batch, pixel, c, Game.Party.Ship == P4ShipState.Aboard ? ShipColor : Party);
    }

    /// <summary>A square no side leads into (solid rock).</summary>
    private static bool IsBlocked(P4DungeonMap m, int x, int y) =>
        Enumerable.Range(0, 4).All(d => m.Move(x, y, d) is P4DungeonMap.Wall or P4DungeonMap.Edge);

    private static void Fill(SpriteBatch batch, Texture2D pixel, Rectangle r, Color c) => batch.Draw(pixel, r, c);

    private static void Outline(SpriteBatch batch, Texture2D pixel, Rectangle r, Color c)
    {
        Fill(batch, pixel, new Rectangle(r.X, r.Y, r.Width, 1), c);
        Fill(batch, pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        Fill(batch, pixel, new Rectangle(r.X, r.Y, 1, r.Height), c);
        Fill(batch, pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}

/// <summary>
/// The IV preview start (<c>--p4-dungeon N</c>): skips the new game's creation at the Guild and puts
/// a party made by IV's own creation record (<see cref="P4CreationRecord"/>, new persons, no ancestors) at the outdoor
/// entry square of location map N. A developer switch like <see cref="TestParty"/>, not a way into the game.
/// </summary>
public static class P4Preview
{
    public const string Switch = "--p4-dungeon";

    /// <summary>The preview party: (name, race, class), each pair one IV allows.</summary>
    public static IReadOnlyList<(string Name, int Race, int Class)> Members { get; } =
    [
        ("BRAND", P4Ids.Human, P4Ids.Fighter), ("ODO", P4Ids.Dwarf, P4Ids.Priest), ("ELWYN", P4Ids.Elf, P4Ids.Wizard),
        ("PIP", P4Ids.Halfling, P4Ids.Thief),
    ];

    /// <summary>The map after <see cref="Switch"/>, null without it; a map with no outdoor entry is an error.</summary>
    public static int? ParseArgs(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        var i = args.ToList().IndexOf(Switch);
        if (i < 0)
            return null;
        var entries = OpenPhantasie.Data.Phantasie4World.DungeonEntries();
        if (i + 1 < args.Count && int.TryParse(args[i + 1], out var map) && entries.ContainsKey(map))
            return map;
        error = $"{Switch} needs a location map with an outdoor entry: {string.Join(", ", entries.Keys)}.";
        return null;
    }

    /// <summary>
    /// The square after the map (<c>--p4-dungeon N X,Y</c>, 0–29 each; s125, to compare a view with a square poked on
    /// XEiJ), null when only the map is given or the text is not a square.
    /// </summary>
    public static (int X, int Y)? ParseSquare(IReadOnlyList<string> args)
    {
        var i = args.ToList().IndexOf(Switch);
        if (i < 0 || i + 2 >= args.Count)
            return null;
        var parts = args[i + 2].Split(',');
        return parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y)
            && x is >= 0 and < P4DungeonFile.Size && y is >= 0 and < P4DungeonFile.Size
            ? (x, y)
            : null;
    }

    /// <summary>
    /// A new IV game (the game tab's rules for new games) with the preview party at map <paramref name="map"/>'s entry, or
    /// at <paramref name="at"/> in that map.
    /// </summary>
    public static Phantasie4Campaign Start(int map, GameOptions options, int seed = 42, (int X, int Y)? at = null)
    {
        var rng = new SeededRandomSource(seed);
        var party = MakeParty(options, rng, map);
        var (_, x, y) = OpenPhantasie.Data.Phantasie4World.DungeonEntries()[map];
        // The outdoor square the party came from is not known here; entering the map replaces it at once.
        var game = OpenPhantasie.Data.Phantasie4World.NewCampaign(options, party, rng, new P4Location(P4Place.Outdoors, 0, x, y));
        game.EnterDungeon(map, at?.X ?? x, at?.Y ?? y);
        return game;
    }

    public const string TownSwitch = "--p4-town";

    /// <summary>The town after <see cref="TownSwitch"/> (1–21), null without it.</summary>
    public static int? ParseTownArgs(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        var i = args.ToList().IndexOf(TownSwitch);
        if (i < 0)
            return null;
        if (i + 1 < args.Count && int.TryParse(args[i + 1], out var town) && town is >= 1 and <= 21)
            return town;
        error = $"{TownSwitch} needs a town 1-21.";
        return null;
    }

    /// <summary>A new IV game with the preview party arriving in town <paramref name="town"/> (the town entry runs).</summary>
    public static Phantasie4Campaign StartInTown(int town, GameOptions options, int seed = 42)
    {
        var rng = new SeededRandomSource(seed);
        var game = OpenPhantasie.Data.Phantasie4World.NewCampaign(options, MakeParty(options, rng, town), rng,
            new P4Location(P4Place.Town, town, 0, 0));
        game.Start();
        return game;
    }

    public const string EndingSwitch = "--p4-ending";

    /// <summary>The ending after <see cref="EndingSwitch"/> (0–4: <c>ending.md</c> §1's 125…225; 3 shows all three pictures), null without it.</summary>
    public static int? ParseEndingArgs(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        var i = args.ToList().IndexOf(EndingSwitch);
        if (i < 0)
            return null;
        if (i + 1 < args.Count && int.TryParse(args[i + 1], out var ending) && ending is >= 0 and <= 4)
            return ending;
        error = $"{EndingSwitch} needs an ending 0-4.";
        return null;
    }

    /// <summary>
    /// A new IV game with the preview party arriving in Pendragon with ending flags f0…f<paramref name="ending"/> set, so the
    /// town entry runs that ending (<see cref="P4TownRules.Ending"/>). A developer switch to watch the ending pictures.
    /// </summary>
    public static Phantasie4Campaign StartEnding(int ending, GameOptions options, int seed = 42)
    {
        var rng = new SeededRandomSource(seed);
        var party = MakeParty(options, rng, P4TownLoop.Pendragon);
        for (var f = 0; f <= ending; f++)
            party.StoryFlags[P4TownLoop.EndingFlag + f] = 1;
        var game = OpenPhantasie.Data.Phantasie4World.NewCampaign(options, party, rng,
            new P4Location(P4Place.Town, P4TownLoop.Pendragon, 0, 0));
        game.Start();
        return game;
    }

    private static P4Party MakeParty(GameOptions options, IRandomSource rng, int map)
    {
        var tables = OpenPhantasie.Data.GameTables.Load(GameId.P4).Phantasie4!.Tables;
        var party = new P4Party();
        foreach (var (name, race, cls) in Members)
        {
            var abilities = P4Creation.RollNewPerson(tables.Creation, rng, race, cls);
            var c = P4CreationRecord.Create(options, tables, rng, name, race, cls, abilities, [], map);
            var m = P4Member.From(c);
            // The guild roster holds the preview party (slots 0 on), as a party formed at the guild would.
            m.GuildSlot = party.Members.Count;
            party.Roster[m.GuildSlot] = m.Clone();
            party.Members.Add(m);
        }
        // As if the party had just left a town (_exittown): +0B (the average level, which caps the monster group size) and
        // each member's +3B melee bonus, which the fight reads.
        P4TownRules.Leave(party);
        return party;
    }
}
