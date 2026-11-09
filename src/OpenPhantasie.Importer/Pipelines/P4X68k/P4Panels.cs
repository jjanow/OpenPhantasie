using System.Buffers.Binary;
using System.Text.Json.Nodes;
using static OpenPhantasie.Importer.Pipelines.P4X68k.P4Util;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>
/// graphics/x68000-panels.json: P4.X's Command-panel tables (port of <c>tools/scripts/x68k_p4_panels.py</c>).
/// <para>The Python finds the <c>_mkicon</c> call sites and their pushed arguments by disassembling P4.X with objdump
/// and asserts every other constant against the disassembly. P4.X is pinned by sha1 (the profile's key file), so the
/// importer keeps what that scan found as a recipe (call site, entry count, table and title addresses: numbers only)
/// and reads the labels, keys, icons and Shift-JIS text from the player's P4.X. The disassembly checks stay the
/// Python's job.</para>
/// </summary>
internal static class P4Panels
{
    private const int DefaultTitle = 0x401D2;

    // (name, _mkicon call site, count, table, title address) as x68k_p4_panels.py's scan finds them.
    private static readonly (string Name, int Site, int Count, int Table, int Title)[] MkIcon =
    [
        ("_about", 0xB7A, 1, 0x3F242, 0x3F416),
        ("_acend", 0xDCC, 2, 0x3F3C2, 0x3F48A),
        ("_acend.2", 0xE2E, 2, 0x3F3C2, 0x3F47B),
        ("_acswitch", 0x1076, 1, 0x3F242, DefaultTitle),
        ("_acsound", 0x1108, 3, 0x3F302, 0x3F4ED),
        ("_syscon", 0x11B8, 6, 0x3F242, 0x3F4F4),
        ("_townmenu.no_party", 0x2818, 1, 0x3F7C4, DefaultTitle),
        ("_townmenu", 0x2944, 6, 0x3F724, DefaultTitle),
        ("_mage", 0x39C0, 1, 0x3FB1C, DefaultTitle),
        ("_trang", 0x4520, 1, 0x3FB1C, DefaultTitle),
        ("_guildmenu", 0x4716, 2, 0x3FA9C, DefaultTitle),
        ("_bank", 0x5878, 3, 0x3FE06, DefaultTitle),
        ("_spc_wait", 0x6A66, 1, 0x40172, DefaultTitle),
        ("_confirm", 0x6AA6, 2, 0x40192, DefaultTitle),
        ("_mlistsubset", 0x740C, 1, 0x402A2, DefaultTitle),
        ("_inspect", 0x7740, 1, 0x402A2, 0x40540),
        ("_party", 0x7C5C, 1, 0x402A2, 0x40560),
        ("_mlistmenu", 0x7D1C, 2, 0x40382, DefaultTitle),
        ("_distribute", 0xA89A, 2, 0x40B6C, DefaultTitle),
        ("_trans", 0xCBE0, 2, 0x4174A, 0x42452),
        ("_trans.2", 0xCC76, 1, 0x4178A, DefaultTitle),
        ("_htran", 0xCE36, 2, 0x4170A, DefaultTitle),
        ("_hprofmenu", 0xD6A0, 1, 0x417CA, 0x42475),
        ("_hhist", 0xDA5A, 2, 0x418FE, DefaultTitle),
        ("_hall", 0xDBB8, 4, 0x41986, DefaultTitle),
        ("_armory", 0xEA5A, 1, 0x42520, DefaultTitle),
        ("_setuse", 0xFE84, 1, 0x42598, DefaultTitle),
        ("_trade", 0x10DEA, 3, 0x42718, DefaultTitle),
        ("_show_gm", 0x11126, 2, 0x42796, 0x42806),
        ("_makemenu", 0x1193E, 3, 0x4289E, DefaultTitle),
        ("_make1menu", 0x11B1C, 3, 0x42968, DefaultTitle),
        ("_make5menu", 0x13D34, 4, 0x42C3A, DefaultTitle),
        ("_ocast", 0x15474, 1, 0x42EAC, 0x432AE),
        ("_ocast.2", 0x1552C, 1, 0x42EAC, DefaultTitle),
        ("_outdoormenu", 0x15704, 6, 0x42DEC, DefaultTitle),
        ("_dsetup", 0x17C44, 6, 0x43598, DefaultTitle),
        ("_timelag", 0x1CBA6, 1, 0x43A66, 0x43F4C),
        ("_findit", 0x24716, 2, 0x4438A, 0x44469),
        ("_cast_47", 0x2D284, 1, 0x44E32, 0x452F4),
        ("_setident", 0x2DD8E, 1, 0x454EC, DefaultTitle),
    ];

    // s140: icon rows outside _mkicon: name -> (site, table, count, x0, dx)
    private static readonly (string Name, int Site, int Table, int Count, int X0, int Dx)[] Rows =
    [
        ("_detailmenu", 0x8E6E, 0x40A0A, 2, 32, 52),
        ("_p_info3menu", 0x815A, 0x4061C, 2, 32, 52),
        ("_page3", 0x11E66, 0x42A1E, 2, 32, 52),
        ("_p_info1menu", 0x163FC, 0x43312, 1, 64, 0),
    ];

    private const int NoyesSite = 0x6066, NoyesYes = 0x4733A, NoyesNo = 0x4733E;
    private const int EwaitSetbtn = 0x1883A, EwaitLabel = 0x43796, TypedLetters = 0x437D8, TypedNumber = 0x437DF;
    private const int NuminSite = 0x5538, NuminTable = 0x472AA;
    private const int DiskSite = 0x1C58, DiskTable = 0x3F5C8, DiskTitle = 0x3F6BA, DiskCaption = 0x3F6C6;
    private const int AcgamendSite = 0xEA0, AcgamendTable = 0x3F362, AcgamendTitle = 0x3F4C1;
    private const int TrgTitleText = 0x3FD0C;
    private const int EndHeadText = 0x413C8, EndScoreText = 0x413D5, EndScoreAfterText = 0x413E4, EndCodeText = 0x413EC;
    private const int EndWordText = 0x413FB, EndCodeAfterText = 0x41400, EndTable = 0x40D1C;
    private const int DeadLine1Text = 0x44097, DeadLine2Text = 0x440B0, GateOlderText = 0x3FA6F, GateDiedText = 0x3FA89;

