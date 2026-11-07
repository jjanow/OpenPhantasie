namespace OpenPhantasie.Core;

/// <summary>
/// The Phantasie II → I character transfer (Phase 09 T5; spec <c>reverse-engineering/atari-st/phantasie2/logic-specs/
/// transfer.md</c> section 8): the ST's backup utility <c>UTILIT2.PRG</c>, TRANSFER CHARACTERS → FROM PHANTASIE 2 TO PHANTASIE 1
/// (<c>0x0d38</c>), runs the I → II converter <c>0x0edc</c> in mode 1: the same per-byte table
/// (<see cref="TransferIToII.ApplyByteTable"/>), no XP raise, and the 36 II-only bytes dropped. Checked live in Hatari
/// (s76, <c>parity/transfer-live.md</c> run C): byte for byte. The manual says nothing about II → I, so there are no
/// options: the ST's values run (<see cref="TransferIToIIRules.St"/>), whatever the II save's <c>p2.transfer-*</c> say.
/// </summary>
public static class TransferIIToI
{
    /// <summary>II's character slots the ST converts (the same 22-slot loop; II's elementals are not converted).</summary>
    public const int CharacterSlots = TransferIToII.CharacterSlots;

    /// <summary>
    /// I's town after the transfer. The converter writes byte 255 := 1 (live: <c>@254</c> = 1 on every run C record), and
    /// I's <c>@254</c> is 0-based (DOS <c>GuildCodec.TownFromDos</c>; the ST I's record follows the DOS layout <c>[~]</c>), so Core town
    /// 2, PINEVILLE. A new I game starts in Pelnor, so Guild → Add there asks the move question and ages the member
    /// (<see cref="GuildHall.AddNeedsMoveConfirm"/>), as I's own code does for any member of another town.
    /// </summary>
    public const int StartTown = 2;

    /// <summary>One character, II → I.</summary>
    public static Character Convert(Character fromII)
    {
        var c = TransferIToII.ApplyByteTable(fromII, TransferIToIIRules.St, StartTown);
        c.Phantasie2 = null; // bytes 282–317 are not written: I's record is 282 bytes (origin, quest score, minions, runes gone)
        return c; // TOSS (@42 / @62) is copied into I's unused words: kept, as the bytes are
    }

    /// <summary>
    /// II's roster, II → I: the first <see cref="CharacterSlots"/> slots (an empty slot stays empty). With I's
    /// <c>guild-limit</c> = <c>none</c> (<paramref name="guildUnlimited"/>) every slot is converted and the result keeps the
    /// input's length (at least 22), as in <see cref="TransferIToII.ConvertRoster"/>.
    /// </summary>
    public static Character?[] ConvertRoster(IReadOnlyList<Character?> iiSlots, bool guildUnlimited = false)
    {
        ArgumentNullException.ThrowIfNull(iiSlots);
        var count = guildUnlimited ? Math.Max(CharacterSlots, iiSlots.Count) : CharacterSlots;
        var slots = new Character?[count];
        for (var i = 0; i < Math.Min(count, iiSlots.Count); i++)
            slots[i] = iiSlots[i] is { } c ? Convert(c) : null;
        return slots;
    }
}
