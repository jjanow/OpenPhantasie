using System.Text;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// <c>TWNS.DAT</c> (3492 B) = 12 towns × 291-byte records [C] (save-file-format.md §Town state): stock u8 × 181 at
/// <c>0..180</c> (by item id), price index u16 at <c>0x10f</c>, exit region/x/y u16 at <c>0x111</c>/<c>0x113</c>/<c>0x115</c>
/// (<see cref="TownState.ExitPosition"/>, read-only), name at <c>279</c> (10 chars, space/NUL padded).
/// Bytes 181..270 are the restock list, u16[45] at <c>+0xb5</c> [C] (read at OV1 <c>0x95c</c>; <see cref="TownState.RestockItems"/>).
/// Bytes 289..290 have no reader or writer — probably the name's NUL terminator plus a pad (the name is printed
/// with <c>%s</c> from <c>+0x117</c>) [~]. Everything outside stock/price/name is carried unchanged (<see cref="TownState.DosRecord"/>).
/// Town id = record index + 1 (Pelnor = 1, as in <c>towns.json</c>).
/// </summary>
public static class TownsCodec
{
    public const int RecordSize = 291;
    public const int TownCount = 12;
    public const int FileSize = RecordSize * TownCount;
    public const int PriceIndexOffset = 0x10f;
    public const int NameOffset = 279;
    public const int NameLength = 10;

    public static IReadOnlyList<TownState> Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != FileSize)
            throw new InvalidDataException($"TWNS.DAT expected {FileSize} bytes, got {data.Length}.");

        var towns = new TownState[TownCount];
        for (var i = 0; i < TownCount; i++)
        {
            var r = data.AsSpan(i * RecordSize, RecordSize);
            var stock = new int[TownState.StockSlotCount];
            for (var k = 0; k < stock.Length; k++)
                stock[k] = r[k];
            towns[i] = new TownState(i + 1, i, DecodeName(r.Slice(NameOffset, NameLength)),
                DosFieldWriter.ReadU16(r, PriceIndexOffset), stock)
            {
                DosRecord = r.ToArray(),
                RestockItems = ReadRestockItems(r),
            };
        }
        return towns;
    }

    /// <summary>TWNS.DAT <c>+0xb5</c>: 45 u16 restock item ids (OV1 <c>0x8e1</c>, town-menus.md §Armory restock).</summary>
    public const int RestockOffset = 0xb5;
    public const int RestockCount = 45;

    public static int[] ReadRestockItems(ReadOnlySpan<byte> record)
    {
        var list = new int[RestockCount];
        for (var k = 0; k < RestockCount; k++)
            list[k] = DosFieldWriter.ReadU16(record, RestockOffset + 2 * k);
        return list;
    }

    /// <summary>
    /// Write TWNS.DAT. Each town (matched by <see cref="TownState.Index"/>) overlays stock, price index and name onto
    /// its carried record; a town without a state is written as zeros (with a note), as is one without a carry
    /// apart from its known fields. Stock above 255 is clamped and listed (P1.4 decision 7).
    /// </summary>
    public static byte[] Write(IEnumerable<TownState> towns, ICollection<DosSaveWarning>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(towns);
        var data = new byte[FileSize];
        var seen = new bool[TownCount];
        foreach (var town in towns)
        {
            if ((uint)town.Index >= TownCount)
                throw new ArgumentException($"Town index {town.Index} out of range.", nameof(towns));
            seen[town.Index] = true;
            var r = data.AsSpan(town.Index * RecordSize, RecordSize);
            var raw = town.DosRecord is { Length: RecordSize } carried ? carried : null;
            raw?.CopyTo(r);
            for (var k = 0; k < TownState.StockSlotCount; k++)
                DosFieldWriter.U8(r, k, town.Stock[k], "TWNS.DAT", GameText.F("core.dossave.x_stock_of_item_x", town.Name, k), warnings);
            DosFieldWriter.U16(r, PriceIndexOffset, town.PriceIndex, "TWNS.DAT", GameText.F("core.dossave.x_price_index", town.Name), warnings);
            if (raw is null || DecodeName(r.Slice(NameOffset, NameLength)) != town.Name)
                Encoding.ASCII.GetBytes(town.Name.PadRight(NameLength)[..NameLength]).CopyTo(r.Slice(NameOffset, NameLength));
        }

        for (var i = 0; i < TownCount; i++)
        {
            if (!seen[i])
                warnings?.Add(new DosSaveWarning(DosSaveWarningKind.Note, "TWNS.DAT",
                    GameText.F("core.dossave.no_state_for_town_x", i)));
        }
        return data;
    }

    private static string DecodeName(ReadOnlySpan<byte> bytes) =>
        Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
}
