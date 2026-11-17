using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// The C64 packs' manifest (I's and II's <c>graphics/c64.json</c>, s218 / s219): the layout the shared engine gives
/// (<see cref="C64PhantasieArt"/>) and the file names it writes; a game gives its notes and its map sprites' bytes.
/// Nothing depends on the copy, so the shipped file equals what an import writes.
/// </summary>
public static class C64PhantasieArtManifest
{
    /// <summary>DOS's cell → glyph ranges (OV2 0x21df), which the C64's dungeon characters $E6–$EC follow (live s218).</summary>
    public static readonly int[][] DungeonCells =
        [[0, 1, 0xE6], [2, 184, 0xE7], [185, 199, 0xEA], [200, 214, 0xE9], [215, 217, 0xEB], [218, 220, 0xEC], [221, 255, 0xE8]];

    /// <summary>s221: the note on <c>find_pictures</c> and the <c>event.*</c> slots (the same for I and II).</summary>
    public const string FindPicturesNote =
        "s221 [C] (I's GRAPHICS.md 7, the compiled BASIC of OUTDOORS / DUNGEON, II's the same): a found item shows " +
        "I$((IT - 1) / 20) (SHIELD ... GOOD TREASURE) in colour IC = 15, 7 if IX = 2 or IX >= 6, 14 if IX = 3, 3 if " +
        "IX = 4; IX is a float, so the single-id colours hit ids 41, 61 and 81 only. find_pictures: [first id, last id, " +
        "picture]. event.trap (light red 10, a spotted trap), event.stairs (grey 12), event.lock (brown 9, a locked " +
        "door, kind 14, whatever the pick), event.exit (green 5, 8 x 6). DOOR is loaded by no program (no slot). The " +
        "C64 prints them over the map; the Client draws each beside its text.";

    /// <summary>
    /// A game's words in the manifest and the map bytes that place the town / inn / dungeon sprites; s221: its ending and
    /// party-wipe slots (<see cref="EndPictures"/>, the <c>ending_scene</c> layout, I's <c>defeat_pictures</c> layout and
    /// its <c>defeat.figure.{n}</c> set).
    /// </summary>
    public sealed record Spec(
        string GameId, string Root, string Note, string TileMapsNote, string TitleNote, string PortraitsNote,
        (int Min, int Max) Town, (int Min, int Max) Inn, (int Min, int Max) Dungeon)
    {
        public IReadOnlyList<(string Slot, string File)> EndPictures { get; init; } = [];
        public JsonObject? EndingScene { get; init; }
        public JsonObject? DefeatPictures { get; init; }
        public JsonObject? C64Defeat { get; init; }
        public JsonObject? SidSounds { get; init; }
    }

    /// <summary>
    /// s223: the busy parts of the end screens' waits, measured in VICE (PAL, I's GRAPHICS.md 9.5) `[~]`: the interpreter's
    /// time around a <c>TI</c> wait in a loop, and the empty <c>FOR</c> loops.
    /// </summary>
    public static class Timings
    {
        /// <summary>A rise step: 0.0606 s measured (91 steps, 5.46–5.53 s) against its TD = 0.05.</summary>
        public const double CloudStepOverhead = 0.0106;
        /// <summary>A door step: 0.0999 s measured against its TL = 0.1 (the TI reset mid-jiffy and the loop cancel out).</summary>
        public const double DoorStepOverhead = 0;
        /// <summary>A speech character (MID$, PRINT): about 6.4 ms.</summary>
        public const double LetterSeconds = 0.0064;
        /// <summary>The thunder GOSUB's FOR 1400 and the BASIC around it: the whole GOSUB 1.79 s, its TD waits 0.5.</summary>
        public const double ThunderLoop = 1.29;
        /// <summary>The fate's sound GOSUB with its TL = 1.08: 1.17 s measured.</summary>
        public const double FateSound = 1.17;
        /// <summary>The ghost's block rewritten byte by byte (GOSUB 0x869): 4.04–4.14 s.</summary>
        public const double FateCopy = 4.1;
        /// <summary>A ghost: 0.33 s apart measured (TL = 0.3).</summary>
        public const double Ghost = 0.33;
    }

