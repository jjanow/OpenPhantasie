using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P3Apple2;

/// <summary>
/// Port of <c>tools/scripts/build_a2_p3_asset_manifest.py</c>: <c>GameData/phantasie3/graphics/apple2.json</c> from the
/// extraction index (<see cref="P3Apple2Art.Index"/>). The layout text and numbers are ours (the script's constants);
/// the picture paths, the tile swaps and whether the title / ending exist come from the index. From a <c>[cr]</c> copy
/// the pack has no title and no ending (its screens are the cracker's), so those fall back to the Client's own.
/// </summary>
public static class P3Apple2ArtManifest
{
    public static JsonObject Pack(JsonObject index)
    {
        foreach (var need in new[] { "monsters", "portraits", "tiles", "dungeon", "screens" })
            if (index[need] is not JsonObject { Count: > 0 })
                throw new InvalidOperationException($"extraction-index.json has no {need}");
        var screens = index["screens"]!.AsObject();
        string File(JsonNode? entry) => (string)entry!["file"]!;
        var pictures = new JsonObject();
        if (screens.ContainsKey("title"))
        {
            pictures["title.cover"] = File(screens["title-picture"]);
            pictures["title.credits"] = File(screens["title"]);
        }
        if (screens.ContainsKey("ending"))
        {
            pictures["scene.reward.good"] = File(screens["ending"]);
            pictures["scene.reward.evil"] = File(screens["ending"]);
        }
        pictures["overworld.party"] = File(index["tiles"]!["party"]);

        var pack = J.O(
            ("_schema", "phantasie.graphics-pack/v1"),
            ("_note", "Apple II Phantasie III art (tools/scripts/extract_a2_p3_graphics.py + build_a2_p3_asset_manifest.py, " +
                      "reverse-engineering/apple2/phantasie3/GRAPHICS.md). Monsters are MONSTER SHAPES sprites (raw hi-res " +
                      "bytes, drawn by MONSTER PLOT at an odd byte column, as the game); party figures, map tiles and dungeon " +
                      "marks are Applesoft shapes (SHAPES) at the game's HCOLOR; colour is an NTSC artifact model [~]. The " +
                      "title and the ending's gods' room come only from an original copy: the [cr] copy's ICE DRAGON and " +
                      "WIN PIC are the cracker's screens. Text: the remaster 5x7 font (the Apple's hi-res font, IMPTEXT, " +
                      "lacks '.', '!', '/' and more; it is in font/). Screen kept at the remaster's 320 x 200 layout: Apple " +
                      "pictures (280 x 192) are drawn 1:1 in it."),
            ("id", "apple2"),
            ("game", "phantasie3"),
            ("platform", "apple2"),
            ("name", "Apple II"),
            ("auto_select", true),
            ("root", P3Apple2Art.Root),
            ("requires", J.Strs(["monsters", "overworld/tiles", "dungeon"])),
            ("screen", J.O(("width", 320), ("height", 200), ("pixel_aspect", J.Ints([1, 1])), ("background", "#000000"))),
            ("font", "remaster-5x7"),
            ("_tile_maps", "OUTDOORS3 line 30: map byte b draws shape 37 + INT(b / 10) in HCOLOR INT(1 + b / 20), 7 x 6 cells, " +
                           "a 10 x 15 region at a time (OT files). Tiles here are keyed by the remaster's (DOS) map byte, drawn " +
                           "as the Apple class the Apple's own maps give it (GRAPHICS.md section 5); tiles 300 + class are the " +
                           "class pictures, which tile_swaps draws for the Netherworld's own terrain. PATHWAY / BRIDGE are drawn " +
                           "in HCOLOR 4 (black) or are empty shapes: not visible, as on the Apple. DUNGPLOT / DUNGON3 5030: 3 x 3 " +
                           "marks, only next to the party; wall 31; 185-199 32, 200-214 33, 215-217 drawn black (glyph 0), " +
                           "218-220 shape 35, empty in III (glyph 0), 221+ 36; any other byte erases the cell (0); the box starts " +
                           "white (DNGLD3 200: glyph 99, not yet seen); the party is the XDRAW cursor 30."),
            ("tile_maps", J.O(
                ("overworld_tile", J.Ints([7, 6])),
                ("overworld_view", J.Ints([10, 15])),
                ("dungeon_glyph", J.Ints([3, 3])),
                ("dungeon_unseen", P3Apple2Art.UnseenGlyph),
                ("dungeon_party", 30),
                ("dungeon_default", 0),
                ("dungeon_cells", J.A(new[] { new[] { 0, 1, 31 }, [185, 199, 32], [200, 214, 33], [215, 217, 0], [218, 220, 0], [221, 255, 36] }
                    .Select(c => (JsonNode?)J.Ints(c)))),
                ("tile_swaps", index["tile_swaps"]!.DeepClone()))));
        if (screens.ContainsKey("title"))
            pack["title_sequence"] = J.O(
                ("_note", "PHANTASIE III (boot program): ICE DRAGON on hi-res page 1, a FOR-loop pause, then the 7 rows of " +
                          "'HIT ANY KEY TO CONTINUE' POKEd in (lines 245-290) and a wait for a key; a key at any point goes on " +
                          "(GOSUB 300). The pauses are BASIC FOR loops (time not decoded [?]): only the last screen is shown, " +
                          "until a key."),
                ("canvas", J.Ints([280, 192])),
                ("background", "#000000"),
                ("steps", J.A([J.O(("picture", "title.credits"))])));
        pack["pictures"] = pictures;
        pack["_portraits"] = "portrait.{race}: the party figure (TOWN3 2350-2360: races 7+ shape 26, DWARF / HALFLING / GNOME " +
                             "/ PIXIE / SPRITE 25, HUMAN / ELF 19, or 27 for a MONK / PRIEST / WIZARD; social class 3 turns " +
                             "any but 25 into 4, which a race-keyed picture cannot show) with the arm 20 (idle) or 22 " +
                             "(attacking). Every body by pose is in figures/.";
        pack["picture_sets"] = J.A(new[]
        {
            ("monster.{n}.idle", "monsters/idle", "{n:D2}-"), ("monster.{n}.attacking", "monsters/attacking", "{n:D2}-"),
            ("portrait.{n}.idle", "portraits/idle", "{n:D2}-"), ("portrait.{n}.attacking", "portraits/attacking", "{n:D2}-"),
            ("overworld.tile.{n}", "overworld/tiles", "{n:D3}."), ("dungeon.cell.{n}", "dungeon", "glyph-{n}."),
        }.Select(s => (JsonNode?)J.O(("name", s.Item1), ("dir", s.Item2), ("prefix", s.Item3), ("ext", ".png"))));
        pack["apple2_field"] = Field();
        return pack;
    }

