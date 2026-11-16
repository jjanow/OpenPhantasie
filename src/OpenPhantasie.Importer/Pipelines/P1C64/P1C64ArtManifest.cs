using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// <c>GameData/phantasie1/graphics/c64.json</c>, the C64 pack's manifest (s218). Everything in it is the layout the C64
/// code gives (GRAPHICS.md) and the file names <see cref="P1C64Art"/> writes; nothing depends on the copy, so the shipped
/// file equals what an import writes.
/// </summary>
public static class P1C64ArtManifest
{
    public static JsonObject Pack() => C64PhantasieArtManifest.Pack(new(
        P1C64Art.GameId, P1C64Art.RootPath,
        Note: "Commodore 64 Phantasie I art (v1.4; the p1-c64 import, P1C64Art; reverse-engineering/c64/phantasie1/" +
            "GRAPHICS.md). Monsters are the MX n sprite bands (one figure: the band's first sprites, expanded as the " +
            "VIC does); portraits the HSPRIT pair of each race (none for UNDEAD); the title PIC.* (multicolour " +
            "bitmap, doubled pixels); town.backdrop the town screen's six buildings at their places (FONT+HER's town " +
            "characters); the map from FONT's characters as UTIL-3.O's routine prints them. Colours: Colodore " +
            "(VICE colodore.vpl). Text: the remaster 5x7 font (the C64's sets are in font/). The item / feature " +
            "pictures (items/) in the programs' colours, beside the find / trap / stairs / lock / exit texts (event.*, " +
            "find_pictures; DOOR not shown). s221: the ending (P.END's Zeus from ZALL, the members' cloud, the lightning " +
            "hand; ending_scene) and the party wipe (CEMETARY's temple, ghost and face; defeat_pictures, DOS's pages, " +
            "defeat.figure.* = the HSPRIT idle figures in yellow, no UNDEAD figure) in ending/ and defeat/.",
        TileMapsNote: "UTIL-3.O $C5C3 (SYS 49152 + 63): map byte b >= 120 prints a blank in black (fog); else class " +
            "b / 10 prints PETSCII $C695[class] OR (text column AND 1) in colour $C688[class], one 8 x 8 " +
            "character a cell, the region's 26 x 20 cells whole (text columns 1-26, rows 5-24): tile b is the " +
            "even map column's character, b + 256 the odd one's. Bytes 40, 41 and 50-59 also place a yellow " +
            "sprite (town 93, inn 94, dungeon 95) 4 pixels left of the cell (overlays, drawn at their size); " +
            "the party is white sprite 92 the same way. The Client's map box holds 19 x 18 of the cells " +
            "around the party (the C64 shows all 26 x 20). Dungeon: FONT characters $E6-$EC in grey (12) on " +
            "black by DOS's cell ranges (as on screen, live s218); a cell not yet seen is the solid $20; the " +
            "party is yellow sprite 91 over its cell.",
        TitleNote: "B lines 200-420: the title picture (PIC.*) stays up while UTIL-3.O and the two fonts load (no key " +
            "wait; its time is the disk's [?]), then the '1 - Start in Pelnor / 2 - Continue' text screen. " +
            "Shown until a key, as the Apple packs do.",
        PortraitsNote: "portrait.{race}: HSPRIT blocks 2 race (idle) and 2 race + 1 (attacking), the pair the fight copies " +
            "for a member of that race (live s218); yellow, the combat row's colour [~]. No UNDEAD pair.",
        Town: (40, 40), Inn: (41, 41), Dungeon: (50, 59))
    {
        EndPictures =
        [
            ("ending.final", "ending/zeus.png"), ("ending.ghost", "ending/sprites/d0-cloud.png"),
            ("ending.arm", "ending/sprites/cf-lightning-hand.png"),
            ("defeat.temple", "defeat/temple.png"), ("defeat.demon", "defeat/temple-demon.png"),
            ("defeat.ghost", "defeat/ghost.png"), ("portrait.ghost", "defeat/ghost.png"),
        ],
        EndingScene = J.O(
            ("_note", "s221 (GRAPHICS.md 9.1, P.END live in VICE): Zeus (ZALL's characters, white) on black; one cloud " +
                "(sprite block $D0, multicolour) per member, the striking hand ($CF, light blue, VIC (202, 62)) and per " +
                "member its figure in the cloud's place (HSPRIT, the pack's yellow portraits; the C64 takes block " +
                "64 + P(168), yellow if P(18) != 0 else light grey [~]); the speech typed in yellow from text row 21. Drawn " +
                "with the remaster's ending screen: the C64 has no fade (fade_vbl 1, the shortest) and wipes Zeus in from " +
                "the bottom up over about 13 s, flashes the hand per member, talks with the face sprite and raises the " +
                "grant hand: not modelled [~]. The clouds rise from y 231 to 150 at x 90, 60, 30 / 240, 270 (VIC) beside " +
                "Zeus; here the row is centred under him (row_top 128, slot_size 24) [~]. Waits (hold, grant, line) and " +
                "the VBL rate are not measured [~]; no C64 sounds."),
            ("palette", C64PhantasieArtManifest.Pens()),
            ("vbl_hz", 50),
            ("fade_vbl", 1),
            ("hold_ticks", 50),
            ("ghost", "ending.ghost"),
            ("arm", "ending.arm"),
            ("arm_x", C64EndScreens.ArmX),
            ("arm_y", C64EndScreens.ArmY),
            ("row_top", 128),
            ("slot_size", 24),
            ("grant_ticks", 100),
            ("text_y", 168),
            ("text_pen", 7),
            ("line_ticks", 200)),
        DefeatPictures = J.O(
            ("_note", "s221 (GRAPHICS.md 9.2, CEMETARY live in VICE): DOS's pages (P1DosDefeatScene) with CEMETARY's " +
                "pictures: defeat.temple (FONTO's temple, dark grey, doorway open), defeat.demon (the same with the Great " +
                "Demon's face, purple), defeat.ghost (SKEL2's ghost, white), defeat.figure.<race> (CSPRIT = HSPRIT's idle " +
                "figures, yellow: the RESURRECTED colour). The C64 colours each fate's figure (UNDEAD cyan, DESTROYED dark " +
                "grey) where DOS clears a destroyed slot and draws race 15 for UNDEAD (no C64 picture: fallback) [~]; its " +
                "door rises out of the doorway (not modelled) [~]. Row: ghosts at VIC y 170 (screen 120) at x 110 - 17.5 n " +
                "+ 35 k; here DOS's gap rule [~]. Lines: text row 23, blue (the C64's astral / demon lines; its fate lines " +
                "take the fate's colour) [~]."),
            ("row_y", 120),
            ("slot_align", 1),
            ("clear_top", 120),
            ("clear_bottom", 143),
            ("text_y", 184),
            ("text_colour", C64PhantasieArtManifest.Pens()[6]!.GetValue<string>())),
    });
}
