using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P3Dos;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P3C64;

/// <summary>
/// The C64's pre-generated party (user s217: imported, from the disk being imported): <c>PCHARS0</c> slots 0–5
/// (CHUMLEY, TRACKER, FINGERS, GRASSHOPPER, BONES, SAURIAN; the manual's "first 6 members of the Guild") written to the
/// import's <c>phantasie3/starter-guild.json</c> in Core's terms, which a new game puts in the Guild's first six slots.
/// The 170-byte record: <c>reverse-engineering/c64/phantasie3/FORMAT.md</c> 3.5 (read from TOWN.OBJ's creation code and
/// PHANT.OBJ's character sheet).
/// <para>Only a fresh <c>PCHARS0</c> (v1.0's or v1.2 (alt)'s own bytes) is read: a played roster may hold other people
/// in those slots, so a played copy imports the tables without the starter party (and says so).</para>
/// </summary>
public static class P3C64StarterGuild
{
    public const string File = "starter-guild.json";

    private const int Record = 170, Starters = 6;

    /// <summary>The pristine <c>PCHARS0</c> (with its load address) of each build.</summary>
    private static readonly Dictionary<string, string> Pristine = new(StringComparer.Ordinal)
    {
        ["9bbf79dbb3f88fa1cfa96301da1634d99bb0256f"] = V10,
        ["c612806fbeae6ba89f6347e7ad789b91ab4ac2b2"] = "v1.2 (alt)",
    };

    private const string V10 = "v1.0";

    private static readonly string[] Names = ["CHUMLEY", "TRACKER", "FINGERS", "GRASSHOPPER", "BONES", "SAURIAN"];

    // Core's enums (Phantasie I's order) = the C64 record's numbers (PHANT.OBJ race / class tables).
    private static readonly string[] CoreRace =
    [
        "Human", "Dwarf", "Elf", "Halfling", "Gnome", "Pixie", "Sprite", "Ogre", "Gnoll", "Troll", "Kobold", "Orc", "Goblin",
        "LizardMan", "Minotaur", "Undead",
    ];

    private static readonly string[] CoreClass = ["Thief", "Monk", "Ranger", "Fighter", "Priest", "Wizard"];

    /// <summary>The C64 status byte (PHANT.OBJ 1343: DEAD OKAY DOWN SLEP OUT) → Core's <c>CharacterStatus</c> name.</summary>
    private static readonly string[] CoreStatus = ["Dead", "Ok", "Down", "Sleep", "Out"];

    /// <summary>The nine skill values P(20 + 2k), the sheet's order.</summary>
    private static readonly string[] Skills =
        ["attack", "parry", "find_item", "find_trap", "disarm_trap", "listen", "fire_bow", "pick_lock", "swim"];

    /// <summary>Writes <see cref="File"/> from a fresh <c>PCHARS0</c>; a played one gives a warning and no file.</summary>
    public static void Write(ImportContext ctx)
    {
        if (Read(ctx.Files) is not { } node)
        {
            ctx.Warn("This C64 copy's Guild roster (PCHARS0) has been played: the pre-generated party is not imported " +
                     "(a new game starts with an empty Guild).");
            return;
        }
        ctx.WriteGameData(P3DosPipeline.Game, File, node, indent: 1);
    }

    /// <summary>The starter party of a fresh <c>PCHARS0</c>, or null when the file is not one of the known fresh ones.</summary>
    public static JsonObject? Read(FileSet files)
    {
        var file = files.Find("PCHARS0") ?? throw new FileNotFoundException("PCHARS0 not found", "PCHARS0");
        if (!Pristine.TryGetValue(file.Sha1, out var build))
            return null;
        var raw = file.Data[2..];
        var members = new JsonArray();
        for (var slot = 0; slot < Starters; slot++)
        {
            var p = raw.AsSpan(Record * slot, Record);
            var name = NameOf(p);
            if (name != Names[slot])
                throw new InvalidDataException($"PCHARS0 slot {slot}: '{name}', expected {Names[slot]}");
            members.Add(Member(slot, p, name, keepAge: build != V10));
        }
        return new JsonObject
        {
            ["_schema"] = "openphantasie.starter-guild/v1",
            ["_source"] = $"{P3C64Tables.Label}: PCHARS0 {build} slots 0-5 (170-B records, FORMAT.md 3.5) [C]",
            ["_note"] = "The pre-generated, pre-equipped party the manual lists as the first 6 Guild members (user s217: " +
                        "imported, from the disk being imported). A new game puts them in the Guild's first six slots. " +
                        "Values in Core's terms: race / class = Core's enum names (= the C64's numbers), social_class = III's " +
                        "(0 Peasant .. 3 Noble), origin = III's (3 Scandor), town = III's town number, spells_known[n - 1] = " +
                        "spell n (0 / 1 known / 2 learnable), age_clock in 1/26 year (null: Guild New's start age, D3-078: v1.0's stored " +
                        "clocks, kept in record_age_clock, read 255-314 years). What the record does not hold (lifespan, " +
                        "body areas, rank, skill bonuses) takes Core's rules.",
            ["tables"] = P3C64Tables.Label,
            ["members"] = members,
        };
    }

    private static string NameOf(ReadOnlySpan<byte> p)
    {
        var sb = new StringBuilder();
        foreach (var b in p.Slice(131, 20))
        {
            if (b == 0)
                break;
            sb.Append((char)(b & 0x7F));
        }
        return sb.ToString().TrimEnd();
    }

    /// <param name="keepAge">D3-078 (user s218): v1.0's stored clocks (255-314 years on its own sheet) are not kept
    /// (age_clock null = Guild New's start age at new game, the record's value in record_age_clock); v1.2's fit the C64's
    /// creation formula and are kept.</param>
    private static JsonObject Member(int slot, ReadOnlySpan<byte> p, string name, bool keepAge)
    {
        var skills = new JsonObject();
        for (var k = 0; k < Skills.Length; k++)
            skills[Skills[k]] = (int)p[20 + 2 * k];
        var carried = new JsonArray();
        foreach (var b in p.Slice(152, 9))
            if (b != 0)
                carried.Add((int)b);
        return new JsonObject
        {
            ["slot"] = slot,
            ["name"] = name,
            ["race"] = CoreRace[p[161]],
            ["class"] = CoreClass[p[169]],
            ["level"] = (int)p[54],
            ["attributes"] = P3Extract.Ints([p[1], p[2], p[3], p[4], p[5], p[6]]),
            ["hp_max"] = (int)p[12],
            ["hp_cur"] = (int)p[13],
            ["mp_ceiling"] = (int)p[9],
            ["mp_max"] = (int)p[10],
            ["mp_cur"] = (int)p[11],
            ["gold"] = p[14] | p[15] << 8,
            ["experience"] = p[51] | p[52] << 8 | p[53] << 16,
            ["age_clock"] = keepAge ? p[16] | p[17] << 8 : null,
            ["record_age_clock"] = p[16] | p[17] << 8,
            ["status"] = CoreStatus[p[18]],
            ["score"] = (int)p[122],
            ["social_class"] = (int)p[128],
            ["origin"] = (int)p[40],
            ["town"] = p[165] + 1,
            ["skills"] = skills,
            ["worn"] = new JsonObject
            {
                ["shield"] = (int)p[48], ["armor"] = (int)p[49], ["weapon"] = (int)p[50], ["bow"] = (int)p[47],
            },
            ["carried"] = carried,
            ["spells_known"] = P3Extract.Ints(p.Slice(61, 60).ToArray().Select(b => (int)b)),
        };
    }
}
