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
            .. C64PhantasieArtManifest.C64EndSprites(ending: true),
        ],
        EndingScene = J.O(
            ("_note", "s221 / s223 (GRAPHICS.md 9.1 / 9.4, P.END's compiled BASIC, ZALL's and UTIL-3.O's 6502): the C64's " +
                "own steps in c64 (the remaster's ending screen fields below are its fallback and unused with c64). " +
                "Positions are screen pixels (VIC x - 24, y - 50), colours Colodore indices. The closing text page " +
                "('Now your party will be transported ...', until a key) is not imported: the screen ends after the " +
                "grant [~]; the speech is Core's 7 lines on the C64's rows; no C64 sounds."),
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
            ("line_ticks", 200),
            ("c64", J.O(
                ("_note", "P.END: the astral line (TD = 2); a cloud per slot (all slots 0..PO), x 90 - 30 m for m < PL = " +
                    "INT(PO / 2 + 1.1), else 240 + 30 (m - PL) (VIC), rising from y 240 to 150 a pixel per TD = 0.05, MC1 " +
                    "yellow / red every 2 steps; TD = 2; Zeus wiped in from the bottom (ZALL $4CC0: 104 rows, 7 frames each); " +
                    "TD = 2; per slot the lightning hand, the thunder (TD 0.3 + 0.2 + FOR 1400), the figure (64 + P(168): the " +
                    "member's own idle figure; yellow if P(18) != 0 else light grey), TD = 0.3, the hand off, TD = 0.7; 'Zeus " +
                    "smiles and says' (row 24) with the face cycling $C6-$CD (UTIL-3.O $C86E, 10 frames each), TD = 3; the " +
                    "speech on rows 18-24, a character at a time, each space 16 + RND * 8 jiffies, each line's end two RND * 8; " +
                    "the grant hand cycling $C0-$C5, TD = 12. vbl_hz: VICE's PAL [~] (an NTSC C64 runs the wipe and the " +
                    "animator at 60). The interpreter's own time (cloud_step_overhead, letter_seconds, the FOR 1400 in " +
                    "thunder_seconds) measured in VICE (PAL) [~]."),
                ("vbl_hz", 50),
                ("jiffy_hz", 60),
                ("astral", J.O(("text", "core.defeat.arrived_at_astral_plane"), ("col", 3), ("row", 23), ("colour", 7), ("seconds", 2))),
                ("cloud", "ending.ghost"),
                ("cloud_mc1", 7),
                ("cloud_left_x", 90 - 24),
                ("cloud_right_x", 240 - 24),
                ("cloud_gap", 30),
                ("cloud_from_y", 240 - 50),
                ("cloud_to_y", 150 - 50),
                ("cloud_step_seconds", 0.05),
                ("cloud_step_overhead", C64PhantasieArtManifest.Timings.CloudStepOverhead),
                ("cloud_flash", J.Ints([7, 2])),
                ("cloud_flash_steps", 2),
                ("after_rise_seconds", 2),
                ("wipe_x", 104),
                ("wipe_y", 16),
                ("wipe_width", 96),
                ("wipe_height", 104),
                ("wipe_frames", 7),
                ("after_wipe_seconds", 2),
                ("strike_hand", "ending.arm"),
                ("strike_x", C64EndScreens.ArmX),
                ("strike_y", C64EndScreens.ArmY),
                ("thunder_seconds", 0.5 + C64PhantasieArtManifest.Timings.ThunderLoop),
                ("figure_seconds", 0.3),
                ("after_strike_seconds", 0.7),
                ("ok_colour", 7),
                ("dead_colour", 15),
                ("smiles", J.O(("text", "c64.endgame.zeus_smiles"), ("col", 8), ("row", 24), ("colour", 7), ("seconds", 3))),
                ("face", J.Strs(Enumerable.Range(0, 8).Select(i => $"ending.face.{i}"))),
                ("face_x", 161 - 24),
                ("face_y", 77 - 50),
                ("face_blank", J.Ints([18 * 8, 4 * 8, 16, 16])),
                ("animate_vbl", 10),
                ("speech_col", 1),
                ("speech_row", 18),
                ("speech_colour", 7),
                ("letter_seconds", C64PhantasieArtManifest.Timings.LetterSeconds),
                ("space_jiffies", 16),
                ("random_jiffies", 8),
                ("hand", J.Strs(Enumerable.Range(0, 6).Select(i => $"ending.hand.{i}"))),
                ("hand_x", 200 - 24),
                ("hand_y", 74 - 50),
                ("hand_blank", J.Ints([22 * 8, 3 * 8, 24, 16])),
                ("grant_seconds", 12)))),
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
        C64Defeat = C64PhantasieArtManifest.DefeatLayout(),
    });
}
