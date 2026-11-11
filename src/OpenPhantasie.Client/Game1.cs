using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer;

namespace OpenPhantasie.Client;

/// <summary>
/// Thin MonoGame shell over <see cref="GameSession"/>. Draws Amiga sprites on a
/// 320×200 virtual canvas with an on-screen HUD (Phase 7 QoL controls).
/// </summary>
public partial class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private VirtualScreen? _screen;
    private AssetCatalog? _assets;
    private GraphicsPackCatalog? _packs;
    private GameDataRepository? _data;
    private GameSession? _session;
    private OverworldTraversal? _overworldView;
    private DungeonTraversal? _dungeonView;
    private ClientSettings _settings = null!;
    private InputMap _input = null!;
    private QoLProgress _qol = null!;
    private CombatPresenter _combatUi = null!;
    private readonly CombatPad _combatPad = new(); // s182: the pad's pick in a fight
    private string _message = "";
    private string _help = "";
    private readonly MessageBandClock _messageBand = new(); // when the town screen's band shows the message
    private readonly AmigaMessageBox _amigaMessage = new(); // III Amiga's window 7 for town / guild lines (s135)
    private readonly P3DamageReportBox _damageReport = new(); // III's Damage Report window (s165, Amiga 0x1d4ea)
    private readonly P2AlertBox _p2Alert = new(); // s158: II ST's form_alert boxes (FORMAT.md 7.4)
    private readonly P2FieldBox _p2FieldBox = new(); // s165: II ST's timed box in the field (0xa18c, tree 40)
    private double _updateSeconds; // Update's game clock (the town door flash)
    // III's open door while the party goes in (Amiga 0xacc8, s102): which door, until when, then what runs.
    private (string Door, double Until, Action? Then)? _doorFlash;
    private bool _showInventory;
    private bool _showBestiary;
    private bool _inventoryCompare;
    private int _inventoryCursor;
    private int _townCursor;
    private int? _trackedCombatHash;
    private SessionPrompts _prompts = null!;
    private readonly DefeatSceneClock _defeatClock = new(); // II's sinking door (DefeatScene)
    private readonly DefeatScenePager _defeatPager = new(); // II's scene text, one line at a time on the bottom line
    private IReadOnlyList<string>? _defeatSceneLines; // the lines the scene showed last frame (null: no scene)
    private VictoryScreen? _victory; // I Amiga's victory screen running (s127), and when it began
    private double _victoryStart;
    private VictoryScreenView? _victoryView;
    private int _victorySoundsStarted; // s128: the victory screen's sounds started so far
    private bool _defeatSceneHeld; // the outcome page holds its speech and fates until the face shows (LinesShown)
    private DosSaveDialog _dosDialog = null!;
    private readonly CastDialog _castDialog = new();
    private readonly ContinueDialog _continueDialog = new();
    private GameMenuDialog _gameMenu = null!;
    private InspectDialog _inspectDialog = null!;
    private readonly SpellPicker _spellPicker = new(); // DOS: one cursor for every spell picker (COM 0x139c)
    private readonly UsePotionDialog _potionDialog = new();
    private readonly HealthDialog _healthDialog = new();
    private readonly TimeLagDialog _timeLagDialog = new();
    private readonly LearnSpellDialog _learnDialog = new();
    private readonly TransportDialog _transportDialog = new();
    private GuildAddDialog _guildAddDialog = null!;
    private readonly GuildDropDialog _guildDropDialog = new();
    private readonly GuildNewDialog _guildNewDialog = new(s => LineageHall.Load(s.Game, ClientSettings.ReadSavePathFor)); // T5 lineage
    private readonly GuildPickDialog _guildPickDialog = new();
    private readonly GuildListDialog _guildListDialog = new();
    private readonly GuildPartyPickDialog _guildWhoDialog = new();
    private UseItemDialog _useItemDialog = null!;
    private IReadOnlyList<ScrollFrameLayout.Edge>? _scrollEdges;
    private IReadOnlyList<ScrollFrameLayout.Edge>? _rewardEdges;
    private IReadOnlyList<ScrollFrameLayout.Edge>? _pageEdges; // III's event page: its frame, kept while the page lasts
    private RewardScene? _rewardEdgesFor;
    private readonly RewardSceneClock _rewardClock = new(); // III's reward scene: its flashes and pages (s109)
    private Audio.TunePlayer? _rewardTune; // D/M1, from the god's picture to the scene's end (Amiga 0x1efe8 / 0x1f24a)
    private RewardScene? _rewardTuneFor;
    private readonly Audio.SoundEffectPlayer _effects = new(); // III Amiga's sound effects (s110): one run at a time
    private object? _effectsFor; // the flash run whose sounds play (a reward run or a combat round's)
    private int _effectsCombatSerial;
    private int _scrollEdgesRead;
    private readonly DistributeDialog _distributeDialog = new();
    private readonly int? _testLevel;
    private readonly GameId _testGame;
    private readonly int? _p4Map, _p4Town, _p4Ending;
    private readonly GameId? _titlePreview;

    /// <summary>The title splash while it shows (s102), its own canvas and pack textures, and what follows it.</summary>
    private TitleSplash? _splash;
    private VirtualScreen? _splashScreen;
    private AssetCatalog? _splashAssets;
    private StringTable? _splashText;
    private Audio.TunePlayer? _splashTune;
    /// <summary>
    /// s157: a <see cref="TitleTune.PlaysOn"/> tune after its splash (III Amiga's D/M0 over the Initial Menu), the game it
    /// belongs to, and whether that game raised a sound effect this frame (<see cref="UpdateGameSounds"/>).
    /// </summary>
    private Audio.TunePlayer? _carriedTune;
    private GameId _carriedTuneGame;
    private bool _gameSoundRaised;
    private double? _carryCutAtMs; // s158: when a started run's first sound cuts the carried tune (TitleTuneCarry.CutAtMs)
    private Action? _afterSplash;
    /// <summary>s151: what a pick of a saved game on the splash's start menu runs (IV's <c>_disk(0)</c>), or null.</summary>
    private Action<int>? _afterSplashLoad;

    /// <summary>Phantasie IV's location-map screen while a IV game runs (T6.3; IV has no <see cref="GameSession"/>).</summary>
    private P4DungeonScreen? _p4;
    private readonly MainMenuScreen _mainMenu = new();
    private OptionsScreen _options = null!;
    private ImportScreen _import = null!;
    private UpdateScreen _update = null!;
    /// <summary>s174: the start-up update check (CHECK UPDATES AT START, release builds only); its result opens the update screen.</summary>
    private Task<Updates.UpdateCheckResult>? _launchCheck;
    private bool _optionsFromGameMenu; // Esc from in-game Options goes back to the game menu
    private string _lineBeforeGameMenu = ""; // s183: the message line the game menu's title replaced
    private readonly Dictionary<GameId, IReadOnlyList<(string Id, string Name)>> _packLists = new();

    /// <summary>The town's current shopper (manual p.11: "who is shopping? Give the character's number"; 1–6 changes it).</summary>
    /// <summary>The current shopper = DOS's one member-list cursor (`towndlg.who_will_withdraw` / SHOP? / USE?, s58).</summary>
    private readonly MemberCursor _members = new();
    private WithdrawDialog _withdrawDialog = null!;
    private readonly DosDepositDialog _dosDepositDialog = new();
    private readonly TradeDialog _tradeDialog = new();
    private readonly AmigaBankDialog _amigaBank = new();
    private readonly TreasuryDialog _treasuryDialog = new(); // P3 QoL: the Bank under the Guild treasury (11-guild-treasury.md)
    private string _promptNote = "";
    private IReadOnlyList<string> _promptNoteLines = []; // the lines _promptNote joins (s140: a map box's line leaves it)

    /// <summary>The feedback bar's message and help as an open prompt last set them; null once put back (s134).</summary>
    private (string Message, string Help)? _promptFeedback;
    private List<string> _defeatCause = new(); // the step's lines shown above `core.defeat.your_party_has_been_defeated`
    private long _fightMark; // log position before the step that started the current fight (AfterFightEnded)
    private readonly PromptMenu _promptMenu = new();
    private List<(int Id, string Name, int Qty, int Price)> _townStock = new();
    private readonly TownMenu _townMenu = new();
    private GameMode? _lastMode;

    /// <param name="testLevel"><see cref="TestParty"/>: start with the six-member test party at this level instead of an
    /// empty roster; null = a normal start.</param>
    /// <param name="testGame">The game the test party starts in (<c>--test-game</c>; I by default).</param>
    /// <param name="p4Map"><see cref="P4Preview"/>: start IV's preview party in this location map; null = a normal start.</param>
    /// <param name="p4Town"><see cref="P4Preview"/>: start IV's preview party arriving in this town (<c>--p4-town</c>).</param>
    /// <param name="titlePreview"><see cref="TitlePreview"/>: open on this game's title splash (<c>--title-screen</c>).</param>
    /// <param name="p4Ending"><see cref="P4Preview"/>: start IV's preview party entering Pendragon into this ending (<c>--p4-ending</c>).</param>
    public Game1(int? testLevel = null, GameId testGame = GameId.P1, int? p4Map = null, int? p4Town = null,
        GameId? titlePreview = null, int? p4Ending = null)
    {
        _p4Ending = p4Ending;
        _titlePreview = titlePreview;
        _testLevel = testLevel;
        _testGame = testGame;
        _p4Map = p4Map;
        _p4Town = p4Town;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = UiText.S("game.openphantasie");
        Window.AllowUserResizing = true;
        _graphics.PreferredBackBufferWidth = ClientSettings.VirtualWidth * 3;
        _graphics.PreferredBackBufferHeight = ClientSettings.VirtualHeight * 3;
        _effects.A500Filter = () => _session is null || A500FilterOn(_session.Game); // s160: AMIGA AUDIO FILTER
    }

    /// <summary>s160: the game's AMIGA AUDIO FILTER switch (<see cref="GameSettings.A500Filter"/>; on before settings load).</summary>
    private bool A500FilterOn(GameId game) => _settings?.Game(game).A500Filter ?? true;

    protected override void Initialize()
    {
        // s158: the player's import (10-importer.md 4.3) before anything reads game data.
        var importRoot = Path.Combine(ClientSettings.DataDirectory, "import");
        GameDataLocator.UseImport(importRoot);
        _settings = ClientSettings.LoadOrDefault();
        _townMenu.AutoSave = _settings.AutoSave;
        ClientSettings.SaveSlotsOn = g => _settings.Game(g).SaveSlots; // QoL SAVE SLOTS: what other games' reads use
        _transportDialog.BeforePick = FreezeForTransport; // s158: II ST fades the frame with the picker
        _townMenu.Sound = _settings.Sound;
        _input = new InputMap(_settings);
        _qol = QoLProgress.LoadOrDefault();
        _combatUi = new CombatPresenter(_settings);
        _dosDialog = new DosSaveDialog(_settings);
        _gameMenu = new GameMenuDialog();
        _options = new OptionsScreen(_settings, PackList);
        _import = new ImportScreen(importRoot) { OnImported = () => AfterImport(importRoot) };
        Window.FileDrop += (_, e) => _import.AddPaths(e.Files);
        var updates = Updates.UpdateService.ForThisBuild();
        _update = new UpdateScreen(c => updates.CheckAsync(c), (release, progress) => InstallUpdate(updates, release, progress),
            Updates.AppVersion.IsRelease);
        if (_settings.CheckUpdatesAtStart && Updates.AppVersion.IsRelease && _testLevel is null && _p4Map is null
            && _p4Town is null && _p4Ending is null && _titlePreview is null)
            _launchCheck = Task.Run(() => updates.CheckAsync());
        _withdrawDialog = new WithdrawDialog(_members);
        Window.TextInput += (_, e) =>
        {
            _dosDialog.OnTextInput(e.Character);
            _import.OnTextInput(e.Character);
            _p4?.OnTextInput(e.Character);
        };
        ClientSettings.AdoptSingleSave();

        // T5.0: the main menu first; --test-party goes straight to I (or the --test-game game).
        if (_testLevel is not null && _testGame == GameId.P4)
            // IV has no GameSession: its test party is the preview party (P4Preview) arriving in Pendragon. --test-level
            // does not train it (IV's creation record sets the level).
            StartPhantasie4(() => P4Preview.StartInTown(P4TownLoop.Pendragon, _settings.Game(GameId.P4).NewGameOptions(GameId.P4)));
        else if (_testLevel is not null)
            StartGame(_testGame);
        else if (_p4Map is int map)
            StartPhantasie4(() => P4Preview.Start(map, _settings.Game(GameId.P4).NewGameOptions(GameId.P4),
                at: P4Preview.ParseSquare(Environment.GetCommandLineArgs())));
        else if (_p4Town is int town)
            StartPhantasie4(() => P4Preview.StartInTown(town, _settings.Game(GameId.P4).NewGameOptions(GameId.P4)));
        else if (_p4Ending is int ending)
            StartPhantasie4(() => P4Preview.StartEnding(ending, _settings.Game(GameId.P4).NewGameOptions(GameId.P4)));
        else if (_titlePreview is null && !MainMenuScreen.Games.Any(MainMenuScreen.IsPlayable))
            _import.Open(firstRun: true); // s158: nothing imported yet: ask for the player's copy first
        else if (_titlePreview is null && ImportRunner.StaleVersions(importRoot).Count > 0)
            _import.OpenRefresh(); // s163: versions built by an older recipe are re-run from their files (4.4)
        base.Initialize();
        // After base.Initialize (LoadContent): the splash needs the graphics device.
        if (_titlePreview is GameId preview)
            BeginSplash(preview, force: true, preview == GameId.P4 ? StartNewPhantasie4 : () => StartGame(preview),
                preview == GameId.P4 ? LoadSavedPhantasie4 : null);
    }

    /// <summary>
    /// s174: the start-up check's result once, if it found a newer release with a build for this machine. A failed check
    /// (no connection, the site down) says nothing: the player didn't ask.
    /// </summary>
    private Updates.UpdateCheckResult? LaunchCheckFound()
    {
        if (_launchCheck is not { IsCompleted: true } done)
            return null;
        _launchCheck = null;
        return done.IsCompletedSuccessfully && done.Result.Outcome == Updates.UpdateCheckOutcome.Available ? done.Result : null;
    }

    /// <summary>s174: download <paramref name="release"/> into the install's work folder and put it over the install.</summary>
    private static async Task InstallUpdate(Updates.UpdateService updates, Updates.UpdateRelease release, IProgress<double> progress)
    {
        var root = Updates.UpdateInstaller.InstallRoot(AppContext.BaseDirectory);
        if (!Updates.UpdateInstaller.CanWrite(root))
            throw new UnauthorizedAccessException(UiText.F("options.update.read_only", root));
        var program = Environment.ProcessPath ?? throw new InvalidOperationException("no program path");
        var archive = Path.Combine(root, Updates.UpdateInstaller.WorkFolder, release.AssetName);
        await updates.DownloadAsync(release, archive, progress).ConfigureAwait(false);
        progress.Report(1.0);
        Updates.UpdateInstaller.Install(archive, root, Path.GetRelativePath(root, program));
    }

    /// <summary>Why the Client is closing, for the exit line (null: the window was closed from outside the game).</summary>
    private string? _exitReason;

    /// <summary>
    /// s180: closes the Client with <paramref name="reason"/> on its exit line (stderr, <see cref="OnExiting"/>), so a
    /// close is never silent: an empty log afterwards means the process was killed (SIGKILL) or crashed natively.
    /// </summary>
    private void ExitFor(string reason)
    {
        _exitReason = reason;
        Exit();
    }

    protected override void OnExiting(object sender, Microsoft.Xna.Framework.ExitingEventArgs args)
    {
        Console.Error.WriteLine($"OpenPhantasie: closing ({_exitReason ?? "window closed"})");
        base.OnExiting(sender, args);
    }

    /// <summary>s174: the new build is in place: start it and close this one (settings are saved as they change).</summary>
    private void RestartAfterUpdate()
    {
        try
        {
            Updates.UpdateInstaller.Relaunch(Environment.ProcessPath!);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            // the files are in place; the player starts it by hand
        }
        ExitFor("restart after update");
    }

    /// <summary>Show IV's screen for the game <paramref name="start"/> builds (T6.3). IV's text is set first: entering a map
    /// already runs its load event, which prints.</summary>
    private void StartPhantasie4(Func<Phantasie4Campaign> start)
    {
        _unifiedTown.Reset();
        _unifiedTownNote = null;
        UiText.Table = UiText.For(GameId.P4);
        _p4 = new P4DungeonScreen(start())
        {
            SlotSaved = slot => SaveSlotExists(GameId.P4, slot), // IV's three slots (12-save-slots.md section 2)
            OriginalSlots = !_settings.Game(GameId.P4).SaveSlots,
        };
        _packs = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4));
        UseGraphicsPack(ChosenPack(GameId.P4)); // T6.4: IV's own pictures (X68000), its 640 × 400 screen
        Window.Title = UiText.S("menu.game.p4");
    }

    /// <summary>A new IV game from the main menu: Pendragon with an empty party (<see cref="Phantasie4World.NewGame"/>).</summary>
    private void StartNewPhantasie4() =>
        StartPhantasie4(() => Phantasie4World.NewGame(_settings.Game(GameId.P4).NewGameOptions(GameId.P4),
            new SeededRandomSource(Environment.TickCount)));

    /// <summary>
    /// s151: the title's Saved Game pick (<c>_disk(0)</c>'s save icon): IV's save as F9 loads it, onto a fresh IV screen. A
    /// save that cannot be read starts a new game with the reason in the note (never a silent new game). QoL SAVE SLOTS:
    /// the icon's slot <paramref name="slot"/> (1–3), either way the SAVE SLOTS switch is (IV keeps its original's three).
    /// </summary>
    private void LoadSavedPhantasie4(int slot)
    {
        var path = P4SlotPath(slot);
        try
        {
            var save = Phantasie4SaveCodec.ReadJsonFile(path);
            StartPhantasie4(() => Phantasie4World.RestoreCampaign(save, new SeededRandomSource(Environment.TickCount)));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException
                                       or KeyNotFoundException or InvalidOperationException
                                       or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            StartNewPhantasie4();
            _p4!.Note = UiText.F("p4.dungeon.load_failed_x", ex.Message);
        }
    }

    /// <summary>F5 / F9 / Esc on IV's screen: IV's own save file beside the other games' (<see cref="Phantasie4SaveCodec"/>).</summary>
    private void RunPhantasie4Request(P4ScreenRequest request)
    {
        switch (request)
        {
            // SAVE SLOTS on: the remaster's picker on the three slots. Off, the screen runs the original's own Save page
            // (_diskmenu(1)) and F9's load page and asks only for the slot picked there.
            case P4ScreenRequest.Save:
                OpenSaveSlots(GameId.P4, SaveSlotMode.Save);
                break;
            case P4ScreenRequest.Load:
                OpenSaveSlots(GameId.P4, SaveSlotMode.Load);
                break;
            case P4ScreenRequest.SaveSlot:
                WriteP4Save(P4SlotPath(_p4!.RequestedSlot), new SaveSlot(GameId.P4, _p4.RequestedSlot, P4SlotPath(_p4.RequestedSlot)));
                break;
            case P4ScreenRequest.LoadSlot:
                LoadP4From(P4SlotPath(_p4!.RequestedSlot));
                break;
            case P4ScreenRequest.MainMenu:
                _p4 = null;
                UseGraphicsPack(null);
                UiText.Table = UiText.For(GameId.P1);
                Window.Title = UiText.S("game.openphantasie");
                break;
            case P4ScreenRequest.Title:
                // s158: _acend(1) clears the party and runs _main again: the title and its _disk(0) menu (New Game / the
                // save). Without a title sequence in the pack: a new game, as the menu's New Game.
                _p4 = null;
                UseGraphicsPack(null);
                if (!BeginSplash(GameId.P4, force: true, StartNewPhantasie4, LoadSavedPhantasie4))
                    StartOrOfferSaves(GameId.P4);
                break;
        }
    }

    /// <summary>
    /// Build <paramref name="game"/>'s session (T5.0 §1) with the game tab's rules for new games and open the game's own
    /// start (I: Pelnor's town menu with an empty party, as before T5.0).
    /// </summary>
    /// <summary>
    /// T5 transfer (<c>transfer.md</c> sections 7 / 8): the running game (II for I → II, I for II → I) is replaced by a new one
    /// under the same locked rules, its roster the source game's converted one
    /// (<see cref="GameSession.TransferFromPhantasieI"/> / <see cref="GameSession.TransferFromPhantasieII"/>); the dialog
    /// shows the count. III's transfer (D3-030) instead adds the kept characters to the running game
    /// (<see cref="GameSession.TransferIntoPhantasieIII"/>).
    /// </summary>
    private void StartTransferredGame(IReadOnlyList<Character?> source)
    {
        if (_dosDialog.TransferIntoIII is { } into) // III adds to the running game (D3-030), no new game
        {
            _dosDialog.ReportTransfer(_session!.TransferIntoPhantasieIII(source, into.From, into.Keep), GameId.P3);
            SetFeedback(_message, _dosDialog.Help);
            return;
        }
        var options = _session!.Options;
        var game = _session.Game;
        ReturnToMainMenu(); // the shell's per-game state goes with the old game, as on Main menu → the game
        _townMenu.Reset();
        StartGame(game, options);
        _dosDialog.ReportTransfer(game == GameId.P1
            ? _session!.TransferFromPhantasieII(source)
            : _session!.TransferFromPhantasieI(source), game);
        SetFeedback(_message, _dosDialog.Help);
    }

    private void StartGame(GameId game, GameOptions? options = null)
    {
        _unifiedTown.Reset();
        _unifiedTownNote = null;
        _data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(game));
        UiText.Table = UiText.For(game);
        _overworldView = _data.CreateTraversal();
        _dungeonView = _data.CreateDungeonTraversal();
        _session = GameSessionFactory.Create(_data, new SeededDosRandomSource(42));
        _prompts = new SessionPrompts(_session);
        _guildAddDialog = new GuildAddDialog(_promptMenu, _data);
        _inspectDialog = new InspectDialog(_promptMenu, _data, _spellPicker);
        _useItemDialog = new UseItemDialog(_data, _members);
        _session.StartNewGame(options ?? _settings.Game(game).NewGameOptions(game));
        _p1SpeedPicked = false;
        if (_testLevel is int level && options is null)
        {
            TestParty.Bootstrap(_session, _data.CreateItemCatalog(), level);
            if (game == GameId.P1 && Environment.GetCommandLineArgs().Contains(TestParty.VictorySwitch))
                TestParty.WinBlackLordFight(_session);
            else if (game == GameId.P1 && Environment.GetCommandLineArgs().Contains(TestParty.DefeatSwitch))
                TestParty.Defeat(_session); // s153: the Amiga's defeat screens (AMIGA SCREENS) or the prompts
            else if (game == GameId.P1 && P1QuestionSwitch(Environment.GetCommandLineArgs()) is { } question)
                OpenP1Question(_session, question); // s154: a map question's requester on demand
            else if (game == GameId.P1 && TestParty.DungeonCell(Environment.GetCommandLineArgs()) is { } cell)
                TestParty.EnterDungeon(_session, cell); // s141: a dungeon step's boxes on demand
            else if (game == GameId.P3 && TestParty.DungeonCell(Environment.GetCommandLineArgs()) is { } p3Cell)
                TestParty.EnterP3Dungeon(_session, p3Cell); // s163: III's dungeon windows (traps, doors) on demand
            else if (game is GameId.P1 or GameId.P2 && TestParty.CombatMonster(Environment.GetCommandLineArgs()) is int monster)
                TestParty.StartFight(_session, monster, TestParty.CombatSurprise(Environment.GetCommandLineArgs()));
            else if (game == GameId.P3 && TestParty.CombatMonster(Environment.GetCommandLineArgs()) is int p3Monster)
                TestParty.StartP3Fight(_session, p3Monster);
            else if (game == GameId.P3 && Environment.GetCommandLineArgs().Contains(TestParty.P3EntryPayoutSwitch))
                TestParty.EnterP3TownWithPool(_session); // s144: the D3-071 "Shares for NAME :" prompts
            else if (game == GameId.P3 && TestParty.P3RewardGood(Environment.GetCommandLineArgs()) is bool good)
                TestParty.RunP3Reward(_session, good); // s169: the gods' reward scene on demand
            else if (game == GameId.P3 && Environment.GetCommandLineArgs().Contains(TestParty.P3AstralSwitch))
                TestParty.P3Defeat(_session); // s169: the Astral Plane on demand
            else if (game == GameId.P3 && TestParty.P3ScrollItem(Environment.GetCommandLineArgs()) is int scroll)
                TestParty.GiveP3Scroll(_session, scroll); // s169: a scroll to read (126: Scroll IV's map)
            else if (game == GameId.P3 && TestParty.P3PlaneCell(Environment.GetCommandLineArgs()) is { } planeCell)
                TestParty.PutOnP3Plane(_session, planeCell); // s169: a plane's map (DOS: CGA palette 1)
            else if (game == GameId.P2 && Environment.GetCommandLineArgs().Contains(TestParty.P2VictorySwitch))
                TestParty.RunP2Ending(_session);
            else if (Environment.GetCommandLineArgs().Contains(TestParty.FieldDeadSwitch))
                TestParty.FieldWithDeadMember(_session); // s163: a field Resurrection on demand
        }
        _packs = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(game));
        UseGraphicsPack(ChosenPack(game));
        _lastMode = null;
        if (_session.Mode == GameMode.Combat && !_combatUi.IsPresenting) // --p1-combat: the fight is on at once
        {
            UpdateCombatEffects(); // the presenter learns the pack's screens before its first round
            _fightMark = _session.LogSequence;
            // --p1-map-boxes (s139): the encounter's boxes over the map first, then the fight's screen.
            if (!(TestParty.MapBoxLines(Environment.GetCommandLineArgs(), _session) is { } boxLines
                  && CurrentGameSettings.OriginalScreens && _assets?.Pack?.MapMessage is { } mapLayout
                  && StartMapMessages(mapLayout, boxLines, _fightMark)))
                _combatUi.Start(_session);
        }
        SetFeedback(_testLevel is int l && options is null ? UiText.F("game.level_x_test_party_ready_in", l) : PadWording.S("game.welcome_to_pelnor_g_enters_the"), // s193 (E30): no G on the pad
            HelpForMode(_session.Mode));
    }

    /// <summary>Game menu → Main menu: drop the session (unsaved progress is lost; the menu asked Y/N).</summary>
    private void ReturnToMainMenu()
    {
        _unifiedTown.Reset();
        _unifiedTownNote = null;
        CloseMapMessages();
        _p1OkSpec = null;
        _p1About = false;
        _p1TownHeld = null;
        CloseDosPages();
        _combatUi.Cancel();
        _showInventory = _showBestiary = _inventoryCompare = false;
        _inventoryScreen.Close();
        _promptNote = "";
        _defeatCause = new();
        _session = null;
        _data = null;
        _overworldView = null;
        _dungeonView = null;
        UseGraphicsPack(null);
        Window.Title = UiText.S("game.openphantasie");
    }

    /// <summary>The game tab's graphics pack if it is present and drawable, else the game's default pack.</summary>
    /// <summary>s169: the pack's <see cref="GraphicsPack.ScenePalettes"/> keys (III DOS's CGA palette 1 screens, FORMAT.md section 10).</summary>
    private const string SceneDungeon = "dungeon", ScenePlane = "plane", SceneCombatField = "combat_field",
        SceneReward = "reward", SceneAstral = "astral";

    /// <summary>s169: the screen key of the frame being drawn (the last screen drawn names it), or null.</summary>
    private string? _frameScene;

    private readonly Dictionary<(GraphicsPack Pack, string Scene), ScreenPalette?> _scenePalettes = new();

    /// <summary>
    /// s169: the frame palette for screen <paramref name="scene"/> when the pack shows it in another palette than its
    /// pictures are cut in (<see cref="GraphicsPack.ScenePalettes"/>): every pixel takes the cut palette's nearest
    /// colour's index, shown in the screen's palette, as CGA's palette register does for the whole screen; else null.
    /// </summary>
    private ScreenPalette? ScenePaletteFor(string? scene)
    {
        if (scene is null || _assets?.Pack is not { CutPalette: { } cut } pack
            || pack.ScenePalettes.GetValueOrDefault(scene) is not { } shown || shown == cut)
            return null;
        if (!_scenePalettes.TryGetValue((pack, scene), out var palette))
            _scenePalettes[(pack, scene)] = palette =
                ScreenPalette.Mapped(pack.Palettes[cut], pack.Palettes[shown]);
        return palette;
    }

    /// <summary>
    /// s169: the screen key of what the mode view shows (<see cref="ScenePaletteFor"/>): III DOS's dungeon map
    /// (ovl4 <c>0x7dd</c>), a plane's map (root <c>0x79e</c>: any map but Scandor's), the fight's field (root
    /// <c>0x1080a</c>; not the orders screen, <c>0xf9ca</c>); null elsewhere.
    /// </summary>
    private string? ModeScene(GameSession s) => ViewMode(s) switch
    {
        GameMode.Dungeon => SceneDungeon,
        GameMode.Overworld when s.Overworld.RegionId != Phantasie3Campaign.Scandor && s.Game == GameId.P3 => ScenePlane,
        GameMode.Combat when _combatUi.FieldRun is not null
            || (!_combatUi.AmigaOrdersShown && _combatUi.AmigaFieldMenuShown) => SceneCombatField, // as the overlays pick
        _ => null,
    };

    private GraphicsPack? ChosenPack(GameId game) =>
        _packs?.Find(_settings.Game(game).Graphics ?? "") is { } chosen && FontSources.CanDraw(chosen)
            ? chosen
            : _packs?.Default;

    /// <summary>The packs the Options graphics row offers for <paramref name="game"/>: present and drawable, default first.</summary>
    private IReadOnlyList<(string Id, string Name)> PackList(GameId game)
    {
        if (_packLists.TryGetValue(game, out var list))
            return list;
        try
        {
            var catalog = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(game));
            list = catalog.Available
                .Where(FontSources.CanDraw)
                .OrderBy(p => p == catalog.Default ? 0 : 1)
                .Select(p => (p.Id, p.Name))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            list = [];
        }
        return _packLists[game] = list;
    }

    private void RunMainMenuChoice(MainMenuChoice choice, GameId game)
    {
        switch (choice)
        {
            case MainMenuChoice.Play:
                // QoL SAVE SLOTS: with saves there, the game opens on its slots (NEW GAME first); IV's title menu shows its own.
                Action start = game == GameId.P4 ? StartNewPhantasie4 : () => StartOrOfferSaves(game);
                if (!BeginSplash(game, force: false, start, game == GameId.P4 ? LoadSavedPhantasie4 : null))
                    StartOrOfferSaves(game);
                break;
            case MainMenuChoice.Options:
                _options.Open();
                break;
            case MainMenuChoice.Quit:
                ExitFor("main menu Quit");
                break;
        }
    }

    /// <summary>
    /// Show <paramref name="game"/>'s title splash (s102) and run <paramref name="after"/> when it ends. False (nothing
    /// shown) when the game's Options switch is off (unless <paramref name="force"/>d) or its chosen pack has no
    /// <c>title_sequence</c> (the DOS CGA pack, a game without a pack). s151: <paramref name="load"/> runs instead when the
    /// splash's start menu (IV's <c>_disk(0)</c>) ends on a save slot.
    /// </summary>
    private bool BeginSplash(GameId game, bool force, Action? after, Action<int>? load = null)
    {
        if (!force && !_settings.Game(game).TitleScreen)
            return false;
        GraphicsPack? pack;
        try
        {
            var catalog = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(game));
            pack = catalog.Find(_settings.Game(game).Graphics ?? "") ?? catalog.Default;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                                       or System.Text.Json.JsonException)
        {
            return false;
        }
        if (pack?.TitleSequence is not { } sequence)
            return false;
        _splash = new TitleSplash(game, sequence) { Animations = pack.PictureAnimations, Panels = pack.CommandPanels };
        if (pack.CommandPanels?.DiskMenu is { } disk)
            _splash.MenuIcons = TitleSplash.DiskMenuIcons(disk, slot => load is not null && SaveSlotExists(game, slot));
        _splashAssets = new AssetCatalog(GraphicsDevice, pack);
        _splashText = UiText.For(game);
        // The splash's own canvas, letterboxed; the pictures are drawn as stored (no pack palette). User s152: the title
        // canvas fills the window at a fractional scale (point sampled); in-game screens stay integer-scaled.
        _splashScreen = new VirtualScreen(GraphicsDevice,
            new ScreenSpec(sequence.Width, sequence.Height, 1, 1, sequence.Background),
            _screen?.Scale ?? _settings.IntegerScale) { FractionalFit = true };
        _afterSplash = after;
        _afterSplashLoad = load;
        StartSplashTune(game, sequence);
        return true;
    }

    /// <summary>
    /// s108: the original's title tune under the splash (the pack's <c>title_sequence.tune</c>), unless the game's TITLE
    /// TUNE switch is off. Silent when the tune's file is missing or there is no sound device.
    /// </summary>
    private void StartSplashTune(GameId game, TitleSequence sequence)
    {
        StopSplashTune();
        StopCarriedTune();
        if (!_settings.Game(game).TitleTune || sequence.Tune is not { } tune)
            return;
        Audio.TuneRenderer? renderer;
        try
        {
            renderer = Audio.TuneRenderer.For(tune, a500Filter: A500FilterOn(game));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return;
        }
        if (renderer is not null)
            _splashTune = Audio.TunePlayer.TryStart(renderer);
    }

    /// <summary>
    /// I's victory screen, once a frame (s127, Amiga h32 <c>+0x6d0</c>): with a pack that has an
    /// <see cref="EndingSceneLayout"/> and the picture, the screen runs by its clock and answers Core's
    /// <see cref="SessionPromptKind.VictoryScene"/> itself when its fade-out is over (the Amiga goes back to Pelnor with no
    /// key). Without one the prompt box shows the speech and waits for a key, as DOS does.
    /// </summary>
    private void UpdateVictoryScreen(double now)
    {
        var scene = _session is not null && _splash is null && CurrentPrompt is { Kind: SessionPromptKind.VictoryScene }
            ? _session.PendingVictoryScene : null;
        if (scene is null || _assets?.Pack?.EndingScene is not { } layout || _assets.Get(PictureNames.EndingFinal) is null)
        {
            if (_victory is not null)
                StopEffects();
            _victory = null;
            return;
        }
        var effects = _assets.Pack.DosoundEffects;
        if (!ReferenceEquals(_victory?.Scene, scene))
        {
            // s128: each sound holds the screen for its length (h89 +0x304 blocks). With the Sound switch off the sound
            // waits are dropped (user s129); the screen's own delays stay.
            VictorySoundTimes? times = null;
            try
            {
                if (_settings.Sound && layout.Sounds is { } sounds && effects?.TryLoadImage() is { } image)
                    times = VictorySoundTimes.From(sounds, n => effects.Hold(image, n));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
            // s135: II's crowd sound (0xb80e(32)) and OK clicks (0xb7e6) with the Sound switch on and the pack's ST sounds.
            // s163: and the next 32 waiting for the last (0xb8f2), from the sound's length.
            _victory = new VictoryScreen(layout, scene, times, _settings.Sound && _assets.Pack.StDosoundEffects is not null,
                P2SoundSeconds);
            _victoryStart = now;
            _victorySoundsStarted = 0;
        }
        var elapsed = now - _victoryStart;
        var starts = _victory.SoundStarts();
        int? due = null;
        while (_victorySoundsStarted < starts.Count && starts[_victorySoundsStarted].At <= elapsed)
            due = starts[_victorySoundsStarted++].Sound;
        if (due is int sound && _settings.Sound)
        {
            if (_assets.Pack.StDosoundEffects is { } st)
                _effects.Play(st, sound);
            else
                _effects.Play(effects, sound);
        }
        else if (_victorySoundsStarted > 0 && _effects.Playing && _victory.SoundCut(_victorySoundsStarted - 1)
                 && elapsed >= starts[_victorySoundsStarted - 1].End - (effects?.CallOverheadSeconds ?? 0))
            _effects.Stop(); // s130: a key ended the sound at a step (h89 +0x304); the call's tail is silent. s156: only a
                             // cut sound: an uncut one plays out (the first frame's hitch clipped 22 by 0.18 s)
        _effects.Update();
        if (_victory.At(elapsed).Done && CurrentPrompt is { } prompt)
        {
            StopEffects();
            AnswerPrompt(prompt, 1);
        }
    }

    /// <summary>
    /// III's gods' reward scene, once a frame (s109): an intro page whose flashes have run moves on by itself (the Amiga
    /// reads no key there); <c>D/M1</c> starts on the god's picture (<c>0x1efe8</c>) and stops when the scene is gone
    /// (<c>0x1f24a</c>: after the speech, the rewards and the fade), or with a load or the main menu. The original plays it
    /// whatever its settings; with no sound device or no file the scene is silent.
    /// </summary>
    private void UpdateRewardScene(double now)
    {
        var reward = _session is not null && _splash is null && CurrentPrompt is { Kind: SessionPromptKind.RewardScene }
            ? _session.PendingRewardScene : null;
        if (reward is null)
        {
            _rewardClock.Reset();
            StopRewardTune();
            if (_effectsFor is IReadOnlyList<RewardFlashLayout.Flash>)
                StopEffects();
            return;
        }
        var pages = GodsRewardScene.Pages(reward, UiText.S("p3.reward.press_a_key"));
        if (_rewardClock.Page < GodsRewardScene.PicturePage && _rewardClock.RunOver(now)
            && _defeatPager.Index(SessionPromptKind.RewardScene, pages) == _rewardClock.Page)
            _defeatPager.Advance(SessionPromptKind.RewardScene, pages);
        // s110: sound 15 before each flash's second frame (0x1b814), all of a run's sounds as one driver run; a page
        // change (the run's end, Return, the picture page) ends it. With the Sound switch off, silent.
        var run = _rewardClock.Run;
        if (!ReferenceEquals(run, _effectsFor) && _effectsFor is IReadOnlyList<RewardFlashLayout.Flash>)
            StopEffects();
        if (run is not null && !ReferenceEquals(run, _rewardCutFor)
            && _assets?.RewardScene?.Flash is { Sound: int cut } cutLayout)
        {
            _rewardCutFor = run; // s158: the run's first sound 15 cuts a carried title tune, with the Sound switch off too
            NoteCarryCut(cutLayout.SoundVbls(run).Select(v => (v, cut)), cutLayout.VblHz);
        }
        if (run is not null && !ReferenceEquals(run, _effectsFor) && _settings.Sound
            && _assets?.RewardScene?.Flash is { Sound: int sound } flashLayout)
        {
            _effectsFor = run;
            _effects.Play(_assets.SoundEffects, flashLayout.SoundVbls(run).Select(v => (v, sound)));
        }
        if (_rewardClock.Page >= GodsRewardScene.PicturePage && !ReferenceEquals(_rewardTuneFor, reward))
        {
            StopRewardTune();
            _rewardTuneFor = reward; // once per scene: a tune that has ended does not start again
            if (_assets?.RewardScene?.Tune is { } tune)
            {
                try
                {
                    if (Audio.TuneRenderer.For(tune, a500Filter: _session is null || A500FilterOn(_session.Game)) is { } renderer)
                        _rewardTune = Audio.TunePlayer.TryStart(renderer);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                }
            }
        }
        _rewardTune?.Update();
    }

    /// <summary>
    /// s110: a combat round's spell flashes (III Amiga) start their sound 15 runs once, with the Sound switch on; the flash
    /// planner follows the pack (no combat flash = none).
    /// </summary>
    private void UpdateCombatEffects()
    {
        var assets = _assets;
        _combatUi.PlanFlashes = assets?.CombatFlash is { } layout && _session is { } session
            ? hits => CombatFlashRun.Plan(hits, layout, m => session.Party.Members.IndexOf(m),
                m => (int)(m.Phantasie3?.Rank ?? P3PartyRank.Front),
                m => UiOverlays.FlashGiant(m, _combatUi.AmigaField?.Invoke() == true, assets.GetMonsterIdle(m.Id)?.Width),
                n => Random.Shared.Next(n))
            : null;
        _combatUi.AmigaField = () => _session is { Game: GameId.P3 } && CurrentGameSettings.OriginalScreens
                                     && assets?.PortraitGrave is not null && assets.KeepWindows?.MenuBars.ContainsKey("fight") == true;
        _combatUi.AmigaOrders = () => _session is { Game: GameId.P3 } && CurrentGameSettings.OriginalScreens
                                      && assets?.BodyFigure is not null && assets.KeepWindows?.Rank is not null;
        _combatUi.AmigaFieldWindows = () => assets?.KeepWindows is { MonsterNames: not null, PartyHealth: not null };
        // s132: I's Amiga combat screen (the AMIGA SCREENS switch and a pack with its layout).
        _combatUi.P1Layout = assets?.Pack?.CombatField;
        _combatUi.AmigaP1Field = () => _session is { Game: GameId.P1 } && CurrentGameSettings.OriginalScreens
                                       && assets?.Pack?.CombatField is not null;
        _combatUi.P1SoundSeconds = P1SoundSeconds;
        _combatUi.P1ClockMs = () => Environment.TickCount64; // the Amiga's CurrentTime runs from boot (h80 +0xd2a's wrap, s136)
        // s136: II ST's round line by line with its sounds (the pack's combat_lines), paced by the game's COMBAT SPEED.
        _combatUi.P2Lines = () => _session is { Game: GameId.P2 } ? assets?.Pack?.StCombatLines : null;
        _combatUi.P2SoundSeconds = P2SoundSeconds;
        // The Amiga waits h3 = 10 polls a line until a Speed is picked, Normal ticked all the same (s132 capture: 0.24 s,
        // 0.66 s after Normal): the remaster's Normal keeps the boot value until the player picks a Speed in this game.
        _combatUi.P1Speed = () => _p1SpeedPicked || _combatUi.FieldSpeed != AmigaCombatSpeed.Normal ? (int)_combatUi.FieldSpeed : 0;
        if (_session is not null) // IV (and the other screens without a session) has no CombatPresenter fight
            _combatUi.FieldSpeed = CurrentGameSettings.CombatSpeed;
        _combatUi.FieldSpeedChosen = speed => // the Amiga fight's Speed menu sets the game's COMBAT SPEED (s119)
        {
            _p1SpeedPicked = true;
            _settings.Game(_session?.Game ?? GameId.P3).CombatSpeed = speed;
            _settings.Save();
        };
        UpdateP1FieldSounds();
        foreach (var sound in _combatUi.TakeP2Sounds())
            if (sound == P2CombatLineRun.Silence)
                _effects.Stop(); // 0xb9e2 (s156): the sparkle's sound silenced after its sprite
            else if (_settings.Sound && _assets?.Pack?.StDosoundEffects is { } st)
                _effects.Play(st, sound); // 0xb80e: the chip silenced, then the new script (cuts off the old one)
        if (_combatUi.FieldRunSerial != _fieldSerial)
        {
            _fieldSerial = _combatUi.FieldRunSerial;
            var vblHz = assets?.CombatFlash?.VblHz ?? 60;
            // s158: the round's first sound (hit, miss, cast, " dies!", the flee stop 33) cuts a carried title tune at its
            // time, with the Sound switch off too (the Amiga has none).
            if (_combatUi.FieldRun is { } planned)
                NoteCarryCut(planned.SoundPlays(vblHz), vblHz);
            if (_combatUi.FieldRun is { } field && _settings.Sound && assets?.SoundEffects is { } effects)
                _effects.Play(effects, field.SoundPlays(vblHz));
        }
        if (_combatUi.FlashRunSerial != _effectsCombatSerial)
        {
            _effectsCombatSerial = _combatUi.FlashRunSerial;
            if (_combatUi.FlashRun is { } flashes)
                NoteCarryCut(flashes.SoundPlays, flashes.Layout.VblHz);
            if (_combatUi.FlashRun is { } run && _settings.Sound)
            {
                _effectsFor = run;
                _effects.Play(assets?.SoundEffects, run.SoundPlays);
            }
        }
        _effects.Update();
    }

    private int _fieldSerial;
    private int _p1RunSerial, _p1SoundsStarted, _p1FightSerial, _p1ToneSerial, _p1StartCuts;
    private bool _p1SpeedPicked; // a Speed picked from I's field menu since the game started (the Amiga's h3 leaves 10)
    private byte[]? _p1SoundImage;
    private object? _p1SoundImageFor;

    /// <summary>
    /// How long I Amiga's sound <paramref name="sound"/> holds the screen (h89 <c>+0x304</c> blocks; s130's per-step and
    /// per-call costs), seconds; 0 with the Sound switch off (user s129: no sound waits) or without the pack's sounds.
    /// </summary>
    private double P1SoundSeconds(int sound)
    {
        if (!_settings.Sound || _assets?.Pack?.DosoundEffects is not { } effects)
            return 0;
        try
        {
            if (!ReferenceEquals(_p1SoundImageFor, effects))
            {
                _p1SoundImage = effects.TryLoadImage();
                _p1SoundImageFor = effects;
            }
            return _p1SoundImage is { } image ? effects.Hold(image, sound).Seconds : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private byte[]? _p2SoundImage;
    private object? _p2SoundImageFor;

    /// <summary>
    /// s136: how long II ST's sound <paramref name="sound"/> runs (its Dosound ticks / 50), seconds; 0 with the Sound switch
    /// off (no sound waits, as I's run) or without the pack's sounds.
    /// </summary>
    private double P2SoundSeconds(int sound)
    {
        if (!_settings.Sound || _assets?.Pack?.StDosoundEffects is not { } effects)
            return 0;
        try
        {
            if (!ReferenceEquals(_p2SoundImageFor, effects))
            {
                _p2SoundImage = effects.TryLoadImage();
                _p2SoundImageFor = effects;
            }
            return _p2SoundImage is { } image ? effects.Seconds(image, sound) : 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>
    /// s132: I Amiga's combat sounds: 17 when a fight's field first shows (h53 <c>+0x274</c>), then each round's sounds at
    /// their places on the run (<see cref="P1CombatFieldRun.SoundPlays"/>), with the Sound switch on.
    /// </summary>
    private void UpdateP1FieldSounds()
    {
        var effects = _assets?.Pack?.DosoundEffects;
        if (_combatUi.P1FieldShown && _combatUi.P1FightSerial != _p1FightSerial)
        {
            _p1FightSerial = _combatUi.P1FightSerial;
            if (_settings.Sound && _combatUi.P1Layout?.FightStartSound is int start)
                _effects.Play(effects, start);
        }
        if (_combatUi.P1StartSoundCuts != _p1StartCuts)
        {
            _p1StartCuts = _combatUi.P1StartSoundCuts; // s138: a key ends sound 17 (h89's player stops at a step)
            _effects.Stop();
        }
        if (_combatUi.P1ToneSerial != _p1ToneSerial)
        {
            _p1ToneSerial = _combatUi.P1ToneSerial; // s134: requester 12's tone after a gadget event (h89 +0xd2)
            if (_settings.Sound && _combatUi.P1ToneSound is int tone)
                _effects.Play(effects, tone);
        }
        if (_combatUi.P1RunSerial != _p1RunSerial)
        {
            _p1RunSerial = _combatUi.P1RunSerial;
            _p1SoundsStarted = 0;
        }
        if (_combatUi.P1Run is not { } run)
            return;
        int? due = null;
        while (_p1SoundsStarted < run.SoundPlays.Count && run.SoundPlays[_p1SoundsStarted].AtMs <= _combatUi.P1RunMs)
            due = run.SoundPlays[_p1SoundsStarted++].Sound;
        if (due is int sound && _settings.Sound)
        {
            if (_assets?.Pack?.StDosoundEffects is { } st)
                _effects.Play(st, sound);
            else
                _effects.Play(effects, sound);
        }
    }

    private PartyDefeatResult? _astralSoundFor;
    private int _astralSoundLine = -1;

    private void StopEffects()
    {
        _effects.Stop();
        _effectsFor = null;
    }

    private void StopRewardTune()
    {
        _rewardTune?.Dispose();
        _rewardTune = null;
        _rewardTuneFor = null;
    }

    private void StopSplashTune()
    {
        _splashTune?.Dispose();
        _splashTune = null;
    }

    private void StopCarriedTune()
    {
        _carriedTune?.Dispose();
        _carriedTune = null;
        _carryCutAtMs = null;
    }

    private object? _rewardCutFor;

    /// <summary>s158: a run of driver starts was planned now: its first start cuts the carried title tune then.</summary>
    private void NoteCarryCut(IEnumerable<(int Frame, int Sound)> plays, double vblHz)
    {
        if (_carriedTune is not null)
            _carryCutAtMs = Audio.TitleTuneCarry.Earlier(_carryCutAtMs,
                Audio.TitleTuneCarry.CutAtMs(Environment.TickCount64, plays, vblHz));
    }

    /// <summary>
    /// s157: the title tune that plays on after its splash (<see cref="Audio.TitleTuneCarry"/>), once a frame: fed while it
    /// sounds, stopped at its end, when its game is left, when TITLE TUNE is switched off, or at the game's first sound
    /// effect (any effect the Client starts, the reward tune, or a sound Core raises with the Sound switch off too: the
    /// Amiga has no switch and its every effect start cuts the music).
    /// </summary>
    private void UpdateCarriedTune()
    {
        var raised = _gameSoundRaised;
        _gameSoundRaised = false;
        if (_carriedTune is null)
            return;
        var cut = _carryCutAtMs is double at && Environment.TickCount64 >= at;
        if (Audio.TitleTuneCarry.Stops(!_carriedTune.Playing, _carriedTuneGame, _session?.Game,
                _settings.Game(_carriedTuneGame).TitleTune, raised || cut || _rewardTune is not null))
            StopCarriedTune();
        else
            _carriedTune.Update();
    }

    /// <summary>
    /// Whether save slot <paramref name="slot"/> of <paramref name="game"/> holds a save (the start menu's "*k:Save" icon,
    /// <c>fames.k</c> in IV): the game's slot files when it has slots (IV always: its original's three,
    /// <see cref="SaveSlots.Slotted"/>), else its one save, which stands for slot 1.
    /// </summary>
    private bool SaveSlotExists(GameId game, int slot) => SaveSlots.Slotted(game, _settings.Game(game).SaveSlots)
        ? slot >= SaveSlots.FirstNumber(game) && slot <= SaveSlots.NumberedCount(game)
          && File.Exists(SaveSlots.PathOf(ClientSettings.DataDirectory, game, slot))
        : slot == 1 && File.Exists(ClientSettings.SavePathFor(game));

    /// <summary>The pointer on the splash canvas (canvas pixels), or null outside it or with the window inactive.</summary>
    private Point? SplashCanvasPoint()
    {
        if (!IsActive || _splash is null || _splashScreen is null)
            return null;
        var state = Mouse.GetState();
        var pp = GraphicsDevice.PresentationParameters;
        var client = Window.ClientBounds;
        var x = client.Width > 0 ? state.X * pp.BackBufferWidth / client.Width : state.X;
        var y = client.Height > 0 ? state.Y * pp.BackBufferHeight / client.Height : state.Y;
        return VirtualScreen.WindowToVirtual(_splashScreen.PresentRect(pp.BackBufferWidth, pp.BackBufferHeight),
            _splash.Sequence.Width, _splash.Sequence.Height, new Point(x, y));
    }

    private void EndSplash()
    {
        var pick = _splash?.MenuPick;
        if (_splash is { } shown && Audio.TitleTuneCarry.CarriesOn(shown.Sequence.Tune, _splashTune?.Playing == true))
        {
            // s157: III Amiga's D/M0 plays on into the game (FORMAT.md 7.1), until its end or the first sound effect.
            StopCarriedTune();
            _carriedTune = _splashTune;
            _carriedTuneGame = shown.Game;
            _gameSoundRaised = false;
            _carryCutAtMs = null;
            _splashTune = null;
        }
        StopSplashTune();
        _splash = null;
        _splashAssets?.Dispose();
        _splashAssets = null;
        _splashScreen?.Dispose();
        _splashScreen = null;
        _splashText = null;
        var after = _afterSplash;
        _afterSplash = null;
        var load = _afterSplashLoad;
        _afterSplashLoad = null;
        if (pick is [var c] && char.IsAsciiDigit(c) && load is not null)
            load(c - '0');
        else
            after?.Invoke();
    }

    /// <summary>Options closed: save the settings and apply scale and graphics.</summary>
    /// <summary>s158: an import committed: re-read it, and forget the pack lists built from the old one.</summary>
    private void AfterImport(string importRoot)
    {
        GameDataLocator.UseImport(importRoot);
        _packLists.Clear();
    }

    private void AfterOptionsClosed()
    {
        if (_optionsFromGameMenu && _session is not null)
            OpenGameMenu();
        _optionsFromGameMenu = false;
        if (!_options.Changed)
            return;
        _townMenu.Sound = _settings.Sound; // the Options SOUND row (D-073 QoL) is the town Option's flag
        _settings.Save();
        if (_session is not null && ChosenPack(_session.Game) is var pack && pack != _assets?.Pack)
            UseGraphicsPack(pack);
        ApplyColorblind();
        if (_screen is not null && _screen.Scale != _settings.IntegerScale)
        {
            _screen.SetScale(_settings.IntegerScale);
            ApplyWindowScale();
        }
    }

    /// <summary>
    /// The running game's colorblind palette switch (QoL) on the screen; off, and on the main menu, there is no filter (the
    /// frame is presented untouched).
    /// </summary>
    private void ApplyColorblind()
    {
        if (_screen is null)
            return;
        var on = _session is not null && _settings.Game(_session.Game).ColorblindPalette;
        if (on != (_screen.Colorblind is not null))
            _screen.Colorblind = ColorblindFilter.For(on);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        if (_assets is null) // the main menu draws without a game's pack
            UseGraphicsPack(null);
        if (_settings.StartMaximized)
            WindowMaximizer.TryMaximize(Window.Handle);
    }

    /// <summary>The graphics packs whose original files are present (T1.5); the Options graphics row offers these (T5.0).</summary>
    public IReadOnlyList<GraphicsPack> GraphicsPacks => _packs?.Available ?? [];

    /// <summary>The pack being drawn, or null (no extracts found).</summary>
    public GraphicsPack? ActiveGraphicsPack => _assets?.Pack;

    /// <summary>
    /// Switch to an available pack by id (T1.5 selection API; the Options graphics row goes through the game's settings). Returns false for
    /// an unknown or absent pack. Nothing calls this by default, so the DOS CGA pack is never drawn unless chosen.
    /// </summary>
    public bool SelectGraphicsPack(string id)
    {
        if (_packs?.Find(id) is not { } pack || !FontSources.CanDraw(pack)) // its font must be drawable (T1.5 font seam)
            return false;
        UseGraphicsPack(pack);
        return true;
    }

    private void UseGraphicsPack(GraphicsPack? pack)
    {
        _assets?.Dispose();
        _assets = new AssetCatalog(GraphicsDevice, pack);
        _victoryView = null; // built on the next victory screen for this pack
        if (_p4 is not null)
            _p4.Assets = _assets;
        var scale = _screen?.Scale ?? _settings.IntegerScale;
        _screen?.Dispose();
        _screen = new VirtualScreen(GraphicsDevice, _assets.Screen, scale, ScreenPalette.From(pack?.Palette));
        ApplyColorblind();
        PixelFont.Use(FontSources.For(pack));
        ApplyWindowScale();
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        var pad = GamePad.GetState(PlayerIndex.One);
        _input.TickStick(pad, gameTime.ElapsedGameTime.TotalMilliseconds); // s185: one left-stick direction per push (I–III)
        UpdateMouse(gameTime, keys); // Game1.Mouse.cs: mouse → keys / cursor moves, and a map walk's next step
        // s183: help lines name the pad's buttons while the pad is the last input (PadWording).
        PadWording.Note(_input.NewButtons(pad).Any(),
            _input.AnyRealKeyPressed(keys) || _mouse.AnyPressed || _mouse.Moved && _pointerMoved || _mouse.WheelSteps != 0);
        _updateSeconds = gameTime.TotalGameTime.TotalSeconds;
        UpdateRewardScene(_updateSeconds);
        UpdateVictoryScreen(_updateSeconds);
        UpdateP1DefeatScreen(_updateSeconds); // s153: I Amiga's defeat screens run by themselves
        UpdateP1DosDefeat(_updateSeconds); // s154: I DOS's defeat lines run by themselves (5.00 s each)
        UpdateP2DefeatHold(); // s165: II ST's DEFEATED page goes on after its 3000 ms hold
        UpdateP1CueSounds(); // s153: the map's step tone, 19 / 27 before their menus
        UpdateP1SheetTone(); // s158: the tone after the sheet's answers, its views' EXIT and PURGE / KEEP
        UpdateP1ScrollBeep(); // s162: the printout question's beep as a scroll is read in town
        UpdateP1ListTones(); // s165: the town's and the Guild's list tones (choosers' cancel, lists, Show party's close)
        UpdateP1AfterFight(); // s165: the fight's field under its finds' KEEP IT? until they are answered
        UpdateCombatEffects();
        if (_delayedCues.Count > 0 && (_input.NewKeys(keys).Any() || _input.NewButtons(pad).Any() || _mouse.AnyPressed))
            CutDelayedCues(); // s141: a key or a click ends a timed box's hold; s192 (pad fix C23): a pad button too
        UpdateGameSounds();
        UpdateP4Sounds();
        UpdateCarriedTune();
        if (_session?.Mode != GameMode.Town)
            _doorFlash = null; // a load or a fight mid-flash drops the pending entry

        // s102: the title splash owns every key and click while shown; a key or click ends a picture, Esc them all.
        if (_splash is not null)
        {
            var pressed = _input.NewKeys(keys).ToList();
            var buttons = _input.NewButtons(pad).ToList();
            if (_splash.CurrentMenu is not null)
            {
                // s151: the start menu (IV's _disk(0)) takes only its icons' keys and a click on an icon.
                foreach (var k in pressed)
                    if (_splash.Choose(TitleSplash.MenuKey(k)))
                        break;
                // A pad: the D-pad / left stick step a lit icon, A picks it (New Game while none is lit; not in the menu's
                // first 0.5 s, so A mashed through the pictures picks nothing: s192, pad fix C20); RB the first save.
                foreach (var b in buttons)
                    _splash.PadStep(InputMap.MenuPadKey(b) switch
                    {
                        Keys.Left or Keys.Up => -1,
                        Keys.Right or Keys.Down => 1,
                        _ => 0,
                    });
                if (!_splash.Done && buttons.Contains(Buttons.A))
                    _splash.PadChoose();
                if (!_splash.Done && buttons.Contains(Buttons.RightShoulder))
                    _splash.Choose(_splash.MenuIcons.FirstOrDefault(i => TitleSplash.SlotOf(i) is not null)?.Key);
                if (!_splash.Done && _mouse.AnyPressed && SplashCanvasPoint() is { } p)
                    _splash.Choose(_splash.MenuKeyAt(p));
            }
            if (_splash.Update(gameTime.ElapsedGameTime.TotalSeconds,
                    pressed.Count > 0 || buttons.Count > 0 || _mouse.AnyPressed,
                    pressed.Contains(Keys.Escape) || buttons.Contains(Buttons.Back) || buttons.Contains(Buttons.B))) // s192 (C21): B too
                EndSplash();
            else if (_splash.Sequence.Tune?.UntilStep is int last && _splash.Step >= last)
                StopSplashTune(); // I: the tune belongs to cover1, a key ends both
            else
                _splashTune?.Update();
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // QoL SAVE SLOTS: the slot picker owns every key while shown (over any game or the main menu).
        if (_slotScreen.IsOpen)
        {
            if (_slotScreen.HandleKeys(_input.NewKeys(keys).Concat(SaveSlotScreen.PadKeys(_input.NewButtons(pad)))))
                RunSaveSlotPick();
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        if (_p4 is not null && !_options.IsOpen)
        {
            _p4.Tick(gameTime.ElapsedGameTime.TotalMilliseconds); // s110: IV's picture animations, the open door
            if (IsMouseVisible && _mouse.Moved)
                _p4.ClearPadFocus(); // s182: the mouse takes over from the pad's picked button
            // s151: buttons under the pointer are lit (_xbtn); s182: so is the button the pad picked (Y / X)
            _p4.Pointer = _p4.PadPointer ?? (IsMouseVisible ? _mouse.Position : null);
            _p4.MysticSkippable = _settings.Game(GameId.P4).MysticSkip;
            _p4.CommonTownKeys = _settings.Game(GameId.P4).CommonTownKeys; // QoL COMMON TOWN KEYS (D3-074)
            _p4.OriginalSlots = !_settings.Game(GameId.P4).SaveSlots; // SAVE SLOTS off: IV's own Save page (12-save-slots.md)
            if (HandleUnifiedTownInput(keys, pad))
            {
                _input.EndFrame(keys, pad);
                base.Update(gameTime);
                return;
            }
            // QoL MYSTIC SKIP: Space / Return, a left click or the pad's A ends the Mystic's counted loops; that input
            // goes nowhere else this frame, so it does not also close the Mystic in its wait.
            if (_p4.MysticBusy && _p4.MysticSkippable
                && (_input.NewKeys(keys).Any(P4DungeonScreen.IsMysticSkipKey) || _input.NewButtons(pad).Contains(Buttons.A)
                    || _mouse.LeftClick is not null)
                && _p4.SkipMystic())
                _pendingClick = null;
            else
            {
                // s182 "Pad coverage": the pad's buttons as the keys this prompt takes (P4DungeonScreen.Pad.cs); a button
                // the pad picked with Y / X is clicked by A as the mouse would (Game1.Mouse.cs Act).
                // s183: the left stick by its position (StickQuantizer: one direction per push, never N then NE).
                var padKeys = _p4.PadKeys([.. _input.NewButtons(pad)], P4DungeonScreen.HeldButtons(pad), _hits.Regions,
                    pad.ThumbSticks.Left, gameTime.ElapsedGameTime.TotalMilliseconds, out var padPress);
                if (padPress is not null)
                    Act(padPress, padPress.Bounds.Center, clicked: true);
                // s187 (pad fix A6): IV's party lines scroll by [ ] / the right stick, its message / menu box by , . / RT +
                // the right stick, as I–III's sidebar (IV reads none of these keys).
                SidebarScroll.Apply(_input.NewKeys(keys), _input.NewButtons(pad), SidebarScroll.RightTriggerHeld(pad),
                    _p4.PartyScroll, _p4.BoxScroll);
                if (_p4.HandleKeys(_input.NewKeys(keys).Concat(padKeys)) is { } request)
                    RunPhantasie4Request(request);
            }
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // T5.0: the Options screen and the main menu own every key while shown (s158: and the import screen; s174: and the
        // update screen).
        if (_session is null && !_options.IsOpen && !_import.IsOpen && !_update.IsOpen && LaunchCheckFound() is { } found)
            _update.OpenWith(found);
        if (_options.IsOpen || _import.IsOpen || _update.IsOpen || _session is null)
        {
            if (_update.IsOpen)
            {
                _update.Update();
                _update.HandleKeys(_input.MenuKeys(keys, pad)); // s182: and the pad (shared map)
                if (_update.RestartRequested)
                    RestartAfterUpdate();
            }
            else if (_import.IsOpen)
            {
                _import.Update();
                _import.HandleKeys(_input.NewKeys(keys).Concat(_import.PadKeys(_input.NewButtons(pad))));
            }
            else if (_options.IsOpen)
            {
                if (_options.HandleKeys(_input.NewKeys(keys).Concat(OptionsScreen.PadKeys(_input.NewButtons(pad)))))
                    AfterOptionsClosed();
                if (_options.ImportRequested)
                {
                    _options.ImportRequested = false;
                    _import.Open(firstRun: false);
                }
                if (_options.UpdateRequested)
                {
                    _options.UpdateRequested = false;
                    _update.Open();
                }
            }
            else if (_mainMenu.HandleKeys(_input.MenuKeys(keys, pad)) is var (choice, game))
                RunMainMenuChoice(choice, game);
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // s140: II ST's TRANSPORTATION fade reads no key (0x260a: Setcolor and Vsync only; s162: I Amiga's h26 +0xe30 the
        // same); keys pressed meanwhile are dropped.
        if (TransportFading)
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // s158: II ST's alert box (form_alert) owns the frame's keys while up: Return (s160: only Return, as the AES form
        // loop) or a click on its button closes it; nothing under it sees the keys (the field loop's click included).
        if (_session is not null && _p2Alert.HandleKeys(_input.MenuKeys(keys, pad))) // s182: pad A = Return
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // s165: II ST's timed field box (field Resurrection's 0xa18c) owns the frame's keys and clicks while up: any of
        // them, or its hold running out, ends it (0x16cbe); the key goes no further (no step, no field click).
        if (_session is not null && _p2FieldBox.Update(_updateSeconds,
                _input.NewKeys(keys).Any() || _input.NewButtons(pad).Any() || _mouse.AnyPressed)) // s182: a pad button too
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        if (_session is { } clickSession && FieldCommandKeys(clickSession, keys, pad))
            PlayClick(); // s141: II ST's field loop 0x16fea clicks (0x1719a) on every key it takes

        // III Amiga's message box (window 7, s135) owns the frame's keys while up: any key or OK closes it, unseen below.
        if (_session is not null && _amigaMessage.HandleKeys(_input.MenuKeys(keys, pad))) // s182: and the menu pad
        {
            if (!_amigaMessage.Shown)
                SetFeedback("", OpenModal?.Help ?? HelpForMode(_session.Mode)); // the line went in the window, not the band
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // s165: III's Damage Report owns the frame's keys while up: any key or OK closes it, unseen below.
        if (_session is not null && _damageReport.HandleKeys(_input.MenuKeys(keys, pad))) // s182: and the menu pad
        {
            if (!_damageReport.Shown)
                _help = OpenModal?.Help ?? HelpForMode(_session.Mode);
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // s154: I Amiga's "LEAVING %s" holds the town picture while the map loads; keys and clicks wait.
        if (_session is { } leaveSession)
        {
            NoteLeaving(leaveSession);
            if (LeavingShown)
            {
                _input.EndFrame(keys, pad);
                base.Update(gameTime);
                return;
            }
        }

        // s154: I Amiga's waits on the map (the step tone before the move, a question's sound before it, the monster
        // pictures' read before the fight) own every key; a move pressed meanwhile is made after.
        if (_session is { } holdSession && UpdateP1Holds(holdSession, keys, pad))
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // s154: I Amiga's STYX / OCEAN OK requester owns every key, button and click until one closes it.
        if (_session is not null && HandleP1OkKeys(keys, pad))
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // s139: I Amiga's encounter boxes over the map own every key and click until the last one closes (h80 +0x546).
        if (_session is { } mapSession && UpdateMapMessages(mapSession,
                _input.NewKeys(keys).Any() || _input.NewButtons(pad).Any() || _mouse.AnyPressed))
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        // s154: I DOS's boxes own every key, click and pad button; each press turns one page (COM 0x37a0).
        if (_session is { } pageSession && UpdateDosPages(pageSession,
                _input.NewKeys(keys).Any() || _input.NewButtons(pad).Any() || _mouse.AnyPressed))
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        if (_session is { Options.UnifiedTownMenu: true } unifiedSession)
            ReturnFromBarGuild(unifiedSession);
        if (HandleUnifiedTownInput(keys, pad))
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        if (_session is { } barSession && (HandleOutsideBarKeys(barSession, _input.NewKeys(keys), _input.NewButtons(pad))
                || HandleP1MapBarKeys(barSession, _input.NewKeys(keys), _input.NewButtons(pad))
                || HandleTownBarKeys(barSession, _input.NewKeys(keys), _input.NewButtons(pad))))
        {
            _input.EndFrame(keys, pad);
            base.Update(gameTime);
            return;
        }

        _withdrawDialog.AmigaKeys = AmigaBankWindows; // s157: III's withdraw in the Bank's window style takes O / C
        _guildAddDialog.AmigaWindow = ConfirmInWindow; // s162: III's Add move question in window 1 takes O / C
        _guildAddDialog.P1Requester = _session?.Mode == GameMode.Guild && P1Requesters is not null; // s192 (C18): A yes, X no
        _inspectDialog.AmigaSpellRows = P1SheetSpellList?.Rows; // s158: I Amiga's SPELLS view is requester 12
        InjectPadCommand(); // s182: a pick in the pad's command list sends its key now, as typed
        var modalWasOpen = OpenModal is not null; // s182: a dialog opened this frame takes no pad button yet
        if (_session is not null && _data is not null && (OpenModal ?? OpenModalDialog(keys, pad)) is { } modal)
        {
            // A panel with more rows than the screen holds scrolls with PgUp / PgDn (s99), when the dialog has no use for them.
            // s182 (Pad coverage): the pad's buttons as keys too (DialogPad: A Return, B Esc, the wheel for digits / names).
            var modalKeys = _input.NewKeys(keys).Concat(modalWasOpen ? ModalPadKeys(modal, pad) : []).ToList();
            if (UiOverlays.TextPanelScroll.CanScroll || UiOverlays.ScrollFrameScroll.CanScroll)
                modalKeys.RemoveAll(k => k is Keys.PageUp or Keys.PageDown
                    && (UiOverlays.TextPanelScroll.CanScroll ? UiOverlays.TextPanelScroll : UiOverlays.ScrollFrameScroll).HandleKey(k));
            var modalLine = modal.HandleKeys(modalKeys, _session);
            if (!modal.IsOpen)
                _dialogPad.Reset(); // s182: the dialogs live on; the next opening starts the pad's wheel again
            OfferPurgeQuestion(_session); // s160: III's purge pick waits for S407 (the list is closed while it asks)
            if (!modal.IsOpen && ClicksOnClose(modal, _session.Mode, modalKeys))
                PlayClick(); // s141: II ST's town lists and dialogs click as they return (0x1964a / form_do sites, 0x674)
            if (modalLine is { } line)
            {
                if (_continueDialog.TakeLoaded())
                {
                    AfterSnapshotApplied();
                    _qol = QoLProgress.LoadOrDefault();
                }
                if (line.Length > 0 && OfferP2FieldBox(_session, line))
                    SetFeedback("", modal.IsOpen ? ModalHelp(modal) : HelpForMode(_session.Mode)); // s165: the line is in the box
                else
                {
                    SetFeedback(line.Length > 0 ? line : _message, modal.IsOpen ? ModalHelp(modal) : HelpForMode(_session.Mode));
                    NoteAlertLine(line); // s140: II ST's alert boxes beep first (0x1628 / 0x1668)
                    OfferAmigaMessage(line);
                    if (line.Length > 0 && !modal.IsOpen)
                        _messageBand.Restart(); // a dialog's outcome, shown again when it repeats the last one
                }
                if (_session.Mode == GameMode.Town)
                    RefreshTownStock();
                if (ReferenceEquals(modal, _gameMenu) && !modal.IsOpen) // s183: before a pick sets its own line
                    _message = GameMenuDialog.LineAfterClose(_message, _lineBeforeGameMenu, TownPictureScreen(_session));
                if (_gameMenu.TakeChoice() is { } picked)
                    RunGameMenuChoice(picked);
                if (_padCommands.TakeChoice() is { } padCommand)
                    _padCommandKey = padCommand; // s182: sent next frame (InjectPadCommand)
                if (!_amigaBank.IsOpen && _amigaBank.TakeFollow() is var follow and > 0) // the manual's bank (s117)
                {
                    var shopper = _session.Party.Members.Count > 0 ? _session.Party.Members[_members.Clamp(_session.Party.Members.Count)] : null;
                    RunTownChoice(_session, follow == 1 ? TownChoice.Withdraw : TownChoice.Deposit, shopper);
                }
                if (_timeLagDialog.TakeChosen() is var (lag, sound))
                {
                    _settings.TimeLag = lag;
                    _townMenu.Sound = _settings.Sound = sound; // the same flag as the town Option's Sound
                    _settings.Save();
                }
            }
            else
                SetFeedback(_message, modal.IsOpen ? ModalHelp(modal) : HelpForMode(_session.Mode));
        }
        else if (_session is not null && _data is not null && (_dosDialog.IsOpen || OpenDosDialog(keys, pad)))
        {
            // Modal: the dialog owns every key (Esc cancels it instead of quitting).
            if (_dosDialog.HandleKeys(_input.NewKeys(keys).Concat(DosDialogPadKeys(pad)), _session, _data) is { } status)
                SetFeedback(status, _dosDialog.IsOpen ? _dosDialog.Help : HelpForMode(_session.Mode));
            else
                SetFeedback(_message, _dosDialog.IsOpen ? _dosDialog.Help : HelpForMode(_session.Mode));
            if (_dosDialog.Imported)
            {
                _dosDialog.Imported = false;
                AfterSnapshotApplied();
            }
            if (_dosDialog.TakeTransfer() is { } source)
                StartTransferredGame(source);
        }
        else if (_session is not null && _data is not null)
        {
            // Esc backs out (or opens the game menu); that frame's keys go no further. A prompt whose menu takes Esc as an
            // answer (III's keep questions: Esc = No, root 0x75a3) gets it instead.
            var backedOut = AnswerPromptWithEscape(keys, pad) || HandleGlobalInput(keys, pad);
            var overlay = _showInventory || _showBestiary || backedOut;
            // Keys that start a fight this frame (Space, a step) must not also answer its first menu.
            var combatWasAwaiting = _combatUi.AwaitingInput;
            var p2RunBefore = _combatUi.P2Run; // s138: a run this frame's key started (an option's answer box) keeps it
            var prompt = CurrentPrompt;
            _promptMenu.Sync(prompt);
            if (!overlay && prompt is not null)
                HandlePrompt(keys, pad, prompt);
            else if (!overlay)
                HandleGameplayInput(keys, pad);

            if (combatWasAwaiting && _combatUi.AwaitingInput && !overlay)
            {
                // s182 (Pad coverage): the pad's D-pad / stick / A as the keys this step takes (CombatPad), after the keyboard's.
                var combatKeys = _input.NewKeys(keys)
                    .Concat(_combatPad.KeysFor(_combatUi.PadStep(_session), _input.NewButtons(pad),
                        SidebarScroll.RightTriggerHeld(pad))).ToList();
                foreach (var key in combatKeys)
                {
                    if (!_combatUi.HandleKey(_session, key))
                        continue;
                    if (!_combatUi.IsPresenting) // a party option ended the fight
                        SetFeedback(_combatUi.LastLine, HelpForMode(_session.Mode));
                    break; // the phase changed: don't read the same frame's keys as the next phase's input
                }
            }

            else if (!_combatUi.AwaitingInput)
                _combatPad.Reset();

            if (_combatUi.P2Run is { } p2Run && ReferenceEquals(p2Run, p2RunBefore) && !overlay
                && (_input.NewKeys(keys).Any() || _input.NewButtons(pad).Contains(Buttons.A)))
                _combatUi.P2Key(); // II ST: a key ends the line's hold (evnt_multi), not a sound wait
            if (_combatUi.IsPresenting)
            {
                var ff = _input.Held(GameAction.FastForwardHold, keys, pad);
                if (_combatUi.Update(_session, gameTime.ElapsedGameTime.TotalMilliseconds, ff))
                    AfterFightEnded(_session);
                if (!InventoryScreenOpen) // the inventory screen keeps its own help and message
                    SetFeedback(_combatUi.LastLine, HelpForCombat());
            }

            if (InventoryScreenOpen)
            {
            }
            else if (CurrentPrompt is { } open)
            {
                // s153: I Amiga's event menu shows its text in requester 21 only, not on the line.
                // s154: I DOS's timed defeat lines show in their panel only (no key asked, no line ahead of its time).
                var dosDefeat = _p1DosDefeat?.Kind == open.Kind;
                SetFeedback(dosDefeat || AmigaDisarmWindowFor(open) is not null ? "" // s163: window 6 holds its title
                    : _promptNote.Length > 0 ? _promptNote
                    : P1MapMenuFor(open) is not null || P1MapRequesterFor(open) is not null ? ""
                    : _confirm.Owns(open) && KeepWindowFor(open) is not null ? "" // s158 / s160: the text is in window 1
                    : FirstLine(open),
                    dosDefeat ? "" : KeepWindowFor(open) is not null ? UiText.S("game.keep_window_ok_cancel")
                    : AmigaDisarmWindowFor(open) is not null ? UiText.S("p3combat.member_window_help")
                    : AmigaShareWindowFor(open) is not null
                        ? PadWording.Format("p3town.bank_window_shares_help", open.Options[0], open.Options[^1]) // s185: the pad's words
                    : EventPageFor(open) is { } page ? (page.AnyKey ? UiText.S("game.event_page_any_key")
                        : EventPadHelp(open) ?? UiText.S("game.event_page_number")) // s182: the pad's pick
                    : HelpForPrompt(open));
                _promptFeedback = (_message, _help);
            }
            else if (_promptNote.Length > 0)
            {
                SetFeedback(_promptNote, HelpForMode(_session.Mode));
                _promptNote = "";
                _promptFeedback = null;
            }
            else if (_promptFeedback is { } left)
            {
                // The prompt closed with no note (e.g. a window-1 answer, s134): its question and help leave the bar;
                // whatever the answer put there since stays.
                _promptFeedback = null;
                if (!_combatUi.IsPresenting)
                    SetFeedback(_message == left.Message ? "" : _message, _help == left.Help ? HelpForMode(_session.Mode) : _help);
            }

            TrackExplorationAndEncounters();
        }

        OfferDamageReport();
        if (_session is not null)
        {
            // Every way into a town (new game, Guild exit, wilderness, TRANSPORTATION, load) lands on the menu bar.
            if (_session.Mode == GameMode.Town && _lastMode != GameMode.Town)
                _townMenu.Reset();
            _lastMode = _session.Mode;
        }

        _input.EndFrame(keys, pad);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_spriteBatch is null || _pixel is null || _screen is null || _assets is null)
        {
            base.Draw(gameTime);
            return;
        }
        if (_splash is not null && _splashScreen is not null)
        {
            BeginHits(); // nothing to hit: a click anywhere ends the picture
            _splashScreen.Begin();
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _splash.Draw(_spriteBatch, _pixel, name => _splashAssets?.Get(name), id => _splashText?.Get(id) ?? id);
            _splash.DrawMenu(_spriteBatch, _pixel, _splashAssets,
                (id, original) => _splashText is { } t && t.Contains(id) ? t.Get(id) : original,
                PadWording.Active ? PadWording.S("title.disk_help") : null); // s192 (C20): pad only, as the original's has none
            _spriteBatch.End();
            _splashScreen.EndAndPresent(_spriteBatch, _pixel);
            EndHits();
            base.Draw(gameTime);
            return;
        }
        BeginHits(); // the draw routines register what the mouse can hit (nothing extra is drawn)
        _frameScene = null; // s169: each screen below names its own (III DOS's CGA palette 1 screens)
        UiOverlays.Screen = _screen; // a scrolling box's content is clipped to it (UiOverlays.Clipped)
        _screen.PaletteOverride = null; // s157: a screen that needs its own palette sets it while it draws
        if (_slotScreen.IsOpen)
        {
            _screen.Begin();
            _screen.BeginLayout(_spriteBatch);
            UiOverlays.DrawMenuScreen(_spriteBatch, _pixel, _slotScreen.Lines(), _slotScreen.Help);
            _spriteBatch.End();
            _screen.EndAndPresent(_spriteBatch, _pixel);
            EndHits();
            base.Draw(gameTime);
            return;
        }
        if (_p4 is not null && !_options.IsOpen)
        {
            _screen.Begin();
            _screen.BeginLayout(_spriteBatch);
            _p4.Draw(_spriteBatch, _pixel);
            DrawUnifiedTown();
            _spriteBatch.End();
            _screen.EndAndPresent(_spriteBatch, _pixel);
            EndHits();
            base.Draw(gameTime);
            return;
        }
        if (_options.IsOpen || _import.IsOpen || _update.IsOpen || _session is null || _data is null)
        {
            _screen.Begin();
            _screen.BeginLayout(_spriteBatch);
            if (_update.IsOpen)
                UiOverlays.DrawMenuScreen(_spriteBatch, _pixel, _update.Lines(), _update.Help);
            else if (_import.IsOpen)
                UiOverlays.DrawImportScreen(_spriteBatch, _pixel, _import); // s185: rows to the help line, a scroll bar
            else if (_options.IsOpen)
                UiOverlays.DrawOptionsScreen(_spriteBatch, _pixel, _options);
            else
                UiOverlays.DrawMenuScreen(_spriteBatch, _pixel, _mainMenu.Lines(), _mainMenu.Help);
            _spriteBatch.End();
            _screen.EndAndPresent(_spriteBatch, _pixel);
            EndHits();
            base.Draw(gameTime);
            return;
        }

        _assets.CycleSeconds = CycleSeconds(_session, gameTime.TotalGameTime.TotalSeconds); // the overworld's water
        NoteLeaving(_session); // s154: the leaving line before the first map frame
        _screen.Begin();
        _screen.BeginLayout(_spriteBatch);
        if (LeavingShown)
        {
            DrawLeaving();
            _spriteBatch.End();
            _screen.EndAndPresent(_spriteBatch, _pixel);
            EndHits();
            base.Draw(gameTime);
            return;
        }
        _frameScene = ModeScene(_session);
        DrawModeView(_session, _pixel, _assets);
        var messageShows = _messageBand.Shows(_message, gameTime.TotalGameTime.TotalSeconds, UiOverlays.TownMessageSeconds);
        if (ViewMode(_session) == GameMode.Combat && !_combatUi.P1FieldShown && _combatUi.FieldRun is null
            && !_combatUi.AmigaOrdersShown && !_combatUi.AmigaFieldMenuShown)
        {
            // The remaster combat panel uses the whole canvas to keep every portrait and rank picture complete;
            // its help and log are in the right column above the party cards.
        }
        else if (P1MapScreen(_session) is { } p1Map)
        {
            // s155: I Amiga's map screen: its menu bar on top, the remaster's message on the Amiga's line (TopEdge 192); a
            // dialog's own key help above it (or on it when there is no message). s156: the overworld's frame under it;
            // s158: the dungeon's pen-10 frame (h56 +0xe7c).
            if (ViewMode(_session) is GameMode.Overworld or GameMode.Dungeon)
                UiOverlays.DrawP1MapFrame(_spriteBatch, _pixel, p1Map, ViewMode(_session) == GameMode.Dungeon);
            var mapHelp = PadWording.Help(_help == HelpForMode(_session.Mode) ? "" : _help); // s183: the pad's wording
            // s183: the overworld's map reaches the line: a dialog's help goes on the idle bar, not over the map's foot.
            var barHelp = ViewMode(_session) == GameMode.Overworld && _p1MapBar.Open is null
                ? UiOverlays.P1MapHelpOnBar(p1Map, FontSources.Named(p1Map.Font, p1Map.FontFile) ?? PixelFont.Font, BandMessage, mapHelp,
                    barIdle: !P1MapBarTakesInput(_session))
                : null;
            // s193 (pad fix E29): the map's own help is blank here, so the pad never learned A / Start / RT+A / the right
            // stick: while the pad is the input, one row of them on the line, once no message holds it (as the town band)
            if (P1MapPadHint(_session, messageShows && BandMessage.Length > 0) is { } padHint)
                UiOverlays.DrawP1MapLine(_spriteBatch, _pixel, p1Map, "", padHint);
            else
                UiOverlays.DrawP1MapLine(_spriteBatch, _pixel, p1Map, BandMessage, barHelp is null ? mapHelp : "",
                    ViewMode(_session) == GameMode.Dungeon ? _session.LastDungeonMessageRecord : null);
            if (barHelp is { } onBar)
                UiOverlays.DrawP1MapBarHelp(_spriteBatch, _pixel, p1Map, onBar);
            else
                UiOverlays.DrawP1MapMenuBar(_spriteBatch, _pixel, p1Map, _p1MapBar, ViewMode(_session) != GameMode.Dungeon,
                    P1MapBarTakesInput(_session), _session);
        }
        else if (FullScreenOverworld(_session))
            DrawMapOnlyHud(_session, messageShows); // s185: and its help line (Game1.MapOnlyHelp.cs)
        else if (TownPictureScreen(_session))
            // The town picture fills the screen (s98): no status or help line; the message on a band for a few seconds,
            // the key help on it (clickable) while the mouse rests on the band's rows or below.
            UiOverlays.DrawTownHud(_spriteBatch, _pixel, _assets.TownScreen,
                messageShows && BandMessage.Length > 0 ? BandMessage : null,
                IsMouseVisible && _mouse.Position is { } at && at.Y >= UiOverlays.TownBandTop(_assets.TownScreen) ? _help
                // s183: the pad in use, its words (LT / RT shopper) once no message holds the band. Pad fix B9 (s188):
                // whenever the pad is the input, not only for a help with a pad twin: a dialog's wheel line, its Y / N
                // line and a help with no twin (the shares line) showed nothing, and the wheel's character nowhere.
                : PadWording.Active && (CurrentPrompt is { IsShares: true }
                    || !(messageShows && BandMessage.Length > 0)) ? PadWording.Help(_help) : null,
                CurrentGameSettings.OriginalScreens,
                gold: TownBarPlaces(_session) is null ? _session : null); // III Amiga: the gold line is on its town bar
        else if (ViewMode(_session) is GameMode.Overworld or GameMode.Dungeon && !CurrentGameSettings.StatusLines)
            // D-077 (manual): no status line; the key help only when a dialog shows its own. s183: and, the pad in use, the
            // map's own help in the pad's words (RT + A, the right stick: no keyboard player's help to learn them from).
            UiOverlays.DrawHud(_spriteBatch, _pixel, _session, _settings, BandMessage,
                _help == HelpForMode(_session.Mode) && !PadHelpShown(_help) ? "" : FieldBandHelp(_session), statusLine: null);
        else
            UiOverlays.DrawHud(_spriteBatch, _pixel, _session, _settings, BandMessage, FieldBandHelp(_session), FieldStatus(_session),
                messageKeys: _mouseUsed && _combatUi.AwaitingOrders);
        if (TownPictureScreen(_session) && TownBarPlaces(_session) is { } townBar) // s156: III Amiga's town bar
            UiOverlays.DrawTownMenuBar(_spriteBatch, _pixel, _assets, townBar, _townBar, TownBarPalette(_session),
                TownBarTakesInput(_session), _session);
        if (OutsideBarShown(_session))
            UiOverlays.DrawOutsideMenuBar(_spriteBatch, _pixel, _assets, _outsideBar, _session.Mode == GameMode.Dungeon,
                CurrentGameSettings.CombatSpeed, _session);
        if (InventoryScreenOpen)
            UiOverlays.DrawInventoryScreen(_spriteBatch, _pixel, _assets, _inventoryScreen, _mouse.Dragging ? _mouse.Position : null);
        else if (_showInventory)
            UiOverlays.DrawInventory(_spriteBatch, _pixel, _session, _data, _inventoryCursor, _inventoryCompare);
        if (_showBestiary)
            UiOverlays.DrawBestiary(_spriteBatch, _pixel, _assets, _data, _qol);
        if (_combatUi.P1FieldShown && !_showInventory && !_showBestiary)
        {
            UiOverlays.DrawP1CombatField(_spriteBatch, _pixel, _assets, _session, _combatUi, _data); // I Amiga's screen (s132)
            // s193 (pad fix E29): the field and its requesters cover the help line: the pad's words on one free row
            if (CombatInputUnobscured && _combatUi.AmigaPadHint(_session) is { } p1Hint)
                UiOverlays.DrawP1PadHint(_spriteBatch, _pixel, _assets, _session, _combatUi, p1Hint);
        }
        if (MapBox() is { } mapBox && _assets.Pack?.MapMessage is { } mapMessage)
        {
            if (mapBox.Window && mapMessage.Window is { } window)
                UiOverlays.DrawP1MapTextWindow(_spriteBatch, _pixel, mapMessage, window, mapBox.Rows); // s144: requester 21, timed
            else
                UiOverlays.DrawP1MapMessage(_spriteBatch, _pixel, mapMessage, mapBox.Rows); // s139: requester 23 over the map
            UiOverlays.Hits?.Clear(); // the requester takes every click (h80 +0x546 drops them)
            if (mapBox.Window && mapMessage.Window is { } rowWindow)
                UiOverlays.HitP1MapTextRows(rowWindow, mapBox.Rows.Count); // s153: but a row's GADGETUP ends the wait
        }
        else if (DosPageRows() is { } dosRows)
        {
            if (DosPageLayout() is { } dosLayout)
                UiOverlays.DrawP1DosPage(_spriteBatch, _pixel, dosLayout); // s156: at DOS's places on its 40 x 25 grid
            else
                UiOverlays.DrawTextPanel(_spriteBatch, _pixel, dosRows); // s154: I DOS's box, one key (scrolls when long)
            UiOverlays.Hits?.Clear(); // a key or a click turns the page
        }
        else if (MapWaitLines() is { } waitLines)
        {
            if (_assets.Pack?.MapMessage is { Window: { } window } layout)
                UiOverlays.DrawP1MapReport(_spriteBatch, _pixel, layout, window, waitLines); // s144: requester 21 with OK
            else
                UiOverlays.DrawTextPanel(_spriteBatch, _pixel, waitLines); // s141: the report in the remaster's panel
            UiOverlays.Hits?.Clear(); // a key or a click goes on, as requester 21's OK
        }
        else if (_combatUi.FieldRun is not null)
            UiOverlays.DrawCombatField(_spriteBatch, _pixel, _assets, _combatUi); // III Amiga: the whole screen (s117)
        else if (_combatUi.AmigaOrdersShown && !_showInventory && !_showBestiary)
            UiOverlays.DrawCombatOrders(_spriteBatch, _pixel, _assets, _session, _data, _combatUi,
                _mouse.LeftDown ? _mouse.Position : null); // III Amiga's orders screen (s118)
        else if (_combatUi.AmigaFieldMenuShown && !_showInventory && !_showBestiary)
        {
            UiOverlays.DrawFieldMenuScreen(_spriteBatch, _pixel, _assets, _session, _combatUi); // its menu bar (s119)
            if (CombatInputUnobscured && _combatUi.AmigaPadHint(_session) is { } p3Hint) // s193 (pad fix E29): on the round line's empty row
                UiOverlays.DrawFieldPadHint(_spriteBatch, _pixel, _assets, _combatUi, p3Hint);
        }
        else if (_session.Mode == GameMode.Combat && !_showInventory && !_showBestiary && !_combatUi.AwaitingInput)
        {
            if (_combatUi.P1FieldShown) // s183: I Amiga's field: the top rows, not the strip over its party row
                UiOverlays.DrawP1HitLine(_spriteBatch, _pixel, _assets, _session, _combatUi, CombatPresenter.FormatHitTooltip(_session));
            else
                UiOverlays.DrawTooltip(_spriteBatch, _pixel, CombatPresenter.FormatHitTooltip(_session));
        }
        if (_session.Mode == GameMode.Combat && _combatUi.InfoBox.Count > 0 && !InventoryScreenOpen)
        {
            // s154: on I Amiga's field Show names is requester 23 (timed) and Party health requester 21 with OK (s158 live:
            // at Top 45, as on the map), in the combat palette; a key or a click closes either.
            var p1Field = _combatUi.P1FieldShown && _combatUi.P1Layout is not null && !_showInventory && !_showBestiary;
            if (p1Field && _combatUi.P1NamesShown)
                UiOverlays.DrawP1NamesList(_spriteBatch, _pixel, _combatUi.P1Layout!, _combatUi.InfoBox);
            else if (p1Field && _combatUi.P1HealthShown && _assets.Pack?.MapMessage is { Window: { FightTop: int top } w } ml
                     && P1MapReports.FightHealth(w, _session) is { } health)
                UiOverlays.DrawP1MapReport(_spriteBatch, _pixel, ml, w.MovedTo(top), health, _combatUi.P1Layout!.Palette);
            else
                UiOverlays.DrawTextPanel(_spriteBatch, _pixel, _combatUi.InfoBox);
            if (p1Field && (_combatUi.P1NamesShown || _combatUi.P1HealthShown))
            {
                UiOverlays.Hits?.Clear();
                UiOverlays.Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, 200)) { Click = [Keys.Enter] });
            }
        }
        if (_session.DungeonPage is null)
            _pageEdges = null; // the next page draws a new frame (Amiga: the first text of a step)
        // s165: III's Damage Report comes before what follows it (a wiped party's Astral Plane, 0x1d4b4 after 0x1d4ea):
        // the prompt under it waits undrawn, so its scene (and the scene's sounds) start only after OK.
        if (CurrentPrompt is { } prompt && !_showInventory && !_showBestiary && !DosPagesUp && !_damageReport.Shown)
        {
            // II: the DECOR temple behind the defeat prompts; III: the Astral Plane behind the outcome (I keeps its box).
            // The scene is the whole ST screen; its text goes one line at a time on the bottom line, no box (s98).
            var shown = Shown(prompt);
            var scene = false;
            if (_confirm.Owns(prompt))
                UiOverlays.Hits?.Clear(); // s160: III's Are-you-sure question over the street / Guild: only its answers click
            if (DrawP1DefeatScreen(prompt))
            {
                scene = true; // s153: I Amiga's defeat screens (P1DefeatScreen), no box
                _defeatSceneLines = null;
                _defeatSceneHeld = false;
            }
            else if (DrawP1DosDefeat(prompt))
            {
                scene = true; // s154: I DOS's defeat lines, timed, no "press Enter"
                _defeatSceneLines = null;
                _defeatSceneHeld = false;
            }
            else if (_session.Game == GameId.P2 && _assets.Decor is not null
                && prompt.Kind is SessionPromptKind.PartyDefeat or SessionPromptKind.DefeatDemonSign or SessionPromptKind.DefeatOutcome)
            {
                var frame = _defeatClock.Observe(prompt.Kind, gameTime.TotalGameTime.TotalSeconds);
                var lines = DefeatScene.LinesShown(prompt.Kind, shown.Lines, frame);
                var fates = _prompts.ShownDefeat is { } defeat // each slot shows its fate once its line prints
                    ? DefeatScene.FatesShown(lines.Count(l => !string.IsNullOrWhiteSpace(l)),
                        _defeatPager.Index(prompt.Kind, lines), defeat.Fates.Count)
                    : 0;
                // s165: the banner is up once a step's report lines paged in front of it are through
                var banner = prompt.Kind == SessionPromptKind.PartyDefeat && !_defeatPager.HasMore(prompt.Kind, lines);
                UpdateDefeatSounds(prompt.Kind, frame, fates, banner); // s140: 23 / 22 / 20 / 21 (FORMAT.md 7.4)
                scene = UiOverlays.DrawDefeatScene(_spriteBatch, _pixel, _assets, prompt.Kind, frame,
                    !_defeatSounds.GhostsShown ? [] // s156: the ghost row after line 1's hold (0x12d54)
                        : DefeatScene.Row(_session, _prompts.ShownDefeat, prompt.Kind, frame.ShowBehindDoor, fates),
                    _defeatPager.Current(prompt.Kind, lines),
                    awaitsReturn: lines.Count >= shown.Lines.Count && !(banner && P2DefeatHolds)); // s165: goes on by itself
                _defeatSceneLines = lines;
                _defeatSceneHeld = lines.Count < shown.Lines.Count;
            }
            // III: the fates are told at the gates of the Astral Plane, the Amiga's full-screen D/W2.cmp (root 0x1dc16):
            // the whole picture, the portraits under it, one line at a time on text row 24 (AstralScene, s98).
            else if (_session.Game == GameId.P3 && prompt.Kind == SessionPromptKind.DefeatOutcome
                     && _assets?.SceneAstralPlane is not null && _prompts.ShownDefeat is { } astralDefeat)
            {
                _frameScene = SceneAstral; // s169: III DOS shows the Astral Plane in CGA palette 1 (root 0x1eba)
                var lines = shown.Lines;
                var index = _defeatPager.Index(prompt.Kind, lines);
                var fates = DefeatScene.FatesShown(lines.Count(l => !string.IsNullOrWhiteSpace(l)), index,
                    astralDefeat.Fates.Count);
                var colour = _assets.PaletteColour(AstralScene.Palette,
                    AstralScene.LineColour(astralDefeat, lines.Count(l => !string.IsNullOrWhiteSpace(l)), index));
                // s117: the demon's sound 45, then 48 / 9 with each member's fate, as its line shows (Sound switch).
                if (!ReferenceEquals(_astralSoundFor, astralDefeat) || _astralSoundLine != index)
                {
                    _astralSoundFor = astralDefeat;
                    _astralSoundLine = index;
                    if (AstralScene.SoundFor(astralDefeat, lines.Count(l => !string.IsNullOrWhiteSpace(l)), index) is int sound)
                    {
                        _gameSoundRaised = true; // s158: cuts a carried title tune, with the Sound switch off too
                        if (_settings.Sound && _assets.SoundEffects is { } effects)
                            _effects.Play(effects, [(0, sound)]);
                    }
                }
                scene = UiOverlays.DrawAstralScene(_spriteBatch, _pixel, _assets, index,
                    _defeatPager.Current(prompt.Kind, lines), AstralScene.Row(astralDefeat, fates), lineColour: colour);
                _defeatSceneLines = lines;
                _defeatSceneHeld = false;
            }
            // III: the gods' reward (Amiga 0x1ee82): intro lines, the god's picture over the party, the speech on the
            // parchment, one page per Return (GodsRewardScene, s102).
            else if (prompt.Kind == SessionPromptKind.RewardScene && _session.PendingRewardScene is { } reward && _assets is not null)
            {
                var pressKey = UiText.S("p3.reward.press_a_key");
                var pages = GodsRewardScene.Pages(reward, pressKey);
                if (_assets.ScrollFrame is { } rewardFrame && !ReferenceEquals(_rewardEdgesFor, reward))
                {
                    _rewardEdges = rewardFrame.Pick(n => Random.Shared.Next(n)); // the random edge pieces, once per scene
                    _rewardEdgesFor = reward;
                }
                var rewardPage = _defeatPager.Index(prompt.Kind, pages);
                // s169: III DOS: the intro, its flashes and the god in CGA palette 1 (ovl4 0x3e), the speech's page in 0
                _frameScene = rewardPage < GodsRewardScene.SpeechPage ? SceneReward : null;
                var now = gameTime.TotalGameTime.TotalSeconds;
                _rewardClock.Observe(reward, rewardPage, now, _assets.RewardScene?.Flash, n => Random.Shared.Next(n));
                scene = UiOverlays.DrawRewardScene(_spriteBatch, _pixel, _assets, reward, rewardPage,
                    pages, _rewardEdges, pressKey, _rewardClock.FlashAt(now));
                _defeatSceneLines = scene ? pages : null;
                _defeatSceneHeld = false;
            }
            // I: Zeus' victory screen (Amiga h32 +0x6d0): the picture fading in, the party's row, the speech (s127).
            else if (prompt.Kind == SessionPromptKind.VictoryScene && _victory is { } victory
                     && _assets?.Pack?.EndingScene is { } endingLayout)
            {
                _defeatSceneLines = null; // no pager: the screen's clock answers the prompt
                _defeatPager.Reset();
                _victoryView ??= new VictoryScreenView(GraphicsDevice, _assets, endingLayout);
                scene = _victoryView.Draw(_spriteBatch, _pixel, victory,
                    victory.At(gameTime.TotalGameTime.TotalSeconds - _victoryStart));
                // A click acts as a key (II's OK form and evnt_multi lines take a click; s128). I Amiga's clicks go to the
                // input port as a press and a release (HandlePrompt, s153).
                if (!endingLayout.QueuedKeys)
                    UiOverlays.Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, UiOverlays.ScreenBottom)) { Click = [Keys.Enter] });
            }
            // III's dungeon event texts on the parchment page (Amiga 0x1d982): the step's texts piled up, "Press a key"
            // or "Press a number" at the foot; the frame's random edges picked once per page.
            else if (EventPageFor(prompt) is { } eventPage && _assets?.ScrollFrame is { } pageFrame)
            {
                _frameScene = null; // s169: III DOS's text page selects palette 0 (root 0x4a3)
                _defeatSceneLines = null; // no pager: a key answers the page
                _defeatPager.Reset();
                _pageEdges ??= pageFrame.Pick(n => Random.Shared.Next(n));
                UiOverlays.DrawScrollFrame(_spriteBatch, _pixel, _assets, pageFrame, _pageEdges, eventPage.Lines,
                    EventPageFooter(eventPage.Footer, eventPage.AnyKey, EventPadFooter(prompt)), eventPage.AnyKey);
                scene = true;
            }
            // s153: I Amiga's event menu, the MESS window with requester 25's buttons (h41 +0x1440).
            else if (P1MapMenuFor(prompt) is { } menuWindow && _session.DungeonEventPrompt is { } menuEvent)
            {
                _defeatSceneLines = null;
                _defeatPager.Reset();
                UiOverlays.DrawP1MapMenu(_spriteBatch, _pixel, _assets!.Pack!.MapMessage!, menuWindow, menuEvent.Message.Lines,
                    menuEvent.Message.OptionCells is { } cells && cells.Count == menuEvent.Message.Options.Count
                        ? cells : menuEvent.Message.Options, PadPromptCursor(prompt));
                scene = true;
            }
            // s154: I Amiga's requesters 0 / 22 for the map's questions (h86 +0x32a, the Black Knights' h65 +0x168).
            else if (P1MapRequesterFor(prompt) is { } requester)
            {
                _defeatSceneLines = null;
                _defeatPager.Reset();
                UiOverlays.DrawP1Requester(_spriteBatch, _pixel, _assets!.Pack!.MapRequesters!, requester);
                if (PadRequesterMark(prompt, requester) is { } mark) // s182: the pad's hidden cursor, outlined
                    UiOverlays.DrawPadMark(_spriteBatch, _pixel, mark);
                scene = true;
            }
            // s157: the manual / no-fee bank's share question in the Bank's window 9 style (FORMAT.md 8.2).
            else if (AmigaShareWindowFor(prompt) is { } shareView)
            {
                _defeatSceneLines = null;
                _defeatPager.Reset();
                scene = UiOverlays.DrawAmigaWindow(_spriteBatch, _pixel, _assets!, _assets!.KeepWindows!, shareView,
                    _mouse.LeftDown ? _mouse.Position : null);
            }
            // s163: III's `core.p3.who_will_disarm` in the Amiga's window 6 (0x127ce(92)).
            else if (AmigaDisarmWindowFor(prompt) is { } disarmView)
            {
                _defeatSceneLines = null;
                _defeatPager.Reset();
                scene = UiOverlays.DrawAmigaWindow(_spriteBatch, _pixel, _assets!, _assets!.KeepWindows!, disarmView,
                    _mouse.LeftDown ? _mouse.Position : null, inDungeon: _session.Dungeon is not null);
            }
            // III's keep questions in the Amiga's own OK / CANCEL windows (FORMAT.md §2.3; user s103).
            else if (KeepWindowFor(prompt) is { } keep)
            {
                _defeatSceneLines = null; // no pager here: Return answers the window
                _defeatPager.Reset();
                scene = UiOverlays.DrawKeepWindow(_spriteBatch, _pixel, _assets!, _assets!.KeepWindows!, keep, shown.Lines,
                    _mouse.LeftDown ? _mouse.Position : null, inDungeon: _session.Dungeon is not null);
            }
            if (!scene)
            {
                _defeatSceneLines = null;
                _defeatPager.Reset();
                UiOverlays.DrawPrompt(_spriteBatch, _pixel, shown, _promptMenu.CursorFor(prompt));
            }
        }
        else
        {
            _defeatSceneLines = null;
            _defeatPager.Reset();
        }
        if (_dosDialog.IsOpen)
            UiOverlays.DrawTextPanel(_spriteBatch, _pixel, _dosDialog.Lines, null, _dosDialog.Page);
        if (OpenModal is { } modal)
        {
            var lines = modal.Lines(_session, _data);
            // The mouse's typing grid, only once the mouse is used (keyboard play draws the panel as before).
            var typing = _mouseUsed && modal is ITypedEntry { TypingAlphabet: { } alphabet } ? TypingGrid.For(alphabet) : null;
            // s154: I Amiga's map Health is requester 21 with OK (h50 +0x420 … +0x54a); a key or a click closes it.
            if (ReferenceEquals(modal, _healthDialog) && P1MapHealth(_session) is { } health
                && _assets?.Pack?.MapMessage is { Window: { } healthWindow } healthLayout)
            {
                UiOverlays.DrawP1MapReport(_spriteBatch, _pixel, healthLayout, healthWindow, health);
                UiOverlays.Hits?.Clear();
                UiOverlays.Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, 200)) { Click = [Keys.Enter] });
            }
            // A picture scroll (III's Scroll IV) shows the pack's full-screen picture, as the Amiga does (0x1f7ee); s169: a
            // pack whose page places it (III DOS, root 0x124) draws the text page's frame and the picture on it.
            else if (modal is UseItemDialog { PictureItem: { } scrollItem } pictureDialog
                     && _assets?.GetScrollPicture(scrollItem) is { } scrollPicture)
            {
                if (_assets.ScrollFrame is { PictureAt: [var px, var py] } pictureFrame)
                {
                    if (_scrollEdges is null || _scrollEdgesRead != pictureDialog.ScrollReads)
                    {
                        _scrollEdges = pictureFrame.Pick(n => Random.Shared.Next(n));
                        _scrollEdgesRead = pictureDialog.ScrollReads;
                    }
                    UiOverlays.DrawScrollFrame(_spriteBatch, _pixel, _assets, pictureFrame, _scrollEdges, []);
                    _spriteBatch.Draw(scrollPicture, new Rectangle(px, py, scrollPicture.Width, scrollPicture.Height), Color.White);
                    if (pictureFrame.PictureCaption is { } caption) // s170: useitem.scroll_picture_caption under the map (root 0x1b4)
                        PixelFont.Draw(_spriteBatch, _pixel, caption.X, caption.Y, UiText.S(caption.Text),
                            VirtualScreen.ParseColor(caption.Colour));
                }
                else
                    _spriteBatch.Draw(scrollPicture, new Rectangle(0, 0, 320, 200), Color.White);
            }
            // A text scroll on the pack's parchment (III Amiga 0x1e2c2), its random edges picked once per reading.
            else if (modal is UseItemDialog { ScrollItem: not null } reading && _assets?.ScrollFrame is { } frame)
            {
                if (_scrollEdges is null || _scrollEdgesRead != reading.ScrollReads)
                {
                    _scrollEdges = frame.Pick(n => Random.Shared.Next(n));
                    _scrollEdgesRead = reading.ScrollReads;
                }
                UiOverlays.DrawScrollFrame(_spriteBatch, _pixel, _assets, frame, _scrollEdges, lines);
            }
            // III Amiga's Bank / Trade windows (s117), with the game's AMIGA SCREENS switch on.
            else if (modal is IAmigaWindowDialog amiga && _session.Game == GameId.P3 && CurrentGameSettings.OriginalScreens
                     && _assets?.KeepWindows is { } windows && amiga.View(_session, _data, windows) is { } view
                     && UiOverlays.DrawAmigaWindow(_spriteBatch, _pixel, _assets, windows, view, _mouse.LeftDown ? _mouse.Position : null,
                         page: (modal as IPagedDialog)?.Page))
            {
            }
            // s154: I Amiga's character sheet (requester 14) for Inspect's sheet in the town, under AMIGA SCREENS.
            else if (ReferenceEquals(modal, _inspectDialog) && P1SheetFor(_session) is { } sheet)
            {
                UiOverlays.DrawP1Requester(_spriteBatch, _pixel, _assets!.Pack!.MapRequesters!, sheet);
                // s192 (pad fix C18): the sheet's buttons draw no highlight; with the pad in use, the one A takes is outlined
                if (PadWording.Active && _inspectDialog.SheetMember is not null
                    && sheet.Buttons.FirstOrDefault(b => b.Index == _inspectDialog.SheetCursor) is { } lit)
                    UiOverlays.DrawPadMark(_spriteBatch, _pixel, new Rectangle(lit.Rect.X, lit.Rect.Y, lit.Rect.W, lit.Rect.H));
            }
            // s162: III's Add move question (S445) in the Amiga's window 1 (0xd24c → 0xaae2), the list stepped aside.
            else if (ReferenceEquals(modal, _guildAddDialog) && _guildAddDialog is { Confirming: true, AmigaWindow: true }
                     && ConfirmWindowLayout() is { } addWindow
                     && UiOverlays.DrawKeepWindow(_spriteBatch, _pixel, _assets!, _assets!.KeepWindows!, addWindow,
                         GuildAddDialog.MoveConfirmPromptFor(_session.Game).Lines, _mouse.LeftDown ? _mouse.Position : null))
            {
            }
            // s163: the scroll's printout questions in the town's Use item as requester 0.
            else if (P1UseItemRequesterFor(modal, _session) is { } useItemQuestion)
                UiOverlays.DrawP1Requester(_spriteBatch, _pixel, _assets!.Pack!.MapRequesters!, useItemQuestion);
            // s156: the Guild's h86 questions (Add's move, Training's cost) as requester 0.
            else if (P1TownRequesterFor(modal, _session) is { } townQuestion)
                UiOverlays.DrawP1Requester(_spriteBatch, _pixel, _assets!.Pack!.MapRequesters!, townQuestion);
            // s156: Guild › New's roll as requester 14 with PURGE / KEEP (h23 +0xc08).
            else if (ReferenceEquals(modal, _guildNewDialog) && P1RolledSheetFor(_session) is { } rolledSheet)
                UiOverlays.DrawP1Requester(_spriteBatch, _pixel, _assets!.Pack!.MapRequesters!, rolledSheet);
            else
                UiOverlays.DrawTextPanel(_spriteBatch, _pixel, lines, typing, (modal as IPagedDialog)?.Page,
                    modal is IWidePanel { WidePanel: true });
            if (ReferenceEquals(modal, _continueDialog))
                HitContinueRows(lines);
            if (ReferenceEquals(modal, _treasuryDialog) && UiOverlays.Hits is { } treasuryHits)
                treasuryHits.Wheel = (Keys.Up, Keys.Down); // the wheel scrolls the treasury's ledger (its own page)
        }
        // III Amiga's credits (File › Info, window 0, s140; the town's ? / / too, s141) over the screen; only its OK takes
        // a click.
        if (_outsideBar.CreditsShown && _session is { } creditsSession
            && _assets?.KeepWindows is { Credits: { } creditsWindow } creditsLayout)
        {
            UiOverlays.Hits?.Clear();
            UiOverlays.DrawCreditsWindow(_spriteBatch, _pixel, _assets, creditsLayout, creditsWindow,
                creditsSession.Mode == GameMode.Dungeon, _mouse.LeftDown ? _mouse.Position : null);
        }
        // III Amiga's Party Status (Options › Party Status / P, window 22, s141), the trip summary; only its OK takes a click.
        if (_outsideBar.StatusShown && _session is { } statusSession
            && _assets?.KeepWindows is { PartyStatus: { } statusWindow } statusLayout)
        {
            UiOverlays.Hits?.Clear();
            UiOverlays.DrawPartyStatusWindow(_spriteBatch, _pixel, _assets, statusLayout, statusWindow,
                P3PartyStatus.Of(statusSession, statusWindow.Kinds) is var status && statusSession.TreasuryOn
                    ? status with { Gold = statusSession.SpendableGold } // P3 QoL: the Guild treasury's spendable gold
                    : status,
                statusSession.Mode == GameMode.Dungeon,
                _mouse.LeftDown ? _mouse.Position : null);
        }
        // s154: I Amiga's STYX / OCEAN OK requester over the map; only its OK takes a click.
        if (P1OkScene() is { } okScene)
        {
            UiOverlays.Hits?.Clear();
            UiOverlays.DrawP1Requester(_spriteBatch, _pixel, _assets!.Pack!.MapRequesters!, okScene, _assets);
        }
        // III Amiga's message box (window 7, s135) over everything; only its OK takes a click.
        if (_amigaMessage.Shown && _assets?.KeepWindows is { Message: { } messageWindow } messageLayout)
        {
            UiOverlays.Hits?.Clear();
            UiOverlays.DrawMessageWindow(_spriteBatch, _pixel, _assets, messageLayout, messageWindow, _amigaMessage.Text,
                _mouse.LeftDown ? _mouse.Position : null, inDungeon: _session.Mode == GameMode.Dungeon);
        }
        // s165: II ST's timed field box (0xa18c: tree 40 at (12, 75)) in the pens of the palette the mode shows.
        if (_p2FieldBox.Rows is { } fieldBoxRows && _assets?.Pack?.StCombatLines?.EndBoxes is { } fieldBoxes)
        {
            UiOverlays.Hits?.Clear(); // any click ends it (Update), nothing under it takes one
            UiOverlays.DrawP2EndBox(_spriteBatch, _pixel, fieldBoxes, fieldBoxRows,
                FontSources.ForRole(_assets.Pack, UiOverlays.P2EndBoxFontRole), P2FieldBoxPalette(_p2FieldBox.Mode));
        }
        // s165: III's Damage Report over everything; only its OK takes a click (the panel: a click anywhere).
        if (_damageReport.Report is { } damage)
        {
            UiOverlays.Hits?.Clear();
            if (!(DamageReportWindow is { } reportWindow && UiOverlays.DrawDamageReportWindow(_spriteBatch, _pixel, _assets!,
                    _assets!.KeepWindows!, reportWindow, damage, _mouse.LeftDown ? _mouse.Position : null,
                    inDungeon: _session.Mode == GameMode.Dungeon)))
            {
                UiOverlays.DrawTextPanel(_spriteBatch, _pixel, P3DamageReportBox.PanelLines(damage));
                UiOverlays.Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, UiOverlays.ScreenBottom)) { Click = [Keys.Enter] });
            }
        }
        // s182 (Pad coverage): the pad's pick in a fight, outlined on whatever the screen drew for it (the help line names it too).
        if (CombatInputUnobscured && _session is { } padSession && _combatUi.AwaitingInput
            && CombatPad.MarkFor(_combatPad.Picked(_combatUi.PadStep(padSession)), UiOverlays.Hits) is { } padMark)
            UiOverlays.DrawPadMark(_spriteBatch, _pixel, padMark);
        // s158: II ST's alert box (form_alert) over everything; only its button takes a click.
        if (_p2Alert.Current is { } stAlert && _assets?.Pack?.FieldSounds?.Alerts?.Look is { } alertLook)
        {
            UiOverlays.Hits?.Clear();
            var (fill, ink) = StAlertColours(alertLook, _session.Mode);
            UiOverlays.DrawStAlert(_spriteBatch, _pixel, alertLook, stAlert, fill, ink,
                _mouse.LeftDown ? _mouse.Position : null, FontSources.ForRole(_assets.Pack, UiOverlays.P2AlertFontRole));
        }
        DrawUnifiedTown();
        _spriteBatch.End();
        if (ScenePaletteFor(_frameScene) is { } scenePalette)
            _screen.PaletteOverride = scenePalette; // s169: III DOS's CGA palette 1 screens
        _screen.EndAndPresent(_spriteBatch, _pixel);
        EndHits();

        Window.Title = UiText.F("game.phantasie_x_x", UiText.Name(_session.Mode), _message);
        if (Window.Title.Length > 100)
            Window.Title = Window.Title[..97] + "...";
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _settings.Save();
        _qol.Save();
        _assets?.Dispose();
        _screen?.Dispose();
        base.UnloadContent();
    }

    /// <summary>The running game's settings (presentation QoL switches, graphics; T5.0).</summary>
    private GameSettings CurrentGameSettings => _settings.Game(_session!.Game);

    private bool CombatInputUnobscured => OpenModal is null && !_dosDialog.IsOpen && CurrentPrompt is null
        && !InventoryScreenOpen && !_showInventory && !_showBestiary && !_amigaMessage.Shown
        && !_outsideBar.CreditsShown && !_outsideBar.StatusShown && !_damageReport.Shown && !_p2Alert.Shown;

    /// <returns>True when Esc was used this frame (backed out or opened the game menu).</returns>
    private bool HandleGlobalInput(KeyboardState keys, GamePadState pad)
    {
        var s = _session!;

        if (InventoryScreenOpen)
        {
            HandleInventoryScreenKeys(keys, pad); // it owns every key, Esc included
            return true;
        }

        // III Amiga's orders screen (s118): with a member picked (its option, the rank window, the spell list) Esc goes
        // back to the member boxes / answers CANCEL instead of opening the game menu; it closes window 7 (any key does, s134).
        // s183: I Amiga's the same (with a member picked, back to the member boxes: HandleP1OrderKey's Esc). s187 (pad fix A3):
        // and I Amiga's Party Options with a menu pulled down (Esc closes it, HandleFieldMenuKey).
        if ((_combatUi.AmigaOrdersShown && (_combatUi.OrderMember >= 0 || _combatUi.AmigaMessageShown)
             || _combatUi.P1OrdersShown && _combatUi.OrderMember >= 0
             || (_combatUi.AmigaFieldMenuShown || _combatUi.P1FieldMenuShown) && _combatUi.FieldMenuOpen is not null // s187 A3: I's too
             || _combatUi.AsksDosRank(s)) // s156: DOS III's rank menu: Esc answers Two
            && _input.Pressed(GameAction.GameMenu, keys, pad))
        {
            _combatUi.HandleKey(s, Keys.Escape);
            SetFeedback(_combatUi.LastLine, HelpForCombat());
            return true;
        }

        // s183 (Pad leftovers): the pad's B on a DOS order list goes back as Backspace does, past the first member's menu.
        if (_input.NewButtons(pad).Contains(Buttons.B) && !_input.Pressed(GameAction.GameMenu, keys, default)
            && _combatUi.PadBackRestartsOrders(s))
        {
            _combatUi.HandleKey(s, Keys.Back);
            SetFeedback(_combatUi.LastLine, HelpForCombat());
            return true;
        }

        if (_input.Pressed(GameAction.GameMenu, keys, pad))
        {
            BackOut(s);
            return true;
        }

        if (_input.Pressed(GameAction.CycleScale, keys, pad))
        {
            _screen?.CycleScale();
            if (_screen is not null)
            {
                _settings.IntegerScale = _screen.Scale;
                ApplyWindowScale();
                _settings.Save();
            }

            SetFeedback(UiText.F("game.scale_x_x", _settings.IntegerScale), HelpForMode(s.Mode));
        }

        if (_input.Pressed(GameAction.ToggleMapLayout, keys, pad))
        {
            _settings.MapOnly = !_settings.MapOnly;
            _settings.Save();
        }
        if (_input.Pressed(GameAction.ToggleAutoMap, keys, pad))
        {
            // s172: the game's AUTO-MAP BOX option itself (GameSettings.AutoMap), so the Options row shows what Tab set.
            CurrentGameSettings.AutoMap = !CurrentGameSettings.AutoMap;
            _settings.Save();
            SetFeedback(CurrentGameSettings.AutoMap ? UiText.S("game.auto_map_on") : UiText.S("game.auto_map_off"), HelpForMode(s.Mode));
        }

        if (_input.Pressed(GameAction.ToggleInventory, keys, pad) && !_showInventory
            && (CurrentGameSettings.InventoryScreen || s.Options.ModernInventory && s.Game != GameId.P4))
            OpenInventoryScreen(s, pad); // QoL: the paperdoll + stash screen (Game1.Inventory.cs)
        else if (_input.Pressed(GameAction.ToggleInventory, keys, pad))
        {
            _showInventory = !_showInventory;
            _showBestiary = false;
            _inventoryCompare = false;
        }

        // B is the manual's Bank key on the town bar (D-066), and Fire Bow on III Amiga's orders screen (s118, 0x21fdc).
        if (_input.Pressed(GameAction.ToggleBestiary, keys, pad) && !(s.Mode == GameMode.Town && _townMenu.Place == TownPlace.Menu)
            && !((_combatUi.AmigaOrdersShown || _combatUi.AmigaFieldMenuShown) && keys.IsKeyDown(Keys.B)))
        {
            _showBestiary = !_showBestiary;
            _showInventory = false;
        }

        if (_showInventory)
        {
            if (_input.PressedMove(CardinalDirection.North, keys, pad))
                _inventoryCursor = Math.Max(0, _inventoryCursor - 1);
            if (_input.PressedMove(CardinalDirection.South, keys, pad))
                _inventoryCursor = Math.Min(_inventoryCursor + 1,
                    Math.Max(0, s.Party.Members.Sum(m => m.InventoryItemIds.Count(id => id != 0)) - 1));
            if (_input.Pressed(GameAction.Confirm, keys, pad))
                _inventoryCompare = !_inventoryCompare;
        }

        if (_showBestiary)
        {
            if (_input.PressedMove(CardinalDirection.North, keys, pad))
                UiOverlays.BestiaryScroll.ScrollBy(-1);
            if (_input.PressedMove(CardinalDirection.South, keys, pad))
                UiOverlays.BestiaryScroll.ScrollBy(1);
        }

        if (_input.Pressed(GameAction.Save, keys, pad))
            SaveOrAsk(s); // s160: III's town asks first
        if (_input.Pressed(GameAction.Load, keys, pad))
            LoadOrAsk(s);
        return false;
    }

    /// <summary>
    /// Esc: closes an overlay, leaves a town building or list for the bar, leaves the Guild for its town; anywhere without
    /// a parent menu (town bar, wilderness, dungeon, combat, a prompt, the starting Guild) it opens the game menu.
    /// </summary>
    private void BackOut(GameSession s)
    {
        if (_showInventory || _showBestiary)
        {
            _showInventory = _showBestiary = false;
            _inventoryCompare = false;
        }
        else if (CurrentPrompt is null && !_combatUi.IsPresenting && s.Mode == GameMode.Town && _townMenu.Back())
            SetFeedback(s.CurrentTown?.Name ?? "", HelpForMode(s.Mode));
        else if (CurrentPrompt is null && !_combatUi.IsPresenting && s.Mode == GameMode.Guild)
        {
            PlayClick(); // s141: as the Guild options' Exit Guild pick (0x56a)
            s.LeaveGuild();
            RefreshTownStock();
            SetFeedback(s.LastMessage, HelpForMode(s.Mode));
        }
        else
            OpenGameMenu();
    }

    /// <summary>
    /// Opens the game menu with its title on the message line; s183: the line it replaced comes back when the menu closes
    /// (<see cref="GameMenuDialog.LineAfterClose"/>), so "GAME MENU" never stays on the line.
    /// </summary>
    private void OpenGameMenu()
    {
        if (_message != GameMenuDialog.Title)
            _lineBeforeGameMenu = _message;
        _gameMenu.Open();
        SetFeedback(GameMenuDialog.Title, _gameMenu.Help);
    }

    private void RunGameMenuChoice(GameMenuChoice choice)
    {
        var s = _session!;
        switch (choice)
        {
            case GameMenuChoice.Save:
                SaveOrAsk(s); // s160: III's town asks first (DOS III Options root 0x8348)
                break;
            case GameMenuChoice.Load:
                LoadOrAsk(s);
                break;
            case GameMenuChoice.Options:
                _options.Open(s);
                _optionsFromGameMenu = true;
                break;
            case GameMenuChoice.MainMenu:
                ReturnToMainMenu();
                break;
            case GameMenuChoice.DosImport when s.Game == GameId.P2: // T5: the row is II's "Transfer from Phantasie I"
                _dosDialog.OpenTransfer(ClientSettings.ReadSavePathFor(GameId.P1), GameId.P1);
                SetFeedback(_message, _dosDialog.Help);
                break;
            case GameMenuChoice.TransferFromI or GameMenuChoice.TransferFromII when s.Game == GameId.P3: // T5, D3-030
                var eraseFirst = s.Options.P3Transfer().EraseRoster;
                if (eraseFirst && s.Mode != GameMode.Town)
                {
                    SetFeedback(UiText.S("transfer.erase_in_town_only"), HelpForMode(s.Mode));
                    break;
                }
                var from = choice == GameMenuChoice.TransferFromI ? GameId.P1 : GameId.P2;
                _dosDialog.OpenTransferIntoIII(ClientSettings.ReadSavePathFor(from), from, eraseFirst);
                SetFeedback(_message, _dosDialog.Help);
                break;
            case GameMenuChoice.TransferFromII: // T5: I's row, the ST utility's II → I
                _dosDialog.OpenTransfer(ClientSettings.ReadSavePathFor(GameId.P2), GameId.P2);
                SetFeedback(_message, _dosDialog.Help);
                break;
            case GameMenuChoice.DosImport or GameMenuChoice.DosExport when s.Game != GameId.P1:
                SetFeedback(UiText.S("dossave.phantasie_i_only"), HelpForMode(s.Mode)); // DOS saves are I's format
                break;
            case GameMenuChoice.DosImport:
                _dosDialog.OpenImport();
                SetFeedback(_message, _dosDialog.Help);
                break;
            case GameMenuChoice.DosExport:
                if (_dosDialog.OpenExport(s) is { } refused)
                    SetFeedback(refused, HelpForMode(s.Mode));
                else
                    SetFeedback(_message, _dosDialog.Help);
                break;
            case GameMenuChoice.Quit:
                ExitFor("game menu Quit");
                break;
        }
    }

    /// <summary>
    /// The modal question Core is waiting on (<see cref="SessionPrompts"/>: party defeat, event menu, exit pair, KEEP IT?,
    /// wilderness inn), or III's "Are you sure …?" question the Client asks (<see cref="P3ConfirmQuestion"/>, s160), or null.
    /// Hidden while the combat presenter still owns the screen.
    /// </summary>
    private SessionPrompt? CurrentPrompt =>
        _combatUi.IsPresenting ? null
        : _confirm.Prompt ?? (P1PromptHeld(_prompts.Current) ? null : _prompts.Current); // s154: held for its sound

    /// <summary>
    /// DOS menus are modal: nothing moves until the prompt is answered. Arrows move the highlight and Return/Space picks
    /// it; each menu id reopens on its last choice (<see cref="PromptMenu"/>, s45/s58). Y/N, the first letter of a
    /// "D)ISARM OR L)EAVE" menu and 1..n still answer directly (manual p.10). One answer per frame so the key can't also
    /// act as a step.
    /// </summary>
    private void HandlePrompt(KeyboardState keys, GamePadState pad, SessionPrompt prompt)
    {
        if (HandleP1DefeatKeys(keys, pad, prompt))
            return;
        if (HandleP1DosDefeatKeys(keys, pad, prompt)) // s183: a key ends the DOS defeat's current line only
            return;
        if (HandleP2DefeatHoldKeys(keys, pad, prompt))
            return;
        if (_victory is not null && prompt.Kind == SessionPromptKind.VictoryScene)
        {
            // I Amiga (s130): presses and releases both reach the input port; II reads presses only (VictoryScreen).
            var t = _updateSeconds - _victoryStart;
            foreach (var _ in _input.NewKeys(keys))
                _victory.Key(t);
            foreach (var _ in _input.ReleasedKeys(keys))
                _victory.Release(t);
            var (padPresses, padReleases) = ScenePadEvents(pad); // s182: A / the D-pad as a key's press and release
            for (var i = 0; i < padPresses; i++)
                _victory.Key(t);
            for (var i = 0; i < padReleases; i++)
                _victory.Release(t);
            if (_assets?.Pack?.EndingScene is { QueuedKeys: true })
            {
                // s153 live: a click is two events in I's input port (MOUSEBUTTONS down and up), as a key's press and release.
                if (_mouse.LeftPressed && _mouse.Position is not null)
                    _victory.Key(t);
                if (_mouse.LeftReleased)
                    _victory.Release(t);
            }
            return;
        }
        foreach (var newKey in _input.NewKeys(keys).Concat(PromptPadKeys(prompt, pad))) // s182: the pad's arrows and A
        {
            var key = newKey;
            if (key is Keys.PageUp or Keys.PageDown && UiOverlays.PromptScroll.CanScroll)
            {
                UiOverlays.PromptScroll.HandleKey(key); // a prompt with more text than the panel holds (s99)
                continue;
            }
            if (key is Keys.PageUp or Keys.PageDown && EventPageFor(prompt) is not null && UiOverlays.ScrollFrameScroll.CanScroll)
            {
                UiOverlays.ScrollFrameScroll.HandleKey(key); // a page with more rows than the parchment holds
                continue;
            }
            if (AmigaShareWindowFor(prompt) is not null)
            {
                if (AmigaShareWindow.Choice(key) is not { } shareKey)
                {
                    _prompts.CancelBankDeposit(); // CANCEL: nothing deposited (s157)
                    break;
                }
                key = shareKey; // O = OK
            }
            if (AmigaDisarmWindowFor(prompt) is not null)
            {
                if (AmigaDisarmWindow.Choice(key) is not { } disarmKey)
                {
                    if (prompt.EscapeChoice is int cancel)
                        AnswerPrompt(prompt, cancel); // CANCEL: the trap is left as on No (ovl4 0x11bc, Amiga 0x1c71a)
                    break;
                }
                key = disarmKey; // O = OK
            }
            var choice = _confirm.Owns(prompt) ? _confirm.Choice(key, ConfirmInWindow, _promptMenu)
                : KeepWindowFor(prompt) is not null ? KeepWindowChoice(key)
                : EventPageFor(prompt) is { } page ? EventPageChoice(prompt, page, key)
                : _promptMenu.HandleKey(prompt, key);
            if (choice is null)
                continue;
            // II's defeat scene: Return first shows the page's next line on the bottom line (DefeatScenePager). While
            // the door sinks the judgement is still to come, so Return waits for it (the ST takes no key there either).
            if (_defeatSceneLines is { } sceneLines && (_defeatSceneHeld || _defeatPager.Advance(prompt.Kind, sceneLines)))
                break;
            AnswerPrompt(prompt, choice.Value);
            break;
        }
    }

    /// <summary>III's event page as drawn: its rows and its foot line.</summary>
    private sealed record EventPage(IReadOnlyList<string> Lines, string Footer, bool AnyKey);

    /// <summary>The parchment covers the HUD: put its pad hint (or selected numbered choice) in the footer itself.</summary>
    public static string EventPageFooter(string original, bool anyKey, string? selected) =>
        !PadWording.Active ? original : selected ?? PadWording.S(anyKey ? "game.event_page_any_key" : "game.event_page_number");

    /// <summary>
    /// III's dungeon event page for <paramref name="prompt"/> (Amiga <c>0x1d982</c>): a text waiting for a key ("Press a
    /// key"), or an event menu whose text is on the page ("Press a number"); null otherwise (the Yes / No and "Who will
    /// disarm" questions keep the prompt box) or when the pack has no parchment.
    /// </summary>
    private EventPage? EventPageFor(SessionPrompt prompt)
    {
        if (_session is not { Game: GameId.P3 } s || _assets?.ScrollFrame is null)
            return null;
        if (prompt.Kind == SessionPromptKind.DungeonPage)
            return new EventPage(prompt.Lines, UiText.S("p3.reward.press_a_key"), AnyKey: true);
        if (prompt.Kind == SessionPromptKind.DungeonEvent && s.DungeonPage is { WaitsForKey: false } page && !prompt.IsYesNo)
            return new EventPage(page.Lines, UiText.S("p3.page.press_a_number"), AnyKey: false);
        return null;
    }

    /// <summary>
    /// A key on the event page: any key answers "Press a key" (Amiga <c>0x1e8c4(1)</c>: any key or a click); a menu takes
    /// only its numbers 1…n (<c>0x1c902</c> loops until one comes).
    /// </summary>
    public static int? EventPageChoice(SessionPrompt prompt, bool anyKey, Keys key) =>
        anyKey ? 1 : prompt.Options.Count > 1 ? PromptMenu.DirectChoice(prompt, key) : null;

    private static int? EventPageChoice(SessionPrompt prompt, EventPage page, Keys key) =>
        EventPageChoice(prompt, page.AnyKey, key);

    /// <summary>
    /// s153: I Amiga's requester 21 with a menu (pack <c>map_message.window.menu</c>) for a dungeon event's MESS menu under
    /// AMIGA SCREENS; null otherwise (the remaster's prompt box).
    /// </summary>
    private Data.MapMessageWindow? P1MapMenuFor(SessionPrompt prompt) =>
        _session is { Game: GameId.P1 } s && CurrentGameSettings.OriginalScreens && prompt.Kind == SessionPromptKind.DungeonEvent
        && !prompt.IsYesNo && s.DungeonEventPrompt is { Message.Options.Count: >= 2 }
        && _assets?.Pack?.MapMessage?.Window is { Menu: not null } window
            ? window
            : null;

    /// <summary>
    /// s157: III's Amiga look is on and the pack has the Bank's window 9 and the OK / CANCEL art: the manual / no-fee bank's
    /// withdraw dialog and share question draw in its style (FORMAT.md 8.2) and take its keys. One gate for both.
    /// </summary>
    private bool AmigaBankWindows =>
        _session is { Game: GameId.P3 } && CurrentGameSettings.OriginalScreens && _assets?.KeepWindows is { Bank: not null }
        && _assets.Get(PictureNames.Button("ok")) is not null && _assets.Get(PictureNames.Button("cancel")) is not null;

    /// <summary>
    /// s163: III's disarm question in the Amiga's window 6 (<see cref="AmigaDisarmWindow"/>) under AMIGA SCREENS with the pack's
    /// window and OK / CANCEL art, or null (the prompt box).
    /// </summary>
    private AmigaWindowView? AmigaDisarmWindowFor(SessionPrompt prompt) =>
        _session is { Game: GameId.P3 } s && CurrentGameSettings.OriginalScreens && _assets?.KeepWindows is { } k
        && _assets.Get(PictureNames.Button("ok")) is not null && _assets.Get(PictureNames.Button("cancel")) is not null
            ? AmigaDisarmWindow.View(s, prompt, _promptMenu.CursorFor(prompt), k)
            : null;

    /// <summary>The share question's Amiga window (s157, <see cref="AmigaShareWindow"/>), or null (the prompt box).</summary>
    private AmigaWindowView? AmigaShareWindowFor(SessionPrompt prompt) =>
        AmigaBankWindows ? AmigaShareWindow.View(prompt, _promptMenu.CursorFor(prompt), _assets!.KeepWindows!) : null;

    /// <summary>
    /// The pack's window for a III keep question (dungeon "Keep …?" = window 1, a fight find with its finder = window 23),
    /// or null: another game, another prompt, or a pack without them (the DOS Yes / No bar then).
    /// </summary>
    private KeepWindowsLayout.Window? KeepWindowFor(SessionPrompt prompt) =>
        _confirm.Owns(prompt) ? (ConfirmInWindow ? ConfirmWindowLayout() : null) // s160: window 1 under the Amiga look only
        : _session is { Game: GameId.P3 } s && _assets?.KeepWindows is { } k
        && _assets.Get(PictureNames.Button("ok")) is not null && _assets.Get(PictureNames.Button("cancel")) is not null
            ? P3WindowFor(prompt.Kind, s.EntryPrompt?.AsksLoad == true,
                s.PendingTreasureFinders.Count > 0 && s.PendingTreasureFinders[0] is not null, k,
                s.DungeonEventPrompt is { } ev && Phantasie3Campaign.YesNoHighlight(ev) is not null)
            : null;

    /// <summary>
    /// III Amiga's window for a prompt <paramref name="kind"/>, or null (the prompt bar): a fight find = window 23
    /// (<c>0x1a044</c>); window 1 (<c>0x1d77e</c>: the text as window 1's title, OK / CANCEL) for the dungeon "Keep …?"
    /// (<c>0x1cb26</c>) and the dungeon entry / exit questions of <c>0x1bb58</c>: `core.p3.load_saved_dungeon`
    /// (<c>0x1bc18</c>, only when <paramref name="entryAsksLoad"/>; the entry's "Will you enter?" is not window 1),
    /// `core.prompt.an_exit_take_it` (<c>0x1bda4</c>) and `core.prompt.save_dungeon_map` (<c>0x1bdc4</c>); and the dungeon
    /// step's "&lt;name&gt; `core.p3.x_hears_monsters_continue`" (<c>0x1c560</c>) and "&lt;name&gt; `core.p3.x_spots_a_trap`"
    /// (<c>0x1c6c8</c>), Core's III Yes / No event questions (<paramref name="yesNoEvent"/>; OK = Yes, CANCEL = No, s134).
    /// </summary>
    public static KeepWindowsLayout.Window? P3WindowFor(SessionPromptKind kind, bool entryAsksLoad, bool fightFind,
        KeepWindowsLayout k, bool yesNoEvent = false) => kind switch
    {
        SessionPromptKind.DungeonEvent when yesNoEvent => k.Dungeon,
        SessionPromptKind.Treasure => fightFind ? k.Fight : k.Dungeon,
        SessionPromptKind.OverworldEntry when entryAsksLoad => k.Dungeon,
        SessionPromptKind.DungeonExitTakeIt or SessionPromptKind.DungeonExitSaveMap => k.Dungeon,
        _ => null,
    };

    /// <summary>
    /// The Amiga's keys for its OK / CANCEL windows (<c>0x3ba0</c>): Return / O = OK, C = CANCEL; the manual's Y / N and
    /// Space answer too (Esc = No goes through <see cref="AnswerPromptWithEscape"/>). There is no highlight to move.
    /// </summary>
    public static int? KeepWindowChoice(Keys key) => key switch
    {
        Keys.Enter or Keys.Space or Keys.O or Keys.Y => 1,
        Keys.C or Keys.N => 2,
        _ => null,
    };

    private void AnswerPrompt(SessionPrompt prompt, int choice)
    {
        var s = _session!;
        if (_confirm.Owns(prompt)) // s160: III's question guards a Client command, not a Core one
        {
            AnswerConfirm(s, choice);
            return;
        }
        var mark = s.LogSequence;
        _defeatCause = new();
        _prompts.Answer(choice);
        if (prompt.Kind == SessionPromptKind.DefeatOutcome)
            _cycleMapAt = null; // s156: back in state 2 from the defeat (h2 +0x328) redraws the map, the same region too
        var notes = s.LogSince(mark).Where(n => !prompt.Lines.Contains(n)).ToList();
        _promptNote = string.Join(" ", _promptNoteLines = notes);
        // s162: an answer's line that III Amiga shows in window 7 ("<name> `core.p3.x_disarms_the_trap`", 0x1c75a) opens it.
        if (s.Mode is GameMode.Dungeon or GameMode.Overworld && notes.FirstOrDefault(n => AmigaMessageBox.Takes(
                _assets?.KeepWindows?.Message, n)) is { } boxed)
            OfferAmigaMessage(boxed);
        AfterSessionAction(s, mark);
        if (prompt.Kind == SessionPromptKind.WildernessInn)
            OfferP1OkLines(s, mark); // s158: I Amiga's `p1field.req.cant_afford` requester
        if (_defeatCause.Count == 0 && !DosPagesUp && CurrentPrompt is { Kind: SessionPromptKind.PartyDefeat } defeat)
            _defeatCause = DefeatReport.Fit(notes, defeat.Lines.Count); // e.g. a failed disarm springs a deadly trap
    }

    /// <summary>
    /// Esc (the game-menu key) on a prompt that takes it as an answer (<see cref="SessionPrompt.EscapeChoice"/>, III's keep
    /// questions: Esc = No): answers it and returns true; otherwise false and Esc keeps its usual meaning. s190 (pad fix
    /// list C11): III Amiga's Bank share window takes Esc / pad B as its CANCEL (C), as the other windows do.
    /// </summary>
    private bool AnswerPromptWithEscape(KeyboardState keys, GamePadState pad)
    {
        if (InventoryScreenOpen || _showInventory || _showBestiary || !_input.Pressed(GameAction.GameMenu, keys, pad))
            return false;
        if (CurrentPrompt is { } shares && AmigaShareWindowFor(shares) is not null)
        {
            _prompts.CancelBankDeposit(); // CANCEL: nothing deposited (s157)
            return true;
        }
        if (CurrentPrompt is not { EscapeChoice: { } esc } prompt)
            return false;
        _promptMenu.Sync(prompt);
        AnswerPrompt(prompt, esc);
        return true;
    }

    private static string FirstLine(SessionPrompt prompt) =>
        prompt.Lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "";

    private static string HelpForPrompt(SessionPrompt prompt) =>
        prompt.IsYesNo && prompt.EscapeChoice == 2 ? UiText.S("game.arrows_enter_y_yes_n_esc_no")
        : prompt.IsYesNo ? UiText.S("game.arrows_enter_or_y_yes_n")
        : prompt.Options.Count == 1 ? UiText.S("game.enter_continue")
        : prompt.Hotkeys is { } letters && !PadWording.Active // s185: the pad answers by D-pad and A (game.arrows_enter_or_1_x)
            ? string.Join("   ", letters.Select((c, i) => $"{c} {prompt.Options[i].Replace(")", "").ToLowerInvariant()}"))
        : prompt.IsShares ? PadWording.Format("game.x_x_shares", prompt.Options[0], prompt.Options[^1])
        : PadWording.F("game.arrows_enter_or_1_x", prompt.Options.Count); // s185: a template: the pad's wording picked here

    private void HandleGameplayInput(KeyboardState keys, GamePadState pad)
    {
        var s = _session!;
        if (_combatUi.IsPresenting)
            return;

        // G enters / leaves the Guild; E)XIT GUILD (manual p.4) leaves it too.
        var guildExit = s.Mode == GameMode.Guild && _input.NewKeys(keys).Contains(Keys.E);
        if ((_input.Pressed(GameAction.OpenGuildDepart, keys, pad) && s.Mode is GameMode.Guild or GameMode.Town) || guildExit)
        {
            // The Guild is a town building (manual p.3): G enters it from the town menu and leaves it back to town.
            if (s.Mode == GameMode.Town)
                s.OpenGuild();
            else
            {
                PlayClick(); // s141: the Guild options' Exit Guild pick (0x4d2 -> 0x56a)
                s.LeaveGuild();
                RefreshTownStock();
            }
            SetFeedback(s.LastMessage, HelpForMode(s.Mode));
        }
        else if (s.Mode == GameMode.Town)
        {
            HandleTownInput(s, keys, pad);
        }
        else if (_input.Pressed(GameAction.Fight, keys, pad) &&
                 s.Mode is GameMode.Overworld or GameMode.Dungeon)
        {
            var mark = s.LogSequence;
            if (s.Mode == GameMode.Dungeon)
                s.BeginDungeonCombat();
            else
                s.BeginWildernessCombat();
            NoteEncounterMonsters(s);
            _fightMark = mark;
            StartFightScreen(s); // s154: I Amiga's monster pictures read first
            SetFeedback(CombatPresenter.FormatHitTooltip(s), HelpForMode(s.Mode));
        }
        else if (s.Mode == GameMode.Overworld)
        {
            ScrollSidebar(keys, pad);
            if ((_input.PressedFieldMove(keys, pad, CurrentGameSettings.DiagonalMoves) ?? _p1MoveNow) is var (dx, dy))
                FieldMove(s, dx, dy);
        }
        else if (s.Mode == GameMode.Dungeon)
        {
            // Top-down map: move in absolute map directions (same keys as the wilderness, manual DUNGEON EXPLORATION).
            // Turning has no default key (D-076: E is east); it stays bindable for a future first-person view.
            ScrollSidebar(keys, pad);
            if (_input.Pressed(GameAction.TurnLeft, keys, pad))
            {
                s.TurnDungeonLeft();
                SetFeedback(UiText.F("game.facing_x", UiText.Name(s.Dungeon!.Facing)), HelpForMode(s.Mode));
            }
            else if (_input.Pressed(GameAction.TurnRight, keys, pad))
            {
                s.TurnDungeonRight();
                SetFeedback(UiText.F("game.facing_x", UiText.Name(s.Dungeon!.Facing)), HelpForMode(s.Mode));
            }
            else if ((_input.PressedFieldMove(keys, pad, CurrentGameSettings.DiagonalMoves) ?? _p1MoveNow) is var (dx, dy))
            {
                FieldMove(s, dx, dy);
            }
        }
    }

    /// <summary>
    /// s172: the sidebar's party cards and II's terrain key scroll by key / pad (<see cref="SidebarScroll"/>) as well as by
    /// the wheel and the bar; a step pressed the same frame still goes.
    /// </summary>
    private void ScrollSidebar(KeyboardState keys, GamePadState pad) =>
        SidebarScroll.Apply(_input.NewKeys(keys), _input.NewButtons(pad), SidebarScroll.RightTriggerHeld(pad),
            UiOverlays.FieldPartyScroll, UiOverlays.TerrainKeyScroll);

    /// <summary>P3 QoL: the Guild treasury's window in place of the Bank's accounts (11-guild-treasury.md section 5).</summary>
    private void OpenTreasury()
    {
        _treasuryDialog.Open();
        SetFeedback(_message, _treasuryDialog.Help);
    }

    /// <summary>The door a town choice goes in by (III's <c>0xacc8</c> callers), or null.</summary>
    private static string? TownDoor(TownChoice choice) => choice switch
    {
        TownChoice.Mystic => "mystic",
        TownChoice.Inn => "inn",
        TownChoice.Guild => "guild",
        _ => null,
    };

    /// <summary>
    /// Shows <paramref name="door"/> open on the street for the pack's <see cref="TownScreenLayout.DoorSeconds"/>, then
    /// runs <paramref name="then"/> (III Amiga, s102). False when the pack has no such door: the caller goes straight in.
    /// </summary>
    private bool StartDoorFlash(string? door, Action? then)
    {
        var layout = _assets?.TownScreen;
        if (door is null || layout?.Doors is not { } doors || !doors.ContainsKey(door) || _assets?.TownDoors is null
            || layout.DoorSeconds <= 0)
            return false;
        _doorFlash = (door, _updateSeconds + layout.DoorSeconds, then);
        return true;
    }

    private void HandleTownInput(GameSession s, KeyboardState keys, GamePadState pad)
    {
        if (_doorFlash is { } flash) // keys wait while the door shows, as the Amiga's 15-tick wait does
        {
            if (_updateSeconds < flash.Until)
                return;
            _doorFlash = null;
            flash.Then?.Invoke();
            return;
        }
        // "WHO IS SHOPPING?" / A)NOTHER SHOPPER: the digit picks the member who buys, trains, withdraws, learns.
        foreach (var key in _input.NewKeys(keys))
        {
            var d = key switch
            {
                >= Keys.D1 and <= Keys.D9 => key - Keys.D0,
                >= Keys.NumPad1 and <= Keys.NumPad9 => key - Keys.NumPad0,
                _ => 0,
            };
            if (d >= 1 && d <= s.Party.Members.Count)
            {
                _members.Row = d - 1;
                SetFeedback(UiText.F("game.x_is_shopping", s.Party.Members[_members.Row].Name), HelpForMode(s.Mode));
                _messageBand.Restart();
            }
        }
        // "Pad coverage" (s182): the pad's left / right trigger step the shopper back / on (TownMenu.ShopperStep).
        foreach (var button in _input.NewButtons(pad))
        {
            if (TownMenu.ShopperStep(button) is var step and not 0 && s.Party.Members.Count > 0)
            {
                _members.Row = TownMenu.StepShopper(_members.Row, s.Party.Members.Count, step);
                SetFeedback(UiText.F("game.x_is_shopping", s.Party.Members[_members.Row].Name), HelpForMode(s.Mode));
                _messageBand.Restart();
            }
        }
        if (s.Party.Members.Count > 0)
            _members.Clamp(s.Party.Members.Count);
        var shopper = s.Party.Members.Count > 0 ? s.Party.Members[_members.Row] : null;

        if (_townMenu.Place == TownPlace.Armory)
        {
            HandleArmoryInput(s, keys, pad, shopper);
            return;
        }

        // D3-074: where the rules have Trade (III), T is the manual's Trade Items, not the remaster's leave-town key.
        if (_townMenu.Place == TownPlace.Menu && _input.Pressed(GameAction.ToggleTown, keys, pad)
            && !(s.CanTrade && _input.NewKeys(keys).Contains(Keys.T)))
        {
            LeaveTown(s);
            SetFeedback(s.LastMessage, HelpForMode(s.Mode));
            return;
        }

        _townMenu.DosBank = s.BankIsDos; // III: the save's p3.bank / p3.trade rules shape the Bank and Option lists
        _townMenu.Trade = s.CanTrade;
        // "Pad coverage" (s182): the pad's D-pad / left stick, A and B as the row's arrows, Return and Esc (B never gets
        // here: the global GameMenu backs out first; LB / RB, Save / Load, come as PgUp / PgDn, which the menu ignores).
        foreach (var key in _input.MenuKeys(keys, pad))
        {
            var before = _townMenu.Place;
            var choice = _townMenu.HandleKey(key);
            if (choice is { } picked)
            {
                if (StartDoorFlash(TownDoor(picked), () => RunTownChoice(s, picked, shopper)))
                    return;
                RunTownChoice(s, picked, shopper);
                return;
            }
            if (_townMenu.Place != before)
            {
                StartDoorFlash(_townMenu.Place switch { TownPlace.Bank => "bank", TownPlace.Armory => "armory", _ => null }, null);
                // P3 QoL (11-guild-treasury.md section 5): under the Guild treasury the Bank is the treasury's window.
                if (_townMenu.Place == TownPlace.Bank && s.TreasuryOn)
                {
                    _townMenu.Back();
                    OpenTreasury();
                    return;
                }
                // III Amiga (s117): the door opens one window, the Bank's own, instead of the Bank list.
                if (_townMenu.Place == TownPlace.Bank && s.Game == GameId.P3 && CurrentGameSettings.OriginalScreens
                    && _assets?.KeepWindows?.Bank is not null)
                {
                    _townMenu.Back();
                    _amigaBank.Open();
                    SetFeedback(_message, _amigaBank.Help);
                    return;
                }
                SetFeedback(_townMenu.Place == TownPlace.Armory ? UiText.S("game.the_armory") : _message, HelpForMode(s.Mode));
                return; // one menu step per frame, so Enter can't also act inside the place it opened
            }
        }

    }

    /// <summary>Inside the Armory: the stock list, Enter buys for the shopper, T / Backspace back to the town bar.</summary>
    private void HandleArmoryInput(GameSession s, KeyboardState keys, GamePadState pad, Character? shopper)
    {
        if (_input.Pressed(GameAction.ToggleTown, keys, pad) || _input.NewKeys(keys).Contains(Keys.Back))
        {
            P1ArmoryTone(); // s165: the stock list's EXIT (h26 +0x8f6)
            _townMenu.LeaveArmory();
            SetFeedback(s.CurrentTown?.Name ?? "", HelpForMode(s.Mode));
            _messageBand.Restart();
            return;
        }

        // "Pad coverage" (s182): the D-pad and the left stick (PressedMove, s190) and A (Confirm) work here, B leaves (BackOut).
        if (_input.PressedMove(CardinalDirection.North, keys, pad))
            _townCursor = Math.Max(0, _townCursor - 1);
        if (_input.PressedMove(CardinalDirection.South, keys, pad))
            _townCursor = Math.Min(Math.Max(0, _townStock.Count - 1), _townCursor + 1);

        if (shopper is not null && _input.Pressed(GameAction.Confirm, keys, pad) && _townStock.Count > 0)
        {
            PlayClick(); // s141: II ST's Armory list clicks on a pick (0x1f8a), before any alert
            P1ArmoryTone(); // s165: I Amiga's tone after the row's gadget (h26 +0x8f6), before the buy's alerts
            var item = _townStock[Math.Clamp(_townCursor, 0, _townStock.Count - 1)];
            var result = s.Buy(shopper, item.Id);
            RefreshTownStock();
            SetFeedback(result.Message, HelpForMode(s.Mode));
        }
    }

    /// <param name="confirmed">s160: III's question was answered Yes (<see cref="AnswerConfirm"/>): run, don't ask again.</param>
    private void RunTownChoice(GameSession s, TownChoice choice, Character? shopper, bool confirmed = false)
    {
        if (!confirmed)
            PlayClick(); // s140: II ST clicks on a menu-bar pick (0x294) before it runs
        string? refused = null;
        switch (choice)
        {
            case TownChoice.Withdraw or TownChoice.Deposit when s.TreasuryOn: // no accounts under the Guild treasury
                OpenTreasury();
                break;
            case TownChoice.Exit:
                LeaveTown(s);
                break;
            case TownChoice.Guild:
                s.OpenGuild();
                break;
            case TownChoice.Mystic:
                s.ConsultMystic();
                break;
            case TownChoice.Inn:
                s.RestAtInn();
                _townMenu.OpenInn(); // menu 0x0d: Exit Inn / Distribute / Printout
                break;
            case TownChoice.Withdraw when s.BankIsDos: // III's DOS bank: `p3town.bank_withdraw_all`
                s.DosBankWithdrawAll();
                refused = s.LastMessage;
                break;
            case TownChoice.Deposit when s.BankIsDos: // `p3town.bank_deposit_some`
                if (s.Party.GoldCarried <= 0)
                    refused = UiText.S("game.no_gold_carried");
                else
                    _dosDepositDialog.Open(s);
                break;
            case TownChoice.Trade:
                _tradeDialog.Data = _data;
                refused = _tradeDialog.Open(s);
                break;
            case TownChoice.Withdraw:
                if (shopper is null)
                    refused = UiText.S("game.no_party");
                else
                    _withdrawDialog.Open(); // `towndlg.who_will_withdraw`
                break;
            case TownChoice.Deposit:
                // D-049 (manual): all carried gold, split by shares — asked member by member as a prompt.
                if (!_prompts.BeginBankDeposit())
                    refused = UiText.S("game.no_gold_carried");
                break;
            case TownChoice.CastSpell:
                refused = _castDialog.Open(s);
                break;
            case TownChoice.UseItem:
                refused = _useItemDialog.Open(s);
                break;
            case TownChoice.ShowParty:
                refused = _inspectDialog.Open(s);
                NoteP1ShowParty(s); // s165: its close plays the list tone (h26 +0x33e)
                break;
            // s160: III asks `p3bar.confirm_distribute` after the empty-party refusal (root 0x83a0…0x83c1),
            // before any share moves, on every route (D, the Option / Inn list, the bar); the town-entry hand-out never asks.
            case TownChoice.Distribute when !confirmed && s.Party.Members.Count > 0 && s.ItemHandOut && AskConfirm(s, "distribute"):
                return;
            case TownChoice.Distribute:
                refused = Distribute(s);
                break;
            // s158 / s160: III's S, Option › Save game and the bar's File › Save ask S390 first (0x844c / Amiga 0xba9a).
            case TownChoice.SaveGame when !confirmed && AskConfirm(s, "save"):
                return;
            case TownChoice.SaveGame:
                SaveGame();
                _messageBand.Restart();
                return;
            case TownChoice.ToggleAutoSave:
                _townMenu.AutoSave = _settings.AutoSave = !_settings.AutoSave;
                _settings.Save();
                refused = _settings.AutoSave ? UiText.S("game.auto_save_on") : UiText.S("game.auto_save_off");
                break;
            case TownChoice.ToggleSound:
                _townMenu.Sound = _settings.Sound = !_settings.Sound;
                _settings.Save();
                refused = _settings.Sound ? UiText.S("game.sound_on") : UiText.S("game.sound_off");
                break;
            case TownChoice.Printout when AskP1Printer(s):
                return; // s163: I Amiga's "PREPARE PRINTER" first (h29 +0x10ba)
            case TownChoice.Printout:
                refused = Printout(s);
                break;
        }

        if (OpenModal is { } opened)
            SetFeedback(refused ?? _message, opened.Help);
        else
            SetFeedback(refused ?? s.LastMessage, HelpForMode(s.Mode));
        if (refused is not null || OpenModal is null)
        {
            _messageBand.Restart(); // the same text twice in a row (a second refusal) shows again
            OfferAmigaMessage(refused ?? (choice == TownChoice.Inn ? s.LastMessage : null));
        }
    }

    /// <summary>
    /// Leaving town; with Auto save on, the game is saved first (OV1 <c>0x483e</c>, "LEAVING %s"). II's
    /// <c>p2.autosave</c> = <c>always</c> (D2-016, the Apple text) saves whatever the toggle says.
    /// </summary>
    private void LeaveTown(GameSession s)
    {
        if (_settings.AutoSave || s.Options.AutoSaveAlways)
            AutoSaveGame(); // QoL SAVE SLOTS: the AUTO slot, never the picker
        var town = s.CurrentTown?.Name;
        if (s.LeaveTown() && town is not null && P1DosLeavingLine(s, town) is { } leaving)
            s.Note(leaving); // s154: I DOS's row 24 after the town loop (OV1 0x483e), the line the overworld opens on
    }

    /// <summary>
    /// s163: I Amiga's town Printout (h29 <c>+0x10ba</c>) asks `p1field.req.prepare_printer` first (h29 <c>+0x1f6</c>, its
    /// beep before it): O.K. prints (a failed write asks again), ABORT prints nothing. False: no requester (the DOS path,
    /// another game, an empty party), the Printout runs at once.
    /// </summary>
    private bool AskP1Printer(GameSession s)
    {
        if (s.Party.Members.Count == 0 || !P1TownRequesterReady("prepare_printer"))
            return false;
        if (P1MapCues && _assets!.Pack!.CueSounds!.Printout is int beep)
            PlayP1CueInTurn(beep);
        OpenP1TownHeld("prepare_printer", answer =>
        {
            if (answer != 0 || _session is not { } now)
                return;
            try
            {
                SetFeedback(UiText.F("game.printed_to_x", PartyPrintout.WriteFile(now.Party, _data!)), HelpForMode(now.Mode));
                _messageBand.Restart();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetFeedback(UiText.F("game.printer_not_ready_x", ex.Message), HelpForMode(now.Mode));
                AskP1Printer(now); // h29 +0x200: asked again until the printer opens
            }
        });
        return true;
    }

    /// <summary>Printout (OV1 <c>0x46dd</c>): the party list goes to a text file, the remaster's printer.</summary>
    private string Printout(GameSession s)
    {
        if (s.Party.Members.Count == 0)
            return UiText.S("game.no_party");
        // s162: I Amiga's `p1field.req.prepare_printer` beeps first (h29 +0x1f6 -> h26 +0x6e); here without its requester
        // (a pack without map_requesters.town.prepare_printer).
        if (P1MapCues && _assets!.Pack!.CueSounds!.Printout is int beep)
            PlayP1CueInTurn(beep);
        try
        {
            return UiText.F("game.printed_to_x", PartyPrintout.WriteFile(s.Party, _data!));
        }
        catch (Exception ex)
        {
            return UiText.F("game.printer_not_ready_x", ex.Message);
        }
    }

    /// <summary>
    /// Manual p.12 D)ISTRIBUTE AND SELL ITEMS: Core's XP / gold split first (D-037), then the DOS item hand-out screen over
    /// every carried and found item (OV1 <c>0x2ab3</c>, <see cref="DistributeDialog"/>). Returns a refusal or null.
    /// </summary>
    private string? Distribute(GameSession s)
    {
        // An imported save can arrive with sack items but nobody in the party (OV1 0x7e7); DOS refuses (0xad5d).
        if (s.Party.Members.Count == 0)
            return UiText.S("game.no_party");
        var split = s.Sack.Experience > 0 || s.Sack.Gold > 0;
        if (split)
            s.DistributeLoot(items: false);
        // QoL modern-inventory: no item pooling; gear stays put and the inventory screen (V) moves and sells items. After a
        // split its own line shows (null); else the note says where the items went.
        return s.ItemHandOut ? _distributeDialog.Open(s) : split ? null : PadWording.S("game.distribute_modern"); // s193 (E30): X
    }

    private enum StepOutcome { Plain, Blocked, Stopped }

    /// <summary>
    /// A move on the wilderness or dungeon map: one step, or a numpad diagonal (QoL, <see cref="GameSettings.DiagonalMoves"/>)
    /// as two ordinary steps. The second runs only after a plain move; if the first is blocked the other order is tried.
    /// </summary>
    private void FieldMove(GameSession s, int dx, int dy)
    {
        if (!StepAfterTone(() => FieldMoveNow(s, dx, dy))) // s154: I Amiga's step tone first, the move after its hold
            FieldMoveNow(s, dx, dy);
    }

    private void FieldMoveNow(GameSession s, int dx, int dy)
    {
        var steps = InputMap.StepsFor(dx, dy);
        var first = FieldStep(s, steps[0]);
        if (steps.Count == 1)
            return;
        if (first == StepOutcome.Plain)
            FieldStep(s, steps[1]);
        else if (first == StepOutcome.Blocked && FieldStep(s, steps[1]) == StepOutcome.Plain)
            FieldStep(s, steps[0]);
    }

    private StepOutcome FieldStep(GameSession s, CardinalDirection dir)
    {
        var mode = s.Mode;
        bool plain, blocked;
        if (mode == GameMode.Overworld)
        {
            var kind = AfterOverworldMove(s, dir).Kind;
            (plain, blocked) = (kind == OverworldMoveKind.Moved, kind == OverworldMoveKind.Blocked);
        }
        else
        {
            DungeonMoveResult? result = null;
            AfterDungeonMove(s, () => result = s.MoveDungeon(dir));
            (plain, blocked) = (result!.Kind == DungeonMoveKind.Moved, result.Kind == DungeonMoveKind.BlockedWall);
        }
        if (blocked && s.Mode == mode && CurrentPrompt is null && !P1HeldRequesterUp)
            return StepOutcome.Blocked;
        return plain && s.Mode == mode && CurrentPrompt is null ? StepOutcome.Plain : StepOutcome.Stopped;
    }

    private OverworldMoveResult AfterOverworldMove(GameSession s, CardinalDirection dir)
    {
        var mark = s.LogSequence;
        _rowLineBeforeStep = _message;
        var result = s.MoveOverworld(dir);
        _prompts.NoteOverworldMove(result); // tile 41 -> "AN INN. ENTER?"
        // A location sign is 1-3 lines (OV2 0x5fae); show them all, not just the last logged line.
        SetFeedback(result.Kind == OverworldMoveKind.LocationText && result.LocationText is { } sign ? sign : s.LastMessage,
            HelpForMode(s.Mode));
        KeepStepTextUnderPrompt(s, mark);
        AfterSessionAction(s, mark);
        OfferP1Ok(s, mark, result); // s154: STYX / OCEAN as I Amiga's OK requester
        return result;
    }

    /// <summary>
    /// A step can stop on a prompt (exit, event menu: <see cref="SessionPrompts"/> shows it next frame) or start a fight.
    /// </summary>
    private void AfterDungeonMove(GameSession s, Action move)
    {
        var mark = s.LogSequence;
        _rowLineBeforeStep = _message;
        move();
        SetFeedback(s.LastMessage, HelpForMode(s.Mode));
        OfferAmigaMessage(s.LastMessage); // s162: III Amiga's "<name> picks the lock." is window 7 (0x1c674)
        KeepStepTextUnderPrompt(s, mark);
        AfterSessionAction(s, mark);
        MarkDungeonVisited(); // s141: after the boxes are planned, so a cell Core has left already is the one revealed
    }

    /// <summary>
    /// A step that opens a prompt keeps its own text on the message line under it: a trap or a swim that kills the last
    /// member opens `core.defeat.your_party_has_been_defeated`, and without this the gas / damage / DIES lines were never seen.
    /// </summary>
    private void KeepStepTextUnderPrompt(GameSession s, long mark)
    {
        if (CurrentPrompt is not { } prompt)
            return;
        var notes = s.LogSince(mark).Select(n => n.Trim()).Where(n => n.Length > 0 && !prompt.Lines.Any(l => l.Trim() == n)).ToList();
        // The message line holds one line; the defeat box shows the report (trap, swim) above the banner.
        _defeatCause = prompt.Kind == SessionPromptKind.PartyDefeat ? DefeatReport.Fit(notes, prompt.Lines.Count) : new();
        if (notes.Count > 0 && _defeatCause.Count == 0)
            _promptNote = string.Join(" ", _promptNoteLines = notes);
    }

    /// <summary>The prompt as drawn: the defeat banner gets the report of the step that caused it on top.</summary>
    private SessionPrompt Shown(SessionPrompt prompt) =>
        prompt.Kind == SessionPromptKind.PartyDefeat && _defeatCause.Count > 0
            ? prompt with { Lines = [.. _defeatCause, .. prompt.Lines] }
            : prompt;

    /// <summary>
    /// After a step or an answer: a town needs its stock, a fight needs the presenter. <paramref name="mark"/> is the
    /// log position before the step, so a fight that wipes the party can show how it began.
    /// </summary>
    private void AfterSessionAction(GameSession s, long mark)
    {
        if (s.Mode == GameMode.Town)
            RefreshTownStock();
        if (PlanMapMessages(s, mark))
            return; // s139: I Amiga's encounter boxes over the map first; the fight's screen follows them
        var planned = PlanDosPages(s, mark);
        _rowLineBeforeStep = null; // only a map step keeps the line from before it
        if (planned)
            return; // s154: I DOS's boxes, one key each; the fight's screen follows them
        if (s.Mode == GameMode.Combat && !_combatUi.IsPresenting && !_p1FightLoading)
        {
            NoteEncounterMonsters(s);
            _fightMark = mark;
            StartFightScreen(s); // s154: I Amiga's monster pictures read first
        }
    }

    /// <summary>
    /// A fight just ended. The round that wipes the party closes the combat screen and opens the defeat box in the same
    /// frame (at any time lag, and at once with fast-forward held), so the box carries the fight's report (encounter,
    /// last blows, DIES) instead of a bare banner.
    /// </summary>
    private void AfterFightEnded(GameSession s)
    {
        // s163: II ST's round has played line by line on the combat screen (P2CombatLineRun), and the ST's defeat scene
        // opens on `core.defeat.your_party_has_been_defeated` (0x12a16): no report pages in front of it, which put the ghost row and
        // sound 23 one key per line later (seen live on Xvfb).
        if (_combatUi.P2Lines?.Invoke() is not null)
            return;
        if (_defeatCause.Count == 0 && CurrentPrompt is { Kind: SessionPromptKind.PartyDefeat } prompt)
            _defeatCause = DefeatReport.Fit(
                s.LogSince(_fightMark).Where(n => !prompt.Lines.Any(l => l.Trim() == n.Trim())), prompt.Lines.Count);
        // s165: I Amiga asks a fight's finds on the fight field (h44 +0x942 / +0x956, before h53 +0x2a4 leaves it): the field
        // stays as state 12's set-up left it while KEEP IT? waits (UpdateP1AfterFight drops it).
        if (s.Game == GameId.P1 && CurrentGameSettings.OriginalScreens && _combatUi.P1Layout is not null
            && s.PendingTreasure.Count > 0 && s.Mode is GameMode.Overworld or GameMode.Dungeon)
            _combatUi.P1AfterFight = new P1CombatFieldSnapshot(
                [.. s.Party.Members.Where(m => m.Status != CharacterStatus.Dead)], []);
    }

    /// <summary>s165: the after-fight field (<see cref="CombatPresenter.P1AfterFight"/>) goes once no find waits or the map is left.</summary>
    private void UpdateP1AfterFight()
    {
        if (_combatUi.P1AfterFight is not null
            && (_session is not { } s || s.PendingTreasure.Count == 0 || s.Mode is not (GameMode.Overworld or GameMode.Dungeon)))
            _combatUi.P1AfterFight = null;
    }

    private void TrackExplorationAndEncounters()
    {
        var s = _session!;
        if (s.Mode == GameMode.Dungeon && s.Dungeon is not null)
            MarkDungeonVisited();

        if (s.Combat is null)
        {
            _trackedCombatHash = null;
            return;
        }

        var hash = s.Combat.GetHashCode();
        if (_trackedCombatHash != hash)
        {
            _trackedCombatHash = hash;
            NoteEncounterMonsters(s);
        }
    }

    /// <summary>
    /// The cells a step draws (OV2 <c>0x20e5</c>): the 3×3 box, or only the party's cell on <c>0x01</c> / lock bank 1.
    /// The party's cell also joins the stood-on set (the mouse walk's <c>0x01</c> rule, <see cref="DungeonWalkPath"/>).
    /// </summary>
    private void MarkDungeonVisited()
    {
        var d = _session?.Dungeon;
        if (d?.Level is not { } level) return;
        var (px, py) = MapPartyAt(d) ?? (d.X, d.Y); // s141: the cell under the boxes until Core's move shows
        foreach (var (x, y) in DungeonSight.StepReveal(level, px, py))
            _qol.MarkVisited(d.LevelId, x, y);
        _qol.MarkStoodOn(d.LevelId, px, py);
    }

    /// <summary>
    /// L)OOK (manual DUNGEON EXPLORATION; DOS Options "Look", OV2 <c>0x1f9f</c>): draw the larger area. Free, no text;
    /// on <c>0x01</c> and door cells DOS silently does nothing.
    /// </summary>
    private void DungeonLook()
    {
        var d = _session!.Dungeon!;
        foreach (var (x, y) in DungeonSight.Look(d.Level!, d.X, d.Y) ?? [])
            _qol.MarkVisited(d.LevelId, x, y);
    }

    private void NoteEncounterMonsters(GameSession s)
    {
        if (s.Combat is null) return;
        foreach (var m in s.Combat.AllMonsters)
            _qol.NoteMonster(m.Id);
        _qol.Save();
    }

    /// <summary>
    /// A save the player asked for (F5, the game menu, a town's Save game). QoL SAVE SLOTS: the slot picker; off, the one
    /// save.
    /// </summary>
    private void SaveGame()
    {
        if (CurrentGameSettings.SaveSlots)
        {
            OpenSaveSlots(_session!.Game, SaveSlotMode.Save);
            return;
        }
        WriteSave(ClientSettings.SavePathFor(_session!.Game), null);
    }

    /// <summary>The autosave on leaving town: always the game's one save (with SAVE SLOTS, its AUTO slot).</summary>
    private void AutoSaveGame() => WriteSave(ClientSettings.SavePathFor(_session!.Game), null);

    /// <summary>Writes the session to <paramref name="path"/> (<paramref name="slot"/>: the slot it is, for the message).</summary>
    private void WriteSave(string path, SaveSlot? slot)
    {
        try
        {
            var snap = _session!.CaptureSnapshot();
            Directory.CreateDirectory(ClientSettings.DataDirectory);
            SessionCodec.WriteJsonFile(path, snap);
            _qol.Save();
            _settings.Save();
            SetFeedback(slot is null ? UiText.S("game.saved") : UiText.F("saveslots.saved_x", SaveSlotScreen.Label(slot)),
                HelpForMode(_session.Mode));
        }
        catch (Exception ex)
        {
            _session!.NoteSaveFailed(); // III: sound 12, the Amiga's Disk Error box (0x20258)
            SetFeedback(UiText.F("game.save_failed_x", ex.Message), HelpForMode(_session!.Mode));
        }
    }

    /// <summary>F6 (town) / F7 open the DOS save dialog (P1.4). Not while a fight, a prompt or an overlay owns the keys.</summary>
    private bool OpenDosDialog(KeyboardState keys, GamePadState pad)
    {
        if (_combatUi.IsPresenting || CurrentPrompt is not null || _showInventory || _showBestiary)
            return false;
        if (_session!.Game == GameId.P2 && _input.Pressed(GameAction.DosImport, keys, pad))
        {
            _dosDialog.OpenTransfer(ClientSettings.ReadSavePathFor(GameId.P1), GameId.P1); // T5: II's utility, I → II
            return true;
        }
        if (_session!.Game != GameId.P1) // DOS saves are Phantasie I's format
            return false;
        if (_input.Pressed(GameAction.DosExport, keys, pad))
        {
            if (_dosDialog.OpenExport(_session!) is { } refused)
                SetFeedback(refused, HelpForMode(_session!.Mode));
        }
        else if (_input.Pressed(GameAction.DosImport, keys, pad))
            _dosDialog.OpenImport();
        return _dosDialog.IsOpen;
    }

    /// <summary>The open key-driven modal (cast, inspect, use potion, health), or null.</summary>
    /// <summary>
    /// s141: whether this frame's keys are a command for II ST's field loop <c>0x16fea</c>, which clicks (<c>0x1719a</c>)
    /// on every key it takes, a step or a wall alike, and on an Options menu pick (live: arrows, unused letters, Cast spell);
    /// its Speed menu does not click. Overworld / dungeon only, with nothing else owning the keys. Esc, Tab, the modifiers,
    /// the function keys and the sidebar's scroll keys (s172, <see cref="SidebarScroll"/>) are the remaster's own.
    /// </summary>
    private bool FieldCommandKeys(GameSession s, KeyboardState keys, GamePadState pad) =>
        s.Mode is GameMode.Overworld or GameMode.Dungeon
        && _assets?.Pack?.FieldSounds?.Click is not null
        && OpenModal is null && CurrentPrompt is null && !_combatUi.IsPresenting && !_showInventory && !_showBestiary
        && !_dosDialog.IsOpen
        && (_input.NewKeys(keys).Any(k => k is not (Keys.Escape or Keys.Tab or (>= Keys.F1 and <= Keys.F24)
                or Keys.LeftShift or Keys.RightShift or Keys.LeftControl or Keys.RightControl or Keys.LeftAlt or Keys.RightAlt)
                && !SidebarScroll.IsScrollKey(k))
            || FieldPadClicks(_input.NewButtons(pad), SidebarScroll.RightTriggerHeld(pad)));

    /// <summary>
    /// s192 (pad fix C23): the pad's buttons that stand for keys II ST's field loop takes, so they click as the keys do: a
    /// step (D-pad / left stick) and A (Fight; with RT held A opens the remaster's command list instead, which does not).
    /// </summary>
    public static bool FieldPadClicks(IEnumerable<Buttons> buttons, bool rightTriggerHeld) =>
        buttons.Any(b => InputMap.MenuPadKey(b) is Keys.Up or Keys.Down or Keys.Left or Keys.Right
            || b == Buttons.A && !rightTriggerHeld);

    /// <summary>
    /// s141: whether closing <paramref name="modal"/> in <paramref name="mode"/> stands for II ST's click as a town list or
    /// dialog returns (any exit, a pick or EXIT: the 0x1964a / form_do sites; pick_member only on its cancel 0x674, which a
    /// member pick that goes on to the next step does not close). Town and Guild only: the field's pickers do not click
    /// (live s141). Not the transport picker (its own click, 0x25b4) nor the remaster's menus. The Guild's "who will learn /
    /// be trained" pick (<see cref="GuildPartyPickDialog"/>, the ST's pick_member, guild_train 0xcdf4) closes on a member pick,
    /// which is silent on the ST: it clicks only when backed out of (<paramref name="keys"/> hold Esc or Backspace, 0x674).
    /// </summary>
    private bool ClicksOnClose(IModalDialog modal, GameMode mode, IReadOnlyCollection<Keys> keys) =>
        mode is GameMode.Town or GameMode.Guild
        && !ReferenceEquals(modal, _gameMenu) && !ReferenceEquals(modal, _continueDialog) && !ReferenceEquals(modal, _padCommands)
        && !ReferenceEquals(modal, _transportDialog) && !ReferenceEquals(modal, _timeLagDialog)
        && (!ReferenceEquals(modal, _guildWhoDialog) || keys.Any(k => k is Keys.Escape or Keys.Back));

    private IModalDialog? OpenModal =>
        _gameMenu.IsOpen ? _gameMenu
        : _continueDialog.IsOpen ? _continueDialog
        : _castDialog.IsOpen ? _castDialog
        : _inspectDialog.IsOpen ? _inspectDialog
        : _potionDialog.IsOpen ? _potionDialog
        : _healthDialog.IsOpen ? _healthDialog
        : _timeLagDialog.IsOpen ? _timeLagDialog
        : _withdrawDialog.IsOpen ? _withdrawDialog
        : _dosDepositDialog.IsOpen ? _dosDepositDialog
        : _tradeDialog.IsOpen ? _tradeDialog
        : _amigaBank.IsOpen ? _amigaBank
        : _treasuryDialog.IsOpen ? _treasuryDialog
        : _learnDialog.IsOpen ? _learnDialog
        : _transportDialog.IsOpen ? _transportDialog
        : _guildAddDialog.IsOpen ? _guildAddDialog
        : _guildDropDialog.IsOpen ? _guildDropDialog
        : _guildNewDialog.IsOpen ? _guildNewDialog
        : _guildPickDialog.IsOpen ? _guildPickDialog
        : _guildListDialog.IsOpen ? _guildListDialog
        : _guildWhoDialog.IsOpen ? _guildWhoDialog
        : _useItemDialog.IsOpen ? _useItemDialog
        : _distributeDialog.IsOpen ? _distributeDialog
        : _padCommands.IsOpen ? _padCommands // s182: the pad's command list (PadCommandDialog)
        : null;

    /// <summary>
    /// The wilderness / dungeon commands (manual WILDERNESS TRAVEL, D-076; DOS menu <c>0x14</c>) C cast, H health, I inspect,
    /// U use potion, T time lag, L look (dungeon), and the town Option's Cast spell (C). Not while a fight, a prompt or an
    /// overlay owns the keys. Returns the dialog it opened, or null.
    /// </summary>
    private IModalDialog? OpenModalDialog(KeyboardState keys, GamePadState pad)
    {
        if (_combatUi.IsPresenting || CurrentPrompt is not null || _showInventory || _showBestiary || _dosDialog.IsOpen)
            return null;
        var s = _session!;
        if (_transportDialog.OpenIfPending(s)) // TRANSPORTATION cast: Core waits for the destination
        {
            SetFeedback(_message, _transportDialog.Help);
            return null;
        }
        if (s.TakeTownItemHandOut() && _distributeDialog.Open(s, foundOnly: true) is null) // D3-071: III's town entry hands found items out
        {
            SetFeedback(_message, _distributeDialog.Help);
            return null; // opens next frame, so the key that entered the town gives nothing
        }
        if (OpenPadCommands(s, pad)) // s182: the pad's list of the Guild's / the field's letter commands
            return _padCommands;
        if (s.Mode is GameMode.Guild or GameMode.Title) // s187 (pad fix A6): the member list scrolls by [ ] / right stick too
            SidebarScroll.Apply(_input.NewKeys(keys), _input.NewButtons(pad), rightTriggerHeld: false,
                UiOverlays.GuildListScroll, UiOverlays.GuildListScroll);
        string? refused = null;
        var shopper = s.Party.Members.Count > 0 ? s.Party.Members[_members.Clamp(s.Party.Members.Count)] : null;
        if (s.Mode == GameMode.Town && _townMenu.Place == TownPlace.Bank && s.TreasuryOn
                 && _input.Pressed(GameAction.BankWithdraw, keys, pad))
            OpenTreasury();
        else if (s.Mode == GameMode.Town && _townMenu.Place == TownPlace.Bank && shopper is not null
                 && !s.BankIsDos && _input.Pressed(GameAction.BankWithdraw, keys, pad))
            _withdrawDialog.Open();
        else if (s.Mode == GameMode.Guild && _input.NewKeys(keys).Contains(Keys.S)) // manual p.4 S)PELLS
            refused = _guildWhoDialog.Open(s, GuildPartyPickDialog.LearnTitle, (ss, m) => _learnDialog.Open(ss, m) ?? "");
        else if (s.Mode == GameMode.Guild && _input.NewKeys(keys).Contains(Keys.T)) // manual p.4 T)RAINING
            refused = _guildWhoDialog.OpenTraining(s);
        else if (s.Mode is GameMode.Guild or GameMode.Title && _input.Pressed(GameAction.GuildAdd, keys, pad))
            refused = _guildAddDialog.Open(s);
        else if (s.Mode is GameMode.Guild or GameMode.Title && _input.Pressed(GameAction.GuildDrop, keys, pad))
            refused = _guildDropDialog.Open(s);
        else if (s.Mode == GameMode.Guild && _input.Pressed(GameAction.GuildNew, keys, pad))
            refused = _guildNewDialog.Open(s);
        else if (s.Mode == GameMode.Guild && _input.Pressed(GameAction.GuildPurge, keys, pad))
            refused = _guildPickDialog.OpenPurge(s);
        else if (s.Mode == GameMode.Guild && _input.Pressed(GameAction.GuildRename, keys, pad))
            refused = _guildPickDialog.OpenRename(s);
        else if (s.Mode == GameMode.Guild && _input.Pressed(GameAction.GuildList, keys, pad))
            refused = _guildListDialog.Open(s);
        // s160: III's Guild "Inspect" (DOS guild_menu item 6, Amiga Guild › Examine): any roster member's sheet, every layout
        else if (s.Mode == GameMode.Guild && GuildPickDialog.ExaminesRoster(s.Game) && _input.Pressed(GameAction.GuildExamine, keys, pad))
            refused = _guildPickDialog.OpenExamine(s, member => _inspectDialog.OpenSheet(member));
        // s156: on I Amiga's map the Options picks get the step tone first (MapOptionAfterTone); the pick runs after it.
        else if (_input.Pressed(GameAction.CastSpell, keys, pad))
        {
            if (MapOptionAfterTone(s, () => _castDialog.Open(s)))
                return null;
            refused = _castDialog.Open(s);
        }
        else if (_input.Pressed(GameAction.Inspect, keys, pad) && s.Mode is GameMode.Overworld or GameMode.Dungeon)
        {
            if (MapOptionAfterTone(s, () => _inspectDialog.Open(s)))
                return null;
            refused = _inspectDialog.Open(s);
        }
        else if (_input.Pressed(GameAction.UsePotion, keys, pad) && s.Mode is GameMode.Overworld or GameMode.Dungeon)
        {
            if (MapOptionAfterTone(s, () => _potionDialog.Open(s)))
                return null;
            refused = _potionDialog.Open(s);
        }
        else if (_input.Pressed(GameAction.Health, keys, pad) && s.Mode is GameMode.Overworld or GameMode.Dungeon)
        {
            if (MapOptionAfterTone(s, () => { _healthDialog.Open(); return null; }))
                return null;
            _healthDialog.Open();
        }
        // Manual: T is a wilderness command, and the dungeon's command line is the same plus L)OOK. With the D-073 QoL
        // switch on, the time lag lives in the Options tab and T is no command.
        else if (_input.Pressed(GameAction.TimeLag, keys, pad) && TimeLagDialog.IsCommand(s.Mode, _settings.Game(s.Game)))
            _timeLagDialog.Open();
        else if (_input.Pressed(GameAction.Look, keys, pad) && s.Mode == GameMode.Dungeon)
        {
            if (MapOptionAfterTone(s, () => { DungeonLook(); return null; }))
                return null;
            DungeonLook();
        }
        else
            return null;
        if (s.Mode is GameMode.Guild or GameMode.Town)
            PlayClick(); // s141: a Guild options pick (0x4d2 -> 0x56a) or the Town options' Cast spell (0x294), first
        if (refused is not null)
        {
            SetFeedback(refused, HelpForMode(s.Mode));
            OfferAmigaMessage(refused); // s158: III's dispatch refuses in window 7 (0xaaac) whichever key asked
        }
        if (OpenModal is { } opened)
            SetFeedback(_message, opened.Help);
        return null; // the keys that opened it are not read as its first answer
    }

    /// <summary>Reset the shell's per-game state after a JSON load or a DOS import replaced the session state.</summary>
    private void AfterSnapshotApplied()
    {
        _unifiedTown.Reset();
        _unifiedTownNote = null;
        _confirm.Close();
        _guildPickDialog.Close();
        _amigaMessage.Close();
        _damageReport.Close();
        _p2Alert.Close();
        _p2FieldBox.Close();
        _p1OkSpec = null;
        _p1About = false;
        _p1TownHeld = null;
        _outsideBar.Close();
        _townBar.Close();
        CloseMapMessages();
        ResetLeaving(_session);
        CloseDosPages();
        _combatUi.Cancel();
        _prompts.Reset();
        _defeatCause = new();
        _promptNote = "";
        _townMenu.Reset();
        if (_session!.Mode == GameMode.Town)
            RefreshTownStock();
        if (_session.Mode == GameMode.Combat)
        {
            _fightMark = _session.LogSequence;
            _combatUi.Start(_session);
        }
    }

    /// <summary>Load (F9, the game menu, a town's Load). QoL SAVE SLOTS: the slot picker; off, the one save.</summary>
    private void LoadGame()
    {
        if (CurrentGameSettings.SaveSlots)
        {
            OpenSaveSlots(_session!.Game, SaveSlotMode.Load);
            return;
        }
        LoadFrom(ClientSettings.SavePathFor(_session!.Game));
    }

    /// <summary>Continue from the save at <paramref name="path"/> (D-069: the Guild-number question, or the party list).</summary>
    private void LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                SetFeedback(UiText.S("game.no_save_file"), HelpForMode(_session!.Mode));
                return;
            }

            // D-069 (manual): Continue asks for the Guild number of the character to call up (ContinueDialog); QoL
            // ContinueFromList shows the save's parties instead.
            var file = SessionCodec.ReadJsonFile(path);
            if (GameSession.ContinuesWithoutAsking(file, _session!.Game))
            {
                // D-069 dos (p1.continue, the file's locked rules): DOS asks nothing and loads the saved party.
                _session.ContinueSavedParty(file);
                AfterSnapshotApplied();
                _qol = QoLProgress.LoadOrDefault();
                SetFeedback(_session.LastMessage, HelpForMode(_session.Mode));
                return;
            }
            _continueDialog.Open(file, CurrentGameSettings.ContinueFromList);
            SetFeedback(_continueDialog.ShowsList ? ContinueDialog.ListQuestion : ContinueDialog.Question, _continueDialog.Help);
        }
        catch (OldSaveFormatException)
        {
            // A save from before game-prefixed ids (s71): refused, not migrated; said in plain words, not the parser's.
            _continueDialog.OpenNotice(UiText.S("continue.old_save_lines").Split('\n'));
            SetFeedback(UiText.S("continue.old_save"), _continueDialog.Help);
        }
        catch (Exception ex)
        {
            SetFeedback(UiText.F("game.load_failed_x", ex.Message), HelpForMode(_session!.Mode));
        }
    }

    private void ApplyWindowScale()
    {
        if (_screen is null) return;
        var (w, h) = _screen.PreferredBackBufferSize;
        _graphics.PreferredBackBufferWidth = w;
        _graphics.PreferredBackBufferHeight = h;
        _graphics.ApplyChanges();
    }

    private void RefreshTownStock()
    {
        _townStock = UiOverlays.BuildTownStock(_session!, _data!);
        UiOverlays.ArmoryScroll.Reset();
        _townCursor = Math.Clamp(_townCursor, 0, Math.Max(0, _townStock.Count - 1));
    }

    private void DrawModeView(GameSession session, Texture2D pixel, AssetCatalog assets)
    {
        const int scale = 6;
        const int ox = 4;
        const int oy = UiOverlays.ContentTop;

        // s136: II's last round plays on line by line after Core has left the fight (victory, wipe): keep its screen.
        if (_combatUi.P2Run is not null && session.Mode != GameMode.Combat)
        {
            FillBackdrop(pixel, Color.Black);
            UiOverlays.DrawCombat(_spriteBatch!, pixel, assets, session, _settings, _combatUi, _data!, _help);
            return;
        }

        switch (ViewMode(session))
        {
            case GameMode.Overworld:
            {
                FillBackdrop(pixel, new Color(18, 24, 20));
                var region = session.CurrentOverworldRegion;
                if (assets.TileMaps is { } tileMaps && assets.GetOverworldTile(0) is not null)
                    DrawTileOverworld(session, region, tileMaps, assets, pixel, 4, oy);
                else if (assets.Maps is { } maps && assets.GetRegionPicture(region.RegionId) is { } picture)
                {
                    if (_settings.MapOnly)
                    {
                        _spriteBatch!.Draw(pixel, new Rectangle(0, 0, 320, 200), Color.Black);
                        DrawOverworldRegion(session, region, maps, picture, assets, pixel,
                            AmigaMapLeft, AmigaMapTop, OverworldRegion.Width, OverworldRegion.Height);
                        break;
                    }
                    DrawOverworldRegion(session, region, maps, picture, assets, pixel,
                        4, oy, OverworldViewCols, OverworldViewRows);
                }
                else
                {
                    // Fallback when the game has no map art (pre-P0.7 placeholder; III without its Amiga extracts): coloured
                    // cells in a party-centred window of at most I's 26×20 cells (III's Scandor is 50×75).
                    var cols = Math.Min(region.GridWidth, OverworldRegion.Width);
                    var rows = Math.Min(region.GridHeight, OverworldRegion.Height);
                    var x0 = Math.Clamp(session.Overworld.X - cols / 2, 0, region.GridWidth - cols);
                    var y0 = Math.Clamp(session.Overworld.Y - rows / 2, 0, region.GridHeight - rows);
                    for (var y = 0; y < rows; y++)
                    for (var x = 0; x < cols; x++)
                    {
                        var rect = new Rectangle(ox + x * scale, oy + y * scale, scale - 1, scale - 1);
                        _spriteBatch!.Draw(pixel, rect, TileColor(session, region, x0 + x, y0 + y));
                    }

                    HitMapView(ox, oy, cols, rows, scale, scale, x0, y0);
                    var pr = new Rectangle(
                        ox + (session.Overworld.X - x0) * scale,
                        oy + (session.Overworld.Y - y0) * scale,
                        scale - 1, scale - 1);
                    _spriteBatch!.Draw(pixel, pr, Color.White);
                }

                UiOverlays.DrawFieldSidebar(_spriteBatch!, pixel, assets, session, !_settings.Game(session.Game).TimeLagInOptions,
                    TerrainKeyFor(session));
                break;
            }
            case GameMode.Dungeon when session.Dungeon is not null:
            {
                FillBackdrop(pixel, new Color(10, 10, 14));
                var level = session.Dungeon.Level ?? _dungeonView!.GetLevel(session.Dungeon.LevelId);
                // The auto-map box (PLAN P3 QoL; neither original draws one: III Amiga's dungeon screen is the whole
                // 24 x 30 level itself, Amiga FORMAT.md section 5). s171: III's tile dungeon got a whole-level overview
                // inside the view left of the cards, in the corner away from the party (DrawDungeonOverview). s172: I and
                // II's glyph view and the fallback grid place it the same way; before, their see-through box sat at
                // (222, 16) over member cards 2, 3 and 5. Each game's AUTO-MAP BOX option (on by default; Tab flips it).
                (Rectangle View, Rectangle Party, Func<int, Color> Colour)? overview = null;
                if (assets.TileMaps is { } tileMaps && assets.GetDungeonCell(tileMaps.UnseenGlyph) is not null)
                {
                    var (view, party) = DrawTileDungeon(session.Dungeon, level, tileMaps, assets, pixel, 4, oy);
                    overview = (view, party, cell => DungeonGlyphColour(assets.GetDungeonCell(tileMaps.DungeonGlyphFor(cell))));
                }
                else if (assets.Maps is { } maps && _data?.MapTiles is { } glyphRule)
                {
                    if (_settings.MapOnly)
                    {
                        if (P1MapScreen(session) is not null)
                            _spriteBatch!.Draw(pixel, new Rectangle(0, 0, 320, 200), Color.Black); // s155: the Amiga's pen 0
                        DrawDungeonGlyphs(session.Dungeon, level, maps, glyphRule, assets, pixel,
                            AmigaDungeonLeft, AmigaDungeonTop, DungeonLevel.Width, DungeonLevel.Height, 1);
                        if (assets.GetDungeonCompass() is { } compass)
                            _spriteBatch!.Draw(compass, new Vector2(AmigaCompassX, AmigaCompassY), Color.White);
                        PixelFont.Draw(_spriteBatch!, pixel, 214, 136, UiText.F("game.d_x_x", session.Dungeon.LevelId, UiText.Name(session.Dungeon.Facing)),
                            new Color(160, 200, 220));
                        var (shownX, shownY) = MapPartyAt(session.Dungeon) ?? (session.Dungeon.X, session.Dungeon.Y);
                        PixelFont.Draw(_spriteBatch!, pixel, 214, 146, $"({shownX},{shownY})", new Color(140, 160, 180));
                        break;
                    }
                    var (view, party) = DrawDungeonGlyphs(session.Dungeon, level, maps, glyphRule, assets, pixel,
                        4, oy, DungeonViewCols, DungeonViewRows, DungeonGlyphScale);
                    overview = (view, party, cell => DungeonGlyphColour(assets.GetDungeonGlyph(glyphRule.DungeonGlyph(cell)),
                        cell < 2 ? Color.Gray : new Color(40, 40, 50)));
                }
                else
                {
                    const int view = 9;
                    var cx = session.Dungeon.X;
                    var cy = session.Dungeon.Y;
                    for (var dy = -view; dy <= view; dy++)
                    for (var dx = -view; dx <= view; dx++)
                    {
                        var x = cx + dx;
                        var y = cy + dy;
                        if (!level.InBounds(x, y)) continue;
                        var visited = _qol.IsVisited(session.Dungeon.LevelId, x, y);
                        Color color;
                        if (!visited)
                            color = new Color(12, 12, 16);
                        else
                        {
                            var cell = level.Get(x, y);
                            color = cell == 0 ? new Color(40, 40, 50) : new Color(90, 70, 50);
                        }

                        var rect = new Rectangle(
                            ox + (dx + view) * scale,
                            oy + (dy + view) * scale,
                            scale - 1, scale - 1);
                        _spriteBatch!.Draw(pixel, rect, color);
                    }

                    HitMapView(ox, oy, 2 * view + 1, 2 * view + 1, scale, scale, cx - view, cy - view);
                    var pr = new Rectangle(ox + view * scale, oy + view * scale, scale - 1, scale - 1);
                    _spriteBatch!.Draw(pixel, pr, Color.Cyan);
                    overview = (new Rectangle(ox, oy, (2 * view + 1) * scale, (2 * view + 1) * scale), pr,
                        cell => cell == 0 ? new Color(40, 40, 50) : new Color(90, 70, 50));
                }

                UiOverlays.DrawFieldSidebar(_spriteBatch!, pixel, assets, session, !_settings.Game(session.Game).TimeLagInOptions);

                if (_settings.Game(session.Game).AutoMap && overview is { } o)
                    UiOverlays.DrawDungeonOverview(_spriteBatch!, pixel, level, session.Dungeon, _qol, o.Colour,
                        UiOverlays.TileOverviewPlacement(o.View, o.Party, level.GridWidth, level.GridHeight));
                break;
            }
            case GameMode.Town:
                if (_townStock.Count == 0)
                    RefreshTownStock();
                if (_doorFlash is { } flash)
                    UiOverlays.DrawTownMenu(_spriteBatch!, pixel, assets, session, _townMenu, openDoor: flash.Door,
                        originalFont: CurrentGameSettings.OriginalScreens, dosBar: TownBarPlaces(session) is null);
                else if (_townMenu.Place == TownPlace.Armory)
                    UiOverlays.DrawArmory(_spriteBatch!, pixel, assets, session, _townStock, _townCursor, _members.Row);
                else
                    UiOverlays.DrawTownMenu(_spriteBatch!, pixel, assets, session, _townMenu,
                        originalFont: CurrentGameSettings.OriginalScreens, dosBar: TownBarPlaces(session) is null);
                break;
            case GameMode.Guild:
            case GameMode.Title:
                UiOverlays.DrawGuild(_spriteBatch!, pixel, assets, session);
                break;
            case GameMode.Combat:
                UiOverlays.DrawCombat(
                    _spriteBatch!, pixel, assets, session, _settings, _combatUi, _data!, _help);
                break;
        }
    }

    // Overworld view (P0.7): the Amiga region picture (h56 0x6bc polygons, 12×9 cells) in a party-centred window that
    // fits the content area (the Amiga shows the whole 26×20 region at (4,15); the remaster layout keeps a sidebar —
    // P2 layout). Then as the Amiga: fogged cells black (h56 0x860), town / inn / dungeon icons (h56 0x5a4, hidden
    // under fog), the party sprite (h77 0xdfe) at its cell's top-left.
    private readonly Dictionary<GameId, IReadOnlyList<TerrainKeyEntry>> _terrainKeys = [];

    /// <summary>
    /// QoL TERRAIN KEY (<see cref="GameSettings.TerrainKey"/>, off = the original: no key): the game's
    /// <c>terrain-key.json</c> rows on the wilderness map when the switch is on and the pack holds their pictures (II's ST
    /// pack); else null.
    /// </summary>
    private IReadOnlyList<TerrainKeyEntry>? TerrainKeyFor(GameSession session)
    {
        if (!_settings.Game(session.Game).TerrainKey || ViewMode(session) != GameMode.Overworld)
            return null;
        if (!_terrainKeys.TryGetValue(session.Game, out var key))
            _terrainKeys[session.Game] = key = TerrainKey.For(session.Game);
        var assets = _assets;
        return assets is not null && key.Any(e => e.Pictures.Any(p => assets.Get(p) is not null)) ? key : null;
    }

    private const int OverworldViewCols = 13;
    private const int OverworldViewRows = 16;

    // The Amiga full-screen positions (FORMAT.md, s52): region picture at (4,15) (h56 0x948), dungeon glyphs at (10,16)
    // (h56 0xd10), compass at (214,68) (h56 0xe98: h77 +0xcfe(12, h69 +0x2af4, 214, 68, 64, 63); s162: every pixel equal
    // to the live entry shot, P1MapBarTests; II ST's 0x5c7a uses the same place).
    private const int AmigaMapLeft = 4;
    private const int AmigaMapTop = 15;
    public const int AmigaDungeonLeft = 10;
    public const int AmigaDungeonTop = 16;
    public const int AmigaCompassX = 214;
    public const int AmigaCompassY = 68;

    /// <summary>The town menu on the full-screen town picture (not the Armory's list screen, not the inventory screen).</summary>
    private bool TownPictureScreen(GameSession s) =>
        s.Mode == GameMode.Town && _townMenu.Place != TownPlace.Armory && !InventoryScreenOpen && _assets is not null;

    private readonly InputWaitCycleClock _cycleClock = new();

    /// <summary>
    /// The time the pack's colour cycle is drawn at: play time, or (s153, <see cref="ColourCycle.InputWaitOnly"/>, I Amiga's
    /// water) the steps taken while the overworld waited for input, polled on the machine's clock (the one the boxes' 8-s
    /// cut reads).
    /// </summary>
    private double CycleSeconds(GameSession s, double playSeconds)
    {
        if (_assets?.Pack?.ColourCycle is not { InputWaitOnly: true } cycle)
            return playSeconds;
        if (MapRedrawn(s))
            _cycleClock.Redraw();
        _cycleClock.Poll(Environment.TickCount64 / 1000.0, MapWaitsForInput(s), cycle);
        return cycle.SecondsAtStep(_cycleClock.StoredShown ? 0 : _cycleClock.Steps);
    }

    private (GameMode Mode, int Region)? _cycleMapAt;

    /// <summary>
    /// s154: the overworld was just drawn afresh with its stored colours (I Amiga: h2 <c>+0x328</c> → h59 <c>+0x62c</c> →
    /// h56 <c>+0x4d4</c>, whose LoadRGB4 of h69 <c>+0x74</c> resets the water's pens): back on the overworld from a town, a
    /// dungeon, a defeat or a load, and a step into another region (h59 <c>+0x8c</c>). Not after a fight: its end loads back
    /// the pens it saved (h53 <c>+0x2a4</c>).
    /// </summary>
    private bool MapRedrawn(GameSession s)
    {
        var view = ViewMode(s);
        (GameMode, int)? now = view == GameMode.Overworld ? (GameMode.Overworld, s.Overworld.RegionId) : (view, -1);
        var before = _cycleMapAt;
        _cycleMapAt = now;
        if (view != GameMode.Overworld || before == now)
            return false;
        return before is not { } b || b.Item1 != GameMode.Combat;
    }

    /// <summary>
    /// s153: the overworld waits for a key with nothing else running (I Amiga's input wait loop h80 <c>+0x1ac</c> polls):
    /// no box or MESS window of a step (nor its sound), no question, dialog or overlay, no fight's screen. The remaster's
    /// own game menu, inventory and bestiary keep it stepping (s154): they stand in for the Amiga's menu strip, under which
    /// the loop keeps polling (a pick's chooser or requester stops it: the remaster's dialogs do).
    /// </summary>
    private bool MapWaitsForInput(GameSession s) =>
        s.Mode == GameMode.Overworld && ViewMode(s) == GameMode.Overworld && _mapRun is null && CurrentPrompt is null && !P1HeldRequesterUp
        && (OpenModal is null || ReferenceEquals(OpenModal, _gameMenu)) && !_dosDialog.IsOpen && !_combatUi.IsPresenting
        && !_p1Holds.Busy(_updateSeconds) && !_outsideBar.CreditsShown && !_outsideBar.StatusShown;

    /// <summary>
    /// s154: the map's Health report as I Amiga's requester 21 (<see cref="P1MapReports.MapHealth"/>), under AMIGA SCREENS with
    /// I's pack on the overworld or in a dungeon; null: the remaster's panel.
    /// </summary>
    private IReadOnlyList<string>? P1MapHealth(GameSession s) =>
        s.Game == GameId.P1 && CurrentGameSettings.OriginalScreens && s.Mode is GameMode.Overworld or GameMode.Dungeon
        && _assets?.Pack?.MapMessage?.Window is { } window
            ? P1MapReports.MapHealth(window, s.Party)
            : null;

    private bool FullScreenOverworld(GameSession s) =>
        _settings.MapOnly && ViewMode(s) == GameMode.Overworld && _assets?.Maps is not null &&
        _assets.GetRegionPicture(s.CurrentOverworldRegion.RegionId) is not null;

    private void DrawOverworldRegion(GameSession session, OverworldRegion region, AmigaMapManifest maps,
        Texture2D picture, AssetCatalog assets, Texture2D pixel, int left, int oy, int cols, int rows)
    {
        var cw = maps.CellWidth;
        var ch = maps.CellHeight;
        var x0 = Math.Clamp(session.Overworld.X - cols / 2, 0, OverworldRegion.Width - cols);
        var y0 = Math.Clamp(session.Overworld.Y - rows / 2, 0, OverworldRegion.Height - rows);
        HitMapView(left, oy, cols, rows, cw, ch, x0, y0);
        _spriteBatch!.Draw(picture, new Rectangle(left, oy, cols * cw, rows * ch),
            new Rectangle(x0 * cw, y0 * ch, cols * cw, rows * ch), Color.White);

        bool InView(int x, int y) => x >= x0 && x < x0 + cols && y >= y0 && y < y0 + rows;
        Rectangle Cell(int x, int y, int w) => new(left + (x - x0) * cw, oy + (y - y0) * ch, w, ch);

        if (maps.Regions.TryGetValue(region.RegionId, out var gfx))
            foreach (var (kind, x, y) in gfx.AllIcons()) // II (ST): every dungeon and castle of the region
                if (InView(x, y) && assets.GetOverworldIcon(kind) is { } icon)
                    _spriteBatch!.Draw(icon, Cell(x, y, cw), Color.White);

        for (var vy = 0; vy < rows; vy++)
        for (var vx = 0; vx < cols; vx++)
            if (region.GetRaw(x0 + vx, y0 + vy) >= maps.FogThreshold)
                _spriteBatch!.Draw(pixel, Cell(x0 + vx, y0 + vy, cw), Color.Black);

        var px = session.Overworld.X;
        var py = session.Overworld.Y;
        // s143: I Amiga frees the party sprite while requester 23 is up (P1MapMessageRun.PartySpriteShown).
        if (!P1MapMessageRun.PartySpriteShown(_mapRun, _updateSeconds * 1000 - _mapRunStartMs))
            return;
        // s153: and moves it off the screen while an h86 requester (every question over the map, and requester 21's
        // reports with OK: SWIMMING:, the damage reports) is up.
        if (MapPartySprite.HiddenUnderPrompt(assets.Pack, CurrentGameSettings.OriginalScreens,
                CurrentPrompt is not null || MapWaitLines() is not null || P1HeldRequesterUp || (_healthDialog.IsOpen && P1MapHealth(session) is not null)
                || P1SheetFor(session) is not null
                // s158 live (FS-UAE over OUT1): every Inspect step, the summary with INSPECT / EXIT and the who list too
                || (_inspectDialog.IsOpen && P1Requesters is not null)))
            return;
        if (assets.GetOverworldParty() is { } party)
            _spriteBatch!.Draw(party, Cell(px, py, party.Width), Color.White);
        else
            _spriteBatch!.Draw(pixel, Cell(px, py, cw), Color.White);
    }

    // Dungeon view (P0.7): Amiga 5×4 map glyphs (h77 0xbc0) drawn 2× in a party-centred window; the glyph per cell
    // follows the DOS rule (OV2 0x21df, map_tiles.dungeon — the Amiga uses the same glyph ids 0xe6..0xed). Unvisited
    // cells stay black, as the map window starts cleared and only redraws the cells round the party.
    private const int DungeonGlyphScale = 2;
    private const int DungeonViewCols = 15;
    private const int DungeonViewRows = 18;

    /// <returns>The view drawn and the party's glyph on it (where the auto-map box must not go).</returns>
    private (Rectangle View, Rectangle Party) DrawDungeonGlyphs(DungeonState state, DungeonLevel level, AmigaMapManifest maps,
        MapTileManifest glyphRule, AssetCatalog assets, Texture2D pixel, int left, int oy, int cols, int rows, int glyphScale)
    {
        var gw = maps.GlyphWidth * glyphScale;
        var gh = maps.GlyphHeight * glyphScale;
        var (px, py) = MapPartyAt(state) ?? (state.X, state.Y); // s141: under op 12's box, still the event cell
        var x0 = Math.Clamp(px - cols / 2, 0, DungeonLevel.Width - cols);
        var y0 = Math.Clamp(py - rows / 2, 0, DungeonLevel.Height - rows);
        HitMapView(left, oy, cols, rows, gw, gh, x0, y0);
        // s162: unseen cells in the pack's colour (I Amiga h56 +0xe2e: pen 13 grey over the map area), else black.
        var unseen = assets.Pack?.DungeonUnseenColour is { } u ? VirtualScreen.ParseColor(u) : Color.Black;
        _spriteBatch!.Draw(pixel, new Rectangle(left, oy, cols * gw, rows * gh), unseen);
        for (var vy = 0; vy < rows; vy++)
        for (var vx = 0; vx < cols; vx++)
        {
            var x = x0 + vx;
            var y = y0 + vy;
            if (!_qol.IsVisited(state.LevelId, x, y)) continue;
            var dest = new Rectangle(left + vx * gw, oy + vy * gh, gw, gh);
            var tex = assets.GetDungeonGlyph(glyphRule.DungeonGlyph(level.Get(x, y)));
            if (tex is not null)
                _spriteBatch!.Draw(tex, dest, Color.White);
            else
                _spriteBatch!.Draw(pixel, dest, level.Get(x, y) < 2 ? Color.Gray : Color.Black);
        }

        var party = new Rectangle(left + (px - x0) * gw, oy + (py - y0) * gh, gw, gh);
        var mark = assets.GetDungeonGlyph(glyphRule.PartyGlyph);
        _spriteBatch!.Draw(mark ?? pixel, party, mark is null ? Color.Red : Color.White);
        return (new Rectangle(left, oy, cols * gw, rows * gh), party);
    }

    // Tile-drawn maps (Phantasie III Amiga, reverse-engineering/amiga/phantasie3/FORMAT.md §4–5). Overworld: a window of
    // ViewColumns × ViewRows tiles (the Amiga's 9×9 of 16×16, 0x9b92), tile = the map byte the remaster plays, the
    // party tile OR-ed over its cell (0x378a). The view follows the party as the Amiga's does (s102, static [C], step
    // routines 0x985e / 0x9912 and their column twins): the party stays in the centre cell except near an edge, where
    // the view stops: at the top / left with the map's first row / column at its edge, at the bottom / right at the
    // size counters the plane change 0x1eb18 sets (the pack's view_counters; the boot runs it too, s125 [C] seen):
    // Scandor 50 × 74 shows columns 42–50 (one past the map) and rows 66–74 (on the map); the Netherworld 31 × 15 one
    // column past its east edge, its last row's cell being the 0xF9 the code writes there (0x1eb3e…0x1ec74). Past the
    // map the Amiga reads on (s108 [C]): a column past the east edge is the next row's first cell (PastMapTile). The
    // planes (9×9) always show the whole plane. III DOS CGA (s160): the same 9×9 of 16×16, tile 89 on the party's cell,
    // and six Scandor tiles drawn as others (the pack's tile_swaps, ovl3 0x0); s170: its view's centre stops at
    // columns − 5 and rows − 4 (view_centre_margin, ovl3 0x13ac…0x1612), so Scandor ends at columns 41–49 and the
    // Netherworld at 22–30: DOS never shows a cell past the map.
    private void DrawTileOverworld(GameSession session, OverworldRegion region, TileMapLayout tiles, AssetCatalog assets,
        Texture2D pixel, int left, int top)
    {
        var tw = tiles.TileWidth;
        var th = tiles.TileHeight;
        var cols = tiles.ViewColumns;
        var rows = tiles.ViewRows;
        (int Columns, int Rows)? counters =
            tiles.ViewCounters.TryGetValue(session.Overworld.RegionId, out var counted) ? counted : null;
        var x0 = Math.Clamp(session.Overworld.X - cols / 2, 0,
            OverworldViewMaxOrigin(region.GridWidth, cols, counters?.Columns, tiles.ViewCentreMargin?.Columns));
        var y0 = Math.Clamp(session.Overworld.Y - rows / 2, 0,
            OverworldViewMaxOrigin(region.GridHeight, rows, counters?.Rows, tiles.ViewCentreMargin?.Rows));
        HitMapView(left, top, cols, rows, tw, th, x0, y0);
        _spriteBatch!.Draw(pixel, new Rectangle(left, top, cols * tw, rows * th), Color.Black);
        for (var vy = 0; vy < rows; vy++)
        for (var vx = 0; vx < cols; vx++)
        {
            var x = x0 + vx;
            var y = y0 + vy;
            var dest = new Rectangle(left + vx * tw, top + vy * th, tw, th);
            if (!region.InBounds(x, y))
            {
                // Past the map: the byte the Amiga reads there (the next row's first cell), or black (FORMAT.md §4).
                if (PastMapTile(region.GridWidth, region.GridHeight, session.Overworld.RegionId, x, y, tiles.PastMap) is
                    { } past)
                {
                    var b = past.Cell is { } c ? region.GetRaw(c.X, c.Y) : past.Tile;
                    if (assets.GetOverworldTile(b) is { } pastTex)
                        _spriteBatch.Draw(pastTex, dest, Color.White);
                }
                continue;
            }
            if (assets.GetOverworldTile(tiles.DrawnTile(session.Overworld.RegionId, region.GetRaw(x, y))) is { } tex)
                _spriteBatch.Draw(tex, dest, Color.White);
            else
                _spriteBatch.Draw(pixel, dest, TileColor(session, region, x, y));
        }

        var party = new Rectangle(left + (session.Overworld.X - x0) * tw, top + (session.Overworld.Y - y0) * th, tw, th);
        if (assets.GetOverworldParty() is { } mark)
            _spriteBatch.Draw(mark, party, Color.White);
        else
            _spriteBatch.Draw(pixel, party, Color.White);
    }

    /// <summary>
    /// The largest first row (column) of the tile overworld view over a map <paramref name="size"/> long with a
    /// <paramref name="view"/>-cell view. <paramref name="counter"/> (the pack's <see cref="TileMapLayout.ViewCounters"/>):
    /// the Amiga stops the view's centre at <c>counter − ⌊view / 2⌋</c> (<c>0x9912</c> / <c>0x9a92</c>), so the view's last
    /// row / column is the counter's (III Scandor: columns 50 = one past the map, rows 74 = the map's last). Without one
    /// the window stays on the map. A map no larger than the view starts at 0. s170: <paramref name="margin"/> (the pack's
    /// <see cref="TileMapLayout.ViewCentreMargin"/>) stops the centre at <c>counter − margin</c> instead (III DOS's
    /// columns: 50 − 5 = 45, so Scandor's view ends at column 49, ovl3 <c>0x1612</c>).
    /// </summary>
    public static int OverworldViewMaxOrigin(int size, int view, int? counter, int? margin = null) =>
        size <= view ? 0
        : counter is { } n ? Math.Max(0, margin is { } m ? n - m - view / 2 : n - (view - 1))
        : size - view;

    /// <summary>
    /// What the Amiga shows on a view cell past a <paramref name="width"/> × <paramref name="height"/> map (FORMAT.md §4,
    /// s108 [C]): it draws map byte <c>y · width + x</c> unchecked, so a cell one column past the east edge is the next
    /// row's first cell (<see cref="PastMapResult.Cell"/>); a byte past the whole map is one the code wrote there
    /// (<paramref name="pastMap"/>, the Netherworld's tile 249) or undefined memory (null: drawn black).
    /// </summary>
    public static PastMapResult? PastMapTile(int width, int height, int regionId, int x, int y,
        IReadOnlyList<TileMapLayout.PastMapCell> pastMap)
    {
        if (x < 0 || y < 0 || x > width)
            return null;
        var index = y * width + x;
        if (index < width * height)
            return new PastMapResult((index % width, index / width), 0);
        foreach (var p in pastMap)
            if (p.Region == regionId && p.Index == index)
                return new PastMapResult(null, p.Tile);
        return null;
    }

    /// <summary>A past-the-map view cell: a map cell to draw, or (when <see cref="Cell"/> is null) a tile.</summary>
    public sealed record PastMapResult((int X, int Y)? Cell, int Tile);

    // Dungeon: one GlyphWidth × GlyphHeight glyph per cell (the Amiga's 8×5, 0x1e47a), chosen from the cell's map byte by
    // the pack's dungeon_cells table (0x1e750); cells the party has not seen draw the pack's "unseen" glyph, as the Amiga
    // starts the map filled with it. The window is party-centred and as large as the content area beside the sidebar
    // allows (the Amiga shows the whole 24×30 map). Which cells count as seen: the remaster's visited set [~].
    private const int TileDungeonWidth = 160;
    private const int TileDungeonHeight = UiOverlays.ContentBottom - UiOverlays.ContentTop;

    /// <summary>The tile dungeon's view; returns its screen rectangle and the party's glyph in it (the s171 overview's placement).</summary>
    private (Rectangle View, Rectangle Party) DrawTileDungeon(DungeonState state, DungeonLevel level, TileMapLayout tiles,
        AssetCatalog assets, Texture2D pixel, int left, int top)
    {
        var gw = tiles.GlyphWidth;
        var gh = tiles.GlyphHeight;
        var cols = Math.Min(level.GridWidth, TileDungeonWidth / gw);
        var rows = Math.Min(level.GridHeight, TileDungeonHeight / gh);
        var x0 = Math.Clamp(state.X - cols / 2, 0, level.GridWidth - cols);
        var y0 = Math.Clamp(state.Y - rows / 2, 0, level.GridHeight - rows);
        HitMapView(left, top, cols, rows, gw, gh, x0, y0);
        for (var vy = 0; vy < rows; vy++)
        for (var vx = 0; vx < cols; vx++)
        {
            var x = x0 + vx;
            var y = y0 + vy;
            var glyph = x == state.X && y == state.Y ? tiles.PartyGlyph
                : _qol.IsVisited(state.LevelId, x, y) ? tiles.DungeonGlyphFor(level.Get(x, y))
                : tiles.UnseenGlyph;
            var dest = new Rectangle(left + vx * gw, top + vy * gh, gw, gh);
            if (assets.GetDungeonCell(glyph) is { } tex)
                _spriteBatch!.Draw(tex, dest, Color.White);
            else
                _spriteBatch!.Draw(pixel, dest, glyph == tiles.PartyGlyph ? Color.Red : Color.Gray);
        }
        return (new Rectangle(left, top, cols * gw, rows * gh),
            new Rectangle(left + (state.X - x0) * gw, top + (state.Y - y0) * gh, gw, gh));
    }

    /// <summary>s171: a dungeon glyph's colour in the overview box (<see cref="UiOverlays.DominantColour"/>), read once per
    /// texture; <paramref name="fallback"/> (grey) when the pack has no such glyph.</summary>
    private Color DungeonGlyphColour(Texture2D? tex, Color? fallback = null)
    {
        if (tex is null)
            return fallback ?? Color.Gray;
        if (_glyphColours.TryGetValue(tex, out var c))
            return c;
        var data = new Color[tex.Width * tex.Height];
        tex.GetData(data);
        return _glyphColours[tex] = UiOverlays.DominantColour(data, Color.Gray);
    }

    private readonly Dictionary<Texture2D, Color> _glyphColours = new(ReferenceEqualityComparer.Instance);

    private void FillBackdrop(Texture2D pixel, Color color) =>
        _spriteBatch!.Draw(pixel,
            new Rectangle(0, UiOverlays.ContentTop, 320, UiOverlays.ContentBottom - UiOverlays.ContentTop),
            color);

    /// <summary>
    /// The fallback cell colour: Phantasie III's maps by their terrain (<see cref="P3Terrain"/>, no art yet), I / II's by
    /// the tile class.
    /// </summary>
    private static Color TileColor(GameSession session, OverworldRegion region, int x, int y)
    {
        var tile = region.GetNormalized(x, y);
        if (session.Campaign is not Phantasie3Campaign { } p3)
            return TileColor(tile);
        var world = p3.World;
        if (world.PlaceAt(region.RegionId, x, y) is { } place)
            return place.Kind switch
            {
                P3PlaceKind.Town => new Color(180, 140, 60),
                P3PlaceKind.Dungeon => new Color(100, 40, 40),
                _ => new Color(200, 170, 110),
            };
        if (world.Terrain.IsBlocked(tile))
            return new Color(20, 30, 90);
        return world.Terrain.TerrainOf(tile, world.IsNetherworld(region.RegionId)) switch
        {
            P3Terrain.Water => new Color(30, 60, 140),
            P3Terrain.Lava => new Color(170, 50, 20),
            0 => new Color(70, 130, 60),   // meadows
            1 => new Color(30, 90, 40),    // forest
            2 => new Color(110, 110, 60),  // hills
            3 => new Color(120, 110, 110), // mountains
            9 => new Color(190, 170, 100), // desert
            6 or 13 => new Color(150, 150, 150), // path, bridge
            11 or 14 or 16 or 18 => new Color(170, 170, 190), // mist, fog, steam
            17 => new Color(50, 40, 50),   // black path
            _ => new Color(80, 90, 80),
        };
    }

    private static Color TileColor(int tile)
    {
        if (tile == OverworldRegion.WallTile) return new Color(30, 30, 35);
        var cls = tile / 10;
        return cls switch
        {
            4 => new Color(180, 140, 60), // town
            5 => new Color(100, 40, 40),  // dungeon
            10 => new Color(30, 60, 140), // water
            _ => new Color(40 + (cls * 12) % 80, 90, 45),
        };
    }

    /// <summary>
    /// The status line's tail (QoL <see cref="GameSettings.StatusLines"/>): region, position and DAY OUT (OV2 <c>0x46c0</c>,
    /// travel time >> 8) in the wilderness; level, facing and position in a dungeon; null elsewhere.
    /// </summary>
    private static string? FieldStatus(GameSession s) => s.Mode switch
    {
        GameMode.Overworld =>
            UiText.F("game.out_x_x_x_day_x", s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y, s.Campaign.DaysOut(s.TravelTime)),
        GameMode.Dungeon when s.Dungeon is not null => UiText.F("game.d_x_x_x_x", s.Dungeon.LevelId, UiText.Name(s.Dungeon.Facing), s.Dungeon.X, s.Dungeon.Y),
        _ => null,
    };

    /// <summary>
    /// III Amiga (s135): a town / guild line whose Amiga equivalent goes through window 7 (<c>0xaaac</c>) opens the message
    /// box, under AMIGA SCREENS with the pack's window; otherwise it stays on the remaster's band or line.
    /// </summary>
    private void OfferAmigaMessage(string? line)
    {
        if (_amigaMessage.Offer(_assets?.KeepWindows?.Message,
                _session is { Game: GameId.P3 } && CurrentGameSettings.OriginalScreens, line))
            _help = UiText.S("p3combat.message_help");
    }

    /// <summary>
    /// s165: III's Damage Report window under AMIGA SCREENS with the pack's window (<see cref="KeepWindowsLayout.DamageReport"/>),
    /// or null (the remaster's text panel shows the report).
    /// </summary>
    private KeepWindowsLayout.InfoWindow? DamageReportWindow =>
        _session is { Game: GameId.P3 } && CurrentGameSettings.OriginalScreens && _assets?.KeepWindows is { DamageReport: { } w }
            ? w : null;

    /// <summary>
    /// s165: a III step or answer that made a Damage Report (<see cref="GameSession.DamageReport"/>) opens its window; the
    /// member lines then leave the band, the prompt's note and the defeat box (the window holds them; "A trap!!!" stays).
    /// </summary>
    private void OfferDamageReport()
    {
        if (!_damageReport.Offer(_session) || _damageReport.Report is not { } report)
            return;
        var noted = report.Notes.Select(n => n.Trim()).ToHashSet();
        if (_promptNoteLines.Count > 0)
            _promptNote = string.Join(" ", _promptNoteLines = [.. _promptNoteLines.Where(n => !noted.Contains(n.Trim()))]);
        _defeatCause = [.. _defeatCause.Where(n => !noted.Contains(n.Trim()))];
        _message = P3DamageReportBox.WithoutReportLines(_message, report);
        if (_promptFeedback is { } shown)
            _promptFeedback = (P3DamageReportBox.WithoutReportLines(shown.Message, report), shown.Help);
        _help = UiText.S("p3combat.message_help"); // any key or OK closes it, as window 7
    }

    /// <summary>
    /// The message for the band / line: none while window 7 shows it (s135). s163: a window title's row break ("\r" and the
    /// next row's leading blanks, III's Monster Evaluate line) is one space on the band's single line.
    /// </summary>
    private string BandMessage => (_amigaMessage.Shown && _message == _amigaMessage.Text) || _mapBoxed.Contains(_message.Trim())
        || _dosPaged.Contains(_message.Trim()) ? "" : OneLine(_message);

    /// <summary>s163: <paramref name="text"/> with each window row break ("\r" and the blanks after it) as one space.</summary>
    public static string OneLine(string text) => System.Text.RegularExpressions.Regex.Replace(text ?? "", "\r *", " ");

    /// <summary>s183: the pad is the input and <paramref name="help"/> has a pad wording (<see cref="PadWording"/>).</summary>
    private static bool PadHelpShown(string help) => PadWording.Shows(help); // s185: a templated pad wording too (Format)

    private void SetFeedback(string message, string help)
    {
        _message = message ?? "";
        _help = help ?? "";
    }

    /// <summary>
    /// s187 (pad fix A5): the field help is the one without "F2 map" (the <c>_nomap</c> ids) when the art pack has no map layout (III, II Apple, I CGA:
    /// F2 changes nothing there).
    /// </summary>
    private bool NoMapLayout => _assets?.Maps is null;

    /// <summary>
    /// s187 (pad fix A5): the map-only dungeon (F2's layout over the pack's glyph map, outside I Amiga's own screen) draws
    /// no party sidebar and no overview box, so its help is the map-only one (no Tab, no [ ]), as the overworld's.
    /// </summary>
    private bool MapOnlyDungeon(GameSession s) =>
        _settings.MapOnly && ViewMode(s) == GameMode.Dungeon && s.Dungeon is not null && P1MapScreen(s) is null
        && _assets is { Maps: not null } a && _data?.MapTiles is not null
        && !(a.TileMaps is { } tiles && a.GetDungeonCell(tiles.UnseenGlyph) is not null);

    /// <summary>The help the field's band draws: the map-only one in place of the dungeon's own (<see cref="MapOnlyDungeon"/>).</summary>
    private string FieldBandHelp(GameSession s) =>
        MapOnlyDungeon(s) && _help == HelpForMode(s.Mode) ? UiText.S("game.map_only_help")
        // s193 (pad fix E29): III Amiga's menu bar outdoors pulls Options down on Start; its pad help names it
        : OutsideBarShown(s) && _help == HelpForMode(s.Mode)
            ? ViewMode(s) == GameMode.Dungeon ? UiText.S("game.outside_bar_dungeon_help") : UiText.S("game.outside_bar_help")
        : _help;

    private string HelpForMode(GameMode mode) => mode switch
    {
        GameMode.Town when _townMenu.Place == TownPlace.Armory => UiText.S("game.up_down_enter_buy_1_6"),
        GameMode.Town when _townMenu.Place != TownPlace.Menu => UiText.S("game.up_down_enter_select_bksp_back"),
        GameMode.Guild => UiText.S("game.l_list_p_purge_n_new"),
        GameMode.Title => UiText.S("game.title_guild_help"), // s185: only A / D work here; Esc (pad B) = game menu
        GameMode.Overworld when _session is { } s && TerrainKeyFor(s) is { Count: > 0 } => (NoMapLayout ? UiText.S("game.3_n_e_s_w_arrows_key_nomap") : UiText.S("game.3_n_e_s_w_arrows_key")),
        GameMode.Overworld => (NoMapLayout ? UiText.S("game.3_n_e_s_w_arrows_nomap") : UiText.S("game.3_n_e_s_w_arrows")),
        // s194 (pad pass): III Amiga's town bar takes Start for its menus and not Back (C15 is the DOS row's), so its own help
        GameMode.Town when _session is { } bs && TownBarPlaces(bs) is not null => UiText.S("game.town_bar_help"),
        GameMode.Town => UiText.S("game.arrows_enter_a_b_m_n"),
        GameMode.Dungeon => (NoMapLayout ? UiText.S("game.3_n_e_s_w_arrows_2_nomap") : UiText.S("game.3_n_e_s_w_arrows_2")),
        GameMode.Combat => UiText.S("game.1_6_orders_enter_keep_bksp"),
        _ => UiText.S("game.f11_scale_esc_menu"),
    };

    private string HelpForCombat() => _combatPad.Help(_combatUi.PadStep(_session!), id => _data?.GetSpell(id).Name ?? "")
        ?? _combatUi.Phase switch
    {
        CombatPhase.PartyOptions => UiText.S("game.arrows_choose_enter_select_shift_auto"),
        CombatPhase.Orders when _combatUi.AsksAmigaSpell || _combatUi.AsksAmigaHeal => _combatUi.LastLine,
        CombatPhase.Orders when _combatUi.AmigaMessageShown => UiText.S("p3combat.message_help"),
        CombatPhase.Orders when _combatUi.AmigaOrdersShown => UiText.S("p3combat.orders_help"),
        // s193 (pad fix E30): where B is not "redo": DOS III's rank menu (B answers Two) and the first member's order
        // menu (B / Esc is the game menu there)
        CombatPhase.Orders when _combatUi.AsksDosRank(_session!) => UiText.S("p3combat.dos_rank_menu_help"),
        CombatPhase.Orders when _combatUi.OnFirstDosOrderMenu(_session!) => UiText.S("game.1_6_orders_enter_keep_esc"),
        _ => HelpForMode(_session!.Mode),
    };

}
