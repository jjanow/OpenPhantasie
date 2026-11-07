namespace OpenPhantasie.Core;

/// <summary>
/// s152: the moments Phantasie IV sounds (<c>reverse-engineering/x68000/phantasie4/FORMAT.md</c> "Sound effects"): each
/// name stands for the call sites of P4.X's sound functions (<c>_sdbeep</c>, <c>_sdnoi</c>, <c>_sdwlk</c>,
/// <c>_sddam</c>, <c>_sdmag</c>, <c>_noise</c>) that the remaster has a counterpart for. Core only notes the moment
/// (<see cref="P4SoundLog"/>); which ADPCM effect plays (or none) is the pack's data (<c>sound_effects.moments</c> in
/// <c>phantasie4/graphics/x68000.json</c>), so another platform's pack can map the same moments.
/// </summary>
public static class P4Sounds
{
    /// <summary><c>_ewait</c>: the location map's "Space" bar waits (<c>_sdbeep</c>).</summary>
    public const string MessageWait = "message.wait";

    /// <summary><c>_xmenu</c>: an event's numbered menu, or a found trap's Disarm / Turn back (<c>_ctrap</c>
    /// <c>0x1AE8E</c>, <c>_dtrap</c> <c>0x1B742</c>), waits for its pick (<c>_sdbeep</c>).</summary>
    public const string EventMenu = "event.menu";

    /// <summary>Event ops INPUTS / INPUTN: the typed band opens (<c>_exe_ev</c> <c>0x196EE</c> / <c>0x197B4</c>,
    /// <c>_sdbeep</c>).</summary>
    public const string EventInput = "event.input";

    /// <summary>Event op 0x20 SOUND (<c>_exe_ev</c> <c>0x19C4A</c>: <c>_sdnoi</c>; its byte is ignored).</summary>
    public const string EventSound = "event.sound";

    /// <summary>Event op OPENGATE when a gate opened, after its three pictures (<c>_openg</c> <c>0x18D5E</c>, <c>_sdnoi</c>).</summary>
    public const string GateOpens = "dungeon.gate_opens";

    /// <summary>A location-map step onto a new square (<c>_dmove</c> <c>0x1ABDA</c>, <c>_sdwlk</c>).</summary>
    public const string DungeonStep = "dungeon.step";

    /// <summary>An outdoor step, on arrival at the square (<c>_omain</c> <c>0x15F1C</c>, <c>_sdwlk</c>).</summary>
    public const string OutdoorStep = "outdoor.step";

    /// <summary>An outdoor step <c>_ochkedge</c> refuses (rapids, the River of Death, the open sea; <c>_omove</c>
    /// <c>0x14AC0</c>, <c>_sdnoi</c>).</summary>
    public const string OutdoorBlocked = "outdoor.blocked";

    /// <summary>The shining sea: "a strange force destroyed the ship!!" after a YES (<c>_obarrier</c> <c>0x14022</c>,
    /// <c>_sdnoi</c>).</summary>
    public const string ShipDestroyed = "outdoor.ship_destroyed";

    /// <summary>"The ship ran aground!" (<c>_save_ship</c> <c>0x14C8E</c>, <c>_sdnoi</c>).</summary>
    public const string RanAground = "outdoor.ran_aground";

    /// <summary>An outdoor fight's key wait before the field (<c>_oencgo</c> <c>0x165F2</c> with <c>_einf3</c>,
    /// <c>_sdbeep</c>).</summary>
    public const string FightWait = "outdoor.fight_wait";

    /// <summary>Damage to a member: in a fight, by a trap, an event or the water (<c>_pdamage</c>, <c>_ddamage</c>,
    /// <c>_sddam</c>).</summary>
    public const string MemberDamaged = "member.damaged";

    /// <summary>Damage to a monster (<c>_mdamage</c>, <c>_sddam</c>).</summary>
    public const string MonsterDamaged = "monster.damaged";

    /// <summary>The damage kills the member (or leaves him Out): <c>_pdamage</c>'s second <c>_sddam</c> (<c>0x1EB4A</c>,
    /// HP ≤ d or condition 0), after the first.</summary>
    public const string MemberKilled = "member.killed";

