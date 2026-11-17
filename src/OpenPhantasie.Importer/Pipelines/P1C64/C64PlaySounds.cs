using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// s228: the C64 packs' sounds in play (I's <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 9.8, II's and III's
/// GRAPHICS.md): the fights' SID routines of I's <c>OUTDOORS</c> / <c>DUNGEON</c> and II's <c>OUTDOORS2</c> /
/// <c>DUNGEON2</c> (compiled BASIC, read in the bytecode `[C]`), I's ring tune (<c>TOWNR</c>) and III's <c>SND</c> (6502)
/// as the register writes they make, in order. Times: the BASIC <c>TI</c> waits as written; the statements and the
/// <c>FOR</c> loops at the times VICE measured (s228, PAL, II's routines forced on its fight screen: every routine's
/// writes dumped with their clocks); I's routines are the same bytecode on the same runtime (<c>RTL-64</c>), so they take
/// II's times `[~]`. Numbers only (the programs' constants, their DATA notes and <c>MN</c>'s field 13): they ship.
/// </summary>
public static class C64PlaySounds
{
    /// <summary>The PAL clock (I's GRAPHICS.md 9.7).</summary>
    private const double Clock = 985_248;

    /// <summary>A <c>POKE</c> of a variable and a constant: 670–760 cycles (VICE).</summary>
    private const double P = 0.0007;

    /// <summary>A <c>POKE</c> whose address is an expression (<c>S + n</c>): 1,700–2,100 cycles (VICE).</summary>
    private const double Q = 0.0019;

    /// <summary>An empty <c>FOR</c> / <c>NEXT</c> pass on the fight screen: AWAKENS's 71 passes 0.0673 s (VICE).</summary>
    private const double EmptyPass = 0.00093;

    /// <summary>The KERNAL's jiffy (CIA 1 timer A), 60 Hz on PAL and NTSC: <c>TI</c> waits count these.</summary>
    private const double Jiffy = 1 / 60.0;

    /// <summary>
    /// <c>SU(0…10)</c>, the AWAKENS notes (the DATA line of <c>OUTDOORS</c> / <c>DUNGEON</c>, I and II the same, read by
    /// <c>FOR I = 0 TO 10: READ SU(I)</c>): C6 E6 G6 C7 D6 F6 A6 B6 D7 E7 F7 in NTSC SID units. The tune draws
    /// <c>SU(INT(RND · 10))</c>, so F7 never sounds.
    /// </summary>
    public static readonly int[] AwakeNotes = [17167, 21629, 25721, 34334, 19269, 22915, 28871, 32407, 38539, 43258, 45830];

    /// <summary>
    /// <c>MZ%(0…8)</c>, I's ring tune (<c>TOWNR</c>'s DATA line, <c>FOR I = 0 TO 8: READ MZ%(I)</c>), −1 ends it. II's
    /// <c>TOWN2</c> holds the same routine and table, but no statement calls it.
    /// </summary>
    public static readonly int[] RingNotes = [4050, 4291, 3215, 2145, 2145, 2408, 2703, 2861];

    /// <summary>
    /// <c>MN</c> field 13 (<c>C%</c>, sprite blocks − 1) of I's monsters 0–79 (v1.4): the marks a damage spell draws on a
    /// monster of that kind, one sound each (<c>FOR II = 0 TO C%(WG) − 1</c>).
    /// </summary>
    public static readonly int[] MarksP1 =
    [
        1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 1, 1, 1, 1, 1, 2, 2, 1, 1,
        1, 3, 2, 1, 1, 4, 1, 1, 2, 3, 1, 1, 4, 4, 2, 1, 1, 1, 2, 1, 2, 2, 1, 1, 2, 2, 4, 4, 4, 2, 2, 2, 2, 3, 4, 2, 4, 1, 1, 2,
    ];

    /// <summary><c>MN</c> field 13 of II's monsters 0–79 (v1.2).</summary>
    public static readonly int[] MarksP2 =
    [
        1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 1, 1, 1, 1, 1, 2, 2, 1, 1,
        1, 3, 2, 1, 1, 1, 1, 1, 2, 3, 1, 1, 2, 4, 2, 1, 1, 1, 2, 1, 2, 2, 1, 1, 2, 2, 4, 4, 4, 2, 2, 2, 3, 1, 1, 4, 2, 4, 4, 4,
    ];

