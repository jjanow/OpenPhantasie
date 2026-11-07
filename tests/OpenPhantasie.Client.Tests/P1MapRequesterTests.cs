using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s154: I Amiga's requesters 0, 22 and 14 (pack <c>map_requesters</c>, Amiga FORMAT.md "Requesters 0, 22 and 14") as
/// <see cref="P1MapRequesters"/> lays them out, rastered and compared pixel by pixel with the FS-UAE shots (file row =
/// screen row + 5).
/// </summary>
public class P1MapRequesterTests
{
    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;

    private static MapRequestersLayout Layout() => Pack().MapRequesters!;

    [OriginalDataFact]
    public void Parse_IsH86sSyntax()
    {
        var (lines, buttons) = P1MapRequesters.Parse("VILMAR|HEARS MONSTERS[RUN|FIGHT]");
        Assert.Equal(["VILMAR", "HEARS MONSTERS"], lines);
        Assert.Equal(["RUN", "FIGHT"], buttons);
        (lines, buttons) = P1MapRequesters.Parse(UiText.S("p1field.req.river_styx"));
        Assert.Equal(["THE RIVER STYX"], lines);
        Assert.Equal(["OK"], buttons);
    }

    [OriginalDataFact]
    public void Pack_ReadsTheThreeRequesters()
    {
        var l = Layout();
        Assert.Equal(new Box(81, 63, 158, 73), l.Menu!.Box);
        Assert.Equal(new Box(46, 70, 227, 60), l.BlackKnights!.Box);
        Assert.Equal(new Box(39, 30, 241, 139), l.Sheet!.Box);
        Assert.Equal("#00AAEE", l.Sheet.Palette[l.Sheet.NamePen]); // the town palette, stored pens
        Assert.Equal(15, l.BlackKnightsMenu);
        Assert.Equal("p1field.req.town_enter", l.Menus[0xe]);
        Assert.Equal("BLACK KNIGHTS!!!", UiText.S(l.BlackKnights.Title));
    }

    /// <summary>Shot <c>-00</c>: "VILMAR" / "HEARS MONSTERS" on rows 2 / 3, RUN and FIGHT; every pixel of the requester.</summary>
    [OriginalDataFact]
    public void Hears_MatchesTheShot()
    {
        var l = Layout();
        var scene = P1MapRequesters.Menu(l, UiText.F(l.Menus[0x11], "VILMAR"), i => Keys.D1 + i);
        Assert.Equal(0, Mismatch("p1-amiga-combat-00-hears-monsters.png", scene, l.Menu!.Box));
        // The check sees a half-cell shift: a trailing blank moves the centred name 3 px left.
        var shifted = P1MapRequesters.Menu(l, UiText.F(l.Menus[0x11], "VILMAR "), i => Keys.D1 + i);
        Assert.True(Mismatch("p1-amiga-combat-00-hears-monsters.png", shifted, l.Menu.Box) > 20);
        Assert.Equal(2, scene.Buttons.Count);
        Assert.Equal(new Box(92, 116, 64, 9), scene.Buttons[0].Rect);
        Assert.Equal(new Box(163, 116, 64, 9), scene.Buttons[1].Rect);
    }

    /// <summary>The shot <c>p1-amiga-black-knights</c>: requester 22 as stored, every pixel.</summary>
    [OriginalDataFact]
    public void BlackKnights_MatchesTheShot()
    {
        var l = Layout();
        var scene = P1MapRequesters.Fixed(l.Palette, l.BlackKnights!, i => Keys.D1 + i);
        Assert.Equal(0, Mismatch("p1-amiga-black-knights.png", scene, l.BlackKnights!.Box));
        Assert.Equal(3, scene.Buttons.Count);
    }

