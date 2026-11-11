using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

public enum UnifiedTownCommand
{
    Guild, Armory, Bank, Inn, Mystic, Training, Learn, Hall, Shipwright, Leave,
    Inspect, Trade, Distribute, Use,
    Cast, Resurrection, Transportation,
    Save, Load, Options, AutoSave, Sound, Printout, GameMenu,
}

public sealed record UnifiedTownRowTag(int Row);
public sealed record UnifiedTownTabTag(int Tab);

public sealed record UnifiedTownContext(GameId Game, int Members, bool Trade = false, bool Hall = false,
    bool Shipwright = false, bool AutoSaveRequired = false)
{
    public string? Refusal(UnifiedTownCommand command)
    {
        if (command is UnifiedTownCommand.Hall or UnifiedTownCommand.Shipwright)
        {
            if (Game != GameId.P4) return UiText.S("unifiedtown.unsupported");
            if (command == UnifiedTownCommand.Hall && !Hall) return UiText.S("unifiedtown.no_hall");
            if (command == UnifiedTownCommand.Shipwright && !Shipwright) return UiText.S("unifiedtown.no_shipwright");
        }
        if (Game == GameId.P4 && command is UnifiedTownCommand.AutoSave or UnifiedTownCommand.Printout)
            return UiText.S("unifiedtown.unsupported");
        if (Members == 0 && command is not (UnifiedTownCommand.Guild or UnifiedTownCommand.Save or UnifiedTownCommand.Load
            or UnifiedTownCommand.Options or UnifiedTownCommand.Sound or UnifiedTownCommand.AutoSave or UnifiedTownCommand.GameMenu))
            return UiText.S("unifiedtown.no_party");
        if (command == UnifiedTownCommand.AutoSave && AutoSaveRequired)
            return UiText.S("unifiedtown.autosave_required");
        if (command == UnifiedTownCommand.Trade && !Trade)
            return UiText.S("unifiedtown.no_trade");
        return null;
    }
}

/// <summary>Shared town navigation only. Hosts supply availability and run the campaign's existing services.</summary>
public sealed class UnifiedTownMenu
{
    public static readonly IReadOnlyList<IReadOnlyList<UnifiedTownCommand>> Tabs =
    [
        new[] { UnifiedTownCommand.Guild, UnifiedTownCommand.Armory, UnifiedTownCommand.Bank, UnifiedTownCommand.Inn,
            UnifiedTownCommand.Mystic, UnifiedTownCommand.Training, UnifiedTownCommand.Learn, UnifiedTownCommand.Hall,
            UnifiedTownCommand.Shipwright, UnifiedTownCommand.Leave },
        new[] { UnifiedTownCommand.Inspect, UnifiedTownCommand.Trade, UnifiedTownCommand.Distribute, UnifiedTownCommand.Use },
        new[] { UnifiedTownCommand.Cast, UnifiedTownCommand.Resurrection, UnifiedTownCommand.Transportation },
        new[] { UnifiedTownCommand.Save, UnifiedTownCommand.Load, UnifiedTownCommand.Options, UnifiedTownCommand.AutoSave,
            UnifiedTownCommand.Sound, UnifiedTownCommand.Printout, UnifiedTownCommand.GameMenu },
    ];
    private static readonly string[] TabIds = ["town", "party", "magic", "game"];
    public static IEnumerable<string> StringKeys() => Enum.GetValues<UnifiedTownCommand>()
        .Select(c => "unifiedtown." + c.ToString().ToLowerInvariant())
        .Concat(TabIds.Select(t => "unifiedtown.tab." + t)).Append("unifiedtown.help");
    private readonly int[] _rows = new int[4];
    public int Tab { get; private set; }
    public int Row => _rows[Tab];
    public IReadOnlyList<UnifiedTownCommand> Commands => Tabs[Tab];
    public UnifiedTownCommand Selected => Commands[Row];
    public static string Label(UnifiedTownCommand command) => UiText.S("unifiedtown." + command.ToString().ToLowerInvariant());
    public void AimRow(int row) => _rows[Tab] = Math.Clamp(row, 0, Commands.Count - 1);
    public void AimTab(int tab) => Tab = Math.Clamp(tab, 0, Tabs.Count - 1);
    public void Reset() { Tab = 0; Array.Clear(_rows); }

