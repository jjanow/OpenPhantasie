# Phantasie IV — spells cast outside combat (spec, 3d.6, s82)

Source: X68000 `P4.X`, static reading of `_pmagic3` (`0x2D3EC`), `_cast_47` (`0x2D0C6`), `_cast_50` (`0x2D2B2`),
`_ident` (`0x2DDBA`) / `_ident_mi` (`0x2DA08`); callers `_ocast` (`0x153A2`, outdoors) / `_dcast` (dungeon) / `_tcast`
(town). Which spells each place offers: `_getmagic(member, place)` (place 5 outdoors, 6 dungeon, 7 town), decoded
below. Combat spells: `combat.md` §9. Written source: manual pp. 30, 35, 48–55 and the spell list. Tags: `[C]` code,
`[~]` likely, `[?]` open. Spell numbers = `_magic` index + 1; the rank r of a spell is the byte table at `0x44C59`
(Healing 1–4 → r 1–4).

| Spell | Effect outside combat `[C]` |
|---|---|
| 1–4 Healing r | target: HP += rand(6 r² + 4) − 1 (**0**–9 / 27 / 57 / 99), capped at HP max; wounds `_heal(target, r)` (`items.md` §3); "%s's wounds healed somewhat." |
| 13 Levitation | `_par`+15 = 1, "The party floated up into the air." (duration and effect: `outdoor.md` §6, D4-039) |
| 15 Identify | the Item Appraisal screen: one chosen item's description (`_ident_mi`, by item kind). **Always succeeds** (no roll) |
| 40 Resurrection | target must be Dead (condition 0), else "Nothing changed."; succeeds if rand(100) < 50 + 5 × (caster level − target level): condition 1, wounds cleared, HP unchanged; else "Nothing changed." (MP cost: charged by the caller, not read here `[?]`) |
| 47 Transportation | only when the caster's current town (+1B) is in a group: towns 1–12 → group 0 (towns 1–12), 14 → group 1 (1–12, 17, 18), 17 / 18 → group 2 (14, 17, 18, 21), 21 → group 3 (17, 18) (`_twnt` `0x44EE2`); the player picks a town from the group by letter and confirms; the party is moved to that town. Otherwise "Transport! …Nothing changed." |
| 65 Super Transport | as Transportation, but the town is random from the group (rand(count)) |
| 49 Monster Evaluation | outdoors: shows the current encounter level `_emlevel` clamped to 1–12; in a dungeon: the fixed level of the map (`_dmlv[map]`: 1 1 4 2 4 3 2 2 4 0 5 6 10 8 0 0 7 10 12 6 6 for maps 1–21). "The monsters around here are about level N." **Never wrong** (no roll) |
| 50 Magic Bag | "Suddenly a battered old bag appeared … put 100 gold in?" Yes, with a free pack slot and party gold ≥ 100: gold −100 and one item from a 10-entry table (`0x44E52`, rand(10)): Leather, Large Shield, Compound Bow, Throwing Daggers, Staff, Knife, Dagger, Duck Potion, Magic 4, Magic 3. Otherwise "Nothing happened, and the bag vanished." (manual p. 54: put 100 gold in first, matches) |
| 63 Divine Aid | a random god (rand(4): "A demigod", Athena, Apollo, Zeus) "whispered in your ear" a fixed hint for the current map (table `0x44E8A`, 21 hints, e.g. map 21 "Success here depends on how many 'Zeus Juice' you have") |

Against the manual:
- Identify (p. 54: 「但し、いつでも識別出来るとは限りません。」 "but it does not always succeed") vs always
  succeeds → `DIFFERENCES-IV.md` D4-053 (decided: manual, 50 % + 5 % per caster level, user s84).
- Monster Evaluation (p. 55: 「時々、間違った報告をすることもあります」 "sometimes reports wrongly") vs never wrong → D4-054
  (decided: manual, off by 1 on (50 − 5 × level, min 10) % of casts, user s84).
- Healing restores 0–N here and in combat (manual 1–N): listed under D4-051.
- Resurrection "sometimes fails; may take 3–4 tries": matches (level-based chance).
- Magic Bag: matches. Transportation "choose the destination town by number": the game uses letters (A, B, …) — wording.

## Which spells each place offers (`_getmagic` `0x2BA96`, `_splist` `0x2B86E`, s102) `[C]`