    /// <summary>
    /// The shot <c>p1-amiga-guild-inspect-sheet</c> (VILMAR): every pixel of requester 14 but the end of the LEVEL line and
    /// the points row, where the remaster keeps D-064's manual wording "OKAY" / "EXPER. POINTS" (the Amiga: "OK" / "EX. PTS").
    /// </summary>
    [OriginalDataFact]
    public void Sheet_MatchesTheShot()
    {
        var l = Layout();
        var vilmar = new Character
        {
            Name = "VILMAR", Level = 1, Class = CharacterClass.Fighter, Race = Race.Dwarf, Status = CharacterStatus.Ok,
            Stats = new CharacterStats(255, 255, 255, 255, 255, 255), AgeClock = 34 * 26, HpCur = 255, HpMax = 255,
            MagicCur = 255, MagicMax = 255, GoldInBank = 34, Experience = 1000, Score = 2,
        };
        var scene = P1MapRequesters.Sheet(l.Sheet!, vilmar);
        var levelEnd = new Box(39 + 7 + 23 * 6, 30 + 20, 60, 6);
        var points = new Box(39 + 121, 30 + 67, 241 - 121 - 2, 6);
        Assert.Equal(0, Mismatch("p1-amiga-guild-inspect-sheet.png", scene, l.Sheet!.Box, levelEnd, points));
        Assert.Contains(scene.Ops, o => o.Text == "EXPER. POINTS  1000");
        Assert.Equal(Keys.Escape, scene.Buttons[^1].Click); // EXIT
        Assert.Equal(Keys.D1, scene.Buttons[0].Click);      // SPELLS
    }

    /// <summary>
    /// D-064's "EXPER. POINTS" leaves six cells for the value; a longer value keeps a blank before it and widens the
    /// requester (fill and both outlines) instead of running over the border (s155).
    /// </summary>
    [OriginalDataFact]
    public void Sheet_WidensForALongPointsRow()
    {
        var s = Layout().Sheet!;
        var m = new Character { Name = "X", Stats = new CharacterStats(1, 1, 1, 1, 1, 1), Experience = 1234567 };
        var scene = P1MapRequesters.Sheet(s, m);
        var row = Assert.Single(scene.Ops, o => o.Text.StartsWith("EXPER. POINTS", StringComparison.Ordinal));
        Assert.Equal("EXPER. POINTS 1234567", row.Text);
        var fill = scene.Ops[0];
        Assert.True(row.Rect.X + row.Text.Length * 6 + 3 <= fill.Rect.X + fill.Rect.W);
        Assert.All(scene.Ops.Where(o => o.Kind == RequesterOpKind.Outline).Take(2),
            o => Assert.True(o.Rect.X + o.Rect.W <= fill.Rect.X + fill.Rect.W && o.Rect.W > s.Box.W - 3));
        var small = P1MapRequesters.Sheet(s, new Character { Name = "X", Stats = new CharacterStats(1, 1, 1, 1, 1, 1), Experience = 99999 });
        Assert.Equal(s.Box.W, small.Ops[0].Rect.W);
        Assert.Contains(small.Ops, o => o.Text == "EXPER. POINTS 99999");
    }

    [OriginalDataFact]
    public void Sheet_ShowsRunesOnlyAsTheCodeDoes()
    {
        var s = Layout().Sheet!;
        var m = new Character { Name = "X", Stats = new CharacterStats(1, 1, 1, 1, 1, 1) };
        Assert.DoesNotContain(P1MapRequesters.Sheet(s, m).Ops, o => o.Text == "RUNES:");
        m.Phantasie1.Runes[1] = 1; // EARTH alone: h29 +0xc18 tests runes 0, 2, 3 only
        Assert.DoesNotContain(P1MapRequesters.Sheet(s, m).Ops, o => o.Text == "RUNES:");
        m.Phantasie1.Runes[2] = 1;
        var texts = P1MapRequesters.Sheet(s, m).Ops.Select(o => o.Text).ToList();
        Assert.Contains("RUNES:", texts);
        Assert.Contains("EARTH", texts);
        Assert.Contains("FIRE", texts);
        Assert.DoesNotContain("AIR", texts);
    }

    /// <summary>s156: the pack's remap is h69 <c>+0x19c</c>; its inverse is the stored h69 <c>+0x1ac</c> (h77 <c>+0x1230</c>).</summary>
    [OriginalDataFact]
    public void PenRemap_IsTheGamesTableAndItsInverse()
    {
        var l = Layout();
        Assert.Equal([0, 12, 1, 14, 3, 10, 2, 9, 4, 7, 6, 13, 8, 11, 5, 15], l.PenRemap);
        Assert.Equal([0, 2, 6, 4, 8, 14, 10, 9, 12, 7, 5, 13, 1, 11, 3, 15], l.PenUnmap);
        Assert.Equal("#EE2244", l.TownPalette![2]);
    }