    public UnifiedTownCommand? HandleKey(Keys key)
    {
        switch (key)
        {
            case Keys.Left: Tab = (Tab + Tabs.Count - 1) % Tabs.Count; return null;
            case Keys.Right: Tab = (Tab + 1) % Tabs.Count; return null;
            case Keys.Up: AimRow((Row + Commands.Count - 1) % Commands.Count); return null;
            case Keys.Down: AimRow((Row + 1) % Commands.Count); return null;
            case Keys.Home: AimRow(0); return null;
            case Keys.End: AimRow(Commands.Count - 1); return null;
            case Keys.Escape or Keys.Back: AimTab(3); return null;
            case Keys.Enter or Keys.Space: return Selected;
            default: return key switch
            {
                Keys.A => UnifiedTownCommand.Armory, Keys.B => UnifiedTownCommand.Bank,
                Keys.G => UnifiedTownCommand.Guild, Keys.N => UnifiedTownCommand.Inn,
                Keys.M => UnifiedTownCommand.Mystic, Keys.L => UnifiedTownCommand.Leave,
                Keys.I => UnifiedTownCommand.Inspect, Keys.T => UnifiedTownCommand.Trade,
                Keys.D => UnifiedTownCommand.Distribute, Keys.U => UnifiedTownCommand.Use,
                Keys.C => UnifiedTownCommand.Cast, Keys.S or Keys.F5 => UnifiedTownCommand.Save,
                Keys.F9 => UnifiedTownCommand.Load, Keys.F10 => UnifiedTownCommand.GameMenu,
                _ => null,
            };
        }
    }

    public static IEnumerable<Keys> PadKeys(IEnumerable<Buttons> buttons) => buttons.Select(b => b switch
    {
        Buttons.LeftShoulder => Keys.F5, Buttons.RightShoulder => Keys.F9,
        Buttons.Start => Keys.F10, _ => InputMap.MenuPadKeys([b]).FirstOrDefault(),
    }).Where(k => k != Keys.None);

    /// <summary>One layout in every art pack's 320×200 shell coordinate system; its hit map covers the original town.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, string town, Func<UnifiedTownCommand, string?> refusal,
        HitMap hits, string? note = null, string? status = null)
    {
        hits.Clear();
        hits.Wheel = (Keys.Up, Keys.Down);
        using var font = PixelFont.With(PixelFont.Remaster5x7);
        batch.Draw(pixel, new Rectangle(0, 0, 320, 200), new Color(12, 17, 31));
        PixelFont.Draw(batch, pixel, 10, 8, UiText.F("unifiedtown.title", town), Color.White);
        var x = 10;
        for (var t = 0; t < Tabs.Count; t++)
        {
            var label = UiText.S("unifiedtown.tab." + TabIds[t]);
            var width = PixelFont.Measure(label) + 10;
            var r = new Rectangle(x, 22, width, 12);
            if (Tab == t) batch.Draw(pixel, r, new Color(60, 80, 110));
            PixelFont.Draw(batch, pixel, x + 4, 24, label, Color.White);
            hits.Add(new HitRegion(r, new UnifiedTownTabTag(t)));
            x += width + 3;
        }
        for (var i = 0; i < Commands.Count; i++)
        {
            var cmd = Commands[i];
            var r = new Rectangle(10, 39 + i * 11, 300, 11);
            if (Row == i) batch.Draw(pixel, r, new Color(40, 55, 80));
            PixelFont.Draw(batch, pixel, 14, r.Y + 1, (Row == i ? "> " : "  ") + Label(cmd),
                refusal(cmd) is null ? Color.White : Color.Gray);
            hits.Add(new HitRegion(r, new UnifiedTownRowTag(i)));
        }
        var reason = note ?? refusal(Selected) ?? UiText.S("unifiedtown.ready");
        var lines = UiOverlays.Wrap(reason, 48).ToList();
        for (var i = 0; i < Math.Min(2, lines.Count); i++)
            PixelFont.Draw(batch, pixel, 10, 154 + i * 10, lines[i], Color.LightGray);
        if (status is not null) PixelFont.Draw(batch, pixel, 10, 172, status, Color.LightGray);
        PixelFont.Draw(batch, pixel, 10, 184,
            UiText.S(PadWording.Active ? "unifiedtown.help.pad" : "unifiedtown.help"), Color.White);
    }
}
