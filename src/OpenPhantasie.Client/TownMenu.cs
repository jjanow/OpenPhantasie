using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>Where the party is inside the town: the menu bar over the town picture, or a building / submenu.</summary>
public enum TownPlace
{
    Menu,
    Armory,
    Bank,
    Option,
    /// <summary>After a rest: Inn menu <c>0x0d</c> (OV1 <c>0x2a0d</c>).</summary>
    Inn,
}

/// <summary>What a town menu pick asks the shell to do (entering the Armory, Bank or Option is handled here).</summary>
public enum TownChoice
{
    Mystic,
    Exit,
    Inn,
    Guild,
    Withdraw,
    Deposit,
    CastSpell,
    UseItem,
    ShowParty,
    Distribute,
    SaveGame,
    ToggleAutoSave,
    ToggleSound,
    Printout,
    /// <summary>III's Trade Items (D3-027; not in DOS III).</summary>
    Trade,
}

/// <summary>
/// The DOS town menu: a highlight bar "Option Bank Mystic Exit Armory Inn Guild" (label table <c>PHANTASI.COM 0x5e6e</c>,
/// s49 captures) over the town picture. Arrows move the highlight and Return/Space picks it (manual C-64 addendum:
/// highlight then RETURN). The bar (COM menu <c>0x1</c>) and each list keep their own cursor, as every DOS menu id does
/// (message-and-text.md §Menus, s58): first-ever highlights are Guild on the bar and Save game in Option (menu <c>0x3</c>,
/// s58 DOSBox <c>po1</c> reopened there), the first entry in Bank and Inn. Bank and Option open a short list below the bar;
/// the Armory is a screen of its own.
/// D-066 (user s53: both): the manual's letter keys work on the bar too — A Armory, B Bank, M Mystic, N Inn, G Guild,
/// L leave town; U use item, I inspect, D distribute, S save game (C cast spell is the shell's global key).
/// </summary>
public sealed class TownMenu
{
    public static readonly string[] BarLabels = ["Option", "Bank", "Mystic", "Exit", "Armory", "Inn", "Guild"];

    /// <summary>Bank menu (manual: W withdraw money, D)EPOSIT CARRIED GOLD).</summary>
    public static readonly string[] BankLabels = ["Withdraw", "Deposit", "Exit"];

    /// <summary>
    /// Town Option, COM menu <c>0x3</c> [C] (s56: labels <c>0xf..0x13</c>, <c>0x7b</c>; OV1 dispatch <c>0x1ebd</c>, jump table
    /// <c>0x1f2e</c>): Cast spell, Use item, Show party, Save game, Auto save (flips <c>[0x3092]</c>, starts off; when on,
    /// leaving town saves, OV1 <c>0x483e</c> → <c>0x4562</c>), Sound (starts on), Printout (OV1 <c>0x46dd</c>), Exit.
    /// The two toggles show their state as a +/- prefix, as the DOS labels do.
    /// </summary>
    public static readonly string[] OptionLabels =
        ["Cast spell", "Use item", "Show party", "Save game", "Auto save", "Sound", "Printout", "Exit"];

    /// <summary>III's DOS bank (<c>p3.bank</c> = <c>dos</c>, ovl2 <c>0x297a</c>): "Withdraw all gold", "Deposit some gold".</summary>
    public static readonly string[] DosBankLabels = ["Withdraw all", "Deposit some", "Exit"];

    /// <summary>The Bank list is III's DOS bank (<see cref="DosBankLabels"/>); set from the save's rules.</summary>
    public bool DosBank { get; set; }

    /// <summary>The Option list offers III's Trade Items after Use item (<see cref="GameSession.CanTrade"/>).</summary>
    public bool Trade { get; set; }

    private string[] BankKeys => DosBank ? DosBankLabels : BankLabels;

    private string[] OptionKeys => Trade
        ? [.. OptionLabels.Take(2), "Trade items", .. OptionLabels.Skip(2)]
        : OptionLabels;

    /// <summary>Inn menu <c>0x0d</c> [C] (s56: no title, labels <c>0x3b..0x3d</c>); choice 1 = Distribute (OV1 <c>0x2ab3</c>).</summary>
    public static readonly string[] InnLabels = ["Exit Inn", "Distribute", "Printout"];

    /// <summary>The Option toggles' state (the Client's saved settings), shown as the label prefix.</summary>
    public bool AutoSave { get; set; }

    public bool Sound { get; set; } = true;

    public TownPlace Place { get; private set; } = TownPlace.Menu;

    /// <summary>First-ever bar highlight: Guild (menu <c>0x1</c>'s initial selection word, static).</summary>
    public const int FirstBarCursor = 6;

    /// <summary>First-ever Option highlight: Save game (menu <c>0x3</c>'s initial selection word, static).</summary>
    public const int FirstOptionCursor = 3;

    private readonly Dictionary<TownPlace, int> _subCursors = new() { [TownPlace.Option] = FirstOptionCursor };

    public int BarCursor { get; private set; } = FirstBarCursor;

    /// <summary>The open list's cursor; each list remembers its own between openings (in memory only).</summary>
    public int SubCursor
    {
        get => _subCursors.GetValueOrDefault(Place);
        private set => _subCursors[Place] = value;
    }

