using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// IV's combat spell list (s152, <c>_getmagic(member, 0)</c>, FORMAT.md 6.9 "Combat spell list"): five category pages
/// from the table at <c>0x44AC8</c> / <c>0x44AE2</c>, every spell of the page lettered, the name and a choice only where
/// castable, the head lines = the pages, then Quit.
/// </summary>
public class Phantasie4CombatSpellListTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4BattleData B = Phantasie4World.LoadBattleData();
    private static readonly IReadOnlyList<string> Names = [.. Enumerable.Range(1, 65).Select(n => $"S{n}")];

    private sealed class Low : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    /// <summary>A one-member Priest party in a dungeon fight at its first order, knowing <paramref name="spells"/>.</summary>
    private static P4Battle Fight(int mp, params int[] spells)
    {
        var p = new P4Party { Gold = 0, AverageLevel = 5 };
        var m = new P4Member
        {
            Name = "M0", Race = P4Ids.Human, Class = P4Ids.Priest, Level = 5, HpMax = 60, Hp = 60, MpMax = 20, Mp = mp,
            Abilities = [15, 15, 15, 15, 15, 15],
        };
        Array.Fill(m.Skills, 60);
        m.Items[0] = 0x1C + 4;
        m.MoveBase = P4Creation.Stride(Code, T.Creation, m.Race, m.Class);
        m.MeleeBonus = P4TownRules.MeleeBonus(m);
        foreach (var s in spells)
            m.Spells[s - 1] = 1;
        p.Members.Add(m);
        var b = P4Battle.Dungeon(Code, T, B, p, new Low(), 0);
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.Equal(P4BattlePrompt.Order, b.Prompt);
        return b;
    }

    [OriginalDataFact]
    public void Pages_AreTheDecodedTable()
    {
        var b = Fight(10);
        Assert.Equal(
        [
            [1, 2, 3, 4, 13, 40],
            [11, 12, 16, 17, 18, 19, 42, 44, 45],
            [5, 6, 7, 8, 34, 35, 36, 37, 51, 52, 53, 54, 55, 56, 57, 58],
            [20, 21, 22, 23, 30, 31, 32, 33, 59, 60, 61, 62, 64],
            [9, 10, 14, 24, 26, 25, 27, 28, 29, 38, 39, 41, 43, 46, 48], // 26 before 25, as the table has it
        ], b.SpellPages.Select(p => p.ToArray()).ToArray());
        // Every combat spell is on exactly one page; 15, 47, 49, 50, 63, 65 on none (combat.md 9.1).
        var all = b.SpellPages.SelectMany(p => p).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 65).Where(P4Battle.CombatSpellBuilt).Order(), all.Order());
        // Window rows 104 + 16 i fit the 360-row window: the longest page (16) fills it exactly.
        Assert.Equal(16, b.SpellPages.Max(p => p.Count));
    }

    [OriginalDataFact]
    public void Heads_AreTheCategoryLinesThenQuit()
    {
        var b = Fight(10);
        Assert.Equal(["p4x.0x44bc2", "p4x.0x44bce", "p4x.0x44bde", "p4x.0x44be8", "p4x.0x44bf2", "p4x.0x44bfc"],
            b.SpellPageHeads);
    }

    [OriginalDataFact]
    public void List_LettersEverySpell_NamesOnlyTheCastable()
    {
        // Knows Healing 2 (MP 2), Levitation (3) and Resurrection (4) with MP 3: Resurrection is too dear.
        var b = Fight(3, 2, 13, 40);
        var list = b.SpellList(0, Names);
        Assert.Equal(P4Battle.SpellListName, list.Name);
        Assert.Equal(6, list.Rows.Count); // A–F: 1, 2, 3, 4, 13, 40
        Assert.Equal([null, "S2", null, null, "S13", null], list.Rows.Select(r => r.Text));
        Assert.Equal([null, 0, null, null, 1, null], list.Rows.Select(r => r.Choice));
        Assert.Equal([2, 13], b.CastableSpells(0));
        Assert.Equal([2, 3, 4, 5, 6, 7], list.HeadChoices); // the five pages, then Quit, after the two spells
    }

    [OriginalDataFact]
    public void Choices_PickASpell_TurnThePage_OrQuit()
    {
        var b = Fight(10, 2, 26, 25);
        Assert.Equal((2, (int?)null, false), b.SpellListChoice(0, 0));
        Assert.Equal(((int?)null, 4, false), b.SpellListChoice(0, 1 + 4)); // head 5 = page 4
        Assert.Equal(((int?)null, (int?)null, true), b.SpellListChoice(0, 1 + 5)); // head 6 = Quit
        // Page 4 keeps the table's order: 26 before 25.
        Assert.Equal([26, 25], b.CastableSpells(4));
        Assert.Equal((26, (int?)null, false), b.SpellListChoice(4, 0));
        var list = b.SpellList(4, Names);
        Assert.Equal(15, list.Rows.Count);
        Assert.Equal("S26", list.Rows[4].Text);
        Assert.Equal(0, list.Rows[4].Choice);
        Assert.Equal(1, list.Rows[5].Choice);
        Assert.Equal(Enumerable.Range(2, 6), list.HeadChoices);
    }

    [OriginalDataFact]
    public void AnEmptyPage_StillHasItsHeads()
    {
        var b = Fight(10, 2);
        var list = b.SpellList(2, Names);
        Assert.All(list.Rows, r => Assert.Null(r.Choice));
        Assert.Equal(Enumerable.Range(0, 6), list.HeadChoices);
        Assert.Equal(((int?)null, 0, false), b.SpellListChoice(2, 0));
    }
}
