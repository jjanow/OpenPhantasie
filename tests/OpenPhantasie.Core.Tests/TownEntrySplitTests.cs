using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// D-037: in I and II the fight pool's XP and gold are divided "when they enter a town" (manual PDF p.15; DOS I
/// <c>0x1954</c> → <c>0x167d</c>, II ST <c>0xbda6</c>, I Amiga h8 <c>+0x16d6</c>), with Core's equal split
/// (<see cref="TownMenus.Distribute"/>). Found items stay for the town's Distribute. III keeps its own payout (D3-071).
/// </summary>
public class TownEntrySplitTests
{
    private static GameSession TwoInParty(GameDataRepository data, GameOptions options, out Character a, out Character b)
    {
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        s.StartNewGame(options);
        s.OpenGuild();
        a = s.CreateAndEnroll("ALPHA", Race.Human, CharacterClass.Fighter);
        b = s.CreateAndEnroll("BRAVO", Race.Elf, CharacterClass.Priest);
        Assert.True(s.TryAddToParty(a, out _));
        Assert.True(s.TryAddToParty(b, out _));
        s.LeaveGuild();
        return s;
    }

    private static GameSession I(string remainder, string treasury, out Character a, out Character b) =>
        TwoInParty(GameDataRepository.LoadPhantasie1(),
            GameOptions.ForPreset(GameId.P1).With(RuleOptionRegistry.PayoutRemainder.Id, remainder).With("treasury", treasury),
            out a, out b);

    [OriginalDataFact]
    public void I_Entry_SplitsXpAndGold_AmongTheLiving_ItemsWait()
    {
        var s = I("first-survivor", "off", out var a, out var b);
        var (xpA, xpB, purse) = (a.Experience, b.Experience, s.Party.GoldCarried);
        s.Sack.DepositRewards(101, 51);
        Assert.True(s.Sack.TryAdd(1));

        s.EnterTown(1);

        Assert.Equal(xpA + 51, a.Experience); // 50 each, the remainder to the first survivor
        Assert.Equal(xpB + 50, b.Experience);
        Assert.Equal(purse + 51, s.Party.GoldCarried);
        Assert.Equal((0, 0), (s.Sack.Experience, s.Sack.Gold));
        Assert.Equal(1, s.Sack.ItemCount); // the hand-out is still the town's Distribute
    }

    [OriginalDataFact]
    public void I_Entry_DosRemainder_IsLost()
    {
        var s = I("dropped", "off", out var a, out var b);
        var (xpA, xpB) = (a.Experience, b.Experience);
        s.Sack.DepositRewards(101, 0);

        s.EnterTown(1);

        Assert.Equal((xpA + 50, xpB + 50), (a.Experience, b.Experience));
        Assert.Equal(0, s.Sack.Experience);
    }

    [OriginalDataFact]
    public void I_Entry_NobodyLiving_KeepsThePool()
    {
        var s = I("first-survivor", "off", out var a, out var b);
        a.Status = CharacterStatus.Dead;
        b.Status = CharacterStatus.Dead;
        var xp = a.Experience;
        s.Sack.DepositRewards(100, 40);

        s.EnterTown(1);

        Assert.Equal((100, 40), (s.Sack.Experience, s.Sack.Gold));
        Assert.Equal(xp, a.Experience);
    }

    [OriginalDataFact]
    public void I_Entry_DeadMember_GetsNoShare()
    {
        var s = I("first-survivor", "off", out var a, out var b);
        b.Status = CharacterStatus.Dead;
        var (xpA, xpB) = (a.Experience, b.Experience);
        s.Sack.DepositRewards(100, 0);

        s.EnterTown(1);

        Assert.Equal((xpA + 100, xpB), (a.Experience, b.Experience));
    }

    [OriginalDataFact]
    public void I_Entry_WithTreasury_BanksTheGold_SplitsTheXp()
    {
        var s = I("first-survivor", "trip", out var a, out var b);
        var (xpA, before) = (a.Experience, s.Treasury.Balance);
        s.Sack.DepositRewards(100, 60);

        s.EnterTown(1);

        Assert.Equal(before + 60, s.Treasury.Balance);
        Assert.Equal(0, s.Party.GoldCarried);
        Assert.Equal(xpA + 50, a.Experience);
        Assert.Equal((0, 0), (s.Sack.Experience, s.Sack.Gold));
    }

    [OriginalDataFact]
    public void II_Entry_SplitsThePool()
    {
        var s = TwoInParty(GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2)),
            GameOptions.ForPreset(GameId.P2).With("treasury", "off"), out var a, out var b);
        var (xpA, xpB, purse) = (a.Experience, b.Experience, s.Party.GoldCarried);
        s.Sack.DepositRewards(80, 20);

        s.EnterTown(1);

        Assert.Equal((xpA + 40, xpB + 40), (a.Experience, b.Experience));
        Assert.Equal(purse + 20, s.Party.GoldCarried);
        Assert.Equal((0, 0), (s.Sack.Experience, s.Sack.Gold));
    }

    [OriginalDataFact]
    public void III_Entry_KeepsItsOwnPayout()
    {
        var s = TwoInParty(GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3)),
            GameOptions.ForPreset(GameId.P3), out _, out _);
        s.Sack.DepositRewards(80, 0);

        s.EnterTown(1);

        Assert.Equal(80, s.Sack.Experience); // D3-071: the "Shares for" prompts divide it
        Assert.True(s.TownXpSharesPending);
    }
}