    /// <summary>The open Bank / Option / Inn list as drawn, or empty on the bar and in the Armory.</summary>
    public IReadOnlyList<string> SubLabels => Place switch
    {
        TownPlace.Bank => BankKeys.Select(LabelText).ToArray(),
        TownPlace.Option => OptionKeys.Select(Display).ToArray(),
        TownPlace.Inn => InnLabels.Select(LabelText).ToArray(),
        _ => [],
    };

    /// <summary>The bar labels as drawn (string table <c>town.label.*</c>).</summary>
    public static IReadOnlyList<string> BarText => BarLabels.Select(LabelText).ToArray();

    /// <summary>
    /// On-screen text of a menu label. The label arrays above are the menus' internal ids (the DOS English words); what
    /// the player sees comes from the string table, <c>town.label.&lt;id lower-case, spaces as _&gt;</c>.
    /// </summary>
    public static string LabelText(string label) =>
        UiText.S("town.label." + label.ToLowerInvariant().Replace(' ', '_'));

    private IReadOnlyList<string> SubKeys => Place switch
    {
        TownPlace.Bank => BankKeys,
        TownPlace.Option => OptionKeys,
        TownPlace.Inn => InnLabels,
        _ => [],
    };

    private string Display(string label) => label switch
    {
        "Auto save" => (AutoSave ? "+" : "-") + LabelText(label),
        "Sound" => (Sound ? "+" : "-") + LabelText(label),
        _ => LabelText(label),
    };

    /// <summary>The Inn menu after a rest (menu <c>0x0d</c>: first-ever on Exit Inn, then its last choice).</summary>
    public void OpenInn() => Open(TownPlace.Inn);

    /// <summary>Back to the bar (town entry, Guild exit, load). The highlight is kept.</summary>
    public void Reset() => Place = TownPlace.Menu;

    /// <summary>One key on the bar or in a Bank / Option list; returns the action to run, or null.</summary>
    public TownChoice? HandleKey(Keys key)
    {
        if (Place == TownPlace.Armory)
            return null;
        var pick = key is Keys.Enter or Keys.Space;
        if (Place == TownPlace.Menu)
        {
            if (key == Keys.Left)
                BarCursor = (BarCursor + BarLabels.Length - 1) % BarLabels.Length;
            else if (key == Keys.Right)
                BarCursor = (BarCursor + 1) % BarLabels.Length;
            else if (pick)
                return PickBar();
            else
                return PickLetter(key);
            return null;
        }

        var labels = SubLabels;
        if (key == Keys.Up)
            SubCursor = Math.Max(0, SubCursor - 1);
        else if (key == Keys.Down)
            SubCursor = Math.Min(labels.Count - 1, SubCursor + 1);
        else if (key == Keys.Back)
            Back();
        else if (pick)
            return PickSub();
        return null;
    }

    /// <summary>From a building or list back to the bar (Esc / Backspace). False when already on the bar.</summary>
    public bool Back()
    {
        if (Place == TownPlace.Menu)
            return false;
        Place = TownPlace.Menu;
        return true;
    }

    /// <summary>Leave the Armory for the bar.</summary>
    public void LeaveArmory()
    {
        if (Place == TownPlace.Armory)
            Place = TownPlace.Menu;
    }

    private TownChoice? PickBar()
    {
        switch (BarLabels[BarCursor])
        {
            case "Option":
                Open(TownPlace.Option);
                return null;
            case "Bank":
                Open(TownPlace.Bank);
                return null;
            case "Armory":
                Place = TownPlace.Armory;
                return null;
            case "Mystic":
                return TownChoice.Mystic;
            case "Inn":
                return TownChoice.Inn;
            case "Guild":
                return TownChoice.Guild;
            default:
                return TownChoice.Exit;
        }
    }

    /// <summary>Manual p.3 / p.11 town keys (D-066).</summary>
    private TownChoice? PickLetter(Keys key)
    {
        switch (key)
        {
            case Keys.A:
                Place = TownPlace.Armory;
                return null;
            case Keys.B:
                Open(TownPlace.Bank);
                return null;
            case Keys.M: return TownChoice.Mystic;
            case Keys.N: return TownChoice.Inn;
            case Keys.G: return TownChoice.Guild;
            case Keys.L: return TownChoice.Exit;
            case Keys.U: return TownChoice.UseItem;
            case Keys.I: return TownChoice.ShowParty;
            case Keys.D: return TownChoice.Distribute;
            case Keys.S: return TownChoice.SaveGame;
            default: return null;
        }
    }

    private TownChoice? PickSub()
    {
        var label = SubKeys[SubCursor];
        if (label is "Exit" or "Exit Inn")
        {
            Place = TownPlace.Menu;
            return null;
        }

        // A Bank / Option pick hands over to a dialog or prompt; the bar is under it when that closes.
        Place = TownPlace.Menu;
        return label switch
        {
            "Withdraw" or "Withdraw all" => TownChoice.Withdraw,
            "Deposit" or "Deposit some" => TownChoice.Deposit,
            "Trade items" => TownChoice.Trade,
            "Cast spell" => TownChoice.CastSpell,
            "Use item" => TownChoice.UseItem,
            "Save game" => TownChoice.SaveGame,
            "Auto save" => TownChoice.ToggleAutoSave,
            "Sound" => TownChoice.ToggleSound,
            "Printout" => TownChoice.Printout,
            "Distribute" => TownChoice.Distribute,
            _ => TownChoice.ShowParty,
        };
    }

    private void Open(TownPlace place) => Place = place; // the list reopens on its own remembered cursor
}
