#!/usr/bin/env python3
"""Write the remaster's item descriptions for town U)SE ITEM (D-061, user s53: manual).

Manual p.12: U)SE ITEM lets an adventurer "find the unusual properties of an item". DOS prints "UNUSABLE" for anything
that is not a potion or a scroll, so there is no DOS text: the full names come from the manual's TREASURES charts
(p.17-18), the lore lines from the scrolls (Nine Rings, Wand of Nikademus, Filmon's reports), and the rest is
remaster-written flavour. Stats (points, strength needed, value) are not stored here: Core computes them from the item
id (``ItemRules.Describe``) so they can't drift from the rules.

Output: src/OpenPhantasie.Data/GameData/phantasie1/item-descriptions.json
"""
from __future__ import annotations

import json

from re_common import REPO

OUT = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1" / "item-descriptions.json"

# Manual p.17 (shields 1-20, armor 21-40) and p.18 (weapons 41-100) "Type" column, spelling tidied.
SHIELDS = ["Glove", "Wooden shield", "Wooden shield +1", "Small shield", "Small shield +1", "Small shield +2",
           "Small shield +3", "Medium shield", "Medium shield +1", "Medium shield +2", "Medium shield +3",
           "Large shield", "Large shield +1", "Large shield +2", "Large shield +3", "Giant shield", "Giant shield +1",
           "Giant shield +2", "Giant shield +3", "God shield"]
ARMOR = ["Clothing", "Robes", "Leather", "Hard leather", "Ring mail", "Scale mail", "Chain mail", "Splint mail",
         "Banded mail", "Plate mail", "Cloth +1", "Robes +1", "Leather +1", "Leather +2", "Ring mail +1",
         "Ring mail +2", "Chain mail +1", "Chain mail +2", "God robes", "God armor"]
WEAPONS = ["Stick", "Knife", "Small club", "Small staff", "Small mace", "Dagger", "Small flail", "Club", "Mace",
           "Small hammer", "Small axe", "Staff", "Short sword", "Flail", "Hammer", "Pitch fork", "Spear", "Axe", "Sword",
           "Heavy mace", "Maul", "Trident", "Large spear", "Large axe", "Morning star", "Pike", "Long sword", "Spetum",
           "Bardiche", "Halberd",
           "Small mace +1", "Dagger +1", "Small mace +2", "Dagger +2", "Dagger +3", "Staff +1", "Dagger +4",
           "Flail +1", "Spear +1", "Axe +1", "Sword +1", "Sword +2", "Sword +3", "Large axe +1", "Sword +4",
           "Sword +5", "Sword +6", "Halberd +1", "Sword +7", "Halberd +2", "Halberd +3", "Sword +10", "Halberd +4",
           "Halberd +5", "Halberd +6", "Halberd +7", "God knife", "God mace", "God axe", "God sword"]

BASE_TEXT = {
    "Glove": "A heavy leather gauntlet, stiff enough to turn a glancing blow.",
    "Wooden shield": "A round shield of oak planks bound with iron.",
    "Small shield": "A light buckler of hammered iron, quick to bring up.",
    "Medium shield": "An iron shield that covers its bearer from shoulder to hip.",
    "Large shield": "A tall steel shield; heavy to carry, hard to get past.",
    "Giant shield": "A massive tower shield only the strongest can lift.",
    "God shield": "A shield made by the gods. It shines with its own pale light.",
    "Clothing": "Ordinary travelling clothes. Better than nothing, but not by much.",
    "Cloth": "Plain-looking cloth, woven with a faint protective charm.",
    "Robes": "Loose robes of the kind wizards and priests wear.",
    "Leather": "A jerkin of boiled leather.",
    "Hard leather": "Leather armor hardened with wax and set with studs.",
    "Ring mail": "Leather sewn with rows of iron rings.",
    "Scale mail": "Overlapping steel scales on a leather coat.",
    "Chain mail": "A shirt of riveted steel links.",
    "Splint mail": "Strips of steel laced over chain mail.",
    "Banded mail": "Bands of steel over a mail shirt; heavy and strong.",
    "Plate mail": "A full suit of steel plate, the best a smith can make.",
    "God robes": "Robes woven by the gods. They turn blades like steel.",
    "God armor": "Armor forged by the gods. No mortal smith can match it.",
    "Stick": "A stout walking stick. It will do in a pinch.",
    "Knife": "A short work knife.",
    "Small club": "A short knotted club.",
    "Small staff": "A light quarterstaff.",
    "Small mace": "A short iron mace.",
    "Dagger": "A double-edged fighting dagger.",
    "Small flail": "A short handle with a spiked ball on a chain.",
    "Club": "A heavy wooden club.",
    "Mace": "A flanged iron mace.",
    "Small hammer": "A one-handed war hammer.",
    "Small axe": "A hand axe, balanced for fighting.",
    "Staff": "A long iron-shod quarterstaff.",
    "Short sword": "A short, broad-bladed sword.",
    "Flail": "Two spiked balls on chains, swung from a long handle.",
    "Hammer": "A heavy war hammer.",
    "Pitch fork": "A farmer's pitch fork. Sharp enough.",
    "Spear": "A long spear with a steel head.",
    "Axe": "A broad-bladed battle axe.",
    "Sword": "A well-made long-bladed sword.",
    "Heavy mace": "A great iron mace that crushes armor.",
    "Maul": "A huge two-handed hammer.",
    "Trident": "A three-pronged fighting fork.",
    "Large spear": "A heavy spear with a long steel head.",
    "Large axe": "A great two-handed axe.",
    "Morning star": "A spiked iron ball on a long haft.",
    "Pike": "A very long spear for keeping foes at bay.",
    "Long sword": "A long, finely balanced sword.",
    "Spetum": "A pole arm with a long blade and two side spikes.",
    "Bardiche": "A pole arm with a great curved axe blade.",
    "Halberd": "An axe blade, a spike and a hook on a long pole.",
    "God knife": "A knife made by the gods. It never dulls.",
    "God mace": "A mace made by the gods. It rings like a bell when it strikes.",
    "God axe": "An axe made by the gods. Its edge glows when it is drawn.",
    "God sword": "A sword made by the gods, the finest weapon in Gelnor.",
}

