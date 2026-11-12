using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>What <see cref="Game1"/> does with the slot picker's outcome (<see cref="SaveSlotRouting.Pick"/>).</summary>
public enum SlotPickAction
{
    /// <summary>Esc on Load, Start or Reset, or on Save with no game running: nothing.</summary>
    Nothing,

    /// <summary>Esc on IV's Save: IV's note says nothing was saved.</summary>
    NotSavedP4,

    /// <summary>Esc on I–III's Save: the feedback line says nothing was saved.</summary>
    NotSaved,

    /// <summary>Save: IV's campaign into the slot.</summary>
    WriteP4,

    /// <summary>Save: I–III's session into the slot.</summary>
    Write,

    /// <summary>Load (F9): IV's slot onto the running IV screen.</summary>
    LoadP4,

    /// <summary>Load (F9): I–III's slot (D-069's Guild-number question, or the party list).</summary>
    Load,

    /// <summary>Reset: the reset's Keep / Erase confirmation for the slot.</summary>
    OpenReset,

    /// <summary>Start (main menu), NEW GAME picked: a new game.</summary>
    StartNew,

    /// <summary>Start (main menu), a slot picked in IV: IV starts from that slot.</summary>
    StartP4Slot,

    /// <summary>Start (main menu), a slot picked in I–III: a new game first, then the slot loads over it (as F9 after Play).</summary>
    StartThenLoad,
}

/// <summary>How the RESET GAME row opens (<see cref="SaveSlotRouting.Reset"/>).</summary>
public enum ResetEntryKind
{
    /// <summary>The slot picker in its Reset mode.</summary>
    Picker,

    /// <summary>The notice "no save": no slot holds a file, or (switch off) the one save is missing.</summary>
    NoSave,

    /// <summary>The one save can't be read: refused.</summary>
    Unreadable,

    /// <summary>The one save is in an old format: refused.</summary>
    OldSave,

    /// <summary>The Keep / Erase confirmation for the game's one save (<see cref="ResetEntry.Slot"/>).</summary>
    Confirm,
}

/// <summary>The RESET GAME row's route: the picker, a notice, or the one save's confirmation (<paramref name="Slot"/>).</summary>
public sealed record ResetEntry(ResetEntryKind Kind, SaveSlot? Slot = null);

/// <summary>
/// QoL SAVE SLOTS (<c>docs/plan/12-save-slots.md</c>) and Reset the game (<c>16-reset-game.md</c>): the shell's routing
/// decisions, pure so they can be tested without a running <see cref="Game1"/>. Game1 asks these and then does the I/O.
/// </summary>
public static class SaveSlotRouting
{
    /// <summary>The slots the picker lists: Save the numbered ones (AUTO only by the autosave), the other modes every slot.</summary>
    public static IReadOnlyList<SaveSlot> Offered(string dir, GameId game, SaveSlotMode mode) =>
        mode == SaveSlotMode.Save ? SaveSlots.Writable(dir, game) : SaveSlots.All(dir, game);

    /// <summary>
    /// A game picked on the main menu: true opens the picker on NEW GAME and the saves (SAVE SLOTS on and a file in any
    /// slot); false starts a new game, as before.
    /// </summary>
    public static bool OffersSaves(string dir, GameId game, bool switchOn) =>
        switchOn && SaveSlots.All(dir, game).Any(s => File.Exists(s.Path));

    /// <summary>
    /// I–III's Save / Load (F5 / F9, the game menu, a town's Save / Load): null = the slot picker (SAVE SLOTS on); else the
    /// game's one save. The autosave never asks (it always writes the one save).
    /// </summary>
    public static string? SingleSavePath(string dir, GameId game, bool switchOn) =>
        switchOn ? null : SaveSlots.SinglePath(dir, game);

    /// <summary>IV's save of slot <paramref name="slot"/> (1–3; IV always has its original's three, either way the switch is); else the one save.</summary>
    public static string P4SlotPath(string dir, int slot) =>
        slot >= 1 && slot <= SaveSlots.NumberedCount(GameId.P4)
            ? SaveSlots.PathOf(dir, GameId.P4, slot)
            : SaveSlots.SinglePath(dir, GameId.P4);

    /// <summary>
    /// The picker closed with <paramref name="pick"/> (null: Esc). <paramref name="p4Running"/> / <paramref name="sessionRunning"/>
    /// say which game screen is up (neither: the main menu).
    /// </summary>
    public static SlotPickAction Pick(GameId game, SaveSlotMode mode, SaveSlotPick? pick, bool p4Running, bool sessionRunning)
    {
        if (pick is null)
        {
            if (mode != SaveSlotMode.Save)
                return SlotPickAction.Nothing;
            return p4Running ? SlotPickAction.NotSavedP4 : sessionRunning ? SlotPickAction.NotSaved : SlotPickAction.Nothing;
        }
        var p4 = game == GameId.P4;
        return pick switch
        {
            { Mode: SaveSlotMode.Save, Slot: not null } => p4 ? SlotPickAction.WriteP4 : SlotPickAction.Write,
            { Mode: SaveSlotMode.Load, Slot: not null } => p4 ? SlotPickAction.LoadP4 : SlotPickAction.Load,
            { Mode: SaveSlotMode.Reset, Slot: not null } => SlotPickAction.OpenReset,
            { Mode: SaveSlotMode.Start, Slot: null } => SlotPickAction.StartNew,
            { Mode: SaveSlotMode.Start } => p4 ? SlotPickAction.StartP4Slot : SlotPickAction.StartThenLoad,
            _ => SlotPickAction.Nothing,
        };
    }

    /// <summary>The RESET GAME row for <paramref name="game"/>: reads the data folder, then <see cref="Reset(bool, bool, SaveSlotInfo?)"/>.</summary>
    public static ResetEntry Reset(string dir, GameId game, bool switchOn)
    {
        if (SaveSlots.Slotted(game, switchOn))
            return Reset(true, SaveSlots.All(dir, game).Any(s => File.Exists(s.Path)), null);
        var single = new SaveSlot(game, SaveSlots.FirstNumber(game), SaveSlots.SinglePath(dir, game));
        return Reset(false, false, SaveSlots.Describe(single));
    }

    /// <summary>
    /// The RESET GAME row: slotted (SAVE SLOTS on, and IV always) → the picker in its Reset mode, or "no save" when no slot
    /// holds a file; else the one save (<paramref name="single"/>) → "no save", the old / unreadable refusal, or its
    /// confirmation.
    /// </summary>
    public static ResetEntry Reset(bool slotted, bool anySlotFile, SaveSlotInfo? single)
    {
        if (slotted)
            return new(anySlotFile ? ResetEntryKind.Picker : ResetEntryKind.NoSave);
        ArgumentNullException.ThrowIfNull(single);
        if (single.Empty)
            return new(ResetEntryKind.NoSave);
        if (single.Unreadable)
            return new(single.Old ? ResetEntryKind.OldSave : ResetEntryKind.Unreadable);
        return new(ResetEntryKind.Confirm, single.Slot);
    }
}
