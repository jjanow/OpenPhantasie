namespace OpenPhantasie.Client;

/// <summary>
/// s154: I Amiga's waits on the map where its code blocks (static `[C]`, FORMAT.md "Sound effects" and "The combat
/// screen"), in the order the Amiga runs them:
/// <list type="bullet">
/// <item>The step tone first: h65 <c>+0x158</c> plays it (h89 <c>+0xd2</c>, the player blocks) when the map's input
/// routine returns any code 0–8, before h47 hands the code on, so the move (or its refusal: the OCEAN / STYX requester, the
/// dungeon wall's beep and box) comes after the tone's hold (<see cref="Defer"/>).</item>
/// <item>A question's sound before the question: 19 before "A TOWN.  ENTER?" / "A DUNGEON.  ENTER?" (h59 <c>+0x342</c>,
/// h41 <c>+0x320</c>, after the party moved), 27 before the BLACK KNIGHTS menu (h74 <c>+0x204</c>): the question stays
/// hidden for the sound's hold (<see cref="HoldPrompt"/>).</item>
/// <item>The monster pictures' disk read after FIGHT, the map still up (h53 <c>+0xd5e</c>; <see cref="Defer"/>).</item>
/// </list>
/// While a hold runs the map takes no input; a move pressed meanwhile is kept and made after it (the Amiga's port keeps the
/// event: a one-step sound can't be cut). No MonoGame types: tests drive it with seconds.
/// </summary>
public sealed class P1MapHolds
{
    private double _until = double.NegativeInfinity;
    private Action? _then;
    private object? _prompt;

    /// <summary>True while a hold runs or its action has not run yet.</summary>
    public bool Busy(double now) => now < _until || _then is not null;

    /// <summary>
    /// Runs <paramref name="run"/> after <paramref name="seconds"/> (at once for 0 or less: no wait with the Sound switch
    /// off, user s129). A deferral while another one is pending runs after it.
    /// </summary>
    public void Defer(double now, double seconds, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (Busy(now))
        {
            var before = _then;
            var from = Math.Max(now, _until);
            _then = () =>
            {
                before?.Invoke();
                Defer(from, seconds, run);
            };
            return;
        }
        if (seconds <= 0)
        {
            run();
            return;
        }
        _until = now + seconds;
        _then = run;
    }

    /// <summary>Hides <paramref name="prompt"/> (a question just opened) for <paramref name="seconds"/>, its sound's hold.</summary>
    public void HoldPrompt(double now, double seconds, object prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (seconds <= 0)
            return;
        _until = Math.Max(_until, now + seconds);
        _prompt = prompt;
    }

    /// <summary>True when <paramref name="prompt"/> is the question held back now (<paramref name="same"/> compares two
    /// questions; by reference without it).</summary>
    public bool PromptHeld(object? prompt, double now, Func<object, object?, bool>? same = null) =>
        prompt is not null && _prompt is not null && now < _until
        && (same?.Invoke(prompt, _prompt) ?? ReferenceEquals(prompt, _prompt));

    /// <summary>Once a frame: the deferred action whose hold is over runs (it may start a new hold).</summary>
    public void Update(double now)
    {
        if (_then is null || now < _until)
            return;
        var then = _then;
        _then = null;
        then();
    }

    /// <summary>Drops every hold and pending action (a load, the main menu).</summary>
    public void Reset()
    {
        _until = double.NegativeInfinity;
        _then = null;
        _prompt = null;
    }
}

/// <summary>
/// s154: I Amiga's monster-picture cache (h53 <c>+0xd5e</c>, static `[C]`): three slots (h54 <c>+2</c>, empty at boot, kept
/// for the program run). At a fight's start each group whose monster is in a slot uses it; a group with the same monster
/// as an earlier group of the fight shares its slot; any other is read from disk into the first slot this fight does not
/// use. No MonoGame types.
/// </summary>
public sealed class P1MonsterShapeCache
{
    private readonly int?[] _slots;

    public P1MonsterShapeCache(int slots) => _slots = new int?[Math.Max(0, slots)];

    /// <summary>The fight's groups' monster ids, in order: how many groups were read from disk (and the cache updated).</summary>
    public int Load(IReadOnlyList<int> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        if (_slots.Length == 0)
            return 0;
        var used = new bool[_slots.Length];
        var missing = new List<int>();
        foreach (var id in groups)
        {
            var slot = Array.IndexOf(_slots, id);
            if (slot >= 0)
                used[slot] = true;
            else if (!missing.Contains(id))
                missing.Add(id);
        }
        var reads = 0;
        foreach (var id in missing)
        {
            var free = Array.IndexOf(used, false);
            if (free < 0)
                break;
            _slots[free] = id;
            used[free] = true;
            reads++;
        }
        return reads;
    }
}

/// <summary>
/// s156: the moves pressed while one of I Amiga's map holds runs (<see cref="P1MapHolds"/>), in order. The Amiga's sound
/// player drains the window's port only before a sound starts (h89 <c>+0x304</c> → h77 <c>+0x3a6</c>); a one-step tone takes
/// one message after its commands, when the port was just emptied, so every key event of the hold stays queued and the
/// map's input routine (h65 <c>+0</c>) takes them one a call after it, each move getting its own tone (static `[C]`). A
/// key's release is no move, so a key pressed and released inside the hold still moves once. When the map stops waiting
/// (a question, a box, a dialog), the rest is dropped (the Amiga's requesters take and drop them `[~]`). A held key's
/// auto-repeat is not modelled (the remaster takes presses only; not seen live) `[~]`. No MonoGame types.
/// </summary>
public sealed class P1MoveQueue
{
    private readonly Queue<(int Dx, int Dy)> _moves = new();

    /// <summary>The moves waiting.</summary>
    public int Count => _moves.Count;

    /// <summary>A move pressed during a hold.</summary>
    public void Press((int Dx, int Dy) move) => _moves.Enqueue(move);

    /// <summary>
    /// After the hold: the next move to make now, or null; <paramref name="mapWaits"/> false (the map no longer waits for a
    /// move) drops them all.
    /// </summary>
    public (int Dx, int Dy)? Next(bool mapWaits)
    {
        if (!mapWaits)
        {
            _moves.Clear();
            return null;
        }
        return _moves.TryDequeue(out var move) ? move : null;
    }

    /// <summary>Drops every move (a load, the main menu).</summary>
    public void Clear() => _moves.Clear();
}
