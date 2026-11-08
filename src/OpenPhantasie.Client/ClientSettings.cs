using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>Logical actions remappable to keyboard / gamepad (Phase 7).</summary>
public enum GameAction
{
    /// <summary>Esc: back out to the parent menu, or open the game menu (<see cref="GameMenuDialog"/>) where there is none.</summary>
    GameMenu,
    Confirm,
    Cancel,
    MoveNorth,
    MoveSouth,
    MoveWest,
    MoveEast,
    TurnLeft,
    TurnRight,
    OpenGuildDepart,
    ToggleTown,
    Inn,
    Mystic,
    Fight,
    BankWithdraw,
    BankDeposit,
    Save,
    Load,
    ToggleAutoMap,
    ToggleInventory,
    ToggleBestiary,
    TimeLag,
    Look,
    FastForwardHold,
    CycleScale,
    DosExport,
    DosImport,
    CastSpell,
    Inspect,
    UsePotion,
    Health,
    GuildAdd,
    GuildDrop,
    GuildNew,
    GuildPurge,
    GuildRename,
    GuildList,
    GuildExamine,
    ToggleMapLayout,
}

/// <summary>Persisted Client preferences + remappable bindings.</summary>
public sealed class ClientSettings
{
    public const int VirtualWidth = 320;
    public const int VirtualHeight = 200;

    public int IntegerScale { get; set; } = 3;

    /// <summary>Open the window maximized (the picture still scales by whole steps, letterboxed). F11 goes back to a scaled window.</summary>
    public bool StartMaximized { get; set; } = true;
    /// <summary>
    /// The manual's T)IME LAG (wilderness command, D-073): 0 is the fastest melee, 9 the slowest. The manual gives no
    /// default; DOS's own Speed menu starts on "normal", so the remaster starts in the middle.
    /// </summary>
    public int TimeLag { get; set; } = DefaultTimeLag;

    public const int DefaultTimeLag = 5;

    /// <summary>
    /// Pause before each combat round is shown, in ms: <c>15 · lag²</c> (remaster curve, the manual gives no times).
    /// 0 shows the round as soon as the orders are in; 5 ≈ 375 ms; 9 ≈ 1.2 s.
    /// </summary>
    public static double RoundDelayMs(int timeLag) => 15.0 * Math.Clamp(timeLag, 0, 9) * Math.Clamp(timeLag, 0, 9);
    public bool AutoMapVisible { get; set; } = true;

    /// <summary>
    /// The Amiga map-only screen (P2): the whole 26×20 region at (4,15), or the dungeon level's 5×4 glyphs at (10,16)
    /// with the compass at (214,68). Off (default, D-077 manual) = the map with the party pictures and the command list
    /// beside it. F2 switches. Renamed from <c>fullScreenMap</c> so a settings file written before D-077 starts on the
    /// manual's screen.
    /// </summary>
    public bool MapOnly { get; set; }

