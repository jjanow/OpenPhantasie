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
        ],
        EndingScene = J.O(
            ("_note", "s221 (GRAPHICS.md 8.1, WIN live in VICE): a yellow text page; the hall (PH2.END-4) with its cyan " +
                "text panel (text rows 20-23); the crowd (TLUM1: 7 expanded green sprites at screen y 110, two frames " +
                "alternating); the party in the hall (hero band at screen y 75, x 86 + 24 k, yellow; the member presented " +
                "in light red, attacking, not modelled [~]); Filmon's lines in the panel; 'INSERT BOOT DISK'. Drawn with " +
                "the remaster's ending screen (II ST's steps): the row by record, centred (slot_size 24) [~]; the lines " +
                "on text row 21 in black over the panel (the C64's panel text colour [?]); the pages in yellow from text " +
                "row 2 / 1, no OK form (button_pen 0, black) [~]. No fade on the C64 (fade_vbl 1) [~]; waits and the " +
                "crowd's flashes the ST's (not measured on the C64) [~]; no C64 sounds."),
            ("palette", C64PhantasieArtManifest.Pens()),
            ("vbl_hz", 50),
            ("fade_vbl", 1),
            ("tick_hz", 1000),
            ("hold_ticks", 2000),
            ("row_top", 75),
            ("slot_size", 24),
            ("grant_ticks", 0),
            ("row_by_record", true),
            ("crowd", J.O(
                ("frames", J.Strs(["ending.crowd.0", "ending.crowd.1"])),
                ("x", 0),
                ("y", 110),
                ("flashes", 10),
                ("flash_ticks", 330))),
            ("text_y", 168),
            ("text_pen", 0),
            ("line_ticks", 3000),
            ("intro_page", J.O(
                ("palette", C64PhantasieArtManifest.Pens()), ("x", 8), ("y", 16), ("line_gap", 8), ("text_pen", 7),
                ("button_pen", 0))),
            ("closing_page", J.O(("x", 8), ("y", 8), ("line_gap", 8), ("text_pen", 7), ("button_pen", 0)))),
    });
}