    /// <summary>The note on <see cref="Field"/>'s block (GRAPHICS.md section 9).</summary>
    public const string FieldNote =
        "OUTDOORS3 / DUNGON3 (the same drawing lines, v1.0 and v3.0) 1540-2006, 4000-4160, 9100-9220 (GRAPHICS.md section 9): " +
        "the round plays on the game's one hi-res screen (SCREEN, the index's screens.frame): the field box bottom right, the " +
        "message box (650: columns 17-38, rows 9-11) above it, the party columns (600-635) on top; the options and orders " +
        "(1068-1470) are the remaster's screen. monster_plot: MONSTER PLOT sprites (whole bytes) with their top-left at byte " +
        "column 21 + 4 * INT(5 * (i + .5) / n) (1550), the front rank (the larger group, 1047) at y 120 (99 for S% 21, a giant), " +
        "the second at 96 (1540); pose B from a swing, spell or breath to the round's end (2006); blanked at the death (4145). " +
        "Party slot k at (147 + 19k, 198 - 8 * (row + 1)) (9100: back 190, middle 182, front 174): a FIRE order draws shape 2 " +
        "for the figure, 3 once it shot (9200, 1624); a hurt leg kneels (shape 6 + the arm); else the body (TOWN3 2350-2360, " +
        "social class 3: shape 4 but over 25) with arm 20, 22 from a swing (1695), 5 from a cast (1760), 21 for a parry that " +
        "took (1527 / 9210); a member's death erases the figure (4025). The sparkle (23) at the figure's point + (14, -23) " +
        "during the cast tones (1762-1764); 4132 XDRAWs shape 47 - 23 * SP at (7 * X% + 3, Y% + 20) for 500 loops: the spell " +
        "hit (24, SP = 1) or the arrow of a bow hit (1, SP = 2); a bow miss (1628) the arrow at (220, 104), (220, 105) in " +
        "DUNGON3, for 250 loops, then the PA pause. Pauses: PA = 120 * TIME LAG loops a line (STARTER 361, 7560); loop_ms a " +
        "rule of thumb for an empty Applesoft FOR / NEXT pass [~]; sparkle_ms the cast tones' length (CALL 770 not timed) [?]. " +
        "The elemental (slot 6: its byte 168 is not known [?]) is not drawn.";

    /// <summary>
    /// The pack's <c>apple2_field</c> for III (s228): the shared block's numbers (screen place, text window, pauses, bodies)
    /// plus <c>monster_plot</c>, III's own field (OUTDOORS3 / DUNGON3; GRAPHICS.md section 9).
    /// </summary>
    public static JsonObject Field() => J.O(
        ("_note", FieldNote),
        ("screen_x", 20), ("screen_y", 4), ("width", 280), ("graphics_rows", 192), ("divider", 0),
        ("lines", new JsonArray()),
        ("party_step", 19),
        ("text_top", 9), ("text_rows", 3), ("text_left", 17), ("columns", 22), ("cell_width", 7), ("cell_height", 8),
        ("index", "extraction-index.json"),
        ("line_loops_per_time_lag", 120), ("hit_loops", 500), ("loop_ms", 1.0), ("sparkle_ms", 300.0),
        ("bodies", J.O(("monster_races_from", 7), ("monster_body", 26), ("human_elf", J.Ints([0, 2])),
            ("small_body", 25), ("plain_body", 19), ("robed_classes", J.Ints([1, 4, 5])), ("robed_body", 27),
            ("elemental_body", 0), ("noble_class", 3), ("noble_body", 4))),
        ("monster_plot", J.O(
            ("party_x", 147), ("party_base_y", 198), ("rank_step", 8), ("rank_ys", J.Ints([120, 96])), ("giant_y", 99),
            ("giant_shape", 21), ("column", 21), ("column_step", 4), ("slots", 5), ("byte_pixels", 7),
            ("hit", J.Ints([3, 20])), ("sparkle", J.Ints([14, -23])), ("arrow_miss", J.Ints([220, 104])),
            ("arrow_miss_dungeon", J.Ints([220, 105])), ("arrow_miss_loops", 250), ("arrow_hit_loops", 500), ("frame", true),
            ("boxes", J.O(("column", 4), ("step", 6), ("width", 5), ("name_row", 0), ("class_row", 1), ("magic_row", 2),
                ("hits_row", 3), ("body_row", 5), ("status_dx", 4), ("marks", "OIBG"))))));
}
