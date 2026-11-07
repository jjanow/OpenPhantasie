using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>A place on the inventory screen an item can be picked up from or dropped on (also the mouse hit tags).</summary>
public abstract record InventorySpot;

/// <summary>Member tab <paramref name="Member"/> (index into <see cref="InventoryScreen.Members"/>).</summary>
public sealed record MemberTabSpot(int Member) : InventorySpot;

/// <summary>A paperdoll box: the worn item of equipment slot <paramref name="SlotId"/>.</summary>
public sealed record SlotSpot(int Member, string SlotId) : InventorySpot;

/// <summary>Carried slot <paramref name="Slot"/> (raw index, holes included) of a member; −1 = the list, no row.</summary>
public sealed record CarriedSpot(int Member, int Slot) : InventorySpot;

/// <summary>Stash slot <paramref name="Slot"/> (raw <see cref="LootSack"/> index); −1 = the list, no row.</summary>
public sealed record StashSpot(int Slot) : InventorySpot;

/// <summary>The stash header's filter (cycles the kinds) and sort words.</summary>
public sealed record StashFilterSpot : InventorySpot;

public sealed record StashSortSpot : InventorySpot;

/// <summary>What a drop does (<see cref="InventoryScreen.Plan"/>).</summary>
public enum InventoryMoveKind
{
    /// <summary>Nothing (dropped back where it came from).</summary>
    None,
    Equip,
    EquipFromStash,
    Unequip,
    Transfer,
    /// <summary>Hand to another member and wear it there (dropped on another member's paperdoll box).</summary>
    TransferAndEquip,
    ToStash,
    FromStash,
    /// <summary>Refused before Core: the item is not one the dropped-on box takes.</summary>
    WrongSlot,
}

/// <summary>One planned move: the members are indexes into <see cref="InventoryScreen.Members"/>.</summary>
public sealed record InventoryMove(
    InventoryMoveKind Kind, int From = -1, int CarriedSlot = -1, int To = -1, int StashSlot = -1, string SlotId = "");

/// <summary>
/// The paperdoll + stash screen (09 T2, presentation option <see cref="GameSettings.InventoryScreen"/>): per-member tabs,
/// the member's worn-item boxes (from the game's <see cref="EquipmentLayout"/>), the 9 carried slots and the party stash
/// (the found-item sack). Holds the cursor and the picked-up item; every move goes through <see cref="GameSession"/>'s
/// inventory actions, so legality and "where" (<see cref="GameSession.InventoryAccess"/>) are Core's. No graphics here:
/// <c>UiOverlays.DrawInventoryScreen</c> draws it, the shell feeds keys and mouse spots.
/// </summary>
/// <remarks>
/// Keys: arrows move (Left = the worn + carried column, Right = the stash), Tab / 1–6 pick the member, Enter or Space picks
/// the item up and places it, E wears / takes off, U takes off, S puts in / takes from the stash, G gives (then 1–6),
/// F filters and O sorts the stash (found / name / kind / value), PageUp / PageDown scroll it, Esc drops what is held, then closes.
/// Gamepad (<see cref="HandleButton"/>, user s75): every key has a button — D-pad / left stick = arrows, A = Enter, B = Esc,
/// Y = E, Back = U, right trigger = S, Start = G (then LB / RB pick who, A gives), LB / RB = the previous / next member,
/// left / right stick click = F / O, right stick up / down = PageUp / PageDown; the shell's X (V) closes.
/// Mouse: hover moves the cursor, a click is Enter (pick / place), a drag is pick + drop, a click on a tab with an item
/// held gives it to that member; dragging over a tab shows that member (<see cref="DragOver"/>), so a drop on their boxes
/// wears it there.
/// </remarks>
public sealed class InventoryScreen
{
    /// <summary>Stash rows shown at once (fewer when paperdoll lines move into the right column, <see cref="StashRowsShown"/>).</summary>
    public const int StashRows = 7;

    /// <summary>Stash rows the screen shows now (the draw sets it from the layout; the rest of the sack scrolls).</summary>
    public int StashRowsShown { get; set; } = StashRows;

    /// <summary>The stash list's scroll state: its bar on the screen, and <see cref="StashScroll"/>.</summary>
    public ScrollBox StashBox { get; } = new();

