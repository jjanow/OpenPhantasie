using OpenPhantasie.Data;
using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s97 T4 leftovers: the fight-find question (root <c>0xf2eb</c>), DOS's dungeon entry question (ovl4 <c>0x6fe</c>, D3-038
/// <c>dos</c>), and Guild Drop with an elemental loaded (ovl2 <c>0x3c92</c>).
/// </summary>
public class P3LeftoverTests
{
    private static GameSession Reload(GameSession s, Action<SessionSnapshot> edit)
    {
        var snap = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        edit(snap);
        var t = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(3));
        t.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(snap)));
        return t;
    }

    [OriginalDataFact]
    public void FightFind_AsksDosTwoLines_DungeonItemKeepsKeepX_FinderSurvivesASave()
    {
        var s = WithParty();
        Assert.True(s.LeaveTown());
        var item = Phantasie3CampaignTests.Data.Items.First(i => i.Id == 66).Name;
        var t = Reload(s, snap =>
        {
            snap.PendingTreasure = [66, 67];
            snap.PendingTreasureFinders = ["HERO1", null];
        });
        var prompts = new SessionPrompts(t);
        var p = prompts.Current!;
        Assert.Equal(SessionPromptKind.Treasure, p.Kind);
        Assert.Equal([$"HERO1 finds {item}", "Will the party keep it?"], p.Lines); // DOS: no full stop after the item
        Assert.Equal(2, p.Options.Count); // Yes / No, Yes first (0xf2eb: "Yes" at column 12, "No" at 25)
        Assert.Equal(["HERO1", null], t.PendingTreasureFinders);
        Assert.False(p.Remembers); // s98: 0xf2eb opens on Yes every time, no menu id memory
        Assert.Null(p.MenuId);
        Assert.Equal(2, p.EscapeChoice); // 0x75a3 returns 0 (No) for Esc

        prompts.Answer(yes: false);
        var other = Phantasie3CampaignTests.Data.Items.First(i => i.Id == 67).Name;
        var keep = prompts.Current!;
        Assert.Equal([GameText.F("core.treasure.you_find_x_keep_it", other)], keep.Lines); // a finder-less offer: dungeon op 6's question
        // s102 [C]: op 6 → ovl4 0x1b20 → root 0x1820(text, 1) → the same menu 0x75a3 with highlight 1: Yes every time, Esc = No.
        Assert.False(keep.Remembers);
        Assert.Null(keep.MenuId);
        Assert.Equal(2, keep.EscapeChoice);
        prompts.Answer(keep.EscapeChoice!.Value);
        Assert.Empty(t.PendingTreasureFinders);
    }

    [OriginalDataFact]
    public void DosEntry_AsksLoadPreviouslySavedDungeon_YesLoadsTheMap_NoTheOriginal_SlotKept()
    {
        var u = WithParty(options: GameOptions.ForPreset(GameId.P3).With("p3.dungeon-map-no", "dos"));
        Assert.True(u.LeaveTown());
        var x = Campaign(u).World.DungeonEntranceX[4];
        u.EnterDungeon(5, x, 0);
        u.Dungeon!.Level!.SetBufferByte(Phantasie3Campaign.EventSquares, 0x45);
        u.AnswerDungeonExit(takeIt: true, saveMap: true);

        foreach (var load in new[] { true, false })
        {
            var t = Reload(u, snap =>
            {
                snap.Wilderness!.EntryKind = OverworldEntryKind.Dungeon;
                snap.Wilderness.EntryId = 5;
                snap.Wilderness.EntryDestX = x;
                snap.Wilderness.EntryName = "A dungeon";
            });
            var prompts = new SessionPrompts(t);
            Assert.Equal(["A dungeon Will you enter?"], prompts.Current!.Lines);
            prompts.Answer(yes: true);
            Assert.Equal(GameMode.Overworld, t.Mode);
            Assert.Equal(["Load previously saved dungeon?"], prompts.Current!.Lines);

            var saved = Reload(t, _ => { }); // a save taken at the second question keeps it
            Assert.True(saved.EntryPrompt!.AsksLoad);

            prompts.Answer(yes: load);
            Assert.Equal(GameMode.Dungeon, t.Mode);
            Assert.Equal(load ? 0x45 : Phantasie3CampaignTests.Data.CreateDungeonTraversal().GetLevel(5).GetBufferByte(Phantasie3Campaign.EventSquares),
                t.Dungeon!.Level!.GetBufferByte(Phantasie3Campaign.EventSquares));
            Assert.Equal([5], t.DungeonSaves.Keys.Order()); // DOS leaves DNP<letter> on disk either way
        }

        // The manual side (default) asks nothing more: YES enters with the saved map.
        var m = WithParty();
        Assert.True(m.LeaveTown());
        var mt = Reload(m, snap =>
        {
            snap.Wilderness!.EntryKind = OverworldEntryKind.Dungeon;
            snap.Wilderness.EntryId = 5;
            snap.Wilderness.EntryDestX = x;
        });
        mt.AnswerOverworldEntry(true);
        Assert.Equal(GameMode.Dungeon, mt.Mode);
    }

    [OriginalDataFact]
    public void Drop_KeepsTheLoadedElementalWhileACharacterIsLeft()
    {
        Assert.True(Phantasie3CampaignTests.Data.Rules.ElementalRulesOrDefault.DropKeeps);
        var s = WithParty(members: 2);
        s.OpenGuild();
        Assert.True(s.TryAddElemental(0, out var err), err);
        var first = s.Party.Members[0];
        Assert.True(GuildHall.TryDropFromParty(s.Party, first, out err), err);
        Assert.NotNull(s.Party.Elemental); // ovl2 0x3c92: the elemental moves up and stays last
        Assert.True(GuildHall.TryDropFromParty(s.Party, s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental); // 0x3e0b: no character left, everything cleared
    }

    [OriginalDataFact]
    public void TrainingPickScreen_ListsTheSkillsInDosOrder()
    {
        var s = WithParty(members: 1);
        var rows = s.TrainingSkillRows(s.Party.Members[0], []);
        Assert.Equal(
            [P3Skill.Attack, P3Skill.Parry, P3Skill.Swim, P3Skill.Listen, P3Skill.FindTrap, P3Skill.Disarm, P3Skill.FindItem,
                P3Skill.PickLock, P3Skill.FireBow], // ovl2 0x18b0: name table DS:0x1438
            rows.Select(r => r.Skill));
    }

    [Fact]
    public void Drop_PhantasieI_StillClearsTheElemental()
    {
        Assert.False(ElementalRules.PhantasieI.DropKeeps);
        var party = new Party();
        var a = new Character { Name = "A" };
        party.Members.Add(a);
        party.Members.Add(new Character { Name = "B" });
        party.Elemental = new Character { Name = "AIR" };
        Assert.True(GuildHall.TryDropFromParty(party, a, out _));
        Assert.Null(party.Elemental);
    }
}