    /// <summary>
    /// s156: the map's Inspect (h50 <c>+0x7b2</c>) shows requester 14 after h77 <c>+0x112e</c> remapped its pens, on the map
    /// palette: border 3 → 14, name 9 → 7, LEVEL 6 → 2, rows 4 → 3, button borders 2 → 1 (no map shot: static).
    /// </summary>
    [OriginalDataFact]
    public void MapSheet_TakesTheRemappedPensOnTheMapPalette()
    {
        var l = Layout();
        var m = new Character { Name = "VILMAR", Stats = new CharacterStats(9, 9, 9, 9, 9, 9) };
        var scene = P1MapRequesters.Remap(P1MapRequesters.Sheet(l.Sheet!, m), l.PenRemap, l.Palette);
        string Colour(RequesterOp o) => scene.Palette[o.Pen];
        Assert.Equal("#000000", Colour(scene.Ops[0]));
        Assert.Equal("#2266AA", Colour(scene.Ops[1]));
        Assert.Equal("#0088AA", Colour(Assert.Single(scene.Ops, o => o.Text == "VILMAR")));
        Assert.Equal("#228800", Colour(Assert.Single(scene.Ops, o => o.Text.StartsWith("LEVEL", StringComparison.Ordinal))));
        Assert.Equal("#EEBB00", Colour(Assert.Single(scene.Ops, o => o.Text.StartsWith("STRENGTH", StringComparison.Ordinal))));
        var ops = scene.Ops.ToList();
        var exit = ops.IndexOf(Assert.Single(ops, o => o.Text == "EXIT"));
        Assert.Equal("#EE0000", Colour(ops[exit - 3]));
        Assert.Equal("#EEEEEE", Colour(ops[exit - 1]));
        Assert.Equal(5, scene.Buttons.Count);
    }

    /// <summary>
    /// s156: Guild › New's roll (h23 <c>+0xc08</c>): no name (IText 123 on the blank slot), PURGE in gadget 41's place and
    /// " KEEP " in gadget 43's, both labels at x 14; PURGE rolls again (N), KEEP keeps (Y). The rest is the sheet.
    /// </summary>
    [OriginalDataFact]
    public void RolledSheet_HasPurgeAndKeepAndNoName()
    {
        var s = Layout().Sheet!;
        var m = new Character { Name = "", Level = 1, Class = CharacterClass.Fighter, Race = Race.Dwarf, Stats = new CharacterStats(9, 9, 9, 9, 9, 9) };
        var scene = P1MapRequesters.Sheet(s, m, rolled: true);
        Assert.DoesNotContain(scene.Ops, o => o.Title);
        Assert.Equal(2, scene.Buttons.Count);
        Assert.Equal(new Box(48, 130, 64, 9), scene.Buttons[0].Rect);
        Assert.Equal(new Box(208, 130, 64, 9), scene.Buttons[1].Rect);
        Assert.Equal(Keys.N, scene.Buttons[0].Click);
        Assert.Equal(Keys.Y, scene.Buttons[1].Click);
        Assert.Equal(48 + 14, Assert.Single(scene.Ops, o => o.Text == "PURGE").Rect.X);
        Assert.Equal(208 + 14, Assert.Single(scene.Ops, o => o.Text == " KEEP ").Rect.X);
        Assert.Contains(scene.Ops, o => o.Text.StartsWith("STRENGTH", StringComparison.Ordinal));
        Assert.DoesNotContain(scene.Ops, o => o.Text is "SPELLS" or "EXIT");
    }

    /// <summary>One line on row 2, OK alone at gadget 0's x 47 (h86 <c>+0x42c</c>).</summary>
    [OriginalDataFact]
    public void AnOkRequester_OneLine_OneButton()
    {
        var l = Layout();
        var scene = P1MapRequesters.Menu(l, UiText.S(l.Oks["core.overworld.the_river_styx"]), _ => Keys.Enter);
        var text = Assert.Single(scene.Ops, o => o.Kind == RequesterOpKind.Text && o.Text == "THE RIVER STYX");
        Assert.Equal(new Box(81 + 10 + 3 * (23 - 14), 63 + 25, 1, 1), text.Rect);
        var ok = Assert.Single(scene.Buttons);
        Assert.Equal(new Box(81 + 47, 63 + 53, 64, 9), ok.Rect);
        Assert.Contains(scene.Ops, o => o.Text == "OK" && o.Rect.X == 81 + 47 + 2 + 3 * 8);
    }

