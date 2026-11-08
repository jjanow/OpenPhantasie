using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// PLAN P3 toggles in the Client: the T)IME LAG presentation switch (D-073), the Withdraw field under the lifted gold cap
/// (D-048) and Guild R)ENAME's list under the rename option (D-065).
/// </summary>
public class P3TogglesTests
{
    private static readonly IReadOnlyList<(string Id, string Name)> OnePack = [("amiga", "Amiga")];

    /// <summary>A new game locked to <paramref name="options"/>, one member in the party, in the Guild.</summary>
    private static (GameSession Session, Character Hero) InGuild(GameOptions options)
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(5));
        s.StartNewGame(options);
        s.OpenGuild();
        var hero = s.CreateAndEnroll("HERO", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(hero, out _));
        return (s, hero);
    }

    private static GameOptions With(string id, string value) => GameOptions.ForPreset(GameId.P1).With(id, value);

    // ---- T)IME LAG (D-073) ----------------------------------------------------------------------------------------

    [Fact]
    public void TimeLag_Off_IsTheManualsTCommand_D073()
    {
        var g = new GameSettings();
        Assert.False(g.TimeLagInOptions);
        Assert.True(TimeLagDialog.IsCommand(GameMode.Overworld, g));
        Assert.True(TimeLagDialog.IsCommand(GameMode.Dungeon, g));
        Assert.False(TimeLagDialog.IsCommand(GameMode.Town, g));
        Assert.Contains(UiOverlays.FieldCommandsFor(GameMode.Overworld), l => l.Contains("T)IME LAG"));
        Assert.Contains(UiOverlays.FieldCommandsFor(GameMode.Dungeon), l => l.Contains("T)IME LAG L)OOK"));
    }

    [Fact]
    public void TimeLag_On_MovesToTheOptionsTab_AndTIsNoCommand_D073()
    {
        var g = new GameSettings { TimeLagInOptions = true };
        Assert.False(TimeLagDialog.IsCommand(GameMode.Overworld, g));
        Assert.False(TimeLagDialog.IsCommand(GameMode.Dungeon, g));
        Assert.DoesNotContain(UiOverlays.FieldCommandsFor(GameMode.Overworld, timeLagCommand: false), l => l.Contains("T)IME"));
        var dungeon = UiOverlays.FieldCommandsFor(GameMode.Dungeon, timeLagCommand: false);
        Assert.DoesNotContain(dungeon, l => l.Contains("T)IME"));
        Assert.Contains("L)OOK", dungeon);
    }

    [OriginalDataFact]
    public void TimeLag_OptionsRows_SetLagAndSound_AlsoInGame_D073()
    {
        var settings = new ClientSettings { TimeLag = 5, Sound = true };
        var o = new OptionsScreen(settings, _ => OnePack, g => g == GameId.P1);
        o.Open(ClientSessions.Overworld()); // in game: presentation still changes
        Assert.DoesNotContain(o.Lines(), l => l.Text.Contains(" TIME LAG (0-9)"));

        o.HandleKeys([Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down,
            Keys.Down, Keys.Down]); // → TIME LAG AND SOUND (I has AMIGA SCREENS, COMBAT SPEED s132, AMIGA AUDIO FILTER s160)
        o.HandleKeys([Keys.Enter]);
        Assert.True(settings.Game(GameId.P1).TimeLagInOptions);
        Assert.Contains(o.Lines(), l => l.Text.Contains("IN OPTIONS"));

        o.HandleKeys([Keys.Down, Keys.Right, Keys.Right]); // lag 5 → 7
        Assert.Equal(7, settings.TimeLag);
        o.HandleKeys([Keys.Down, Keys.Enter]); // sound off
        Assert.False(settings.Sound);
        Assert.True(o.Changed);
    }

    // ---- Gold cap (D-048) -----------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData("manual", 65_535)]
    [InlineData("dos", 65_535)]
    [InlineData("lifted", 100_000)]
    public void Withdraw_Field_Allows65535_OrTheWholeBalanceWhenLifted_D048(string value, int typed)
    {
        var (s, hero) = InGuild(With("p1.gold-cap", value));
        s.LeaveGuild();
        hero.GoldInBank = 100_000;
        var d = new WithdrawDialog();
        d.Open(hero);
        d.HandleKeys([Keys.Back, Keys.Back, Keys.Back, Keys.Back, Keys.Back, Keys.Back], s);
        Assert.Equal(0, d.Amount);
        if (value == "lifted")
        {
            d.HandleKeys([Keys.D1, Keys.D0, Keys.D0, Keys.D0, Keys.D0, Keys.D0], s); // a sixth digit fits
            Assert.Equal(typed, d.Amount);
            d.HandleKeys([Keys.Enter], s);
            Assert.Equal(0, hero.GoldInBank);
            Assert.Equal(100_000, s.Party.GoldCarried);
        }
        else
        {
            d.HandleKeys([Keys.D6, Keys.D5, Keys.D5, Keys.D3, Keys.D5], s);
            Assert.Equal(typed, d.Amount);
            d.HandleKeys([Keys.D0], s); // DOS's five-digit field drops the leading 6: 55350
            Assert.Equal(55_350, d.Amount);
        }
    }

    // ---- Rename (D-065) -------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Rename_ListsOnlyNonPartyMembers_UnderTheManual_D065()
    {
        var (s, _) = InGuild(GameOptions.ForPreset(GameId.P1));
        var d = new GuildPickDialog();
        Assert.Equal(GameSession.RenamePartyMemberRefusal, d.OpenRename(s)); // only a party member in the Guild
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void Rename_ListsAndRenamesAPartyMember_UnderTheOption_D065()
    {
        var (s, hero) = InGuild(With("p1.rename-party", "allowed"));
        var d = new GuildPickDialog();
        Assert.Null(d.OpenRename(s));
        Assert.Contains(d.Lines(s, ClientSessions.Data), l => l.Contains("HERO"));
        d.HandleKeys([Keys.Enter], s);
        d.HandleKeys([Keys.A, Keys.L, Keys.I, Keys.Enter], s);
        Assert.Equal("ALI", hero.Name);
        Assert.Same(hero, s.Party.Members[0]);
    }
}
