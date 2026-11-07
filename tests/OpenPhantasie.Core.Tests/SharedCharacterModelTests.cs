using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T1.1: <see cref="Character"/> is the game-neutral record; GUILD offsets live in <see cref="GuildCodec"/>,
/// Phantasie I-only words in <see cref="Phantasie1CharacterData"/>, and the slots other games use stay empty in I.
/// </summary>
public class SharedCharacterModelTests
{
    private static readonly Lazy<CharacterCreationTables> TablesLazy =
        new(() => GameDataRepository.LoadPhantasie1().CreationTables);
    private static CharacterCreationTables Tables => TablesLazy.Value;

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static Lineage SampleLineage() =>
        new([new ParentRef("p4:race-a", "p4:class-a"), new ParentRef(null, "p4:class-b")]);

    private static IReadOnlyList<Deed> SampleDeeds() =>
        [new Deed(Deed.Slew, new ContentId(GameId.P1, 72)), new Deed("p4:kind-b", null)];

    private static BodyCondition SampleBody() =>
        new(new Dictionary<string, string> { ["head"] = "okay", ["torso"] = "injured" });

    [OriginalDataTheory]
    [InlineData("GUILD.3chars.dat")]
    [InlineData("samples/GUILD.pass4.dat")]
    public void GuildImport_LeavesOtherGamesSlotsEmpty_AndFillsTheIExtension(string file)
    {
        var chars = GuildCodec.ReadBinaryFile(Fixture(file), Tables).OfType<Character>().ToList();
        Assert.NotEmpty(chars);
        Assert.All(chars, ch =>
        {
            Assert.Null(ch.Lineage);
            Assert.Null(ch.BodyCondition);
            Assert.Null(ch.Deeds);
            Assert.NotNull(ch.Phantasie1.DosRecord);
            Assert.Equal(Phantasie1CharacterData.RuneCount, ch.Phantasie1.Runes.Length);
        });
    }

    [OriginalDataTheory]
    [InlineData("GUILD.3chars.dat")]
    [InlineData("samples/GUILD.pass4.dat")]
    [InlineData("GUILD.roster22.dat")]
    public void GuildExport_NeverWritesLineageOrBodyCondition(string file)
    {
        var original = File.ReadAllBytes(Fixture(file));
        var plain = GuildCodec.WriteBinary(GuildCodec.ReadBinary(original, Tables));

        var slots = GuildCodec.ReadBinary(original, Tables);
        foreach (var ch in slots.OfType<Character>())
        {
            ch.Lineage = SampleLineage();
            ch.BodyCondition = SampleBody();
            ch.Deeds = SampleDeeds();
        }
        Assert.Equal(plain, GuildCodec.WriteBinary(slots));

        // Same without the raw-record carry: the field map alone ignores them too.
        foreach (var ch in slots.OfType<Character>())
            ch.Phantasie1.DosRecord = null;
        var mapOnly = GuildCodec.WriteBinary(slots);
        foreach (var ch in slots.OfType<Character>())
        {
            ch.Lineage = null;
            ch.BodyCondition = null;
            ch.Deeds = null;
        }
        Assert.Equal(mapOnly, GuildCodec.WriteBinary(slots));
    }

    [Fact]
    public void Json_RoundTripsLineageBodyConditionAndDeeds()
    {
        var ch = new Character { Name = "heir", Lineage = SampleLineage(), BodyCondition = SampleBody(), Deeds = SampleDeeds() };
        var back = Assert.Single(GuildCodec.ReadJson(GuildCodec.WriteJson([ch])));

        Assert.NotNull(back.Lineage);
        Assert.Equal(ch.Lineage!.Parents, back.Lineage!.Parents);
        Assert.NotNull(back.BodyCondition);
        Assert.Equal(ch.BodyCondition!.Areas.OrderBy(kv => kv.Key), back.BodyCondition!.Areas.OrderBy(kv => kv.Key));
        Assert.Equal(ch.Deeds, back.Deeds);
    }

