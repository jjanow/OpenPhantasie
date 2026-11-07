using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class GuildCodecTests
{
    private static CharacterCreationTables Tables => GameDataRepository.LoadPhantasie1().CreationTables;

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [OriginalDataFact]
    public void ReadBinary_3CharsFixture_ParsesKnownRoster()
    {
        var slots = GuildCodec.ReadBinaryFile(Fixture("GUILD.3chars.dat"), Tables);
        var filled = slots.Where(c => c is not null).Cast<Character>().ToList();
        Assert.Equal(3, filled.Count);

        var wiz = Assert.Single(filled, c => c.Name == "wizardo");
        Assert.Equal(Race.Elf, wiz.Race);
        Assert.Equal(CharacterClass.Wizard, wiz.Class);
        Assert.Equal(3, wiz.Stats.Str);
        Assert.Equal(17, wiz.Stats.Int);
        Assert.Equal(4, wiz.HpMax);
        Assert.Equal(7, wiz.MagicMax);
        Assert.Equal(256, wiz.GoldInBank);
        Assert.Equal(1, wiz.SpellsKnown[4]); // FIREFLASH I

        var priest = Assert.Single(filled, c => c.Name == "priesto");
        Assert.Equal(1, priest.SpellsKnown[0]); // HEALING I

        var thief = Assert.Single(filled, c => c.Name == "thiefo");
        Assert.Equal(Race.LizardMan, thief.Race);
        Assert.Equal(38, thief.Skills.Attack);
        Assert.Equal(40, thief.Skills.Parry);
    }

    [OriginalDataFact]
    public void DivineI_IsDosSlot57_At210_ReadAndWrite()
    {
        var data = File.ReadAllBytes(Fixture("GUILD.3chars.dat"));
        var slots = GuildCodec.ReadBinary(data, Tables);
        var i = slots.ToList().FindIndex(c => c?.Name == "wizardo");
        var rec = i * GuildCodec.RecordSize;

        Assert.Equal(210, GuildCodec.SpellFlagOffset(SpellId.DivineI));
        // DOS record: @210 = 1 (endgame grant, OV1), @204 (empty slot 54) = 7 as a marker.
        data[rec + 210] = 1; data[rec + 211] = 0;
        data[rec + 204] = 7; data[rec + 205] = 0;
        var wiz = GuildCodec.ReadBinary(data, Tables)[i]!;
        Assert.Equal(1, wiz.SpellsKnown[SpellId.DivineI]);

        // Export: @210 follows the remaster flag; the empty slot word @204 keeps the imported bytes.
        wiz.SpellsKnown[SpellId.DivineI] = 0;
        var outBytes = GuildCodec.WriteBinary(new[] { wiz });
        Assert.Equal(0, outBytes[210] | (outBytes[211] << 8));
        Assert.Equal(7, outBytes[204]);

        // A fresh character (no raw record) granted DIVINE I lands at @210, not @204.
        var fresh = new Character { Name = "Victor", SpellsKnown = new int[SpellId.Count] };
        fresh.SpellsKnown[SpellId.DivineI] = 1;
        var freshBytes = GuildCodec.WriteBinary(new[] { fresh });
        Assert.Equal(1, freshBytes[210]);
        Assert.Equal(0, freshBytes[204]);
    }

    [OriginalDataFact]
    public void BinaryRoundTrip_Preserves3CharsFixture()
    {
        var original = File.ReadAllBytes(Fixture("GUILD.3chars.dat"));
        var slots = GuildCodec.ReadBinary(original, Tables);
        var rewritten = GuildCodec.WriteBinary(slots);

        // Compare only occupied record bytes that we encode (name + confirmed fields).
        for (var i = 0; i < 3; i++)
        {
            var a = original.AsSpan(i * GuildCodec.RecordSize, GuildCodec.RecordSize);
            var b = rewritten.AsSpan(i * GuildCodec.RecordSize, GuildCodec.RecordSize);
            var ch = slots[i]!;

            Assert.Equal(ch.Stats.Str, b[0] | (b[1] << 8));
            Assert.Equal(ch.Name.PadRight(12)[..12],
                System.Text.Encoding.ASCII.GetString(b.Slice(270, 12)).TrimEnd('\0', ' ').PadRight(12)[..12]);
            Assert.Equal((byte)ch.Race, b[216]);
            Assert.Equal((byte)ch.Class, b[220]);
            Assert.Equal(ch.Skills.Attack, b[30] | (b[31] << 8));
            Assert.Equal(ch.GoldInBank, b[262] | (b[263] << 8));
            Assert.True(a.SequenceEqual(b) || FieldsMatch(ch, b),
                $"slot {i} round-trip field mismatch");
        }
    }

    [OriginalDataFact]
    public void JsonRoundTrip_PreservesCreatedCharacter()
    {
        var created = CharacterCreation.Create(
            new CharacterCreationRequest
            {
                Name = "jsono",
                Race = Race.Human,
                Class = CharacterClass.Ranger,
            },
            GameDataRepository.LoadPhantasie1().CreationTables,
            new SeededDosRandomSource(7));

        var json = GuildCodec.WriteJson([created]);
        var loaded = Assert.Single(GuildCodec.ReadJson(json));
        Assert.Equal(created.Name, loaded.Name);
        Assert.Equal(created.Race, loaded.Race);
        Assert.Equal(created.Class, loaded.Class);
        Assert.Equal(created.Stats, loaded.Stats);
        Assert.Equal(created.Skills, loaded.Skills);
        Assert.Equal(created.SpellsKnown, loaded.SpellsKnown);
        Assert.Equal(created.GoldInBank, loaded.GoldInBank);
    }

    [Fact]
    public void ReadJson_PreS19Save_ZeroesWord28AndKeepsWord26()
    {
        // User decision s19: a legacy "scrollCountOrWeaponHit" is not carried into @28 (weapon damage A).
        var json = GuildCodec.WriteJson([new Character { Name = "old", WeaponDamageA = 3 }])
            .Replace("\"weaponDamage28\": 3", "\"scrollCountOrWeaponHit\": 4")
            .Replace("\"name\": \"old\"", "\"name\": \"old\", \"weaponDamageA\": 7");
        Assert.Contains("scrollCountOrWeaponHit", json);

        var loaded = Assert.Single(GuildCodec.ReadJson(json));
        Assert.Equal(0, loaded.WeaponDamageA);
        Assert.Equal(7, loaded.Phantasie1.Word26);

        var current = Assert.Single(GuildCodec.ReadJson(
            GuildCodec.WriteJson([new Character { Name = "new", WeaponDamageA = 3 }])));
        Assert.Equal(3, current.WeaponDamageA);
    }

    [OriginalDataFact]
    public void Pass4Fixture_LoadsSixCharactersWithScore()
    {
        var slots = GuildCodec.ReadBinaryFile(Fixture("GUILD.pass4.dat"), Tables);
        var filled = slots.Where(c => c is not null).Cast<Character>().ToList();
        Assert.True(filled.Count >= 6);
        Assert.Contains(filled, c => c.Name == "wizardo" && c.Score == 2);
    }

    [OriginalDataFact]
    public void Pass4Fixture_DecodesOrdinalAndPartyOrderStamp()
    {
        // [C] Phase 8 P0.1: @92 = 1-based creation ordinal; @260 = 2 × ordinal + 10
        // once the character joins an active party (static, not the age clock).
        var slots = GuildCodec.ReadBinaryFile(Fixture("GUILD.pass4.dat"), Tables);
        var order = slots.Where(c => c is not null).Cast<Character>()
            .ToDictionary(c => c.Name);

        var expected = new (string Name, int Ordinal, int Stamp)[]
        {
            ("wizardo", 1, 12), ("priesto", 2, 14), ("thiefo", 3, 16),
            ("fighto", 4, 18), ("crusho", 5, 20), ("gobbo", 6, 22),
        };
        foreach (var (name, ordinal, stamp) in expected)
        {
            Assert.Equal(ordinal, order[name].RosterOrdinal);
            Assert.Equal(stamp, order[name].Phantasie1.PartyOrderStamp);
            Assert.Equal(2 * ordinal + 10, order[name].Phantasie1.PartyOrderStamp);
            Assert.Equal(CharacterStatus.Ok, order[name].Status);
        }
    }

    [OriginalDataFact]
    public void ReadBinary_Pass4_CombatFieldsAndNineItemSlots()
    {
        // s19: record base 0x5bb0 → weapon B @240, shield @242, armor @244, damage A @28,
        // carried items @222..@238 (worn included); scroll count comes from the items.
        var slots = GuildCodec.ReadBinaryFile(Fixture("GUILD.pass4.dat"), Tables);
        var wizardo = slots.First(c => c?.Name.Trim() == "wizardo")!;
        Assert.Equal(new[] { 48, 48, 46, 46, 46, 22, 21, 4, 128 }, wizardo.InventoryItemIds);
        Assert.Equal(1, wizardo.ScrollCount);
        Assert.Equal(0, wizardo.WeaponDamageB); // no weapon worn
        Assert.Equal(1, wizardo.ArmorSoak);     // CLOTHING

        var thiefo = slots.First(c => c?.Name.Trim() == "thiefo")!;
        Assert.Equal(new[] { 48, 22, 4 }, thiefo.InventoryItemIds);
        Assert.Equal(0, thiefo.ScrollCount);
        Assert.Equal(2, thiefo.WeaponDamageA);
        Assert.Equal(4, thiefo.WeaponDamageB);
        Assert.Equal(4, thiefo.ShieldSoak);
        Assert.Equal(2, thiefo.ArmorSoak);
        Assert.Equal(35, thiefo.Word12); // defense rating

        var crusho = slots.First(c => c?.Name.Trim() == "crusho")!;
        Assert.Equal(1, crusho.MagicPotions); // MAGIC 1 = id 111
    }

    [OriginalDataFact]
    public void DrunkPotion_GapIsExportedInPlace_AndReimported()
    {
        // s58: the drunk potion's slot word @222 + 2k is written as 0; later slots keep their words.
        var original = File.ReadAllBytes(Fixture("GUILD.pass4.dat"));
        var slots = GuildCodec.ReadBinary(original, Tables).ToArray();
        var crusho = slots.First(c => c?.Name.Trim() == "crusho")!;
        var k = crusho.InventoryItemIds.IndexOf(111);
        var before = crusho.InventoryItemIds.ToArray();
        crusho.MagicCur = 0;
        Assert.True(Magic.UsePotion(crusho, crusho, k).Success);

        var rec = new byte[GuildCodec.RecordSize];
        GuildCodec.WriteRecord(crusho, rec);
        for (var i = 0; i < Character.InventorySlots; i++)
        {
            var expected = i == k ? 0 : i < before.Length ? before[i] : 0;
            Assert.Equal(expected, rec[222 + 2 * i] | (rec[223 + 2 * i] << 8));
        }

        var back = GuildCodec.ReadBinary(GuildCodec.WriteBinary(slots), Tables).First(c => c?.Name.Trim() == "crusho")!;
        Assert.Equal(crusho.InventoryItemIds, back.InventoryItemIds);
    }

    [OriginalDataFact]
    public void InteriorEmptySlots_RoundTripByteIdentical()
    {
        var ch = new Character { Name = "gappy" };
        ch.InventoryItemIds.AddRange(new[] { 48, 0, 22, 0, 0, 4 });
        var file = GuildCodec.WriteBinary([ch]);
        var back = GuildCodec.ReadBinary(file, Tables).OfType<Character>().Single();
        Assert.Equal(new[] { 48, 0, 22, 0, 0, 4 }, back.InventoryItemIds);
        Assert.Equal(file, GuildCodec.WriteBinary([back]));
    }

    [Fact]
    public void EquipmentCounters_CountItemIdRanges_Com2cf6()
    {
        // COM 0x2cf6: 101–110 HEALING, 111–120 MAGIC, 121–140 SCROLL, 151+ treasure; 141–150 rings are neither.
        var ch = new Character();
        ch.InventoryItemIds.AddRange(new[] { 100, 101, 110, 111, 120, 121, 140, 141, 150, 151, 160 });
        Assert.Equal(2, ch.HealingPotions);
        Assert.Equal(2, ch.MagicPotions);
        Assert.Equal(2, ch.ScrollCount);
        Assert.Equal(2, ch.Treasures);
    }

    [OriginalDataFact]
    public void BinaryRoundTrip_Pass4_PreservesOrdinalStampRaceEchoAndStatus()
    {
        // @92 ordinal, @256 displayed race, @260 party-order stamp, @24 status,
        // @88 level and @218 in-party flag all survive a decode→encode cycle.
        var original = File.ReadAllBytes(Fixture("GUILD.pass4.dat"));
        var slots = GuildCodec.ReadBinary(original, Tables);
        var rewritten = GuildCodec.WriteBinary(slots);

        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] is null)
                continue;
            var a = original.AsSpan(i * GuildCodec.RecordSize, GuildCodec.RecordSize);
            var b = rewritten.AsSpan(i * GuildCodec.RecordSize, GuildCodec.RecordSize);

            Assert.Equal(a[92] | (a[93] << 8), b[92] | (b[93] << 8));   // @92 ordinal
            Assert.Equal(a[216], b[216]);                              // @216 race
            Assert.Equal(a[256] | (a[257] << 8), b[256] | (b[257] << 8)); // @256 race echo
            Assert.Equal(a[216], b[256]);                              // echo == race
            Assert.Equal(a[260] | (a[261] << 8), b[260] | (b[261] << 8)); // @260 stamp
            Assert.Equal(a[218], b[218]);                              // @218 in-party
            Assert.Equal(a[24] | (a[25] << 8), b[24] | (b[25] << 8));   // @24 status
            Assert.Equal(a[88] | (a[89] << 8), b[88] | (b[89] << 8));   // @88 level
        }
    }

    [OriginalDataFact]
    public void H3StatusFixture_DecodesStatusLevelAndUndeadRace()
    {
        // [C] PLAN H3 (s10): pass-4 save byte-edited, then checked in DOSBox Guild→Inspect
        // and the sheet: @24=0 → DEAD, @24=2 → SLEEP, @88=7 → "LEVEL 7",
        // @256=15 → race UNDEAD (status OK). Screenshots: working/snapshots/h3-status-s10/.
        var order = GuildCodec.ReadBinaryFile(Fixture("GUILD.h3status.dat"), Tables)
            .Where(c => c is not null).Cast<Character>().ToDictionary(c => c.Name);

        Assert.Equal(CharacterStatus.Dead, order["wizardo"].Status);
        Assert.Equal(CharacterStatus.Sleep, order["priesto"].Status);
        Assert.Equal(CharacterStatus.Ok, order["thiefo"].Status);
        Assert.Equal(7, order["thiefo"].Level);
        Assert.Equal(Race.Undead, order["fighto"].Race);     // @256 = 15
        Assert.Equal(Race.Dwarf, order["fighto"].BirthRace); // @216 keeps the birth race
        Assert.Equal(CharacterStatus.Ok, order["fighto"].Status);
        Assert.Equal(1, order["fighto"].Level);
    }

    [OriginalDataFact]
    public void H3StatusFixture_BinaryRoundTripIsByteExactOnStatusFields()
    {
        var original = File.ReadAllBytes(Fixture("GUILD.h3status.dat"));
        var rewritten = GuildCodec.WriteBinary(GuildCodec.ReadBinary(original, Tables));

        for (var i = 0; i < 6; i++)
        {
            foreach (var off in new[] { 24, 88, 218, 256 })
            {
                var at = i * GuildCodec.RecordSize + off;
                Assert.Equal(original[at] | (original[at + 1] << 8), rewritten[at] | (rewritten[at + 1] << 8));
            }
        }
    }

    [OriginalDataFact]
    public void UndeadThatDies_KeepsUndeadRaceThroughRoundTrip()
    {
        // H4 (s10): UNDEAD is a race, independent of status — a dead undead stays undead.
        var fighto = GuildCodec.ReadBinaryFile(Fixture("GUILD.h3status.dat"), Tables)
            .First(c => c?.Name == "fighto")!;
        fighto.Status = CharacterStatus.Dead;

        var bytes = GuildCodec.WriteBinary([fighto]);
        Assert.Equal(0, bytes[24]);   // @24 DEAD
        Assert.Equal(15, bytes[256]); // @256 UNDEAD
        Assert.Equal((byte)Race.Dwarf, bytes[216]);

        var back = GuildCodec.ReadBinary(bytes, Tables)[0]!;
        Assert.Equal(Race.Undead, back.Race);
        Assert.Equal(CharacterStatus.Dead, back.Status);

        var json = Assert.Single(GuildCodec.ReadJson(GuildCodec.WriteJson([fighto])));
        Assert.Equal(Race.Undead, json.Race);
        Assert.Equal(Race.Dwarf, json.BirthRace);
    }

    [OriginalDataFact]
    public void Roster22Fixture_DroppedMembersStayOkAndLevelOne()
    {
        // s9 save: all 22 characters dropped from the party, so @218 (in-party flag) = 0
        // everywhere. Before H3 the codec read @218 as status and returned Empty for all.
        var filled = GuildCodec.ReadBinaryFile(Fixture("GUILD.roster22.dat"), Tables)
            .Where(c => c is not null).Cast<Character>().ToList();

        Assert.Equal(22, filled.Count);
        Assert.All(filled, c =>
        {
            Assert.Equal(CharacterStatus.Ok, c.Status);
            Assert.Equal(1, c.Level);
            Assert.False(c.InParty);
        });
    }

    [OriginalDataFact]
    public void Pass4Fixture_PartyMembersAreFlaggedInParty()
    {
        var filled = GuildCodec.ReadBinaryFile(Fixture("GUILD.pass4.dat"), Tables)
            .Where(c => c is not null).Cast<Character>().ToList();
        Assert.All(filled, c => Assert.True(c.InParty));
    }

    [OriginalDataFact]
    public void JsonRoundTrip_PreservesOrdinalAndPartyOrderStamp()
    {
        var wizardo = GuildCodec.ReadBinaryFile(Fixture("GUILD.pass4.dat"), Tables)
            .First(c => c?.Name == "wizardo")!;
        Assert.Equal(1, wizardo.RosterOrdinal);
        Assert.Equal(12, wizardo.Phantasie1.PartyOrderStamp);

        var json = GuildCodec.WriteJson([wizardo]);
        var loaded = Assert.Single(GuildCodec.ReadJson(json));
        Assert.Equal(wizardo.RosterOrdinal, loaded.RosterOrdinal);
        Assert.Equal(wizardo.Phantasie1.PartyOrderStamp, loaded.Phantasie1.PartyOrderStamp);
    }

    [OriginalDataFact]
    public void ParseRecord_SyntheticRecord_ReadsS11RelabelledFields()
    {
        // Fixtures can't tell @16 from @18 (equal everywhere), so build a record where they differ.
        var rec = new byte[GuildCodec.RecordSize];
        void W(int off, int v) { rec[off] = (byte)v; rec[off + 1] = (byte)(v >> 8); }
        System.Text.Encoding.ASCII.GetBytes("synth").CopyTo(rec, 270);
        W(12, 33);   // @12 seventh stat word
        W(14, 10);   // MP ceiling
        W(16, 9);    // MP max
        W(18, 3);    // MP current
        W(24, 1);
        W(92, 1);    // occupied
        for (var i = 0; i < 10; i++) W(50 + 2 * i, 40 + i);
        W(94, 1);    // cast since rest
        W(252, 12);  // DOS lifespan steps
        W(254, 3);   // current town
        W(258, 1);   // has aged
        W(264, 571); // age clock

        var ch = GuildCodec.ParseRecord(rec, Tables)!;
        Assert.Equal(10, ch.MpCeiling);
        Assert.Equal(9, ch.MagicMax);
        Assert.Equal(3, ch.MagicCur);
        Assert.Equal(33, ch.Word12);
        Assert.Equal(Enumerable.Range(40, 10), ch.SkillBonuses);
        Assert.True(ch.CastSinceRest);
        Assert.Equal(72 * 26, ch.Lifespan); // DOS @252 = 12 ignored: Human band, @10 = 0 clamped to 8 (D-003, user s17)
        Assert.Equal(4, ch.CurrentTown); // @254 = 3 is the 0-based record index → town id 4 [~] (s39)
        Assert.True(ch.HasAged);
        Assert.Equal(571, ch.AgeClock);
        Assert.Equal(21, ch.AgeYears);

        var back = new byte[GuildCodec.RecordSize];
        GuildCodec.WriteRecord(ch, back);
        foreach (var off in new[] { 12, 14, 16, 18, 50, 68, 94, 254, 258, 264 })
            Assert.Equal(rec[off] | (rec[off + 1] << 8), back[off] | (back[off + 1] << 8));
    }

    [OriginalDataFact]
    public void Fixtures_SkillBonuses_MatchCreationSeedFormula()
    {
        foreach (var name in new[] { "GUILD.pass4.dat", "GUILD.roster22.dat" })
        {
            foreach (var ch in GuildCodec.ReadBinaryFile(Fixture(name), Tables).OfType<Character>())
                Assert.Equal(CharacterCreation.SkillBonusSeeds(ch.Stats), ch.SkillBonuses);
        }
    }

    [OriginalDataFact]
    public void JsonRoundTrip_PreservesNewFields_AndReadsPreS17Keys()
    {
        var ch = GuildCodec.ReadBinaryFile(Fixture("GUILD.pass4.dat"), Tables).First(c => c?.Name == "priesto")!;
        ch.CastSinceRest = true;
        ch.HasAged = true;
        ch.CurrentTown = 2;
        var loaded = Assert.Single(GuildCodec.ReadJson(GuildCodec.WriteJson([ch])));
        Assert.Equal(ch.MpCeiling, loaded.MpCeiling);
        Assert.Equal(ch.Lifespan, loaded.Lifespan);
        Assert.Equal(ch.AgeClock, loaded.AgeClock);
        Assert.Equal(ch.SkillBonuses, loaded.SkillBonuses);
        Assert.True(loaded.CastSinceRest);
        Assert.True(loaded.HasAged);
        Assert.Equal(2, loaded.CurrentTown);

        // Saves written before s17 used combatRating / birthStamp for @14 / @264.
        const string old = """[{"name":"old","class":"Priest","combatRating":10,"birthStamp":574,"intelligence":13}]""";
        var o = Assert.Single(GuildCodec.ReadJson(old));
        Assert.Equal(10, o.MpCeiling);
        Assert.Equal(574, o.AgeClock);
        Assert.Equal(CharacterCreation.SkillBonusSeeds(o.Stats), o.SkillBonuses);
    }

    private static bool FieldsMatch(Character ch, ReadOnlySpan<byte> b) =>
        ch.Stats.Str == (b[0] | (b[1] << 8))
        && ch.MagicMax == (b[16] | (b[17] << 8))
        && ch.HpMax == (b[20] | (b[21] << 8));
}
