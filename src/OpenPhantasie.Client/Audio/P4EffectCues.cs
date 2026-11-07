using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Audio;

/// <summary>
/// s152: Phantasie IV's sound moments (Core's <c>P4Sounds</c>, noted in <c>P4Party.Sounds</c>) to the pack's ADPCM effects
/// (<see cref="X68kAdpcmEffects.Moments"/>). A moment with several effects takes them in turn, one per note, whether or not
/// sound is on (<c>_sdwlk</c> toggles <c>_wlkflg</c> before <c>_voice</c> tests the switch); moments with the same list share
/// one turn, as <c>_omain</c> and <c>_dmove</c> share <c>_wlkflg</c>. Effects started while one
/// plays wait for it (taken as <c>_ADPCMOUT</c> waiting for the running output `[~]`); at most <see cref="MaxWaiting"/> wait,
/// since the remaster resolves a fight's round at once where the original spaces its sounds by its animations `[~]`.
/// </summary>
public sealed class P4EffectCues
{
    public const int MaxWaiting = 4;

    private readonly Dictionary<string, int> _turns = new(StringComparer.Ordinal);
    private readonly List<int> _waiting = [];

    /// <summary>The effects waiting for the running one.</summary>
    public IReadOnlyList<int> Waiting => _waiting;

    /// <summary>The effects <paramref name="moments"/> play, in order (a silent moment adds none); each note advances its
    /// list's turn.</summary>
    public IReadOnlyList<int> EffectsFor(X68kAdpcmEffects pack, IEnumerable<string> moments)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(moments);
        var effects = new List<int>();
        foreach (var moment in moments)
        {
            var list = pack.EffectsFor(moment);
            if (list.Count == 0)
                continue;
            var key = string.Join(',', list);
            _turns.TryGetValue(key, out var turn);
            effects.Add(list[turn % list.Count]);
            _turns[key] = (turn + 1) % list.Count;
        }
        return effects;
    }

    /// <summary>Queue <paramref name="effects"/> behind the running ones (the oldest beyond <see cref="MaxWaiting"/> are dropped).</summary>
    public void Enqueue(IEnumerable<int> effects)
    {
        _waiting.AddRange(effects);
        if (_waiting.Count > MaxWaiting)
            _waiting.RemoveRange(0, _waiting.Count - MaxWaiting);
    }

    /// <summary>The waiting effects, now started (the queue empties).</summary>
    public IReadOnlyList<int> TakeWaiting()
    {
        var all = _waiting.ToList();
        _waiting.Clear();
        return all;
    }

    /// <summary>
    /// A new game or a load: nothing waiting. The turns go on (s156): <c>_wlkflg</c> (<c>0x4A35C</c>) is 0 only at program
    /// start; neither <c>_load</c> nor a new game sets it, so the first step after a load takes the turn the last step
    /// left, as in P4.X run without a restart.
    /// </summary>
    public void Reset() => _waiting.Clear();
}