    // s162: the other titled windows: (name, _wopen site, x, y, width, height, title) as x68k_p4_panels.py reads them.
    private static readonly (string Name, int Site, int X, int Y, int W, int H, int Title)[] Titled =
    [
        ("_inn", 0x24F8, 160, 32, 312, 352, 0x3F6D6),
        ("_townent", 0x2ECA, 160, 32, 312, 352, 0x3FA65),
        ("_townent.2", 0x2F96, 160, 32, 312, 352, 0x3FA7F),
        ("_mage", 0x3A96, 160, 32, 312, 352, 0x3FC1C),
        ("_guildmenu", 0x4752, 160, 64, 312, 180, 0x3FDD5),
        ("_gatebank", 0x5326, 160, 132, 312, 160, 0x3FEAF),
        ("_bank", 0x596C, 160, 32, 312, 352, 0x3FEF8),
        ("_mystic", 0x6FD6, 160, 90, 312, 220, 0x40210),
        ("_distribute", 0xA81A, 160, 32, 312, 352, 0x40BD8),
        ("_endstory", 0xAC66, 160, 32, 312, 352, 0x413BB),
        ("_arathamus", 0xAD26, 160, 32, 312, 352, 0x41408),
        ("_banquet", 0xAE14, 160, 32, 312, 352, 0x4142E),
        ("_hall", 0xDCA2, 160, 52, 312, 312, 0x424FD),
        ("_armory", 0xEA0A, 160, 32, 312, 352, 0x4257B),
        ("_setuse", 0xFE34, 160, 32, 312, 352, 0x4270A),
        ("_trade", 0x10D9A, 160, 32, 312, 352, 0x4278D),
        ("_setident", 0x2DD3E, 160, 32, 312, 352, 0x45756),
    ];
    private const int SklTitleText = 0x4168B, SklHeaderText = 0x41698, SklEndText = 0x416F3;
    private const int TrgNeedText = 0x3FD34, TrgNeedAfterText = 0x3FD47, TrgCostText = 0x3FD5B, TrgCostAfterText = 0x3FD6E;
    private const int TrgQuestionText = 0x3FD8A, TrgShortText = 0x3FD97, TrgPoorText = 0x3FDAE, TrgPoor2Text = 0x3FDBF, TrgNothingText = 0x3FD13;

    private static string Text(byte[] img, int a) => CStr(img, a);

    private static string Key(int key) => key != 0x20 ? ((char)key).ToString() : "Space";

    private static JsonObject Icon(byte[] img, int e)
    {
        var icon = BinaryPrimitives.ReadInt32BigEndian(img.AsSpan(e + 20));
        var key = BinaryPrimitives.ReadInt32BigEndian(img.AsSpan(e + 24));
        return Obj(("icon", icon), ("key", Key(key)), ("label", P4xId(e)), ("text", Text(img, e)));
    }

    private static string Site(int a) => "0x" + a.ToString("X");

    private static JsonObject Win(int x, int y, int w, int h) => Obj(("x", x), ("y", y), ("width", w), ("height", h));

