using System.Text.Json;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// Edge-triggered keyboard + optional gamepad mapping onto <see cref="GameAction"/>.
/// </summary>
public sealed class InputMap
{
    private readonly ClientSettings _settings;
    private KeyboardState _prevKeys;
    private GamePadState _prevPad;
    private readonly List<Keys> _injected = [];

    public InputMap(ClientSettings settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public void EndFrame(KeyboardState keys, GamePadState pad)
    {
        _prevKeys = keys;
        _prevPad = pad;
        _injected.Clear();
    }

    /// <summary>
    /// Keys the mouse sends this frame (<see cref="MouseInput"/>): they count as newly pressed, in this order and repeats
    /// kept, after the real keys in <see cref="NewKeys"/> and in <see cref="Pressed"/> / <see cref="PressedMove"/>. Cleared
    /// by <see cref="EndFrame"/>.
    /// </summary>
    public void Inject(IEnumerable<Keys> keys) => _injected.AddRange(keys);

    public IReadOnlyList<Keys> Injected => _injected;

    /// <summary>A real key went down this frame (not a mouse-sent one).</summary>
    public bool AnyRealKeyPressed(KeyboardState keys) => keys.GetPressedKeys().Any(k => _prevKeys.IsKeyUp(k));

    private bool New(KeyboardState keys, Keys key) =>
        (keys.IsKeyDown(key) && _prevKeys.IsKeyUp(key)) || _injected.Contains(key);

    public bool Pressed(GameAction action, KeyboardState keys, GamePadState pad)
    {
        var key = _settings.GetKey(action);
        if (key != Keys.None && New(keys, key))
            return true;

        return PadHeld(action, pad) && !PadHeld(action, _prevPad);
    }

    public bool Held(GameAction action, KeyboardState keys, GamePadState pad)
    {
        var key = _settings.GetKey(action);
        if (key != Keys.None && keys.IsKeyDown(key))
            return true;

        return PadHeld(action, pad);
    }

    /// <summary>Raw keys newly pressed this frame (for typed input such as combat orders).</summary>
    public IEnumerable<Keys> NewKeys(KeyboardState keys) =>
        keys.GetPressedKeys().Where(k => _prevKeys.IsKeyUp(k)).Concat(_injected);

    /// <summary>Keys released this frame (I Amiga's victory screen counts releases as input events).</summary>
    public IEnumerable<Keys> ReleasedKeys(KeyboardState keys) =>
        _prevKeys.GetPressedKeys().Where(keys.IsKeyUp);

    /// <summary>Gamepad buttons newly pressed this frame (stick directions and triggers count as buttons), for screens
    /// that map every button themselves (<see cref="InventoryScreen.HandleButton"/>). s185: the left stick's directions are
    /// this frame's push from <see cref="TickStick"/> (one direction per push), not the stick's raw buttons.</summary>
    public IEnumerable<Buttons> NewButtons(GamePadState pad) =>
        AllButtons.Where(b => !StickQuantizer.IsStickButton(b) && pad.IsButtonDown(b) && !_prevPad.IsButtonDown(b))
            .Concat(_stickPush);

    private readonly StickQuantizer _menuStick = new();
    private IReadOnlyList<Buttons> _stickPush = [];

    /// <summary>
    /// s185 (Pad leftovers): read the left stick once a frame, before any <see cref="NewButtons"/>. The stick's raw buttons
    /// cross their thresholds on different frames (a diagonal push came as Up, then Right); here a push counts once,
    /// as one of four directions (<see cref="StickQuantizer"/>), and <see cref="NewButtons"/> carries it as that stick button
    /// this frame. I–III's menus, lists and prompts read it so; IV filters the stick's buttons out and runs its own
    /// (eight-way) quantiser.
    /// </summary>
    public void TickStick(GamePadState pad, double elapsedMs) =>
        _stickPush = _menuStick.Update(pad.ThumbSticks.Left, elapsedMs, eightWay: false) is { } push
            ? StickQuantizer.ButtonsOf(push)
            : [];

    private static readonly Buttons[] AllButtons = Enum.GetValues<Buttons>();

    /// <summary>
    /// "Pad coverage" (s182): the keys a pad button stands for on a screen that reads keys only (dialogs, prompts, town
    /// menus, IV, the main menu, Options): the D-pad and left stick are the arrows, A is Return, B is Esc, and the
    /// shoulders are PgUp / PgDn (a long panel's scroll). Other buttons send nothing; a screen that needs more (a digit,
    /// Y / N) adds its own on top. Order follows <paramref name="buttons"/>.
    /// </summary>
    public static IEnumerable<Keys> MenuPadKeys(IEnumerable<Buttons> buttons) =>
        buttons.Select(MenuPadKey).Where(k => k != Keys.None);

    /// <summary>One button's key for <see cref="MenuPadKeys"/>, or <see cref="Keys.None"/>.</summary>
    public static Keys MenuPadKey(Buttons button) => button switch
    {
        Buttons.DPadUp or Buttons.LeftThumbstickUp => Keys.Up,
        Buttons.DPadDown or Buttons.LeftThumbstickDown => Keys.Down,
        Buttons.DPadLeft or Buttons.LeftThumbstickLeft => Keys.Left,
        Buttons.DPadRight or Buttons.LeftThumbstickRight => Keys.Right,
        Buttons.A => Keys.Enter,
        Buttons.B => Keys.Escape,
        Buttons.LeftShoulder => Keys.PageUp,
        Buttons.RightShoulder => Keys.PageDown,
        _ => Keys.None,
    };

    /// <summary>
    /// This frame's new keys (<see cref="NewKeys"/>) followed by the pad's newly pressed buttons as keys
    /// (<see cref="MenuPadKeys"/>): the one feed for a key-only screen so a pad-only player can use it.
    /// </summary>
    public IEnumerable<Keys> MenuKeys(KeyboardState keys, GamePadState pad) =>
        NewKeys(keys).Concat(MenuPadKeys(NewButtons(pad)));

    /// <summary>A list / cursor step: the bound move key (N / S / W / E by default), an arrow key or the D-pad.</summary>
    public bool PressedMove(CardinalDirection dir, KeyboardState keys, GamePadState pad)
    {
        var action = dir switch
        {
            CardinalDirection.North => GameAction.MoveNorth,
            CardinalDirection.South => GameAction.MoveSouth,
            CardinalDirection.West => GameAction.MoveWest,
            _ => GameAction.MoveEast,
        };
        if (Pressed(action, keys, pad))
            return true;

        var arrow = dir switch
        {
            CardinalDirection.North => Keys.Up,
            CardinalDirection.South => Keys.Down,
            CardinalDirection.West => Keys.Left,
            _ => Keys.Right,
        };
        if (New(keys, arrow))
            return true;

        return dir switch
        {
            CardinalDirection.North =>
                pad.DPad.Up == ButtonState.Pressed && _prevPad.DPad.Up == ButtonState.Released,
            CardinalDirection.South =>
                pad.DPad.Down == ButtonState.Pressed && _prevPad.DPad.Down == ButtonState.Released,
            CardinalDirection.West =>
                pad.DPad.Left == ButtonState.Pressed && _prevPad.DPad.Left == ButtonState.Released,
            _ =>
                pad.DPad.Right == ButtonState.Pressed && _prevPad.DPad.Right == ButtonState.Released,
        };
    }

    /// <summary>
    /// A step on the wilderness or dungeon map as (dx, dy), y growing south, or null. Four-way: <see cref="PressedMove"/>
    /// plus the manual's <c>3</c> for north (D-076: the 3-E-S-W diamond) and numpad 8 / 2 / 4 / 6. With
    /// <paramref name="diagonals"/> (QoL) numpad 7 / 9 / 1 / 3, or Home / PgUp / End / PgDn when Num Lock is off.
    /// </summary>
    public (int Dx, int Dy)? PressedFieldMove(KeyboardState keys, GamePadState pad, bool diagonals)
    {
        bool New(Keys k) => this.New(keys, k);

        if (PressedMove(CardinalDirection.North, keys, pad) || New(Keys.D3) || New(Keys.NumPad8)) return (0, -1);
        if (PressedMove(CardinalDirection.South, keys, pad) || New(Keys.NumPad2)) return (0, 1);
        if (PressedMove(CardinalDirection.West, keys, pad) || New(Keys.NumPad4)) return (-1, 0);
        if (PressedMove(CardinalDirection.East, keys, pad) || New(Keys.NumPad6)) return (1, 0);
        if (!diagonals) return null;
        if (New(Keys.NumPad7) || New(Keys.Home)) return (-1, -1);
        if (New(Keys.NumPad9) || New(Keys.PageUp)) return (1, -1);
        if (New(Keys.NumPad1) || New(Keys.End)) return (-1, 1);
        if (New(Keys.NumPad3) || New(Keys.PageDown)) return (1, 1);
        return null;
    }

    /// <summary>
    /// The ordinary steps a field move is made of: one for a straight move; for a diagonal north/south first, then
    /// east/west (<see cref="GameSettings.DiagonalMoves"/>).
    /// </summary>
    public static IReadOnlyList<CardinalDirection> StepsFor(int dx, int dy)
    {
        var steps = new List<CardinalDirection>(2);
        if (dy != 0) steps.Add(dy < 0 ? CardinalDirection.North : CardinalDirection.South);
        if (dx != 0) steps.Add(dx < 0 ? CardinalDirection.West : CardinalDirection.East);
        return steps;
    }

    /// <summary>
    /// s183 (Pad leftovers): the right stick pushed left or right, well clear of up / down (those scroll the sidebar and a
    /// long prompt): the pad's F2, the map layout switch (<see cref="GameAction.ToggleMapLayout"/>).
    /// </summary>
    public static bool RightStickSideways(GamePadState pad) =>
        Math.Abs(pad.ThumbSticks.Right.X) > 0.6f && Math.Abs(pad.ThumbSticks.Right.Y) < 0.4f;

    private static bool PadHeld(GameAction action, GamePadState pad) =>
        action switch
        {
            GameAction.Confirm or GameAction.Fight => pad.Buttons.A == ButtonState.Pressed,
            GameAction.Cancel or GameAction.GameMenu => pad.Buttons.B == ButtonState.Pressed,
            GameAction.ToggleInventory => pad.Buttons.X == ButtonState.Pressed,
            GameAction.ToggleBestiary => pad.Buttons.Y == ButtonState.Pressed,
            GameAction.Save => pad.Buttons.LeftShoulder == ButtonState.Pressed,
            GameAction.Load => pad.Buttons.RightShoulder == ButtonState.Pressed,
            GameAction.TurnLeft => pad.Buttons.LeftStick == ButtonState.Pressed,
            GameAction.TurnRight => pad.Buttons.RightStick == ButtonState.Pressed,
            GameAction.ToggleAutoMap => pad.Buttons.Back == ButtonState.Pressed,
            GameAction.ToggleMapLayout => RightStickSideways(pad),
            GameAction.FastForwardHold => pad.Triggers.Left > 0.5f,
            GameAction.OpenGuildDepart => pad.Buttons.LeftShoulder == ButtonState.Pressed
                                         && pad.Buttons.RightShoulder == ButtonState.Pressed,
            _ => false,
        };
}

/// <summary>Client-only visited dungeon cells + bestiary unlocks, persisted beside the save.</summary>
public sealed class QoLProgress
{
    public Dictionary<string, List<string>> VisitedByLevel { get; set; } = new();

