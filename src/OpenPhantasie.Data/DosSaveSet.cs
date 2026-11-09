using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// The five DOS save files as raw images (P1.4, <c>08-parity-closeout.md</c>): <c>GUILD.DAT</c>, <c>SACK.DAT</c>,
/// <c>TWNS.DAT</c>, <c>DNG.SAV</c>, <c>DNGX.SAV</c>. A file missing from a folder is null.
/// </summary>
public sealed class DosSaveSet
{
    public const string GuildFile = "GUILD.DAT";
    public const string SackFile = "SACK.DAT";
    public const string TownsFile = "TWNS.DAT";
    public const string DngFile = "DNG.SAV";
    public const string DngxFile = "DNGX.SAV";

    public static readonly IReadOnlyList<string> FileNames = [GuildFile, SackFile, TownsFile, DngFile, DngxFile];

    public byte[]? Guild { get; init; }
    public byte[]? Sack { get; init; }
    public byte[]? Towns { get; init; }
    public byte[]? DngSav { get; init; }
    public byte[]? DngxSav { get; init; }

    /// <summary>Read whichever of the five files exist in <paramref name="folder"/> (names matched case-insensitively).</summary>
    public static DosSaveSet ReadFolder(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(folder);
        byte[]? Load(string name) => FindFile(folder, name) is { } path ? File.ReadAllBytes(path) : null;
        return new DosSaveSet
        {
            Guild = Load(GuildFile),
            Sack = Load(SackFile),
            Towns = Load(TownsFile),
            DngSav = Load(DngFile),
            DngxSav = Load(DngxFile),
        };
    }

    /// <summary>The files of the set that already exist in <paramref name="folder"/> (for the overwrite confirmation).</summary>
    public static IReadOnlyList<string> ExistingFiles(string folder) =>
        Directory.Exists(folder)
            ? FileNames.Select(n => FindFile(folder, n)).OfType<string>().ToList()
            : Array.Empty<string>();

    /// <summary>
    /// Write all five files. Never overwrites without <paramref name="overwrite"/> (P1.4 decision 9): if any of them
    /// exists and it is false, nothing is written and an <see cref="IOException"/> is thrown.
    /// </summary>
    public void WriteFolder(string folder, bool overwrite = false)
    {
        if (Guild is null || Sack is null || Towns is null || DngSav is null || DngxSav is null)
            throw new InvalidOperationException("An export set must hold all five files.");
        var existing = ExistingFiles(folder);
        if (existing.Count > 0 && !overwrite)
            throw new IOException($"{folder} already holds {string.Join(", ", existing.Select(Path.GetFileName))}.");
        Directory.CreateDirectory(folder);
        void Save(string name, byte[] data) => File.WriteAllBytes(FindFile(folder, name) ?? Path.Combine(folder, name), data);
        Save(GuildFile, Guild);
        Save(SackFile, Sack);
        Save(TownsFile, Towns);
        Save(DngFile, DngSav);
        Save(DngxFile, DngxSav);
    }

