namespace OpenPhantasie.Core;

/// <summary>What a <see cref="TreasuryEntry"/> records (<c>docs/plan/11-guild-treasury.md</c> section 3).</summary>
public enum TreasuryEntryKind
{
    /// <summary>Trip gold the party brought into town (purse and found gold).</summary>
    Trip,

    /// <summary>Account gold collected: starting gold, level-up gold, transfers in.</summary>
    Accounts,

    /// <summary>An item sold (Distribute, Trade, IV's arrival hand-out).</summary>
    Sale,

    /// <summary>Gold won in the field and banked at once (the <c>safe</c> value).</summary>
    Loot,

    Armory,
    Training,
    Spells,
    Inn,
    Ship,
    Magic,
    Event,
}

/// <summary>One ledger line: the trip it belongs to, its kind, the amount (+ in, − out) and the balance after it.</summary>
public sealed record TreasuryEntry(int Trip, TreasuryEntryKind Kind, long Amount, long Balance);

/// <summary>
/// The Guild treasury (P3 QoL, <c>docs/plan/11-guild-treasury.md</c>): one gold pool per save when the <c>treasury</c>
/// rules option is on. It holds the balance (no cap), the current trip number and a ledger of the last
/// <see cref="LedgerSize"/> entries. Trip gold stays in each game's own stores (I–III purse + found gold, IV purse) until a
/// sweep moves it here; spending takes trip gold first, then the treasury (<see cref="GoldPurse"/>).
/// </summary>
public sealed class Treasury
{
    public const int LedgerSize = 50;

    private readonly List<TreasuryEntry> _ledger = new();

    public long Balance { get; private set; }

    /// <summary>The current trip: 0 until the party first leaves town, +1 each time it leaves.</summary>
    public int Trip { get; private set; }

    /// <summary>The ledger, oldest first.</summary>
    public IReadOnlyList<TreasuryEntry> Ledger => _ledger;

    /// <summary>Adds <paramref name="amount"/> (≥ 0) and logs it; a zero amount logs nothing.</summary>
    public void Add(long amount, TreasuryEntryKind kind)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount == 0)
            return;
        Balance += amount;
        Log(kind, amount);
    }

    /// <summary>
    /// Takes up to <paramref name="amount"/> (≥ 0) from the balance and logs it; returns what was taken (never more than
    /// the balance).
    /// </summary>
    public long Take(long amount, TreasuryEntryKind kind)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        var taken = Math.Min(amount, Balance);
        if (taken == 0)
            return 0;
        Balance -= taken;
        Log(kind, -taken);
        return taken;
    }

    /// <summary>The party left town: a new trip starts.</summary>
    public void StartTrip() => Trip++;

    /// <summary>Sum of the ledger entries of trip <paramref name="trip"/> still in the ledger.</summary>
    public long TripTotal(int trip) => _ledger.Where(e => e.Trip == trip).Sum(e => e.Amount);

    private void Log(TreasuryEntryKind kind, long amount)
    {
        _ledger.Add(new TreasuryEntry(Trip, kind, amount, Balance));
        if (_ledger.Count > LedgerSize)
            _ledger.RemoveAt(0);
    }

    public TreasurySnapshot ToSnapshot() => new()
    {
        Balance = Balance,
        Trip = Trip,
        Ledger = _ledger.Select(e => new TreasuryEntrySnapshot
        {
            Trip = e.Trip, Kind = e.Kind.ToString(), Amount = e.Amount, Balance = e.Balance,
        }).ToArray(),
    };

    /// <summary>Rebuilds a treasury from a snapshot; null gives an empty one. Unknown entry kinds are dropped.</summary>
    public static Treasury From(TreasurySnapshot? snap)
    {
        var t = new Treasury();
        if (snap is null)
            return t;
        t.Balance = Math.Max(0, snap.Balance);
        t.Trip = Math.Max(0, snap.Trip);
        foreach (var e in snap.Ledger ?? [])
            if (Enum.TryParse<TreasuryEntryKind>(e.Kind, out var k))
                t._ledger.Add(new TreasuryEntry(e.Trip, k, e.Amount, e.Balance));
        if (t._ledger.Count > LedgerSize)
            t._ledger.RemoveRange(0, t._ledger.Count - LedgerSize);
        return t;
    }

    public Treasury Clone() => From(ToSnapshot());
}