    /// <summary>A click on a button answers its prompt as the remaster's keys do: Y / N on YES / NO, digits elsewhere.</summary>
    [OriginalDataTheory]
    [InlineData(SessionPromptKind.OverworldEntry, 0xe, true)]
    [InlineData(SessionPromptKind.OverworldEntry, 0x1c, true)]
    [InlineData(SessionPromptKind.WildernessInn, 0x16, true)]
    [InlineData(SessionPromptKind.WildernessEncounter, 0x10, false)]
    [InlineData(SessionPromptKind.WildernessEncounter, 0x11, false)]
    [InlineData(SessionPromptKind.WildernessEncounter, 0xf, false)]
    public void ButtonClicks_AnswerThePrompt(SessionPromptKind kind, int menu, bool yesNo)
    {
        List<string> options = yesNo ? [SessionPrompts.Yes, SessionPrompts.No]
            : menu == 0xf ? ["RUN", "FIGHT", "SURRENDER"] : ["A", "B"];
        var prompt = new SessionPrompt(kind, ["Q"], options) { MenuId = menu };
        var l = Layout();
        var scene = menu == l.BlackKnightsMenu
            ? P1MapRequesters.Fixed(l.Palette, l.BlackKnights!, i => P1MapRequesters.ClickKey(prompt, i))
            : P1MapRequesters.Menu(l, UiText.F(l.Menus[menu], "VILMAR"), i => P1MapRequesters.ClickKey(prompt, i));
        Assert.Equal(options.Count, scene.Buttons.Count);
        for (var i = 0; i < scene.Buttons.Count; i++)
        {
            var menus = new PromptMenu();
            Assert.Equal(i + 1, menus.HandleKey(prompt, scene.Buttons[i].Click));
        }
    }

    /// <summary>
    /// s156: the dungeon's questions (h41 <c>+0x11c</c>: "%s %s" of the name and "|SPOTS A TRAP.||DISARM IT?"): four lines
    /// (three '|', no rotation) from row 0, the name's line "VILMAR " centred with its blank; D-052's DISARM / LEAVE labels.
    /// </summary>
    [OriginalDataFact]
    public void Trap_FourLinesFromRowZero_TheNameKeepsItsBlank()
    {
        var l = Layout();
        var scene = P1MapRequesters.Menu(l, UiText.F(l.Kinds["DungeonTrap"], "VILMAR"), i => Keys.D1 + i);
        var name = Assert.Single(scene.Ops, o => o.Text == "VILMAR ");
        Assert.Equal(new Box(81 + 10 + 3 * (23 - 7), 63 + 11, 1, 1), name.Rect);
        Assert.Equal(63 + 18, Assert.Single(scene.Ops, o => o.Text == "SPOTS A TRAP.").Rect.Y);
        Assert.Equal(63 + 32, Assert.Single(scene.Ops, o => o.Text == "DISARM IT?").Rect.Y);
        Assert.Contains(scene.Ops, o => o.Text == "DISARM");
        Assert.Contains(scene.Ops, o => o.Text == "LEAVE");
        var two = P1MapRequesters.Menu(l, UiText.S(l.Menus[0x1e]), i => Keys.D1 + i);
        Assert.Equal(63 + 25, Assert.Single(two.Ops, o => o.Text == "WOULD YOU LIKE TO SAVE").Rect.Y);
        var find = P1MapRequesters.Menu(l, UiText.F(l.Menus[0x1b], "SWORD"), i => Keys.D1 + i);
        Assert.Contains(find.Ops, o => o.Text == "YOU FIND SWORD.");
        Assert.Contains(find.Ops, o => o.Text == "KEEP IT?");
        // Every item's line fits the 23 centred cells (h86 +0x49e), so none reaches the borders.
        var longest = GameDataRepository.LoadPhantasie1().Items.Max(i => UiText.F(l.Menus[0x1b], i.Name).Split('|')[0].Length);
        Assert.True(longest <= l.Menu!.RowCells, $"{longest} cells");
    }

