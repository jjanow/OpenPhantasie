using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>Inspect pages (COM 0x2ef4 / 0x2c65 / 0x2b7e / 0x2cf6), field Health (OV2 0x450c) and Use potion (OV2 0x43e2).</summary>
public class InspectAndPotionTests
{
    private static Character Sample()
    {
        var ch = new Character
        {
            Name = "PAPAST", Level = 1, Class = CharacterClass.Priest, Race = Race.Human, Status = CharacterStatus.Ok,
            Stats = new CharacterStats(14, 16, 12, 13, 14, 9), HpCur = 4, HpMax = 5, MagicCur = 7, MagicMax = 7,
            GoldInBank = 49, AgeClock = 24 * 26, Score = 3,
            Skills = new CharacterSkills(48, 29, 12, 15, 8, 12, 6, 27),
            WornWeapon = 41, WornArmor = 21, WornShield = 1,
        };
        return ch;
    }

    [OriginalDataFact]
    public void Sheet_UsesTheComFormatStrings_ManualExample()
    {
        var lines = InspectDialog.SheetLines(Sample());
        Assert.Equal("PAPAST", lines[0]);
        Assert.Equal("LEVEL  1 PRIEST HUMAN OKAY", lines[1]); // manual p.11 (D-064); DOS prints "OK"
        Assert.Equal("STRENGTH      14 AGE             24", lines[2]);
        Assert.Equal("INTELLIGENCE  16 HIT POINTS   4/5  ", lines[3]);
        Assert.Equal("DEXTERITY     12 MAGIC        7/7  ", lines[4]);
        Assert.Equal("CONSTITUTION  13 GOLD IN BANK    49", lines[5]);
        Assert.Equal("CHARISMA      14 EXPER. POINTS    0", lines[6]);
        Assert.Equal("LUCK           9 SCORE            3", lines[7]);
        Assert.Equal(8, lines.Count); // no rune line

        var withGear = InspectDialog.SheetLines(Sample(), ClientSessions.Data);
        Assert.Equal("EQUIPMENT", withGear[8]); // D-064: on the sheet, as in the manual example
        Assert.Matches(@"^\S.* \(\d+\)", withGear[9]);
    }

    [OriginalDataFact]
    public void Sheet_RuneLine_OnlyWhenAirFireOrWater_DosQuirk()
    {
        var ch = Sample();
        ch.Phantasie1.Runes[1] = 1; // EARTH
        ch.Phantasie1.Runes[4] = 1; // GOD
        Assert.Equal(8, InspectDialog.SheetLines(ch).Count);
        ch.Phantasie1.Runes[2] = 1; // FIRE
        Assert.Equal("runes: EARTH FIRE GOD", InspectDialog.SheetLines(ch)[^1]);
    }

    [OriginalDataFact]
    public void Counters_CountCarriedItems_AndRingsAsPositionalLetters()
    {
        var ch = Sample();
        ch.InventoryItemIds.AddRange([101, 105, 111, 121, 141, 143, 160]);
        var lines = InspectDialog.CounterLines(ch);
        Assert.Equal("2  hl potions    1  mg potions", lines[0]);
        Assert.Equal("1  scroll(s)     1  treasures", lines[1]);
        Assert.Equal("RING:A C      ", lines[2]);
        Assert.Equal(3, lines.Count);
    }

    [OriginalDataFact]
    public void Counters_WandAlone_PrintsBlankRingLineAndTheWand()
    {
        var ch = Sample();
        ch.InventoryItemIds.Add(150);
        var lines = InspectDialog.CounterLines(ch);
        Assert.Equal("RING:         ", lines[2]);
        Assert.Equal("THE WAND OF NIKADEMUS", lines[3]);
    }

    [OriginalDataFact]
    public void Equipment_AndSkills()
    {
        var eq = InspectDialog.EquipmentLines(Sample(), ClientSessions.Data);
        Assert.Equal(3, eq.Count);
        Assert.All(eq, l => Assert.Contains("PTS:", l));
        var sk = InspectDialog.SkillLines(Sample());
        Assert.Equal("attack     48      parry        29", sk[0]);
        Assert.Equal(4, sk.Count);
    }

