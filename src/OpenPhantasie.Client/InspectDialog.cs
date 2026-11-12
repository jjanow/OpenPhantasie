using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Inspect. Wilderness / dungeon Options → Inspect is OV2 <c>0x464a</c> [C] (s49 static): a party summary
/// (<see cref="SummaryLines"/>), then menu <c>0x15</c> "INSPECT MEMBER?" NO / YES; YES → `inspect.whom_do_you_want_to_inspect`
/// (picker mode 7), repeated until cancelled. Town and Guild go straight to the picker (manual p.11 "INSPECT PARTY MEMBERS").
/// A member's view is COM <c>0x2aa6</c> [C]: the stat sheet (<see cref="SheetLines"/>) with menu <c>0xa</c>
/// SPELLS / EQUIPMENT / SKILLS / POTIONS; a pick shows that page (<see cref="SpellRows"/>, <see cref="EquipmentLines"/>,
/// <see cref="SkillLines"/>, <see cref="CounterLines"/>), a key returns to the sheet and menu (SPELLS: Return or Esc), ESC
/// leaves. Both menus are
/// DOS cursor menus with memory (<see cref="PromptMenu"/>). Text comes from the COM/OV2 format strings.
/// </summary>
public sealed class InspectDialog : IModalDialog, IPagedDialog, IPadArrows, IPadWheel
{
    /// <summary>Pad fix B7 (s188): the pad wheel on "whom" (the other steps take the D-pad / A) (<see cref="IPadWheel"/>).</summary>
    public string? PadWheel(GameSession session) => _step == Step.Who ? DialogPad.Picks(session.Party.Members.Count) : null;

    /// <summary>Pad fix B8 (s189): the member the wheel's number inspects (<see cref="IPadWheel.PadPickName"/>).</summary>
    public string? PadPickName(GameSession session, GameDataRepository data, int pick) =>
        _step == Step.Who && pick <= session.Party.Members.Count ? session.Party.Members[pick - 1].Name : null;

    private enum Step { Closed, Summary, AskMember, Who, Sheet, Page }

    /// <summary>Menu <c>0x15</c> (labels "INSPECT MEMBER?" / NO / YES — NO is the first option).</summary>
    public static SessionPrompt InspectMemberPrompt { get; } = new(SessionPromptKind.None, [UiText.S("inspect.inspect_member")], [UiText.S("inspect.no"), UiText.S("inspect.yes")]) { MenuId = 0x15 };

    /// <summary>Menu <c>0xa</c> under the sheet; its first-ever highlight is SKILLS (s58 DOSBox).</summary>
    public static SessionPrompt PageMenu { get; } =
        new(SessionPromptKind.None, [UiText.S("inspect.inspect")], [UiText.S("inspect.spells"), UiText.S("inspect.equipment"), UiText.S("inspect.skills"), UiText.S("inspect.potions")]) { MenuId = 0xa };

    /// <summary>INSPECT MEMBER?'s YES / NO box and the page menu move with Left / Right (the pad keeps them); an open page
    /// types nothing, so it has no pad wheel either (s182 live pass: the WHOM step's wheel hint stayed on it).</summary>
    public bool TakesLeftRight => _step is Step.AskMember or Step.Sheet or Step.Page;

    public static string WhoTitle => UiText.S("inspect.whom_do_you_want_to_inspect"); // OV2 DS 0xbe73

    /// <summary>Most text lines a modal panel shows in the 150-pixel content area (10 px a line).</summary>
    public const int MaxLines = 13;

    /// <summary>Width of the sheet's stat rows in layout columns; the EQUIPMENT block wraps at it (measured with the font).</summary>
    private const int SheetWidth = 35;

    private readonly PromptMenu _menus;
    private readonly GameDataRepository _data;
    private readonly SpellPicker _spellPicker;
    private Step _step;
    private Character? _who;
    private bool _sheetOnly; // s158: opened on one member's sheet (Guild › Examine of a roster member): Esc closes
    private int _page;
    private int _scroll;

    public InspectDialog(PromptMenu menus, GameDataRepository data, SpellPicker? spellPicker = null)
    {
        _menus = menus;
        _data = data;
        _spellPicker = spellPicker ?? new SpellPicker();
    }

