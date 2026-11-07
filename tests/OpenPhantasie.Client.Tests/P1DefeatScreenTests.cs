using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s153, I Amiga's party-defeat screens (h32 <c>+0</c> / <c>+0x55c</c>): they run by themselves; each line waits 180 polls
/// or a key; the sounds hold the screen. Live marks from FS-UAE (capture_s153_sounds.md, the second defeat, two members, no
/// keys), seconds from decor.pic's appearance.
/// </summary>
public class P1DefeatScreenTests
{
    private static GraphicsPack Pack => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;

    private static PartyDefeatResult Defeat() => new()
    {
        Fates = [(new Character { Name = "A", Race = Race.Human }, DefeatFate.Destroyed),
                 (new Character { Name = "B", Race = Race.Elf }, DefeatFate.Resurrected)],
        Log = ["YOUR PARTY HAS BEEN DEFEATED", "YOU HAVE ARRIVED AT THE ASTRAL PLANE", "THE GREAT DEMON APPEARS AND SPEAKS:",
               "A YOU HAVE BEEN DESTROYED", "B YOU HAVE BEEN RESURRECTED"],
    };

    private static P1DefeatScreen Screen(bool sound = true)
    {
        var pack = Pack;
        var fx = pack.DosoundEffects!;
        var image = fx.TryLoadImage()!;
        return new P1DefeatScreen(pack.DefeatScreen!, Defeat(), sound ? n => fx.Hold(image, n) : null);
    }

    [OriginalDataFact]
    public void Pack_HasTheAmigaScreens()
    {
        var l = Pack.DefeatScreen!;
        Assert.Equal((180, 300, 23, 20, 21), (l.LinePolls, l.EndPolls, l.GhostSound, l.DemonSound, l.FateSound));
        Assert.Equal(0.02187, l.PollSeconds);
    }

    [OriginalDataFact]
    public void Pack_HasTheCueSounds()
    {
        // s153: h65 +0x158's step tone, h59 / h41's 19 before the entry menus, h74's 27 before BLACK KNIGHTS.
        var cues = Pack.CueSounds!;
        Assert.Equal((31, 19, 27), (cues.Step, cues.OverworldEntry, cues.BlackKnights));
        Assert.Equal(12, cues.Resurrected); // s156: 12 after an out-of-fight Resurrection (h38 +0x466)
        // s158: the sheet's tone (h89 +0xd2), 22 before the town TRANSPORTATION's fade (h26 +0xe4a), the alerts' beep (h89 +0xe0).
        Assert.Equal((31, 22), (cues.SheetTone, cues.Transport));
        Assert.Equal(32, cues.Alerts!.Sound);
        Assert.Contains(cues.Alerts.Lines, l => l.Key == "core.guild.move_not_in_town" && l.Modes.Contains("guild"));
        Assert.DoesNotContain(cues.Alerts.Lines, l => l.Key == "core.inn.you_cant_afford_it"); // h71 +0xe76: no beep
    }

    /// <summary>
    /// s158 live (FS-UAE, the run's defeat counter patched to 4 through the debugger, then a defeat): the 5th defeat's sign
    /// "Sorry, we've run out of Demons" sits at the pack's (139, 39), every pixel of <c>decor.sorry-sign</c> equal to
    /// <c>p1-amiga-defeat-5th-sign-live</c> (file row = screen row + 5).
    /// </summary>
    [OriginalDataFact]
    public void FifthDefeatSign_MatchesTheLiveShot()
    {
        var l = Pack.DefeatScreen!;
        Assert.Equal((5, 139, 39), (l.SignDefeat, l.SignX, l.SignY));
        var root = Path.Combine(Pack.RootDirectory, "..", "..", "..");
        var (sw, sh, sign) = P1AmigaFontTests.Pixels(Path.Combine(root, "reverse-engineering", "amiga", "extracted", "ui", "decor-sorry-sign.png"));
        var (w, _, shot) = P1AmigaFontTests.Pixels(Path.Combine(root, "parity", "shots", "p1-amiga-defeat-5th-sign-live.png"));
        Assert.Equal((48, 40), (sw, sh)); // the blit is opaque: all 1920 pixels
        for (var y = 0; y < sh; y++)
            for (var x = 0; x < sw; x++)
                Assert.Equal(sign[y * sw + x], shot[(l.SignY + y + 5) * w + l.SignX + x]);
    }

