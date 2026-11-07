using System.Text;
using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// Files built from other I tables: item descriptions (<c>build_item_descriptions.py</c>: our text, plus each
/// scroll's first line and the treasure names from the copy) and spells (shipped, names from the copy).
/// </summary>
internal static class Derived
{
    // ---- item descriptions (build_item_descriptions.py, D-061) -------------------------------------------------
    // Ours: full names from the manual's TREASURES charts (p.17-18) and remaster-written text.

    private static readonly string[] Shields =
    [
        "Glove", "Wooden shield", "Wooden shield +1", "Small shield", "Small shield +1", "Small shield +2",
        "Small shield +3", "Medium shield", "Medium shield +1", "Medium shield +2", "Medium shield +3", "Large shield",
        "Large shield +1", "Large shield +2", "Large shield +3", "Giant shield", "Giant shield +1", "Giant shield +2",
        "Giant shield +3", "God shield",
    ];

    private static readonly string[] Armor =
    [
        "Clothing", "Robes", "Leather", "Hard leather", "Ring mail", "Scale mail", "Chain mail", "Splint mail",
        "Banded mail", "Plate mail", "Cloth +1", "Robes +1", "Leather +1", "Leather +2", "Ring mail +1", "Ring mail +2",
        "Chain mail +1", "Chain mail +2", "God robes", "God armor",
    ];

    private static readonly string[] Weapons =
    [
        "Stick", "Knife", "Small club", "Small staff", "Small mace", "Dagger", "Small flail", "Club", "Mace",
        "Small hammer", "Small axe", "Staff", "Short sword", "Flail", "Hammer", "Pitch fork", "Spear", "Axe", "Sword",
        "Heavy mace", "Maul", "Trident", "Large spear", "Large axe", "Morning star", "Pike", "Long sword", "Spetum",
        "Bardiche", "Halberd",
        "Small mace +1", "Dagger +1", "Small mace +2", "Dagger +2", "Dagger +3", "Staff +1", "Dagger +4", "Flail +1",
        "Spear +1", "Axe +1", "Sword +1", "Sword +2", "Sword +3", "Large axe +1", "Sword +4", "Sword +5", "Sword +6",
        "Halberd +1", "Sword +7", "Halberd +2", "Halberd +3", "Sword +10", "Halberd +4", "Halberd +5", "Halberd +6",
        "Halberd +7", "God knife", "God mace", "God axe", "God sword",
    ];