    [Fact]
    public void RecordDeed_Appends_AndCloneKeepsTheRecord()
    {
        var ch = new Character { Name = "hero" };
        ch.RecordDeed(new Deed(Deed.Slew, new ContentId(GameId.P1, 72)));
        var copy = ch.Clone();
        ch.RecordDeed(new Deed("p4:kind-b", null));

        Assert.Equal(2, ch.Deeds!.Count);
        Assert.Equal([new Deed(Deed.Slew, new ContentId(GameId.P1, 72))], copy.Deeds!);
    }

    [Fact]
    public void Json_EmptySlotsAreNotWritten_AndReadBackNull()
    {
        var json = GuildCodec.WriteJson([new Character { Name = "plain" }]);
        Assert.DoesNotContain("lineage", json);
        Assert.DoesNotContain("bodyCondition", json);
        Assert.DoesNotContain("deeds", json);
        Assert.Contains("phantasie1", json);

        var back = Assert.Single(GuildCodec.ReadJson(json));
        Assert.Null(back.Lineage);
        Assert.Null(back.BodyCondition);
        Assert.Null(back.Deeds);
    }

    [OriginalDataFact]
    public void Json_RoundTripsTheIExtension()
    {
        var ch = GuildCodec.ReadBinaryFile(Fixture("samples/GUILD.pass4.dat"), Tables).OfType<Character>().First();
        ch.Phantasie1.Runes = [1, 0, 1, 0, 1];
        ch.Phantasie1.Word90 = 105;
        ch.Phantasie1.Word26 = 9;

        var back = Assert.Single(GuildCodec.ReadJson(GuildCodec.WriteJson([ch])));
        Assert.Equal(ch.Phantasie1.Runes, back.Phantasie1.Runes);
        Assert.Equal(105, back.Phantasie1.Word90);
        Assert.Equal(9, back.Phantasie1.Word26);
        Assert.Equal(ch.Phantasie1.PartyOrderStamp, back.Phantasie1.PartyOrderStamp);
        Assert.Equal(ch.Phantasie1.DosRecord, back.Phantasie1.DosRecord);
        Assert.Equal(ch.Phantasie1.DosRecordLifespan, back.Phantasie1.DosRecordLifespan);
    }

    [Fact]
    public void Clone_DeepCopiesTheIExtension_AndKeepsTheSlots()
    {
        var ch = new Character
        {
            Name = "a",
            Phantasie1 = { Runes = [1, 1, 0, 0, 0], Word90 = 3, DosRecord = new byte[GuildCodec.RecordSize] },
            Lineage = SampleLineage(),
            BodyCondition = SampleBody(),
        };
        var copy = ch.Clone();

        Assert.NotSame(ch.Phantasie1, copy.Phantasie1);
        Assert.NotSame(ch.Phantasie1.Runes, copy.Phantasie1.Runes);
        Assert.NotSame(ch.Phantasie1.DosRecord, copy.Phantasie1.DosRecord);
        Assert.Equal(ch.Phantasie1.Runes, copy.Phantasie1.Runes);
        Assert.Equal(3, copy.Phantasie1.Word90);
        Assert.Same(ch.Lineage, copy.Lineage);           // immutable records may be shared
        Assert.Same(ch.BodyCondition, copy.BodyCondition);

        copy.Phantasie1.Runes[4] = 1;
        Assert.Equal(0, ch.Phantasie1.Runes[4]);
    }

    [Fact]
    public void FieldMap_FitsTheRecord()
    {
        Assert.Equal(282, GuildCodec.RecordSize);
        Assert.Equal(GuildCodec.RecordSize, GuildCodec.Field.Name + GuildCodec.Field.NameLength);
        Assert.Equal(GuildCodec.Field.WeaponDamageB, GuildCodec.Field.Items + 2 * Character.InventorySlots);
    }
}