    private static string? FindFile(string folder, string name) =>
        Directory.EnumerateFiles(folder)
            .FirstOrDefault(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Result of <see cref="DosSaveConverter.Import"/>: a session snapshot plus the warnings list.</summary>
public sealed record DosImportResult(SessionSnapshot Snapshot, IReadOnlyList<DosSaveWarning> Warnings);

/// <summary>Result of <see cref="DosSaveConverter.Export"/>. <see cref="Files"/> is null when a blocker was found.</summary>
public sealed record DosExportResult(DosSaveSet? Files, IReadOnlyList<DosSaveWarning> Warnings)
{
    public bool Blocked => Files is null;
}

/// <summary>
/// DOS save set ↔ remaster <see cref="SessionSnapshot"/> (P1.4, decisions s38). Import loads as-is and lists every
/// state the remaster rules would not have produced; export runs in town only, after the XP/gold split, refuses a sack
/// over 60 items, and clamps + lists values that don't fit. Unknown bytes ride along in the raw-record carries
/// (<see cref="Phantasie1CharacterData.DosRecord"/>, <see cref="TownState.DosRecord"/>; the sack keeps its slot holes), so an
/// unchanged import exports byte-identical files.
/// </summary>
public static class DosSaveConverter
{
    /// <summary>
    /// Convert a DOS save set into a snapshot for <see cref="GameSession.ApplySnapshot"/>.
    /// <paramref name="data"/> supplies the D-003 lifespan tables and, without TWNS.DAT, the town exit positions.
    /// <paramref name="goldCap"/> is the save's <c>p1.gold-cap</c> (D-048): a bank balance over that cap (65,000, 65,535 under
    /// <c>dos</c>, none when lifted) is listed as a rule break.
    /// </summary>
    public static DosImportResult Import(DosSaveSet set, GameDataRepository data, GoldCapRule goldCap = GoldCapRule.Manual)
        => Import(set, data, goldCap, dosGuildSize: false, dosLifespans: false, guildUnlimited: false);

    /// <summary>
    /// <see cref="Import(DosSaveSet, GameDataRepository, GoldCapRule)"/> under a session's rules: the notes follow its
    /// <c>p1.gold-cap</c>, <c>p1.guild-size</c> (D-002; no note under <c>guild-limit</c> = <c>none</c>) and <c>p1.lifespans</c> (D-003).
    /// </summary>
    public static DosImportResult Import(DosSaveSet set, GameDataRepository data, GameOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Import(set, data, options.GoldCap, options.IsDos(RuleOptionRegistry.GuildSize),
            options.IsDos(RuleOptionRegistry.Lifespans), options.GuildUnlimited);
    }

    private static DosImportResult Import(DosSaveSet set, GameDataRepository data, GoldCapRule goldCap, bool dosGuildSize,
        bool dosLifespans, bool guildUnlimited)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(data);
        var warnings = new List<DosSaveWarning>();
        var snap = new SessionSnapshot
        {
            Version = SessionSnapshot.CurrentVersion,
            RosterSlots = new Character?[Math.Max(GuildCodec.SlotCount, data.Rules.GuildSlotCount)],
        };

        // GUILD.DAT
        if (set.Guild is null)
        {
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile, GameText.S("core.dossave.guild_missing")));
        }
        else
        {
            var slots = GuildCodec.ReadBinary(set.Guild, data.CreationTables);
            for (var i = 0; i < slots.Count; i++)
            {
                snap.RosterSlots[i] = slots[i];
                var rec = set.Guild.AsSpan(i * GuildCodec.RecordSize, GuildCodec.RecordSize);
                // Purge (OV1 0x23d5) and destruction clear only @218/@256/@92: the name and stats stay, but a slot
                // with @92 = 0 is empty to DOS (OV1 0x0C59), so GuildCodec reads it as empty.
                if (slots[i] is null && rec.IndexOfAnyExcept((byte)0) >= 0)
                    warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile,
                        rec[270] != 0 && DosFieldWriter.ReadU16(rec, 92) == 0
                            ? GameText.F("core.dossave.slot_x_purged", i)
                            : GameText.F("core.dossave.slot_x_no_name", i)));
                if (slots[i] is { } ch)
                    CheckImportedCharacter(ch, i, rec, goldCap.Apply(data.Rules.BankGoldCap), warnings);
            }

            var count = slots.Count(c => c is not null);
            var guildMax = dosGuildSize ? data.Rules.ForPlatform("dos").GuildMaxCharacters : data.Rules.GuildMaxCharacters;
            if (count > guildMax && !guildUnlimited) // guild-limit = none: every count fits
                warnings.Add(new(DosSaveWarningKind.RuleBreak, DosSaveSet.GuildFile,
                    GameText.F("core.dossave.x_characters_guild_holds_x", count, guildMax)));
            if (count > 0)
                warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile,
                    GameText.S(dosLifespans ? "core.dossave.lifespans_dos" : "core.dossave.lifespans")));
        }

        // TWNS.DAT (read before the party: the party's town supplies the wilderness position)
        if (set.Towns is null)
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.TownsFile, GameText.S("core.dossave.towns_missing")));
        else
            snap.Towns = TownsCodec.Read(set.Towns).ToArray();

        // Party [C] (load, OV1 0x7dd(2) loop 0x866..0x89d): roster slots 0..21 in order, every record with @218 != 0
        // goes to the next party slot. @260 and @92 are not read.
        var members = snap.RosterSlots
            .Select((c, i) => (c, i))
            .Where(t => t.c is { InParty: true })
            .ToList();
        if (members.Count > Party.MaxMembers)
            warnings.Add(new(DosSaveWarningKind.RuleBreak, DosSaveSet.GuildFile,
                GameText.F("core.dossave.x_marked_in_party_first_x_x", members.Count, Party.MaxMembers,
                    string.Join(", ", members.Take(Party.MaxMembers).Select(m => m.c!.Name)))));
        var party = members.Take(Party.MaxMembers).ToList();
        snap.PartySlotIndices = party.Select(t => t.i).ToArray();

        // [C] The town is @254 of party slot 0 (the first flagged record); with nobody flagged OV1 0x7e7 sets the town
        // index [0xc470] := 0, so DOS opens the Pelnor town menu with an empty party (OV1 0xad5d refuses what needs one).
        // Character.CurrentTown is already the 1-based town id (GuildCodec converts the 0-based @254).
        var townId = 1;
        if (party.Count == 0)
        {
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile,
                GameText.S("core.dossave.no_active_party")));
        }
        else
        {
            var towns = party.Select(t => t.c!.CurrentTown).Distinct().ToList();
            townId = towns[0];
            if (towns.Count > 1)
                warnings.Add(new(DosSaveWarningKind.RuleBreak, DosSaveSet.GuildFile,
                    GameText.F("core.dossave.different_towns_x_opens_in_x", string.Join(", ", towns), townId)));
            if (townId is < 1 or > TownsCodec.TownCount)
            {
                // [?] DOS would index past the 12 TWNS records; the remaster opens in Pelnor instead.
                warnings.Add(new(DosSaveWarningKind.RuleBreak, DosSaveSet.GuildFile,
                    GameText.F("core.dossave.party_town_x_not_a_town", GuildCodec.TownToDos(townId))));
                townId = 1;
            }
        }
        snap.Mode = GameMode.Town;
        snap.CurrentTownId = townId;
        // [C] Town Exit (OV1 0x3309..0x331a) puts the party at the town's TWNS +0x111/+0x113/+0x115 (region, x, y),
        // so the import starts the wilderness position there. The words are static: game data serves without TWNS.DAT.
        var town = snap.Towns.FirstOrDefault(t => t.Id == townId) ?? data.CreateTownState(townId);
        if (town.ExitPosition is { } at)
        {
            snap.OverworldRegionId = at.Region;
            snap.OverworldX = at.X;
            snap.OverworldY = at.Y;
            // The tile class there (PHANTASIA's exit is a plain cell, save-file-format.md §Town state); 4 = town tile 40.
            var region = data.OverworldRegions.FirstOrDefault(r => r.RegionId == at.Region);
            snap.OverworldTerrainClass =
                region is not null && (uint)at.Y < (uint)region.Tiles.Count && (uint)at.X < (uint)region.Tiles[at.Y].Count
                    ? OverworldRegion.Normalize(region.Tiles[at.Y][at.X]) / 10
                    : 4;
        }

        // SACK.DAT — XP/GP are not in the file (split before every DOS save): 0.
        if (set.Sack is null)
        {
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.SackFile, GameText.S("core.dossave.sack_missing")));
        }
        else
        {
            var slots = SackCodec.ReadSlots(set.Sack);
            snap.Sack = new LootSackSnapshot { ItemIds = SackCodec.TrimSlots(slots) }; // [D✓] s65: holes kept
        }

        // DNG.SAV + DNGX.SAV
        if (set.DngSav is null || set.DngxSav is null)
        {
            if (set.DngSav is not null || set.DngxSav is not null)
                warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.DngFile,
                    GameText.S("core.dossave.one_dungeon_file")));
        }
        else
        {
            var slot = DungeonSaveCodec.Read(set.DngSav, set.DngxSav);
            if (slot is null)
            {
                if (set.DngxSav.AsSpan().IndexOfAnyExcept((byte)0) >= 0 || set.DngSav.AsSpan().IndexOfAnyExcept((byte)0) >= 0)
                    warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.DngFile,
                        GameText.S("core.dossave.dungeon_id_0_with_data")));
            }
            else
            {
                snap.DungeonSave = new DungeonSaveSnapshot
                {
                    DungeonId = slot.DungeonId,
                    LevelBuffer = slot.LevelBuffer,
                    ScreenImage = slot.ScreenImage,
                };
            }
        }

        return new DosImportResult(snap, warnings);
    }

    /// <summary>
    /// D-003 <c>dos</c> (<c>p1.lifespans</c>): every imported character keeps its DOS lifespan <c>@252</c> (× 256 into
    /// age-clock units), in place of the manual band the import gives (user s17, the manual rule), so export writes it back
    /// unchanged.
    /// </summary>
    private static void KeepDosLifespans(SessionSnapshot snap)
    {
        foreach (var ch in snap.RosterSlots)
        {
            if (ch?.Phantasie1.DosRecord is not { Length: GuildCodec.RecordSize } raw)
                continue;
            ch.Lifespan = DosFieldWriter.ReadU16(raw, GuildCodec.Field.Lifespan) * Aging.DosLifespanStep;
            ch.Phantasie1.DosRecordLifespan = ch.Lifespan;
        }
    }

    /// <summary>Import into a live session (<see cref="GameSession.ApplySnapshot"/>).</summary>
    public static IReadOnlyList<DosSaveWarning> ImportInto(GameSession session, DosSaveSet set, GameDataRepository data)
    {
        ArgumentNullException.ThrowIfNull(session);
        var result = Import(set, data, session.Options); // the notes follow the session's rules (D-048, D-002, D-003)
        // T5.0 §5: a DOS save holds no remaster options, so the running session's rules stay (gold cap, restock, …).
        result.Snapshot.Options ??= GameOptionsSnapshot.From(session.Options);
        if (session.Options.IsDos(RuleOptionRegistry.Lifespans))
            KeepDosLifespans(result.Snapshot);
        session.ApplySnapshot(result.Snapshot);
        return result.Warnings;
    }

    /// <summary>
    /// Build the five DOS files from a snapshot. Blockers (nothing written): not in a town (manual: "S)AVE GAME is always
    /// accomplished in a town"), more than 60 sack items, or fight XP/gold / carried gold with no living member to take
    /// it. DOS splits the XP/gold pool and the carried purse into the members' <c>@266</c> / bank <c>@262</c> before
    /// every save; the export does that automatically on copies (user s40, P1.4 decision 6 — see <see cref="AutoSplit"/>),
    /// so the snapshot and the live game keep their purse. Towns the session never built come from
    /// <paramref name="townFallback"/> (the game data), with a note. The split uses <paramref name="rules"/>' bank cap
    /// (default Phantasie I's, the only game with DOS saves). <paramref name="dropRemainder"/> (default: the snapshot's
    /// <c>p1.payout-remainder</c> is <c>dropped</c>, Q2 / D-037): the fight XP/gold payout loses its remainders, as on town entry.
    /// </summary>
    public static DosExportResult Export(
        SessionSnapshot snap, Func<int, TownState>? townFallback = null, RulesProfile? rules = null, bool? dropRemainder = null)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var warnings = new List<DosSaveWarning>();

        if (snap.Mode != GameMode.Town || snap.CurrentTownId is null)
            warnings.Add(new(DosSaveWarningKind.Blocker, "*",
                GameText.S("core.dossave.only_in_a_town")));
        var sackItems = snap.Sack.ItemIds.Count(id => id != 0);
        if (sackItems > SackCodec.SlotCount)
            warnings.Add(new(DosSaveWarningKind.Blocker, DosSaveSet.SackFile,
                GameText.F("core.dossave.sack_holds_x_dos_holds_x", sackItems, SackCodec.SlotCount)));
        if (warnings.Any(w => w.Kind == DosSaveWarningKind.Blocker))
            return new DosExportResult(null, warnings);
        var roster = snap.RosterSlots;
        if (snap.Sack.Experience != 0 || snap.Sack.Gold != 0 || snap.PartyGold != 0 || TreasuryGold(snap) != 0)
            roster = AutoSplit(snap, rules ?? RulesProfile.PhantasieI,
                dropRemainder ?? snap.Options?.ToOptions(snap.Game).PayoutRemainderDropped ?? false, warnings);
        if (warnings.Any(w => w.Kind == DosSaveWarningKind.Blocker))
            return new DosExportResult(null, warnings);

        if (snap.Elemental is not null)
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile,
                GameText.S("core.dossave.elemental_not_saved")));

        // D-069: DOS loads every record with @218 != 0 into one party (OV1 0x866..0x89d), so members of the parked
        // parties go out without the flag; their purse and sack have no place in the DOS files.
        var parked = snap.OtherParties.SelectMany(p => p.PartySlotIndices)
            .Where(i => (uint)i < (uint)roster.Length && roster[i] is not null && !snap.PartySlotIndices.Contains(i))
            .Distinct().Order().ToList();
        if (parked.Count > 0)
        {
            if (ReferenceEquals(roster, snap.RosterSlots))
                roster = roster.Select(c => c?.Clone()).ToArray();
            foreach (var i in parked)
                roster[i]!.InParty = false;
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile,
                GameText.F(snap.OtherParties.Length == 1 ? "core.dossave.one_party_x_x_other_party" : "core.dossave.one_party_x_x_other_parties",
                    string.Join(", ", parked.Select(i => roster[i]!.Name)), snap.OtherParties.Length)));
        }

        roster = FitGuild(roster, snap.PartySlotIndices, warnings);
        var guild = GuildCodec.WriteBinary(roster, warnings);
        var sack = SackCodec.Write(snap.Sack.ItemIds, warnings);

        var towns = snap.Towns.ToDictionary(t => t.Index);
        for (var i = 0; i < TownsCodec.TownCount; i++)
        {
            if (towns.ContainsKey(i) || townFallback is null) continue;
            var town = townFallback(i + 1);
            towns[i] = town;
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.TownsFile,
                GameText.F("core.dossave.x_not_visited", town.Name)));
        }
        var twns = TownsCodec.Write(towns.Values, warnings);

        byte[] dng, dngx;
        if (snap.DungeonSave is { } ds)
        {
            var slot = new DungeonSaveSlot(ds.DungeonId, ds.LevelBuffer, ds.ScreenImage);
            dng = DungeonSaveCodec.WriteDngSav(slot);
            dngx = DungeonSaveCodec.WriteDngxSav(slot);
        }
        else
        {
            // No slot: both files zero, as on the master disk / after Utility → Reset (no dungeon id matches).
            dng = new byte[DungeonSaveCodec.DngSavSize];
            dngx = new byte[DungeonSaveCodec.DngxSavSize];
        }

        return new DosExportResult(
            new DosSaveSet { Guild = guild, Sack = sack, Towns = twns, DngSav = dng, DngxSav = dngx },
            warnings);
    }

    /// <summary>
    /// The QoL option <c>guild-limit</c> = <c>none</c> lets the roster grow past GUILD.DAT's
    /// <see cref="GuildCodec.SlotCount"/> records. A roster that fits is returned as it is (the export stays byte for byte
    /// what it was). Otherwise the export keeps the active party (<paramref name="partySlots"/>) and then the others by Guild
    /// number up to the file's size; a kept character past the file's slots moves into a free lower slot (a note), and every
    /// character left out is named (a clamp warning). Only the DOS files lose them: the snapshot is not changed.
    /// </summary>
    public static Character?[] FitGuild(Character?[] roster, IReadOnlyCollection<int> partySlots, List<DosSaveWarning> warnings)
    {
        const int size = GuildCodec.SlotCount;
        if (roster.Length <= size)
            return roster;
        var occupied = roster.Select((c, i) => (Char: c, Index: i)).Where(x => x.Char is not null).ToList();
        if (occupied.All(x => x.Index < size))
            return roster[..size];

        var kept = occupied.Where(x => partySlots.Contains(x.Index))
            .Concat(occupied.Where(x => !partySlots.Contains(x.Index)))
            .Take(size)
            .Select(x => x.Index)
            .ToHashSet();
        var fitted = new Character?[size];
        foreach (var i in kept.Where(i => i < size))
            fitted[i] = roster[i];
        var moved = false;
        foreach (var i in kept.Where(i => i >= size).Order())
        {
            fitted[Array.IndexOf(fitted, null)] = roster[i]; // kept.Count <= size, so a hole is always left
            moved = true;
        }
        if (moved)
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile, GameText.F("core.dossave.guild_renumbered", size)));
        var left = occupied.Where(x => !kept.Contains(x.Index)).ToList();
        if (left.Count > 0)
            warnings.Add(new(DosSaveWarningKind.Clamp, DosSaveSet.GuildFile,
                GameText.F("core.dossave.guild_x_characters_dos_holds_x", occupied.Count, size,
                    string.Join(", ", left.Select(x => x.Char!.Name)))));
        return fitted;
    }

    /// <summary>
    /// P1.4 decision 6 (user s40: the manual is silent, so do it automatically): pay the fight XP/gold out as on town entry
    /// (<see cref="TownMenus.Distribute"/>, D-037: equal split, remainder to the first survivor, or lost when
    /// <paramref name="dropRemainder"/>, <c>p1.payout-remainder</c> = <c>dropped</c>), then put the carried
    /// gold into the living members' banks in equal shares, rounded up so nothing is left over (user s80). Bank gold over the cap (65,000, or
    /// 65,535 under the DOS or lifted <c>p1.gold-cap</c>) is lost (D-048) and listed. Works on clones; returns the roster to write. No living member → a blocker.
    /// </summary>
    /// <summary>The Guild treasury's balance when <paramref name="snap"/> keeps one (<c>11-guild-treasury.md</c> section 5), else 0.</summary>
    private static long TreasuryGold(SessionSnapshot snap) =>
        snap.Options?.ToOptions(snap.Game).TreasuryOn == true ? snap.Treasury?.Balance ?? 0 : 0;

    private static Character?[] AutoSplit(
        SessionSnapshot snap, RulesProfile rules, bool dropRemainder, List<DosSaveWarning> warnings)
    {
        var roster = snap.RosterSlots.Select(c => c?.Clone()).ToArray();
        var party = new Party(rules) { Death = Campaigns.For(rules.Game).Death }; // the game's death step, not I's by default
        foreach (var i in snap.PartySlotIndices)
            if ((uint)i < (uint)roster.Length && roster[i] is { } m)
                party.Members.Add(m);
        var living = party.LivingMembers.ToList();
        if (living.Count == 0)
        {
            warnings.Add(new(DosSaveWarningKind.Blocker, DosSaveSet.GuildFile,
                GameText.F("core.dossave.party_holds_x_xp_x_gp_no_living", snap.Sack.Experience, snap.Sack.Gold + snap.PartyGold)));
            return roster;
        }

        party.GoldCarried = snap.PartyGold;
        if (TreasuryGold(snap) is > 0 and var treasury) // s175: the Guild treasury joins the purse the split banks
        {
            party.GoldCarried += treasury;
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile,
                GameText.F("core.dossave.treasury_x_into_banks", treasury)));
        }
        TownMenus.Distribute(party, new LootSack
        {
            Experience = snap.Sack.Experience, Gold = snap.Sack.Gold, GoldCap = rules.PartyGoldCap,
        }, dropRemainder: dropRemainder);
        var carried = party.GoldCarried;
        // User s80: the carried purse is rounded up so it divides evenly; every account gets the same share, even if
        // that adds up to living.Count - 1 gold.
        var share = (carried + living.Count - 1) / living.Count;
        for (var k = 0; k < living.Count; k++)
        {
            var lost = living[k].AddBankGold(share, party.BankGoldCap);
            if (lost > 0)
                warnings.Add(new(DosSaveWarningKind.Clamp, DosSaveSet.GuildFile,
                    GameText.F("core.dossave.x_bank_full_at_x_lost_x", living[k].Name, party.BankGoldCap, lost)));
        }
        warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile,
            GameText.F("core.dossave.split_x_xp_x_gp_then_x_into_x", snap.Sack.Experience, snap.Sack.Gold, carried, living.Count)));
        return roster;
    }

    /// <summary>
    /// Export a live session (<see cref="GameSession.CaptureSnapshot"/>), unvisited towns from <paramref name="data"/>.
    /// The split uses the save's gold caps (<c>p1.gold-cap</c>, D-048), but never above DOS's 16-bit 65,535: with the cap
    /// lifted, a share that would pass 65,535 is lost and listed, and a balance already above it is written as 65,535 and
    /// listed (the u16 clamp). The live game keeps its gold either way.
    /// </summary>
    public static DosExportResult ExportFrom(GameSession session, GameDataRepository data)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(data);
        var rules = session.Rules with
        {
            PartyGoldCap = Math.Min(session.Party.GoldCap, GoldCaps.Dos),
            BankGoldCap = Math.Min(session.Party.BankGoldCap, GoldCaps.Dos),
        };
        return Export(session.CaptureSnapshot(), data.CreateTownState, rules, session.Options.PayoutRemainderDropped);
    }

    private static void CheckImportedCharacter(
        Character ch, int slot, ReadOnlySpan<byte> rec, long bankGoldCap, List<DosSaveWarning> warnings)
    {
        var status = DosFieldWriter.ReadU16(rec, 24);
        if (status > 2)
            warnings.Add(new(DosSaveWarningKind.RuleBreak, DosSaveSet.GuildFile,
                GameText.F("core.dossave.x_slot_x_status_x", ch.Name, slot, status)));
        if (ch.GoldInBank > bankGoldCap)
            warnings.Add(new(DosSaveWarningKind.RuleBreak, DosSaveSet.GuildFile,
                GameText.F("core.dossave.x_slot_x_bank_x_over_x", ch.Name, slot, ch.GoldInBank, bankGoldCap)));
        var inParty = DosFieldWriter.ReadU16(rec, 218);
        if (inParty > 1)
            warnings.Add(new(DosSaveWarningKind.RuleBreak, DosSaveSet.GuildFile,
                GameText.F("core.dossave.x_slot_x_party_flag_x", ch.Name, slot, inParty)));
        var sawEmpty = false;
        for (var off = 222; off < 222 + 2 * Character.InventorySlots; off += 2)
        {
            var id = DosFieldWriter.ReadU16(rec, off);
            if (id == 0) { sawEmpty = true; continue; }
            if (!sawEmpty) continue;
            warnings.Add(new(DosSaveWarningKind.Note, DosSaveSet.GuildFile,
                GameText.F("core.dossave.x_slot_x_item_gaps", ch.Name, slot)));
            break;
        }
    }
}
