namespace OpenPhantasie.Core;

/// <summary>
/// What a character transfer did (<see cref="GameSession.TransferFromPhantasieI"/>, <see cref="GameSession.TransferFromPhantasieII"/>,
/// <see cref="GameSession.TransferIntoPhantasieIII"/>).
/// </summary>
/// <param name="Transferred">Characters now in the target game's Guild.</param>
/// <param name="LeftBehind">
/// Names of source characters not brought over: I ↔ II, past the 22 slots (the target's <c>guild-limit</c> = <c>game</c>);
/// III, a kept character whose name is already in the Guild or who found no room.
/// </param>
public sealed record CharacterTransferResult(int Transferred, IReadOnlyList<string> LeftBehind);

public sealed partial class GameSession
{
    /// <summary>
    /// Phase 09 T5, the I → II transfer of the original's utility (<c>atari-st/phantasie2/logic-specs/transfer.md</c> §7):
    /// a new II game whose roster is I's roster run through <see cref="TransferIToII.ConvertRoster"/> under this save's
    /// rules (<see cref="GameOptions.TransferIToII"/>, and II's <c>guild-limit</c>: <c>none</c> converts every slot, else the
    /// ST's first 22). Runs only on a Phantasie II game just started (<see cref="StartNewGame"/>, nobody in the party): the
    /// ST rewrites the towns from templates, zeroes the sack and deletes the dungeon saves, which a new game already is.
    /// <para>
    /// The roster is replaced slot for slot (Guild number = slot + 1). A remaster character with no DOS <c>@92</c> gets its
    /// slot number, as the DOS export writes it, so the XP raise of §4 runs for it. Everyone arrives out of the party
    /// (the ST keeps <c>@218</c> but lists them as free members). Creation's origin rule is not run: origin stays Gelnor.
    /// </para>
    /// </summary>
    /// <param name="iRoster">Phantasie I's Guild slots (a remaster I save's top-level roster); never changed.</param>
    public CharacterTransferResult TransferFromPhantasieI(IReadOnlyList<Character?> iRoster)
    {
        ArgumentNullException.ThrowIfNull(iRoster);
        if (Game != GameId.P2)
            throw new InvalidOperationException($"The I → II transfer runs in Phantasie II, not {Game.Prefix()}.");
        if (!FreshStart || Party.Members.Count > 0)
            throw new InvalidOperationException("The I → II transfer starts a new Phantasie II game.");

        var unlimited = Options.GuildUnlimited;
        var converted = TransferIToII.ConvertRoster(WithOrdinals(iRoster), Options.TransferIToII, unlimited);
        return ReplaceRoster(converted, iRoster);
    }

    /// <summary>
    /// Phase 09 T5, the II → I transfer of the ST's utility (<c>atari-st/phantasie2/logic-specs/transfer.md</c> section 8): a new
    /// I game whose roster is II's roster run through <see cref="TransferIIToI.ConvertRoster"/> (the ST's values; the manual
    /// has no II → I, so no options) under I's <c>guild-limit</c>. Runs only on a Phantasie I game just started, nobody in
    /// the party: the ST rewrites I's towns and maps from templates and deletes the sack and dungeon saves, which a new
    /// game already is. Everyone arrives out of the party, in Pineville's Guild (<see cref="TransferIIToI.StartTown"/>).
    /// </summary>
    /// <param name="iiRoster">Phantasie II's Guild slots (a remaster II save's top-level roster); never changed.</param>
    public CharacterTransferResult TransferFromPhantasieII(IReadOnlyList<Character?> iiRoster)
    {
        ArgumentNullException.ThrowIfNull(iiRoster);
        if (Game != GameId.P1)
            throw new InvalidOperationException($"The II → I transfer runs in Phantasie I, not {Game.Prefix()}.");
        if (!FreshStart || Party.Members.Count > 0)
            throw new InvalidOperationException("The II → I transfer starts a new Phantasie I game.");
        return ReplaceRoster(TransferIIToI.ConvertRoster(WithOrdinals(iiRoster), Options.GuildUnlimited), iiRoster);
    }

