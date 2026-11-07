using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s152: Phantasie IV's sound moments (<see cref="P4Sounds"/>, <c>FORMAT.md</c> "Sound effects"): Core notes the moment
/// where P4.X calls its sound function (<c>_sdwlk</c> in <c>_dmove</c>, <c>_sdbeep</c> in <c>_ewait</c>, <c>_sdnoi</c> for
/// SOUND and OPENGATE, <c>_sddam</c> in <c>_ddamage</c>, …) into the party's <see cref="P4SoundLog"/>.
/// </summary>
public class P4SoundMomentTests
{
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);

    private sealed class Fixed(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => Math.Clamp(value, minInclusive, maxExclusive - 1);
    }

    private static P4Party Party()
    {
        var p = new P4Party();
        for (var i = 0; i < 2; i++)
        {
            var m = new P4Member { Name = $"M{i}", Level = 3, HpMax = 20, Hp = 20, Defence = 10 };
            Array.Fill(m.Skills, 50);
            p.Members.Add(m);
        }
        return p;
    }

    /// <summary>A 30 × 30 map open everywhere with the given triggers and code.</summary>
    private static P4Dungeon Dungeon(P4Party party, IReadOnlyList<P4EventTrigger> triggers, byte[] code,
        Action<ushort[]>? edit = null)
    {
        var words = new ushort[31 * 31];
        for (var i = 0; i < words.Length; i++)
            words[i] = (31 << 6) | (7 << 11);
        edit?.Invoke(words);
        var file = new P4DungeonFile(4, "test", words, triggers, code);
        return new P4Dungeon(Manual, T, party, new Fixed(100), _ => file, i => $"item{i}");
    }

    private static readonly P4EventTrigger[] LoadOnly = [new(0x20, 0, 4, 0)];

    [Fact]
    public void Log_KeepsTheSequence_AndTheMomentsSince()
    {
        var log = new P4SoundLog();
        log.Note(P4Sounds.DungeonStep);
        var at = log.Sequence;
        log.Note(P4Sounds.MessageWait);
        log.Note(P4Sounds.EventSound);
        Assert.Equal(3, log.Sequence);
        Assert.Equal([P4Sounds.MessageWait, P4Sounds.EventSound], log.Since(at));
        Assert.Empty(log.Since(log.Sequence));
        for (var i = 0; i < 40; i++)
            log.Note(P4Sounds.DungeonStep);
        Assert.Equal(32, log.Since(0).Count); // only the last 32 are kept
    }

    [OriginalDataFact]
    public void DungeonStep_OneWalkPerSquareEntered_NoneIntoAWall()
    {
        var party = Party();
        var d = Dungeon(party, LoadOnly, [0x22, 0x00]);
        d.Enter(4, 5, 5, 2);
        var at = party.Sounds.Sequence;
        d.Move(8); // NE: E now, N queued for the next pass (two squares, two _sdwlk)
        Assert.Equal([P4Sounds.DungeonStep, P4Sounds.DungeonStep], party.Sounds.Since(at));

        var walled = Party();
        var w = Dungeon(walled, LoadOnly, [0x22, 0x00], words => words[5 * 31 + 6] = (ushort)((31 << 6) | (1 << 11)));
        w.Enter(4, 5, 5, 2);
        at = walled.Sounds.Sequence;
        w.Move(5); // E into the wall west of (6, 5): the square is unchanged, no _sdwlk
        Assert.DoesNotContain(P4Sounds.DungeonStep, walled.Sounds.Since(at));
    }

    [OriginalDataFact]
    public void Events_SoundGateDamageAndWait_InTheirOrder()
    {
        // Square (5, 5): SOUND 7; OPENGATE 9, 9 (not a gate: silent); OPENGATE 3, 3 (a gate: _sdnoi); DAMAGE 3; WAIT; END
        byte[] code = [0x22, 0x00, 0x20, 7, 0x1D, 9, 9, 0x1D, 3, 3, 0x1A, 3, 0x0B, 0x00];
        var party = Party();
        var d = Dungeon(party, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code,
            words => words[3 * 31 + 3] = (ushort)((16 << 6) | (7 << 11)));
        d.Enter(4, 5, 5, 0);
        Assert.Equal([P4Sounds.EventSound, P4Sounds.GateOpens, P4Sounds.MemberDamaged, P4Sounds.MemberDamaged,
            P4Sounds.MessageWait], party.Sounds.Since(0));
        Assert.Equal(P4DungeonPrompt.Wait, d.Prompt);
    }

    [OriginalDataFact]
    public void EventMenuAndTypedPrompts_Beep()
    {
        // Square (5, 5): MCLEAR; MADD 14 (map 4's text o04.14); MENU; END
        var party = Party();
        var d = Dungeon(party, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], [0x22, 0x00, 0x27, 0x28, 14, 0, 0x29, 0x00]);
        d.Enter(4, 5, 5, 0);
        Assert.Equal([P4Sounds.EventMenu], party.Sounds.Since(0));

        var typed = Party();
        Dungeon(typed, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], [0x22, 0x00, 0x0C, 0x00]).Enter(4, 5, 5, 0);
        Assert.Equal([P4Sounds.EventInput], typed.Sounds.Since(0));
    }

    [OriginalDataFact]
    public void Ancestor2BySameOrDifferent_DrawsEachCombiningRace()
    {
        var c = GameTables.Load(GameId.P4).Phantasie4!.Tables.Creation;
        var elf = new P4Ancestor(P4Ids.Elf, P4Ids.Wizard, [10, 16, 14, 10, 12]);
        Assert.Equal(2, P4Creation.SecondBySameOrDifferent(Manual, c, new Fixed(80), elf, same: false).RacesShown);
        Assert.Equal(0, P4Creation.SecondBySameOrDifferent(Manual, c, new Fixed(20), elf, same: false).RacesShown);
    }
}
