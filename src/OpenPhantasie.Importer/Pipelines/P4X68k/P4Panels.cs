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
    private const int DiskSite = 0x1C58, DiskTable = 0x3F5C8, DiskTitle = 0x3F6BA;
    private const int AcgamendSite = 0xEA0, AcgamendTable = 0x3F362, AcgamendTitle = 0x3F4C1;
    private const int TrgTitleText = 0x3FD0C;
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
                          "= load save k; no other key does anything.";
        outObj["disk"] = Obj(("site", Site(DiskSite)), ("title", P4xId(DiskTitle)), ("title_text", Text(img, DiskTitle)),
            ("icons", new JsonArray(new[] { 0, 2, 3, 4 }.Select(k => (JsonNode)Icon(img, DiskTable + 32 * k)).ToArray())));

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
        return outObj;
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