    private static readonly Dictionary<string, string> BaseText = new()
    {
        ["Glove"] = "A heavy leather gauntlet, stiff enough to turn a glancing blow.",
        ["Wooden shield"] = "A round shield of oak planks bound with iron.",
        ["Small shield"] = "A light buckler of hammered iron, quick to bring up.",
        ["Medium shield"] = "An iron shield that covers its bearer from shoulder to hip.",
        ["Large shield"] = "A tall steel shield; heavy to carry, hard to get past.",
        ["Giant shield"] = "A massive tower shield only the strongest can lift.",
        ["God shield"] = "A shield made by the gods. It shines with its own pale light.",
        ["Clothing"] = "Ordinary travelling clothes. Better than nothing, but not by much.",
        ["Cloth"] = "Plain-looking cloth, woven with a faint protective charm.",
        ["Robes"] = "Loose robes of the kind wizards and priests wear.",
        ["Leather"] = "A jerkin of boiled leather.",
        ["Hard leather"] = "Leather armor hardened with wax and set with studs.",
        ["Ring mail"] = "Leather sewn with rows of iron rings.",
        ["Scale mail"] = "Overlapping steel scales on a leather coat.",
        ["Chain mail"] = "A shirt of riveted steel links.",
        ["Splint mail"] = "Strips of steel laced over chain mail.",
        ["Banded mail"] = "Bands of steel over a mail shirt; heavy and strong.",
        ["Plate mail"] = "A full suit of steel plate, the best a smith can make.",
        ["God robes"] = "Robes woven by the gods. They turn blades like steel.",
        ["God armor"] = "Armor forged by the gods. No mortal smith can match it.",
        ["Stick"] = "A stout walking stick. It will do in a pinch.",
        ["Knife"] = "A short work knife.",
        ["Small club"] = "A short knotted club.",
        ["Small staff"] = "A light quarterstaff.",
        ["Small mace"] = "A short iron mace.",
        ["Dagger"] = "A double-edged fighting dagger.",
        ["Small flail"] = "A short handle with a spiked ball on a chain.",
        ["Club"] = "A heavy wooden club.",
        ["Mace"] = "A flanged iron mace.",
        ["Small hammer"] = "A one-handed war hammer.",
        ["Small axe"] = "A hand axe, balanced for fighting.",
        ["Staff"] = "A long iron-shod quarterstaff.",
        ["Short sword"] = "A short, broad-bladed sword.",
        ["Flail"] = "Two spiked balls on chains, swung from a long handle.",
        ["Hammer"] = "A heavy war hammer.",
        ["Pitch fork"] = "A farmer's pitch fork. Sharp enough.",
        ["Spear"] = "A long spear with a steel head.",
        ["Axe"] = "A broad-bladed battle axe.",
        ["Sword"] = "A well-made long-bladed sword.",
        ["Heavy mace"] = "A great iron mace that crushes armor.",
        ["Maul"] = "A huge two-handed hammer.",
        ["Trident"] = "A three-pronged fighting fork.",
        ["Large spear"] = "A heavy spear with a long steel head.",
        ["Large axe"] = "A great two-handed axe.",
        ["Morning star"] = "A spiked iron ball on a long haft.",
        ["Pike"] = "A very long spear for keeping foes at bay.",
        ["Long sword"] = "A long, finely balanced sword.",
        ["Spetum"] = "A pole arm with a long blade and two side spikes.",
        ["Bardiche"] = "A pole arm with a great curved axe blade.",
        ["Halberd"] = "An axe blade, a spike and a hook on a long pole.",
        ["God knife"] = "A knife made by the gods. It never dulls.",
        ["God mace"] = "A mace made by the gods. It rings like a bell when it strikes.",
        ["God axe"] = "An axe made by the gods. Its edge glows when it is drawn.",
        ["God sword"] = "A sword made by the gods, the finest weapon in Gelnor.",
    };

    private const string RingText =
        "One of the Nine Magical Rings that the Great Wizards of Gelnor forged with the aid of the gods to stand " +
        "against Nikademus. The scrolls say the rings are significant to the plight of Gelnor. The Mystic counts " +
        "each ring towards the party's score.";

    private const string WandText =
        "The Wand of Nikademus, carried by the Black Lord. Filmon the Sage writes that mortals can use it as a " +
        "very powerful weapon against evil, so it should not be destroyed if possible. The Mystic counts it " +
        "heavily towards the party's score.";

    private static readonly Dictionary<string, string> TreasureText = new()
    {
        ["EMERALD"] = "A green emerald, cut and polished.",
        ["SAPPHIRE"] = "A deep blue sapphire, one of those the Black Knights hoard.",
        ["RUBY"] = "A blood-red ruby.",
        ["COIN"] = "An old coin of Gelnor, from before the invasion.",
        ["DIAMOND"] = "A flawless diamond.",
        ["KEY"] = "An old iron key.",
        ["GOLD STAT"] = "A large gold statue fashioned after an ox and encrusted with jewels.",
        ["GOLD KEY"] = "A key of solid gold.",
        ["PLATINUM"] = "A bar of platinum.",
        ["GOLD COIN"] = "A heavy gold coin.",
        ["GOLD RING"] = "A golden ring of incredible workmanship.",
    };

    private const string TreasureTail = "Worth little in a fight, but the Armory pays well for it.";

    private static int Plus(string name) => name.Contains('+') ? int.Parse(name[(name.LastIndexOf('+') + 1)..]) : 0;

    private static string BaseName(string name)
    {
        var i = name.LastIndexOf(" +", StringComparison.Ordinal);
        return i < 0 ? name : name[..i];
    }

    private static string EquipmentText(string name)
    {
        var text = BaseText[BaseName(name)];
        if (Plus(name) is var p and not 0)
            text += $" It is enchanted (+{p}).";
        return text;
    }