    private bool OnSpellPage => _step == Step.Page && _page == 0;

    public bool IsOpen => _step != Step.Closed;

    /// <summary>s154: the member whose sheet is up (the stat sheet with its page menu), or null on any other step.</summary>
    public Character? SheetMember => _step == Step.Sheet ? _who : null;

    /// <summary>s192 (pad fix C18): the page menu's highlight on the sheet (0 spells … 3 potions; requester 14's button index).</summary>
    public int SheetCursor => _menus.CursorFor(PageMenu);

    /// <summary>s156: the open page (0 spells, 1 equipment, 2 skills, 3 potions) and its member, or null on any other step.</summary>
    public (int Page, Character Member)? PageShown => _step == Step.Page && _who is { } who ? (_page, who) : null;

    /// <summary>
    /// s158: rows a page of I Amiga's SPELLS view shows when it is requester 12 (h29 <c>+0xe90</c> → h23 <c>+0x570(record
    /// +0x60, 1)</c>, browse only), or null (the remaster's page with the shared spell cursor). The Client sets it.
    /// </summary>
    public int? AmigaSpellRows { get; set; }

    /// <summary>s158: the first spell of requester 12's page (h23 <c>+0x570</c>'s scroll offset; 0 when the view opens).</summary>
    public int AmigaSpellFirst { get; private set; }

    /// <summary>
    /// s165: counts the who list's answers (a member picked, or Esc / Backspace). In the Guild, I Amiga's Inspect lists in
    /// h26 <c>+0x346</c> → <c>+0x412</c> (`inspect.whom_do_you_want_to_inspect`, h5 <c>+0x196a</c>), which plays the tone after
    /// each gadget (<c>+0x614</c>); the Client plays it there only.
    /// </summary>
    public int WhoTones { get; private set; }

    public string Help => _step switch
    {
        Step.Summary => UiText.S("inspect.any_key"),
        Step.AskMember => UiText.S("inspect.arrows_enter"),
        Step.Who => UiText.S("inspect.1_7_inspect_esc_done"),
        Step.Sheet => UiText.S("inspect.arrows_enter_page_esc_back"),
        Step.Page when _page == 0 => UiText.S("inspect.up_down_move_enter_esc_back"),
        Step.Page => UiText.S("inspect.up_down_scroll_any_key_back"),
        _ => "",
    };

    public string? Open(GameSession session)
    {
        if (session.Party.Members.Count == 0)
            return UiText.S("inspect.no_party");
        _step = session.Mode is GameMode.Overworld or GameMode.Dungeon ? Step.Summary : Step.Who;
        _who = null;
        _sheetOnly = false;
        return null;
    }