    /// <summary>
    /// Per-game settings (T5.0, <c>09-startup-options.md</c> §3, §6), keyed by game prefix (<c>p1</c> …): presentation
    /// switches and the rules new games start with. <see cref="Game"/> fills in a missing entry.
    /// </summary>
    public Dictionary<string, GameSettings> Games { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The settings of <paramref name="game"/>, created with the defaults when the file has none.</summary>
    public GameSettings Game(GameId game)
    {
        if (!Games.TryGetValue(game.Prefix(), out var g))
            Games[game.Prefix()] = g = new GameSettings();
        return g;
    }

    /// <summary>Settings files before T5.0 kept I's two QoL switches at the top level; read once into <c>games.p1</c>.</summary>
    [JsonPropertyName("numpadDiagonals")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyNumpadDiagonals { get; set; }

    /// <inheritdoc cref="LegacyNumpadDiagonals"/>
    [JsonPropertyName("statusLines")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyStatusLines { get; set; }

    /// <summary>Town Option "Auto save" (DOS <c>[0x3092]</c>, starts off): leaving town saves the game.</summary>
    public bool AutoSave { get; set; }

    /// <summary>Town Option "Sound" (DOS starts "+Sound"): I's own in-game sound question; it also switches III Amiga's sound effects (s110: sound 15 on the reward and combat flashes; the Amiga itself has no switch, 0xb126). The title tune and III's reward tune have their own rules (<see cref="GameSettings.TitleTune"/>).</summary>
    public bool Sound { get; set; } = true;

    /// <summary>Last folder used for DOS save import/export (P1.4); empty = <see cref="DosSaveDialog.DefaultFolder"/>.</summary>
    public string DosSaveFolder { get; set; } = "";
    public Dictionary<string, string> KeyBindings { get; set; } = DefaultKeyBindings();

    /// <summary>
    /// Bumped when default bindings change meaning: a file written with an older version gets <see cref="ResetOnUpgrade"/>
    /// put back to the defaults. 0 = a file from before versioning; 2 = D-076 (the manual's N/3 E S W moves and I / U
    /// commands replace WASD, P and K). Fresh settings get the current version from <see cref="LoadOrDefault"/>.
    /// </summary>
    public int BindingsVersion { get; set; }

    public const int CurrentBindingsVersion = 2;

    private static readonly GameAction[] ResetOnUpgrade =
    [
        GameAction.MoveNorth, GameAction.MoveSouth, GameAction.MoveWest, GameAction.MoveEast,
        GameAction.TurnLeft, GameAction.TurnRight, GameAction.Inspect, GameAction.UsePotion,
    ];

    public static Dictionary<string, string> DefaultKeyBindings() => new()
    {
        [nameof(GameAction.GameMenu)] = nameof(Keys.Escape),
        [nameof(GameAction.Confirm)] = nameof(Keys.Enter),
        [nameof(GameAction.Cancel)] = nameof(Keys.Back),
        // D-076 (manual WILDERNESS TRAVEL): N north (3 too, fixed in InputMap), E east, S south, W west; arrows and the
        // numpad also move. Turning has no key in the manual, so it starts unbound.
        [nameof(GameAction.MoveNorth)] = nameof(Keys.N),
        [nameof(GameAction.MoveSouth)] = nameof(Keys.S),
        [nameof(GameAction.MoveWest)] = nameof(Keys.W),
        [nameof(GameAction.MoveEast)] = nameof(Keys.E),
        [nameof(GameAction.TurnLeft)] = nameof(Keys.None),
        [nameof(GameAction.TurnRight)] = nameof(Keys.None),
        [nameof(GameAction.OpenGuildDepart)] = nameof(Keys.G),
        [nameof(GameAction.ToggleTown)] = nameof(Keys.T),
        [nameof(GameAction.TimeLag)] = nameof(Keys.T), // wilderness only (T)IME LAG); T leaves town buildings
        [nameof(GameAction.Look)] = nameof(Keys.L), // dungeon only (L)OOK)
        [nameof(GameAction.Inn)] = nameof(Keys.I),
        [nameof(GameAction.Mystic)] = nameof(Keys.M),
        [nameof(GameAction.Fight)] = nameof(Keys.Space),
        [nameof(GameAction.BankWithdraw)] = nameof(Keys.Y),
        [nameof(GameAction.BankDeposit)] = nameof(Keys.U),
        [nameof(GameAction.Save)] = nameof(Keys.F5),
        [nameof(GameAction.Load)] = nameof(Keys.F9),
        [nameof(GameAction.ToggleAutoMap)] = nameof(Keys.Tab),
        [nameof(GameAction.ToggleInventory)] = nameof(Keys.V),
        [nameof(GameAction.ToggleBestiary)] = nameof(Keys.B),
        [nameof(GameAction.FastForwardHold)] = nameof(Keys.LeftShift),
        [nameof(GameAction.CycleScale)] = nameof(Keys.F11),
        [nameof(GameAction.DosExport)] = nameof(Keys.F6),
        [nameof(GameAction.DosImport)] = nameof(Keys.F7),
        [nameof(GameAction.CastSpell)] = nameof(Keys.C),
        [nameof(GameAction.Inspect)] = nameof(Keys.I), // wilderness / dungeon only (I)NSPECT); I shows the party in town
        [nameof(GameAction.UsePotion)] = nameof(Keys.U), // wilderness / dungeon only (U)SE POTION); U uses an item in town
        [nameof(GameAction.Health)] = nameof(Keys.H),
        [nameof(GameAction.GuildAdd)] = nameof(Keys.A), // Guild only (A)DD MEMBER)
        [nameof(GameAction.GuildDrop)] = nameof(Keys.D), // Guild only (D)ROP MEMBER)
        [nameof(GameAction.GuildNew)] = nameof(Keys.N), // Guild only (N)EW MEMBER)
        [nameof(GameAction.GuildPurge)] = nameof(Keys.P), // Guild only (P)URGE)
        [nameof(GameAction.GuildRename)] = nameof(Keys.R), // Guild only (R)ENAME)
        [nameof(GameAction.GuildList)] = nameof(Keys.L), // Guild only (L)IST MEMBERS); L leaves town on the town bar
        [nameof(GameAction.GuildExamine)] = nameof(Keys.I), // III Guild only (DOS III guild_menu item 6 "Inspect", s160)
        [nameof(GameAction.ToggleMapLayout)] = nameof(Keys.F2),
    };

    public Keys GetKey(GameAction action)
    {
        var name = action.ToString();
        // Actions added after a settings file was written fall back to their default binding.
        if ((KeyBindings.TryGetValue(name, out var keyName) || DefaultKeyBindings().TryGetValue(name, out keyName)) &&
            Enum.TryParse<Keys>(keyName, ignoreCase: true, out var key))
            return key;
        return Keys.None;
    }

    public void SetKey(GameAction action, Keys key) =>
        KeyBindings[action.ToString()] = key.ToString();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenPhantasie",
            "settings.json");

    /// <summary>The folder of settings, saves and printouts.</summary>
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenPhantasie");

    /// <summary>The remaster save of <paramref name="game"/> (T5.0: one per game, <c>savegame-p1.json</c> …).</summary>
    public static string SavePathFor(GameId game) => Path.Combine(DataDirectory, $"savegame-{game.Prefix()}.json");

    /// <summary>
    /// Before T5.0 the only save was I's <c>savegame.json</c>; rename it to I's per-game name once (the file is unchanged).
    /// </summary>
    public static void AdoptSingleSave()
    {
        try
        {
            var old = Path.Combine(DataDirectory, "savegame.json");
            var p1 = SavePathFor(GameId.P1);
            if (File.Exists(old) && !File.Exists(p1))
                File.Move(old, p1);
        }
        catch (IOException)
        {
            // Leave it; Continue then reports no save.
        }
    }

    public static string QoLPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenPhantasie",
            "qol.json");

    public static ClientSettings LoadOrDefault()
    {
        try
        {
            if (File.Exists(SettingsPath) && FromJson(File.ReadAllText(SettingsPath)) is { } s)
                return s;
        }
        catch
        {
            // Fall through to defaults.
        }

        return new ClientSettings { BindingsVersion = CurrentBindingsVersion };
    }

    /// <summary>Settings as a start reads them: missing bindings filled in, scale and time lag clamped.</summary>
    public static ClientSettings? FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<ClientSettings>(json, JsonOptions);
        if (s is null)
            return null;
        var defaults = DefaultKeyBindings();
        if (s.BindingsVersion < CurrentBindingsVersion)
        {
            foreach (var action in ResetOnUpgrade)
                s.KeyBindings[action.ToString()] = defaults[action.ToString()];
            s.BindingsVersion = CurrentBindingsVersion;
        }
        foreach (var (k, v) in defaults)
            s.KeyBindings.TryAdd(k, v);
        if (s.IntegerScale < 1) s.IntegerScale = 1;
        if (s.IntegerScale > 8) s.IntegerScale = 8;
        s.TimeLag = Math.Clamp(s.TimeLag, 0, 9);
        s.Games = new Dictionary<string, GameSettings>(s.Games ?? new(), StringComparer.Ordinal);
        if (s.LegacyNumpadDiagonals is bool diagonals)
            s.Game(GameId.P1).DiagonalMoves = diagonals;
        if (s.LegacyStatusLines is bool status)
            s.Game(GameId.P1).StatusLines = status;
        s.LegacyNumpadDiagonals = s.LegacyStatusLines = null;
        return s;
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}

/// <summary>One game's settings (T5.0 §3): presentation, changed any time, and the rules new games are locked to.</summary>
public sealed class GameSettings
{
    /// <summary>The graphics pack id (<c>amiga</c>, <c>dos-cga</c>, …); null or absent = the game's default pack.</summary>
    public string? Graphics { get; set; }