    /// <summary>A sound's steps in time order: each step lands <c>dt</c> after the last write before it.</summary>
    private sealed class Seq
    {
        public readonly JsonArray Steps = new();
        private double _t;
        private JsonObject? _wait;

        private Seq Add(double dt, string kind, JsonNode body, double span = 0, params (string Key, JsonNode? Value)[] extra)
        {
            _t += dt;
            var step = J.O(("at", Math.Round(_t, 6)), (kind, body));
            foreach (var (k, v) in extra)
                step[k] = v;
            if (_wait is not null)
            {
                step["rnd_wait"] = _wait;
                _wait = null;
            }
            Steps.Add(step);
            _t += span;
            return this;
        }

        /// <summary>Pokes <paramref name="every"/> seconds apart, the list run <paramref name="repeat"/> times.</summary>
        public Seq Pokes(double dt, double every, int repeat, params JsonNode[] pokes)
        {
            var extra = new List<(string, JsonNode?)>();
            if (every > 0)
                extra.Add(("every", Math.Round(every, 6)));
            if (repeat > 1)
                extra.Add(("repeat", repeat));
            return Add(dt, "poke", J.A(pokes), (pokes.Length * repeat - 1) * every, [.. extra]);
        }

        public Seq Poke(double dt, int register, int value) => Pokes(dt, 0, 1, Pk(register, value));

        /// <summary>A poke of <c>base + INT(RND · span)</c>.</summary>
        public Seq Rnd(double dt, int register, int @base, int span, double every = 0, int repeat = 1) =>
            Pokes(dt, every, repeat, J.A([JsonValue.Create(register), J.O(("rnd", J.Ints([@base, span])))]));

        public Seq Sweep(double dt, int register, int from, int to, int by, double every, int div = 1)
        {
            var sweep = J.O(("regs", J.Ints([register])), ("from", from), ("to", to), ("by", by), ("every", Math.Round(every, 6)));
            if (div > 1)
                sweep["div"] = div;
            return Add(dt, "sweep", sweep, ((to - from) / by) * every);
        }

        public Seq Pick(double dt, int high, int low, int[] values, int draw) =>
            Add(dt, "pick", J.O(("regs", J.Ints([high, low])), ("values", J.Ints(values)), ("draw", draw)));

        /// <summary>The next step waits <c>INT(base + RND · span)</c> empty passes first.</summary>
        public Seq RndWait(int @base, int span)
        {
            _wait = J.O(("base", @base), ("span", span), ("every", EmptyPass));
            return this;
        }

        /// <summary>The time of the last write so far.</summary>
        public double T => _t;

        /// <summary>A step with no writes: the sound's end after a closing wait (its length for cues in turn).</summary>
        public Seq End(double dt) => Add(dt, "poke", new JsonArray());
    }

    private static JsonNode Pk(int register, int value) => J.Ints([register, value]);

    private static JsonNode[] Zeros(int first, int last) =>
        [.. Enumerable.Range(first, last - first + 1).Select(r => Pk(r, 0))];

