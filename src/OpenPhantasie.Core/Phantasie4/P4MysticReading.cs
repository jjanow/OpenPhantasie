namespace OpenPhantasie.Core;

/// <summary>Where the Mystic's visit is (<see cref="P4MysticReading"/>).</summary>
public enum P4MysticPhase
{
    /// <summary>The first counted loop: 10 000 bare <c>_anima</c> calls, records 15–18 (no bubble, no text).</summary>
    Gazing,
    /// <summary>Record 19 (the crystal ball) armed too: 3000 more calls.</summary>
    Reading,
    /// <summary><c>MYSTLK</c>'s speech bubble over the picture with the score and verdict in it: 3000 more calls, 15–19.</summary>
    Speaking,
    /// <summary><c>_clanim</c> + records 15–18 again, then <c>_spc_wait</c>: the input loop, a key closes the Mystic.</summary>
    Waiting,
}

/// <summary>
/// IV's Mystic visit as P4.X plays it (<c>_mystic</c> <c>0x6BF0</c>, <c>0x6F78</c>; <c>FORMAT.md</c> §6.10, towns.md §7) [C]:
/// <c>MYSAN</c> to page 1, <c>MYSTIC</c> at (160, 90), records 15–18 armed; 10 000 bare <c>_anima</c> calls; record 19 armed
/// (<c>0x6C2A</c>) and 3000 more; <c>MYSTLK</c> unpacked over the picture's top, the score and verdict printed in it; 3000
/// more (record 19 still armed); <c>_clanim</c>, records 15–18 armed again (record 19's last frame stays on the picture) and
/// <c>_spc_wait</c>. The three counted loops read no keys (the remaster's MYSTIC SKIP switch, off by default, lets a key
/// end them at once: <see cref="SkipLoops"/>). A bare call took 1.359 ms on average in XEiJ (s118; the pack's
/// <c>p4.mystic.reading</c> <c>pass_ms</c>): 13.3 + 4.2 + 4.2 s measured; the wait runs at the input loop's 6.12 ms.
/// Headless: the Client ticks it and draws what <see cref="Player"/> shows.
/// </summary>
public sealed class P4MysticReading
{
    /// <summary>The counted loops' lengths: <c>0x6C02</c>, <c>0x6C48</c>, <c>0x6F24</c> [C].</summary>
    public const int GazePasses = 10000, ReadPasses = 3000, SpeakPasses = 3000;

    /// <summary>The Mystic picture's records (<c>_setanim</c> at <c>0x7074</c> / <c>0x6F52</c>) and the reading's (<c>0x6C2A</c>) [C].</summary>
    public const int First = 15, Last = 18, ReadingRecord = 19;

    private readonly double _barePass, _waitPass;
    private double _carry;
    private int _left;

    /// <summary>
    /// Starts the visit on <paramref name="player"/> with the picture <paramref name="scene"/> (a new <c>_setanim</c> 15–18);
    /// <paramref name="barePassMilliseconds"/> is a bare <c>_anima</c> call, <paramref name="waitPassMilliseconds"/> a pass of
    /// <c>_spc_wait</c>'s input loop.
    /// </summary>
    public P4MysticReading(PictureAnimationPlayer player, string scene, double barePassMilliseconds, double waitPassMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!(barePassMilliseconds > 0))
            throw new ArgumentOutOfRangeException(nameof(barePassMilliseconds));
        if (!(waitPassMilliseconds > 0))
            throw new ArgumentOutOfRangeException(nameof(waitPassMilliseconds));
        Player = player;
        Scene = scene;
        _barePass = barePassMilliseconds;
        _waitPass = waitPassMilliseconds;
        player.Hide(); // the Mystic is always a new picture (_clrtdraw, _xunpack)
        player.Show(scene, First, Last, barePassMilliseconds);
        _left = GazePasses;
    }

    public PictureAnimationPlayer Player { get; }

    /// <summary>The scene key the visit runs on.</summary>
    public string Scene { get; }

    public P4MysticPhase Phase { get; private set; } = P4MysticPhase.Gazing;

    /// <summary>The speech bubble and its text are up (from the third loop on).</summary>
    public bool BubbleUp => Phase >= P4MysticPhase.Speaking;

    /// <summary>Keys are read only in <c>_spc_wait</c>; the counted loops ignore them.</summary>
    public bool AcceptsKeys => Phase == P4MysticPhase.Waiting;

    /// <summary>Bare calls left in the current counted loop (0 while waiting).</summary>
    public int PassesLeft => Phase == P4MysticPhase.Waiting ? 0 : _left;

    /// <summary><paramref name="milliseconds"/> of real time: the counted loops at the bare pass, then the wait's input loop.</summary>
    public void Advance(double milliseconds)
    {
        if (!(milliseconds > 0))
            return;
        if (Phase == P4MysticPhase.Waiting)
        {
            Player.Advance(milliseconds);
            return;
        }
        _carry += milliseconds;
        while (Phase != P4MysticPhase.Waiting)
        {
            var passes = (int)Math.Min(Math.Min(Math.Floor(_carry / _barePass), _left), PictureAnimationPlayer.MaxPassesPerAdvance);
            if (passes <= 0)
                return;
            Player.Run(passes);
            _carry -= passes * _barePass;
            _left -= passes;
            if (_left == 0)
                Step();
        }
        // Time left after the last counted loop goes to the wait.
        if (_carry > 0)
            Player.Advance(_carry);
        _carry = 0;
    }

    /// <summary>
    /// QoL (presentation switch MYSTIC SKIP, off = P4.X: the loops read no keys): runs every pass the counted loops still
    /// have at once, through the same <see cref="PictureAnimationPlayer.Run"/> and phase steps as <see cref="Advance"/>,
    /// so the picture ends where the waited-out reading ends (record 19's last frame up, records 15–18 re-armed) and the
    /// visit is in <see cref="P4MysticPhase.Waiting"/>. Only the wait is skipped: the score and verdict are the town
    /// loop's (<c>W.Box</c>), set before the reading starts. True when there was anything left to skip.
    /// </summary>
    public bool SkipLoops()
    {
        if (Phase == P4MysticPhase.Waiting)
            return false;
        while (Phase != P4MysticPhase.Waiting)
        {
            while (_left > 0)
            {
                var passes = Math.Min(_left, PictureAnimationPlayer.MaxPassesPerAdvance);
                Player.Run(passes);
                _left -= passes;
            }
            Step();
        }
        _carry = 0;
        return true;
    }

    private void Step()
    {
        switch (Phase)
        {
            case P4MysticPhase.Gazing:
                Player.Arm(ReadingRecord, ReadingRecord, _barePass);
                Phase = P4MysticPhase.Reading;
                _left = ReadPasses;
                break;
            case P4MysticPhase.Reading:
                Phase = P4MysticPhase.Speaking;
                _left = SpeakPasses;
                break;
            case P4MysticPhase.Speaking:
                Player.Arm(First, Last, _waitPass, clearFirst: true);
                Phase = P4MysticPhase.Waiting;
                _left = 0;
                break;
        }
    }
}
