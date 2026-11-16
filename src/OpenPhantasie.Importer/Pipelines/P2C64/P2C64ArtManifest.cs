using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P1C64;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P2C64;

/// <summary>
/// <c>GameData/phantasie2/graphics/c64.json</c>, II's C64 pack manifest (s219; I's layout, <see cref="C64PhantasieArtManifest"/>):
/// the notes and II's map sprite bytes (the dungeon sprite on byte 50 only). Nothing depends on the copy, so the shipped
/// file equals what an import writes.
/// </summary>
public static class P2C64ArtManifest
{
    public static JsonObject Pack() => C64PhantasieArtManifest.Pack(new(
        P2C64Art.GameId, P2C64Art.RootPath,
        Note: "Commodore 64 Phantasie II art (v1.2; the p2-c64 import, P2C64Art; reverse-engineering/c64/phantasie2/" +
            "GRAPHICS.md). C64 I's engine: FONT+HER, HSPRIT and the item pictures are I's byte for byte. Monsters are the " +
            "MX n sprite bands of side 1 (one figure: the band's first sprites, expanded as the VIC does); portraits the " +
            "HSPRIT pair of each race (none for UNDEAD); the title PIC.* (multicolour bitmap, doubled pixels); " +
            "town.backdrop the town screen's six buildings at their places (FONT+HER's town characters); the map from " +
            "II's FONT characters as II's UTIL-3.O routine prints them. Colours: Colodore (VICE colodore.vpl). Text: the " +
            "remaster 5x7 font (the C64's sets are in font/). The item / feature pictures (items/, I's files and rule) in " +
            "the programs' colours, beside the find / trap / stairs / lock / exit texts (event.*, find_pictures; DOOR " +
            "not shown). s221: the ending (WIN's hall from PH2.END-4, TLUM1's crowd; ending_scene) and the party wipe " +
            "(CEMETARY2's temple, ghost and face through the ST's defeat scene: ui.decor, decor.demon-face, " +
            "portrait.ghost) in ending/ and defeat/.",
        TileMapsNote: "UTIL-3.O $C5C3 (SYS 49152 + 63): map byte b >= 120 prints a blank in black (fog); else class " +
            "b / 10 prints PETSCII $C697[class] OR (text column AND 1) in colour $C68A[class], one 8 x 8 character a " +
            "cell, the region's 26 x 20 cells whole (text columns 1-26, rows 5-24): tile b is the even map column's " +
            "character, b + 256 the odd one's. Bytes 40, 41 and 50 (only 50: I's routine takes 50-59) also place a " +
            "yellow sprite (town 93, inn 94, dungeon 95) 4 pixels left of the cell (overlays, drawn at their size); the " +
            "party is white sprite 92 the same way. The Client's map box holds 19 x 18 of the cells around the party " +
            "(the C64 shows all 26 x 20). Dungeon: as I's, FONT characters $E6-$EC in grey (12) on black by DOS's cell " +
            "ranges; a cell not yet seen is the solid $20; the party is yellow sprite 91 over its cell.",
        TitleNote: "B (v1.2): a credits text screen first (lines 590-670, at least 5 s; not drawn here), then the title " +
            "picture (PIC.*, lines 185-290) stays up while UTIL-3.O and the two fonts load (no key wait; its time is the " +
            "disk's [?]), then the '1 - Start in Pippacott / 2 - Continue saved game' text screen. Shown until a key, as " +
            "the Apple packs do.",
        PortraitsNote: "portrait.{race}: HSPRIT (I's file) blocks 2 race (idle) and 2 race + 1 (attacking), the pair the " +
            "fight copies for a member of that race (C64 I, live s218); yellow, the combat row's colour [~]. No UNDEAD pair.",
        Town: (40, 40), Inn: (41, 41), Dungeon: (50, 50))
    {
        EndPictures =
        [
            ("ending.final", "ending/hall.png"), ("ending.crowd.0", "ending/crowd-0.png"), ("ending.crowd.1", "ending/crowd-1.png"),
            ("ui.decor", "defeat/temple.png"), ("decor.demon-face", "defeat/temple-demon-doorway.png"),
            ("portrait.ghost", "defeat/ghost.png"),
            ("defeat.temple", "defeat/temple.png"), ("defeat.ghost", "defeat/ghost.png"),
            .. C64PhantasieArtManifest.C64EndSprites(ending: false),
        ],
        EndingScene = J.O(
            ("_note", "s221 / s223 (GRAPHICS.md 8.1, WIN's compiled BASIC, live in VICE): the intro page, black on yellow, " +
                "no key: up while PH2.END-4 and TLUM1 load (16.85 s measured with VICE's true drive, PAL [~]); the hall " +
                "(PH2.END-4) with its cyan panel (the panel's text cyan on inverted characters: black letters); the crowd " +
                "(TLUM1: 7 expanded green sprites at screen y 110); the whole party in the row from the start (hero band at " +
                "screen y 75, x 86 + 24 k, yellow); per member: FOR 4000 (4.17 s), the member in light red in its attacking " +
                "pose in its place while the panel names it (1.09 s) and FOR 4000 (4.17 s), then the cheer: the crowd's " +
                "second frame for the sound (9.92 s), then the first again (whole cheer 10.22 s). s224: drawn by c64_win " +
                "(WIN's own steps; the remaster's ending screen fields here are its fallback, unused with c64_win). The " +
                "closing page black on yellow cells (the hall's inverted characters still loaded). No fade on the C64; no " +
                "C64 sounds: SID playback [?] (I's GRAPHICS.md 9.6)."),
            ("palette", C64PhantasieArtManifest.Pens()),
            ("vbl_hz", 50),
            ("fade_vbl", 1),
            ("tick_hz", 1000),
            ("hold_ticks", 3870),
            ("row_top", 75),
            ("slot_size", 24),
            ("grant_ticks", 4170),
            ("row_by_record", true),
            ("row_from_start", true),
            ("row_left", 110 - 24),
            ("present_colour", 10),
            ("crowd", J.O(
                ("frames", J.Strs(["ending.crowd.1", "ending.crowd.0"])),
                ("x", 0),
                ("y", 110),
                ("flashes", 1),
                ("flash_ticks", 9920))),
            ("text_y", 168),
            ("text_pen", 0),
            ("line_ticks", 5260),
            ("intro_page", J.O(
                ("palette", C64PhantasieArtManifest.Pens()), ("x", 8), ("y", 16), ("line_gap", 8), ("text_pen", 0),
                ("button_pen", 0), ("background_pen", 7), ("no_button", true), ("seconds", 16.85))),
            ("closing_page", J.O(("palette", C64PhantasieArtManifest.Pens()), ("x", 8), ("y", 8), ("line_gap", 8), ("text_pen", 0), ("button_pen", 0), ("background_pen", 7), ("no_button", true))),
            ("c64_win", J.O(
                ("_note", "s224 (GRAPHICS.md 8.1, WIN's compiled BASIC, timed in VICE PAL with the true drive [~]): the intro " +
                    "page, no key, while PH2.END-4 loads (intro_seconds); the hall alone for FOR 2000 (1.74 s) and TLUM1's " +
                    "load (7.75 s) (hall_seconds); the crowd (frame 0) and the whole party with the header lines (FILMON " +
                    "SPEAKS - at column 2, row 20; the next PRINT at column 0, row 21); FOR 4000 (for_seconds); a cheer; per " +
                    "presented member: FOR 4000; the member light red in its attacking pose with name / THE x ONE / FROM y on " +
                    "rows 20-22 at INT(20 - LEN / 2); FOR 1000 (title_seconds, the prints included); FOR 4000; a cheer; the " +
                    "member yellow again. A cheer: the panel cleared and THE CROWD GOES WILD! (column 10, row 21); the crowd's " +
                    "pointers + 8 at cheer_frame_at for the SID cheer (cheer_frame_seconds), - 8, the panel cleared " +
                    "(cheer_line_seconds), the next step at cheer_seconds. Only WIN's POKEs change the crowd's frame (VICE " +
                    "s224: no interrupt alternates it). The wands prompt (FILMON SPEAKS - at column 2, row 20, the three wand " +
                    "lines from column 0, rows 21-23, then (PRESS RET)) and the closing page each until a key. Core presents " +
                    "the members it titles (records 0..n-1, n = OK members, the ST's rule); the C64 presents every slot [~]."),
                ("intro_seconds", 16.85),
                ("hall_seconds", 9.49),
                ("crowd", J.Strs(["ending.crowd.0", "ending.crowd.1"])),
                ("crowd_x", 0),
                ("crowd_y", 110),
                ("row_left", 110 - 24),
                ("row_top", 125 - 50),
                ("slot_size", 24),
                ("present_colour", 10),
                ("text_colour", 0),
                ("header_at", new JsonArray(J.Ints([2, 20]), J.Ints([0, 21]))),
                ("for_seconds", 4.17),
                ("title_seconds", 1.09),
                ("title_row", 20),
                ("title_centre", 20),
                ("title_word", "c64.p2.title_the_x_one"),
                ("title_from", "c64.p2.title_from_x"),
                ("cheer_line", J.O(("text", "c64.p2.crowd_goes_wild"), ("col", 10), ("row", 21), ("colour", 0), ("seconds", 0))),
                ("cheer_frame_at", 0.16),
                ("cheer_frame_seconds", 9.93),
                ("cheer_line_seconds", 10.15),
                ("cheer_seconds", 10.23),
                ("wands_at", new JsonArray(J.Ints([2, 20]), J.Ints([0, 21]), J.Ints([0, 22]), J.Ints([0, 23]))),
                ("wands_key", "c64.p2.press_ret")))),
        C64Defeat = C64PhantasieArtManifest.DefeatLayout(),
    });
}