`_getmagic(member, place)` lists one page of spell numbers from a byte table (`0x44AC8`: {first, last} per page into
the list at `0x44AE2`). Places 5–7 have one page; every spell of the page gets its letter (A, B, … by page place), but its
name and button only if its state is 1 (known) and its MP cost ≤ the member's current MP (s151: the letter is drawn
before the test, so the letters keep their places with gaps; FORMAT.md 6.9 "Lists in window 34"). "SPACE: Quit" returns 100. The page cannot change for places 5–7 (s110): the
loop moves to page place + `_bvs(key)` on any key that is neither SPACE nor a letter, but `_spelltop` clears every button
(`_clrbtn`, `_clrbtd` sets `_btd` to −1) and registers only SPACE (`_btd`[5]) and the listed letters (`_btd`[6 + i]),
and `_mgetc` returns only registered buttons (`_inkey3` looks the key up in the button table, else waits) `[C]`.

| Place | Spells offered (in this order) |
|---|---|
| 5 outdoors (`_ocast(5)`) | Healing 1–4, Levitation (13), Resurrection (40), Identify (15), Monster Evaluation (49), Magic Bag (50) |
| 6 dungeon (`_dcast`) | the same + Divine Aid (63) |
| 7 town (`_tcast`) | Healing 1–4, Levitation, Resurrection, Identify, Transportation (47), Magic Bag, Super Transport (65) |
| 8 Magic Training Hall | five pages of 13 in number order; state 2 (open) only, no MP test (`training.md` §5) |
| 0 / other (combat) | five category pages: Non-combat, Combat (received), Combat × 3 (lists at `0x44AE2` + 0 … 58; s152: their spells, the head lines and the remaster build in FORMAT.md 6.9 "Combat spell list") |

**`_ocast(place)` `[C]`:** pick the caster (SPACE leaves), then the spell; for Healing 1–4 and Resurrection the player
also picks the target member (SPACE leaves). "<caster> cast the spell." then the effect (`_pmagic3`), then MP −= the
spell's cost (floor 0) **whatever the outcome** ("Nothing changed." still costs). Identify (15) redraws the screen. The
menu repeats until a spell moves the party (`_cmovf`: Transportation).

Against the manual (D4-061): p. 54–55 put Levitation and Divine Aid (#63) in "the wilds or dungeons" and Identify /
Magic Bag "anywhere"; the code also offers Levitation in town and leaves Divine Aid out of the outdoor list. pp. 32 / 35
("in town only Healing, Resurrection, Transportation"; "outdoors only Healing, Monster Evaluation, Resurrection")
contradict pp. 54–55 inside the manual.

## Item appraisal texts (`_ident_mi` `0x2DA08`, s110) `[C]`

Identify (and nothing else) shows one line chosen by the item id. Strings at `0x45511`… (English in
`extracted/text/strings.json`; the remaster's data `identify_lines.by_item`):

| Item ids | Text |
|---|---|
| 0x55–0x5E (Healing 1–10) | "Hit points will be restored somewhat." |
| 0x5F–0x68 (Magic 1–10) | "Magic points will be restored somewhat." |
| 0x69–0x6D, 0x75–0x7D except 0x79 / 0x7C (ability potions) | "It will affect abilities." |
| 0x6E–0x72 (spell potions) | "It teaches a spell." |
| 0x14, 0x19 | "Armour that feels weightless." |
| 0x34, 0x35, 0x3B, 0x3F, 0x4A, 0x4B, 0x53, 0x54 | "It has a special effect on blows against enemies." |
| 0x73, 0x74 (Duck / Anti-Duck) | "It changes your form." |
| 0x79 Magic Elixir | "Wounds will heal somewhat." |
| 0x7C Pelham's Potion | "This potion seems better left unused." |
| 0x7E Zeus Juice | "With it you may take part in the work of the gods." |
| 0x7F Motion Potion, 0x82 Shoes of Speed | "It lengthens your stride." |
| 0x80 Nigel's Nostrum, 0x83 Ring of Healing | "It restores some hit points." |
| 0x81 Ring of Magic Power | "It restores some magic power." |
| 0x84 Charon's Raft / 0x85 Staff | "A raft for crossing the River of Hell." / "A pole used with the raft …" |
| 0x87 Rod of Resurrection | "It brings the dead back to life." |
| 0x89 Fighter's Gauntlets | "It strengthens fighters." |
| 0x91 The Box | "Beware. Inside is a world of darkness." |
| every other id (plain weapons and armour, 0x86 Fireflash Wand, 0x88 Thieve's Gloves, 0x8A Wizard's Hat, 0x8B Ranger's Boots, scrolls) | "It seems to have no special effect." |

Open `[?]`: what Transportation does to time or age.
