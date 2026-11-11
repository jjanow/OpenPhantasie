using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The Options screen (T5.0, <c>09-startup-options.md</c> §2): a General tab and one tab per game. A game tab holds its
/// presentation switches (any time) and the rules new games start with (preset, rules options, other games' feature
/// modules). Opened in game it shows General and the running game only, with that save's rules read-only.
/// Up/Down pick a row (PgUp/PgDn a page, Home/End the ends), Left/Right or Return change it (on the tab row: the tab),
/// Esc closes. Below the list a box describes the row under the cursor (<see cref="Detail"/>): what it does, what each
/// value means, the value each preset gives and its <c>DIFFERENCES</c> entry; [ and ] scroll it.
/// </summary>
public sealed class OptionsScreen
{
    /// <summary>Rows shown at once below the tab row; more scroll.</summary>
    public const int VisibleRows = 8;

    /// <summary>Label column width: the longest label ("BODY-PART INJURY (III)", 22) plus a gap.</summary>
    private const int LabelWidth = 24;

    private const int MinScale = 1;
    private const int MaxScale = 6; // VirtualScreen.CycleScale's range

    private readonly ClientSettings _settings;
    private readonly Func<GameId, IReadOnlyList<(string Id, string Name)>> _packs;
    private readonly Func<GameId, bool> _playable;
    private GameOptions? _runningOptions;
    private List<GameId?> _tabs = [];
    private int _tab;
    private int _scroll;

    /// <param name="packs">The graphics packs present for a game (id, name), default pack first.</param>
    /// <param name="playable">Whether a game is built; null = <see cref="MainMenuScreen.IsPlayable"/>.</param>
    public OptionsScreen(
        ClientSettings settings,
        Func<GameId, IReadOnlyList<(string Id, string Name)>> packs,
        Func<GameId, bool>? playable = null)
    {
        _settings = settings;
        _packs = packs;
        _playable = playable ?? MainMenuScreen.IsPlayable;
    }

    public bool IsOpen { get; private set; }

    /// <summary>The tab shown: null = General, else a game.</summary>
    public GameId? Tab => _tabs.Count == 0 ? null : _tabs[_tab];

    /// <summary>The selected row: 0 = the tab row, then the tab's rows.</summary>
    public int Cursor { get; private set; }

    /// <summary>Something changed since <see cref="Open"/> (the shell saves the settings and applies them).</summary>
    public bool Changed { get; private set; }

    public string Help => UiText.S("options.help");

    /// <summary>The description box under the list; it starts at its top for each row (<see cref="DetailKey"/>).</summary>
    public ScrollBox DetailBox { get; } = new();

    /// <summary>Which row <see cref="Detail"/> describes: changes when the tab or the cursor does.</summary>
    public int DetailKey => HashCode.Combine(_tab, Cursor);

    /// <summary>
    /// From the main menu (<paramref name="running"/> null): General and all four games, on General. In game: General and
    /// the running game, on the game's tab, its rules locked.
    /// </summary>
    public void Open(GameSession? running = null)
    {
        OpenFor(running?.Options);
    }

    /// <summary>Options for a running campaign, including IV, whose rules remain locked.</summary>
    public void OpenFor(GameOptions? runningOptions)
    {
        _runningOptions = runningOptions;
        _tabs = runningOptions is null
            ? [null, .. MainMenuScreen.Games.Select(g => (GameId?)g)]
            : [null, runningOptions.Game];
        _tab = runningOptions is null ? 0 : 1;
        Cursor = 0;
        _scroll = 0;
        DetailBox.Reset();
        Changed = false;
        ImportRequested = false;
        UpdateRequested = false;
        IsOpen = true;
    }