    /// <summary>
    /// The fight sounds of one game and terrain (<paramref name="dungeon"/>: <c>DUNGEON</c> / <c>DUNGEON2</c>, else
    /// <c>OUTDOORS</c> / <c>OUTDOORS2</c>), keyed <c>fight.{terrain}.{sound}</c>. Where the programs differ:
    /// <list type="bullet">
    /// <item>a member's death holds its gate 3 × 50 jiffies in I's <c>OUTDOORS</c>, 3 × 70 in II's, 3 × 61 in both
    /// dungeons;</item>
    /// <item>IS ALIVE holds <c>250 + RND · 300</c> passes outdoors, <c>· 400</c> in the dungeons;</item>
    /// <item>the cast's last sweep writes the loop's value in the dungeons, 0 outdoors (<c>POKE D3, 0</c>);</item>
    /// <item>I's <c>DUNGEON</c> clears <c>S + M</c> in place of <c>S + I</c> before the cast (<c>M</c> unknown there,
    /// so that clear is left out `[~]`); II's <c>DUNGEON2</c> clears all 25;</item>
    /// <item>the monster's death (and the encounter's start) ends on 101 empty passes outdoors, 201 in the dungeons;</item>
    /// <item>a damage spell's mark: outdoors the hit's noise for 16 jiffies, in the dungeons noise left on after
    /// 16 jiffies.</item>
    /// </list>
    /// </summary>
    public static void AddFight(JsonObject sounds, bool p2, bool dungeon)
    {
        var key = "fight." + (dungeon ? "dungeon" : "outdoors") + ".";

        // OUTDOORS 0x66fc / DUNGEON 0x669e / OUTDOORS2 0x6c48 / DUNGEON2 0x6c0f: a member CASTS A SPELL.
        var cast = new Seq();
        var t0 = 0.0;
        if (p2 || !dungeon)
        {
            cast.Pokes(0, 0.0024, 1, Zeros(0, 24));
            t0 = 0.003;
        }
        cast.Poke(t0, 20, 1).Poke(0.0018, 17, 8).Poke(0.0033, 5, 4).Poke(P, 6, 196);
        for (var o = 1; o <= 15; o++)
            cast.Poke(o == 1 ? 0.0029 : 0.0021, 24, o).Poke(P, 4, 17).Rnd(0.013, 1, 50, 6 * o, 0.0119, 6).Poke(0.0016, 4, 16);
        cast.Poke(0.0016, 5, 9).Poke(P, 6, 9).Poke(P, 4, 129);
        if (dungeon)
            cast.Sweep(0.0033, 1, 42, 10, -1, 0.00217);
        else
            cast.Pokes(0.0029, 0.00176, 33, Pk(1, 0));
        cast.Poke(0.0017, 4, 128).Poke(P, 24, 0).Pokes(0.0018, 0.00185, 1, Zeros(0, 24));
        if (dungeon)
            cast.Poke(0.0019, 24, 0);
        sounds[key + "cast"] = cast.Steps;

        // OUTDOORS 0x6845 / DUNGEON 0x6766: a monster CASTS A SPELL or BREATHES (DUNGEON also: the scripts' SMOKE FILLS
        // THE AIR, A SPELL IS CAST, THE PARTY IS MOVED).
        sounds[key + "monster_cast"] = new Seq().Poke(0, 5, 204).Poke(P, 6, 11).Poke(P, 24, 15).Poke(P, 0, 0).Poke(P, 4, 129)
            .Sweep(0.0032, 1, 37, 12, -1, 0.04266).Poke(0.0427, 4, 128).Poke(P, 24, 0).Steps;

        // OUTDOORS 0x6889 / DUNGEON 0x67aa: a member AWAKENS: FOR O = 0 TO 10, eleven notes SU(INT(RND * 10)), each 71
        // empty passes long.
        var awake = new Seq().Poke(0, 5, 5).Poke(P, 6, 69).Poke(P, 24, 15);
        for (var n = 0; n <= 10; n++)
            awake.Pick(0.0192, 1, 0, AwakeNotes, 10).Poke(0.0017, 4, 17).Poke(0.0679, 4, 16);
        sounds[key + "awakens"] = awake.Poke(0.0016, 24, 0).Steps;

        // OUTDOORS 0x68f7 / DUNGEON 0x6818: IS ALIVE (a Resurrection); 0x6964 / 0x6885: MISSES (the same body from 0x6916).
        sounds[key + "alive"] = new Seq().Poke(0, 15, 67).Poke(0.0015, 5, 9).Poke(0.0013, 6, 9).Poke(0.0008, 24, 15)
            .Rnd(0.0097, 1, 1, 3).Poke(0.0022, 4, 21).RndWait(250, dungeon ? 400 : 300).Poke(0.001, 4, 20).Poke(0.001, 24, 0).Steps;
        sounds[key + "miss"] = new Seq().Poke(0, 5, 6).Poke(0.0013, 6, 13).Poke(0.0008, 24, 15)
            .Rnd(0.0092, 1, 240, 14).Poke(0.0024, 4, 129).RndWait(250, dungeon ? 400 : 300).Poke(0.001, 4, 128).Poke(P, 24, 0).Steps;

        // OUTDOORS 0x697f / DUNGEON 0x68a0: a member HITS (and the swimmer who TAKES DAMAGE): noise, 40 jiffies.
        sounds[key + "hit"] = HitBody(new Seq(), 40).Steps;

        // OUTDOORS 0x69c5 / DUNGEON 0x68e6: the encounter's start and a monster's DIES: two saws crossing.
        var enc = new Seq().Poke(0, 5, 9).Poke(P, 6, 153).Poke(P, 24, 15).Poke(0.0018, 12, 153).Poke(0.0021, 12, 153)
            .Poke(P, 4, 33).Poke(0.0019, 11, 33);
        for (var o = 5; o <= 15; o++)
            enc.Poke(o == 5 ? 0.0028 : 0.0344, 1, o).Poke(0.0031, 8, 25 - o);
        sounds[key + "encounter"] = enc.Poke(0.0338, 4, 32).Poke(0.0019, 11, 32).Poke(0.0019, 11, 0).Poke(0.0018, 18, 0)
            .Poke(dungeon ? 0.202 : 0.1015, 24, 0).Steps;

        // OUTDOORS 0x6a40 / DUNGEON 0x6961: a member damaged by a monster (its swing, spell or breath; 0x2e11 after its
        // TAKES n DAMAGE) who lives: 200 gate-on pokes.
        sounds[key + "wound"] = new Seq().Poke(0, 1, 6).Poke(0.0008, 0, 6).Poke(P, 5, 9).Poke(P, 6, 15).Poke(0.0008, 1, 7)
            .Poke(Q, 15, 30).Poke(0.0017, 24, 15).Pokes(0.0023, 0.00172, 200, Pk(4, 33)).Poke(0.0017, 4, 32).Poke(P, 24, 0).Steps;

        // OUTDOORS 0x6a81 / DUNGEON 0x69a2: a monster HITS: noise and a saw, nine RND passes, the volume 15 TO 0 STEP -.1.
        sounds[key + "monster_hit"] = new Seq().Poke(0, 5, 10).Poke(P, 6, 42).Poke(P, 1, 1).Poke(0.0008, 0, 12).Poke(Q, 12, 10)
            .Poke(Q, 13, 42).Poke(0.0018, 8, 2).Poke(0.0021, 7, 90).Poke(0.0008, 4, 129).Poke(Q, 11, 33)
            .Pokes(0.0096, 0.0103, 9, Pk(24, 15)).Poke(0.0026, 4, 128).Poke(Q, 11, 32).Sweep(0.003, 24, 150, 0, -1, 0.00233, 10)
            .Steps;

        // OUTDOORS 0x6752 / DUNGEON 0x6a91 (OUTDOORS2 0x6c9e, DUNGEON2 0x7002): a member DIES (called 3 times in a fight,
        // once at the swim's death): triangle ring-modulated, the gate on through 3 waits.
        var wait = dungeon ? 61 : p2 ? 70 : 50;
        sounds[key + "dies"] = new Seq().Poke(0, 1, 3).Poke(Q, 5, 10).Poke(Q, 6, 2).Poke(0.0021, 15, 4).Poke(0.0017, 24, 15)
            .Poke(0.0018, 23, 240).Poke(0.0016, 22, 31).Pokes(0.0032, wait * Jiffy, 3, Pk(4, 21)).Poke(wait * Jiffy, 4, 20).Steps;

        // OUTDOORS 0x6792 / DUNGEON 0x6a5a: one mark of a damage spell on a monster (II: also the tossed rock's hit).
        sounds[key + "mark"] = dungeon
            ? new Seq().Pokes(0, 0.00245, 1, Zeros(0, 24)).Poke(0.0016, 0, 151).Poke(0.0021, 1, 126).Poke(Q, 5, 57)
                .Poke(0.002, 24, 15).Poke(Q, 4, 129).End(16 * Jiffy).Steps
            : HitBody(new Seq(), 16).Steps;
    }