    /// <summary>
    /// The title splash (s102, <see cref="TitleSplash"/>): picking the game shows its start-up pictures as the original
    /// did (the pack's <c>title_sequence</c>) before its first screen. On by default, as the originals; off goes straight
    /// to the game. A presentation switch.
    /// </summary>
    public bool TitleScreen { get; set; } = true;

    /// <summary>
    /// The title tune (s108): the splash plays the tune the original played under its start-up pictures (the pack's
    /// <c>title_sequence.tune</c>; I <c>tamb.dat</c>, II's Dosound, III <c>D/m0</c>, IV's OPMDRV song 0 on the YM2151). On by
    /// default, as the originals. Off = silent pictures, each held as long as with the tune (I's cover1 lasts the tune's
    /// length either way). A presentation switch; the Phantasie I town "Sound" option is the game's own and does not gate it.
    /// </summary>
    public bool TitleTune { get; set; } = true;

    /// <summary>
    /// s160 (user, s159): the Amiga's sounds (I and III: title tune, sound effects, III's reward tune; everything on
    /// <see cref="Audio.PaulaMixer"/>) through the A500's analogue output filters (<see cref="Audio.A500AudioFilter"/>, the
    /// LED filter in, as the originals leave it). On by default, as the A500 sounds; off = Paula's raw output, brighter.
    /// A presentation switch, shown on the I and III tabs; a sound already playing keeps its setting.
    /// </summary>
    public bool A500Filter { get; set; } = true;