    /// <summary>s223: the end pictures both packs add for the C64's own end screens (the face and hand frames, the door, the face).</summary>
    public static IEnumerable<(string Slot, string File)> C64EndSprites(bool ending)
    {
        if (ending)
        {
            for (var i = 0; i < 8; i++)
                yield return ($"ending.face.{i}", $"ending/sprites/c{6 + i:x}-face-{i}.png");
            for (var i = 0; i < 6; i++)
                yield return ($"ending.hand.{i}", $"ending/sprites/c{i}-hand-{i}.png");
        }
        yield return ("defeat.door", "defeat/door.png");
        yield return ("defeat.face", "defeat/demon-face.png");
    }

    /// <summary>
    /// s223: <c>c64_defeat</c>, the same for I and II (<c>CEMETARY</c> = <c>CEMETARY2</c> but for its file names; I's
    /// GRAPHICS.md 9.4): screen pixels (VIC x − 24, y − 50), Colodore indices, CEMETARY's <c>TL</c> waits.
    /// </summary>
    public static JsonObject DefeatLayout() => J.O(
        ("_note", "s223 (I's GRAPHICS.md 9.2 / 9.4, CEMETARY's compiled BASIC): runs by itself, no key. 'Your party has " +
            "been defeated' (red, column 6, row 23), TL = 2; the temple (dark grey) with its door (sprite 7, light grey, " +
            "x- and y-expanded, behind the characters) and the astral line (blue, column 1, row 23); a ghost per member at " +
            "VIC y 170, x = INT((220 - 35 PO) / 2) + 35 k, one each TL = 0.3; a sound, TL = 2; the door rises from VIC y 77 " +
            "to 0, one pixel each TL = 0.1; the face (purple, expanded, in front) and the demon's line, TL = 4; per member " +
            "the rows cleared (an UNDEAD fate's name on row 21, column 1, cyan: undead_name_row), a sound with TL = 1.08, " +
            "its line in the fate's colour, its ghost's block overwritten with CSPRIT block P(145) (its own figure) a byte " +
            "per BASIC pass, still white (64 passes, the 4.1 s copy; s224 morph), then coloured, a destroyed member's then " +
            "switched off (ON DF GOSUB), TL = 2; TL = 6. The interpreter's time around the waits measured in VICE (PAL, " +
            "s223) [~]: ghosts 0.33 s apart, the fate sound 1.17 s, the copy 4.1 s. The C64's own fate rule and texts are " +
            "not used: Core's fates and lines (I's UNDEAD line holds the name too [~])."),
        ("temple", "defeat.temple"),
        ("ghost", "defeat.ghost"),
        ("door", "defeat.door"),
        ("face", "defeat.face"),
        ("defeated", J.O(("text", "core.defeat.your_party_has_been_defeated"), ("col", 6), ("row", 23), ("colour", 2), ("seconds", 2))),
        ("text_col", 1),
        ("text_row", 23),
        ("astral_colour", 6),
        ("demon_colour", 6),
        ("row_x2", 220 - 2 * 24),
        ("row_gap", 35),
        ("row_y", 170 - 50),
        ("ghost_seconds", Timings.Ghost),
        ("after_ghosts_seconds", 2),
        ("door_x", 148 - 24),
        ("door_y", 77 - 50),
        ("door_steps", 78),
        ("door_step_seconds", 0.1),
        ("door_step_overhead", Timings.DoorStepOverhead),
        ("face_x", 148 - 24),
        ("face_y", 81 - 50),
        ("demon_seconds", 4),
        ("fate_sound_seconds", Timings.FateSound),
        ("fate_copy_seconds", Timings.FateCopy),
        ("fate_seconds", 2),
        ("destroyed_colour", 11),
        ("resurrected_colour", 7),
        ("undead_colour", 3),
        ("end_seconds", 6),
        ("undead_name_row", 21),
        ("undead_name_col", 1));

    /// <summary>
    /// s226: the empty <c>FOR</c> loop's pass in <c>WIN</c> (the band IRQ running): its <c>FOR 4000</c> took 4.171 s in VICE
    /// (PAL, I's GRAPHICS.md 9.5, s224), 4001 passes.
    /// </summary>
    public const double WinForPass = 4.171 / 4001;

    /// <summary>s226: <c>WIN</c>'s cheer (<c>GOSUB 0x749</c>): 9.928 s in VICE (s224).</summary>
    public const double WinCheerSeconds = 9.928;

    /// <summary>
    /// s226 `[~]`: one pass of the cheer's pitch sweeps, the cheer's measured time less its <c>FOR 400</c> and 16 ×
    /// <c>FOR 500</c> at <see cref="WinForPass"/>, over its 58 sweep passes (the POKEs' own time folded in).
    /// </summary>
    public static double WinSweepPass => (WinCheerSeconds - 401 * WinForPass - 16 * 501 * WinForPass) / 58;