    /// <summary>Python's <c>str.title()</c> for ASCII names.</summary>
    private static string Title(string s)
    {
        var sb = new StringBuilder(s.Length);
        var prevCased = false;
        foreach (var c in s)
        {
            var cased = char.IsLetter(c);
            sb.Append(cased ? (prevCased ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c)) : c);
            prevCased = cased;
        }
        return sb.ToString();
    }

    private static JsonObject Entry(string full, string text) => new() { ["full_name"] = full, ["text"] = text };

    public static JsonObject ItemDescriptions(JsonObject items, JsonObject scrolls)
    {
        var itemList = (JsonArray)items["items"]!;
        var scrollList = (JsonArray)scrolls["scrolls"]!;
        var output = new JsonObject();
        for (var i = 0; i < Shields.Length; i++)
            output[(1 + i).ToString()] = Entry(Shields[i], EquipmentText(Shields[i]));
        for (var i = 0; i < Armor.Length; i++)
            output[(21 + i).ToString()] = Entry(Armor[i], EquipmentText(Armor[i]));
        for (var i = 0; i < Weapons.Length; i++)
            output[(41 + i).ToString()] = Entry(Weapons[i], EquipmentText(Weapons[i]));
        for (var n = 1; n <= 10; n++)
        {
            output[(100 + n).ToString()] = Entry($"Healing potion {n}", "A vial of red liquid. Drinking it closes wounds.");
            output[(110 + n).ToString()] = Entry($"Magic potion {n}", "A vial of blue liquid. Drinking it restores a spellcaster's magic.");
        }
        for (var n = 1; n <= 20; n++)
        {
            // Original text: the scroll's first non-blank line, quoted.
            var first = ((JsonArray)scrollList[n - 1]!["lines"]!).Select(l => Py.Strip((string)l!)).First(l => l.Length != 0);
            output[(120 + n).ToString()] = Entry($"Scroll {n}", $"A scroll of the secret society: \"{first.TrimEnd(',', '.')}\".");
        }
        const string letters = "ABCDEFGHI";
        for (var k = 0; k < letters.Length; k++)
            output[(141 + k).ToString()] = Entry($"Ring {letters[k]}", RingText);
        output["150"] = Entry("The Wand of Nikademus", WandText);
        for (var i = 151; i < 181; i++)
        {
            var it = itemList[i]!;
            var name = (string)it["name"]!;
            var kind = TreasureText.ContainsKey(name) ? name : name.Split(' ')[0];
            var full = name == "GOLD STAT" ? "Gold statue" : Title(name);
            output[((int)it["index"]!).ToString()] = Entry(full, $"{TreasureText[kind]} {TreasureTail}");
        }
        if (output.Count != 180)
            throw Py.Bad($"item descriptions: {output.Count} entries, expected 180");
        return new JsonObject
        {
            ["_source"] = "Remaster-written (D-061, user s53): full names from the manual's TREASURES charts (p.17-18), ring / wand " +
                          "lore from the scrolls, other text invented. Stats are computed by Core (ItemRules.Describe). " +
                          "tools/scripts/build_item_descriptions.py",
            ["items"] = output,
        };
    }

    // ---- spells.json (hand-written; names by index from the copy) ----------------------------------------------

    /// <summary>
    /// The shipped spells.json (our categories, summaries, MP costs, notes) with every <c>name</c> taken from the
    /// copy's spell table by index. Null when nothing is shipped.
    /// </summary>
    public static JsonObject? Spells(JsonObject? shipped, IReadOnlyList<string> spellNames)
    {
        if (shipped is null)
            return null;
        var doc = (JsonObject)shipped.DeepClone();
        var list = (JsonArray)doc["spells"]!;
        for (var i = 0; i < list.Count; i++)
        {
            var o = (JsonObject)list[i]!;
            var index = (int)o["index"]!;
            if (index < 0 || index >= spellNames.Count)
                throw Py.Bad($"spells.json: index {index} outside the copy's {spellNames.Count} spells");
            // Rebuilt so "name" sits right after "index" whether or not the overlay still has it (the public overlay
            // has the original names removed).
            var rebuilt = new JsonObject();
            foreach (var (key, value) in o.ToList())
            {
                if (key == "name")
                    continue;
                o.Remove(key);
                rebuilt[key] = value;
                if (key == "index")
                    rebuilt["name"] = spellNames[index];
            }
            list[i] = rebuilt;
        }
        return doc;
    }
}
