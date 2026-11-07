# Phantasie IV — using items outside combat (spec, 3d.6, s82)

Source: X68000 `P4.X`, static reading of `_use` (`0xFEB0`), `_use_hp` (`0xEDC4`), `_use_mp` (`0xECD2`), `_use_op`
(`0xEEF0`), `_use_mi` (`0xF8C6`), `_use_sc` (`0xFAD0`), `_use_box` (`0xFCB2`), `_spoile` (`0xEC4A`), `_ddamage`
(`0x1A322`), `_heal` (`0x1FFA`). Written sources: manual pp. 45 (Table 2), 57–58 (Tables 3–4); the item catalogue
(`docs/manuals/phantasie4/phantasie4-item-catalog-v1.00-utf8.txt`, secondary). Tags: `[C]` code, `[~]` likely, `[?]`
open. Not cross-checked on MSX2; nothing checked in an emulator. Using items in combat: `combat.md`.

Record offsets: `../FORMAT.md` §6.2 (+0A condition, +0C wounds, +19 race, +29 move base, +2F–34 Str, Int, Dex, Con,
Cha, Luck, +37 / +38 MP max / current, +39 / +3A HP max / current, +52–5A carried items, +5B–63 their charges, +73…
spell states, +B9 saved race). Item ids: `../extracted/names.json` `itm`.

## 1. The Use Item screen (`_use`) `[C]`

The player picks a member, then one of that member's nine carried items:

| Item ids | What happens |
|---|---|
| 0x55–0x80 (potions), 0x87 Rod of Resurrection | the item is **removed from the slot at once**, then the player picks the member who drinks it / is touched (any party member) |
| 0x81–0x8B except 0x87 (rings, shoes, Charon's items, wand, gloves, gauntlets, hat, boots) | `_use_mi` on the owner, with the slot's charge byte |
| 0x8C–0x90 (scrolls, Orc Diary) | `_use_sc`: shows the scroll text (`scr` files, `SCBACK.PKH`) |
| 0x91 The Box | removed from the slot, `_use_box` (story event: "The party is sucked into the box") |
| others | nothing |

## 2. Spoiling (`_spoile`) `[C]`

Every potion except the Rod of Resurrection first rolls rand(100) (1..100): **> 90 (10 %)** → "This potion has gone
bad!" (`0x425C4`), the drinker takes rand(8) (1–8) damage (`_ddamage`: HP − d; d ≥ HP → HP 0 and condition Dead; if
nobody is left alive the party-dead path runs) and the potion has **no other effect** (it is still used up). The
manual and the catalogue do not mention spoiling.

## 3. Healing and magic potions `[C]`

Healing n (item 0x54 + n, n = 1–10), `_use_hp`:

```
HP = min(HP max, HP + n²)
wounds: _heal(member, k) with k = trunc((n + 1) / 3)
```

Magic n (item 0x5E + n), `_use_mp`: `MP = min(MP max, MP + 3 n)`.

`_heal(member, k)` (also used by the inn with k = 2 and by spells): k is a budget of wound points. The six body parts
hold levels 0–3 (light 1, serious 2, lost 3). Starting at level min(k, 3), every part at that level is cleared and its
level is taken from the budget; then the next lower level, and so on while budget remains (the level searched never
exceeds the budget left). So k = 1 heals one light wound, k = 2 one serious or two light, k = 3 one lost part, or one
serious + one light, or three light.

| Healing n | HP (code) | HP (manual Table 3) | k (code) | manual Table 2 heals |
|---|---|---|---|---|
| 1 | 1 | 1 | **0 (no wound healed)** | Healing 1 = one light wound |
| 2 | 4 | 4 | 1 | Healing 1 |
| 3 | 9 | 9 | 1 | Healing 1 |
| 4 | 16 | 16 | **1** | Healing 2 = one serious or two light |
| 5 | 25 | 25 | 2 | Healing 2 |
| 6 | **36** | **35** | 2 | Healing 2 |
| 7 | 49 | 49 | **2** | Healing 3 = one lost, or one serious + one light |
| 8 | 64 | 64 | 3 | Healing 3 |
| 9 | 81 | 81 | 3 | Healing 3 |
| 10 | 100 | 100 | **3** | Healing 4 = one lost part or more |

Magic 1–10 restore 3–30 MP, as manual Table 4 and the catalogue. **Conflict:** the code's wound healing for potions
1, 4, 7 and 10 is one step below the manual's Table 2 / Table 3 (→ `DIFFERENCES-IV.md` D4-035, PENDING). Healing 6's
36 vs the printed 35 is listed there too (the printed table is otherwise exactly n²).

## 4. Special potions (`_use_op`) `[C]`

"If < c" means the change is made only while the value is below c (so a value can reach c, not pass it).