    /// <summary>
    /// The dungeon cells the party has stood on, per level id (<c>"x,y"</c>, as <see cref="VisitedByLevel"/>). A mouse
    /// walk routes through a <c>0x01</c> cell (drawn as wall) only once it is in here (user s77,
    /// <see cref="FieldPath.Dungeon"/>).
    /// </summary>
    public Dictionary<string, List<string>> StoodOnByLevel { get; set; } = new();

    public HashSet<int> SeenMonsterIds { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void MarkVisited(int levelId, int x, int y) => Mark(VisitedByLevel, levelId, x, y);

    public bool IsVisited(int levelId, int x, int y) => IsIn(VisitedByLevel, levelId, x, y);

    public IEnumerable<(int X, int Y)> VisitedCells(int levelId) => Cells(VisitedByLevel, levelId);

    /// <summary>The party stands on (<paramref name="x"/>, <paramref name="y"/>) of level <paramref name="levelId"/>.</summary>
    public void MarkStoodOn(int levelId, int x, int y) => Mark(StoodOnByLevel, levelId, x, y);

    public bool IsStoodOn(int levelId, int x, int y) => IsIn(StoodOnByLevel, levelId, x, y);

    public IEnumerable<(int X, int Y)> StoodOnCells(int levelId) => Cells(StoodOnByLevel, levelId);

    private static void Mark(Dictionary<string, List<string>> byLevel, int levelId, int x, int y)
    {
        var key = levelId.ToString();
        if (!byLevel.TryGetValue(key, out var list))
        {
            list = new List<string>();
            byLevel[key] = list;
        }

        var cell = $"{x},{y}";
        if (!list.Contains(cell))
            list.Add(cell);
    }

    private static bool IsIn(Dictionary<string, List<string>> byLevel, int levelId, int x, int y) =>
        byLevel.TryGetValue(levelId.ToString(), out var list) && list.Contains($"{x},{y}");

    private static IEnumerable<(int X, int Y)> Cells(Dictionary<string, List<string>> byLevel, int levelId)
    {
        if (!byLevel.TryGetValue(levelId.ToString(), out var list))
            yield break;
        foreach (var cell in list)
        {
            var parts = cell.Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var x) &&
                int.TryParse(parts[1], out var y))
                yield return (x, y);
        }
    }

    public void NoteMonster(int id) => SeenMonsterIds.Add(id);

    public void Save()
    {
        var path = ClientSettings.QoLPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static QoLProgress LoadOrDefault()
    {
        try
        {
            if (File.Exists(ClientSettings.QoLPath))
            {
                var p = JsonSerializer.Deserialize<QoLProgress>(
                    File.ReadAllText(ClientSettings.QoLPath), JsonOptions);
                if (p is not null)
                {
                    p.SeenMonsterIds ??= new HashSet<int>();
                    p.VisitedByLevel ??= new();
                    p.StoodOnByLevel ??= new();
                    return p;
                }
            }
        }
        catch
        {
            // defaults
        }

        return new QoLProgress();
    }
}
