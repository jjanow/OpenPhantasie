using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// PLAN P3 rules toggles (T5.0 descriptors): Armory restock (D-054), gold cap (D-048) and renaming party members (D-065).
/// Each is pinned for its preset values and for both behaviours through a session locked to it.
/// </summary>
public class P3RuleOptionsTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static GameSession NewGame(GameOptions options, int seed = 3)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        s.StartNewGame(options);
        return s;
    }

    private static GameOptions With(string id, string value) => GameOptions.ForPreset(GameId.P1).With(id, value);

    /// <summary>A new game with one party member, back at the Pelnor town menu.</summary>
    private static (GameSession Session, Character Hero) TownWithHero(GameOptions options, int seed = 3)
    {
        var s = NewGame(options, seed);
        s.OpenGuild();
        var hero = s.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(hero, out _));
        s.LeaveGuild();
        return (s, hero);
    }

    // ---- presets --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("p1.armory-restock", "manual", "dos", "manual", "D-054")]
    [InlineData("p1.gold-cap", "manual", "dos", "lifted", "D-048")]
    [InlineData("p1.rename-party", "refused", "allowed", "allowed", "D-065")]
    public void Descriptor_PresetValues(string id, string manual, string dos, string remaster, string difference)
    {
        var d = RuleOptionRegistry.For(GameId.P1).Single(o => o.Id == id);
        Assert.Equal(manual, d.Manual);
        Assert.Equal(dos, d.Dos);
        Assert.Equal(remaster, d.Remaster);
        Assert.Equal(difference, d.Difference);
        Assert.Equal(manual, d.Values[0]); // the manual's value is listed first
        Assert.Equal(manual, GameOptions.ForPreset(GameId.P1).Get(id));
    }

    [Fact]
    public void Presets_GiveTheTypedRules()
    {
        var manual = GameOptions.ForPreset(GameId.P1);
        Assert.False(manual.DosArmoryRestock);
        Assert.Equal(GoldCapRule.Manual, manual.GoldCap);
        Assert.False(manual.RenamePartyMembers);

        var dos = GameOptions.ForPreset(GameId.P1, OptionPreset.Dos);
        Assert.True(dos.DosArmoryRestock);
        Assert.Equal(GoldCapRule.Dos, dos.GoldCap);
        Assert.True(dos.RenamePartyMembers);

        var remaster = GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster);
        Assert.False(remaster.DosArmoryRestock);
        Assert.Equal(GoldCapRule.Lifted, remaster.GoldCap);
        Assert.True(remaster.RenamePartyMembers);
    }

    // ---- Armory restock (D-054) -----------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData("manual")]
    [InlineData("dos")]
    public void ArmoryRestock_OnTownEntry_FollowsTheSavesRule_D054(string value)
    {
        var s = NewGame(With("p1.armory-restock", value), seed: 21);
        var town = s.CurrentTown!;
        var armorAndShields = Enumerable.Range(1, 40).ToArray();
        foreach (var id in armorAndShields)
            town.SetStock(id, 5);

        s.EnterTown(1); // average exit level 0: 18 wipes over ids 0..119, no restock draws

        var lowered = armorAndShields.Where(id => s.CurrentTown!.GetStock(id) < 5).ToList();
        if (value == "manual")
            Assert.Empty(lowered); // the manual: armor and shields stay
        else
            Assert.NotEmpty(lowered); // DOS: 18 wipes over 120 ids land on some of the 40 armor / shield ids
    }

    // ---- Gold cap (D-048) -----------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData("manual", 65_000L)]
    [InlineData("dos", 65_535L)]
    [InlineData("lifted", (long)int.MaxValue)]
    public void GoldCap_SetsThePurseBankAndSackCaps_D048(string value, long cap)
    {
        var s = NewGame(With("p1.gold-cap", value));
        Assert.Equal(cap, s.Party.GoldCap);
        Assert.Equal(cap, s.Party.BankGoldCap);
        Assert.Equal(cap, s.Sack.GoldCap);
    }

    [OriginalDataTheory]
    [InlineData("manual", 65_000L, 15_000L)]
    [InlineData("dos", 65_535L, 14_465L)]
    [InlineData("lifted", 80_000L, 0L)]
    public void GoldCap_WithdrawKeepsUpToTheCap_TheRestIsLost_D048(string value, long purse, long lost)
    {
        var (s, hero) = TownWithHero(With("p1.gold-cap", value));
        s.Party.GoldCarried = 60_000;
        hero.GoldInBank = 20_000;
        var r = s.BankWithdraw(hero, 20_000);
        Assert.Equal(TownTransactionKind.Ok, r.Kind);
        Assert.Equal(0, hero.GoldInBank); // the full amount leaves the account (DOS)
        Assert.Equal(purse, s.Party.GoldCarried);
        Assert.Equal(20_000 - lost, r.GoldDelta);
    }

    [OriginalDataTheory]
    [InlineData("manual", 65_000)]
    [InlineData("lifted", 100_000)]
    public void GoldCap_DepositAndFoundGold_FollowTheCap_D048(string value, int expected)
    {
        var (s, hero) = TownWithHero(With("p1.gold-cap", value));
        hero.GoldInBank = 50_000;
        s.Party.GoldCarried = 50_000;
        s.BankDeposit([1]);
        Assert.Equal(expected, hero.GoldInBank);

        s.Sack.DepositRewards(0, 70_000);
        s.Sack.DepositRewards(0, 30_000);
        Assert.Equal(Math.Min(expected, 100_000), s.Sack.Gold);
    }

    [OriginalDataFact]
    public void GoldCap_IsLockedIntoTheSave_D048()
    {
        var s = NewGame(With("p1.gold-cap", "lifted"));
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());
        var other = GameSessionFactory.Create(Data, new SeededDosRandomSource(4));
        Assert.Equal(65_000, other.Party.GoldCap);
        other.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal(GoldCapRule.Lifted, other.Options.GoldCap);
        Assert.Equal(GoldCaps.Lifted, other.Party.GoldCap);
        Assert.Equal(GoldCaps.Lifted, other.Sack.GoldCap);
    }

    [OriginalDataFact]
    public void GoldCap_Lifted_DosExportStaysValid_SplitStopsAt65535_AndBigBalancesClamp_D048()
    {
        var (s, hero) = TownWithHero(With("p1.gold-cap", "lifted"));
        s.OpenGuild();
        var second = s.CreateAndEnroll("Second", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(second, out _));
        s.LeaveGuild();
        hero.GoldInBank = 100_000;  // above any DOS word
        second.GoldInBank = 60_000;
        s.Party.GoldCarried = 20_000; // split 10,000 each into the banks for the save

        var r = DosSaveConverter.ExportFrom(s, Data);
        Assert.False(r.Blocked, string.Join("; ", r.Warnings));
        var guild = r.Files!.Guild!;
        int Bank(Character c)
        {
            var slot = Array.IndexOf(s.Roster.Slots, c);
            var at = slot * GuildCodec.RecordSize + GuildCodec.Field.BankGold;
            return guild[at] | guild[at + 1] << 8;
        }
        Assert.Equal(65_535, Bank(hero));   // clamped to the DOS word, listed
        Assert.Equal(65_535, Bank(second)); // the share stops at 65,535, the rest is lost and listed
        Assert.Contains(r.Warnings, w => w.Kind == DosSaveWarningKind.Clamp && w.Message.Contains("100000"));
        Assert.Contains(r.Warnings, w => w.Kind == DosSaveWarningKind.Clamp && w.Message.Contains("bank is full at 65535"));

        // The live game keeps its gold.
        Assert.Equal(100_000, hero.GoldInBank);
        Assert.Equal(20_000, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void DosImport_KeepsTheRunningRules_T50()
    {
        var (s, _) = TownWithHero(With("p1.gold-cap", "lifted"));
        var files = DosSaveConverter.ExportFrom(s, Data).Files!;
        var other = NewGame(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos), seed: 8);
        DosSaveConverter.ImportInto(other, files, Data);
        Assert.Equal(OptionPreset.Dos, other.Options.Preset); // a DOS save holds no remaster options
        Assert.Equal(GoldCaps.Dos, other.Party.GoldCap);
    }

    [OriginalDataTheory]
    [InlineData(OptionPreset.Dos, false)]      // 65,535 cap: a 65,200 balance is fine
    [InlineData(OptionPreset.Manual, true)]    // 65,000 cap: listed
    [InlineData(OptionPreset.Remaster, false)] // lifted: no cap to pass
    public void DosImport_BankWarning_FollowsTheSessionsGoldCap_D048(OptionPreset preset, bool warned)
    {
        var (s, hero) = TownWithHero(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
        hero.GoldInBank = 65_200;
        s.Party.GoldCarried = 0; // nothing for the export's split to add
        var files = DosSaveConverter.ExportFrom(s, Data).Files!;

        var other = NewGame(GameOptions.ForPreset(GameId.P1, preset), seed: 8);
        var warnings = DosSaveConverter.ImportInto(other, files, Data);
        Assert.Equal(warned, warnings.Any(w => w.Kind == DosSaveWarningKind.RuleBreak && w.Message.Contains("65200")));
        if (warned)
            Assert.Contains(warnings, w => w.Message.Contains("cap of 65000"));
        // The plain import (no session) keeps the game's own 65,000; GoldCapRule.Dos raises it to 65,535.
        Assert.Contains(DosSaveConverter.Import(files, Data).Warnings, w => w.Message.Contains("65200"));
        Assert.DoesNotContain(DosSaveConverter.Import(files, Data, GoldCapRule.Dos).Warnings, w => w.Message.Contains("65200"));
    }

    [OriginalDataTheory]
    [InlineData(OptionPreset.Dos, "DOS @252 value (D-003")]
    [InlineData(OptionPreset.Manual, "remaster bands (D-003)")]
    public void DosImport_LifespanNote_FollowsTheSessionsLifespans_D003(OptionPreset preset, string note)
    {
        var (s, _) = TownWithHero(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
        var files = DosSaveConverter.ExportFrom(s, Data).Files!;
        var warnings = DosSaveConverter.ImportInto(NewGame(GameOptions.ForPreset(GameId.P1, preset), seed: 8), files, Data);
        Assert.Contains(warnings, w => w.Message.Contains(note));
    }

    // ---- Rename party members (D-065) -----------------------------------------------------------------------------

    [OriginalDataFact]
    public void Rename_Refused_ForAPartyMember_UnderTheManual_D065()
    {
        var (s, hero) = TownWithHero(GameOptions.ForPreset(GameId.P1));
        s.OpenGuild();
        Assert.False(s.CanRename(hero));
        Assert.False(s.TryRename(hero, "Ali", out var error));
        Assert.Equal(GameSession.RenamePartyMemberRefusal, error);
        Assert.Equal("Hero", hero.Name);
    }

    [OriginalDataFact]
    public void Rename_Allowed_ForAPartyMember_UnderTheOption_D065()
    {
        var (s, hero) = TownWithHero(With("p1.rename-party", "allowed"));
        s.OpenGuild();
        Assert.True(s.CanRename(hero));
        Assert.True(s.TryRename(hero, "Ali", out _));
        Assert.Equal("Ali", hero.Name);
        Assert.Same(hero, s.Party.Members[0]); // the party holds the renamed record
        Assert.False(s.TryRename(hero, "Ali9999999", out var tooLong)); // INPUT NAME's rules still apply
        Assert.NotEqual("", tooLong);
    }
}
