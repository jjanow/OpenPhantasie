using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s163: III's "Who will disarm the trap?" (DOS ovl4 <c>0x1189…0x11bc</c>, Amiga <c>0x1c6d6…0x1c71a</c>, static): the member
/// picker asks again while the pick is Dead or Out, and its cancel leaves the trap as "No" does (nothing sprung, the cell
/// kept). And III's out-of-fight healing says "The character's health improves..." (DOS root <c>0x19be</c>, Amiga
/// <c>0xfd42</c>), where I's prints nothing.
/// </summary>
public class P3DisarmPickerTests
{
    /// <summary>The party on the archives' trap 2 at (23, 8), its "Who will disarm the trap?" waiting.</summary>
    private static (GameSession S, SessionPrompts Prompts) AtWhoWillDisarm()
    {
        var s = WithParty();
        Phantasie3DungeonTests.Enter(s, 5, 23, 8);
        Assert.Equal(2, s.CampaignAccess.CurrentDungeonLevel.Get(23, 8));
        s.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(2, -1,
            new DungeonMessage([GameText.F("core.p3.x_spots_a_trap", s.Party.Members[0].Name)],
                [SessionPrompts.Yes, SessionPrompts.No]));
        var prompts = new SessionPrompts(s);
        prompts.Answer(1); // OK: who disarms?
        Assert.True(Phantasie3Campaign.AsksDisarmer(s.DungeonEventPrompt!));
        return (s, prompts);
    }

    [OriginalDataFact]
    public void Cancel_LeavesTheTrap_NothingSprung()
    {
        var (s, prompts) = AtWhoWillDisarm();
        var who = prompts.Current!;
        Assert.Equal(s.Party.Members.Count + 1, who.EscapeChoice);
        var hp = s.Party.Members.Select(m => m.HpCur).ToList();
        prompts.Answer(who.EscapeChoice!.Value);
        Assert.Null(s.DungeonEventPrompt);
        Assert.Equal(2, s.CampaignAccess.CurrentDungeonLevel.Get(23, 8)); // the trap is still there
        Assert.Equal((s.Dungeon!.PrevX, s.Dungeon.PrevY), (s.Dungeon.X, s.Dungeon.Y)); // back, as No
        Assert.Equal(hp, s.Party.Members.Select(m => m.HpCur).ToList());
        Assert.DoesNotContain(GameText.S("core.p3.a_trap"), s.Log);
    }

    [OriginalDataTheory]
    [InlineData(CharacterStatus.Dead)]
    [InlineData(CharacterStatus.Out)]
    public void DeadOrOutPick_AsksAgain(CharacterStatus status)
    {
        var (s, prompts) = AtWhoWillDisarm();
        s.Party.Members[1].Status = status;
        var hp = s.Party.Members.Select(m => m.HpCur).ToList();
        prompts.Answer(2);
        Assert.NotNull(s.DungeonEventPrompt); // the picker again
        Assert.True(Phantasie3Campaign.AsksDisarmer(s.DungeonEventPrompt!));
        Assert.Equal(2, s.CampaignAccess.CurrentDungeonLevel.Get(23, 8));
        Assert.Equal(hp, s.Party.Members.Select(m => m.HpCur).ToList());
    }

    [OriginalDataFact]
    public void Success_ClearsTheTrap_ThePartyBackWhereItCameFrom()
    {
        var (s, prompts) = AtWhoWillDisarm();
        var d = s.Dungeon!;
        (d.PrevX, d.PrevY) = (23, 7);
        var thief = s.Party.Members[3];
        thief.Skills = thief.Skills with { DisarmTrap = 100 }; // rnd(100) <= 100: always
        prompts.Answer(4);
        Assert.Equal(Phantasie3Campaign.SpentTrapCell, s.CampaignAccess.CurrentDungeonLevel.Get(23, 8));
        Assert.Equal((23, 7), (d.X, d.Y)); // step result -1, as No
        Assert.Contains(GameText.F("core.p3.x_disarms_the_trap", thief.Name), s.Log);
    }

    [OriginalDataFact]
    public void OutOfFightHealing_SaysTheHealthImproves_InIII()
    {
        var s = WithParty();
        var priest = s.Party.Members[1];
        priest.HpCur = 1;
        var r = SpellCaster.CastOutOfCombat(s.Party, priest, SpellId.HealingI, priest, new SeededDosRandomSource(3),
            CastLocation.Field, rules: s.Options);
        Assert.True(r.Success);
        Assert.Equal("The character's health improves...", r.Message);
    }
}