    /// <summary>s156: a click on the dungeon questions' buttons answers as the keys do (Y / N; the trap's 1 / 2 = D / L).</summary>
    [OriginalDataTheory]
    [InlineData(SessionPromptKind.DungeonExitTakeIt, 0x1d)]
    [InlineData(SessionPromptKind.DungeonExitSaveMap, 0x1e)]
    [InlineData(SessionPromptKind.DungeonLock, 0x1f)]
    [InlineData(SessionPromptKind.Treasure, 0x1b)]
    [InlineData(SessionPromptKind.DungeonTrap, 0)]
    public void DungeonButtonClicks_AnswerThePrompt(SessionPromptKind kind, int menu)
    {
        var l = Layout();
        var trap = kind == SessionPromptKind.DungeonTrap;
        var prompt = new SessionPrompt(kind, ["Q"], trap ? [SessionPrompts.Disarm, SessionPrompts.Leave] : [SessionPrompts.Yes, SessionPrompts.No])
            { MenuId = trap ? null : menu };
        var scene = P1MapRequesters.Menu(l, UiText.F(trap ? l.Kinds["DungeonTrap"] : l.Menus[menu], "X"), i => P1MapRequesters.ClickKey(prompt, i));
        Assert.Equal(2, scene.Buttons.Count);
        for (var i = 0; i < 2; i++)
            Assert.Equal(i + 1, new PromptMenu().HandleKey(prompt, scene.Buttons[i].Click));
    }

    /// <summary>
    /// s156: the town's questions are requester 0 with its stored pens on the town palette (fill 5 <c>#226644</c>, borders 2
    /// <c>#EE2244</c>); "NOT IN THE TOWN" has three lines (from row 1), "IT WILL COST" two (row 2) with AGREE / NOT.
    /// </summary>
    [OriginalDataFact]
    public void TownQuestions_StoredPensOnTheTownPalette()
    {
        var l = Layout();
        var move = P1MapRequesters.Town(l, "guild_add_move", i => i == 0 ? Keys.Y : Keys.N)!;
        Assert.Equal("#226644", move.Palette[move.Ops[0].Pen]);
        Assert.Equal("#EE2244", move.Palette[move.Ops[1].Pen]);
        Assert.Equal(63 + 18, Assert.Single(move.Ops, o => o.Text == "NOT IN THE TOWN").Rect.Y);
        Assert.Equal("#EEEEEE", move.Palette[Assert.Single(move.Ops, o => o.Text == "TO GET HERE").Pen]);
        Assert.Equal([Keys.Y, Keys.N], move.Buttons.Select(b => b.Click));
        var cost = P1MapRequesters.Town(l, "training_cost", i => i == 0 ? Keys.A : Keys.N, 1200)!;
        Assert.Equal(63 + 25, Assert.Single(cost.Ops, o => o.Text == "IT WILL COST").Rect.Y);
        Assert.Contains(cost.Ops, o => o.Text == "1200 GOLD");
        Assert.Contains(cost.Ops, o => o.Text == "AGREE");
        Assert.Contains(cost.Ops, o => o.Text == "NOT");
        Assert.Null(P1MapRequesters.Town(l, "nothing", _ => Keys.Enter));
    }

    /// <summary>
    /// s156: Info › About Phantasie is requester 27 (h80 <c>+0xcb4</c>): (69, 57) 182 × 86, two pen-15 outlines and a pen-0
    /// one, the 176 × 80 logo (h81 <c>+0x22</c>, logo.pic plane after plane) at (72, 60); a click on it closes.
    /// </summary>
    [OriginalDataFact]
    public void About_IsRequester27WithTheLogo()
    {
        var l = Layout();
        var scene = P1MapRequesters.About(l);
        Assert.Equal(new Box(69, 57, 182, 86), scene.Ops[0].Rect);
        Assert.Equal([15, 15, 0], scene.Ops.Where(o => o.Kind == RequesterOpKind.Outline).Select(o => o.Pen));
        var pic = Assert.Single(scene.Ops, o => o.Kind == RequesterOpKind.Picture);
        Assert.Equal(new Box(72, 60, 176, 80), pic.Rect);
        Assert.Equal("ui.logo", pic.Text);
        Assert.Equal(pic.Rect, Assert.Single(scene.Buttons).Rect);
        Assert.Equal(Keys.Enter, scene.Buttons[0].Click);
        var path = Pack().Resolve("ui.logo")!;
        var (w, h, px) = P1AmigaFontTests.Pixels(path);
        Assert.Equal((176, 80), (w, h));
        Assert.Equal(0x888888, px[0]);             // the logo's grey ground (map pen 13)
        Assert.Contains(0xEE0000, px.Take(176 * 20)); // the red "phantasie" title
    }