| Id | Item | Code effect | Catalogue (secondary) |
|---|---|---|---|
| 0x69 | Strength Potion | Str +1 if < 24 | str +1 |
| 0x6A | Dexterity Potion | Dex +1 if < 24 | dex +1 |
| 0x6B | Charisma Potion | Cha +1 if < 24 | cha +1 |
| 0x6C | Intelligence Potion | Int +1 if < 24 | int +1 |
| 0x6D | Magic Power Potion | **+2F (Str) +2 if < 30**, MP = MP max | MP +2 |
| 0x6E | Healing Spell Potion | learns spell 2 Healing 2 (state 1) | healing 3 |
| 0x6F | Fireflash III Potion | learns spell 7 Fire Flash 3 | fireflash 3 |
| 0x70 | Sleep Spell Potion | learns spell 38 Sleep | sleep |
| 0x71 | Flamebolt II Potion | learns spell 56 Flameball 2 | flameball 3 |
| 0x72 | Summon Elemental Potion | learns spell 42 Summon El. | Dispell Undead |
| 0x73 | Duck Potion | unless already a Duck: saves the race in +B9, race = 34 (Duck) | changes form |
| 0x74 | Anti-Duck Potion | if a Duck: race = +B9 | changes form |
| 0x75 / 0x76 / 0x77 | Giant / Ogre / Halfling Strength | Str = 22 / 19 / 9 | str 22 / 19 / 9 |
| 0x78 | Mage Mind Potion | Int = 18 | int 18 |
| 0x79 | Magic Elixir | HP = HP max + 10, MP = MP max + 10 (bytes; no cap, can wrap) | HP and MP restored |
| 0x7A | Filmon's Concoction | Str −1 if > 4, Int +1 if < 24, Cha +2 if < 24 | cha 22 |
| 0x7B | Pixie Dust | Dex +1 if < 24; move base (+29) +2 if < 18, then at least 10 | dex +1, mov +2 |
| 0x7C | Pelham's Potion | damage rand(26) + 24 (25–50, `_ddamage`) | better not drunk |
| 0x7D | Mo's Mysterious Mix | for each of Str, Int, Dex, Con, Cha, Luck: + (rand(2) − 1) if < 24, then − (rand(2) − 1) if > 3 (each ability −1, 0 or +1) | all ±1 |
| 0x7E | Zeus Juice | learns spell 65 Super Transport (+B3 = 1); message "You feel you could cast any spell at all. But is it wise to drink it here? … better to start over without saving." | "the work of the gods" |
| 0x7F | Motion Potion | move base +2 if < 18 | mov ? |
| 0x80 | Nigel's Nostrum | HP + 100 if the result is < 256 (may exceed HP max) | HP +100?! |
| 0x87 | Rod of Resurrection | (no spoil roll) wounds cleared, condition = 1 Okay (HP unchanged) | raises the dead |

Notes `[~]`: the Magic Power Potion raising +2F (Strength, `../FORMAT.md` §6.2) instead of MP max looks like an
original slip (the catalogue says MP +2); the code is what it is. The catalogue's spell numbers differ for 0x6E, 0x71
and 0x72; the manual is silent on special potions, so these are not raised (s81 rule: secondary sources vs code are
not raised where the manual is silent).

## 5. Magic items (`_use_mi`) `[C]`

| Id | Item | Effect when used (charges = the slot's charge byte) |
|---|---|---|
| 0x81 | Ring of Magic Power | while charges > 0 and MP < MP max: charges −1, MP +1; then **+2F (Str) +1 if < 24** `[~]` (same slip?) |
| 0x83 | Ring of Healing | if charges ≥ 2: charges −2, HP + rand(27) (1–27), cap HP max |
| 0x89 | Fighter's Gauntlets | if the owner is a Fighter (class 3) and charges > 0: charges = 0, Str +2 if < 30 (once) |
| 0x82, 0x84–0x86, 0x88, 0x8A, 0x8B | Shoes of Speed, Charon's Raft / Staff, Fireflash Wand, Thieve's Gloves, Wizard's Hat, Ranger's Boots | nothing here (passive or used elsewhere: Shoes of Speed in `_baserate`; Charon's items: `towns.md` §10 / `outdoor.md`; the other four have no effect anywhere, below) |

Rings 0x81 / 0x83 are recharged to 20 at the inn (`towns.md` §4).

**Fireflash Wand (0x86), Thieve's Gloves (0x88), Wizard's Hat (0x8A), Ranger's Boots (0x8B): no effect in P4.X**
`[C]` (s110). Their ids appear in the code only in `_use_mi`'s do-nothing case (`0xF8C6`, `switch` cases 0x86 / 0x88 /
0x8A / 0x8B → `break`). The only item-presence test, `_haveit(member, id)`, has three callers (`_ochkedge` 132 / 133
Charon's items, `_baserate` 130 Shoes of Speed); no compare against 134 / 136 / 138 / 139 (or their signed bytes)
exists elsewhere, no event script tests them (`HASITEM`), and `_equip` / `_cal_user` treat ids ≥ 0x55 only as "always
usable". The game's own appraisal agrees: `_ident_mi` gives all four "It seems to have no special effect."
(`0x4573C`, `spells-noncombat.md`). The item catalogue (secondary) describes effects (the wand "can use fireflash",
the gloves / hat / boots "work for thieves / wizards / rangers"); the manual is silent, so this is not raised (s81 rule).
Shop price and the sale value are their only use.

## 6. Open `[?]`

- (s110 resolved: Thieve's Gloves, Wizard's Hat, Ranger's Boots and the Fireflash Wand have no effect, §5.)
- (s110 resolved: no item can be used in combat. `_use` is called only from the town (`_town`), outdoor (`_omain`) and
  dungeon (`_dmain`) menus, and the combat actions (`combat.md` §2) have no Use; the manual p. 45 agrees: "After combat,
  use healing potions to restore HP.") `[C]`