    /// <summary>The item detail box's scroll state (s99: a long description scrolls rather than being cut).</summary>
    public ScrollBox DetailBox { get; } = new();

    /// <summary>
    /// The worn boxes' scroll state, in box lines (s102): when long worn names leave no room for the carried rows, the
    /// worn column scrolls instead of cutting them.
    /// </summary>
    public ScrollBox WornBox { get; } = new();

    private GameSession? _session;
    private readonly Dictionary<int, string> _names = new();
    private readonly Dictionary<int, int> _values = new();

    public bool IsOpen => _session is not null;

    /// <summary>The member whose paperdoll is shown (index into <see cref="Members"/>).</summary>
    public int Member { get; private set; }

    /// <summary>True when the cursor is in the stash column (else the worn boxes + carried list).</summary>
    public bool InStash { get; private set; }

    /// <summary>Left column row: 0..slots−1 the worn boxes, then the 9 carried slots.</summary>
    public int Row { get; private set; }

    /// <summary>Row in <see cref="StashView"/>.</summary>
    public int StashRow { get; private set; }

    /// <summary>First <see cref="StashView"/> row shown.</summary>
    public int StashScroll
    {
        get => StashBox.Offset;
        private set
        {
            StashBox.Set(StashView().Count, StashRowsShown);
            StashBox.ScrollTo(value);
        }
    }

    /// <summary>0 = all, 1..n = the layout's slots in order, n+1 = everything else.</summary>
    public int Filter { get; private set; }

    public StashSort Sort { get; private set; }

    /// <summary>The picked-up item's place, or null.</summary>
    public InventorySpot? Held { get; private set; }

    /// <summary>G was pressed: the next 1–6 (or a tab) is who receives <see cref="Held"/>.</summary>
    public bool Giving { get; private set; }

    /// <summary>The stash order O cycles: as found, by name, by kind (slot, best first), by value (Armory price, highest first).</summary>
    public enum StashSort { Found, Name, Kind, Value }