    /// <summary>
    /// s156: the sheet's views (h29 <c>+0xcca</c>): SKILLS = requester 9 (eight rows, two columns, pen 4), EQUIPMENT =
    /// requester 10 (three "%-13.12sPTS:%-2u" rows, backfill 7), POTIONS = requester 11 (counts, RINGS:, the wand); all at
    /// (72, 69) 176 × 62 with EXIT at GREL (−71, −17) = (176, 113), which sends Esc. SPELLS has none here (null).
    /// </summary>
    [OriginalDataFact]
    public void SheetPages_AreRequesters9To11()
    {
        var s = Layout().Sheet!;
        var data = GameDataRepository.LoadPhantasie1();
        var m = new Character
        {
            Name = "X", Stats = new CharacterStats(9, 9, 9, 9, 9, 9),
            Skills = new CharacterSkills(Attack: 12, Parry: 5, FindItem: 42, SpotTrap: 40, DisarmTrap: 41, Listen: 7, PickLock: 43, Swim: 30),
        };
        Assert.Null(P1MapRequesters.SheetPageFor(s, 0, m, data));
        var skills = P1MapRequesters.SheetPageFor(s, 2, m, data)!;
        Assert.Equal(new Box(72, 69, 176, 62), skills.Ops[0].Rect);
        Assert.Equal(new Box(176, 113, 64, 9), Assert.Single(skills.Buttons).Rect);
        Assert.Equal(Keys.Escape, skills.Buttons[0].Click);
        var attack = Assert.Single(skills.Ops, o => o.Text == "ATTACK  12");
        Assert.Equal((72 + 6, 69 + 6, 4), (attack.Rect.X, attack.Rect.Y, attack.Pen));
        Assert.Equal(69 + 33, Assert.Single(skills.Ops, o => o.Text == "PICK LOCK    43").Rect.Y);
        Assert.Equal(72 + 82, Assert.Single(skills.Ops, o => o.Text == "PICK LOCK    43").Rect.X);
        var equipment = P1MapRequesters.SheetPageFor(s, 1, m, data)!;
        Assert.Equal(7, equipment.Ops[0].Pen);
        Assert.Equal(3, equipment.Ops.Count(o => o.Kind == RequesterOpKind.Text && o.Text.Contains("PTS:", StringComparison.Ordinal)));
        Assert.Equal(72 + 29, equipment.Ops.First(o => o.Text.Contains("PTS:", StringComparison.Ordinal)).Rect.X);
        Assert.Equal(["0  HL POTIONS 0  MG POTIONS", "0  SCROLL(S)  0  TREASURES", "", ""], P1MapRequesters.PotionRows(m));
        m.InventoryItemIds.AddRange([101, 101, 143, 150]);
        Assert.Equal(["2  HL POTIONS 0  MG POTIONS", "0  SCROLL(S)  0  TREASURES", "RINGS:   C", "THE WAND OF NIKADEMUS"],
            P1MapRequesters.PotionRows(m));
    }

    /// <summary>s158: the master disk's EMILY as FS-UAE showed her (Guild › Add, Inspect over the overworld).</summary>
    private static Character Emily()
    {
        var m = new Character
        {
            Name = "EMILY", Level = 1, Class = CharacterClass.Wizard, Race = Race.Elf, Status = CharacterStatus.Ok,
            Stats = new CharacterStats(11, 18, 16, 3, 18, 12), AgeClock = 241 * 26, HpCur = 1, HpMax = 1, MagicCur = 8,
            MagicMax = 8, GoldInBank = 290, Experience = 240, Score = 2, WornWeapon = 48, WornArmor = 22, WornShield = 4,
            Skills = new CharacterSkills(Attack: 38, Parry: 33, FindItem: 17, SpotTrap: 19, DisarmTrap: 13, Listen: 17, PickLock: 13, Swim: 8),
        };
        m.SpellsKnown[4] = 1; // FIREFLASH I
        return m;
    }