    public static JsonObject Build(byte[] img)
    {
        var panels = new JsonObject();
        foreach (var (name, site, count, table, title) in MkIcon)
        {
            var titleAddr = title == 0 || img[title] == 0 ? DefaultTitle : title;
            panels[name] = Obj(("site", Site(site)), ("title", P4xId(titleAddr)), ("title_text", Text(img, titleAddr)),
                ("icons", new JsonArray(Enumerable.Range(0, count).Select(k => (JsonNode)Icon(img, table + 32 * k)).ToArray())));
        }
        // s158: _acgamend(m) computes its call: _mkicon(3 - m, 0x3F362 + 32 * m, 1, "Select Game"); m = 0 (_syscon's E)
        // lists SPACE, E, T; m = 1 (_endstory, _deadmsg) only E and T.
        for (var m = 0; m < 2; m++)
        {
            var first = AcgamendTable + 32 * m;
            panels["_acgamend" + (m == 1 ? ".1" : "")] = Obj(("site", Site(AcgamendSite)), ("title", P4xId(AcgamendTitle)),
                ("title_text", Text(img, AcgamendTitle)),
                ("icons", new JsonArray(Enumerable.Range(0, 3 - m).Select(k => (JsonNode)Icon(img, first + 32 * k)).ToArray())));
        }
        var outObj = Obj(
            ("_schema", "phantasie.p4.panels/v1"),
            ("_source", "Phantasie IV X68000 P4.X _mkicon / _seticon (FORMAT.md 6.9 'Command panel'), extracted by " +
                        "tools/scripts/x68k_p4_panels.py (s136)."),
            ("_layout", "Pack pixels (P4.X's 640 x 400 window space). window = _wopen(34, x, y, width, height); icon i at " +
                        "(icon_x[i & 1], icon_top + icon_step * i) in the window, icon_size square (icon.pix record 'icon'); " +
                        "label in font 1 centred on icon x + icon_size / 2 at icon y + icon_size, first character colour 2, " +
                        "the rest colour 0 (all colour 2 when the label starts with a space). key = the hot key (_setbtn); " +
                        "the clickable box is the icon only."),
            ("window", Win(496, 48, 136, 324)),
            ("icon_x", ArrOf(38, 62)),
            ("icon_top", 8),
            ("icon_step", 52),
            ("icon_size", 32),
            ("panels", panels));

        var rows = new JsonObject();
        foreach (var (name, site, table, count, x0, dx) in Rows)
            rows[name] = Obj(("site", Site(site)), ("window", Win(314, 270, 150, 48)), ("sheet", Obj(("width", 472), ("height", 338))),
                ("icon_x", Arr(Enumerable.Range(0, count).Select(k => x0 + dx * k))), ("icon_y", 2),
                ("icons", new JsonArray(Enumerable.Range(0, count).Select(k => (JsonNode)Icon(img, table + 32 * k)).ToArray())));
        outObj["_rows"] = "s140: icon rows outside _mkicon: _seticon(36, x, y) in window 36 (_wopen68, 150 x 48); s141: at " +
                          "(window.x, window.y) = (314, 270) of the sheet's area (window 35, 'sheet', 472 x 338: 8 pixels in " +
                          "from its right edge, 20 up from its bottom), flag 0 = no fill and no frame (the icons sit on the " +
                          "sheet's grey, colour 14); same picture / label rules as the panels. icon i at (icon_x[i], icon_y).";
        outObj["rows"] = rows;
        outObj["_noyes"] = "s140: _noyes(win, 104, 300) in the shipwright's window (_wopen(-1, 160, 32, 312, 352)): the frame " +
                           "_whole(x - 8, y - 8, 120, 32) sunken 2 pixels (colour 1 top / left, 15 bottom / right), buttons " +
                           "48 x 16 filled colour 4 at (x, y) and (x + 56, y) with their text centred in font 0; key Y or a click " +
                           "on YES = yes, any other key = no. s141: (x, y) is in the shipwright's window, whose origin is " +
                           "SHIP.PKH's (the picture's top-left); 'clear' = _wfbox(win, 14, x - 10, y - 10, 124, 36), the " +
                           "window's grey round the frame.";
        outObj["noyes"] = Obj(
            ("site", Site(NoyesSite)), ("window", Win(160, 32, 312, 352)), ("x", 104), ("y", 300),
            ("frame", Obj(("x", -8), ("y", -8), ("width", 120), ("height", 32), ("dark", 1), ("light", 15))),
            ("clear", Obj(("x", -10), ("y", -10), ("width", 124), ("height", 36))),
            ("button_colour", 4), ("back_colour", 14),
            ("buttons", ArrOf(
                Obj(("x", 0), ("y", 0), ("width", 48), ("height", 16), ("key", "Y"), ("label", P4xId(NoyesYes)), ("text", Text(img, NoyesYes))),
                Obj(("x", 56), ("y", 0), ("width", 48), ("height", 16), ("key", "N"), ("label", P4xId(NoyesNo)), ("text", Text(img, NoyesNo))))));

        outObj["_lists"] = "s151: lists P4.X draws as text buttons in window 34 (FORMAT.md 6.9 'Lists in window 34'). window = " +
                           "the list's own _wopen (absent: window 34 as the Command panel left it, title bar kept), panel = the " +
                           "_mkicon panel drawn first in the same window. head = text buttons over the list (_spelltop: text at " +
                           "(x, y), button (rows.button_x, y, rows.button_width, rows.button_height) on key); centred = a " +
                           "_wprcen line centred on x; rule = a _wbox outline; clear = the list area's _wfbox. Row i: the key " +
                           "('A' + i for keys 'letter', the digit i + 1 for 'digit') at (key_x, top + step * i), its text at " +
                           "text_x, extra_chars characters of a second field at extra_x, a button over the row; font 0, colour 0.";
        outObj["lists"] = Lists(img, panels);

        outObj["_title"] = "s151: _wttl: the title in the current font (0), style 0, colour 0, no cell box, centred on " +
                           "width >> 1 (x - (_fsize >> 1)) at y -18 of the window, i.e. 3 rows under the bar's black top line.";
        outObj["title"] = Obj(("font", 0), ("colour", 0), ("y", -18));
        outObj["_hover"] = "s151: _s_ms_get_cp (every _mcget pass of _mgetc's wait) XORs the button under the mouse pointer and " +
                           "puts it back when the pointer leaves (0x820BC = the lit button); _mgetc clears it before returning. " +
                           "_xbtn: type 64 (a plain button) = _xbox(5, ...) (each pixel's colour ^ 5), type 128 = nothing, else " +
                           "_xicon(type & 63): the icon's opaque pixels ^ 0xFFFF (colour ^ 15). A key press shows no look.";
        outObj["hover"] = Obj(("icon_xor", 15), ("box_xor", 5));
        outObj["_ewait"] = "s151: _ewait in the location map's message window (304 wide; _eprint fills its top 96 rows colour " +
                           "15): a colour-0 line (line), the label centred on label_x at label_y (font 0, colour 0), a plain " +
                           "button (type 64) over 'button' pressing Space; only Space or that click ends the wait. 'band' = " +
                           "INPUTS / INPUTN's colour-14 band over the same rows, their prompt at (prompt_x, band y).";
        outObj["ewait"] = Obj(
            ("site", Site(EwaitSetbtn)), ("message", Obj(("width", 304), ("height", 96), ("colour", 15))),
            ("line", Obj(("x", 0), ("y", 80), ("width", 304), ("height", 1), ("colour", 0))),
            ("label", P4xId(EwaitLabel)), ("text", Text(img, EwaitLabel)), ("label_x", 152), ("label_y", 82),
            ("button", Win(0, 81, 304, 15)), ("key", "Space"),
            ("band", Obj(("x", 0), ("y", 80), ("width", 304), ("height", 16), ("colour", 14))), ("prompt_x", 16),
            ("letters", P4xId(TypedLetters)), ("letters_text", Text(img, TypedLetters)),
            ("number", P4xId(TypedNumber)), ("number_text", Text(img, TypedNumber)));

        var keys = new JsonArray();
        for (var k = 0; k < 12; k++)
        {
            var o = NuminTable + 12 * k;
            int ch = BinaryPrimitives.ReadInt32BigEndian(img.AsSpan(o)), key = BinaryPrimitives.ReadInt32BigEndian(img.AsSpan(o + 4)),
                button = BinaryPrimitives.ReadInt32BigEndian(img.AsSpan(o + 8));
            keys.Add(Obj(("text", ((char)ch).ToString()), ("key", key switch { 8 => "Back", 13 => "Enter", _ => ((char)key).ToString() }),
                ("value", button)));
        }
        outObj["_numin"] = "s151: _numin's keypad in window 34 (the Bank's panel) at (x, y): key k at (x + step * (k % 3), " +
                           "y + step * (k / 3)), size x size, ' c ' in font 0 colour text_colour on cells of cell_colour; " +
                           "frame = _whole (sunken, dark top / left, light bottom / right) relative to (x, y), clear = the " +
                           "window grey drawn over it after; each key a plain button (type 64). value: 0-9 digits, 10 Return, " +
                           "11 erase. cursor = the steady block at the typed amount's end.";
        outObj["numin"] = Obj(
            ("site", Site(NuminSite)), ("window", 34), ("x", 40), ("y", 224), ("step", 20), ("size", 16), ("digits", 5),
            ("frame", Obj(("x", -8), ("y", -8), ("width", 72), ("height", 92), ("dark", 1), ("light", 15))),
            ("clear", Obj(("x", -10), ("y", -10), ("width", 76), ("height", 96))),
            ("cell_colour", 4), ("text_colour", 0), ("back_colour", 14), ("keys", keys),
            ("cursor", Obj(("width", 8), ("height", 16), ("colour", 0))));

        outObj["_shipwright"] = "s151: _shipyard's window (titled, so its grey is colour 14) with SHIP.PKH (picture_height rows) " +
                                "at its origin: the offer's lines start at 'lines' (the first at the window's text cursor, " +
                                "continued by _wprint), the answer centred on answer x at answer y after _noyes's box is " +
                                "cleared, the gold line at gold (x, y): label, the amount right-aligned in 'digits' digit " +
                                "widths (each leading zero = two spaces), the unit. Font 0, colour text_colour.";
        outObj["shipwright"] = Obj(
            ("window", Win(160, 32, 312, 352)), ("picture_height", 220), ("back_colour", 14),
            ("text_colour", 0), ("title", "p4x.0x400d4"), ("title_text", Text(img, 0x400D4)),
            ("lines", ArrOf(Obj(("x", 16), ("y", 236)), Obj(("x", 32), ("y", 252)), Obj(("x", 32), ("y", 268)))),
            ("answer", Obj(("x", 156), ("y", 300), ("yes", "p4x.0x40147"), ("no", "p4x.0x40160"))),
            ("gold", Obj(("x", 68), ("y", 336), ("label", "p4x.0x3fe66"), ("label_text", Text(img, 0x3FE66)), ("digits", 8),
                ("unit", "p4x.0x3fe76"), ("unit_text", Text(img, 0x3FE76)))));

        outObj["_disk"] = "s151: _diskmenu(0) (0x1AB4), the title's New Game / Saved Game panel (_disk(0) after _openning, no " +
                          "key between): window 34 as the panels' (title 'Select File'), New Game at icon place 0, then one " +
                          "'*k:Save' icon per save slot k (1-3) that exists (fames.k), at the next places 1, 2, 3; the label as " +
                          "written (its first character '*' in colour 2). One _mgetc: New Game = a new game (_load(0)), slot k " +
                          "= load save k; no other key does anything. _diskmenu(1) (the System menu's Save, _disk(1)): 'space' (table " +
                          "entry 1) at place 0, then all three slot icons at places 1-3 with the label's first byte '*' " +
                          "(saved) or ' ', and 'caption' centred on x at y in window 34 (_wkprcen(34, 68, 308, ...)); " +
                          "SPACE goes back, slot k = save k.";
        outObj["disk"] = Obj(("site", Site(DiskSite)), ("title", P4xId(DiskTitle)), ("title_text", Text(img, DiskTitle)),
            ("icons", new JsonArray(new[] { 0, 2, 3, 4 }.Select(k => (JsonNode)Icon(img, DiskTable + 32 * k)).ToArray())),
            ("space", Icon(img, DiskTable + 32)),
            ("caption", Obj(("x", 68), ("y", 308), ("label", P4xId(DiskCaption)), ("text", Text(img, DiskCaption)))));

        outObj["_sklup"] = "s152: _sklup's level-up skill window (FORMAT.md 6.9 'Level-up skill picks'): window 32 at " +
                           "(window.x, window.y), drawn with no fill or frame over _trang's titled window ('under', its grey " +
                           "back_colour); bar = the black box over its top with the title centred on title x at title y in " +
                           "title colour (at the end: the bar in done.back, done's line in done.colour); header at (x, y); " +
                           "frame = _whole (sunken, 2 pixels outside the rect); row i: its area cleared to rows.back, 'A' + i " +
                           "at key_x, the skill at skill_x, now and after as _wdouti(3) at now_x / after_x, a plain button " +
                           "(type 64) over (button x, y, width, height) on 'A' + i; the prompt at prompt (x, y) (cleared after " +
                           "each pick), the trained line at trained (x, y). Text in font 0, colour text_colour, cell boxes " +
                           "cell_colour (_font(-1, -1, -1, 14)). y < 0 is above the window (the bar).";
        outObj["sklup"] = Obj(
            ("site", "0xC0EE"), ("window", Win(160, 136, 312, 248)),
            ("under", Win(160, 32, 312, 352)), ("back_colour", 14),
            ("bar", Obj(("x", 0), ("y", -20), ("width", 312), ("height", 20), ("colour", 0))),
            ("title", Obj(("x", 156), ("y", -16), ("colour", 15), ("label", P4xId(SklTitleText)), ("text", Text(img, SklTitleText)))),
            ("done", Obj(("x", 156), ("y", -16), ("colour", 2), ("back", 14), ("label", P4xId(SklEndText)), ("text", Text(img, SklEndText)))),
            ("header", Obj(("x", 16), ("y", 20), ("label", P4xId(SklHeaderText)), ("text", Text(img, SklHeaderText)))),
            ("frame", Obj(("x", 4), ("y", 36), ("width", 304), ("height", 176), ("dark", 1), ("light", 15))),
            ("rows", Obj(("count", 11), ("top", 36), ("step", 16), ("key_x", 16), ("skill_x", 40), ("now_x", 112), ("after_x", 192),
                ("digits", 3), ("back", 14), ("button", Obj(("x", 4), ("width", 304), ("height", 16))))),
            ("prompt", Obj(("x", 16), ("y", 2))), ("trained", Obj(("x", 16), ("y", 224))),
            ("text_colour", 0), ("cell_colour", 14));

        JsonObject Seg(int a, params (string, object?)[] kw)
        {
            var o = Obj(kw);
            o["label"] = P4xId(a);
            o["text"] = Text(img, a);
            return o;
        }
        outObj["_trang"] = "s156: _trang's own titled window 32 (FORMAT.md 6.9 'Level-up skill picks'), opened again at each " +
                           "member pick (0x45F6): grey back_colour; the member's name at name (x, y); then the note's segments " +
                           "in order, each at its (x, y) when it has one, else at the text cursor after the last: a string " +
                           "(label / text) or the number (_wdoutl(32, 137, n): left-aligned, no padding) in its font / colour " +
                           "(default font 0, text_colour); short_of_gold follows the cost question at the cursor after a YES the " +
                           "purse cannot pay. A level-up's _sklup window (sklup) sits in its lower part.";
        outObj["trang"] = Obj(
            ("site", "0x3C30"), ("window", Win(160, 32, 312, 352)), ("back_colour", 14),
            ("title", P4xId(TrgTitleText)), ("title_text", Text(img, TrgTitleText)),
            ("text_colour", 0), ("line_step", 16), ("name", Obj(("x", 16), ("y", 16))),
            ("notes", Obj(
                ("poor_condition", ArrOf(Seg(TrgPoorText), Seg(TrgPoor2Text, ("x", 32), ("y", 32)))),
                ("nothing_more", ArrOf(Seg(TrgNothingText))),
                ("needs_experience", ArrOf(Seg(TrgNeedText), Obj(("x", 16), ("y", 32), ("number", true), ("font", 1), ("colour", 11)),
                    Seg(TrgNeedAfterText))),
                ("cost", ArrOf(Seg(TrgCostText), Obj(("x", 16), ("y", 32), ("number", true), ("font", 1), ("colour", 8)),
                    Seg(TrgCostAfterText), Seg(TrgQuestionText, ("x", 16), ("y", 48)))))),
            ("short_of_gold", Seg(TrgShortText)));
        outObj["_titled"] = "s162: the titled windows (_wopen(win, x, y, width, height, 1, title): window grey colour 14, the " +
                            "title bar and frame of _wwind / _wactive, FORMAT.md 6.9 'Window frames') that the buildings, the town " +
                            "gate, the item commands and the ending open in the picture's place; the training hall's (trang) and " +
                            "the shipwright's are their own entries.";
        var titled = new JsonObject();
        foreach (var (name, site, x, y, w, h, title) in Titled)
            titled[name] = Obj(("site", Site(site)), ("window", Win(x, y, w, h)), ("title", P4xId(title)),
                ("title_text", Text(img, title)));
        outObj["titled"] = titled;
        // s162: _endstory's final screen; its line table 0x40D1C holds lines 0-1, 2-7 and 8 (the question).
        var endLines = Enumerable.Range(0, 9).Select(i => (int)BinaryPrimitives.ReadUInt32BigEndian(img.AsSpan(EndTable + 4 * i)))
            .ToArray();
        outObj["_endstory"] = "s162: _endstory's final screen (0xA9E6) in its titled window (titled._endstory): the heading " +
                              "centred on heading.x at heading.y in font 0, style 8 (shadow: FORMAT.md 6.12), colour 1; then the " +
                              "segments in order, each at its (x, y) when it has one, else at the text cursor after the last: a " +
                              "string (label / text) or the score (_wdouti(32, digits, n)) in its font / colour (default font 0, " +
                              "text_colour); after the SPACE wait the question in font 1, style 1 (bold), colour 2.";
        var segments = new JsonArray(
            Seg(endLines[0], ("x", 32), ("y", 80)), Seg(endLines[1], ("x", 32), ("y", 96)),
            Seg(EndScoreText, ("x", 32), ("y", 128)), Obj(("number", true), ("digits", 3), ("font", 1), ("colour", 8)),
            Seg(EndScoreAfterText),
            Seg(EndCodeText, ("x", 32), ("y", 144)), Seg(EndWordText, ("font", 1), ("colour", 8)), Seg(EndCodeAfterText));
        for (var i = 2; i < 8; i++)
            segments.Add(Seg(endLines[i], ("x", 32), ("y", 80 + 64 + 16 * i)));
        outObj["endstory"] = Obj(("site", "0xA9E6"), ("text_colour", 0), ("line_step", 16),
            ("heading", Seg(EndHeadText, ("x", 140), ("y", 48), ("font", 0), ("style", 8), ("colour", 1))),
            ("segments", segments),
            ("question", Seg(endLines[8], ("x", 32), ("y", 80 + 64 + 16 * 9), ("font", 1), ("style", 1), ("colour", 2))));
        outObj["_townent"] = "s162: _townent's aging rows in its Town Gate window (titled._townent): member m's row at (x, y + " +
                             "step * m), the name then the line (grown_older / died); the ink as the window's (text_colour, not " +
                             "read: [~]).";
        outObj["townent"] = Obj(("site", "0x2CF2"), ("x", 8), ("y", 8), ("step", 16), ("text_colour", 0),
            ("grown_older", Seg(GateOlderText)), ("died", Seg(GateDiedText)));
        outObj["_deadmsg"] ="s162: _deadmsg (0x1E308), a wiped-out party: _xblack (screen: the whole 768 x 512 graphic screen " +
                             "filled colour 0), then its untitled window (window: no frame of _wwind's) filled colour fill, two " +
                             "frames (_wbox outlines) in frame_colour, the lines centred on x at y in font 0, style 1 (bold), " +
                             "colour text_colour; _sdbeep, any key; _xblack again before _acgamend(1).";
        outObj["deadmsg"] = Obj(("site", "0x1E308"), ("screen", Obj(("width", 768), ("height", 512), ("colour", 0))),
            ("window", Win(100, 168, 440, 64)), ("fill", 0),
            ("frames", ArrOf(Win(2, 2, 436, 60), Win(3, 3, 434, 58))),
            ("frame_colour", 15), ("font", 0), ("style", 1), ("text_colour", 15),
            ("lines", ArrOf(Seg(DeadLine1Text, ("x", 220), ("y", 16)), Seg(DeadLine2Text, ("x", 220), ("y", 32)))));
        BuildingWindows(outObj, img, Seg);
        return outObj;
    }