    /// <summary>The hit's body (OUTDOORS 0x697f): noise through voice 3's ring, <paramref name="jiffies"/> on.</summary>
    private static Seq HitBody(Seq s, int jiffies) => s.Poke(0, 1, 4).Poke(0.001, 5, 9).Poke(Q, 15, 30).Poke(0.0017, 24, 31)
        .Poke(0.0018, 23, 0).Poke(0.0017, 21, 1).Poke(P, 4, 133).Poke(jiffies * Jiffy, 4, 20);

    /// <summary>
    /// s229: the key click `[C]`, which every program's input routine plays for each key it GETs and each stick move it
    /// reads (I's GRAPHICS.md 9.8). Three bodies, the same in I and II: <c>key_click.outdoors</c> (<c>OUTDOORS</c>
    /// <c>0x6198</c>, <c>OUTDOORS2</c> <c>0x6707</c>), <c>key_click.dungeon</c> (<c>DUNGEON</c> <c>0x6a1a</c>,
    /// <c>DUNGEON2</c> <c>0x6f8b</c>) and <c>key_click.town</c> (<c>TOWNR</c> <c>0x510d</c>, <c>TOWN2</c> <c>0x55f3</c>): volume 10,
    /// triangle gate, then the envelope (outdoors / dungeon AD 20 SR 9, the town AD 9 and no SR), frequency $1464,
    /// <c>TI$ = "000000"</c> and 3 jiffies, gate off, AD 9; <c>OUTDOORS</c> also sets the volume to 4 after it (the
    /// dungeon and the town leave 10). Statement times as the fights' (VICE) `[~]`.
    /// </summary>
    public static void AddClicks(JsonObject sounds)
    {
        Seq Body(Seq s, bool town)
        {
            s.Poke(0, 24, 10).Poke(town ? Q : P, 4, 17);
            if (town)
                s.Poke(Q, 5, 9);
            else
                s.Poke(Q, 5, 20).Poke(Q, 6, 9);
            return s.Poke(town ? Q : P, 1, 20).Poke(town ? Q : P, 0, 100).Poke(3 * Jiffy + 0.002, 4, 16).Poke(town ? Q : P, 5, 9);
        }
        sounds["key_click.outdoors"] = Body(new Seq(), false).Poke(P, 24, 4).Steps;
        sounds["key_click.dungeon"] = Body(new Seq(), false).Steps;
        sounds["key_click.town"] = Body(new Seq(), true).Steps;
    }

