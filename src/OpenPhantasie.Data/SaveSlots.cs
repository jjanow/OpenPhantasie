using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// One save slot of a game (<see cref="SaveSlots"/>): <see cref="Number"/> 0 is I–III's autosave slot (AUTO), 1.. the
/// numbered slots; <see cref="Path"/> its file.
/// </summary>
public sealed record SaveSlot(GameId Game, int Number, string Path)
{
    /// <summary>I–III's autosave slot: leaving town writes it (I / II's Auto save, II's <c>p2.autosave</c>, III's D3-028).</summary>
    public bool IsAuto => Number == SaveSlots.AutoNumber;
}

/// <summary>What a slot holds (<see cref="SaveSlots.Describe"/>): nothing, a readable save, or a file that can't be read.</summary>
/// <param name="Written">The file's last write time (local), or null when the slot is empty.</param>
/// <param name="Preview">The save's party and place, or null when the slot is empty or unreadable.</param>
/// <param name="Old">The file is a save from an older remaster version (refused, not migrated).</param>
public sealed record SaveSlotInfo(SaveSlot Slot, DateTime? Written, SaveSlotPreview? Preview, bool Old = false)
{
    public bool Empty => Written is null;

    /// <summary>A file is there but can't be loaded (unreadable or old).</summary>
    public bool Unreadable => Written is not null && Preview is null;
}

/// <summary>
/// QoL SAVE SLOTS (<c>docs/plan/12-save-slots.md</c>): the save files of each game in a folder. The single save every game
/// had before (<c>savegame-pN.json</c>) stays where it was and is the game's first slot: I–III's autosave slot (AUTO), IV's
/// slot 1 (IV has no autosave; its original's three slots, manual p. 33). The other slots are <c>savegame-pN-K.json</c>:
/// I–III slots 1–9, IV slots 2–3. With the option off I–III use only the first slot, as before; IV keeps its three (its
/// original's Save page runs them, <see cref="Slotted"/>).
/// </summary>
public static class SaveSlots
{
    /// <summary>The number of I–III's autosave slot.</summary>
    public const int AutoNumber = 0;

    /// <summary>The numbered slots: nine in I–III (the keys 1–9), IV's original three (<c>_diskmenu</c>'s <c>*1:Save</c>–<c>*3:Save</c>).</summary>
    public static int NumberedCount(GameId game) => game == GameId.P4 ? 3 : 9;

    /// <summary>
    /// Whether the game uses its slot files: always in IV (its original's three slots; the switch picks the screen, the
    /// original's Save page or the remaster's picker), else when the game's SAVE SLOTS switch is on.
    /// </summary>
    public static bool Slotted(GameId game, bool switchOn) => switchOn || game == GameId.P4;

    /// <summary>I–III have an autosave slot; IV saves only when asked.</summary>
    public static bool HasAuto(GameId game) => game != GameId.P4;

    /// <summary>The game's single save of before (and with the option off): <c>savegame-pN.json</c> in <paramref name="dir"/>.</summary>
    public static string SinglePath(string dir, GameId game) => System.IO.Path.Combine(dir, $"savegame-{game.Prefix()}.json");

    /// <summary>Slot <paramref name="number"/>'s file: the single save for the game's first slot, else <c>savegame-pN-K.json</c>.</summary>
    public static string PathOf(string dir, GameId game, int number)
    {
        if (number < FirstNumber(game) || number > NumberedCount(game))
            throw new ArgumentOutOfRangeException(nameof(number), number, null);
        return number == FirstNumber(game)
            ? SinglePath(dir, game)
            : System.IO.Path.Combine(dir, $"savegame-{game.Prefix()}-{number}.json");
    }

    /// <summary>The game's first slot: AUTO (0) in I–III, slot 1 in IV.</summary>
    public static int FirstNumber(GameId game) => HasAuto(game) ? AutoNumber : 1;

    /// <summary>Every slot Load lists, in order: AUTO (I–III), then 1..<see cref="NumberedCount"/>.</summary>
    public static IReadOnlyList<SaveSlot> All(string dir, GameId game) =>
        Enumerable.Range(FirstNumber(game), NumberedCount(game) - FirstNumber(game) + 1)
            .Select(n => new SaveSlot(game, n, PathOf(dir, game, n))).ToArray();

    /// <summary>The slots Save offers: the numbered ones (AUTO is written only by the autosave).</summary>
    public static IReadOnlyList<SaveSlot> Writable(string dir, GameId game) => All(dir, game).Where(s => !s.IsAuto).ToArray();

    /// <summary>
    /// The save another game reads (transfers, the lineage Hall, IV's Hall): with <paramref name="slotsOn"/> (IV: always),
    /// the newest file among the game's slots, else (or when no slot holds a file) the single save.
    /// </summary>
    public static string ReadPath(string dir, GameId game, bool slotsOn)
    {
        if (!Slotted(game, slotsOn))
            return SinglePath(dir, game);
        string? best = null;
        var bestTime = DateTime.MinValue;
        foreach (var slot in All(dir, game))
        {
            if (!File.Exists(slot.Path))
                continue;
            var t = File.GetLastWriteTimeUtc(slot.Path);
            if (best is null || t > bestTime)
                (best, bestTime) = (slot.Path, t);
        }
        return best ?? SinglePath(dir, game);
    }

    /// <summary>Each of <paramref name="slots"/> with its file's date and preview; a file that can't be read says so.</summary>
    public static IReadOnlyList<SaveSlotInfo> Describe(IEnumerable<SaveSlot> slots) => slots.Select(Describe).ToArray();

    /// <summary><paramref name="slot"/>'s file: empty, its preview, or unreadable. Never throws for a bad file.</summary>
    public static SaveSlotInfo Describe(SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        DateTime written;
        try
        {
            if (!File.Exists(slot.Path))
                return new(slot, null, null);
            written = File.GetLastWriteTime(slot.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(slot, null, null);
        }
        try
        {
            var preview = slot.Game == GameId.P4
                ? SaveSlotPreview.Of(Phantasie4SaveCodec.ReadJsonFile(slot.Path))
                : SaveSlotPreview.Of(SessionCodec.ReadJsonFile(slot.Path));
            return new(slot, written, preview);
        }
        catch (OldSaveFormatException)
        {
            return new(slot, written, null, Old: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or KeyNotFoundException
                                       or InvalidOperationException or UnauthorizedAccessException
                                       or System.Text.Json.JsonException)
        {
            return new(slot, written, null);
        }
    }
}