    /// <summary>s163: the inn's rows, the bank's tables and the ending's message window (x68k_p4_panels.py building_windows).</summary>
    private static void BuildingWindows(JsonObject outObj, byte[] img, Func<int, (string, object?)[], JsonObject> seg)
    {
        JsonObject Seg(int a, params (string, object?)[] kw) => seg(a, kw);
        JsonObject Rect(int x, int y, int w, int h, int colour) =>
            Obj(("x", x), ("y", y), ("width", w), ("height", h), ("colour", colour));
        outObj["_inn"] = "s163: _inn's rows in its titled window (titled._inn) under INN.PKH: the sunken box _whole(x, y, width, " +
                         "box_pad + row_step * party size) (2-pixel frame outside it, FORMAT.md 6.9), member m's row at (text_x, " +
                         "text_y + row_step * m) for each living member: the name, slept, and mp_up after it on the same row when " +
                         "the MP roll is won. Font 0, ink text_colour, on the window's grey.";
        outObj["inn"] = Obj(("site", "0x2188"), ("box", Obj(("x", 8), ("y", 210), ("width", 296))), ("box_pad", 4),
            ("row_step", 16), ("text_x", 16), ("text_y", 212), ("text_colour", 0), ("slept", Seg(0x3F6F0)),
            ("mp_up", Seg(0x3F6FC)));
        outObj["_bank"] = "s163: _bank / _deposit / _withdraw in the bank's titled window (titled._bank) under BANK.PKH: 'clear' " +
                          "the grey under the picture; accounts = _bank's table (sunken box, headers, a colour-0 line, member i's " +
                          "name and balance at y row_y + row_step * i, balances _wlocdi(digits): each leading zero two spaces); " +
                          "deposit / withdraw: a black band with the heading centred in colour 15, their box, headers, line and " +
                          "rows (y = row_y + row_step * m); the deposit's share buttons ' n ' (n = 0-3) on button_colour cells; " +
                          "after the shares 'clear_per' and the per-share line. gold = _disppcg's line (the shipwright's).";
        outObj["bank"] = Obj(
            ("site", "0x57A4"), ("clear", Rect(0, 180, 312, 172, 14)), ("text_colour", 0),
            ("accounts", Obj(("frame", Win(32, 194, 248, 116)),
                ("headers", ArrOf(Seg(0x3FF14, ("x", 64), ("y", 196)), Seg(0x3FF19, ("x", 216), ("y", 196)))),
                ("line", Rect(64, 211, 184, 1, 0)), ("row_y", 212), ("row_step", 16),
                ("columns", Obj(("name", 64), ("balance", 208))), ("digits", 5))),
            ("deposit", Obj(("band", Rect(0, 180, 312, 16, 0)), ("heading", Seg(0x3FE7A, ("x", 156), ("y", 180), ("colour", 15))),
                ("frame", Win(32, 210, 216, 116)),
                ("headers", ArrOf(Seg(0x3FE83, ("x", 64), ("y", 212)), Seg(0x3FE88, ("x", 148), ("y", 212)))),
                ("line", Rect(64, 227, 160, 1, 0)), ("row_y", 228), ("row_step", 16),
                ("columns", Obj(("name", 64), ("share", 196))),
                ("buttons", Obj(("x", 272), ("y", 240), ("step", 20), ("width", 24), ("height", 16), ("count", 4), ("colour", 4),
                    ("pad", Seg(0x3FE93)))),
                ("clear_per", Rect(0, 336, 312, 16, 14)), ("per_share", Seg(0x3FE99, ("x", 16), ("y", 336))), ("per_digits", 6),
                ("per_unit", Seg(0x3FEA8)))),
            ("withdraw", Obj(("band", Rect(0, 180, 312, 16, 0)), ("heading", Seg(0x3FED7, ("x", 156), ("y", 180), ("colour", 15))),
                ("frame", Win(16, 210, 280, 116)),
                ("headers", ArrOf(Seg(0x3FEE0, ("x", 32), ("y", 212)), Seg(0x3FEE5, ("x", 96), ("y", 212)),
                    Seg(0x3FEEA, ("x", 160), ("y", 212)), Seg(0x3FEF1, ("x", 224), ("y", 212)))),
                ("line", Rect(32, 227, 240, 1, 0)), ("row_y", 228), ("row_step", 16),
                ("columns", Obj(("name", 32), ("balance", 96), ("limit", 160), ("amount", 224))), ("digits", 5))),
            ("gold", Obj(("x", 68), ("y", 336), ("label", P4xId(0x3FE66)), ("label_text", Text(img, 0x3FE66)), ("digits", 8),
                ("unit", P4xId(0x3FE76)), ("unit_text", Text(img, 0x3FE76)))));
        JsonObject ItemRows() => Obj(("top", 64), ("step", 32), ("count", 9), ("letter_x", 16), ("icon_x", 24), ("name_x", 56),
            ("points_x", 208), ("points_digits", 3), ("users_x", 240), ("users_step", 8));
        outObj["_item_windows"] = "s163: the item windows' rows (FORMAT.md 6.9 'Item lists'; titled.<key> is each window): row i " +
                                  "at y = top + step * i, 32 high; the letter 'A' + i in letter_colour at (letter_x, y + 8), the " +
                                  "icon (font 2, 32 x 32) at (icon_x, y), the name at (name_x, y + 8), the points (ids 1-84) " +
                                  "padded to points_digits at points_x (or not_usable when the marked member cannot use it), the " +
                                  "price padded to price_digits at price_x, the users' digits m + 1 at users_x + users_step * m; " +
                                  "headers, the colour-0 line, sunken frames (_whole) and the member's name at their places. " +
                                  "s165: _distribute's equipment table under its divider (equipment: _init_eq / _disp_eq): the " +
                                  "headers at y header_y, a colour-0 line, member m's row at y row_y + row_step * m: m + 1 in " +
                                  "number_colour at number_x, the name at name_x, the class name at class_x cut while its width is " +
                                  "below class_width (_flen), each slot's value (record +6B / +B7 / +B6 / +6A / +69) padded to " +
                                  "value_digits at its x (clear: the values' cells cleared first); a member button over the row " +
                                  "(button x / width / height, key '1' + m); the placed item's slot boxed in the header (kind_box: " +
                                  "_wxbox XOR xor at the slot's x).";
        outObj["item_windows"] = Obj(
            ("letter_colour", 2), ("text_colour", 0), ("back_colour", 14),
            ("_armory", Obj(
                ("rows", Obj(("top", 32), ("step", 32), ("count", 6), ("letter_x", 32), ("icon_x", 44), ("name_x", 80),
                    ("points_x", 192), ("points_digits", 4), ("price_x", 240), ("price_digits", 5))),
                ("headers", ArrOf(Seg(0x42588, ("x", 80), ("y", 16)), Seg(0x4258D, ("x", 192), ("y", 16)),
                    Seg(0x42593, ("x", 248), ("y", 16)))),
                ("line", Rect(32, 31, 248, 1, 0)),
                ("frames", ArrOf(Win(16, 16, 280, 208), Win(245, 224, 51, 16))),
                ("more", Seg(0x42583, ("x", 253), ("y", 224))), ("not_usable", Seg(0x42540)),
                ("member", Obj(("x", 20), ("y", 256))), ("message", Obj(("x", 32), ("y", 288))))),
            ("_trade", Obj(("rows", ItemRows()),
                ("headers", ArrOf(Seg(0x4277C, ("x", 56), ("y", 48)), Seg(0x42781, ("x", 192), ("y", 48)),
                    Seg(0x42787, ("x", 240), ("y", 48)))),
                ("line", Rect(16, 63, 280, 1, 0)), ("member", Obj(("x", 16), ("y", 16))))),
            ("_distribute", Obj(
                ("rows", Obj(("top", 32), ("step", 32), ("count", 5), ("icon_x", 16), ("name_x", 48), ("points_x", 208),
                    ("points_digits", 3), ("users_x", 240), ("users_step", 8))),
                ("headers", ArrOf(Seg(0x40BAC, ("x", 48), ("y", 16)), Seg(0x40BB1, ("x", 192), ("y", 16)),
                    Seg(0x40BB7, ("x", 240), ("y", 16)))),
                ("line", Rect(16, 31, 280, 1, 0)),
                ("frames", ArrOf(Win(16, 208, 280, 4))), ("lit_xor", 15), ("equipment", EquipmentTable(Seg)))));
        outObj["_endmsg"] = "s163: _endmsg(block)'s window 33 for each ending page: untitled (white, colour 15, black frame and " +
                            "shadow) at window (x, y) of the screen, y + lower_offset for blocks > 3 other than 6; line k at " +
                            "(text_x, text_y + line_step * k), at most 3 a page, ink colour 0. _common_proc's Lord titles go in " +
                            "the same window (its first line). The pictures under it: the ending's street (origin (160, 20)), " +
                            "the banquet / cell windows (160, 32).";
        outObj["endmsg"] = Obj(("site", "0xA8B2"), ("window", Win(160, 68, 312, 52)), ("lower_offset", 232),
            ("upper_blocks", ArrOf(0, 1, 2, 3, 6)), ("text_x", 16), ("text_y", 4), ("line_step", 14), ("lines", 3),
            ("fill", 15), ("text_colour", 0),
            ("scene_y", Obj(("p4.ending.street", 20), ("p4.ending.banquet", 32), ("p4.ending.cell", 32))));
        ShareWindows(outObj, img, Seg);
    }