    /// <summary>
    /// s158 live: Inspect's sheet over the overworld (<c>p1-amiga-map-inspect-sheet</c>, EMILY): requester 14's stored pens
    /// remapped onto the map palette, every pixel but the pointer, the LEVEL line's end and the points row (D-064's
    /// "OKAY" / "EXPER. POINTS").
    /// </summary>
    [OriginalDataFact]
    public void MapSheet_MatchesTheLiveShot()
    {
        var l = Layout();
        var scene = P1MapRequesters.Remap(P1MapRequesters.Sheet(l.Sheet!, Emily()), l.PenRemap, l.Palette);
        var levelEnd = new Box(39 + 7 + 20 * 6, 30 + 20, 60, 6);
        var points = new Box(39 + 121, 30 + 67, 241 - 121 - 2, 6);
        var pointer = new Box(60, 100, 20, 18);
        Assert.Equal(0, Mismatch("p1-amiga-map-inspect-sheet.png", scene, l.Sheet!.Box, levelEnd, points, pointer));
    }

    /// <summary>s158 live: the sheet's SKILLS / EQUIPMENT / POTIONS views over the overworld (requesters 9 / 10 / 11), every pixel.</summary>
    [OriginalDataTheory]
    [InlineData(2, "p1-amiga-map-sheet-skills.png")]
    [InlineData(1, "p1-amiga-map-sheet-equipment.png")]
    [InlineData(3, "p1-amiga-map-sheet-potions.png")]
    public void MapSheetPages_MatchTheLiveShots(int page, string shot)
    {
        var l = Layout();
        var scene = P1MapRequesters.Remap(P1MapRequesters.SheetPageFor(l.Sheet!, page, Emily(), GameDataRepository.LoadPhantasie1())!,
            l.PenRemap, l.Palette);
        Assert.Equal(0, Mismatch(shot, scene, scene.Ops[0].Rect));
    }

    /// <summary>
    /// s158: the sheet's SPELLS view is the fight's requester 12 (h29 <c>+0xe90</c> → h23 <c>+0x570</c>), its stored pens
    /// remapped over the overworld: every pixel of <c>p1-amiga-map-sheet-spells</c> (EMILY, FIREFLASH I) but the pointer.
    /// Both scroll gadgets ghosted (one spell), a row sends its digit, EXIT Esc; in the town the stored pens stay.
    /// </summary>
    [OriginalDataFact]
    public void SheetSpells_IsRequester12_MatchesTheLiveShot()
    {
        var l = Layout();
        var list = Pack().CombatField!.SpellList!.Spells;
        var data = GameDataRepository.LoadPhantasie1();
        string[] names = [.. SpellPicker.Known(Emily()).Select(id => UiOverlays.P1SpellName(id, data))];
        Assert.Equal(["FIREFLASH I"], names);
        var stored = P1MapRequesters.SheetSpells(list, l.Sheet!.Palette, l.PenUnmap, names, 0);
        var scene = P1MapRequesters.Remap(stored, l.PenRemap, l.Palette);
        Assert.Equal(0, Mismatch("p1-amiga-map-sheet-spells.png", scene, list.Box, new Box(78, 130, 22, 22)));
        Assert.Equal([Keys.D1, Keys.Escape], scene.Buttons.Select(b => b.Click));
        // Seven spells: page 1 has the down gadget live, page 2 (first 6) the up gadget and one row.
        string[] seven = ["A", "B", "C", "D", "E", "F", "G"];
        Assert.Contains(P1MapRequesters.SheetSpells(list, l.Sheet.Palette, l.PenUnmap, seven, 0).Buttons, b => b.Click == Keys.Down);
        var second = P1MapRequesters.SheetSpells(list, l.Sheet.Palette, l.PenUnmap, seven, 6);
        Assert.Equal([Keys.D1, Keys.Up, Keys.Escape], second.Buttons.Select(b => b.Click));
        Assert.Equal(l.PenUnmap[list.FillPen], stored.Ops[0].Pen); // stored pens: the town draws them as they are
    }

    /// <summary>
    /// s158 live: the SPELLS view in the Guild (Inspect › OMAR, <c>p1-amiga-guild-sheet-spells</c>): requester 12's stored
    /// pens on the town palette, a full page of six with the down gadget live, every pixel but the pointer.
    /// </summary>
    [OriginalDataFact]
    public void SheetSpells_InTheGuild_MatchesTheLiveShot()
    {
        var l = Layout();
        var list = Pack().CombatField!.SpellList!.Spells;
        string[] names = ["FIREFLASH I", "FIREFLASH II", "FIREFLASH III", "QUICKNESS I", "QUICKNESS II", "STRENGTH I", "MORE"];
        var scene = P1MapRequesters.SheetSpells(list, l.Sheet!.Palette, l.PenUnmap, names, 0);
        Assert.Equal(0, Mismatch("p1-amiga-guild-sheet-spells.png", scene, list.Box, new Box(76, 132, 26, 28)));
    }