    /// <summary>
    /// s158: straight to <paramref name="member"/>'s sheet and page menu (III's Guild › Examine of any roster member,
    /// Amiga <c>0xbabc</c> → <c>0xd796</c>, DOS guild_menu item 6 → root <c>0x86f0</c>); Esc on the sheet closes the dialog
    /// (the picker that opened it asks again).
    /// </summary>
    public void OpenSheet(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        _who = member;
        _sheetOnly = true;
        _step = Step.Sheet;
        _menus.Sync(PageMenu);
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            switch (_step)
            {
                case Step.Summary:
                    _step = Step.AskMember;
                    _menus.Sync(InspectMemberPrompt);
                    break;
                case Step.AskMember:
                    if (key == Keys.Escape)
                        return Close();
                    if (_menus.HandleKey(InspectMemberPrompt, key) is { } yesNo)
                    {
                        _menus.Sync(null);
                        if (yesNo != 2)
                            return Close();
                        _step = Step.Who;
                    }
                    break;
                case Step.Who:
                    if (key is Keys.Escape or Keys.Back)
                    {
                        WhoTones++;
                        return Close();
                    }
                    var d = DialogKeys.Digit(key);
                    if (d >= 1 && d <= session.Party.Members.Count)
                    {
                        WhoTones++;
                        _who = session.Party.Members[d - 1];
                        _step = Step.Sheet;
                        _menus.Sync(PageMenu);
                    }
                    break;
                case Step.Sheet:
                    if (key is Keys.Escape or Keys.Back)
                    {
                        if (_sheetOnly)
                            return Close();
                        _menus.Sync(null);
                        _step = Step.Who; // the picker repeats until cancelled
                        break;
                    }
                    if (_menus.HandleKey(PageMenu, key) is { } page)
                    {
                        _menus.Sync(null);
                        _page = page - 1;
                        _scroll = 0;
                        AmigaSpellFirst = 0;
                        _step = Step.Page;
                        if (_page == 0 && _who is not null)
                            _spellPicker.Fit(_who);
                    }
                    break;
                case Step.Page when _page == 0 && AmigaSpellRows is int rows && _who is not null:
                    // s158: I Amiga's requester 12 (h23 +0x570, browse): scroll gadget 2 / 3 (Up / Down) move a page of
                    // `rows` back / on (h23 +0x758 / +0x77c; nothing while ghosted); EXIT (Esc, Return, E) or a live row
                    // (its digit) ends it (h29 +0xe90 ignores the pick): back to the sheet.
                    var known = SpellPicker.Known(_who).Count;
                    var row = DialogKeys.Digit(key);
                    if (key == Keys.Up)
                        AmigaSpellFirst -= Math.Min(AmigaSpellFirst, rows);
                    else if (key == Keys.Down)
                        AmigaSpellFirst += known > AmigaSpellFirst + rows ? rows : 0;
                    else if (key is Keys.Enter or Keys.Escape or Keys.Back or Keys.E
                             || row >= 1 && row <= Math.Min(rows, known - AmigaSpellFirst))
                    {
                        _step = Step.Sheet;
                        _menus.Sync(PageMenu);
                    }
                    break;
                case Step.Page when _page == 0:
                    // COM 0x139c: Up/Down move the shared cursor; Return or Esc closes; other keys do nothing.
                    if (_who is not null && _spellPicker.Move(_who, key))
                        break;
                    if (key is Keys.Enter or Keys.Escape)
                    {
                        _step = Step.Sheet;
                        _menus.Sync(PageMenu);
                    }
                    break;
                case Step.Page:
                    if (key is Keys.Up or Keys.Down)
                    {
                        var max = Math.Max(0, PageLines().Count - MaxLines);
                        _scroll = Math.Clamp(_scroll + (key == Keys.Down ? 1 : -1), 0, max);
                        break;
                    }
                    _step = Step.Sheet;
                    _menus.Sync(PageMenu);
                    break;
            }
        }
        return null;
    }

    public ListPage? Page => _step == Step.Page ? ListPage.Of(0, MaxLines, _scroll, PageLines().Count) : null;

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) => _step switch
    {
        Step.Summary => SummaryLines(session),
        Step.AskMember => [.. InspectMemberPrompt.Lines, MenuBar(InspectMemberPrompt)],
        Step.Who => [WhoTitle, .. session.Party.Members.Select((m, i) => $"{i + 1} {m.Name}")],
        Step.Sheet when _who is not null => [.. SheetLines(_who, data, session.TreasuryOn), MenuBar(PageMenu)],
        Step.Page => PageLines().Skip(_scroll).Take(MaxLines).ToList(),
        _ => [],
    };

    /// <summary>Lines of the open page (before scrolling).</summary>
    private IReadOnlyList<string> PageLines() => _who is null ? [] : _page switch
    {
        0 => SpellRows(_who, _data),
        1 => EquipmentLines(_who, _data),
        2 => SkillLines(_who),
        _ => CounterLines(_who),
    };

    private string MenuBar(SessionPrompt menu)
    {
        var c = _menus.CursorFor(menu);
        return string.Join(" ", menu.Options.Select((o, i) => (i == c ? ">" : " ") + o));
    }

    private string Close()
    {
        _menus.Sync(null);
        _step = Step.Closed;
        _who = null;
        _sheetOnly = false;
        return "";
    }

    /// <summary>
    /// The wilderness / dungeon party summary (OV2 <c>0x464a</c>) [C]: <c>"DAY OUT%21u"</c> (time <c>&gt;&gt; 8</c>),
    /// <c>"`core.combat.x_experience_points_and_x_gold`%11lu"</c> (pool <c>[0x63f4]·10</c>; Core keeps whole XP, D-053), <c>"GOLD PIECES%17u"</c> (purse
    /// <c>[0x63f6]</c>; Core's carried + found gold, one purse vs two <c>[~]</c>), then for each found-item class with a count
    /// <c>"%-4.3d%s(S)"</c>: class <c>(id − 1) / 20</c> of the found items, names SHIELD, ARMOR, WEAPON, GOOD WEAPON,
    /// MG WEAPON, POTION, SCROLL, TREASURE, GOOD TREASURE (DS <c>0xbdd3</c>); ids past 180 are not counted.
    /// </summary>
    public static IReadOnlyList<string> SummaryLines(GameSession session)
    {
        var counts = new int[ClassNames9.Length];
        foreach (var id in session.Sack.ItemIds)
        {
            var c = (id - 1) / 20;
            if (id > 0 && c < counts.Length)
                counts[c]++;
        }
        var lines = new List<string>
        {
            UiText.F("inspect.day_out_x", session.Campaign.DaysOut(session.TravelTime)),
            UiText.F("inspect.experience_points_x", session.Sack.Experience),
            // Under the Guild treasury: everything the party can spend (11-guild-treasury.md section 5).
            UiText.F("inspect.gold_pieces_x", session.SpendableGold),
        };
        for (var i = 0; i < counts.Length; i++)
            if (counts[i] != 0)
                lines.Add(UiText.F("inspect.x_x_s", counts[i].ToString("000"), ClassNames9[i]));
        return lines;
    }

    private static string[] ClassNames9 =>
        [UiText.S("inspect.shield"), UiText.S("inspect.armor"), UiText.S("inspect.weapon"), UiText.S("inspect.good_weapon"), UiText.S("inspect.mg_weapon"), UiText.S("inspect.potion"), UiText.S("inspect.scroll"), UiText.S("inspect.treasure"), UiText.S("inspect.good_treasure")];

    // COM table 0x6dfe is DEAD / OK / SLEEP; D-064 (user s53): the manual's "OKAY".
    internal static string[] StatusNames => [UiText.S("inspect.dead"), UiText.S("inspect.okay"), UiText.S("inspect.sleep")];
    internal static string[] ClassNames => [UiText.S("inspect.thief"), UiText.S("inspect.monk"), UiText.S("inspect.ranger"), UiText.S("inspect.fighter"), UiText.S("inspect.priest"), UiText.S("inspect.wizard")]; // 0x6d43

    internal static string[] RaceNames => // 0x6d77
    [
        UiText.S("inspect.human"), UiText.S("inspect.dwarf"), UiText.S("inspect.elf"), UiText.S("inspect.halfling"), UiText.S("inspect.gnome"), UiText.S("inspect.pixie"), UiText.S("inspect.sprite"), UiText.S("inspect.ogre"), UiText.S("inspect.gnoll"), UiText.S("inspect.troll"), UiText.S("inspect.kobold"), UiText.S("inspect.orc"),
        UiText.S("inspect.goblin"), UiText.S("inspect.liz_man"), UiText.S("inspect.minotaur"), UiText.S("inspect.undead"),
    ];

    private static string[] RuneNames => [UiText.S("inspect.air"), UiText.S("inspect.earth"), UiText.S("inspect.fire"), UiText.S("inspect.water"), UiText.S("inspect.god")]; // @72..@80

    internal static string Named(string[] table, int index) =>
        (uint)index < (uint)table.Length ? table[index] : $"#{index}";

    /// <summary>
    /// The stat sheet (COM <c>0x2ef4</c>): name, <c>"LEVEL%3u %s %s %s"</c> = level <c>@88</c>, class, race, status (the manual's
    /// order "LEVEL 1 PRIEST HUMAN OKAY"), then STR/AGE (<c>@264 / 26</c>), INT/HP <c>@22/@20</c>, DEX/MAGIC <c>@18/@16</c>,
    /// CON/GOLD IN BANK, CHA/EXPER. POINTS, LUCK/SCORE, the EQUIPMENT block, and the <c>runes:</c> line — printed only if AIR
    /// <c>@72</c>, FIRE <c>@76</c> or WATER <c>@78</c> is set (DOS quirk, endgame.md §2.2), listing every rune held.
    /// D-064 (user s53, manual p.11 example): "OKAY", "EXPER. POINTS" and the equipment on the sheet (DOS: "OK", "EX. PTS",
    /// equipment only on its own page) — weapon, armor, shield as "CLUB (4)".
    /// </summary>
    /// <param name="treasury">The save keeps the Guild treasury (P3 QoL): no per-character gold, so the line drops it.</param>
    public static IReadOnlyList<string> SheetLines(Character ch, GameDataRepository? data = null, bool treasury = false)
    {
        var st = ch.Stats;
        var lines = new List<string>
        {
            ch.Name,
            UiText.F("inspect.level_x_x_x_x", ch.Level, Named(ClassNames, (int)ch.Class), Named(RaceNames, (int)ch.Race), Named(StatusNames, (int)ch.Status)),
            UiText.F("inspect.strength_x_age_x", st.Str, ch.AgeYears),
            UiText.F("inspect.intelligence_x_hit_points_x_x", st.Int, ch.HpCur, ch.HpMax),
            UiText.F("inspect.dexterity_x_magic_x_x", st.Dex, ch.MagicCur, ch.MagicMax),
            treasury ? UiText.F("inspect.constitution_x", st.Con) : UiText.F("inspect.constitution_x_gold_in_bank_x", st.Con, ch.GoldInBank),
            // DOS prints the points in an 11-wide field after "EX. PTS" (save-file-format.md), so a gap always stays; the
            // manual's longer "EXPER. POINTS" leaves 5, so a 5-digit or longer value keeps a space before it (s99).
            UiText.F("inspect.charisma_x_exper_points_x", st.Cha, ch.Experience >= 10000 ? $" {ch.Experience}" : ch.Experience.ToString()),
            UiText.F("inspect.luck_x_score_x", st.Luck, ch.Score),
        };
        if (data is not null)
        {
            lines.Add(UiText.S("inspect.equipment"));
            var row = "";
            // III's sheet lists the bow too (manual "the weapon, armor, shield, bow"); I / II have no bow slot (WornBow 0).
            int[] worn = ch.WornBow != 0
                ? [ch.WornWeapon, ch.WornArmor, ch.WornShield, ch.WornBow]
                : [ch.WornWeapon, ch.WornArmor, ch.WornShield];
            foreach (var id in worn)
            {
                var cell = $"{data.Items.FirstOrDefault(i => i.Id == id)?.Name ?? $"#{id}"} ({Points(id, data)})";
                if (row.Length > 0 && PixelFont.Measure(row + "  " + cell) > SheetWidth * PixelFont.Advance)
                {
                    lines.Add(row);
                    row = "";
                }
                row = row.Length == 0 ? cell : row + "  " + cell;
            }
            lines.Add(row);
        }
        var r = ch.Phantasie1.Runes;
        bool Has(int i) => i < r.Length && r[i] != 0;
        if (Has(0) || Has(2) || Has(3))
            lines.Add(UiText.S("inspect.runes") + string.Concat(Enumerable.Range(0, RuneNames.Length).Where(Has).Select(i => RuneNames[i])));
        return lines;
    }

    /// <summary>
    /// `inspect.spells_available` (COM <c>0x2b18</c> → <c>0x2864</c>) [C] (s58): the casting picker COM <c>0x139c</c>
    /// (<see cref="SpellPicker"/>) — names only, the bar fixed with 3 known spells above it and 2 below, on the cursor
    /// shared with every spell picker.
    /// </summary>
    public IReadOnlyList<string> SpellRows(Character ch, GameDataRepository data) =>
        [UiText.S("inspect.spells_available"), .. _spellPicker.Rows(ch, data)];

    /// <summary>
    /// Equipment (COM <c>0x2c65</c>): weapon <c>@86</c>, armor <c>@84</c>, shield <c>@82</c>, each <c>"%-13.12sPTS:%-2u"</c> with the
    /// points of <c>0x3053</c> (= <see cref="ItemRules.CombatGate"/>; a game with a point table, III, prints its table's).
    /// III adds a fourth row for a worn bow, as its sheet lists it (manual "the weapon, armor, shield, bow"); III's own
    /// window was not read, so the row's place and format are I's `[~]` (s206). I / II have no bow slot (WornBow 0).
    /// </summary>
    public static IReadOnlyList<string> EquipmentLines(Character ch, GameDataRepository data) =>
        ch.WornBow != 0
            ? [EquipLine(ch.WornWeapon, data), EquipLine(ch.WornArmor, data), EquipLine(ch.WornShield, data), EquipLine(ch.WornBow, data)]
            : [EquipLine(ch.WornWeapon, data), EquipLine(ch.WornArmor, data), EquipLine(ch.WornShield, data)];

    private static string EquipLine(int id, GameDataRepository data)
    {
        var name = data.Items.FirstOrDefault(i => i.Id == id)?.Name ?? $"#{id}";
        if (name.Length > 12)
            name = name[..12];
        return UiText.F("inspect.x_pts_x", name, Points(id, data));
    }

    /// <summary>
    /// The item's points: the game's table (<c>items.json</c> <c>points</c>; III's <c>DS:0x113c</c>; I's and II's files hold
    /// I's formulas), else <see cref="ItemRules.CombatGate"/>.
    /// </summary>
    private static int Points(int id, GameDataRepository data) =>
        id <= 0 ? 0 : data.Items.FirstOrDefault(i => i.Id == id)?.Points ?? ItemRules.CombatGate(id).Points;

    /// <summary>
    /// Skills (COM <c>0x2b7e</c>), the COM label order: attack, parry, spot trap, disarm trap, pick lock, find item, swim,
    /// listen (<c>"attack%7u"</c> …), two per row.
    /// </summary>
    public static IReadOnlyList<string> SkillLines(Character ch)
    {
        var k = ch.Skills;
        string[] cells =
        [
            UiText.F("inspect.attack_x", k.Attack), UiText.F("inspect.parry_x", k.Parry), UiText.F("inspect.spot_trap_x", k.SpotTrap), UiText.F("inspect.disarm_trap_x", k.DisarmTrap),
            UiText.F("inspect.pick_lock_x", k.PickLock), UiText.F("inspect.find_item_x", k.FindItem), UiText.F("inspect.swim_x", k.Swim), UiText.F("inspect.listen_x", k.Listen),
        ];
        return Enumerable.Range(0, cells.Length / 2).Select(i => $"{cells[2 * i],-19}{cells[2 * i + 1]}").ToList();
    }

    /// <summary>
    /// The counters (COM <c>0x2cf6</c> → <c>0x2da5</c>) [C], counted from the nine carried items: <c>"%-3uhl potions"</c> (101–110),
    /// <c>"%-3umg potions"</c> (111–120), <c>"%-3uscroll(s)"</c> (121–140), <c>"%-3utreasures"</c> (151+); items 141–150 write
    /// letter <c>id − 76</c> ('A'..) at position <c>id − 141</c> of a 10-space buffer. `inspect.the_wand_of_nikademus` if position 9 (WAND
    /// 150) is set; then the buffer is cut to 9 and <c>"RING:%s"</c> is printed if any of 141–150 is carried (so a wand
    /// alone prints "RING:" with blanks).
    /// </summary>
    public static IReadOnlyList<string> CounterLines(Character ch)
    {
        var buf = Enumerable.Repeat(' ', 10).ToArray();
        var anyRing = false;
        foreach (var id in ch.InventoryItemIds.Where(id => id is >= 141 and <= 150))
        {
            buf[id - 141] = (char)(id - 76);
            anyRing = true;
        }
        var lines = new List<string>
        {
            // Columns 4 and 21 of the DOS box.
            UiText.F("inspect.x_hl_potions", ch.HealingPotions).PadRight(17) + UiText.F("inspect.x_mg_potions", ch.MagicPotions),
            UiText.F("inspect.x_scroll_s", ch.ScrollCount).PadRight(17) + UiText.F("inspect.x_treasures", ch.Treasures),
        };
        if (anyRing)
            lines.Add(UiText.S("inspect.ring") + new string(buf, 0, 9));
        if (buf[9] != ' ')
            lines.Add(UiText.S("inspect.the_wand_of_nikademus"));
        return lines;
    }
}