    private static JsonNode Poke(int register, int value) => J.Ints([register, value]);

    private static JsonArray Zeros(int first, int last, bool down = false) =>
        J.A((down ? Enumerable.Range(first, last - first + 1).Reverse() : Enumerable.Range(first, last - first + 1))
            .Select(r => (JsonNode?)Poke(r, 0)));

    private static JsonArray Pokes(JsonArray head, params int[][] pokes)
    {
        foreach (var p in pokes)
            head.Add(Poke(p[0], p[1]));
        return head;
    }

    private static JsonObject PokeStep(double at, JsonArray pokes) => J.O(("at", Math.Round(at, 6)), ("poke", pokes));

    /// <summary>
    /// s226: the end screens' SID sounds (I's GRAPHICS.md 9.6, read again s226 from the bytecode and <c>ZALL</c>'s 6502)
    /// `[C]`: <paramref name="ending"/> = P.END's (I: the wind, the thunder, the speech click), <paramref name="win"/> =
    /// WIN's cheer (II); both packs have CEMETARY's two (the same bytes in <c>CEMETARY2</c>).
    /// </summary>
    public static JsonObject SidSounds(bool ending, bool win)
    {
        var sounds = new JsonObject();
        if (ending)
        {
            // ZALL $4D40: Y = $1C down to 0: $D400 + Y := $4E65[Y]; then the IRQ hook $4D73.
            byte[] table = [0x00, 0x64, 0x00, 0x00, 0x80, 0xF0, 0x1E, 0x00, 0x64, 0x00, 0x00, 0x10, 0xF0, 0xC0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x0F, 0, 0, 0, 0];
            sounds["wind"] = J.A([
                PokeStep(0, J.A(Enumerable.Range(0, 29).Reverse().Select(r => (JsonNode?)Poke(r, table[r])))),
                J.O(("at", 0), ("zall_wind", J.O(("low", 100), ("high", 200), ("start_wait", 2), ("freq_regs", J.Ints([1, 8])),
                    ("control_regs", J.Ints([4, 11])), ("gates_on", J.Ints([0x81, 0x11])), ("gates_off", J.Ints([0x80, 0x10]))))),
            ]);
            // ZALL $4E46: Y = $1C down to 0: $D400 + Y := 0; the IRQ vector back.
            sounds["wind_stop"] = J.A([PokeStep(0, Zeros(0, 28, down: true))]);
            // P.END 0xbb6: FOR I = S TO S + 22: POKE S, 0 (S, not I: only $D400 is cleared, 23 times).
            sounds["thunder"] = J.A([
                PokeStep(0, Pokes(J.A([Poke(0, 0)]), [24, 143], [5, 10], [19, 10], [6, 170], [20, 169], [4, 21], [18, 17],
                    [1, 68], [15, 42])),
                PokeStep(0.3, Pokes(new JsonArray(), [4, 20], [18, 16])),
            ]);
            // P.END 0xc43: the setup, TI$ = "000000", the gate, voice 1's low byte := 110 + RND * 70; the caller's wait.
            var click = Pokes(new JsonArray(), [0, 188], [1, 2], [24, 15], [7, 15], [8, 0], [12, 0], [13, 244], [11, 21]);
            click.Add(J.A([JsonValue.Create(0), J.O(("rnd", J.Ints([110, 70])))]));
            sounds["click"] = J.A([PokeStep(0, click)]);
            sounds["click_off"] = J.A([PokeStep(0, Pokes(new JsonArray(), [11, 16]))]);
        }
        // CEMETARY 0x90e: FOR V = 0 TO 20: POKE S + V, 0; voice 2's noise through the low-pass, gate left on.
        sounds["rumble"] = J.A([PokeStep(0, Pokes(Zeros(0, 20), [21, 7], [23, 242], [24, 31], [22, 0], [13, 240], [10, 4],
            [15, 48], [12, 245], [8, 8], [22, 0], [11, 135]))]);
        // CEMETARY 0x97b: FOR V = 0 TO 24: POKE S + V, 0; ...; TL = 1.08; the gate off.
        sounds["fate"] = J.A([
            PokeStep(0, Pokes(Zeros(0, 24), [1, 3], [5, 41], [15, 30], [3, 8], [24, 31], [23, 4], [21, 1], [4, 21])),
            PokeStep(1.08, Pokes(new JsonArray(), [4, 20])),
        ]);
        // s228: the fights' sounds (both terrains), I's ring tune (C64PlaySounds, GRAPHICS.md 9.8).
        C64PlaySounds.AddFight(sounds, p2: win, dungeon: false);
        C64PlaySounds.AddFight(sounds, p2: win, dungeon: true);
        if (ending)
            C64PlaySounds.AddRing(sounds);
        if (win)
        {
            // WIN 0x749: the clear, the setup; FOR F = 11650 TO 10500 STEP -40 (low, high); gate off; FOR 400; gate on;
            // FOR F = 10500 TO 11650 STEP 40; FOR V = 15 TO 0 STEP -1: POKE S + 24, V: FOR M = 1 TO 500.
            var d = WinSweepPass;
            var down = 29 * d;
            var gap = 401 * WinForPass;
            sounds["cheer"] = J.A([
                PokeStep(0, Pokes(Zeros(0, 24), [5, 238], [6, 172], [22, 126], [23, 0], [4, 133], [24, 79])),
                Sweep(0, [0, 1], 11650, 10500, -40, d),
                PokeStep(down, Pokes(new JsonArray(), [4, 132])),
                PokeStep(down + gap, Pokes(new JsonArray(), [4, 133])),
                Sweep(down + gap, [0, 1], 10500, 11650, 40, d),
                Sweep(down + gap + down, [24], 15, 0, -1, 501 * WinForPass),
            ]);
        }
        return J.O(
            ("_note", "s226 (GRAPHICS.md 9.6) [C]: the end screens' sounds as their programs write the SID ($D400 + register), " +
                "in order; times in seconds from the sound's start (BASIC's TI waits; the VICE-measured interpreter time " +
                "for the FOR loops, PAL). Played on one 6581 model per screen (Client Sid6581). wind / wind_stop: ZALL $4D40 " +
                "/ $4E46 (P.END step 3 to after the wipe's TD = 2), the IRQ routine $4D73 once a jiffy (zall_wind). thunder: " +
                "P.END 0xbb6 per strike (its clear loop pokes $D400 only). click / click_off: P.END 0xc43 per space and " +
                "line end, the gate off after the caller's 16 or RND * 8 jiffies. rumble: CEMETARY 0x90e after the ghosts, " +
                "its gate never turned off (it sounds until the first fate's clear). fate: CEMETARY 0x97b per member. cheer: " +
                "WIN 0x749; the sweeps' pass is the measured 9.928 s less the FOR loops at WIN's 1.042 ms a pass [~]. " +
                "s228 (GRAPHICS.md 9.8) [C]: fight.{outdoors,dungeon}.* = the fights' SID routines of " +
                (win ? "OUTDOORS2 / DUNGEON2" : "OUTDOORS / DUNGEON") + " (cast, monster_cast, awakens, alive, miss, hit, " +
                "encounter, wound, monster_hit, dies, mark), the statements and FOR passes at the times VICE measured " +
                "(II's routines forced on its fight screen" + (win ? "" : "; I's are the same bytecode [~]") + "); the " +
                "TI waits as written. marks: MN field 13 (C%) of monsters 0-79, the marks (each a mark sound) a damage " +
                "spell draws on that monster." + (ending ? " town.ring: TOWNR 0x503f, U)SE of an item 141-150 in town." : "")),
            ("clock_hz", 985248),
            ("jiffy_hz", 60),
            ("tail_seconds", 2),
            ("marks", J.Ints(win ? C64PlaySounds.MarksP2 : C64PlaySounds.MarksP1)),
            ("sounds", sounds));

        static JsonObject Sweep(double at, int[] regs, int from, int to, int by, double every) =>
            J.O(("at", Math.Round(at, 6)), ("sweep", J.O(("regs", J.Ints(regs)), ("from", from), ("to", to), ("by", by),
                ("every", Math.Round(every, 6)))));
    }

