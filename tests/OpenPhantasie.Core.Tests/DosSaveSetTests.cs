using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>P1.4 save-file compatibility: raw-record carry, SACK/TWNS codecs, DOS set import/export (s39).</summary>
public class DosSaveSetTests
{
    private static readonly Lazy<GameDataRepository> DataLazy = new(GameDataRepository.LoadPhantasie1);
    private static GameDataRepository Data => DataLazy.Value;

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "samples", name);
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static byte[] Bytes(string name) => File.ReadAllBytes(Sample(name));

    /// <summary>
    /// The GUILD.DAT the codec writes back: slots with <c>@92 = 0</c> are empty to DOS (OV1 <c>0x0C59</c>), so the
    /// DESTROYED members' leftover records (s36 sample: wizardo, priesto, crusho, monko, rango) come back as zeros.
    /// </summary>
    private static byte[] Occupied(byte[] guild)
    {
        var copy = (byte[])guild.Clone();
        for (var i = 0; i < GuildCodec.SlotCount; i++)
        {
            var o = i * GuildCodec.RecordSize;
            if (copy[o + 92] == 0 && copy[o + 93] == 0)
                Array.Clear(copy, o, GuildCodec.RecordSize);
        }
        return copy;
    }

    public static TheoryData<string> GuildFiles => new()
    {
        "samples/GUILD.3chars.dat", "samples/GUILD.pass2.dat", "samples/GUILD.pass3.dat", "samples/GUILD.pass4.dat",
        "samples/GUILD.s35-after-inn.dat", "samples/GUILD.s35-dead.dat", "samples/GUILD.s36-after-wipes.dat",
        "GUILD.h3status.dat", "GUILD.roster22.dat",
    };

    // --- Per-file byte-exact round trips ---------------------------------------------------------------------

    [OriginalDataTheory]
    [MemberData(nameof(GuildFiles))]
    public void Guild_ImportExport_IsByteIdentical(string file)
    {
        var original = File.ReadAllBytes(Fixture(file));
        var rewritten = GuildCodec.WriteBinary(GuildCodec.ReadBinary(original, Data.CreationTables));
        Assert.Equal(Occupied(original), rewritten);
    }

    /// <summary>
    /// P1.1 field map (s49): with the raw-record carry dropped, the codec alone must rebuild every byte of every real
    /// record except the fields Core does not keep — <c>@246..@251</c> (fight-start copies, rebuilt each combat),
    /// <c>@252</c> (D-003 lifespan band replaces the DOS word) and the name's padding bytes.
    /// </summary>
    [OriginalDataTheory]
    [MemberData(nameof(GuildFiles))]
    public void Guild_FieldMapAlone_RebuildsEveryMappedByte(string file)
    {
        var original = Occupied(File.ReadAllBytes(Fixture(file)));
        var slots = GuildCodec.ReadBinary(original, Data.CreationTables);
        foreach (var ch in slots.OfType<Character>())
            ch.Phantasie1.DosRecord = null;
        var rewritten = GuildCodec.WriteBinary(slots);
        var names = GuildCodec.ReadBinary(rewritten, Data.CreationTables).Select(c => c?.Name).ToList();
        Assert.Equal(slots.Select(c => c?.Name), names);
        Assert.All(Diff(original, rewritten), o =>
            Assert.True(o % GuildCodec.RecordSize is (>= 246 and <= 253) or (>= 270 and <= 281),
                $"record byte @{o % GuildCodec.RecordSize} (slot {o / GuildCodec.RecordSize}) not rebuilt by the field map"));
    }

    [OriginalDataFact]
    public void DestroyedRecords_AreEmptySlots_OnImport()
    {
        // P0.8 (s56): DOS keeps a DESTROYED member's name and stats and only zeroes @218/@24/@92 at the next town entry.
        var slots = GuildCodec.ReadBinary(Bytes("GUILD.s36-after-wipes.dat"), Data.CreationTables);
        Assert.Equal(["thiefo", "fighto", "gobbo"], slots.OfType<Character>().Select(c => c.Name));
        Assert.Null(slots[0]); // wizardo
    }

    [OriginalDataFact]
    public void RemasterCharacter_ExportsItsSlotNumber_InAt92()
    {
        var hero = new Character { Name = "nova", Stats = new CharacterStats(10, 10, 10, 10, 10, 10) };
        var bytes = GuildCodec.WriteBinary([null, null, hero]);
        Assert.Equal(3, bytes[2 * GuildCodec.RecordSize + 92]);
        Assert.Equal("nova", GuildCodec.ReadBinary(bytes, Data.CreationTables)[2]!.Name);
    }

    [OriginalDataFact]
    public void RemasterParty_ExportsWithInPartyFlag_AndReimportsAsTheParty()
    {
        // s49: Guild Add sets @218 (OV1 0x2851). Before, a remaster-born party exported with @218 = 0 and came back
        // with no party.
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var a = session.CreateAndEnroll("Alfa", Race.Human, CharacterClass.Fighter);
        var b = session.CreateAndEnroll("Bravo", Race.Elf, CharacterClass.Wizard);
        var c = session.CreateAndEnroll("Charlie", Race.Dwarf, CharacterClass.Priest);
        Assert.True(session.TryAddToParty(a, out _));
        Assert.True(session.TryAddToParty(b, out _));
        session.EnterTown(1);

        var export = DosSaveConverter.ExportFrom(session, Data);
        Assert.False(export.Blocked, string.Join("; ", export.Warnings));
        var import = DosSaveConverter.Import(export.Files!, Data);
        var party = import.Snapshot.PartySlotIndices.Select(i => import.Snapshot.RosterSlots[i]!.Name).ToList();
        Assert.Equal(["Alfa", "Bravo"], party);
        Assert.DoesNotContain(c.Name, party);
    }

    [OriginalDataFact]
    public void Guild_RunesAndWord90_WriteOnlyTheirWords()
    {
        // No fixture holds a rune or a pool visit, so pin the offsets by change: runes @72..@80 (AIR, EARTH, FIRE, WATER,
        // GOD; endgame.md §2.2) and the pool product @90 (3·5·7 = all three pools).
        var original = Bytes("GUILD.pass4.dat");
        var slots = GuildCodec.ReadBinary(original, Data.CreationTables);
        var ch = slots[1]!;
        ch.Phantasie1.Runes = [1, 0, 1, 0, 1];
        ch.Phantasie1.Word90 = 105;
        var after = GuildCodec.WriteBinary(slots);
        var at = GuildCodec.RecordSize;
        Assert.Equal([at + 72, at + 76, at + 80, at + 90], Diff(original, after));
        Assert.Equal(105, after[at + 90] | after[at + 91] << 8);
        var back = GuildCodec.ReadBinary(after, Data.CreationTables)[1]!;
        Assert.Equal([1, 0, 1, 0, 1], back.Phantasie1.Runes);
        Assert.Equal(105, back.Phantasie1.Word90);
    }

    [OriginalDataTheory]
    [MemberData(nameof(GuildFiles))]
    public void Guild_CarrySurvivesSessionJson(string file)
    {
        var original = File.ReadAllBytes(Fixture(file));
        var slots = GuildCodec.ReadBinary(original, Data.CreationTables);
        var snap = new SessionSnapshot { RosterSlots = slots.ToArray() };
        var back = SessionCodec.ReadJson(SessionCodec.WriteJson(snap));
        Assert.Equal(Occupied(original), GuildCodec.WriteBinary(back.RosterSlots));
    }

    [OriginalDataTheory]
    [InlineData("SACK.s35.dat")]
    [InlineData("SACK.s36-after-wipes.dat")]
    public void Sack_ImportExport_IsByteIdentical(string file)
    {
        var original = Bytes(file);
        var sack = new LootSack();
        SackCodec.Read(original, sack);
        Assert.Equal(0, sack.Experience);
        Assert.Equal(0, sack.Gold);
        Assert.Equal(original, SackCodec.Write(sack.ItemIds));
    }

    [OriginalDataFact]
    public void Sack_S35_DecodesThirteenItems()
    {
        var sack = new LootSack();
        SackCodec.Read(Bytes("SACK.s35.dat"), sack);
        Assert.Equal(new[] { 128, 128, 48, 48, 46, 46, 46, 21, 21, 4, 4, 4, 4 }, sack.ItemIds);
    }

    [Fact]
    public void Sack_Holes_RoundTrip_AndAKeptItemFillsTheFirstHole()
    {
        // [D✓] s65 DOSBox: holes at slots 0, 1, 2, 7; keeping SM AXE (51) and LEATHER (23) gave [51, 23, 0, 100, …].
        int[] before = [0, 0, 0, 100, 61, 5, 25, 0, 101];
        var data = new byte[SackCodec.FileSize];
        for (var i = 0; i < before.Length; i++)
            data[2 * i] = (byte)before[i];
        var sack = new LootSack();
        SackCodec.Read(data, sack);
        Assert.Equal(before, sack.ItemIds);
        Assert.Equal(data, SackCodec.Write(sack.ItemIds));

        Assert.True(sack.TryAdd(51));
        Assert.True(sack.TryAdd(23));
        Assert.Equal(new[] { 51, 23, 0, 100, 61, 5, 25, 0, 101 }, sack.ItemIds);
        var written = SackCodec.Write(sack.ItemIds);
        Assert.Equal(new byte[] { 51, 0, 23, 0, 0, 0, 100, 0 }, written[..8]);
    }

    [Fact]
    public void Sack_SecondBeg_HitsTheSameEvenSlots()
    {
        // [D✓] s65 DOSBox: ids 1..60 → BEG → keep three → BEG again leaves [0, 2, 0, 4, …, 0, 48, 0, 50, 51..60].
        var sack = new LootSack();
        for (var id = 1; id <= 60; id++)
            sack.ItemIds.Add(id);
        sack.ForfeitToBeggars();
        Assert.True(sack.TryAdd(200));
        Assert.True(sack.TryAdd(201));
        Assert.True(sack.TryAdd(202));
        Assert.Equal(new[] { 200, 2, 201, 4, 202, 6 }, sack.ItemIds.Take(6));
        sack.ForfeitToBeggars();
        var expected = Enumerable.Range(1, 60).Select(id => id <= 49 && id % 2 == 1 ? 0 : id);
        Assert.Equal(expected, sack.ItemIds);
    }

    [OriginalDataTheory]
    [InlineData("TWNS.pass2.dat")]
    [InlineData("TWNS.s35.dat")]
    public void Towns_ImportExport_IsByteIdentical(string file)
    {
        var original = Bytes(file);
        var towns = TownsCodec.Read(original);
        Assert.Equal(original, TownsCodec.Write(towns));
    }

    [OriginalDataFact]
    public void Towns_Pass2_DecodesPelnorStockPriceAndNames()
    {
        var towns = TownsCodec.Read(Bytes("TWNS.pass2.dat"));
        var pelnor = towns[0];
        Assert.Equal((1, "PELNOR", 150), (pelnor.Id, pelnor.Name, pelnor.PriceIndex));
        // Pass 2 bought CLOTHING / DAGGER / MAGIC 1 / SCROLL 8: 5 → 4.
        Assert.Equal(new[] { 4, 4, 4, 4 }, new[] { 21, 46, 111, 128 }.Select(pelnor.GetStock));
        Assert.Equal("STARVILLE", towns[10].Name);
        Assert.Equal(Data.Towns.Select(t => (t.Id, t.Name, t.PriceIndex)), towns.Select(t => (t.Id, t.Name, t.PriceIndex)));
    }

    [OriginalDataFact]
    public void Towns_StockChange_TouchesOneByte_AndClampsAbove255()
    {
        var original = Bytes("TWNS.s35.dat");
        var towns = TownsCodec.Read(original);
        towns[2].SetStock(21, 7);
        var w = new List<DosSaveWarning>();
        var once = TownsCodec.Write(towns, w);
        Assert.Empty(w);
        Assert.Equal(new[] { 2 * TownsCodec.RecordSize + 21 }, Diff(original, once));

        towns[2].SetStock(21, 300);
        var clamped = TownsCodec.Write(towns, w);
        Assert.Equal(255, clamped[2 * TownsCodec.RecordSize + 21]);
        Assert.Contains(w, x => x.Kind == DosSaveWarningKind.Clamp && x.File == "TWNS.DAT");
    }

    // --- GUILD overlay ---------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Guild_BankChange_TouchesOnlyItsWord()
    {
        var original = Bytes("GUILD.pass4.dat");
        var slots = GuildCodec.ReadBinary(original, Data.CreationTables);
        slots[2]!.GoldInBank += 100;
        var offsets = Diff(original, GuildCodec.WriteBinary(slots));
        Assert.All(offsets, o => Assert.InRange(o, 2 * GuildCodec.RecordSize + 262, 2 * GuildCodec.RecordSize + 263));
        Assert.NotEmpty(offsets);
    }

    [OriginalDataFact]
    public void Guild_Lifespan_ImportedWordKept_UntilTheLifespanChanges()
    {
        var original = Bytes("GUILD.pass4.dat");
        var slots = GuildCodec.ReadBinary(original, Data.CreationTables);
        var wiz = slots[0]!;
        Assert.Equal(171, original[252] | original[253] << 8); // DOS elf lifespan
        Assert.NotEqual(171, GuildCodec.LifespanToDos(wiz.Lifespan)); // the D-003 band converts to something else
        Assert.Equal(original, GuildCodec.WriteBinary(slots));

        wiz.Lifespan += 512;
        var after = GuildCodec.WriteBinary(slots);
        Assert.Equal(GuildCodec.LifespanToDos(wiz.Lifespan), after[252] | after[253] << 8);
    }

    [OriginalDataFact]
    public void Guild_SoldItem_ClearsTheTrailingSlot()
    {
        var original = Bytes("GUILD.pass4.dat");
        var slots = GuildCodec.ReadBinary(original, Data.CreationTables);
        var wiz = slots[0]!;
        Assert.Equal(9, wiz.InventoryItemIds.Count);
        wiz.InventoryItemIds.RemoveAt(0);
        var after = GuildCodec.WriteBinary(slots);
        Assert.Equal(48, after[222]);
        Assert.Equal(0, after[238] | after[239] << 8);
    }

    [OriginalDataFact]
    public void Guild_RenamedCharacter_RewritesTheName_AndOddFlagsSurvive()
    {
        var original = Bytes("GUILD.pass4.dat");
        original[24] = 5;   // status word with no DOS name
        original[218] = 3;  // in-party word 3 (s9 gobbo)
        var w = new List<DosSaveWarning>();
        var import = DosSaveConverter.Import(new DosSaveSet { Guild = original }, Data);
        Assert.Contains(import.Warnings, x => x.Kind == DosSaveWarningKind.RuleBreak && x.Message.Contains("@24 = 5"));
        Assert.Contains(import.Warnings, x => x.Kind == DosSaveWarningKind.RuleBreak && x.Message.Contains("@218 = 3"));

        var slots = import.Snapshot.RosterSlots;
        Assert.Equal(original, GuildCodec.WriteBinary(slots, w));
        slots[0]!.Name = "merlin";
        var after = GuildCodec.WriteBinary(slots, w);
        Assert.Equal("merlin      ", System.Text.Encoding.ASCII.GetString(after, 270, 12));
        Assert.Equal(5, after[24]);
        Assert.Equal(3, after[218]);
        Assert.Empty(w);
    }

    [Fact]
    public void Guild_OutOfRange_IsClampedAndListed()
    {
        var ch = new Character { Name = "eldar", AgeClock = 78_000, GoldInBank = 70_000 };
        var w = new List<DosSaveWarning>();
        var bytes = GuildCodec.WriteBinary([ch], w);
        Assert.Equal(0xffff, bytes[264] | bytes[265] << 8);
        Assert.Equal(0xffff, bytes[262] | bytes[263] << 8);
        Assert.Contains(w, x => x.Kind == DosSaveWarningKind.Clamp && x.Message.Contains("@264"));
        Assert.Contains(w, x => x.Kind == DosSaveWarningKind.Clamp && x.Message.Contains("@262"));
    }

    // --- Full sets ---------------------------------------------------------------------------------------------

    public static TheoryData<string, string?, string?, string?, string?> Sets => new()
    {
        // pass 2 has no SACK.DAT fixture (exported as the empty sack, all zeros).
        { "GUILD.pass2.dat", null, "TWNS.pass2.dat", "DNG.pass2.sav", "DNGX.pass2.sav" },
        { "GUILD.s35-after-inn.dat", "SACK.s35.dat", "TWNS.s35.dat", "DNG.s35.sav", "DNGX.s35.sav" },
        { "GUILD.s35-dead.dat", "SACK.s35.dat", "TWNS.s35.dat", "DNG.s35.sav", "DNGX.s35.sav" },
        // s36 has no TWNS/DNG fixtures: towns from game data, dungeon slot empty (zeros).
        { "GUILD.s36-after-wipes.dat", "SACK.s36-after-wipes.dat", null, null, null },
    };

    [OriginalDataTheory]
    [MemberData(nameof(Sets))]
    public void Set_ImportThroughSessionAndJson_ExportsByteIdentical(
        string guild, string? sack, string? towns, string? dng, string? dngx)
    {
        var set = new DosSaveSet
        {
            Guild = Bytes(guild),
            Sack = sack is null ? null : Bytes(sack),
            Towns = towns is null ? null : Bytes(towns),
            DngSav = dng is null ? null : Bytes(dng),
            DngxSav = dngx is null ? null : Bytes(dngx),
        };

        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        var importWarnings = DosSaveConverter.ImportInto(session, set, Data);
        Assert.DoesNotContain(importWarnings, w => w.Kind == DosSaveWarningKind.Blocker);
        Assert.Equal(GameMode.Town, session.Mode);
        Assert.Equal(1, session.CurrentTownId);

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(2));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot())));

        var export = DosSaveConverter.ExportFrom(loaded, Data);
        Assert.False(export.Blocked, string.Join("; ", export.Warnings));
        Assert.DoesNotContain(export.Warnings, w => w.Kind == DosSaveWarningKind.Clamp);
        var files = export.Files!;
        Assert.Equal(Occupied(set.Guild), files.Guild);
        Assert.Equal(set.Sack ?? new byte[SackCodec.FileSize], files.Sack);
        if (set.Towns is not null)
            Assert.Equal(set.Towns, files.Towns);
        Assert.Equal(set.DngSav ?? new byte[DungeonSaveCodec.DngSavSize], files.DngSav);
        Assert.Equal(set.DngxSav ?? new byte[DungeonSaveCodec.DngxSavSize], files.DngxSav);
    }

    [OriginalDataFact]
    public void Import_S35_FormsThePartyInItsTown()
    {
        var import = DosSaveConverter.Import(new DosSaveSet
        {
            Guild = Bytes("GUILD.s35-after-inn.dat"),
            Sack = Bytes("SACK.s35.dat"),
            Towns = Bytes("TWNS.s35.dat"),
        }, Data);
        var snap = import.Snapshot;
        Assert.Equal(GameMode.Town, snap.Mode);
        var names = snap.PartySlotIndices.Select(i => snap.RosterSlots[i]!.Name).ToArray();
        // [C] Roster order (load loop OV1 0x866..0x89d), not the @260 stamps (which would give monko, rango first).
        Assert.Equal(new[] { "wizardo", "priesto", "fighto", "crusho", "monko", "rango" }, names);
        Assert.Equal(1, snap.CurrentTownId); // @254 = 0 = Pelnor (0-based record index [C])
        Assert.Equal(1, snap.RosterSlots[snap.PartySlotIndices[0]]!.CurrentTown); // codec: @254 + 1
        Assert.Equal(13, snap.Sack.ItemIds.Length);
        Assert.Equal(TownsCodec.TownCount, snap.Towns.Length);
        Assert.Contains(import.Warnings, w => w.Message.Contains("D-003"));
    }

    [OriginalDataFact]
    public void Import_EnterPelnor_Export_KeepsAt254Zero()
    {
        var set = new DosSaveSet { Guild = Bytes("GUILD.s35-after-inn.dat") };
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(9));
        DosSaveConverter.ImportInto(session, set, Data);
        session.EnterTown(1); // Pelnor: Aging writes town id 1 to every member
        var guild = DosSaveConverter.ExportFrom(session, Data).Files!.Guild!;
        foreach (var i in session.CaptureSnapshot().PartySlotIndices)
            Assert.Equal(0, guild[i * GuildCodec.RecordSize + 254] | guild[i * GuildCodec.RecordSize + 255] << 8);
    }

    [OriginalDataFact]
    public void NewGame_ExportsTheMasterDiskTowns()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(10));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Newbie", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.EnterTown(1);
        var r = DosSaveConverter.ExportFrom(session, Data);
        Assert.False(r.Blocked, string.Join("; ", r.Warnings));
        var master = Fixture("master/TWNS.DAT"); // original-files/TWNS.DAT, linked
        Assert.Equal(File.ReadAllBytes(master), r.Files!.Towns);
    }

    [OriginalDataFact]
    public void Import_Roster22_WarnsD002_KeepsAll()
    {
        var import = DosSaveConverter.Import(new DosSaveSet { Guild = File.ReadAllBytes(Fixture("GUILD.roster22.dat")) }, Data);
        Assert.Equal(22, import.Snapshot.RosterSlots.Count(c => c is not null));
        Assert.Contains(import.Warnings, w => w.Kind == DosSaveWarningKind.RuleBreak && w.Message.Contains("D-002"));
        // [C] OV1 0x7e7: nobody flagged → the Pelnor town menu with an empty party.
        Assert.Equal(GameMode.Town, import.Snapshot.Mode);
        Assert.Equal(1, import.Snapshot.CurrentTownId);
        Assert.Empty(import.Snapshot.PartySlotIndices);
    }

    [OriginalDataFact]
    public void Import_NoParty_OpensPelnorWithAnEmptyParty_AndExitIsRefused()
    {
        var guild = Bytes("GUILD.s35-after-inn.dat");
        for (var i = 0; i < GuildCodec.SlotCount; i++)
            guild[i * GuildCodec.RecordSize + 218] = guild[i * GuildCodec.RecordSize + 219] = 0;
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(4));
        DosSaveConverter.ImportInto(session, new DosSaveSet { Guild = guild, Sack = Bytes("SACK.s35.dat") }, Data);
        Assert.Equal(GameMode.Town, session.Mode);
        Assert.Equal(1, session.CurrentTownId);
        Assert.Empty(session.Party.Members);
        Assert.False(session.LeaveTown()); // OV1 0x3250: no party, no Exit
        // The town bar with nobody to act (the sack still holds the s35 items): nothing throws.
        session.RestAtInn();
        session.ConsultMystic();
        session.DistributeLoot(items: false);
        session.BeginItemDistribution();
        session.OpenGuild();
        session.LeaveGuild(); // the Guild is in Pelnor
        Assert.Equal(GameMode.Town, session.Mode);
    }

    [OriginalDataFact]
    public void Import_PartyTown_IsTheFirstFlaggedRosterRecord()
    {
        // [C] the party's town is @254 of party slot 0 = the first @218 record in roster order.
        var guild = Bytes("GUILD.s35-after-inn.dat");
        var import = DosSaveConverter.Import(new DosSaveSet { Guild = guild }, Data);
        var first = import.Snapshot.PartySlotIndices[0];
        var second = import.Snapshot.PartySlotIndices[1];
        guild[first * GuildCodec.RecordSize + 254] = 4;  // town index 4 → Core town 5
        guild[second * GuildCodec.RecordSize + 254] = 7; // another member elsewhere: listed, not used
        var r = DosSaveConverter.Import(new DosSaveSet { Guild = guild }, Data);
        Assert.Equal(5, r.Snapshot.CurrentTownId);
        Assert.Contains(r.Warnings, w => w.Kind == DosSaveWarningKind.RuleBreak && w.Message.Contains("different towns"));
    }

    [OriginalDataTheory]
    [InlineData(1)]
    [InlineData(12)] // OLYMPIA: no tile-40 feature carries arg 12 (OUT18's arg 99 does) — the old lookup missed it
    [InlineData(11)]
    public void Import_WildernessPosition_IsTheTownsTwnsExit(int townId)
    {
        // [C] TWNS +0x111/+0x113/+0x115 (town Exit, OV1 0x3309..0x331a).
        var guild = Bytes("GUILD.s35-after-inn.dat");
        for (var i = 0; i < GuildCodec.SlotCount; i++)
            guild[i * GuildCodec.RecordSize + 254] = (byte)(townId - 1);
        var twns = Bytes("TWNS.s35.dat");
        var r = DosSaveConverter.Import(new DosSaveSet { Guild = guild, Towns = twns }, Data);
        var o = (townId - 1) * TownsCodec.RecordSize;
        int U16(int at) => twns[o + at] | twns[o + at + 1] << 8;
        Assert.Equal(townId, r.Snapshot.CurrentTownId);
        Assert.Equal((U16(0x111), U16(0x113), U16(0x115)),
            (r.Snapshot.OverworldRegionId, r.Snapshot.OverworldX, r.Snapshot.OverworldY));
        Assert.NotEqual(0, r.Snapshot.OverworldRegionId);
        Assert.DoesNotContain(r.Warnings, w => w.Message.Contains("not found"));

        // Without TWNS.DAT the master-disk record in the game data gives the same static words.
        var noTowns = DosSaveConverter.Import(new DosSaveSet { Guild = guild }, Data).Snapshot;
        Assert.Equal((r.Snapshot.OverworldRegionId, r.Snapshot.OverworldX, r.Snapshot.OverworldY),
            (noTowns.OverworldRegionId, noTowns.OverworldX, noTowns.OverworldY));
    }

    [OriginalDataFact]
    public void Creation_SetsTheCreatingGuildsTown_AndExportsIt()
    {
        // [C] OV1 0xdfb: creation writes the Guild's town into @254.
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(11));
        session.StartNewGame();
        session.EnterTown(2);
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Towner", Race.Human, CharacterClass.Fighter);
        Assert.Equal(2, hero.CurrentTown);
        Assert.False(session.AddNeedsMoveConfirm(hero)); // created here: no "Not in the town" gate
        Assert.True(session.TryAddToParty(hero, out _));
        session.LeaveGuild();
        var slot = Array.IndexOf(session.Roster.Slots, hero);
        var export = DosSaveConverter.ExportFrom(session, Data);
        Assert.False(export.Blocked, string.Join("; ", export.Warnings));
        var guild = export.Files!.Guild!;
        Assert.Equal(1, guild[slot * GuildCodec.RecordSize + 254]);
    }

    [OriginalDataFact]
    public void Import_BankOver65000_IsKept_WithARuleBreak_D048()
    {
        var guild = Bytes("GUILD.s35-after-inn.dat");
        var slot = Enumerable.Range(0, 22).First(i => guild[i * 282] != 0);
        guild[slot * 282 + 262] = 0xff; // @262 = 65535
        guild[slot * 282 + 263] = 0xff;
        var r = DosSaveConverter.Import(new DosSaveSet { Guild = guild }, Data);
        Assert.Equal(65_535, r.Snapshot.RosterSlots[slot]!.GoldInBank);
        Assert.Contains(r.Warnings, w => w.Kind == DosSaveWarningKind.RuleBreak && w.Message.Contains("D-048"));
    }

    // --- Export blockers ---------------------------------------------------------------------------------------

    private static SessionSnapshot TownSnapshot() =>
        DosSaveConverter.Import(new DosSaveSet { Guild = Bytes("GUILD.s35-after-inn.dat") }, Data).Snapshot;

    [OriginalDataFact]
    public void Export_OutsideTown_IsBlocked()
    {
        var snap = TownSnapshot();
        snap.Mode = GameMode.Overworld;
        snap.CurrentTownId = null;
        var r = DosSaveConverter.Export(snap, Data.CreateTownState);
        Assert.True(r.Blocked);
        Assert.Contains(r.Warnings, w => w.Kind == DosSaveWarningKind.Blocker && w.Message.Contains("town"));
    }

    [OriginalDataFact]
    public void Export_SackOver60_IsBlocked_NothingDropped()
    {
        var snap = TownSnapshot();
        snap.Sack = new LootSackSnapshot { ItemIds = Enumerable.Repeat(21, 61).ToArray() };
        var r = DosSaveConverter.Export(snap, Data.CreateTownState);
        Assert.True(r.Blocked);
        Assert.Contains(r.Warnings, w => w.Kind == DosSaveWarningKind.Blocker && w.Message.Contains("61 items"));

        snap.Sack = new LootSackSnapshot { ItemIds = Enumerable.Repeat(21, 60).ToArray() };
        Assert.False(DosSaveConverter.Export(snap, Data.CreateTownState).Blocked);
    }

    [OriginalDataFact]
    public void Export_UnsplitPool_AndCarriedGold_AreSplitAutomatically()
    {
        // User s40 (P1.4 decision 6): no blocker — the export pays the pool out and banks the purse, on copies.
        var snap = TownSnapshot();
        var members = snap.PartySlotIndices.Select(i => snap.RosterSlots[i]!).ToList();
        var living = members.Where(m => m.Status == CharacterStatus.Ok).ToList();
        Assert.NotEmpty(living);
        var bankBefore = living.Select(m => m.GoldInBank).ToList();
        var xpBefore = living.Select(m => m.Experience).ToList();
        var n = living.Count;
        snap.Sack = new LootSackSnapshot { Experience = 40L * n + 1, Gold = 12 };
        snap.PartyGold = 5;

        var r = DosSaveConverter.Export(snap, Data.CreateTownState);

        Assert.False(r.Blocked);
        Assert.Contains(r.Warnings, w => w.Kind == DosSaveWarningKind.Note && w.Message.Contains("split for the save"));
        var back = DosSaveConverter.Import(r.Files!, Data).Snapshot;
        var total = 17L;
        for (var k = 0; k < n; k++)
        {
            var slot = Array.IndexOf(snap.RosterSlots, living[k]);
            var exported = back.RosterSlots[slot]!;
            Assert.Equal(xpBefore[k] + 40 + (k == 0 ? 1 : 0), exported.Experience);
            Assert.Equal(bankBefore[k] + (total + n - 1) / n, exported.GoldInBank); // rounded up, same for all (user s80)
        }
        // The snapshot itself is untouched.
        Assert.Equal(5, snap.PartyGold);
        Assert.Equal(bankBefore, living.Select(m => m.GoldInBank).ToList());
    }

    [OriginalDataTheory]
    [InlineData(true)]  // passed in
    [InlineData(false)] // read from the snapshot's locked rules
    public void Export_AutoSplit_FollowsPayoutRemainder_Dropped_Q2(bool explicitFlag)
    {
        // p1.payout-remainder = dropped (Original (DOS) preset, D-037 / Q2): the fight payout loses its remainders.
        var snap = TownSnapshot();
        snap.Options = GameOptionsSnapshot.From(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
        var living = snap.PartySlotIndices.Select(i => snap.RosterSlots[i]!).Where(m => m.Status == CharacterStatus.Ok).ToList();
        var n = living.Count;
        Assert.True(n > 1);
        var bankBefore = living.Select(m => m.GoldInBank).ToList();
        var xpBefore = living.Select(m => m.Experience).ToList();
        snap.Sack = new LootSackSnapshot { Experience = 40L * n + 1, Gold = 10L * n + 1 };

        var r = explicitFlag
            ? DosSaveConverter.Export(snap, Data.CreateTownState, dropRemainder: true)
            : DosSaveConverter.Export(snap, Data.CreateTownState);

        Assert.False(r.Blocked);
        var back = DosSaveConverter.Import(r.Files!, Data).Snapshot;
        for (var k = 0; k < n; k++)
        {
            var exported = back.RosterSlots[Array.IndexOf(snap.RosterSlots, living[k])]!;
            Assert.Equal(xpBefore[k] + 40, exported.Experience); // no remainder to the first survivor
            Assert.Equal(bankBefore[k] + 10, exported.GoldInBank);
        }
    }

    [OriginalDataFact]
    public void Export_AutoSplit_ExplicitFirstSurvivor_OverridesTheSnapshotRules()
    {
        var snap = TownSnapshot();
        snap.Options = GameOptionsSnapshot.From(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
        var first = snap.RosterSlots[snap.PartySlotIndices.First(i => snap.RosterSlots[i]!.Status == CharacterStatus.Ok)]!;
        var n = snap.PartySlotIndices.Count(i => snap.RosterSlots[i]!.Status == CharacterStatus.Ok);
        var xpBefore = first.Experience;
        snap.Sack = new LootSackSnapshot { Experience = 40L * n + 1 };
        var r = DosSaveConverter.Export(snap, Data.CreateTownState, dropRemainder: false);
        var back = DosSaveConverter.Import(r.Files!, Data).Snapshot;
        Assert.Equal(xpBefore + 41, back.RosterSlots[Array.IndexOf(snap.RosterSlots, first)]!.Experience);
    }

    [OriginalDataFact]
    public void Export_UnvisitedTowns_ComeFromGameData_WithANote()
    {
        var r = DosSaveConverter.Export(TownSnapshot(), Data.CreateTownState);
        Assert.False(r.Blocked);
        Assert.Equal(TownsCodec.FileSize, r.Files!.Towns!.Length);
        Assert.Equal(TownsCodec.TownCount, r.Warnings.Count(w => w.File == "TWNS.DAT" && w.Kind == DosSaveWarningKind.Note));
    }

    [OriginalDataFact]
    public void WriteFolder_NeverOverwritesWithoutConfirmation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "p14-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = DosSaveConverter.Export(TownSnapshot(), Data.CreateTownState).Files!;
            files.WriteFolder(dir);
            Assert.Equal(5, DosSaveSet.ExistingFiles(dir).Count);
            Assert.Throws<IOException>(() => files.WriteFolder(dir));
            files.WriteFolder(dir, overwrite: true);
            var back = DosSaveSet.ReadFolder(dir);
            Assert.Equal(files.Guild, back.Guild);
            Assert.Equal(files.DngxSav, back.DngxSav);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    // --- Session: town state kept between visits and saves ---------------------------------------------------

    [OriginalDataFact]
    public void Session_TownStock_PersistsAcrossVisitsAndJson()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        session.EnterTown(1);
        // id 128 (SCROLL 8): the town-entry restock (s41, ArmoryRestock) only wipes ids 0..119, so this survives re-entry.
        session.CurrentTown!.SetStock(128, 1);
        session.EnterTown(2);
        session.EnterTown(1);
        Assert.Equal(1, session.CurrentTown!.GetStock(128));

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(4));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot())));
        Assert.Equal(1, loaded.CurrentTown!.GetStock(128));
        Assert.Equal(new[] { 1, 2 }, loaded.Towns.Keys.OrderBy(k => k));
    }

    // --- P0.5: per-round PARTY OPTIONS flags on load ----------------------------------------------------------

    private static GameSession FightingSession(int seed)
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Porter", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(Data.Monsters.First(m => m.HpBase <= 8).Id);
        return session;
    }

    [OriginalDataFact]
    public void ApplySnapshot_ResetsRoundFlags_FromThePreviousGame()
    {
        var fresh = FightingSession(5).CaptureSnapshot(); // taken before this round's morale check
        var session = FightingSession(6);
        Assert.Equal(MoraleOutcome.Stand, session.StartCombatRound());
        Assert.True(session.CombatMoraleCheckedThisRound);

        session.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(fresh)));
        Assert.False(session.CombatMoraleCheckedThisRound);
        Assert.False(session.CombatOptionChosenThisRound);
    }

    [OriginalDataFact]
    public void ApplySnapshot_KeepsRoundFlags_SavedAfterTheMoraleCheck()
    {
        var session = FightingSession(7);
        Assert.Equal(MoraleOutcome.Stand, session.StartCombatRound());
        var json = SessionCodec.WriteJson(session.CaptureSnapshot());

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(8));
        loaded.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.True(loaded.CombatMoraleCheckedThisRound);
        Assert.False(loaded.CombatOptionChosenThisRound);
    }

    private static List<int> Diff(byte[] a, byte[] b)
    {
        Assert.Equal(a.Length, b.Length);
        return Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
    }
}