    [OriginalDataFact]
    public void Wilderness_SummaryThenInspectMemberNoYes_ThenSheetAndPageMenu()
    {
        var s = ClientSessions.Overworld();
        s.Sack.DepositRewards(120, 30, [103, 104, 41]);
        var d = new InspectDialog(new PromptMenu(), ClientSessions.Data);
        Assert.Null(d.Open(s));
        var summary = d.Lines(s, ClientSessions.Data);
        Assert.StartsWith("DAY OUT", summary[0]);
        Assert.Equal("EXPERIENCE POINTS        120", summary[1]);
        Assert.Contains("001 WEAPON(S)", summary); // 41 → class (41−1)/20 = 2; 103, 104 → class 5
        Assert.Contains("002 POTION(S)", summary);

        d.HandleKeys([Keys.Enter], s); // any key → menu 0x15
        Assert.Equal("INSPECT MEMBER?", d.Lines(s, ClientSessions.Data)[0]);
        Assert.Equal(">NO  YES", d.Lines(s, ClientSessions.Data)[1]); // NO is the first option
        d.HandleKeys([Keys.Right, Keys.Enter], s);
        Assert.Equal(InspectDialog.WhoTitle, d.Lines(s, ClientSessions.Data)[0]);
        d.HandleKeys([Keys.D2], s);
        var sheet = d.Lines(s, ClientSessions.Data);
        Assert.Equal("Priesto", sheet[0]);
        Assert.Equal(" SPELLS  EQUIPMENT >SKILLS  POTIONS", sheet[^1]); // menu 0xa's first-ever highlight (s58)

        d.HandleKeys([Keys.Enter], s); // SKILLS
        Assert.StartsWith("attack", d.Lines(s, ClientSessions.Data)[0]);
        d.HandleKeys([Keys.Space], s); // back to the sheet, cursor kept on SKILLS
        Assert.Equal(" SPELLS  EQUIPMENT >SKILLS  POTIONS", d.Lines(s, ClientSessions.Data)[^1]);
        d.HandleKeys([Keys.Escape], s); // back to the picker
        Assert.Equal(InspectDialog.WhoTitle, d.Lines(s, ClientSessions.Data)[0]);
        d.HandleKeys([Keys.Escape], s);
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void SpellsPage_IsTheScrollingPicker_WithASharedCursor()
    {
        var s = ClientSessions.Overworld();
        var priest = s.Party.Members[1];
        Array.Clear(priest.SpellsKnown);
        foreach (var id in new[] { 0, 4, 5, 6, 7, 8 })
            priest.SpellsKnown[id] = 1;
        var names = new[] { 0, 4, 5, 6, 7, 8 }.Select(id => ClientSessions.Data.GetSpell(id).Name).ToArray();
        var picker = new SpellPicker();
        var d = new InspectDialog(new PromptMenu(), ClientSessions.Data, picker);
        d.Open(s);
        d.HandleKeys([Keys.Enter, Keys.Right, Keys.Enter, Keys.D2, Keys.Left, Keys.Left, Keys.Enter], s); // summary, YES, Priesto, SPELLS

        var page = d.Lines(s, ClientSessions.Data);
        Assert.Equal(["SPELLS AVAILABLE:", " ", " ", " ", ">" + names[0], " " + names[1], " " + names[2]], page);
        d.HandleKeys([Keys.Down, Keys.Down, Keys.Down, Keys.Down], s);
        page = d.Lines(s, ClientSessions.Data);
        Assert.Equal([" " + names[1], " " + names[2], " " + names[3], ">" + names[4], " " + names[5], " "], page.Skip(1));
        Assert.Equal(4, picker.Cursor);

        d.HandleKeys([Keys.Space], s); // not a picker key
        Assert.Equal("SPELLS AVAILABLE:", d.Lines(s, ClientSessions.Data)[0]);
        d.HandleKeys([Keys.Escape], s); // closes the page
        Assert.Equal(">SPELLS  EQUIPMENT  SKILLS  POTIONS", d.Lines(s, ClientSessions.Data)[^1]);
        d.HandleKeys([Keys.Enter], s);
        Assert.Equal(">" + names[4], d.Lines(s, ClientSessions.Data)[4]); // the cursor is kept
    }

    /// <summary>
    /// s158: with I Amiga's SPELLS view as requester 12 (<see cref="InspectDialog.AmigaSpellRows"/>) the page scrolls by six
    /// (h23 <c>+0x758</c> / <c>+0x77c</c>; nothing when it cannot move), and EXIT or a live row ends the view (h29
    /// <c>+0xe90</c> ignores the pick); a row past the list does nothing.
    /// </summary>
    [OriginalDataFact]
    public void AmigaSpellsView_PagesBySix_AndARowOrExitEndsIt()
    {
        var s = ClientSessions.Overworld();
        var priest = s.Party.Members[1];
        Array.Clear(priest.SpellsKnown);
        foreach (var id in new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
            priest.SpellsKnown[id] = 1;
        var d = new InspectDialog(new PromptMenu(), ClientSessions.Data) { AmigaSpellRows = 6 };
        d.Open(s);
        d.HandleKeys([Keys.Enter, Keys.Right, Keys.Enter, Keys.D2, Keys.Left, Keys.Left, Keys.Enter], s); // summary, YES, member 2, SPELLS
        Assert.Equal((0, priest), d.PageShown);
        d.HandleKeys([Keys.Up], s);
        Assert.Equal(0, d.AmigaSpellFirst);
        d.HandleKeys([Keys.Down], s);
        Assert.Equal(6, d.AmigaSpellFirst);
        d.HandleKeys([Keys.Down], s); // 8 spells: no third page
        Assert.Equal(6, d.AmigaSpellFirst);
        d.HandleKeys([Keys.D3], s); // row 3 of a two-row page: inert
        Assert.NotNull(d.PageShown);
        d.HandleKeys([Keys.D2], s);
        Assert.Same(priest, d.SheetMember);
        d.HandleKeys([Keys.Enter], s); // SPELLS again: the first page
        Assert.Equal(0, d.AmigaSpellFirst);
        d.HandleKeys([Keys.Escape], s);
        Assert.Same(priest, d.SheetMember);
    }

    [OriginalDataFact]
    public void Town_GoesStraightToThePicker()
    {
        var s = ClientSessions.Overworld();
        s.EnterTown(1);
        var d = new InspectDialog(new PromptMenu(), ClientSessions.Data);
        d.Open(s);
        Assert.Equal(InspectDialog.WhoTitle, d.Lines(s, ClientSessions.Data)[0]);
    }

    [OriginalDataFact]
    public void FieldHealth_Format()
    {
        var ch = Sample();
        Assert.Equal("PAPAST    OKAY  HP   4/5   MP  7/7  ", HealthDialog.Line(ch));
    }

    [OriginalDataFact]
    public void UsePotion_WhoWhichOnWhom()
    {
        var s = ClientSessions.Overworld();
        var fighter = s.Party.Members[0];
        var priest = s.Party.Members[1];
        fighter.InventoryItemIds.Add(103); // HEALING 3 → 9 HP
        priest.HpCur = 1;
        var d = new UsePotionDialog();
        Assert.Null(d.Open(s));
        d.HandleKeys([Keys.D1], s);
        Assert.Equal("1 HEALING 3", d.Lines(s, ClientSessions.Data)[1]);
        d.HandleKeys([Keys.D1], s);
        Assert.Equal("ON WHOM:", d.Lines(s, ClientSessions.Data)[0]);
        d.HandleKeys([Keys.D2], s);
        Assert.False(d.IsOpen);
        Assert.Equal(Math.Min(priest.HpMax, 10), priest.HpCur);
        Assert.DoesNotContain(103, fighter.InventoryItemIds);
    }

    [OriginalDataFact]
    public void UsePotion_NotInCombat()
    {
        var s = ClientSessions.Fight();
        Assert.NotNull(new UsePotionDialog().Open(s));
    }
}