    /// <summary>
    /// s158: I Amiga's alert lines beep as the h26 <c>+0x6e</c> callers do: a Guild refusal in the Guild, the map Cast's
    /// "NOT ENOUGH MAGIC" on the map only, a formatted line by its start; the town's other lines do not.
    /// </summary>
    [OriginalDataFact]
    public void AlertLines_BeepWhereTheAmigaDoes()
    {
        var beeps = new P2AlertBeeps(Pack.CueSounds!.Alerts!);
        Assert.True(beeps.Beeps(GameText.S("core.guild.party_is_full"), GameMode.Guild));
        Assert.True(beeps.Beeps(GameText.F("core.guild.you_need_x_more_xp", 120), GameMode.Guild));
        Assert.True(beeps.Beeps(GameText.S("core.magic.not_enough_mp"), GameMode.Overworld));
        Assert.False(beeps.Beeps(GameText.S("core.magic.not_enough_mp"), GameMode.Town));
        Assert.False(beeps.Beeps(GameText.S("core.inn.you_cant_afford_it"), GameMode.Overworld));
        Assert.Equal(1, beeps.BeepsFor([], [GameText.S("core.armory.not_enough_gold")], GameMode.Town));
    }

    [OriginalDataFact]
    public void Timeline_RunsByItself_OnTheLiveMarks()
    {
        var s = Screen();
        var read = Pack.DefeatScreen!.PictureRead;
        double At(double live) => read + live; // live marks counted from decor.pic
        const double tol = 0.05;
        Assert.True(s.At(read - 0.01).Black);
        var shown = s.At(read);
        Assert.False(shown.Black);
        Assert.Equal("YOUR PARTY HAS BEEN DEFEATED", shown.Text);
        Assert.Equal((0, 90, false), (shown.Ghosts, shown.DoorHeight, shown.Face));
        Assert.Equal(1, s.At(At(3.94)).Ghosts);                       // live 3.934 (180 polls), one a frame
        Assert.Equal(2, s.At(At(3.94 + 1 / 60.0)).Ghosts);
        Assert.Equal("YOU HAVE ARRIVED AT THE ASTRAL PLANE", s.At(At(4.55 + tol)).Text); // after sound 23 (live 4.55)
        Assert.InRange(s.DoorAt, At(8.65) - tol, At(8.65) + tol);    // live 8.65
        Assert.True(s.At(At(12)).DoorSinking);
        Assert.Equal(0, s.At(At(15.82 + tol)).DoorHeight);           // live 15.82
        Assert.False(s.At(At(20.38) - tol).Face);
        Assert.True(s.At(At(20.38) + tol).Face);                     // after Delay(180) and sound 20 (live 20.38)
        Assert.Equal(0, s.At(At(25.12) - tol).Fates);
        Assert.Equal(1, s.At(At(25.12) + tol).Fates);                // sound 21, then the fate (live 25.12)
        Assert.Equal(2, s.At(At(29.85) + tol).Fates);                // live 29.85
        Assert.InRange(s.DoneAt, At(40.37) - tol, At(40.37) + tol);  // 180 + 300 polls later (live 40.37)
        Assert.True(s.At(s.DoneAt).Done);
        Assert.Equal([23, 20, 21, 21], s.SoundStarts().Select(x => x.Sound));
    }

    [OriginalDataFact]
    public void Row_GhostsThenFates()
    {
        var s = Screen();
        var row = s.Row(s.At(s.DoneAt - 0.01));
        Assert.Equal(DefeatPortraitKind.Cleared, row[0].Kind);      // destroyed: the slot cleared
        Assert.Equal((DefeatPortraitKind.Race, (int)Race.Elf), (row[1].Kind, row[1].RaceId));
        var ghosts = s.Row(s.At(s.DoorAt));
        Assert.All(ghosts, p => Assert.Equal(DefeatPortraitKind.Ghost, p.Kind));
    }

    [OriginalDataFact]
    public void Keys_EndLines_SoundsDrainTheRest()
    {
        var s = Screen();
        var full = s.DoneAt;
        var read = Pack.DefeatScreen!.PictureRead;
        s.Key(read + 1);                 // ends line 1
        s.Release(read + 1.01);          // in the ghosts' blits: queued, sound 23 drains it
        Assert.Equal(full - (180 * 0.02187 - 1), s.DoneAt, 6);
        var noSound = Screen(sound: false);
        Assert.Empty(noSound.SoundStarts());
        Assert.True(noSound.DoneAt < full);
    }
}