    /// <summary>The damage kills the monster: <c>_mdamage</c>'s second <c>_sddam</c> (<c>0x21CEE</c>, HP ≤ d or
    /// <c>_mc</c> ≤ 0), after the first.</summary>
    public const string MonsterKilled = "monster.killed";

    /// <summary>A member casts in a fight, after the spell's name (<c>_chrexe</c> <c>0x28EA4</c>, <c>_sdmag</c>).</summary>
    public const string MemberCasts = "combat.member_casts";

    /// <summary>A monster casts, after the spell's name (<c>_monexe</c> <c>0x256AC</c>, <c>_sdmag</c>).</summary>
    public const string MonsterCasts = "combat.monster_casts";

    /// <summary>A combatant that does not fly takes a step on the field (<c>_b_face</c> <c>0x22E74</c>, <c>_noise</c>).</summary>
    public const string CombatStep = "combat.step";

    /// <summary>Sink: a flier lands (<c>_pmagic1</c> <c>0x2C490</c> / <c>_mmagic</c> <c>0x29E6C</c>, <c>_noise</c>).</summary>
    public const string Lands = "combat.lands";

    /// <summary>A spell cast outside a fight (<c>_pmagic3</c> <c>0x2D892</c>, <c>_sdmag</c>).</summary>
    public const string SpellCast = "spell.cast";

    /// <summary>Identify's appraisal window opens (<c>_ident_mi</c> <c>0x2DBBC</c>, <c>_sdmag</c>).</summary>
    public const string Identify = "spell.identify";

    /// <summary>A potion or magic item takes effect (<c>_use_hp</c> / <c>_use_mp</c> / <c>_use_op</c> / <c>_use_mi</c>,
    /// <c>_sdmag</c>).</summary>
    public const string ItemUsed = "item.used";

    /// <summary>Ancestor 2 by S / D: each race that combines is drawn with a click while the roll picks one (<c>_mopta</c>
    /// <c>0x13ACC</c>, <c>_noise</c>; <see cref="P4SecondAncestorResult.RacesShown"/>).</summary>
    public const string AncestorCandidate = "creation.ancestor_candidate";

    /// <summary>"The party has been wiped out.": before its key wait (<c>_deadmsg</c> <c>0x1E484</c>, <c>_sdbeep</c>).</summary>
    public const string WipedOut = "party.wiped_out";

    /// <summary>An ending page waits for SPACE (<c>_endmsg</c> <c>0xA922</c>, <c>_common_proc</c> <c>0xAFFA</c>,
    /// <c>_sdbeep</c>).</summary>
    public const string EndingPage = "ending.page";

    /// <summary>The final screen's save question (<c>_endstory</c> <c>0xABFC</c>, before <c>_confirm</c>) and its answer
    /// (<c>0xAC1A</c>, after a YES's save or a NO; <see cref="P4TownLoop.AnswerEnding"/>), each <c>_sdbeep</c>.</summary>
    public const string EndingFinal = "ending.final";
}

/// <summary>
/// s152: the sound moments (<see cref="P4Sounds"/>) a Phantasie IV game noted, for the front end: a sequence that only
/// grows, and the last 32 moments. Not saved.
/// </summary>
public sealed class P4SoundLog
{
    private const int Kept = 32;
    private readonly List<string> _moments = [];

    /// <summary>How many moments were ever noted.</summary>
    public long Sequence { get; private set; }

    /// <summary>Note that <paramref name="moment"/> happened.</summary>
    public void Note(string moment)
    {
        ArgumentException.ThrowIfNullOrEmpty(moment);
        Sequence++;
        _moments.Add(moment);
        if (_moments.Count > Kept)
            _moments.RemoveRange(0, _moments.Count - Kept);
    }

    /// <summary>The moments noted after <paramref name="sequence"/> (an earlier <see cref="Sequence"/>), oldest first.</summary>
    public IReadOnlyList<string> Since(long sequence)
    {
        var count = (int)Math.Clamp(Sequence - sequence, 0, _moments.Count);
        return _moments.GetRange(_moments.Count - count, count);
    }
}
