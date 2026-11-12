using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// P3 "Reset the game" (<c>docs/plan/16-reset-game.md</c> section 3): one save is read, a new game is built from GameData
/// under the save's own locked rules, Keep carries the characters over, and the result is written back over the same file.
/// The Hall of Glory (IV) is copied in both modes. Nothing is written when the save can't be read.
/// </summary>
public static class GameReset
{
    /// <summary>
    /// The version profile whose I tables are the Amiga's own grids (from <c>maps.int</c>, never written by play): DOS's
    /// re-fog rule is not run on them (they hold the Amiga's own x8 cells).
    /// </summary>
    public const string AmigaOnlyP1Tables = "p1-amiga-1.0";

    /// <summary>Resets the I–III save at <paramref name="path"/> with the game data at <paramref name="gameDataDirectory"/>.</summary>
    /// <param name="dosFog">Run DOS I's re-fog rule on the regions (I with DOS-sourced data, <see cref="DosFogFor"/>).</param>
    public static SessionSnapshot ResetSession(string path, string gameDataDirectory, ResetMode mode, bool dosFog)
    {
        var save = SessionCodec.ReadJsonFile(path);
        var reset = ResetSession(save, GameDataRepository.Load(gameDataDirectory), mode, dosFog);
        SessionCodec.WriteJsonFile(path, reset);
        return reset;
    }

    /// <summary>The reset game for <paramref name="save"/> (not written).</summary>
    public static SessionSnapshot ResetSession(SessionSnapshot save, GameDataRepository data, ResetMode mode, bool dosFog)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(data);
        if (save.Game == GameId.P4 || data.Game != save.Game)
            throw new ArgumentException($"The save is {save.Game.Prefix()}, the data {data.Game.Prefix()}.", nameof(save));
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(Environment.TickCount));
        session.StartNewGame(save.Options?.ToOptions(save.Game) ?? GameOptions.ForPreset(save.Game));
        if (dosFog && save.Game == GameId.P1)
            session.ApplyDosResetFog();
        if (mode == ResetMode.Keep)
            session.AdoptResetRoster(save);
        return session.CaptureSnapshot();
    }

    /// <summary>Whether a reset of <paramref name="game"/> runs DOS I's re-fog rule: I, unless its tables are the Amiga's own.</summary>
    public static bool DosFogFor(GameId game) =>
        game == GameId.P1 && GameDataLocator.TablesVersion(game) != AmigaOnlyP1Tables;

    /// <summary>Resets IV's save at <paramref name="path"/> on the shipped IV data.</summary>
    public static P4CampaignSnapshot ResetPhantasie4(string path, ResetMode mode)
    {
        var save = Phantasie4SaveCodec.ReadJsonFile(path);
        var options = save.Options.ToOptions(GameId.P4);
        var game = Phantasie4World.NewGame(options, new SeededRandomSource(Environment.TickCount));
        ResetPhantasie4(save, game.Party, options, game.Tables.Towns, mode);
        var reset = game.Capture();
        Phantasie4SaveCodec.WriteJsonFile(path, reset);
        return reset;
    }

    /// <summary>
    /// IV's side on a new game's <paramref name="party"/> (empty, in Pendragon): the Hall copied; with Keep, the save's
    /// roster with each member's record written back to its Guild slot, the purse deposited by IV's own deposit (one share
    /// each) or into the treasury, the treasury shared into the accounts, every record in Pendragon, then the new game's
    /// treasury sweep.
    /// </summary>
    public static void ResetPhantasie4(P4CampaignSnapshot save, P4Party party, GameOptions options, P4TownTables towns,
        ResetMode mode)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(party);
        var old = save.Party.ToParty();
        Array.Copy(old.HallDescendants, party.HallDescendants, P4Party.HallSlots);
        Array.Copy(old.HallImports, party.HallImports, P4Party.HallSlots);
        if (mode != ResetMode.Keep)
            return;

        var treasury = old.Treasury.Clone();
        if (old.Gold > 0 && options.TreasuryOn)
            treasury.Add(old.Gold, TreasuryEntryKind.Trip);
        else if (old.Gold > 0)
        {
            // The members, or with no party every roster record (members are copies of roster records).
            List<P4Member> takers = old.Members.Count > 0 ? old.Members : [.. old.Roster.OfType<P4Member>()];
            var accounts = P4Town.Deposit(options, towns, old.Gold, [.. takers.Select(m => m.Account)],
                [.. takers.Select(_ => 1)]);
            for (var i = 0; i < accounts.Length; i++)
                takers[i].Account = accounts[i];
        }

        if (old.ExperiencePool > 0 && old.Members.Count > 0)
        {
            var xp = P4Town.ShareExperience(old.ExperiencePool, [.. old.Members.Select(m => m.Experience)],
                [.. old.Members.Select(_ => 1)]);
            for (var i = 0; i < xp.Length; i++)
                old.Members[i].Experience = xp[i];
        }
        // The pack is party storage (Distribute / Trade put items back with P, towns.md), so it comes over.
        Array.Copy(old.Pack, party.Pack, P4Party.PackSlots);
        Array.Copy(old.PackCharges, party.PackCharges, P4Party.PackSlots);

        var roster = old.Roster.Select(m => m?.Clone()).ToArray();
        foreach (var m in old.Members)
        {
            var slot = m.GuildSlot is >= 0 and < P4Party.RosterSlots ? m.GuildSlot : Array.IndexOf(roster, null);
            if (slot < 0)
                continue; // no room: cannot happen (members come from the roster) [~]
            m.GuildSlot = slot;
            roster[slot] = m.Clone();
        }
        var count = roster.Count(m => m is not null);
        if (options.TreasuryOn && count > 0 && treasury.Balance > 0)
        {
            var share = treasury.Balance / count;
            foreach (var m in roster)
                if (m is not null)
                    m.Account += share;
        }
        for (var i = 0; i < P4Party.RosterSlots; i++)
        {
            if (roster[i] is { } m)
                m.Town = P4TownLoop.Pendragon;
            party.Roster[i] = roster[i];
        }
        party.SweepTreasury(options, inTown: true);
    }
}
