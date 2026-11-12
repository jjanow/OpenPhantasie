namespace OpenPhantasie.Core;

/// <summary>
/// P3 "Reset the game" (<c>docs/plan/16-reset-game.md</c>): Purge (the original's own command: DOS I's Utility → Reset the
/// game, the manual's "Starts a new game, from scratch") or Keep (the remaster's: the characters stay, the world resets).
/// </summary>
public enum ResetMode
{
    /// <summary>An empty Guild, as on first play.</summary>
    Purge,

    /// <summary>The save's characters stay (out of the party, in the start town); the world resets.</summary>
    Keep,
}

public sealed partial class GameSession
{
    /// <summary>
    /// 16-reset-game.md section 3, Keep (I–III): <paramref name="save"/>'s roster, its trip gold settled and its treasury handed
    /// out (<see cref="KeptRoster"/>), becomes this game's roster slot for slot, everyone in the start town. Runs on a game
    /// just started (<see cref="StartNewGame"/> under the save's rules); the save is not changed.
    /// </summary>
    public void AdoptResetRoster(SessionSnapshot save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Game != Game)
            throw new ArgumentException($"The save is {save.Game.Prefix()}, this game is {Game.Prefix()}.", nameof(save));
        var roster = KeptRoster(save, Rules);
        ReplaceRoster([.. roster], roster);
        foreach (var c in Roster.Occupied)
            c.CurrentTown = Campaign.StartTownId;
    }

    /// <summary>
    /// The roster a Keep reset carries over, as clones: the trip gold (purse + sack gold) goes into the treasury when the
    /// save's treasury is on, else into the party members' bank by the Bank's deposit with one share each
    /// (<see cref="TownMenus.DepositShares(Party, IReadOnlyList{int})"/>, DOS's remainder dropped); then the treasury is
    /// shared out as the transfers do (<see cref="TreasuryShares"/>).
    /// </summary>
    public static IReadOnlyList<Character?> KeptRoster(SessionSnapshot save, RulesProfile? rules = null)
    {
        ArgumentNullException.ThrowIfNull(save);
        var roster = save.RosterSlots.Select(c => c?.Clone()).ToArray();
        var trip = Math.Max(0, save.PartyGold) + Math.Max(0, save.Sack?.Gold ?? 0);
        var treasuryOn = save.Options?.ToOptions(save.Game).TreasuryOn ?? false;
        var treasury = Treasury.From(save.Treasury);
        if (trip > 0 && treasuryOn)
            treasury.Add(trip, TreasuryEntryKind.Trip);
        else if (trip > 0)
        {
            var party = new Party(rules ?? RulesProfile.PhantasieI) { GoldCarried = trip }; // the game's bank cap
            party.Members.AddRange(Takers(save, roster));
            if (party.Members.Count > 0)
                TownMenus.DepositShares(party, [.. party.Members.Select(_ => 1)]);
        }
        // The pending XP pool (paid on town entry), one share each, the remainder dropped as the gold's.
        var xp = Math.Max(0, save.Sack?.Experience ?? 0);
        var takers = Takers(save, roster);
        if (xp > 0 && takers.Count > 0)
        {
            var each = xp / takers.Count;
            foreach (var m in takers)
                m.Experience += each;
        }
        var count = roster.Count(c => c is not null);
        if (treasuryOn && count > 0 && treasury.Balance > 0)
        {
            var share = treasury.Balance / count;
            foreach (var c in roster)
                if (c is not null)
                    c.GoldInBank = (int)Math.Min(int.MaxValue, c.GoldInBank + share);
        }
        return roster;
    }

    /// <summary>Who takes the trip gold and XP: the party members, or with no party every roster character.</summary>
    private static List<Character> Takers(SessionSnapshot save, Character?[] roster)
    {
        var party = save.PartySlotIndices.Where(i => i >= 0 && i < roster.Length && roster[i] is not null)
            .Select(i => roster[i]!).ToList();
        return party.Count > 0 ? party : [.. roster.OfType<Character>()];
    }

    /// <summary>
    /// DOS I's Reset (OV1 <c>0x4885</c> → <c>0x4914</c>, <c>save-file-format.md</c> "Other writers"): every region but OUT1 /
    /// OUT5 is fogged again and its spent fixed fights (x8) armed again (x7). A no-op on pristine DOS data. Only for regions
    /// with fog (I / II's 26×20 maps); the caller decides whether the data are DOS's (not the Amiga's own grids).
    /// </summary>
    public void ApplyDosResetFog()
    {
        foreach (var region in _overworld.Regions)
        {
            if (!region.HasFog || DosResetSkips.Contains(region.FileName))
                continue;
            DosResetCells(region.Tiles);
        }
    }

    /// <summary>The regions <c>0x4885</c> leaves alone: blocks 0 / 4 of <c>0x4ca8</c>'s read order.</summary>
    private static readonly HashSet<string> DosResetSkips = new(StringComparer.OrdinalIgnoreCase) { "OUT1.DAT", "OUT5.DAT" };

    /// <summary><c>0x4914</c>'s rule on one region's cells: unfog, x8 → x7, fog.</summary>
    public static void DosResetCells(byte[] tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        for (var i = 0; i < tiles.Length; i++)
        {
            var b = (int)tiles[i];
            if (b >= OverworldRegion.FogOffset)
                b -= OverworldRegion.FogOffset;
            if (b % 10 == 8)
                b--;
            tiles[i] = (byte)(b + OverworldRegion.FogOffset);
        }
    }
}