    /// <summary>Open on <paramref name="member"/>; with no adventurers in the party the screen stays closed.</summary>
    public void Open(GameSession session, GameDataRepository data, int member = 0)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(data);
        if (!session.Party.Adventurers.Any())
        {
            Close();
            return;
        }
        _session = session;
        _names.Clear();
        _values.Clear();
        foreach (var item in data.Items)
        {
            _names.TryAdd(item.Id, item.Name);
            _values.TryAdd(item.Id, item.ValueFactor);
        }
        Member = Math.Clamp(member, 0, Math.Max(0, Members.Count - 1));
        InStash = false;
        Row = 0;
        StashRow = StashScroll = 0;
        Held = null;
        Giving = false;
    }

    public void Close()
    {
        _session = null;
        Held = null;
        Giving = false;
    }

    private GameSession S => _session ?? throw new InvalidOperationException("The inventory screen is not open.");

    /// <summary>The party's adventurers (a summoned elemental has no inventory).</summary>
    public IReadOnlyList<Character> Members => S.Party.Adventurers.ToList();

    public Character Current => Members[Member];

    public EquipmentLayout Layout => S.Equipment;

    public int LeftRows => Layout.Slots.Count + Character.InventorySlots;

    /// <summary>Null when the moves can be used here; else the reason line (shown read-only).</summary>
    public string? ReadOnlyReason => S.InventoryAccess is var r and not InventoryRefusal.None ? Reason(r, Current) : null;

    /// <summary>The last input was the gamepad: <see cref="Help"/> shows the button hints.</summary>
    public bool UsingPad { get; set; }

    public string Help => UsingPad
        ? Giving ? UiText.S("inventory.help_give_pad")
        : Held is not null ? UiText.S("inventory.help_held_pad")
        : UiText.S("inventory.help_pad")
        : Giving ? UiText.S("inventory.help_give")
        : Held is not null ? UiText.S("inventory.help_held")
        : UiText.S("inventory.help");

    /// <summary>Items in the stash (holes not counted).</summary>
    public int StashCount => S.Sack.ItemCount;

    /// <summary>Most items the stash holds under this save's rules (D-018: 90, or DOS's 60; III D3-020: 90, DOS 60, or null = no limit).</summary>
    public int? StashCapacity => S.Sack.Capacity == int.MaxValue ? null : S.Sack.Capacity; // III p3.found-items unlimited: none

    /// <summary>What wearing <paramref name="itemId"/> would change for <paramref name="member"/> (read-only).</summary>
    public GearComparison? Compare(Character member, int itemId) => S.CompareGear(member, itemId);

    /// <summary>The item's description lines (<see cref="ItemRules.Describe"/>; the first is its name).</summary>
    public IReadOnlyList<string> Describe(int itemId) => S.DescribeItem(itemId);

    public string ItemName(int id) => _names.TryGetValue(id, out var n) ? n : $"#{id}";

    /// <summary>What the Armory pays for <paramref name="id"/> (<see cref="ItemRules.SellPrice"/>; 0 for an unknown item).</summary>
    public int ItemValue(int id) => ItemRules.SellPrice(_values.TryGetValue(id, out var v) ? v : 0);

    // --- the stash list ---------------------------------------------------------------------------------------------

    /// <summary>Filter names: ALL, each layout slot, OTHER (string ids <c>inventory.filter.*</c> / <c>inventory.slot.*</c>).</summary>
    public int FilterCount => Layout.Slots.Count + 2;

    public string FilterLabel => Filter == 0 ? UiText.S("inventory.filter.all")
        : Filter <= Layout.Slots.Count ? SlotLabel(Layout.Slots[Filter - 1].Id)
        : UiText.S("inventory.filter.other");

    public string SortLabel => Sort switch
    {
        StashSort.Name => UiText.S("inventory.sort.name"),
        StashSort.Kind => UiText.S("inventory.sort.kind"),
        StashSort.Value => UiText.S("inventory.sort.value"),
        _ => UiText.S("inventory.sort.found"),
    };

    /// <summary>The stash rows as shown: filtered and sorted; each keeps its raw slot (what the moves take).</summary>
    public IReadOnlyList<(int Slot, int ItemId)> StashView()
    {
        IEnumerable<(int Slot, int ItemId)> items = InventoryActions.StashItems(S.Sack);
        var layout = Layout;
        if (Filter > 0 && Filter <= layout.Slots.Count)
        {
            var slot = layout.Slots[Filter - 1];
            items = items.Where(x => slot.Takes(x.ItemId));
        }
        else if (Filter > layout.Slots.Count)
            items = items.Where(x => layout.SlotFor(x.ItemId) is null);
        return Sort switch
        {
            StashSort.Name => items.OrderBy(x => ItemName(x.ItemId), StringComparer.Ordinal).ThenBy(x => x.Slot).ToList(),
            StashSort.Kind => items.OrderBy(x => KindOrder(x.ItemId))
                .ThenByDescending(x => ItemRules.CombatGate(x.ItemId).Points).ThenBy(x => x.Slot).ToList(),
            StashSort.Value => items.OrderByDescending(x => ItemValue(x.ItemId)).ThenBy(x => x.Slot).ToList(),
            _ => items.ToList(),
        };
    }

    private int KindOrder(int itemId) =>
        Layout.SlotFor(itemId) is { } s ? Layout.Slots.ToList().IndexOf(s) : Layout.Slots.Count;

    /// <summary>The slot label (<c>inventory.slot.&lt;id&gt;</c>), or the id in capitals for a slot with no string yet.</summary>
    public static string SlotLabel(string slotId) =>
        UiText.Table.Contains("inventory.slot." + slotId) ? UiText.Table.Get("inventory.slot." + slotId) : slotId.ToUpperInvariant();

    // --- what is where ----------------------------------------------------------------------------------------------

    /// <summary>The spot under the keyboard cursor.</summary>
    public InventorySpot CursorSpot
    {
        get
        {
            if (InStash)
            {
                var view = StashView();
                return new StashSpot(StashRow < view.Count ? view[StashRow].Slot : -1);
            }
            var slots = Layout.Slots.Count;
            return Row < slots ? new SlotSpot(Member, Layout.Slots[Row].Id) : new CarriedSpot(Member, Row - slots);
        }
    }

    /// <summary>The item at <paramref name="spot"/> (0 = none).</summary>
    public int ItemAt(InventorySpot spot) => spot switch
    {
        SlotSpot s when Valid(s.Member) => WornItems.Get(Members[s.Member], s.SlotId),
        CarriedSpot c when Valid(c.Member) && (uint)c.Slot < (uint)Members[c.Member].InventoryItemIds.Count
            => Members[c.Member].InventoryItemIds[c.Slot],
        StashSpot k when (uint)k.Slot < (uint)S.Sack.ItemIds.Count => S.Sack.ItemIds[k.Slot],
        _ => 0,
    };

    /// <summary>The member an item at <paramref name="spot"/> belongs to (the shown one for the stash).</summary>
    public int OwnerOf(InventorySpot spot) => spot switch
    {
        SlotSpot s => s.Member,
        CarriedSpot c => c.Member,
        MemberTabSpot t => t.Member,
        _ => Member,
    };

    /// <summary>
    /// Why <paramref name="itemId"/> can't be worn by <paramref name="member"/> when it is gear they fail the gate for
    /// (the greyed rows); null for wearable gear and for items no slot takes (potions are not "unusable").
    /// </summary>
    public string? UnusableReason(Character member, int itemId) =>
        InventoryActions.CanWear(member, itemId, Layout) == InventoryRefusal.Unusable
            ? Reason(InventoryRefusal.Unusable, member)
            : null;

    private bool Valid(int member) => (uint)member < (uint)Members.Count;

    // --- keys -------------------------------------------------------------------------------------------------------

    /// <summary>One key; returns a line for the message strip (null = nothing to say). Esc with nothing held closes.</summary>
    public string? HandleKey(Keys key)
    {
        if (!IsOpen)
            return null;
        UsingPad = false;
        return Key(key);
    }

    /// <summary>
    /// One newly pressed gamepad button: the keyboard's twin (see the class remarks). In the G "to whom?" step A gives to the
    /// shown member (LB / RB pick them), as 1–6 does. Unmapped buttons do nothing.
    /// </summary>
    public string? HandleButton(Buttons button)
    {
        if (!IsOpen)
            return null;
        UsingPad = true;
        if (button == Buttons.LeftShoulder)
        {
            SelectMember((Member + Members.Count - 1) % Members.Count);
            return null;
        }
        if (button == Buttons.A && Giving)
            return GiveTo(Member);
        return PadKey(button) is { } key ? Key(key) : null;
    }

    /// <summary>The key a pad button stands for (LB, the previous member, has no single key and is handled apart).</summary>
    public static Keys? PadKey(Buttons button) => button switch
    {
        Buttons.DPadUp or Buttons.LeftThumbstickUp => Keys.Up,
        Buttons.DPadDown or Buttons.LeftThumbstickDown => Keys.Down,
        Buttons.DPadLeft or Buttons.LeftThumbstickLeft => Keys.Left,
        Buttons.DPadRight or Buttons.LeftThumbstickRight => Keys.Right,
        Buttons.A => Keys.Enter,
        Buttons.B => Keys.Escape,
        Buttons.Y => Keys.E,
        Buttons.Back => Keys.U,
        Buttons.RightTrigger => Keys.S,
        Buttons.Start => Keys.G,
        Buttons.RightShoulder => Keys.Tab,
        Buttons.LeftStick => Keys.F,
        Buttons.RightStick => Keys.O,
        Buttons.RightThumbstickUp => Keys.PageUp,
        Buttons.RightThumbstickDown => Keys.PageDown,
        _ => null,
    };

    private string? Key(Keys key)
    {
        if (Giving && Digit(key) is int g)
            return GiveTo(g - 1);
        switch (key)
        {
            case Keys.Escape or Keys.Back:
                if (Held is not null || Giving)
                    return PutBack();
                Close();
                return "";
            case Keys.Up: MoveRow(-1); return null;
            case Keys.Down: MoveRow(1); return null;
            case Keys.Left or Keys.Right: // the worn + carried column | the stash
                InStash = key == Keys.Right;
                ClampCursor();
                return null;
            case Keys.PageUp: ScrollStash(-1); return null;
            case Keys.PageDown: ScrollStash(1); return null;
            case Keys.OemOpenBrackets: DetailBox.ScrollBy(-1); return null; // [ and ]: the item detail box
            case Keys.OemCloseBrackets: DetailBox.ScrollBy(1); return null;
            case Keys.Tab:
                SelectMember((Member + 1) % Members.Count);
                return null;
            case Keys.Enter or Keys.Space:
                return Click(CursorSpot);
            case Keys.E: return Wear(CursorSpot);
            case Keys.U: return TakeOff(CursorSpot);
            case Keys.S: return Stash(CursorSpot);
            case Keys.G: return StartGive(CursorSpot);
            case Keys.F:
                Filter = (Filter + 1) % FilterCount;
                StashRow = StashScroll = 0;
                return null;
            case Keys.O:
                Sort = (StashSort)(((int)Sort + 1) % Enum.GetValues<StashSort>().Length);
                return null;
        }
        if (Digit(key) is int d)
            return Click(new MemberTabSpot(d - 1));
        return null;
    }

    private void MoveRow(int delta)
    {
        if (InStash)
        {
            var count = StashView().Count;
            StashRow = count == 0 ? 0 : Math.Clamp(StashRow + delta, 0, count - 1);
            ClampCursor();
        }
        else
            Row = Math.Clamp(Row + delta, 0, LeftRows - 1);
    }

    /// <summary>The wheel: the stash list one row (the cursor stays on screen).</summary>
    public void ScrollStash(int delta)
    {
        var count = StashView().Count;
        StashScroll = Math.Clamp(StashScroll + delta, 0, Math.Max(0, count - StashRowsShown));
        StashRow = Math.Clamp(StashRow, StashScroll, Math.Max(StashScroll, Math.Min(count - 1, StashScroll + StashRowsShown - 1)));
    }

    private void SelectMember(int member)
    {
        if (Valid(member))
            Member = member;
    }

    private void ClampCursor()
    {
        var count = StashView().Count;
        StashRow = Math.Clamp(StashRow, 0, Math.Max(0, count - 1));
        if (StashRow < StashScroll) StashScroll = StashRow;
        if (StashRow >= StashScroll + StashRowsShown) StashScroll = StashRow - StashRowsShown + 1;
        StashScroll = Math.Clamp(StashScroll, 0, Math.Max(0, count - StashRowsShown));
        Row = Math.Clamp(Row, 0, LeftRows - 1);
    }

    // --- pointing (mouse hover) -------------------------------------------------------------------------------------

    /// <summary>Put the cursor on <paramref name="spot"/> (mouse hover). Tabs and list backgrounds don't move it.</summary>
    public void Point(InventorySpot spot)
    {
        if (!IsOpen)
            return;
        switch (spot)
        {
            case SlotSpot s when s.Member == Member && Layout.Slots.ToList().FindIndex(x => x.Id == s.SlotId) is var i and >= 0:
                InStash = false;
                Row = i;
                break;
            case CarriedSpot { Slot: >= 0 } c when c.Member == Member:
                InStash = false;
                Row = Layout.Slots.Count + c.Slot;
                break;
            case StashSpot { Slot: >= 0 } k:
                var row = StashView().ToList().FindIndex(x => x.Slot == k.Slot);
                if (row >= 0)
                {
                    InStash = true;
                    StashRow = row;
                    ClampCursor();
                }
                break;
        }
    }

    /// <summary>
    /// The pointer is over <paramref name="spot"/> during a mouse drag: over a member tab with an item held, that member's
    /// paperdoll and carried list are shown, so the item can be dropped on their boxes (stash or another member → wear it
    /// there). Elsewhere it moves the cursor as hover does. Letting go on the tab still gives the item to that member.
    /// </summary>
    public void DragOver(InventorySpot spot)
    {
        if (!IsOpen)
            return;
        if (spot is MemberTabSpot tab && Held is not null)
            SelectMember(tab.Member);
        else
            Point(spot);
    }

    // --- pick up / place --------------------------------------------------------------------------------------------

    /// <summary>
    /// Enter / a click on <paramref name="spot"/>: with nothing held, picks up the item there (a tab selects that member,
    /// the filter / sort words cycle); with an item held, drops it there.
    /// </summary>
    public string? Click(InventorySpot spot)
    {
        if (!IsOpen)
            return null;
        switch (spot)
        {
            case StashFilterSpot:
                return Key(Keys.F);
            case StashSortSpot:
                return Key(Keys.O);
        }
        if (Held is { } held)
        {
            if (Giving && spot is not MemberTabSpot)
                return null; // G waits for a member
            if (spot is MemberTabSpot { Member: var m } && !Valid(m))
                return null;
            return Drop(held, spot);
        }
        if (spot is MemberTabSpot tab)
        {
            SelectMember(tab.Member);
            return null;
        }
        return PickUp(spot);
    }

    /// <summary>Pick up the item at <paramref name="spot"/> (keyboard Enter, a click, the start of a drag).</summary>
    public string? PickUp(InventorySpot spot)
    {
        if (!IsOpen || spot is MemberTabSpot or StashFilterSpot or StashSortSpot)
            return null;
        Point(spot);
        if (ItemAt(spot) is not (var id and > 0))
            return Reason(InventoryRefusal.NoItem, Current);
        if (S.InventoryAccess is var access and not InventoryRefusal.None)
            return Reason(access, Current);
        Held = spot;
        Giving = false;
        return UiText.F("inventory.holding_x", ItemName(id));
    }

    /// <summary>Drop <paramref name="source"/>'s item on <paramref name="target"/>: plan it, do it, and say what happened.</summary>
    public string Drop(InventorySpot source, InventorySpot target)
    {
        Held = null;
        Giving = false;
        var line = Do(Plan(source, target));
        ClampCursor();
        if (target is MemberTabSpot { Member: var m } && Valid(m))
            SelectMember(m);
        return line;
    }

    /// <summary>Drop nothing: what is held goes back where it was (Esc, a drop outside the screen's places).</summary>
    public string PutBack()
    {
        var had = Held is not null || Giving;
        Held = null;
        Giving = false;
        return had ? UiText.S("inventory.put_back") : "";
    }

    private string? StartGive(InventorySpot spot)
    {
        var line = PickUp(spot);
        if (Held is null)
            return line;
        Giving = true;
        return UiText.F("inventory.give_x_to_whom", ItemName(ItemAt(spot)));
    }

    private string GiveTo(int member)
    {
        if (Held is not { } held || !Valid(member))
            return UiText.F("inventory.give_x_to_whom", Held is { } h ? ItemName(ItemAt(h)) : "");
        return Drop(held, new MemberTabSpot(member));
    }

    /// <summary>E: wear the item at the cursor (a worn one is taken off).</summary>
    private string? Wear(InventorySpot spot)
    {
        if (ItemAt(spot) == 0)
            return Reason(InventoryRefusal.NoItem, Current);
        return spot switch
        {
            SlotSpot s => Do(new InventoryMove(InventoryMoveKind.Unequip, From: s.Member, SlotId: s.SlotId)),
            CarriedSpot c when InventoryActions.IsWorn(Members[c.Member], c.Slot, Layout)
                => Do(new InventoryMove(InventoryMoveKind.Unequip, From: c.Member,
                    SlotId: Layout.SlotFor(ItemAt(c))!.Id)),
            CarriedSpot c => Do(new InventoryMove(InventoryMoveKind.Equip, From: c.Member, CarriedSlot: c.Slot)),
            StashSpot k => After(Do(new InventoryMove(InventoryMoveKind.EquipFromStash, To: Member, StashSlot: k.Slot))),
            _ => null,
        };
    }

    /// <summary>U: take off the worn item at the cursor.</summary>
    private string? TakeOff(InventorySpot spot) => spot switch
    {
        SlotSpot s => Do(new InventoryMove(InventoryMoveKind.Unequip, From: s.Member, SlotId: s.SlotId)),
        CarriedSpot c when InventoryActions.IsWorn(Members[c.Member], c.Slot, Layout)
            => Do(new InventoryMove(InventoryMoveKind.Unequip, From: c.Member, SlotId: Layout.SlotFor(ItemAt(c))!.Id)),
        CarriedSpot c when ItemAt(c) > 0 => Reason(InventoryRefusal.NothingWorn, Members[c.Member]),
        _ => Reason(InventoryRefusal.NoItem, Current),
    };

    /// <summary>S: carried / worn → the stash; a stash item → the shown member.</summary>
    private string? Stash(InventorySpot spot) => spot switch
    {
        StashSpot k => After(Do(new InventoryMove(InventoryMoveKind.FromStash, To: Member, StashSlot: k.Slot))),
        _ when ItemAt(spot) == 0 => Reason(InventoryRefusal.NoItem, Current),
        _ => After(Do(Plan(spot, new StashSpot(-1)))),
    };

    private string After(string line)
    {
        ClampCursor();
        return line;
    }

    // --- the drop table ---------------------------------------------------------------------------------------------

    /// <summary>
    /// What dropping the item at <paramref name="source"/> on <paramref name="target"/> does (pure; legality is Core's):
    /// <list type="bullet">
    /// <item>carried / worn → own box: wear it (a box of another slot: <see cref="InventoryMoveKind.WrongSlot"/>);
    /// → another member's box: give it and wear it there</item>
    /// <item>carried / worn → another member's tab or list: give it; worn → own list: take it off</item>
    /// <item>carried / worn → stash: put it in; stash → a box: wear it from the stash; stash → a tab or list: take it</item>
    /// <item>anything back where it came from: nothing</item>
    /// </list>
    /// </summary>
    public InventoryMove Plan(InventorySpot source, InventorySpot target)
    {
        var layout = Layout;
        var id = ItemAt(source);
        if (source is StashSpot stash)
        {
            return target switch
            {
                SlotSpot s when layout.SlotFor(id)?.Id is { } own && own != s.SlotId
                    => new InventoryMove(InventoryMoveKind.WrongSlot, To: s.Member, StashSlot: stash.Slot, SlotId: s.SlotId),
                SlotSpot s => new InventoryMove(InventoryMoveKind.EquipFromStash, To: s.Member, StashSlot: stash.Slot, SlotId: s.SlotId),
                CarriedSpot c => new InventoryMove(InventoryMoveKind.FromStash, To: c.Member, StashSlot: stash.Slot),
                MemberTabSpot t => new InventoryMove(InventoryMoveKind.FromStash, To: t.Member, StashSlot: stash.Slot),
                _ => new InventoryMove(InventoryMoveKind.None),
            };
        }

        // A worn box stands for the carried slot the worn pointer names (the first copy, as InventoryActions.IsWorn).
        int from, slot;
        string? wornSlot = null;
        switch (source)
        {
            case SlotSpot s:
                from = s.Member;
                wornSlot = s.SlotId;
                slot = id == 0 ? -1 : Members[from].InventoryItemIds.IndexOf(id);
                break;
            case CarriedSpot c:
                from = c.Member;
                slot = c.Slot;
                if (InventoryActions.IsWorn(Members[from], slot, layout))
                    wornSlot = layout.SlotFor(id)!.Id;
                break;
            default:
                return new InventoryMove(InventoryMoveKind.None);
        }

        switch (target)
        {
            case SlotSpot s:
                if (layout.SlotFor(id)?.Id is { } own && own != s.SlotId)
                    return new InventoryMove(InventoryMoveKind.WrongSlot, from, slot, s.Member, SlotId: s.SlotId);
                if (s.Member != from)
                    return new InventoryMove(InventoryMoveKind.TransferAndEquip, from, slot, s.Member, SlotId: s.SlotId);
                return wornSlot == s.SlotId
                    ? new InventoryMove(InventoryMoveKind.None)
                    : new InventoryMove(InventoryMoveKind.Equip, from, slot, from, SlotId: s.SlotId);
            case CarriedSpot c when c.Member == from:
            case MemberTabSpot t when t.Member == from:
                // Back on the own list: a worn item dragged off its box is taken off; a carried one stays.
                return source is SlotSpot && wornSlot is not null
                    ? new InventoryMove(InventoryMoveKind.Unequip, from, slot, from, SlotId: wornSlot)
                    : new InventoryMove(InventoryMoveKind.None);
            case CarriedSpot c:
                return new InventoryMove(InventoryMoveKind.Transfer, from, slot, c.Member);
            case MemberTabSpot t:
                return new InventoryMove(InventoryMoveKind.Transfer, from, slot, t.Member);
            case StashSpot:
                return new InventoryMove(InventoryMoveKind.ToStash, from, slot);
            default:
                return new InventoryMove(InventoryMoveKind.None);
        }
    }

    /// <summary>Run <paramref name="move"/> through the session; the line says what happened or why not.</summary>
    public string Do(InventoryMove move)
    {
        var s = S;
        var members = Members;
        Character? M(int i) => (uint)i < (uint)members.Count ? members[i] : null;
        var from = M(move.From);
        var to = M(move.To);
        var item = move.Kind switch
        {
            InventoryMoveKind.EquipFromStash or InventoryMoveKind.FromStash or InventoryMoveKind.WrongSlot when move.StashSlot >= 0
                => ItemAt(new StashSpot(move.StashSlot)),
            InventoryMoveKind.Unequip when from is not null => WornItems.Get(from, move.SlotId),
            _ when from is not null => ItemAt(new CarriedSpot(move.From, move.CarriedSlot)),
            _ => 0,
        };
        var name = ItemName(item);
        InventoryRefusal r;
        switch (move.Kind)
        {
            case InventoryMoveKind.None:
                return "";
            case InventoryMoveKind.WrongSlot:
                return s.InventoryAccess is var a and not InventoryRefusal.None
                    ? Reason(a, to ?? Current)
                    : UiText.F("inventory.goes_in_x", Layout.SlotFor(item) is { } own ? SlotLabel(own.Id) : "");
            case InventoryMoveKind.Equip:
                r = s.TryEquip(from!, move.CarriedSlot);
                return r == InventoryRefusal.None ? UiText.F("inventory.done.wear", from!.Name, name) : Reason(r, from!);
            case InventoryMoveKind.EquipFromStash:
                r = s.TryEquipFromStash(move.StashSlot, to!);
                return r == InventoryRefusal.None ? UiText.F("inventory.done.wear", to!.Name, name) : Reason(r, to!);
            case InventoryMoveKind.Unequip:
                r = s.TryUnequip(from!, move.SlotId);
                return r == InventoryRefusal.None ? UiText.F("inventory.done.take_off", from!.Name, name) : Reason(r, from!);
            case InventoryMoveKind.Transfer:
                r = s.TryTransferItem(from!, move.CarriedSlot, to!);
                return r == InventoryRefusal.None ? UiText.F("inventory.done.give", name, to!.Name) : Reason(r, r == InventoryRefusal.SameMember ? from! : to!);
            case InventoryMoveKind.TransferAndEquip:
                // Both halves are checked first, so a refusal never leaves the item half moved.
                r = s.CanTransferItem(from!, move.CarriedSlot, to!);
                if (r == InventoryRefusal.None)
                    r = s.InventoryAccess is var access and not InventoryRefusal.None ? access
                        : InventoryActions.CanWear(to!, item, Layout);
                if (r != InventoryRefusal.None)
                    return Reason(r, r is InventoryRefusal.SameMember ? from! : to!);
                s.TryTransferItem(from!, move.CarriedSlot, to!);
                s.TryEquip(to!, to!.InventoryItemIds.IndexOf(item));
                return UiText.F("inventory.done.give_wear", name, to.Name);
            case InventoryMoveKind.ToStash:
                r = s.TryPutInStash(from!, move.CarriedSlot);
                return r == InventoryRefusal.None ? UiText.F("inventory.done.to_stash", name) : Reason(r, from!);
            case InventoryMoveKind.FromStash:
                r = s.TryTakeFromStash(move.StashSlot, to!);
                return r == InventoryRefusal.None ? UiText.F("inventory.done.from_stash", to!.Name, name) : Reason(r, to!);
            default:
                return "";
        }
    }

    /// <summary>
    /// The refusal's line; <c>carry_full</c> and <c>same_member</c> name <paramref name="who"/>. Under the "anywhere"
    /// stash rule, or in a place the rule allows, <c>not_here</c> (a fight, an open D)ISTRIBUTE, a waiting treasure) says
    /// "not now" instead of "in town".
    /// </summary>
    public string Reason(InventoryRefusal refusal, Character who) => refusal switch
    {
        InventoryRefusal.None => "",
        InventoryRefusal.NotHere when InventoryActions.StashAccessible(S.Mode, S.Options.StashAccess)
            || S.Options.StashAccess == StashAccessRule.Anywhere => UiText.S("inventory.not_now"),
        _ => UiText.F(InventoryActions.ReasonKey(refusal), who.Name),
    };

    private static int? Digit(Keys key) =>
        key is >= Keys.D1 and <= Keys.D9 ? key - Keys.D0
        : key is >= Keys.NumPad1 and <= Keys.NumPad9 ? key - Keys.NumPad0
        : null;
}