    /// <summary>
    /// s158 live: "A DUNGEON.  ENTER?" over the overworld (<c>p1-amiga-map-requester0-a-dungeon</c>): requester 0 with its
    /// one line on row 2 and YES / NO, every pixel but the pointer.
    /// </summary>
    [OriginalDataFact]
    public void OneLineQuestion_MatchesTheLiveShot()
    {
        var l = Layout();
        var scene = P1MapRequesters.Menu(l, UiText.S("p1field.req.dungeon_enter"), i => Keys.D1 + i);
        Assert.Equal(0, Mismatch("p1-amiga-map-requester0-a-dungeon.png", scene, l.Menu!.Box, new Box(118, 90, 20, 22)));
    }

    /// <summary>
    /// s158 live: the wilderness inn without the gold (h71 <c>+0xe76</c>) is requester 0 "YOU CAN'T AFFORD IT" with SORRY
    /// (<c>p1-amiga-map-inn-cant-afford</c>), every pixel but the pointer; the pack's <c>oks</c> name Core's line for it.
    /// </summary>
    [OriginalDataFact]
    public void InnCantAfford_IsAnOkRequester_MatchesTheLiveShot()
    {
        var l = Layout();
        Assert.Equal("p1field.req.cant_afford", l.Oks["core.inn.you_cant_afford_it"]);
        var scene = P1MapRequesters.Menu(l, UiText.S("p1field.req.cant_afford"), _ => Keys.Enter);
        Assert.Single(scene.Buttons);
        Assert.Equal(0, Mismatch("p1-amiga-map-inn-cant-afford.png", scene, l.Menu!.Box, new Box(120, 118, 22, 22)));
    }

    /// <summary>Rasters <paramref name="scene"/> on black and counts pixels in <paramref name="area"/> that differ from the shot.</summary>
    private static int Mismatch(string shot, RequesterScene scene, Box area, params Box[] skip)
    {
        var l = Layout();
        var screen = new int[320 * 200];
        int Colour(int pen) => Convert.ToInt32(scene.Palette[pen][1..], 16);
        void Set(int x, int y, int c)
        {
            if (x is >= 0 and < 320 && y is >= 0 and < 200)
                screen[y * 320 + x] = c;
        }
        var body = FontSources.Named(l.Font, l.FontFile)!;
        var title = FontSources.Named(l.TitleFont, l.TitleFontFile)!;
        foreach (var op in scene.Ops)
        {
            var r = op.Rect;
            var c = Colour(op.Pen);
            switch (op.Kind)
            {
                case RequesterOpKind.Fill:
                    for (var y = r.Y; y < r.Y + r.H; y++)
                        for (var x = r.X; x < r.X + r.W; x++)
                            Set(x, y, c);
                    break;
                case RequesterOpKind.Outline:
                    for (var x = r.X; x < r.X + r.W; x++)
                    {
                        Set(x, r.Y, c);
                        Set(x, r.Y + r.H - 1, c);
                    }
                    for (var y = r.Y; y < r.Y + r.H; y++)
                    {
                        Set(r.X, y, c);
                        Set(r.X + r.W - 1, y, c);
                    }
                    break;
                default:
                    var font = op.Title ? title : body;
                    var cx = r.X;
                    foreach (var ch in op.Text)
                    {
                        var g = font.Glyph(ch);
                        for (var row = 0; row < font.Height; row++)
                            for (var col = 0; col < g.Advance; col++)
                                if (g.IsSet(col, row))
                                    Set(cx + col, r.Y + row, c);
                        cx += g.Advance;
                    }
                    break;
            }
        }
        var path = Path.Combine(Pack().RootDirectory, "..", "..", "..", "parity", "shots", shot);
        var (w, _, px) = P1AmigaFontTests.Pixels(path);
        var bad = 0;
        for (var y = area.Y; y < area.Y + area.H; y++)
            for (var x = area.X; x < area.X + area.W; x++)
            {
                if (skip.Any(s => x >= s.X && x < s.X + s.W && y >= s.Y && y < s.Y + s.H))
                    continue;
                if (px[(y + 5) * w + x] != screen[y * 320 + x])
                    bad++;
            }
        return bad;
    }
}