    /// <summary>Handle this frame's keys; true on the frame Esc closed the screen.</summary>
    public bool HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (!IsOpen)
            return false;
        foreach (var key in newKeys)
        {
            var rows = Rows();
            switch (key)
            {
                case Keys.Escape or Keys.Back:
                    IsOpen = false;
                    return true;
                case Keys.Up:
                    Cursor = NextSelectable(rows, -1);
                    break;
                case Keys.Down:
                    Cursor = NextSelectable(rows, +1);
                    break;
                case Keys.PageUp:
                    Cursor = PageSelectable(rows, -1);
                    break;
                case Keys.PageDown:
                    Cursor = PageSelectable(rows, +1);
                    break;
                case Keys.Home:
                    Cursor = 0;
                    break;
                case Keys.End:
                    Cursor = LastSelectable(rows);
                    break;
                case Keys.OemOpenBrackets:
                    DetailBox.ScrollBy(-1);
                    break;
                case Keys.OemCloseBrackets:
                    DetailBox.ScrollBy(1);
                    break;
                case Keys.Left:
                    Change(rows, -1);
                    break;
                case Keys.Right or Keys.Enter or Keys.Space:
                    Change(rows, +1);
                    if (!IsOpen)
                        return true; // a row that closes the screen (IMPORT GAMES, UPDATE NOW)
                    break;
            }
        }
        return false;
    }

    /// <summary>
    /// "Pad coverage" (s182): a pad on this screen. The shared map (<see cref="InputMap.MenuPadKey"/>: D-pad / stick =
    /// arrows, so Left / Right on the tab row switch tabs and on a row change it; A = Return, B = Esc, LB / RB = PgUp / PgDn)
    /// plus the triggers for [ and ] (LT / RT scroll the description box).
    /// </summary>
    public static IEnumerable<Keys> PadKeys(IEnumerable<Buttons> buttons) => buttons.Select(b => b switch
    {
        Buttons.LeftTrigger => Keys.OemOpenBrackets,
        Buttons.RightTrigger => Keys.OemCloseBrackets,
        _ => InputMap.MenuPadKey(b),
    }).Where(k => k != Keys.None);

    /// <summary>
    /// Mouse hover on line <paramref name="line"/> of <see cref="Lines"/>: the cursor goes to that row if Up/Down could stop
    /// there (the tab row or a row with a value). True if it is such a row.
    /// </summary>
    public bool Point(int line)
    {
        var rows = Rows();
        int row;
        if (line == 1)
            row = 0;
        else if (line >= 2 && line - 2 < VisibleRows && _scroll + line - 2 < rows.Count)
            row = _scroll + line - 1;
        else
            return false;
        if (row > 0 && !rows[row - 1].Selectable)
            return false;
        Cursor = row;
        return true;
    }

    /// <summary>
    /// Mouse click on line <paramref name="line"/>, character <paramref name="column"/>: on the tab row the tab clicked is
    /// shown; on a setting row its value steps on, as Return does. The "…" line scrolls on.
    /// </summary>
    public void Click(int line, int column)
    {
        if (!Point(line))
        {
            if (line != 2 + VisibleRows) // " ..." below the last visible row: on to the next row with a value
                return;
            var rows = Rows();
            for (var r = _scroll + VisibleRows + 1; r <= rows.Count; r++)
                if (rows[r - 1].Selectable)
                {
                    Cursor = r;
                    break;
                }
            return;
        }
        if (Cursor > 0)
        {
            Change(Rows(), +1);
            return;
        }
        // Tab names after the marker column: "[name]" or " name ", one space apart (see Lines).
        var at = 1;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var width = TabName(_tabs[i]).Length + 2;
            if (column >= at && column < at + width)
            {
                if (i != _tab)
                {
                    _tab = i;
                    _scroll = 0;
                }
                return;
            }
            at += width + 1;
        }
    }

    private static string TabName(GameId? tab) =>
        tab is { } g ? UiText.S("options.tab." + g.Prefix()) : UiText.S("options.tab.general");

    /// <summary>Every row of the tab as <see cref="Lines"/> writes it, scrolled or not (no cursor mark).</summary>
    public IReadOnlyList<string> RowTexts() => [.. Rows().Select(RowText)];

    private static string RowText(Row row) => row.Value is null ? row.Label : $"{row.Label,-LabelWidth}{row.Value}";

    public IReadOnlyList<MenuLine> Lines()
    {
        var rows = Rows();
        if (Cursor - 1 < _scroll) _scroll = Math.Max(0, Cursor - 1);
        if (Cursor - 1 >= _scroll + VisibleRows) _scroll = Cursor - VisibleRows;

        // The brackets replace a space on each side, so the tab names stay put when the tab changes.
        var tabs = string.Join(" ", _tabs.Select((t, i) =>
        {
            var name = TabName(t);
            return i == _tab ? "[" + name + "]" : " " + name + " ";
        }));
        var lines = new List<MenuLine>
        {
            new(UiText.S("options.title"), MenuLineStyle.Title),
            new((Cursor == 0 ? ">" : " ") + tabs, Cursor == 0 ? MenuLineStyle.Selected : MenuLineStyle.Normal),
        };
        for (var i = _scroll; i < Math.Min(rows.Count, _scroll + VisibleRows); i++)
        {
            var row = rows[i];
            var text = RowText(row);
            var selected = i + 1 == Cursor;
            lines.Add(new((selected ? ">" : " ") + text,
                selected ? MenuLineStyle.Selected
                : row.Header ? MenuLineStyle.Header
                : row.Change is null ? MenuLineStyle.Dim
                : MenuLineStyle.Normal));
        }
        if (rows.Count > _scroll + VisibleRows)
            lines.Add(new(" ...", MenuLineStyle.Dim));
        return lines;
    }

    /// <summary>
    /// One row: a label, its value (null for a header or a note), how Left/Right change it (null = fixed), the string id of
    /// its description (<c>….desc</c>) and, for a rules option, its descriptor (the box lists the presets' values).
    /// </summary>
    private sealed record Row(
        string Label, string? Value, Action<int>? Change, bool Header = false, string? Desc = null,
        RuleOptionDescriptor? Rule = null, bool Locked = false)
    {
        /// <summary>
        /// Up/Down stop on every row with a value, fixed ones too (a save's locked rules, a module not built), so a list
        /// longer than the screen can be scrolled and read; Left/Right change only rows that can change.
        /// </summary>
        public bool Selectable => !Header && Value is not null;
    }

    private List<Row> Rows() => Tab is { } game ? GameRows(game) : GeneralRows();

    private List<Row> GeneralRows()
    {
        List<Row> rows =
        [
            Described("options.window_scale", _settings.IntegerScale.ToString(),
                d => Set(() => _settings.IntegerScale = Math.Clamp(_settings.IntegerScale + d, MinScale, MaxScale))),
            Described("options.start_maximized", OnOff(_settings.StartMaximized),
                _ => Set(() => _settings.StartMaximized = !_settings.StartMaximized)),
        ];
        // s158: re-run the first-run importer (from the main menu only: an import replaces data a running game uses).
        if (_runningOptions is null)
            rows.Add(Described("options.import_games", UiText.S("options.import_games.open"), _ =>
            {
                ImportRequested = true;
                IsOpen = false;
            }));
        // s174: the self-updater (remaster only). The check at start is a switch; UPDATE NOW checks at once (from the main
        // menu only: an update replaces the program and restarts it).
        rows.Add(Described("options.update.at_start", OnOff(_settings.CheckUpdatesAtStart),
            _ => Set(() => _settings.CheckUpdatesAtStart = !_settings.CheckUpdatesAtStart)));
        if (_runningOptions is null)
            rows.Add(Described("options.update.now_row", "v" + Updates.AppVersion.Text, _ =>
            {
                UpdateRequested = true;
                IsOpen = false;
            }));
        return rows;
    }

    /// <summary>s174: the UPDATE NOW row was picked: the screen closed and the shell opens the update screen.</summary>
    public bool UpdateRequested { get; set; }

    /// <summary>The IMPORT GAMES row was picked: the screen closed and the shell opens the import screen.</summary>
    public bool ImportRequested { get; set; }

    private List<Row> GameRows(GameId game)
    {
        if (!_playable(game))
            return [new(UiText.S("options.not_built_yet"), null, null)];

        var g = _settings.Game(game);
        var rows = new List<Row>();

        var packs = _packs(game);
        var pack = packs.FirstOrDefault(p => p.Id == g.Graphics);
        if (pack.Id is null && packs.Count > 0)
            pack = packs[0];
        rows.Add(Described("options.graphics", pack.Name ?? UiText.S("options.none"),
            packs.Count < 2 ? null : d => Set(() => g.Graphics = packs[Cycle(packs.ToList().FindIndex(p => p.Id == pack.Id), d, packs.Count)].Id)));
        // s102: the original's start-up pictures when the game is picked (on = as the original).
        rows.Add(Described("options.title_screen", OnOff(g.TitleScreen), _ => Set(() => g.TitleScreen = !g.TitleScreen)));
        // s108: the original's title tune under those pictures (on = as the original).
        rows.Add(Described("options.title_tune", OnOff(g.TitleTune), _ => Set(() => g.TitleTune = !g.TitleTune)));
        if (game is GameId.P1 or GameId.P3)
        {
            // s160: the A500's output filters on the Amiga's Paula sounds (on = as the A500).
            rows.Add(Described("options.a500_filter", OnOff(g.A500Filter), _ => Set(() => g.A500Filter = !g.A500Filter)));
        }
        if (game is GameId.P3 or GameId.P1)
        {
            // s117: the Amiga's combat field, bank and trade windows (on = as the original), and its Speed menu; I's
            // combat screen (s132).
            rows.Add(Described("options.original_screens", OnOff(g.OriginalScreens),
                _ => Set(() => g.OriginalScreens = !g.OriginalScreens)));
        }
        if (game is GameId.P3 or GameId.P1 or GameId.P2)
        {
            // s136: II ST's Speed menu (fast 5 / normal 10 / slow 1000 ms a combat line, FORMAT.md 7.3) as COMBAT SPEED.
            rows.Add(Described("options.combat_speed", g.CombatSpeed switch
                {
                    AmigaCombatSpeed.Fast => UiText.S("options.combat_speed.fast"),
                    AmigaCombatSpeed.Slow => UiText.S("options.combat_speed.slow"),
                    _ => UiText.S("options.combat_speed.normal"),
                },
                d => Set(() => g.CombatSpeed = (AmigaCombatSpeed)(Cycle((int)g.CombatSpeed - 1, d, 3) + 1))));
        }
        rows.Add(Described("options.diagonal_moves", OnOff(g.DiagonalMoves), _ => Set(() => g.DiagonalMoves = !g.DiagonalMoves)));
        rows.Add(Described("options.status_lines", OnOff(g.StatusLines), _ => Set(() => g.StatusLines = !g.StatusLines)));
        rows.Add(Described("options.continue_from_list", OnOff(g.ContinueFromList),
            _ => Set(() => g.ContinueFromList = !g.ContinueFromList)));
        // PLAN P3 QoL (D3-028, 12-save-slots.md): numbered save slots beside the autosave; off = the one save.
        rows.Add(Described("options.save_slots", OnOff(g.SaveSlots), _ => Set(() => g.SaveSlots = !g.SaveSlots)));
        rows.Add(Described("options.inventory_screen", OnOff(g.InventoryScreen),
            _ => Set(() => g.InventoryScreen = !g.InventoryScreen)));
        // D-073 QoL: the manual's T)IME LAG + sound question as rows here instead of the T command.
        rows.Add(Described("options.time_lag_where",
            g.TimeLagInOptions ? UiText.S("options.time_lag_where.options") : UiText.S("options.time_lag_where.command"),
            _ => Set(() => g.TimeLagInOptions = !g.TimeLagInOptions)));
        if (g.TimeLagInOptions)
        {
            rows.Add(Described("options.time_lag", _settings.TimeLag.ToString(),
                d => Set(() => _settings.TimeLag = Cycle(_settings.TimeLag, d, 10))));
            rows.Add(Described("options.sound", OnOff(_settings.Sound), _ => Set(() => _settings.Sound = !_settings.Sound)));
        }
        // PLAN P3 QoL: the colorblind palette (red-green), a remap of the finished frame; off = the original colours.
        rows.Add(Described("options.colorblind", OnOff(g.ColorblindPalette),
            _ => Set(() => g.ColorblindPalette = !g.ColorblindPalette)));
        if (game is GameId.P1 or GameId.P2 or GameId.P3)
        {
            // PLAN P3 QoL (s172): the dungeon auto-map's overview box (Tab flips it in game); IV's screen has none.
            rows.Add(Described("options.auto_map", OnOff(g.AutoMap), _ => Set(() => g.AutoMap = !g.AutoMap)));
        }
        if (game is GameId.P2)
        {
            // PLAN P3 QoL (D2-014): the rulebook's terrain key beside the wilderness map; off = the ST (no key).
            rows.Add(Described("options.terrain_key", OnOff(g.TerrainKey), _ => Set(() => g.TerrainKey = !g.TerrainKey)));
        }
        if (game is GameId.P4)
        {
            // PLAN P3 QoL (s115): a key ends the Mystic's counted loops; off = P4.X (no keys until the wait).
            rows.Add(Described("options.mystic_skip", OnOff(g.MysticSkip), _ => Set(() => g.MysticSkip = !g.MysticSkip)));
        }

        if (game is GameId.P3 or GameId.P4)
        {
            // QoL (user s180, D3-074): the manual's town letters in every game; I and II have them already.
            rows.Add(Described("options.common_town_keys", OnOff(g.CommonTownKeys),
                _ => Set(() => g.CommonTownKeys = !g.CommonTownKeys)));
        }

        var locked = _runningOptions is not null;
        var rules = _runningOptions ?? g.NewGameOptions(game);
        rows.Add(new(locked ? UiText.S("options.rules_locked") : UiText.S("options.rules_new_games"), null, null, Header: true));

        var presets = Enum.GetValues<OptionPreset>();
        rows.Add(Described("options.preset", UiText.S(rules.Preset.LabelId(game, UiText.Table.Contains)),
            locked ? null : d => SetRules(g, rules.WithPreset(presets[Cycle(Array.IndexOf(presets, rules.Preset), d, presets.Length)])))
            with { Locked = locked });
        // 11-guild-treasury.md section 6: the original rules first, then the pure convenience options under QUALITY OF LIFE.
        var qol = rules.Options.Where(o => o.Qol).ToList();
        foreach (var option in rules.Options.Where(o => !o.Qol))
            rows.Add(RuleRow(option));
        if (qol.Count > 0)
        {
            rows.Add(new(UiText.S("options.quality_of_life"), null, null, Header: true));
            foreach (var option in qol)
                rows.Add(RuleRow(option));
        }

        Row RuleRow(RuleOptionDescriptor option)
        {
            var value = rules.Values[option.Id];
            var values = option.Values;
            return new(UiText.S("option." + option.Id), UiText.S($"option.{option.Id}.{value}"),
                locked ? null : d => SetRules(g, rules.With(option.Id, values[Cycle(values.ToList().IndexOf(value), d, values.Count)])),
                Desc: $"option.{option.Id}.desc", Rule: option, Locked: locked);
        }

        var others = FeatureModuleRegistry.All.Where(m => !m.IsDefaultIn(game) && m.CrossGame && m.HomeGame != game).ToList();
        if (others.Count > 0)
        {
            rows.Add(new(UiText.S("options.from_other_games"), null, null, Header: true));
            foreach (var m in others)
            {
                var on = rules.Modules.Contains(m.Id);
                rows.Add(new(UiText.S("module." + m.Id),
                    m.Implemented ? OnOff(on) : UiText.S("options.not_built"),
                    locked || !m.Implemented ? null : _ => SetRules(g, rules.WithModule(m.Id, !on)),
                    Desc: $"module.{m.Id}.desc", Locked: locked));
            }
        }
        return rows;
    }

    /// <summary>A row labelled by string <paramref name="id"/> and described by <c>id.desc</c>.</summary>
    private static Row Described(string id, string? value, Action<int>? change) =>
        new(UiText.S(id), value, change, Desc: id + ".desc");

    /// <summary>
    /// The description box's paragraphs for the row under the cursor: its label, what it does, then for a rules option the
    /// value each preset gives and the <c>DIFFERENCES</c> entry it comes from. Unwrapped; the screen wraps them.
    /// </summary>
    public IReadOnlyList<MenuLine> Detail()
    {
        if (Cursor == 0)
            return [new(UiText.S("options.tab.desc"), MenuLineStyle.Normal)];
        var rows = Rows();
        if (Cursor > rows.Count)
            return [];
        var row = rows[Cursor - 1];
        var lines = new List<MenuLine> { new(row.Label.Trim(), MenuLineStyle.Header) };
        if (row.Desc is { } desc && UiText.Table.Contains(desc))
            lines.Add(new(PadWording.S(desc), MenuLineStyle.Normal)); // s193 (pad fix E31): a .desc.pad twin while the pad is the input
        if (row.Rule is { } rule && Tab is { } game)
        {
            foreach (var preset in new[] { OptionPreset.Manual, OptionPreset.Dos, OptionPreset.Remaster })
                lines.Add(new(UiText.F("options.detail.preset", UiText.S(preset.LabelId(game, UiText.Table.Contains)),
                    UiText.S($"option.{rule.Id}.{rule.ValueFor(preset)}")), MenuLineStyle.Dim));
            if (rule.Difference is { } entry)
                lines.Add(new(UiText.F("options.detail.difference", entry, DifferencesFile(entry)), MenuLineStyle.Dim));
        }
        if (row.Change is null && row.Value is not null)
            lines.Add(new(row.Locked ? UiText.S("options.detail.locked") : UiText.S("options.detail.fixed"), MenuLineStyle.Dim));
        return lines;
    }

    /// <summary>The file a <c>D-</c>, <c>D2-</c>, <c>D3-</c> or <c>D4-</c> entry lives in.</summary>
    private static string DifferencesFile(string entry) => entry[..entry.IndexOf('-')] switch
    {
        "D2" => "DIFFERENCES-II.md",
        "D3" => "DIFFERENCES-III.md",
        "D4" => "DIFFERENCES-IV.md",
        _ => "DIFFERENCES.md",
    };

    private void Change(List<Row> rows, int direction)
    {
        if (Cursor == 0)
        {
            _tab = Cycle(_tab, direction, _tabs.Count);
            _scroll = 0;
            return;
        }
        rows[Cursor - 1].Change?.Invoke(direction);
    }

    /// <summary>The next row Up/Down lands on: the tab row or a row with a value (headers and notes are skipped).</summary>
    private int NextSelectable(List<Row> rows, int direction)
    {
        var count = rows.Count + 1;
        var next = Cursor;
        for (var i = 0; i < count; i++)
        {
            next = Cycle(next, direction, count);
            if (next == 0 || rows[next - 1].Selectable)
                return next;
        }
        return 0;
    }

    /// <summary>
    /// PgUp/PgDn: a page of rows (<see cref="VisibleRows"/>) up or down to the nearest row Up/Down could stop on, the tab
    /// row and the last row being the ends; from an end it wraps to the other, as Up/Down do.
    /// </summary>
    private int PageSelectable(List<Row> rows, int direction)
    {
        var last = LastSelectable(rows);
        if (direction > 0 && Cursor >= last)
            return 0;
        if (direction < 0 && Cursor == 0)
            return last;
        var target = Math.Clamp(Cursor + direction * VisibleRows, 0, last);
        var stop = direction > 0 ? Cursor : 0; // up: down to the tab row; down: not back past the cursor
        for (var r = target; r > stop; r--)
            if (rows[r - 1].Selectable)
                return r;
        return direction > 0 ? last : 0;
    }

    /// <summary>The last row Up/Down can stop on (the tab row when the tab has none).</summary>
    private static int LastSelectable(List<Row> rows)
    {
        for (var r = rows.Count; r > 0; r--)
            if (rows[r - 1].Selectable)
                return r;
        return 0;
    }

    private void Set(Action change)
    {
        change();
        Changed = true;
    }

    private void SetRules(GameSettings g, GameOptions next) => Set(() => g.SetNewGameOptions(next));

    private static int Cycle(int index, int direction, int count) => ((index + direction) % count + count) % count;

    private static string OnOff(bool on) => on ? UiText.S("options.on") : UiText.S("options.off");
}

/// <summary>Where the Options screen's list and description box sit on the 320×200 screen.</summary>
public static class OptionsLayout
{
    /// <summary>The tab row's top; the rows follow 10 pixels apart, then the "..." line.</summary>
    public const int ListTop = 27;

    public const int DetailX = 10;
    public const int DetailTop = ListTop + (OptionsScreen.VisibleRows + 2) * 10;
    public const int DetailWidth = 300;
    public const int DetailHeight = 185 - DetailTop;

    /// <summary>
    /// The help line under the box. s183 live pass: one pixel higher (and the box one shorter), so the line's glyphs no
    /// longer sit on the screen frame's bottom edge (they ran to y 194, the edge is 195).
    /// </summary>
    public const int HelpY = 187;

    /// <summary>Text lines the box shows at once.</summary>
    public const int DetailLines = (DetailHeight - 3) / PixelFont.LineHeight;

    /// <summary>Text columns, leaving room for the scroll bar.</summary>
    public const int DetailColumns = (DetailWidth - 12) / PixelFont.Advance;
}