    /// <summary>s165: Share out's equipment table under its divider (x68k_p4_panels.py equipment_table).</summary>
    private static JsonObject EquipmentTable(Func<int, (string, object?)[], JsonObject> seg)
    {
        JsonObject Head(int a, int x) => seg(a, [("x", x), ("y", 224)]);
        return Obj(("site", "0x9F4E"), ("header_y", 224),
            ("headers", ArrOf(Head(0x40BBD, 16), Head(0x40BBF, 32), Head(0x40BC4, 136), Head(0x40BC9, 184), Head(0x40BCC, 208),
                Head(0x40BCF, 232), Head(0x40BD2, 256), Head(0x40BD5, 280))),
            ("line", Obj(("x", 16), ("y", 239), ("width", 280), ("height", 1), ("colour", 0))),
            ("row_y", 240), ("row_step", 16), ("number_x", 16), ("number_colour", 2), ("name_x", 32), ("class_x", 136),
            ("class_width", 32), ("value_digits", 2),
            ("values", ArrOf(Obj(("slot", "weapon"), ("x", 184)), Obj(("slot", "armour"), ("x", 208)),
                Obj(("slot", "shield"), ("x", 232)), Obj(("slot", "bow"), ("x", 256)), Obj(("slot", "thrown"), ("x", 280)))),
            ("clear", Obj(("x", 184), ("width", 112), ("colour", 14))),
            ("button", Obj(("x", 16), ("width", 280), ("height", 16))),
            ("kind_box", Obj(("y", 224), ("width", 16), ("height", 15), ("xor", 15))));
    }