    /// <summary>
    /// I's <c>TOWNR</c> <c>0x503f</c> `[C]`: U)SE of an item 141–150 (RING A–I, WAND) plays this and returns to the menu.
    /// The 25 registers cleared, then six passes I = 1…6 of the eight notes: A = MZ% / 2 · I, L% = INT(A / 256) to voice
    /// 1's frequency high byte, H% = A − 256 · L% to voice 3's (the ring), a note every 2 jiffies (<c>TI</c>); the
    /// volume off 33 jiffies after the last. The clear loop's pass is DUNGEON2's (VICE) `[~]`.
    /// </summary>
    public static void AddRing(JsonObject sounds)
    {
        var s = new Seq().Pokes(0, 0.00185, 1, Zeros(0, 24));
        var first = -1.0;
        var notes = 0;
        for (var i = 1; i <= 6; i++)
        {
            s.Poke(i == 1 ? Q : 0.0005, 24, 15).Poke(Q, 5, 0).Poke(Q, 6, 240).Poke(Q, 4, 21).Poke(Q, 3, 1);
            foreach (var mz in RingNotes)
            {
                var a = mz / 2.0 * i;
                var l = (int)Math.Floor(a / 256);
                var h = (int)Math.Floor(a - 256 * l);
                // The first note at once (TM not set yet), each later one 2 jiffies after the one before (TM := TI).
                var at = first < 0 ? s.T + 0.0005 : first + notes * 2 * Jiffy;
                if (first < 0)
                    first = at;
                s.Poke(at - s.T, 1, l).Poke(0.0005, 15, h).Poke(Q, 4, 21).Poke(Q, 3, 1);
                notes++;
            }
        }
        sounds["town.ring"] = s.Poke(first + (notes - 1) * 2 * Jiffy + 33 * Jiffy - s.T, 24, 0).Steps;
    }