/// <summary>Save form of a <see cref="Treasury"/>.</summary>
public sealed class TreasurySnapshot
{
    public long Balance { get; set; }
    public int Trip { get; set; }
    public TreasuryEntrySnapshot[] Ledger { get; set; } = [];
}

public sealed class TreasuryEntrySnapshot
{
    public int Trip { get; set; }
    public string Kind { get; set; } = "";
    public long Amount { get; set; }
    public long Balance { get; set; }
}

/// <summary>
/// The spend helper of the treasury option (<c>11-guild-treasury.md</c> section 4): spendable gold is trip gold plus the
/// treasury; a spend takes trip gold first (in the order the game's own code took it), then the treasury. Without a
/// treasury it is the game's original purse logic: only trip gold counts.
/// </summary>
public static class GoldPurse
{
    /// <summary>
    /// Takes <paramref name="amount"/> from <paramref name="stores"/> in order (each a getter / setter pair over a trip
    /// store), then from <paramref name="treasury"/> (null = off). Returns false and takes nothing when the total is short.
    /// </summary>
    public static bool TrySpend(
        long amount, Treasury? treasury, TreasuryEntryKind kind, params (Func<long> Get, Action<long> Set)[] stores)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        var total = stores.Sum(s => Math.Max(0, s.Get())) + (treasury?.Balance ?? 0);
        if (total < amount)
            return false;
        var left = amount;
        foreach (var (get, set) in stores)
        {
            var take = Math.Min(left, Math.Max(0, get()));
            if (take > 0)
                set(get() - take);
            left -= take;
        }
        if (left > 0)
            treasury!.Take(left, kind);
        return true;
    }
}

/// <summary>
/// The Treasury window's text, shared by every game's look (<c>11-guild-treasury.md</c> section 5): the balance, then
/// the ledger newest first, grouped by trip with each trip's total. Strings <c>core.treasury.*</c> (shared table).
/// </summary>
public static class TreasuryReport
{
    /// <summary>The gold line: spendable gold, plus the trip gold when there is any (away from town).</summary>
    public static string GoldLine(long spendable, long tripGold) =>
        tripGold > 0
            ? GameText.F("core.treasury.gold_x_trip_x", spendable, tripGold)
            : GameText.F("core.treasury.gold_x", spendable);

    /// <summary>The window's lines: title, balance, then each trip (newest first) with its entries.</summary>
    public static IReadOnlyList<string> Lines(Treasury treasury, long tripGold = 0)
    {
        ArgumentNullException.ThrowIfNull(treasury);
        var lines = new List<string>
        {
            GameText.S("core.treasury.title"),
            GameText.F("core.treasury.balance_x", treasury.Balance),
        };
        if (tripGold > 0)
            lines.Add(GameText.F("core.treasury.carried_x", tripGold));
        if (treasury.Ledger.Count == 0)
        {
            lines.Add(GameText.S("core.treasury.no_entries"));
            return lines;
        }
        foreach (var trip in treasury.Ledger.Reverse().GroupBy(e => e.Trip))
        {
            lines.Add("");
            lines.Add(GameText.F("core.treasury.trip_x_total_x", trip.Key, Signed(trip.Sum(e => e.Amount))));
            foreach (var e in trip)
                lines.Add(GameText.F("core.treasury.entry_x_x_x",
                    GameText.S(KindId(e.Kind)), Signed(e.Amount), e.Balance));
        }
        return lines;
    }

    /// <summary>The string id of a ledger kind's label.</summary>
    public static string KindId(TreasuryEntryKind kind) => kind switch
    {
        TreasuryEntryKind.Trip => "core.treasury.kind.trip",
        TreasuryEntryKind.Accounts => "core.treasury.kind.accounts",
        TreasuryEntryKind.Sale => "core.treasury.kind.sale",
        TreasuryEntryKind.Loot => "core.treasury.kind.loot",
        TreasuryEntryKind.Armory => "core.treasury.kind.armory",
        TreasuryEntryKind.Training => "core.treasury.kind.training",
        TreasuryEntryKind.Spells => "core.treasury.kind.spells",
        TreasuryEntryKind.Inn => "core.treasury.kind.inn",
        TreasuryEntryKind.Ship => "core.treasury.kind.ship",
        TreasuryEntryKind.Magic => "core.treasury.kind.magic",
        TreasuryEntryKind.Event => "core.treasury.kind.event",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Signed(long n) => n > 0 ? "+" + n : n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