    /// <summary>s165: _mage's own window and _gatebank's share-out window (x68k_p4_panels.py share_windows).</summary>
    private static void ShareWindows(JsonObject outObj, byte[] img, Func<int, (string, object?)[], JsonObject> seg)
    {
        JsonObject Seg(int a, params (string, object?)[] kw) => seg(a, kw);
        outObj["_mage"] = "s165: _mage's own titled window 32 (0x3A96), opened again at each member pick, as trang: the name at " +
                          "name (x, y), then one note: poor_condition, nothing_more (record +09 >= 125 or level > 30), " +
                          "not_worthy (else, when the quota is spent or no spell is open) or offer (the price: _wdoutl(32, 137, n) " +
                          "in font 1 colour 8 after 'each'); after a pick the result row is cleared (result_clear) and " +
                          "short_of_gold / results.failed / results.learned printed at (16, 64) in colour 2; it stays up through " +
                          "the next spell list.";
        outObj["mage"] = Obj(
            ("site", "0x34B0"), ("window", Win(160, 32, 312, 352)), ("back_colour", 14),
            ("title", P4xId(0x3FC1C)), ("title_text", Text(img, 0x3FC1C)),
            ("text_colour", 0), ("line_step", 16), ("name", Obj(("x", 16), ("y", 16))),
            ("notes", Obj(
                ("poor_condition", ArrOf(Seg(0x3FCE5), Seg(0x3FCF6, ("x", 32), ("y", 32)))),
                ("nothing_more", ArrOf(Seg(0x3FC27))),
                ("not_worthy", ArrOf(Seg(0x3FC48, ("x", 16), ("y", 32)))),
                ("offer", ArrOf(Seg(0x3FC6B, ("x", 120), ("y", 16)), Seg(0x3FC76, ("x", 16), ("y", 32)),
                    Obj(("number", true), ("font", 1), ("colour", 8)), Seg(0x3FC81), Seg(0x3FC97, ("x", 16), ("y", 48)))))),
            ("short_of_gold", Seg(0x3FCB4, ("x", 16), ("y", 64), ("colour", 2))),
            ("results", Obj(("failed", Seg(0x3FCC7, ("x", 16), ("y", 64), ("colour", 2))),
                ("learned", Seg(0x3FCD2, ("x", 16), ("y", 64), ("colour", 2))))),
            ("result_clear", Obj(("x", 0), ("y", 64), ("width", 311), ("height", 16), ("colour", 14))));
        outObj["_gatebank"] = "s165: _gatebank's share-out window (titled._gatebank, 312 x 160 at (160, 132)): the pool line (the " +
                              "party's experience x 10, _wdoutl(win, pool_digits)) then pool_unit; the share buttons ' n ' (n = " +
                              "0-3) at (x, y + step * n) on colour cells inside the sunken frame; member m's name at (name, row_y " +
                              "+ row_step * m), shown when his share is asked; the share digit at x share; after the last share " +
                              "each row's share is overwritten at x share by the experience given (_wdoutl(win, gain_digits)), " +
                              "then a SPACE wait.";
        outObj["gatebank"] = Obj(
            ("site", "0x4EF0"), ("text_colour", 0),
            ("pool", Seg(0x3FEBC, ("x", 68), ("y", 136))), ("pool_digits", 8), ("pool_unit", Seg(0x3FECC)),
            ("frame", Win(264, 32, 40, 92)),
            ("buttons", Obj(("x", 272), ("y", 40), ("step", 20), ("width", 24), ("height", 16), ("count", 4), ("colour", 4),
                ("pad", Seg(0x3FED1)))),
            ("row_y", 16), ("row_step", 16), ("columns", Obj(("name", 32), ("share", 164))), ("gain_digits", 8));
    }