    /// <summary>
    /// III's <c>SND</c> (6502 at <c>$126E</c>, v1.0) as <c>PHANT.OBJ</c>'s fights call it `[C]`; the writes at their CPU
    /// cycles (<c>STA abs</c> 4, <c>LDA</c> 2 / 4). <c>entry0</c> (<c>$127D</c>): voice 1's frequency high byte, the
    /// control's off value, the low byte, pulse $8080, AD 0, SR, the gate on and off 6 cycles later.
    /// Each starts with the volume PHANT.OBJ sets at boot ($D418 = 15, <c>$213A</c>).
    /// <list type="bullet">
    /// <item><c>fight.miss</c>: entry1 (<c>$12A8</c>), a member's or a monster's miss: noise $8000 (low byte as left), AD
    /// $71, SR 0, the gate left on.</item>
    /// <item><c>fight.hit</c>: entry2 (<c>$12C2</c>), any damaging hit: entry0 as noise ($81 / $80) with A = $42 (the
    /// last byte of "AND HITS"'s address, the print routine's A), then 10 jiffies.</item>
    /// <item><c>fight.ping</c>: entry0 three times, A = $90, $B0, $90, 13 frames apart: a party attack spell (FIREFLASH,
    /// MINDBLAST, FLAMEBOLT) on each monster it strikes, before its hit.</item>
    /// <item><c>fight.death</c>: entry4 (<c>$12E2</c>), a member's --DEATH--: entry0 at $04B4 with SR $FA, the
    /// <c>$1310</c> delay twice (A = $FF: 1,791 cycles a pass), entry0 at $0430.</item>
    /// </list>
    /// </summary>
    public static void AddP3(JsonObject sounds)
    {
        // PHANT.OBJ $213A ($113A at run time) sets the volume to 15 once at boot; no fight sound writes it, so each sound
        // here starts from that state (a fresh chip per score in the Client).
        static JsonArray Boot(JsonArray steps)
        {
            steps.Insert(0, J.O(("at", 0), ("poke", J.A([Pk(24, 15)]))));
            return steps;
        }

        sounds["fight.miss"] = Boot(Cycles(0, [(0, 4, 0x80), (6, 1, 0x80), (12, 5, 0x71), (18, 6, 0), (24, 4, 0x81)]));
        sounds["fight.hit"] = Boot(Entry0(0, 0x42, 0, 0xF6, 0x81, 0x80, 10 * Jiffy));
        var ping = new JsonArray();
        foreach (var (n, a) in new[] { (0, 0x90), (1, 0xB0), (2, 0x90) })
            foreach (var step in Entry0(n * 13 / 50.0, a, 0, 0xF6, 0x11, 0x10, 0))
                ping.Add(step?.DeepClone());
        sounds["fight.ping"] = Boot(ping);
        var death = new JsonArray();
        var gap = 2 * (255 * 1791 + 20) / Clock; // the delay's two calls with X = $FF
        foreach (var step in Entry0(0, 4, 0xB4, 0xFA, 0x11, 0x10, 0))
            death.Add(step?.DeepClone());
        foreach (var step in Entry0(gap, 4, 0x30, 0xFA, 0x11, 0x10, 0))
            death.Add(step?.DeepClone());
        sounds["fight.death"] = Boot(death);
    }

    /// <summary>SND's entry0 at <paramref name="at"/> seconds: the writes of <c>$127D</c>–<c>$12A4</c> at their cycles.</summary>
    private static JsonArray Entry0(double at, int high, int low, int sr, int on, int off, double tail)
    {
        var steps = Cycles(at, [(0, 1, high), (8, 4, off), (14, 0, low), (20, 2, 0x80), (24, 3, 0x80), (30, 5, 0), (36, 6, sr),
            (42, 4, on), (48, 4, off)]);
        if (tail > 0)
            steps.Add(J.O(("at", Math.Round(at + 48 / Clock + tail, 6)), ("poke", new JsonArray())));
        return steps;
    }

    private static JsonArray Cycles(double at, (int Cycle, int Register, int Value)[] writes) =>
        J.A(writes.Select(w => (JsonNode?)J.O(("at", Math.Round(at + w.Cycle / Clock, 9)), ("poke", J.A([Pk(w.Register, w.Value)])))));
}