    /// <summary>
    /// Phase 09 T5, the I / II → III transfer (<c>phantasie3/FORMAT.md</c> section 6, D3-001…D3-007, D3-016, D3-030): a menu
    /// action any time (D3-030, the manual's utilities option), adding the kept characters to III's Guild roster
    /// (<see cref="Phantasie3Campaign.AdmitTransferred"/>). DOS's per-character `transfer.keep_question` is
    /// <paramref name="keep"/> (source slot indices; null keeps everyone). The source's elementals are never in a remaster
    /// roster. A character that finds no room (D3-029's roster limit) or whose name is already in the Guild stays behind.
    /// <c>p3.transfer-roster</c> = <c>erase</c> (the manual's text, D3-003) first erases every III character, the party with
    /// them; the default adds, as DOS.
    /// </summary>
    /// <param name="sourceRoster">The source game's Guild slots (a remaster I or II save's top-level roster); never changed.</param>
    /// <param name="source"><see cref="GameId.P1"/>, or <see cref="GameId.P2"/> unless <c>p3.transfer-source</c> = <c>dos</c>.</param>
    public CharacterTransferResult TransferIntoPhantasieIII(IReadOnlyList<Character?> sourceRoster, GameId source,
        IReadOnlyCollection<int>? keep = null)
    {
        ArgumentNullException.ThrowIfNull(sourceRoster);
        if (Campaign is not Phantasie3Campaign p3)
            throw new InvalidOperationException($"The I / II → III transfer runs in Phantasie III, not {Game.Prefix()}.");
        var rules = Options.P3Transfer();
        if (source is not (GameId.P1 or GameId.P2) || (source == GameId.P2 && !rules.FromIIAllowed))
            throw new InvalidOperationException($"Phantasie III does not take characters from {source.Prefix()} under these rules.");

        if (rules.EraseRoster && Mode != GameMode.Town)
            throw new InvalidOperationException("With the roster erased first, the transfer runs in a town.");

        var wand = p3.SourceHasWand(sourceRoster);
        if (rules.EraseRoster)
        {
            Roster.Reset(0);
            Party.Members.Clear();
            Party.Elemental = null;
            _otherParties.Clear();
        }

        var added = 0;
        var leftBehind = new List<string>();
        for (var i = 0; i < sourceRoster.Count; i++)
        {
            if (sourceRoster[i] is not { } from || (keep is not null && !keep.Contains(i)))
                continue;
            if (!Roster.HasRoom || !CharacterCreation.IsNameUnique(from.Name, Roster.Occupied)
                || Roster.FreeSlot() is not (var slot and >= 0))
            {
                leftBehind.Add(from.Name);
                continue;
            }
            Roster.Slots[slot] = p3.AdmitTransferred(this, from, source, rules, wand);
            added++;
        }
        return new CharacterTransferResult(added, leftBehind);
    }

    /// <summary>A slot holding a character is an occupied record: a remaster character with no DOS <c>@92</c> gets slot + 1.</summary>
    private static Character?[] WithOrdinals(IReadOnlyList<Character?> roster) =>
        roster.Select((c, i) => c is { RosterOrdinal: 0 } ? WithOrdinal(c.Clone(), i + 1) : c).ToArray();

    /// <summary>The target's roster becomes <paramref name="converted"/> slot for slot; party, elemental, purse and parked parties emptied.</summary>
    private CharacterTransferResult ReplaceRoster(Character?[] converted, IReadOnlyList<Character?> source)
    {
        var leftBehind = source.Skip(converted.Length).OfType<Character>().Select(c => c.Name).ToArray();
        Roster.Reset(converted.Length);
        for (var i = 0; i < converted.Length; i++)
        {
            if (converted[i] is { } c)
                c.InParty = false;
            Roster.Slots[i] = converted[i];
        }
        Party.Members.Clear();
        Party.Elemental = null;
        Party.GoldCarried = 0;
        _otherParties.Clear();
        return new CharacterTransferResult(converted.Count(c => c is not null), leftBehind);
    }

    private static Character WithOrdinal(Character c, int ordinal)
    {
        c.RosterOrdinal = ordinal;
        return c;
    }
}