    private static JsonObject Lists(byte[] img, JsonObject panels)
    {
        JsonObject Rows(int top, int key, int text, string keys, int width, int? extraX = null, int? extraChars = null)
        {
            var o = Obj(("top", top), ("step", 16), ("key_x", key), ("text_x", text));
            if (extraX is not null)
            {
                o["extra_x"] = extraX;
                o["extra_chars"] = extraChars;
            }
            o["keys"] = keys;
            o["button_x"] = 0;
            o["button_height"] = 15;
            o["button_width"] = width;
            return o;
        }
        JsonObject SpellWindow(JsonObject e)
        {
            e["window"] = Win(496, 32, 136, 360);
            e["title"] = P4xId(0x44C4F);
            e["title_text"] = Text(img, 0x44C4F);
            e["rule"] = Obj(("x", 0), ("y", 96), ("width", 136), ("height", 3), ("colour", 1));
            e["clear"] = Obj(("x", 0), ("y", 104), ("width", 136), ("height", 256), ("colour", 14));
            return e;
        }
        JsonArray HeadTable(int table, int count, int x, int y0, int dy) => new(Enumerable.Range(0, count).Select(k =>
        {
            var p = (int)BinaryPrimitives.ReadUInt32BigEndian(img.AsSpan(table + 4 * k));
            return (JsonNode)Obj(("x", x), ("y", y0 + dy * k), ("key", (k + 1).ToString()), ("label", P4xId(p)), ("text", Text(img, p)));
        }).ToArray());

        var lists = new JsonObject();
        var gm = SpellWindow(Obj(("function", "0x2BA96")));
        gm["head"] = ArrOf(Obj(("x", 8), ("y", 8), ("key", "Space"), ("label", P4xId(0x44C42)), ("text", Text(img, 0x44C42))));
        gm["rows"] = Rows(104, 4, 18, "letter", 136);
        lists["_getmagic"] = gm;
        var gm8 = SpellWindow(Obj(("function", "0x2BA96")));
        gm8["head"] = HeadTable(0x44A98 + 4 * 6, 6, 8, 0, 16);
        gm8["rows"] = Rows(104, 4, 18, "letter", 136);
        lists["_getmagic.8"] = gm8;
        var gm0 = SpellWindow(Obj(("function", "0x2BA96")));
        gm0["head"] = HeadTable(0x44A98, 6, 8, 0, 16);
        gm0["rows"] = Rows(104, 4, 18, "letter", 136);
        lists["_getmagic.0"] = gm0;
        if (!panels.ContainsKey("_cast_47"))
            throw new InvalidOperationException("_cast_47 panel missing");
        lists["_cast_47"] = Obj(("function", "0x2D0C6"), ("panel", "_cast_47"), ("rows", Rows(60, 4, 16, "letter", 136)));
        lists["_racemenu"] = Obj(("function", "0x12838"), ("rows", Rows(16, 4, 16, "digit", 135)));
        lists["_rracemenu"] = Obj(("function", "0x125C8"), ("rows", Rows(16, 4, 16, "digit", 135)));
        lists["_classmenu"] = Obj(("function", "0x1238A"), ("rows", Rows(16, 4, 16, "digit", 135)));
        lists["_set_fame_lst"] = Obj(("function", "0x638C"),
            ("centred", ArrOf(Obj(("x", 68), ("y", 8), ("label", P4xId(0x401DA)), ("text", Text(img, 0x401DA))))),
            ("rows", Rows(24, 4, 18, "letter", 135, 96, 3)));
        return lists;
    }
}
