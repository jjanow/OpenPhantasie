using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>combat_flash</c> (Phantasie III Amiga; <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c> §2.4): the
/// sparkle <c>0x1b814(x, y, right)</c> as the spell effect of a fight. <c>damage_monster</c> <c>0x1833e</c> calls it on
/// the monster hit when its flash argument is set (the party's spells: <c>0x181b8</c>, <c>0x182c6</c>, <c>0x186f6</c>; a
/// melee hit passes 0), and <c>damage_character</c> <c>0x17e16</c> on the member's portrait when its argument is set (the
/// monsters' specials and their stunning / draining touches; a plain melee hit passes 0 and prints "hits!" with sound 15
/// instead). Each flash draws <see cref="Pictures"/> one over the other, each followed by <c>0x2af6(w)</c> =
/// <see cref="Wait"/> + <see cref="WaitExtraVbl"/> VBLs, with <see cref="Sound"/> started before frame
/// <see cref="SoundFrame"/>; then the rectangle is restored. The speed word <c>0x241e8</c> is 6 in a fight: set at boot
/// (<c>0x7656</c>) and set back after the reward scene (<c>0x1f13a</c>). In the combat palette (heros.cmp's, <c>0x19906</c>).
/// <para>Position (bytes of 8 px, then pixels): x += rnd mod <see cref="XJitter"/> (+ <see cref="RightAdd"/> when
/// <c>right</c>), at most <see cref="XMax"/>; y += rnd mod <see cref="YJitter"/> + <see cref="YAdd"/>. A member: x =
/// max(0, <see cref="PartyXPerSlot"/>·slot + <see cref="PartyXAdd"/>), y = <see cref="PartyY"/>, right; its portrait
/// is at (<see cref="PortraitXPerSlot"/>·slot, <see cref="PortraitTopByRank"/>[record <c>+8</c>]) (<c>0x1b42e</c>). A
/// monster: the group's x of that monster (<c>+60 + 2i</c>), its <c>+70</c> y, right = the giant flag <c>+72</c>: the
/// monster's picture top-left (<c>0x1b150</c>).</para>
/// </summary>
public sealed record CombatFlashLayout(
    IReadOnlyList<string> Pictures, int Wait, int WaitExtraVbl, int VblHz, int UnitX, int XJitter, int RightAdd, int XMax,
    int YJitter, int YAdd, int? Sound, int SoundFrame, int PartyXPerSlot, int PartyXAdd, int PartyY, int PortraitXPerSlot,
    IReadOnlyList<int> PortraitTopByRank)
{
    /// <summary>VBLs one frame of a flash lasts.</summary>
    public int FrameVbl => Wait + WaitExtraVbl;

    /// <summary>VBLs a whole flash lasts.</summary>
    public int FlashVbl => Pictures.Count * FrameVbl;

    /// <summary>
    /// The flash on party slot <paramref name="slot"/> whose combat row word is <paramref name="rank"/> (III: Back 0,
    /// Middle 1, Front 2), as its top-left relative to the member's portrait top-left; jitter from <paramref name="next"/>
    /// (<c>next(n)</c> = 0..n − 1, x first as <c>0x1b814</c>).
    /// </summary>
    public (int X, int Y) PartyOffset(int slot, int rank, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var (x, y) = Place(Math.Max(0, PartyXPerSlot * slot + PartyXAdd), PartyY, right: true, next);
        var top = PortraitTopByRank[Math.Clamp(rank, 0, PortraitTopByRank.Count - 1)];
        return (x - PortraitXPerSlot * slot, y - top);
    }

    /// <summary>
    /// The flash on a monster, relative to its picture's top-left (the monster's own x and y; <paramref name="giant"/> =
    /// <c>right</c>). The cap at <see cref="XMax"/> bytes depends on where the Amiga put the group; the remaster's rank
    /// list has no such place, so it is not applied here `[~]`.
    /// </summary>
    public (int X, int Y) MonsterOffset(bool giant, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var dx = next(XJitter) + (giant ? RightAdd : 0);
        var dy = next(YJitter) + YAdd;
        return (dx * UnitX, dy);
    }

    private (int X, int Y) Place(int xBytes, int y, bool right, Func<int, int> next)
    {
        xBytes += next(XJitter);
        if (right)
            xBytes += RightAdd;
        xBytes = Math.Min(xBytes, XMax);
        y += next(YJitter) + YAdd;
        return (xBytes * UnitX, y);
    }

    internal static CombatFlashLayout FromDto(Dto dto, int? packVblHz, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: combat_flash: {what}.");
        if (dto.Pictures is not { Count: > 0 } pictures || pictures.Any(string.IsNullOrWhiteSpace))
            throw Bad("no pictures");
        var hz = dto.VblHz ?? packVblHz;
        if (dto.Wait < 0 || dto.UnitX <= 0 || dto.XJitter <= 0 || dto.YJitter <= 0 || hz is not > 0
            || dto.PortraitTopByRank is not { Count: > 0 } tops)
            throw Bad("bad numbers");
        if (dto.Sound is <= 0 || dto.SoundFrame < 0 || dto.SoundFrame >= pictures.Count)
            throw Bad("bad sound");
        return new CombatFlashLayout(pictures, dto.Wait, dto.WaitExtraVbl, hz.Value, dto.UnitX, dto.XJitter, dto.RightAdd,
            dto.XMax ?? int.MaxValue, dto.YJitter, dto.YAdd, dto.Sound, dto.SoundFrame, dto.PartyXPerSlot, dto.PartyXAdd,
            dto.PartyY, dto.PortraitXPerSlot, tops);
    }

    internal sealed class Dto
    {
        public List<string>? Pictures { get; set; }
        public int Wait { get; set; }
        [JsonPropertyName("wait_extra_vbl")] public int WaitExtraVbl { get; set; }
        [JsonPropertyName("vbl_hz")] public int? VblHz { get; set; }
        [JsonPropertyName("unit_x")] public int UnitX { get; set; }
        [JsonPropertyName("x_jitter")] public int XJitter { get; set; }
        [JsonPropertyName("right_add")] public int RightAdd { get; set; }
        [JsonPropertyName("x_max")] public int? XMax { get; set; }
        [JsonPropertyName("y_jitter")] public int YJitter { get; set; }
        [JsonPropertyName("y_add")] public int YAdd { get; set; }
        public int? Sound { get; set; }
        [JsonPropertyName("sound_frame")] public int SoundFrame { get; set; }
        [JsonPropertyName("party_x_per_slot")] public int PartyXPerSlot { get; set; }
        [JsonPropertyName("party_x_add")] public int PartyXAdd { get; set; }
        [JsonPropertyName("party_y")] public int PartyY { get; set; }
        [JsonPropertyName("portrait_x_per_slot")] public int PortraitXPerSlot { get; set; }
        [JsonPropertyName("portrait_top_by_rank")] public List<int>? PortraitTopByRank { get; set; }
    }
}
