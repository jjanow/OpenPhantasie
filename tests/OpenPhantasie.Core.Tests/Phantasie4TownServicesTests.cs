using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's town buildings and party menus (s102; <see cref="P4TownLoop"/> screens, <see cref="P4Menus"/>; specs
/// <c>towns.md</c> §3–§9, <c>training.md</c> §4.1–§5, <c>spells-noncombat.md</c>, <c>items.md</c>, <c>ending.md</c>).
/// </summary>
public class Phantasie4TownServicesTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4Atlas Atlas = Phantasie4World.LoadAtlas();
    private static readonly P4ServiceData Data = Phantasie4World.LoadServiceData();

    /// <summary>Returns the scripted rand results (1..n) in order, then 1 (the lowest) for every later draw.</summary>
    private sealed class ScriptRng(params int[] results) : IRandomSource
    {
        private readonly Queue<int> _q = new(results);

        public int Next(int minInclusive, int maxExclusive) =>
            _q.Count > 0 ? Math.Clamp(_q.Dequeue(), minInclusive, maxExclusive - 1) : minInclusive;
    }

    private static P4Member Member(string name, int cls = P4Ids.Fighter, int level = 1)
    {
        var m = new P4Member
        {
            Name = name, Class = cls, Race = P4Ids.Human, Level = level, HpMax = 20, Hp = 20, MpMax = 4, Mp = 4,
            Abilities = [15, 15, 15, 15, 12, 10], AgingFactor = 200, Social = 3, MoveBase = 7,
        };
        Array.Fill(m.Skills, 20);
        return m;
    }

    /// <summary>A party of <paramref name="n"/> whose members are also in the guild roster (slots 0…).</summary>
    private static P4Party Party(int n = 3, long gold = 5000)
    {
        var p = new P4Party { Gold = gold };
        for (var i = 0; i < n; i++)
        {
            var m = Member($"M{i}");
            m.GuildSlot = i;
            p.Roster[i] = m.Clone();
            p.Members.Add(m);
        }
        return p;
    }

    private static P4TownLoop Town(P4Party party, IRandomSource? rng = null, GameOptions? o = null, int town = 2)
    {
        var w = new P4TownLoop(o ?? Manual, T, Atlas, party, rng ?? new ScriptRng(), Data);
        // Code rules: no gold deposit at the gate; the pool is empty, so the town opens at once.
        w.Arrive(town);
        while (w.Prompt == P4TownPrompt.Shares)
            w.EnterShares([.. party.Members.Select(_ => 1)]);
        return w;
    }

    private static int Find(P4TownLoop w, string id)
    {
        var text = GameText.S(id);
        for (var i = 0; i < w.Choices.Count; i++)
        {
            if (w.Choices[i].Text.Contains(text, StringComparison.Ordinal))
                return i;
        }
        throw new InvalidOperationException($"No choice {text}.");
    }

    // --- Guild: training and the level-up -------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Training_PaysAndLevelsUp_WithThreeSkillPicks()
    {
        var party = Party(gold: 0);
        var m = party.Members[0];
        m.Experience = 1_000_000;
        var w = Town(party, new ScriptRng(), Code);
        party.Gold = 30000;
        w.Enter(P4Building.Guild);
        Assert.Equal(P4TownPrompt.Pick, w.Prompt);
        w.Choose(1); // T: Training hall
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
        var cost = P4Training.TrainingCost(T.Training, 1, 12, P4Town.PriceIndex(T.Towns, 2));
        Assert.Contains(w.Box, l => l.Contains(cost.ToString(), StringComparison.Ordinal));
        var account = m.Account;
        w.Answer(true);
        Assert.Equal(30000 - cost, party.Gold);
        Assert.Equal(account + 27 * 1 * 30, m.Account); // Noble: 3^3 × L × 30
        // Three picks; the same skill may be picked again.
        var attack = m.Skill(P4Skill.Attack);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(P4TownPrompt.Pick, w.Prompt);
            Assert.Equal(P4Ids.Skills, w.Choices.Count);
            w.Choose((int)P4Skill.Attack);
        }
        Assert.NotEqual(attack, m.Skill(P4Skill.Attack));
        Assert.Equal(P4TownPrompt.Who, w.Prompt); // s156: straight on to the member pick (_calpow: no _spc_wait)
        Assert.Equal(2, m.Level);
        Assert.True(m.HpMax > 20);
        w.Choose(-1);
        w.Choose(3); // Leave the guild
        Assert.Equal(P4TownPrompt.Menu, w.Prompt);
        Assert.Equal(1, party.Roster[0]!.Level);
        w.Enter(P4Building.Guild); // _guild copies the party records back into the roster
        Assert.Equal(2, party.Roster[0]!.Level);
    }

    [OriginalDataFact]
    public void LevelUp_SkillWindow_NamesEveryRowAndFollowsThePicks()
    {
        var party = Party(gold: 0);
        var m = party.Members[0];
        m.Experience = 1_000_000;
        var w = Town(party, new ScriptRng(), Code);
        party.Gold = 30000;
        w.Enter(P4Building.Guild);
        Assert.Null(w.SkillWindow);
        Assert.Null(w.TrainingWindow);
        w.Choose(1); // T: Training hall
        Assert.Null(w.SkillWindow); // the cost question: not yet
        var cost = P4Training.TrainingCost(T.Training, 1, 12, P4Town.PriceIndex(T.Towns, 2));
        Assert.Equal(new P4TrainingWindow(m.Name, P4TrainingNote.Cost, cost, false), w.TrainingWindow);
        w.Answer(true);
        string? trained = null;
        for (var left = 3; left > 0; left--)
        {
            var s = w.SkillWindow!;
            Assert.Equal(P4TownPrompt.Pick, w.Prompt);
            Assert.Null(w.ListWindow); // its own window, not a strip list
            Assert.Equal("_sklup", P4SkillWindow.Name);
            Assert.Equal((left, false, trained), (s.Left, s.Done, s.Trained));
            Assert.Equal(GameText.S("p4x.0x416b6") + left + GameText.S("p4x.0x416c2"), s.Prompt);
            // _sklpr: every skill in skill order, now and after (_calsk), the row's choice = the skill = the line.
            Assert.Equal(P4Ids.Skills, s.Rows.Count);
            Assert.Equal(w.Choices.Count, s.Rows.Count);
            for (var k = 0; k < P4Ids.Skills; k++)
            {
                Assert.Equal(new P4SkillRow(P4Names.At(Data.Names.Skills, k), m.Skills[k],
                    P4LevelUp.SkillAfter(T, m, (P4Skill)k), k), s.Rows[k]);
                Assert.StartsWith(s.Rows[k].Skill, w.Choices[k].Text, StringComparison.Ordinal);
            }
            var pick = left == 2 ? P4Skill.Swim : P4Skill.Attack;
            var after = s.Rows[(int)pick].After;
            w.Choose((int)pick);
            Assert.Equal(after, m.Skill(pick));
            trained = P4Names.At(Data.Names.Skills, (int)pick) + GameText.S("p4x.0x416dc");
            if (left > 1)
                Assert.Equal(after, w.SkillWindow!.Rows[(int)pick].Now); // the picked row redrawn
        }
        // The end: no prompt, the last trained line, done (the bar's "This training session is over."), kept up through
        // _trang's member pick (s156) with _trang's own window.
        Assert.Equal(P4TownPrompt.Who, w.Prompt);
        var done = w.SkillWindow!;
        Assert.Equal((0, true, null, trained), (done.Left, done.Done, done.Prompt, done.Trained));
        Assert.Equal(m.Skill(P4Skill.Swim), done.Rows[(int)P4Skill.Swim].Now);
        Assert.Equal(P4TrainingNote.Cost, w.TrainingWindow!.Note);
        w.Choose(0); // the next member pick opens window 32 again: the skill window goes
        Assert.Null(w.SkillWindow);
        Assert.Equal((m.Name, P4TrainingNote.Cost), (w.TrainingWindow!.Member, w.TrainingWindow.Note));
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
    }

    [OriginalDataFact]
    public void Training_ShortOfExperience_SaysHowMuch()
    {
        var party = Party();
        var w = Town(party, o: Code);
        w.Enter(P4Building.Guild);
        w.Choose(1);
        var need = P4Training.ExperienceNeeded(T.Training, P4Ids.Fighter, party.Members[0].Abilities, 1);
        Assert.Equal(P4TownPrompt.Who, w.Prompt);
        Assert.Contains(w.Box, l => l.Contains(need.ToString(), StringComparison.Ordinal));
        Assert.Equal(new P4TrainingWindow(party.Members[0].Name, P4TrainingNote.NeedsExperience, need, false),
            w.TrainingWindow); // s156: _trang's window, kept through the member pick
        w.Choose(-1); // SPACE: _clrtdraw, _wclose
        Assert.Null(w.TrainingWindow);
    }

    [OriginalDataTheory]
    [InlineData(false, 16)]
    [InlineData(true, 1)]
    public void D4_060_Level15_GoesOnOrWrapsToOne(bool x68000, int expected)
    {
        var o = x68000 ? Code : Manual;
        var m = Member("A", P4Ids.Wizard, 15);
        P4LevelUp.Begin(o, T, m);
        P4LevelUp.Finish(o, T, new ScriptRng(), m);
        Assert.Equal(expected, m.Level);
        // The magic level adds the levels above 14 to the level-14 column.
        Assert.Equal(Math.Min(255, T.Training.MagicLevel[P4Ids.Wizard][14] + 1), m.MagicLevel);
    }

    [OriginalDataFact]
    public void LevelUp_OpensTheRowsSpells_ButKeepsKnownOnes()
    {
        var m = Member("W", P4Ids.Wizard, 1);
        var row = T.Training.SpellsGranted[P4Ids.Wizard][1];
        m.Spells[row[0] - 1] = 1;
        P4LevelUp.Finish(Manual, T, new ScriptRng(), m);
        Assert.Equal(1, m.Spells[row[0] - 1]);
        Assert.All(row.Skip(1), s => Assert.Equal(2, m.Spells[s - 1]));
    }

    [OriginalDataFact]
    public void MagicHall_SellsAnOpenSpell_AndAgesTheLearner()
    {
        var party = Party();
        var m = party.Members[0];
        m.Class = P4Ids.Wizard;
        m.Level = 3;
        m.Spells[8] = 2; // spell 9 open
        var w = Town(party, new ScriptRng(), Code);
        party.Gold = 20000;
        w.Enter(P4Building.Guild);
        w.Choose(2); // M
        Assert.Equal(P4TownPrompt.Pick, w.Prompt);
        Assert.Equal(P4Names.At(Data.Names.Spells, 8), w.Choices[0].Text);
        var price = P4Training.SpellPrice(T.Training, P4Ids.Wizard, 3, 15, 12, P4Town.PriceIndex(T.Towns, 2));
        var age = m.AgeUnits;
        w.Choose(0);
        Assert.Equal(20000 - price, party.Gold);
        Assert.Equal(age + 2, m.AgeUnits);
        Assert.True(m.Knows(9)); // rand(100) = 1 < 5 × Int
        w.Continue();
        Assert.Equal(P4TownPrompt.Who, w.Prompt); // no open spell left: the member is done
    }

    [OriginalDataFact]
    public void MagicHall_SpellPage_IsGetmagicsWindow()
    {
        // s151: _getmagic(member, 8): the page's 13 spells lettered A…M, the name and button only for open ones (_splist);
        // the page buttons 1–5 and 6 Quit above stand for the choices after the spells.
        var party = Party();
        var m = party.Members[0];
        m.Class = P4Ids.Wizard;
        m.Level = 3;
        m.Spells[2] = 2; // spell 3 open
        m.Spells[8] = 2; // spell 9 open
        var w = Town(party, new ScriptRng(), Code);
        party.Gold = 20000;
        w.Enter(P4Building.Guild);
        w.Choose(2); // M
        var list = w.ListWindow!;
        Assert.Equal("_getmagic.8", list.Name);
        Assert.Equal(T.Service.MagicHallPage, list.Rows.Count);
        Assert.Equal((P4Names.At(Data.Names.Spells, 2), (int?)0), (list.Rows[2].Text, list.Rows[2].Choice));
        Assert.Equal((P4Names.At(Data.Names.Spells, 8), (int?)1), (list.Rows[8].Text, list.Rows[8].Choice));
        Assert.Equal(((string?)null, (int?)null), (list.Rows[0].Text, list.Rows[0].Choice));
        Assert.Equal([2, 3, 4, 5, 6, 7], list.HeadChoices);
        Assert.Equal(w.Choices.Count - 1, list.HeadChoices[^1]); // 6: Quit
        w.Choose(list.HeadChoices[1]); // page 2: no open spell there
        Assert.All(w.ListWindow!.Rows, r => Assert.Null(r.Choice));
    }

    [OriginalDataFact]
    public void MagicHall_Window_OfferThenTheResultRow_KeptThroughTheMemberPick()
    {
        // s165: _mage's window 32 (FORMAT.md 6.9): the offer with the price, after a pick the result row at (16, 64).
        var party = Party();
        var m = party.Members[0];
        m.Class = P4Ids.Wizard;
        m.Level = 3;
        m.Spells[8] = 2;
        var w = Town(party, new ScriptRng(), Code);
        party.Gold = 20000;
        w.Enter(P4Building.Guild);
        w.Choose(2); // M
        var price = P4Training.SpellPrice(T.Training, P4Ids.Wizard, 3, 15, 12, P4Town.PriceIndex(T.Towns, 2));
        Assert.Equal(new P4TrainingWindow("M0", P4TrainingNote.Offer, price, false, P4TrainingWindow.MagicHall),
            w.TrainingWindow);
        w.Choose(0);
        Assert.Equal(P4LessonResult.Learned, w.TrainingWindow!.Result);
        w.Continue();
        Assert.Equal(P4TownPrompt.Who, w.Prompt);
        Assert.Equal(P4LessonResult.Learned, w.TrainingWindow!.Result); // window 32 stays up through the member pick
        w.Choose(-1); // E: End training
        Assert.Null(w.TrainingWindow);
    }

    [OriginalDataFact]
    public void MagicHall_Window_Refusals_AndShortOfGold()
    {
        var party = Party();
        foreach (var x in party.Members)
            (x.Class, x.Level) = (P4Ids.Wizard, 3);
        party.Members[0].Condition = 2; // Sleep: poor health
        party.Members[2].Score = 125;   // after an ending, no spell open: nothing more (record +09 >= 125, 0x35A8)
        var w = Town(party, new ScriptRng(), Code);
        w.Enter(P4Building.Guild);
        w.Choose(2);
        Assert.Equal((P4TrainingNote.PoorCondition, P4TrainingWindow.MagicHall),
            (w.TrainingWindow!.Note, w.TrainingWindow.Hall));
        w.Choose(1); // no open spell: not yet worthy (0x35F2)
        Assert.Equal(P4TrainingNote.NotWorthy, w.TrainingWindow!.Note);
        w.Choose(2);
        Assert.Equal(P4TrainingNote.NothingMoreToTeach, w.TrainingWindow!.Note);
        party.Members[1].Spells[8] = 2;
        party.Gold = 0;
        w.Choose(1);
        Assert.Equal(P4TrainingNote.Offer, w.TrainingWindow!.Note);
        w.Choose(0);
        Assert.True(w.TrainingWindow!.ShortOfGold); // "You do not have enough gold." and the member is done
        Assert.Equal(P4TownPrompt.Who, w.Prompt);
    }

    [OriginalDataFact]
    public void Creation_Lists_AreDrawnAsTheOriginalsWindows()
    {
        // s151: _racemenu / _classmenu / _set_fame_lst name their window-34 lists; the rows are the choices one for one.
        var g = Town(Party(), o: Code);
        g.Enter(P4Building.Guild);
        g.Choose(0);
        g.Choose(1); // N: creation
        Assert.Null(g.ListWindow); // _makemenu: the icon panel
        g.Choose(2); // No: a plain character
        Assert.Equal("_racemenu", g.ListWindow!.Name);
        Assert.Equal(g.Choices.Select(c => c.Text), g.ListWindow.Rows.Select(r => r.Text));
        Assert.Equal(Enumerable.Range(0, 7).Cast<int?>(), g.ListWindow.Rows.Select(r => r.Choice));
        g.Choose(0); // the first race
        Assert.Equal("_classmenu", g.ListWindow!.Name);
        Assert.Equal(g.Choices.Select(c => c.Text), g.ListWindow.Rows.Select(r => r.Text));
        g.Choose(0);
        Assert.Equal((P4TownPrompt.Confirm, (P4ListWindow?)null), (g.Prompt, g.ListWindow));
        g.Answer(false);
        g.Choose(1); // Yes: a descendant
        g.Choose(1); // H: a person from the Hall
        var hall = g.ListWindow!;
        Assert.Equal("_set_fame_lst", hall.Name);
        Assert.Equal(g.HallPersons.Select(h => h.Name), hall.Rows.Select(r => r.Text));
        Assert.All(hall.Rows, r => Assert.True(r.Extra!.Length <= 3)); // the race's first three characters
    }

    // --- Guild roster -----------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void FormParty_PicksInOrder_ChoosesTheElemental_AndAgesTravellers()
    {
        var party = Party(3);
        var w = Town(party, o: Code);
        party.Members[1].Town = 5; // as if M1 had been left in another town
        w.Enter(P4Building.Guild);
        w.Choose(0); // G: roster
        w.Choose(0); // P: form party (clears the party first)
        Assert.Empty(party.Members);
        w.Choose(2); // slot 2 first
        w.Choose(1);
        w.Choose(-1);
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
        var age = party.Roster[1]!.AgeUnits;
        w.Answer(true);
        Assert.Equal(P4TownPrompt.Pick, w.Prompt); // the elemental
        w.Choose(1);
        Assert.Equal(1, party.Elemental);
        Assert.Equal(["M2", "M1"], party.Members.Select(m => m.Name));
        Assert.Equal(age + 4, party.Members[1].AgeUnits);
        Assert.All(party.Members, m => Assert.Equal(2, m.Town));
        Assert.Equal(1, party.StoryFlags[P4Party.AttitudeFlag]);
    }

    [OriginalDataFact]
    public void Roster_DeleteAndRename()
    {
        var party = Party(3);
        var w = Town(party, o: Code);
        w.Enter(P4Building.Guild);
        w.Choose(0);
        w.Choose(4); // R: rename
        w.Choose(0);
        Assert.Equal(P4TownPrompt.Text, w.Prompt);
        w.EnterText("ARTHURIUSXX");
        w.Answer(true);
        Assert.Equal("ARTHURIU", party.Roster[0]!.Name); // 8 characters
        w.Choose(-1);
        w.Choose(3); // D: delete
        w.Choose(1);
        w.Answer(true);
        Assert.Null(party.Roster[1]);
    }

    /// <summary>
    /// s185 (Pad leftovers "Rename takes a blank name"): as <c>_re_name</c> (0x7520) reads it, an empty name (its first byte
    /// 0, tested at 0x7564) goes back to the pick with no question, while a name of spaces (<c>_wscan</c> 0x34550 takes
    /// 0x20) is asked and kept. D4-073 (PENDING) puts the spaces case to the user; until then the original's rule stays.
    /// </summary>
    [OriginalDataFact]
    public void Rename_AnEmptyNameChangesNothing_ANameOfSpacesIsTaken()
    {
        var party = Party(3);
        var w = Town(party, o: Code);
        var was = party.Roster[0]!.Name;
        w.Enter(P4Building.Guild);
        w.Choose(0);
        w.Choose(4); // R: rename
        w.Choose(0);
        w.EnterText("");
        Assert.Equal(P4TownPrompt.Pick, w.Prompt); // back to the roster pick, no Okay / Cansel
        Assert.Equal(was, party.Roster[0]!.Name);
        w.Choose(0);
        w.EnterText("  ");
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
        w.Answer(true);
        Assert.Equal("  ", party.Roster[0]!.Name);
    }

    // --- Bank ---------------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Bank_DepositsByShares_AndWithdrawsUnderTheCap()
    {
        var party = Party(2, gold: 0);
        var w = Town(party, o: Code);
        party.Gold = 3000;
        w.Enter(P4Building.Bank);
        w.Choose(0); // D
        Assert.Equal(P4TownPrompt.Shares, w.Prompt);
        w.EnterShares([2, 1]);
        Assert.Equal(0, party.Gold);
        Assert.Equal([1998L, 999L], party.Members.Select(m => m.Account)); // 3000 / 3.0001 = 999 per share
        w.Continue();
        w.Choose(1); // W
        Assert.Equal(P4TownPrompt.Number, w.Prompt);
        Assert.Equal(1998, w.NumberLimit);
        w.EnterNumber(5000); // cut to the limit
        w.EnterNumber(100);
        Assert.Equal(2098, party.Gold);
        Assert.Equal([0L, 899L], party.Members.Select(m => m.Account));
        w.Continue();
        w.Choose(2);
        Assert.Equal(P4TownPrompt.Menu, w.Prompt);
    }

    // --- Armory -------------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Armory_BuysAtTheTownPrice_AndMarksWhatAMemberCannotUse()
    {
        var party = Party(1, gold: 0);
        var w = Town(party, o: Code, town: 13); // Skull City: 120 %
        party.Gold = 100000;
        Array.Clear(party.ShopStock);
        party.ShopStock[40] = 1; // Long Sword (needs 2 Str + Dex ≥ 51)
        party.ShopStock[1] = 2;
        party.Members[0].Abilities[0] = 15;
        w.Enter(P4Building.Armory);
        Assert.Equal([1, 40], w.ShopPage);
        Assert.Contains("N-U", w.Choices[1].Text, StringComparison.Ordinal);
        // _itemlist draws each row's icon (font 2, _idat b0 + 0x20): the rows carry their item; MORE / L / members none.
        Assert.Equal([1, 40, 0, 0], w.Choices.Take(4).Select(c => c.Item));
        var price = P4Town.BuyPrice(Data.Items.Value(1), 120);
        w.Choose(0);
        Assert.Equal(100000 - price, party.Gold);
        Assert.Equal(1, party.ShopStock[1]);
        Assert.Contains(1, party.Members[0].Items);
        Assert.Contains(w.Box, l => l.Contains(Data.Items.Name(1), StringComparison.Ordinal));
        w.Choose(w.Choices.Count - 2); // L
        Assert.Equal(P4TownPrompt.Menu, w.Prompt);
    }

    [OriginalDataFact]
    public void Items_CarryTheirIconGlyph_IdatB0()
    {
        // items.json "icon" = _idat b0 (FORMAT.md section 6.5): 0–49, the font-2 glyph 0x20 + b0 the item lists draw.
        Assert.Null(Data.Items.Icon(0));
        Assert.All(Enumerable.Range(1, 156), id => Assert.InRange(Data.Items.Icon(id)!.Value, 0, 49));
        Assert.Equal(50, Enumerable.Range(0, 157).Select(id => Data.Items[id]!.Icon).Distinct().Count());
    }

    [OriginalDataFact]
    public void TownLoad_AddsEightRandomItems_FromTheLevelSum()
    {
        var party = Party(3);
        // Sum of levels 3 → rand(2 × 3 + 5) = rand(11); the script gives 1 → entry 2 every time.
        var w = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(1, 1, 1, 1, 1, 1, 1, 1), Data);
        w.Arrive(2);
        Assert.Equal(8, party.ShopStock[T.Towns.ShopRandomItems[2]]);
    }

    // --- Distribution, exchange and selling ---------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Arrival_SharesOutThePack_HighestIdFirst()
    {
        var party = Party(2);
        party.Pack[0] = 1;   // Wooden Shield
        party.Pack[5] = 90;  // a healing potion
        party.Pack[9] = 130; // a treasure
        var w = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(), Data);
        w.Arrive(2);
        Assert.Equal(P4TownPrompt.Pick, w.Prompt);
        Assert.Equal(130, w.HeldItem);
        w.Choose(2); // P: back to the pack
        Assert.Equal(90, w.HeldItem);
        w.Choose(1); // to M1
        Assert.Equal(1, w.HeldItem);
        var gold = party.Gold;
        w.Choose(3); // S: sell
        Assert.Equal(gold + Data.Items.Value(1) / 4 + 1, party.Gold);
        Assert.Equal((1, 2), (party.GoodsCount[1], party.GoodsTown[1]));
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        w.Continue();
        Assert.Equal(P4TownPrompt.Menu, w.Prompt);
        Assert.Equal(130, party.Pack[0]);
        Assert.Contains(90, party.Members[1].Items);
    }

    [OriginalDataFact]
    public void Trade_MovesAnItemBetweenMembers()
    {
        var party = Party(2);
        party.Members[0].Items[0] = 79;
        var w = Town(party, o: Code);
        w.Command(P4TownCommand.Trade);
        Assert.Equal([79, 0], w.Choices.Take(2).Select(c => c.Item)); // _tritmlst: an empty slot has no icon
        w.Choose(0); // take M0's slot 0
        Assert.Equal(79, w.HeldItem);
        w.Choose(P4Member.CarriedSlots + 1); // give to M1
        Assert.Contains(79, party.Members[1].Items);
        Assert.DoesNotContain(79, party.Members[0].Items);
        w.Choose(w.Choices.Count - 1); // L: Done
        Assert.Equal(P4TownPrompt.Menu, w.Prompt);
    }

    // --- Leaving, the inn, the Hall, the ending -----------------------------------------------------------------------------

    [OriginalDataFact]
    public void Leaving_WritesMeleeBonus_MaxScore_AndRoundedAverage()
    {
        var party = Party(2);
        party.Members[0].Level = 4;
        party.Members[1].Level = 5;
        party.Members[1].Score = 130;
        party.Members[0].Abilities[0] = 17;
        var w = Town(party, o: Code);
        w.Enter(P4Building.Leave);
        Assert.Equal(130, party.MaxScore);
        Assert.Equal(5, party.AverageLevel); // trunc(4.5 + 0.5)
        Assert.Equal((int)Math.Truncate(17 / 6 + 0.5 * 4 / 11.0), party.Members[0].MeleeBonus);
    }

    [OriginalDataFact]
    public void Inn_MayRaiseMaxMp_AfterACombatCast()
    {
        var party = Party(1);
        var m = party.Members[0];
        (m.MagicLevel, m.MpMax, m.MpGrowth) = (10, 4, true);
        var window = new P4MessageWindow();
        P4Town.InnStay(party, window, T.Service, new ScriptRng(100)); // 100 ≤ (10 − 4) × 20
        Assert.Equal(5, m.MpMax);
        Assert.False(m.MpGrowth);
        Assert.Equal(5, m.Mp);
        P4Town.InnStay(party, window, T.Service, new ScriptRng(1));
        Assert.Equal(5, m.MpMax); // the flag is cleared
    }

    [OriginalDataFact]
    public void Hall_ShowsTheShippedPersons_AndTheirDeeds()
    {
        Assert.Equal(["Lord Wood", "Filmon", "Nikademus"], Data.Hall.Select(h => h.Name));
        Assert.Equal(("p4x.0x41bc8", "p4x.0x41be0"), P4TownLoop.HallDeeds(Data.Hall[0]));
        var imported = Data.Hall[0] with { Slot = 3, Results = [100, 0, 0] };
        Assert.Equal("p4x.0x41c35", P4TownLoop.HallDeeds(imported).First);
        var party = Party();
        var w = Town(party, o: Code, town: 1);
        w.Enter(P4Building.HallOfGlory);
        w.Choose(1);
        w.Choose(2);
        Assert.Equal("Nikademus", w.HallPerson!.Name);
    }

    [OriginalDataFact]
    public void Scene_FollowsWhatTheOriginalShows()
    {
        // FORMAT.md §6.9 (s107): the street at the menu, each building's picture, the Guild's only at its own menu.
        var party = Party();
        var w = Town(party, o: Code, town: 1);
        Assert.Equal(P4TownScene.Street, w.Scene);
        w.Enter(P4Building.Inn);
        Assert.Equal(P4TownScene.Inn, w.Scene);
        w.Continue();
        Assert.Equal(P4TownScene.Street, w.Scene);
        w.Enter(P4Building.Mystic);
        Assert.Equal(P4TownScene.Mystic, w.Scene);
        w.Continue();
        w.Enter(P4Building.Guild);
        Assert.Equal(P4TownScene.Guild, w.Scene);
        w.Choose(0); // the roster clears the picture (_mlist → _clrtdraw)
        Assert.Equal(P4TownScene.None, w.Scene);
        w.Choose(-1);
        Assert.Equal(P4TownScene.Guild, w.Scene);
        w.Choose(3);
        Assert.Equal(P4TownScene.Street, w.Scene);
        w.Enter(P4Building.Armory);
        Assert.Equal(P4TownScene.None, w.Scene);
    }

    [OriginalDataFact]
    public void Scene_NoStreetDuringTheArrivalPrompts()
    {
        // _town: _clrtdraw, then _townent / _welcome; _tload draws the street only once the menu opens.
        var party = Party();
        party.ExperiencePool = 30;
        var w = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(), Data);
        w.Arrive(2);
        Assert.Equal(P4TownPrompt.Shares, w.Prompt);
        Assert.Equal(P4TownScene.None, w.Scene);
        while (w.Prompt != P4TownPrompt.Menu)
        {
            if (w.Prompt == P4TownPrompt.Shares)
                w.EnterShares([.. party.Members.Select(_ => 1)]);
            else
                w.Continue();
        }
        Assert.Equal(P4TownScene.Street, w.Scene);
    }

    [OriginalDataFact]
    public void Scene_HallHistory_StepsThroughTheNinePictures()
    {
        var w = Town(Party(), o: Code, town: 1);
        w.Enter(P4Building.HallOfGlory);
        Assert.Equal(P4TownScene.Hall, w.Scene);
        w.Choose(0);
        for (var page = 0; page < P4TownLoop.HistoryPages.Length; page++)
        {
            Assert.Equal((P4TownScene.HallHistory, page), (w.Scene, w.HallHistoryPage));
            w.Continue();
        }
        Assert.Equal(P4TownScene.Hall, w.Scene);
    }

    [OriginalDataFact]
    public void IconMenu_NamesTheOriginalsPanel_AndHistoryLLeavesAtOnce()
    {
        // s138: each screen names its _mkicon panel as it yields; null = the default (_spc_wait / _confirm).
        var w = Town(Party(), o: Code, town: 1);
        Assert.Null(w.IconMenu);
        w.Enter(P4Building.HallOfGlory);
        Assert.Equal("_hall", w.IconMenu);
        w.Choose(0);
        // _hhist: pages 1–8 wait on its L / N panel, the last one on _spc_wait.
        for (var page = 0; page < P4TownLoop.HistoryPages.Length - 1; page++)
        {
            Assert.Equal(("_hhist", true), (w.IconMenu, w.CanLeaveHistory));
            w.Continue();
        }
        Assert.Equal((P4TownPrompt.Wait, (string?)null, false), (w.Prompt, w.IconMenu, w.CanLeaveHistory));
        Assert.Throws<InvalidOperationException>(w.LeaveHistory);
        w.Continue();
        Assert.Equal("_hall", w.IconMenu);

        w.Choose(0);
        w.Continue();
        w.LeaveHistory(); // L on page 2: back to the Hall's menu
        Assert.Equal((P4TownScene.Hall, P4TownPrompt.Pick, "_hall"), (w.Scene, w.Prompt, w.IconMenu));
        w.Choose(1);
        Assert.Equal("_hprofmenu", w.IconMenu);
        w.Choose(0);
        Assert.Equal((P4TownPrompt.Wait, (string?)null), (w.Prompt, w.IconMenu)); // _dsp_hinfo: _spc_wait

        var g = Town(Party(), o: Code);
        g.Enter(P4Building.Guild);
        Assert.Equal("_guildmenu", g.IconMenu);
        g.Choose(1);
        Assert.Equal((P4TownPrompt.Who, "_trang"), (g.Prompt, g.IconMenu));
        g.Choose(-1);
        g.Choose(0);
        Assert.Equal("_mlistmenu", g.IconMenu);
        g.Choose(1); // N: creation
        Assert.Equal("_makemenu", g.IconMenu);
        g.Choose(1); // Yes: a descendant
        Assert.Equal("_make1menu", g.IconMenu);
        g.Choose(-1);
        g.Choose(0); // E: back to the roster
        g.Choose(3); // D: Delete
        Assert.Equal("_mlistsubset", g.IconMenu);
        g.Choose(0);
        Assert.Equal((P4TownPrompt.Confirm, (string?)null), (g.Prompt, g.IconMenu)); // _dislodge: _confirm
    }

    [OriginalDataTheory]
    [InlineData(new[] { 0 }, 125, 0)]
    [InlineData(new[] { 0, 1, 2 }, 175, 3)]
    [InlineData(new[] { 0, 1, 2, 3 }, 200, 9)]
    [InlineData(new[] { 0, 1, 2, 3, 4 }, 225, 9)]
    public void Ending_ScoresAndRanks(int[] flags, int score, int social)
    {
        var party = Party(2);
        party.Members[0].Social = 0;
        party.Members[1].Social = 0;
        foreach (var f in flags)
            party.StoryFlags[P4TownLoop.EndingFlag + f] = 1;
        var w = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(), Data);
        w.Arrive(P4TownLoop.Pendragon);
        while (w.Prompt == P4TownPrompt.Wait)
            w.Continue();
        Assert.Equal(P4TownPrompt.Ending, w.Prompt);
        Assert.Equal(score, w.EndingScore);
        Assert.All(party.Members, m => Assert.Equal(social, m.Social));
        Assert.All(party.Members, m => Assert.Equal(score + 1, m.Score)); // + the rounded average level 1
        Assert.Contains(w.Box, l => l.Contains(score.ToString(), StringComparison.Ordinal));
        Assert.Contains(w.Box, l => l.Contains(GameText.S("p4x.0x413fb"), StringComparison.Ordinal)); // "Duck"
    }

    [OriginalDataFact]
    public void Ending_FinalScreen_WaitsThenAsksToSave_BeepsBeforeAndAfterTheAnswer()
    {
        // s158, _endstory 0xAB88–0xAC26: lines 0–7 (the STARCRAFT line last), _spc_wait, the question (line 8), _sdbeep,
        // _confirm, YES → _disk(1), then _sdbeep and _acgamend(1) on either answer.
        var party = Party(2);
        party.StoryFlags[P4TownLoop.EndingFlag] = 1;
        var w = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(), Data);
        w.Arrive(P4TownLoop.Pendragon);
        int Beeps() => party.Sounds.Since(0).Count(m => m == P4Sounds.EndingFinal);
        IReadOnlyList<string> lastWait = [];
        while (w.Prompt == P4TownPrompt.Wait)
        {
            lastWait = [.. w.Box];
            Assert.Equal(0, Beeps());
            w.Continue();
        }
        Assert.Contains(lastWait, l => l.Contains("STARCRAFT", StringComparison.Ordinal)); // the wait shows lines 0–7
        Assert.DoesNotContain(GameText.S("p4x.0x4139f"), lastWait);
        Assert.Equal((P4TownPrompt.Ending, P4EndingStep.Question, "_confirm"), (w.Prompt, w.EndingStep, w.IconMenu));
        Assert.Equal(GameText.S("p4x.0x4139f"), w.Box[^1]); // "<<Save the game?>>"
        Assert.Equal(1, Beeps());

        var saved = w.Capture(); // the shell's save at the question (_disk(1))
        w.AnswerEnding();
        Assert.Equal((P4TownPrompt.Ending, P4EndingStep.Answered, (string?)null), (w.Prompt, w.EndingStep, w.IconMenu));
        Assert.Equal(2, Beeps());
        Assert.Throws<InvalidOperationException>(w.AnswerEnding);

        var back = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(), Data);
        back.Restore(saved); // a save made at the question runs the ending again, up to the question
        while (back.Prompt == P4TownPrompt.Wait)
            back.Continue();
        Assert.Equal((P4TownPrompt.Ending, P4EndingStep.Question), (back.Prompt, back.EndingStep));
    }

    [OriginalDataTheory]
    [InlineData(new[] { 0 }, 200, 9)]
    [InlineData(new[] { 0, 1 }, 200, 9)]
    [InlineData(new[] { 0, 1, 2, 3 }, 200, 9)]
    [InlineData(new[] { 0, 1, 2, 3, 4 }, 225, 9)]
    public void Ending_SavedAtTheFinalScreen_ReloadRunsAnEndingAgain(int[] flags, int score, int social)
    {
        // ending.md §1.1: the save holds Pendragon; _game → _town → _ending with the flags the first ending left.
        var party = Party(2);
        foreach (var f in flags)
            party.StoryFlags[P4TownLoop.EndingFlag + f] = 1;
        var w = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(), Data);
        w.Arrive(P4TownLoop.Pendragon);
        while (w.Prompt == P4TownPrompt.Wait)
            w.Continue();
        Assert.Equal(P4TownPrompt.Ending, w.Prompt);
        var saved = w.Capture();

        var back = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(), Data);
        back.Restore(saved);
        Assert.Equal(P4TownScene.EndingStreet, back.Scene);
        while (back.Prompt == P4TownPrompt.Wait)
            back.Continue();
        Assert.Equal(P4TownPrompt.Ending, back.Prompt);
        Assert.Equal(score, back.EndingScore);
        Assert.All(party.Members, m => Assert.Equal(social, m.Social));
        Assert.All(party.Members, m => Assert.Equal(score + 1, m.Score));
    }

    [OriginalDataTheory]
    [InlineData(new[] { 0 }, new[] { P4TownScene.EndingStreet, P4TownScene.EndingFinal })]
    [InlineData(new[] { 0, 1, 2 }, new[] { P4TownScene.EndingStreet, P4TownScene.EndingBanquet, P4TownScene.EndingFinal })]
    [InlineData(new[] { 0, 1, 2, 3 }, new[] { P4TownScene.EndingStreet, P4TownScene.EndingBanquet, P4TownScene.EndingCell,
        P4TownScene.EndingFinal })]
    [InlineData(new[] { 0, 1, 2, 3, 4 }, new[] { P4TownScene.EndingStreet, P4TownScene.EndingBanquet, P4TownScene.EndingFinal })]
    public void Ending_Pictures_StreetThenBanquetThenCell(int[] flags, P4TownScene[] expected)
    {
        // ending.md §1, FORMAT.md §6.9: _endset (street + END1), _banquet (END2) before blocks 4 / 7, _arathamus (END3) before 9;
        // s162: _endstory's own titled window, no picture.
        var party = Party(2);
        foreach (var f in flags)
            party.StoryFlags[P4TownLoop.EndingFlag + f] = 1;
        var w = new P4TownLoop(Code, T, Atlas, party, new ScriptRng(), Data);
        w.Arrive(P4TownLoop.Pendragon);
        var seen = new List<P4TownScene> { w.Scene };
        while (w.Prompt == P4TownPrompt.Wait)
        {
            w.Continue();
            if (seen[^1] != w.Scene)
                seen.Add(w.Scene);
        }
        Assert.Equal(expected, seen);
    }

    // --- Party menus --------------------------------------------------------------------------------------------------------

    private static P4Menus Menus(P4Party party, IRandomSource? rng = null, GameOptions? o = null) =>
        new(o ?? Manual, T, party, rng ?? new ScriptRng(), Data);

    [OriginalDataFact]
    public void D4_061_CastLists_ByPlace()
    {
        Assert.Contains(13, P4TownRules.CastList(Code, T.Service, P4CastPlace.Town));
        Assert.DoesNotContain(13, P4TownRules.CastList(Manual, T.Service, P4CastPlace.Town));
        Assert.DoesNotContain(63, P4TownRules.CastList(Code, T.Service, P4CastPlace.Outdoors));
        Assert.Contains(63, P4TownRules.CastList(Manual, T.Service, P4CastPlace.Outdoors));
        Assert.Equal([1, 2, 3, 4, 13, 40, 15, 49, 50, 63], P4TownRules.CastList(Code, T.Service, P4CastPlace.Dungeon));
    }

    [OriginalDataFact]
    public void Cast_Healing_OnAMember_CostsMp()
    {
        var party = Party(2);
        var c = party.Members[0];
        c.Spells[0] = 1;
        c.Mp = 4;
        party.Members[1].Hp = 5;
        var menus = Menus(party, new ScriptRng(6), Code);
        menus.Open(P4MenuCommand.Cast, P4CastPlace.Outdoors, 9);
        menus.Choose(0); // caster
        Assert.Equal(P4MenuPrompt.Pick, menus.Prompt);
        // s151: _getmagic's window: every spell of the place lettered, only castable ones named and clickable; SPACE above.
        var list = menus.ListWindow!;
        Assert.Equal("_getmagic", list.Name);
        Assert.Equal(P4TownRules.CastList(Code, T.Service, P4CastPlace.Outdoors).Count, list.Rows.Count);
        Assert.Equal((P4Names.At(Data.Names.Spells, 0), (int?)0), (list.Rows[0].Text, list.Rows[0].Choice));
        Assert.All(list.Rows.Skip(1), r => Assert.Equal(((string?)null, (int?)null), (r.Text, r.Choice)));
        Assert.Equal([-1], list.HeadChoices);
        menus.Choose(0); // Healing 1
        Assert.Null(menus.ListWindow);
        Assert.Equal(P4MenuPrompt.Who, menus.Prompt);
        menus.Choose(1);
        Assert.Equal(10, party.Members[1].Hp); // rand(10) − 1 = 5
        Assert.Equal(4 - P4Combat.MpCost(Code, T.Combat, 1), c.Mp);
        Assert.Equal(P4MenuPrompt.Who, menus.Prompt); // s156: no wait after a healing spell (_pmagic3 / _ocast)
        Assert.Contains(menus.Box, l => l.Contains(party.Members[1].Name, StringComparison.Ordinal)); // its line stays
        menus.Choose(-1);
        Assert.Equal(P4MenuPrompt.Closed, menus.Prompt);
    }

    [OriginalDataFact]
    public void Cast_Transportation_MovesTheCampaignToTheTown()
    {
        var party = Party(1);
        var m = party.Members[0];
        m.Spells[46] = 1;
        m.Mp = 10;
        var game = Phantasie4World.NewCampaign(Code, party, new ScriptRng(), new P4Location(P4Place.Town, 2, 0, 0));
        game.Start();
        Assert.Equal(P4TownPrompt.Menu, game.Town.Prompt);
        game.OpenMenu(P4MenuCommand.Cast);
        var menus = game.Menus!;
        menus.Choose(0);
        menus.Choose(menus.Choices.ToList().FindIndex(c => c.Text == P4Names.At(Data.Names.Spells, 46)));
        Assert.Equal((P4MenuPrompt.Pick, "_cast_47"), (menus.Prompt, menus.IconMenu)); // s140: the "Transport" panel
        Assert.Equal("_cast_47", menus.ListWindow!.Name); // s151: the towns under its icon, one row per choice
        Assert.Equal(menus.Choices.Count, menus.ListWindow.Rows.Count);
        menus.Choose(4); // E: the fifth town of group 0 = town 5
        Assert.Equal((P4MenuPrompt.Confirm, (string?)null), (menus.Prompt, menus.IconMenu)); // _confirm
        menus.Answer(true);
        menus.Continue(); // the menu closes once the party moved
        Assert.Equal(P4MenuPrompt.Closed, menus.Prompt);
        game.Follow();
        Assert.Equal(P4Place.Town, game.Place);
        Assert.Equal(5, game.Town.Town);
    }

    [OriginalDataFact]
    public void MagicBag_TakesHundredGold_ForAnItem()
    {
        var party = Party(1, gold: 150);
        var m = party.Members[0];
        m.Spells[49] = 1;
        m.Mp = 10;
        var menus = Menus(party, new ScriptRng(3));
        menus.Open(P4MenuCommand.Cast, P4CastPlace.Outdoors, 9);
        menus.Choose(0);
        menus.Choose(0); // the only castable spell
        menus.Continue();
        menus.Continue();
        menus.Answer(true);
        Assert.Equal(50, party.Gold);
        Assert.Equal(T.Service.MagicBagItems[2], party.Pack[0]);
    }

    [OriginalDataFact]
    public void PartyInfo_CountsThePack_AndCyclesTheStrategy()
    {
        var party = Party(1);
        party.Pack[0] = 2;
        party.Pack[1] = 90;
        party.Pack[2] = 95;
        var menus = Menus(party);
        Assert.Equal([1, 0, 0, 0, 2, 0], menus.ItemsFound());
        menus.Open(P4MenuCommand.PartyInfo, P4CastPlace.Outdoors, 9);
        menus.Choose(0);
        menus.Choose(0);
        Assert.Equal(2, party.StoryFlags[P4Party.AttitudeFlag]);
        menus.Choose(-1);
        Assert.Equal(P4MenuPrompt.Closed, menus.Prompt);
    }

    [OriginalDataFact]
    public void PartyInfo_D4_062_ManualShowsGainsSinceTown_CodeShowsPurseAndPool()
    {
        var party = Party(1);
        party.Gold = 500;
        party.GoldSinceTown = 120;
        party.ExperiencePool = 37;
        var manual = Menus(party, o: Manual);
        manual.Open(P4MenuCommand.PartyInfo, P4CastPlace.Outdoors, 9);
        Assert.Contains(manual.Box, l => l.EndsWith(" 120", StringComparison.Ordinal));
        Assert.Contains(manual.Box, l => l.EndsWith(" 370", StringComparison.Ordinal));
        var code = Menus(party, o: Code);
        code.Open(P4MenuCommand.PartyInfo, P4CastPlace.Outdoors, 9);
        Assert.Contains(code.Box, l => l.EndsWith(" 500", StringComparison.Ordinal));
        Assert.Contains(code.Box, l => l.EndsWith(" 37", StringComparison.Ordinal));
        P4TownRules.Leave(party);
        Assert.Equal(0, party.GoldSinceTown);
    }

    [OriginalDataFact]
    public void Use_HealingPotion_IsDrunkByTheChosenMember()
    {
        var party = Party(2);
        party.Members[0].Items[0] = 0x55 + 2; // Healing 3
        party.Members[1].Hp = 1;
        var menus = Menus(party, new ScriptRng(50)); // not spoiled
        menus.Open(P4MenuCommand.Use, P4CastPlace.Town, 2);
        menus.Choose(0);
        Assert.Equal(0x57, menus.HeldItem);
        Assert.Equal(0, party.Members[0].Items[0]);
        menus.Choose(P4Member.CarriedSlots + 1);
        Assert.Equal(10, party.Members[1].Hp); // 9 HP
        menus.Choose(menus.Choices.Count - 1);
        Assert.Equal(P4MenuPrompt.Closed, menus.Prompt);
    }

    [OriginalDataFact]
    public void Use_SpoiledPotion_GoesStraightBackToTheItemPick()
    {
        var party = Party(2);
        party.Members[0].Items[0] = 0x55 + 2;
        var menus = Menus(party, new ScriptRng(99, 0)); // rand(100) > 90: spoiled
        menus.Open(P4MenuCommand.Use, P4CastPlace.Town, 2);
        menus.Choose(0);
        menus.Choose(P4Member.CarriedSlots + 1);
        // _spoile: its line, the damage, no _spc_wait (s156): the item pick again, the line still shown.
        Assert.Equal((P4MenuPrompt.Pick, "_setuse"), (menus.Prompt, menus.IconMenu));
        Assert.Contains(menus.Box, l => l.Contains(GameText.S("p4x.0x425c4").Trim(), StringComparison.Ordinal));
        Assert.Equal(0, menus.HeldItem);
    }

    [OriginalDataFact]
    public void Profile_PageTwo_CyclesTheStrategy()
    {
        var party = Party(1);
        var menus = Menus(party);
        menus.Open(P4MenuCommand.Profile, P4CastPlace.Town, 2);
        menus.Choose(0);
        Assert.Equal(1, menus.Profile!.Value.Page);
        menus.Choose(0); // next page
        Assert.Equal(2, menus.Profile!.Value.Page);
        menus.Choose(3); // D: movement 3 → 4
        Assert.Equal(4, party.Members[0].Strategy[3]);
    }

    [OriginalDataFact]
    public void Profile_PagesGoAsTheCodeDoes_NextBackAndSpaceLeaves()
    {
        // _detail / _page2 / _page3 (s140, static [C]): N 1 → 2 → 3, page 3's N back to 2; SPACE leaves on every page.
        var party = Party(1);
        var menus = Menus(party);
        menus.Open(P4MenuCommand.Profile, P4CastPlace.Town, 2);
        Assert.Equal((P4MenuPrompt.Who, "_inspect"), (menus.Prompt, menus.IconMenu));
        menus.Choose(0);
        Assert.Equal((1, "_detailmenu"), (menus.Profile!.Value.Page, menus.IconMenu));
        menus.Choose(0); // N
        Assert.Equal((2, "_p_info3menu", 6), (menus.Profile!.Value.Page, menus.IconMenu, menus.Choices.Count));
        menus.Choose(5); // N
        Assert.Equal((3, "_page3"), (menus.Profile!.Value.Page, menus.IconMenu));
        Assert.Equal(GameText.S("p4x.0x42a3e"), menus.Choices[0].Text); // "N:Previous page"
        menus.Choose(0); // N: back
        Assert.Equal(2, menus.Profile!.Value.Page);
        menus.Choose(-1); // SPACE on page 2 leaves the sheet
        Assert.Null(menus.Profile);
        Assert.Equal((P4MenuPrompt.Who, "_inspect"), (menus.Prompt, menus.IconMenu));
        menus.Choose(0);
        menus.Choose(-1); // SPACE on page 1
        Assert.Equal(P4MenuPrompt.Who, menus.Prompt);
        menus.Choose(-1);
        Assert.Equal(P4MenuPrompt.Closed, menus.Prompt);
    }

    [OriginalDataFact]
    public void PartyMenus_NameTheOriginalsPanels()
    {
        // s140: each party-menu prompt names its _mkicon panel or window-36 row (FORMAT.md 6.9); waits default to
        // _spc_wait (null), the spell list leaves the strip empty ("": _getmagic's window takes it).
        var party = Party(2);
        var c = party.Members[0];
        c.Spells[0] = 1;
        c.Spells[14] = 1; // Identify (15)
        c.Mp = 20;
        party.Members[1].Items[0] = 0x55;
        var menus = Menus(party, new ScriptRng(6), Code);
        menus.Open(P4MenuCommand.PartyInfo, P4CastPlace.Outdoors, 9);
        Assert.Equal("_p_info1menu", menus.IconMenu);
        menus.Choose(-1);
        menus.Open(P4MenuCommand.Use, P4CastPlace.Outdoors, 9);
        Assert.Equal("_setuse", menus.IconMenu);
        Assert.Equal(0, menus.Choices[0].Item); // M0 carries nothing
        menus.Choose(menus.Choices.Count - 1);
        menus.Open(P4MenuCommand.Cast, P4CastPlace.Outdoors, 9);
        Assert.Equal("_ocast", menus.IconMenu);
        menus.Choose(0);
        Assert.Equal((P4MenuPrompt.Pick, ""), (menus.Prompt, menus.IconMenu));
        menus.Choose(0); // Healing 1: whom?
        Assert.Equal((P4MenuPrompt.Who, "_ocast.2"), (menus.Prompt, menus.IconMenu));
        menus.Choose(1); // s156: healing has no _spc_wait (_pmagic3): straight back to the caster pick
        Assert.Equal((P4MenuPrompt.Who, "_ocast"), (menus.Prompt, menus.IconMenu));
        menus.Choose(0);
        menus.Choose(menus.Choices.ToList().FindIndex(x => x.Text == P4Names.At(Data.Names.Spells, 14)));
        Assert.Equal((P4MenuPrompt.Who, "_setident"), (menus.Prompt, menus.IconMenu));
        menus.Choose(1);
        Assert.Equal((P4MenuPrompt.Pick, "_setident"), (menus.Prompt, menus.IconMenu));
        Assert.Equal(0x55, menus.Choices[0].Item);
        menus.Choose(0); // the potion: its appraisal, _spc_wait
        Assert.Equal((P4MenuPrompt.Wait, (string?)null), (menus.Prompt, menus.IconMenu));
        menus.Continue(); // _ident: one item, then the spell ends; _ocast has no wait of its own (s156)
        Assert.Equal((P4MenuPrompt.Who, "_ocast"), (menus.Prompt, menus.IconMenu));
    }

    // --- Saves ----------------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Save_KeepsTheRoster_TheShop_AndTheNewRecordBytes()
    {
        var party = Party(2);
        party.Roster[5] = Member("Spare");
        party.ShopStock[7] = 3;
        (party.GoodsCount[9], party.GoodsTown[9]) = (2, 4);
        party.Members[0].Strategy[4] = 5;
        party.Members[0].MpGrowth = true;
        party.Elemental = 2;
        var game = Phantasie4World.NewCampaign(Manual, party, new ScriptRng(), new P4Location(P4Place.Town, 2, 0, 0));
        var json = Phantasie4SaveCodec.WriteJson(game.Capture());
        var back = Phantasie4World.RestoreCampaign(Phantasie4SaveCodec.ReadJson(json), new ScriptRng());
        Assert.Equal("Spare", back.Party.Roster[5]!.Name);
        Assert.Equal(3, back.Party.ShopStock[7]);
        Assert.Equal((2, 4), (back.Party.GoodsCount[9], back.Party.GoodsTown[9]));
        Assert.Equal(5, back.Party.Members[0].Strategy[4]);
        Assert.True(back.Party.Members[0].MpGrowth);
        Assert.Equal(2, back.Party.Elemental);
        Assert.Equal(json, Phantasie4SaveCodec.WriteJson(back.Capture()));
    }

    [OriginalDataFact]
    public void Capture_IsRefused_WithAPotionInHand()
    {
        var party = Party(2);
        party.Members[0].Items[0] = 0x57;
        var game = Phantasie4World.NewCampaign(Code, party, new ScriptRng(), new P4Location(P4Place.Town, 2, 0, 0));
        game.Start();
        game.OpenMenu(P4MenuCommand.Use);
        game.Menus!.Choose(0);
        Assert.Equal(0x57, game.Menus.HeldItem);
        Assert.False(game.CanSave);
        Assert.Throws<InvalidOperationException>(() => game.Capture());
    }
}