RING_TEXT = ("One of the Nine Magical Rings that the Great Wizards of Gelnor forged with the aid of the gods to stand "
             "against Nikademus. The scrolls say the rings are significant to the plight of Gelnor. The Mystic counts "
             "each ring towards the party's score.")
WAND_TEXT = ("The Wand of Nikademus, carried by the Black Lord. Filmon the Sage writes that mortals can use it as a "
             "very powerful weapon against evil, so it should not be destroyed if possible. The Mystic counts it "
             "heavily towards the party's score.")
TREASURE_TEXT = {
    "EMERALD": "A green emerald, cut and polished.",
    "SAPPHIRE": "A deep blue sapphire, one of those the Black Knights hoard.",
    "RUBY": "A blood-red ruby.",
    "COIN": "An old coin of Gelnor, from before the invasion.",
    "DIAMOND": "A flawless diamond.",
    "KEY": "An old iron key.",
    "GOLD STAT": "A large gold statue fashioned after an ox and encrusted with jewels.",
    "GOLD KEY": "A key of solid gold.",
    "PLATINUM": "A bar of platinum.",
    "GOLD COIN": "A heavy gold coin.",
    "GOLD RING": "A golden ring of incredible workmanship.",
}
TREASURE_TAIL = "Worth little in a fight, but the Armory pays well for it."


def plus(name: str) -> int:
    return int(name.rsplit("+", 1)[1]) if "+" in name else 0


def base(name: str) -> str:
    return name.rsplit(" +", 1)[0]


def equipment_text(name: str) -> str:
    b = base(name)
    text = BASE_TEXT[b]
    if plus(name):
        text += f" It is enchanted (+{plus(name)})."
    return text


def main() -> None:
    items = json.loads((REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1" / "items.json").read_text())["items"]
    scrolls = json.loads((REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1" / "scrolls.json").read_text())["scrolls"]
    out: dict[str, dict] = {}
    for i, full in enumerate(SHIELDS, start=1):
        out[str(i)] = {"full_name": full, "text": equipment_text(full)}
    for i, full in enumerate(ARMOR, start=21):
        out[str(i)] = {"full_name": full, "text": equipment_text(full)}
    for i, full in enumerate(WEAPONS, start=41):
        out[str(i)] = {"full_name": full, "text": equipment_text(full)}
    for n in range(1, 11):
        out[str(100 + n)] = {"full_name": f"Healing potion {n}",
                             "text": "A vial of red liquid. Drinking it closes wounds."}
        out[str(110 + n)] = {"full_name": f"Magic potion {n}",
                             "text": "A vial of blue liquid. Drinking it restores a spellcaster's magic."}
    for n in range(1, 21):
        lines = [l.strip() for l in scrolls[n - 1]["lines"] if l.strip()]
        out[str(120 + n)] = {"full_name": f"Scroll {n}", "text": f"A scroll of the secret society: \"{lines[0].rstrip(',.')}\"."}
    for k, letter in enumerate("ABCDEFGHI"):
        out[str(141 + k)] = {"full_name": f"Ring {letter}", "text": RING_TEXT}
    out["150"] = {"full_name": "The Wand of Nikademus", "text": WAND_TEXT}
    for it in items[151:181]:
        name = it["name"]
        kind = name if name in TREASURE_TEXT else name.split(" ")[0]
        full = "Gold statue" if name == "GOLD STAT" else name.title()
        out[str(it["index"])] = {"full_name": full, "text": f"{TREASURE_TEXT[kind]} {TREASURE_TAIL}"}

    assert len(out) == 180, len(out)
    doc = {
        "_source": "Remaster-written (D-061, user s53): full names from the manual's TREASURES charts (p.17-18), ring / wand "
                   "lore from the scrolls, other text invented. Stats are computed by Core (ItemRules.Describe). "
                   "tools/scripts/build_item_descriptions.py",
        "items": out,
    }
    OUT.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n")
    print(f"wrote {OUT} ({len(out)} items)")


if __name__ == "__main__":
    main()