    /// <summary>s221: the Colodore palette as the 16 pens of an <c>ending_scene</c> (each art colour is one pen).</summary>
    public static JsonArray Pens() => J.Strs(C64Gfx.ColodoreHex);

    public static JsonObject Pack(Spec spec)
    {
        var pictures = new JsonObject
        {
            ["title.cover"] = "screens/title.png",
            ["town.backdrop"] = "screens/town.png",
            ["overworld.party"] = "overworld/party.png",
            [$"overworld.tile.{C64PhantasieArt.TownTile}"] = "overworld/town.png",
            [$"overworld.tile.{C64PhantasieArt.InnTile}"] = "overworld/inn.png",
            [$"overworld.tile.{C64PhantasieArt.DungeonTile}"] = "overworld/dungeon.png",
        };
        foreach (var (_, name, _, slot) in C64PhantasieArt.ItemPictureColours)
            if (slot is not null)
                pictures[slot] = $"items/{name}.png";
        foreach (var (slot, file) in spec.EndPictures)
            pictures[slot] = file;
        for (var b = 0; b < 256; b++)
        {
            var name = b >= C64PhantasieArt.FogFrom ? "fog" : $"class-{b / 10:D2}";
            pictures[$"overworld.tile.{b}"] = $"overworld/tiles/{name}-even.png";
            pictures[$"overworld.tile.{b + C64PhantasieArt.OddColumnTile}"] = $"overworld/tiles/{name}-odd.png";
        }

        var sets = new List<(string Name, string Dir, string Prefix)>
        {
            ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
            ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
            ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
        };
        // s221: CEMETARY's fate figures are CSPRIT blocks 0-14 = HSPRIT's idle blocks (the portraits, in yellow: the
        // RESURRECTED colour); no UNDEAD figure.
        if (spec.DefeatPictures is not null)
            sets.Add(("defeat.figure.{n}", "portraits/idle", "{n:D2}-"));
        var pack = J.O(
            ("_schema", "phantasie.graphics-pack/v1"),
            ("_note", spec.Note),
            ("id", "c64"),
            ("game", spec.GameId),
            ("platform", "c64"),
            ("name", "Commodore 64"),
            ("auto_select", true),
            ("root", spec.Root),
            ("requires", J.Strs(["monsters", "overworld/tiles", "dungeon"])),
            ("screen", J.O(("width", 320), ("height", 200), ("pixel_aspect", J.Ints([1, 1])), ("background", "#000000"))),
            ("palette", J.Strs(C64Gfx.ColodoreHex)),
            ("font", "remaster-5x7"),
            ("_tile_maps", spec.TileMapsNote),
            ("tile_maps", J.O(
                ("overworld_tile", J.Ints([8, 8])),
                ("overworld_view", J.Ints([19, 18])),
                ("dungeon_glyph", J.Ints([8, 8])),
                ("dungeon_unseen", C64PhantasieArt.UnseenGlyph),
                ("dungeon_party", C64PhantasieArt.PartyGlyph),
                ("dungeon_party_over", true),
                ("dungeon_default", 0xE7),
                ("dungeon_cells", J.A(DungeonCells.Select(c => (JsonNode?)J.Ints(c)))),
                ("odd_column_tile", C64PhantasieArt.OddColumnTile),
                ("overlays", J.A(new[]
                {
                    new[] { spec.Town.Min, spec.Town.Max, C64PhantasieArt.TownTile, -4, 0 },
                    [spec.Inn.Min, spec.Inn.Max, C64PhantasieArt.InnTile, -4, 0],
                    [spec.Dungeon.Min, spec.Dungeon.Max, C64PhantasieArt.DungeonTile, -4, 0],
                }.Select(o => (JsonNode?)J.Ints(o)))),
                ("party_offset", J.Ints([-4, 0])))),
            ("title_sequence", J.O(
                ("_note", spec.TitleNote),
                ("canvas", J.Ints([320, 200])),
                ("background", "#000000"),
                ("steps", J.A([J.O(("picture", "title.cover"))])))),
            ("pictures", pictures),
            ("_find_pictures", FindPicturesNote),
            ("find_pictures", J.A(C64PhantasieArt.FindPictures.Select(f => (JsonNode?)J.A([f.First, f.Last, $"event.find.{f.Name}"])))),
            ("_portraits", spec.PortraitsNote));
        if (spec.EndingScene is not null)
            pack["ending_scene"] = spec.EndingScene;
        if (spec.DefeatPictures is not null)
            pack["defeat_pictures"] = spec.DefeatPictures;
        if (spec.C64Defeat is not null)
            pack["c64_defeat"] = spec.C64Defeat;
        if (spec.SidSounds is not null)
            pack["sid_sounds"] = spec.SidSounds;
        pack["picture_sets"] = J.A(sets.Select(s => (JsonNode?)J.O(("name", s.Name), ("dir", s.Dir), ("prefix", s.Prefix), ("ext", ".png"))));
        return pack;
    }
}
