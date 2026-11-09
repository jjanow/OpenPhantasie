# Phantasie III — elementals (Mud, Mist, Air) as the 7th party slot

Phase 09 T4 3c.6 (s81, static; nothing run in DOSBox). Source of truth: the III manual (Guild ▸ List PDF 5 / pr.4,
Summon Elemental #48 PDF 11 / pr.16). Code: DOS `MAIN.EXE` (addresses `../FORMAT.md` §1). Tags: `[C]` code / data
bytes, `[~]` probable, `[?]` not decoded. No manual ↔ code conflict found.

## 1. The three records `[C]` (ovl2 `0x3f04`)

A new data disk's roster starts with three elementals: root `0xa856` sets the roster count to 3 and calls ovl2
`0x3f04`, which writes roster slots `count − 3 … count − 1` (so slots 0, 1, 2) as **Air, Mist, Mud**. All three get:
race 16 Elemental, class 0 Fighter, level 15, status Okay, rank Front, roster slot `+0x7e` = own index, all six
attributes 25, kit shield 4 / armour 22 / weapon 64 / bow 41 (the class kit's items, worn; carried count stays 0),
spell count 0 before the lists below.

| | Magic max = cur | HP max = cur | Attack | Parry | Spells |
|---|---|---|---|---|---|
| **Air** | 30 | 150 | 150 | 32 | 31 spells (DS `0x1bb2`): 3, 4, 7, 8, 11, 12, 14, 16, 19, 20, 23, 24, 27, 28, 31, 32, 34, 35, 36, 38, 39, 40, 41, 42, 43, 46, 47, 49, 51, 55, 56 |
| **Mist** | 20 | 200 | 210 | 42 | 25–32 (Weakness I–IV, Binding I–IV) |
| **Mud** | 12 | 250 | 255 | 38 | none |

Other skills stay 0. The manual (PDF 5 / pr.4) puts them as "the last three names on the list … they are fighters,
… guild numbers, and … reside in town 0"; DOS puts them first (slots 0–2) and their town word is 0 (never written)
`[~]`. List order is not a rule; not raised. (The Amiga save holds them in roster slots 20–24, `../FORMAT.md` §5.)

The Guild refuses to Purge ("[original text]"), Rename ("You cannot rename an Elemental.") or Inspect /
examine an elemental (strings 406, 414, 415, 488) `[C]` strings and guild code.

**Purge** (root `guild_menu` `0xafe3…0xb09c`) `[C]` s98 static: only with more than the 3 elemental records in the roster;
an elemental → "[original text]" (406); a character in the party (record `+0x24` ≠ 0) → "You cannot purge
a party member." (448); else the confirmation (407) and root `0x864a` closes the roster gap (later records move down,
party records' roster slot `+0x7e` above it −1, count −1). Party slots and the elemental are never touched, so in DOS
Purge never meets a loaded elemental. The manual's Purge drops the adventurer "from the guild list from the party", and
Apple II purges a party member too — **D3-040** (decided s99: manual). The remaster purges a party member and
keeps a loaded elemental while a character is left, as Drop (§2); s172 (user): the every-game `elemental-purge` = `game`
follows the preset in III (`rules.json` `elementals.purge` = `as-drop`, `purge_original` = `refused`: the Original (DOS)
preset refuses, string 448); the former `p3.purge` is folded in. The elemental records are never purged. Cross-game QoL
(PLAN P3): `elemental-purge` (and `elemental-load`, `elemental-drop` for §2's rules) can put I's, III's manual or DOS III's
rule in I, II or III; `game` in every preset. The remaster refuses a Purge of an elemental in every game, as DOS does here
(`GuildHall.PurgeRefusal`); s172: III's Purge list shows the three elementals after the characters (only when a character
is listed, as DOS), and a pick is refused with string 406 "[original text]" (III's table).

## 2. Adding one to the party `[C]` (Guild ▸ Add Member, root `0xad93` → ovl2 `0x3a0a`)

State: party records at `FAR:0x236 + 0x106·i` (7 slots), `FAR:0xb614` = characters in the party (≤ 6),
`0x27f3:0x0cc8` = filled party slots including the elemental (≤ 7), `FAR:0xb612` = elemental in party.

```
add(r):                                       # r = roster index
    if r is an elemental (race 16):
        if filled_slots < 1:  "[original text]"     # needs at least one character first
        elif elemental_in_party: "[original text]"
        else: copy r into slot filled_slots; filled_slots += 1; elemental_in_party = 1
        return                                 # does not change the character count
    if r already in a party:  "[original text]"
    if characters >= 6:       "[original text]"
    if r.town != current town: "It will take this character two months to make the journey here." (Yes/No)
                               Yes → r.age clock += 4, r.town = current      # 4 = 2 months at 26 per year
    mark r in party
    if elemental_in_party: move the elemental up one slot, put r in its old slot   # the elemental stays last
    else: put r in slot filled_slots
    characters += 1; filled_slots += 1
```

- The elemental **does not count** toward the six (manual "does not count as a member") and needs **at least one
  character** in the party, not six (manual question Q12; I's "will only serve a party of six" is not III's rule).
- Guild ▸ Add with all 7 slots filled: "The party is fully assembled." (elemental present) or "You can only add an
  elemental." `[C]` strings, `[~]` exact branch.
- Drop Member on the elemental clears the flag and `filled_slots` (ovl2 `0x3c92`). Built s172 (`drops_elemental`): the
  remaster's Drop list holds the loaded elemental after the characters (`GameSession.TryDropElemental`).
- Drop Member on a **character** with an elemental loaded `[C]` (s97 static, ovl2 `0x3d09…0x3e5a`): the records after
  the dropped one move up a slot (`filled_slots − 1`), so the elemental stays last and stays loaded; `characters` drops
  by one. Only when no character is left (`0x3e0b`: `characters ≤ 0`) are the elemental flag, `filled_slots` and
  `characters` zeroed and every roster in-party mark cleared. The manual is silent; no conflict.
- **Built (s95):** the rules profile's `elementals` block (`ElementalRules`: III `min_members` 1, `blocks_add` false;
  I's six-and-block when absent); the Guild Add list offers elementals once one character is in. **s97:** `drop_keeps`
  true (`ElementalRules.DropKeeps`): a Drop keeps the elemental while a character is left (I: every Drop clears it). The every-game options `elemental-load` / `elemental-drop` can play either game's rule in I, II and III (`ElementalGuildRules`).

## 3. Summoning in combat `[C]` (spell #48, root `0xc912`)

```
cast Summon Elemental:
    if not elemental_in_party: nothing happens        # the spell has no target to bring
    elif FAR:0x4880 (elemental already in this fight): nothing happens
    else: "[original text]"; FAR:0x4880 = 1; FAR:0xb576 = 1
```

The message is DS `0x656a`, printed through the combat message routine root `0xef5e` only on the success branch; the two
"nothing happens" branches print nothing `[C]`. Built s97 (`SpellCaster`, III only; I's OV2 `0x8cc` stays silent).
Only the elemental "loaded in at a guild" can come (manual PDF 11 / pr.16, agrees). While `FAR:0x4880` is set the
elemental fights from party slot 6; Flee's "anyone still able" test skips it (`encounters.md` §4). Whether the spell's
magic points are spent when nothing happens `[?]`. `FAR:0x4880` is cleared at the next fight start `[~]`.