    /// <summary>
    /// QoL (D-076, off = manual's four directions): numpad 7 / 9 / 1 / 3 (or Home / PgUp / End / PgDn with Num Lock off)
    /// step diagonally on the wilderness and dungeon maps. A diagonal is two ordinary steps (north/south first, the other
    /// order if that one is blocked); the second is skipped when the first stops on anything but a plain move. On by
    /// default, as the user asked.
    /// </summary>
    public bool DiagonalMoves { get; set; } = true;

    /// <summary>
    /// QoL (D-077, off = manual): the remaster's status line (mode, gold, party, scale, region, position, days out) and
    /// the key help line on the wilderness and dungeon screens. A dialog's own help line shows either way.
    /// </summary>
    public bool StatusLines { get; set; }

    /// <summary>
    /// QoL (PLAN P3, off = manual D-069 "GUILD NUMBER OF THE CHARACTER?"): Continue lists the save's parties (leader, size,
    /// where) to pick with Up/Down + Return; a last row still asks for a Guild number (a character in no party). A
    /// presentation switch, not a rule: no save is loaded when Continue asks.
    /// </summary>
    public bool ContinueFromList { get; set; }

    /// <summary>
    /// QoL (PLAN P3 / 09 T2, off = the original screens): the inventory key (V) opens the paperdoll + stash screen
    /// (<see cref="InventoryScreen"/>) instead of the item overlay. Where items may be moved is the save's rules option
    /// <c>p1.stash-access</c> (Core <see cref="OpenPhantasie.Core.GameSession.InventoryAccess"/>); elsewhere the screen is
    /// read-only. A presentation switch: D)ISTRIBUTE and the rest are unchanged either way.
    /// </summary>
    public bool InventoryScreen { get; set; }

    /// <summary>
    /// QoL (PLAN P3, D-073; off = the manual's wilderness / dungeon <b>T</b>)IME LAG command): the time lag (0–9) and the
    /// sound switch are rows of this game's Options tab, changed any time (also in game, from the Esc menu), and <b>T</b> is
    /// no command. A presentation switch, not a rule: melee speed and sound don't change the game. The values stay the
    /// top-level <see cref="ClientSettings.TimeLag"/> / <see cref="ClientSettings.Sound"/> either way.
    /// </summary>
    public bool TimeLagInOptions { get; set; }

    /// <summary>
    /// QoL (PLAN P3, off = the original colours): the colorblind palette (<see cref="ColorblindFilter"/>, red-green:
    /// deuteranopia and protanopia). The status/HP colours, the map colours and the confusable art colours are remapped on
    /// the finished frame from <c>GameData/shared/colorblind.json</c>. A presentation switch; off leaves every pixel as
    /// drawn.
    /// </summary>
    public bool ColorblindPalette { get; set; }

    /// <summary>
    /// The original's own screens where the remaster has rebuilt them (s117; III Amiga: the combat field with one line at a
    /// time, the Bank and Trade windows). On by default, as the original; off = the remaster's screens (cards and rank
    /// list, a round's lines at once; the remaster's bank and trade dialogs). A presentation switch.
    /// </summary>
    public bool OriginalScreens { get; set; } = true;

    /// <summary>
    /// The original's Speed menu (Fast / Normal / Slow) for combat's waits; Normal as the original starts. III Amiga's and I
    /// Amiga's combat field, II ST's line-by-line round (s136: 5 / 10 / 1000 ms a line, FORMAT.md 7.3).
    /// </summary>
    public AmigaCombatSpeed CombatSpeed { get; set; } = AmigaCombatSpeed.Normal;

    /// <summary>The rules a new game starts with (preset, values, modules); null = Original (manual).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameOptionsSnapshot? NewGameRules { get; set; }

    /// <summary>The rules a new game of <paramref name="game"/> is locked to (lenient, <see cref="GameOptions.Restore"/>).</summary>
    public GameOptions NewGameOptions(GameId game) =>
        NewGameRules?.ToOptions(game) ?? GameOptions.ForPreset(game);

    public void SetNewGameOptions(GameOptions options) => NewGameRules = GameOptionsSnapshot.From(options);
}
