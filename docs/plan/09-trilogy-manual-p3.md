# Phantasie III vs Phantasie I (and II where relevant): rules matrix from the SSI manuals

Cold file (Phase 09 T0): the full manual-to-manual comparison behind `09-trilogy-matrix.md`. Every claim carries a page
citation, taken from the manuals' text layers and page renders; check the cited page before building a rule on it.

## Sources and citation keys

- **I**: `docs/manuals/phantasie1/Phantasie_I_1986_SSI_manual_text.pdf`. Cited by PDF page. PDF 7–15 = printed pp.2–10; PDF 16 is the centrefold art spread (printed pp.11–12, art only, no rules); PDF 17–23 = printed pp.13–19; PDF 25 is the terrain key. PDF 2–3 are the C64 supplement.
- **I-RC**: `docs/manuals/phantasie1/phantasie1-refcard.pdf`. This is the C64 supplement card. It repeats I PDF 2–3 and adds only C64 UI details, such as the 9-character name limit.
- **III**: `docs/manuals/phantasie3/phantasie3-manual.pdf` (US). The PDF pages are two-page spreads and are cited as "PDF n / pr. x–y": PDF 3 = pr.1, 4 = 2–3, 5 = 4–5, 6 = 6–7, 7 = 8–9, 8 = 10–11, 9 = 12–13, 10 = 14–15, 11 = 16–17, 12 = 18–19, 13 = 20–21, 14 = 22–23, 15 = 24–25, 16 = credits, 17 = spell-table card.
- **III-AU**: `phantasie3-manual-australia.pdf`. PDF 2–15 carry the same rulebook pages as the US manual. PDF 16–18 are computer command cards (C64, ST, Amiga, IBM) and PDF 19 is the spell-table card.
- **II**: `docs/manuals/phantasie2/phantasie2-manual.pdf`. This is actually the combined "Phantasie I and Phantasie II Rulebook" (PDF 1). The II-specific changes are on PDF 16, and the I→II transfer text is on PDF 12 and 16.
- **WW**: `docs/manuals/phantasie1-and-3/phantasie1-and-3-wizardworks-manual-1990.pdf`. Single pages. The I part is PDF 2–11; the III part is PDF 12–14.

Method: `pdftotext` per page plus page renders for the tables (the III and I spell-availability tables, the I potions page, the AU IBM card). US vs AU: an automated word-sequence diff of the rulebook pages found only OCR noise and column reordering (**not a line-by-line proofread**). The only real AU differences are the missing warranty/TOC-left page and the added command cards (PDF 16–19).

---

## 1. What III adds or changes vs I

### 1a. Character creation

| Topic | Phantasie I | Phantasie III | Change |
|---|---|---|---|
| Party / unit | Party of up to 6 (I PDF 9) | "a party of six adventurers" (III PDF 3 / pr.1) | same |
| Races | Human, Dwarf, Elf, Gnome, Halfling, plus "Random Creature" → Gnoll, Goblin, Kobold, Lizard Man, Minotaur, Ogre, Orc, Pixie, Sprite, Troll (I PDF 7) | Same 5 races plus the same 10 random creatures (III PDF 3–4 / pr.1–2) | same list, rewritten flavour text |
| Random-creature limits | "only be thieves or fighters", pay "hefty training fees" (I PDF 7) | "can only be Fighters and Thieves", "much larger training fees and stay in the back rooms at the inn" (III PDF 3–4 / pr.1–2) | inn back-room line is flavour only; no mechanic is stated |
| Classes | Fighter, Monk, Priest, Ranger, Thief, Wizard (I PDF 7) | same six (III PDF 4 / pr.2) | same |
| Attribute range | 3–22 (I PDF 8) | 3–22 (III PDF 4 / pr.3) | same |
| Max attributes by race | table (I PDF 8) | table (III PDF 4 / pr.3) | **identical values** (checked row by row) |
| Class attribute modifiers | Shown as absolute values around 10, e.g. Fighter 12/8/11/10/9, Wizard 6/13/8/10/11 (I PDF 8–9) | Shown as offsets, e.g. Fighter +2/−2/+1/0/−1, Wizard −4/+3/−2/0/+1 (III PDF 4 / pr.3) | **same numbers, different presentation** |
| LUCK | random 8–13 (I PDF 11) | random 8–13 (III PDF 6 / pr.7) | same |
| Skills | 8: attack, parry, swim, listen, spot trap, disarm trap, find item, pick lock (I PDF 12) | **9**: adds **Fire Bow** (III PDF 4 / pr.3). Attack can exceed 100 at high level (ibid.) | **new skill** |
| Racial/class skill bonuses | none stated | "Thieves get a free +30 to their Fire Bow Skill. Elves receive a +15 to Fire Bow Skill." (III PDF 14 / pr.23) | **new** |
| Social class | none | **New.** "four social classes are: Peasant Laborer, Craftsman, and Noble" (III PDF 5 / pr.4). It sets starting gold and the gold gained per level. Humans, Elves and Dwarves tend higher (pr.4); App. II adds Gnomes and Halflings (III PDF 14 / pr.23) | **new**; the class names are garbled (section 3 item 5) |
| Starting gold | "All characters start with 256 gold pieces" (I PDF 12) | depends on social class (III PDF 5 / pr.4) | **changed** |
| Pre-generated party | not stated as such (I PDF 9 only shows an example roster) | "A reasonable beginning party has been pre-generated and pre-equipped and is listed as the first 6 members" (III PDF 5 / pr.4; PDF 14 / pr.22) | **new**. The IBM card says: "No pregenerated characters exist on the disk on the IBM version." (III-AU PDF 18) |
| Name length | C64: max 9 characters (I-RC p.2; WW PDF 3) | not stated in III | — |

### 1b. Levels and training

| Topic | I | III |
|---|---|---|
| Training | Guild T)RAINING shows the XP needed and the cost; the party must carry gold (I PDF 10) | Same, and the cost comes "out of the party treasury" (III PDF 5 / pr.5) |
| Skill training | not described | **New.** Each level the adventurer trains **3 skills**; "may train in the same skill more than once, but the advantage gained is decreased each time" (III PDF 5 / pr.5; PDF 4 / pr.3; App. II PDF 14 / pr.23) |
| Gold on level-up | none | **New.** An amount of gold that depends on social class (III PDF 5 / pr.4; PDF 14 / pr.23) |
| XP needed | "determine the character's level" (I PDF 12) | "depends on his race, attributes, and class" (III PDF 6 / pr.7). Not stated in I, so this may only be a clarification |
| HP by class/level (CON 10) | table L1–15 (I PDF 17, checked on the render) | table L1–15 (III PDF 8 / pr.11) |
| Max MP by class/level | table (I PDF 20) | table (III PDF 10 / pr.14) |
| Level cap | the tables stop at 15 | the tables stop at 15 |

The HP and max-MP tables are **identical** between I and III. Neither manual states a hard level cap; the tables simply stop at 15.

### 1c. Combat

| Topic | I | III |
|---|---|---|
| Encounter (party) menu | 1 Fight, 2 Accept Surrender, 3 Greetings, 4 Beg for Mercy, 5 Flee (I PDF 14–15) | same 5 options (III PDF 7–8 / pr.9–10; keys on PDF 13 / pr.21). The menu is shown again after every round (III PDF 8 / pr.11; PDF 13 / pr.21) |
| Individual options | 1 Thrust, 2 Attack, 3 Slash, 4 Lunge, 5 Cast, 6 Parry, 7 Redo (I PDF 15) | 1 Thrust, 2 Attack, 3 Slash, 4 Lunge, **5 Aim**, 6 Spell, 7 Parry, **8 Fire (bow)**, **9 Other** → 1 Front, 2 Middle, 3 Back, 4 Time Lag/sound, 5 Redo (III PDF 8 / pr.10–11; keys on PDF 13 / pr.21) |
| Thrust | "one hard, solid swing, which is more likely to hit and does 1 to 2 more points of damage" (I PDF 15) | "one hard, accurate swing … greatest chance of hitting and does 1-2 points more damage" (III PDF 8 / pr.10) |
| Attack / Slash | Attack = 2 normal swings at rank 1; Slash = 3–4 swings, each less likely to hit, 1–2 less damage (I PDF 15) | Same swings. **New fallbacks**: if attack % is too low, Attack becomes Thrust and Slash becomes Attack (III PDF 8 / pr.11) |
| Lunge | fighters only, one swing at rank 2 (I PDF 15) | same. **New**: a non-fighter who Lunges Thrusts instead (III PDF 8 / pr.11) |
| Aim | — | **New.** One swing aimed at head or torso, lower chance to hit. Falls back to Thrust if attack % is too low (III PDF 8 / pr.11; PDF 9 / pr.13) |
| Ranged attacks | none in I | **Fire Bow** at a chosen rank (III PDF 8 / pr.11). Bows "replaced thrown rocks"; "easier to hit … and Armor is less effective vs. them". Position has no effect on bow hits (III PDF 14–15 / pr.23–24). Section 4 |
| Thieves | can thrust/attack/slash any rank (I PDF 15) | same (III PDF 8 / pr.11) |
| Party formation | none. Only monster ranks exist; rank 1 is closest and most attacks hit only rank 1 (I PDF 14) | **New: Front / Middle / Back.** Front gives a better chance to hit with hand weapons but an "even greater chance to be hit"; Back is harder to hit and much worse at hitting with hand weapons. The bonus is relative to the rest of the party ("If all of the adventurers are Back, then none of them get any bonuses") and does not affect spells or bows (III PDF 8 / pr.11; PDF 15 / pr.24) |
| Monster ranks | rank list with counts (I PDF 14) | rank, name, and **per-monster status U/S/D/K** (Unconscious/Sleeping/Down/Okay) (III PDF 15 / pr.24) |
| Status effects on hit chance | — | "Sleeping adventurers and monsters are automatically hit … Down … much easier to hit … but can still dodge" (III PDF 15 / pr.24) |
| Adventurer statuses | "health status" only (I PDF 11) | Okay, Down, Sleeping, Dead, Out (unconscious) (III PDF 7 / pr.8) |
| Body-part injury | none | **New**, section 4 |
| Accept Surrender | take gold and items "if the monsters do surrender" (I PDF 15) | "demands surrender from a depleted group"; tip: "it only works after you've killed a percentage of the enemy" (III PDF 8 / pr.10; PDF 15 / pr.25) |
| Beg for Mercy | lose gold and some newfound items (I PDF 15) | lose "all of the party's gold, some of its new items" (III PDF 8 / pr.10) |
| Flee | if it fails, "the monsters will continue to attack" (I PDF 15) | if it fails, "the monsters get free shots" (III PDF 8 / pr.10) |
| Monsters flee mid-fight | combat ends when monsters "have run away" (I PDF 15); no rule given | explicit rule: "At the end of every combat round the monsters will reorganize. If they are losing badly, they may attempt to flee." (III PDF 8 / pr.11) |
| Night encounters | the watch may hear monsters; Awaken helps (I PDF 15) | same, and the watch rolls its Listen % (III PDF 8 / pr.11) |
| Time Lag | wilderness command T, 0–9 (I PDF 13) | same, and also in combat via Other→4 (III PDF 7 / pr.9; PDF 8 / pr.11) |
| Redo | key 7 (I PDF 15) | moved into the Other submenu (III PDF 13 / pr.21) |

### 1d. Magic

| Topic | I | III |
|---|---|---|
| MP cost | 1–4 per spell (I PDF 18) | 1–4 (III PDF 9 / pr.13) |
| Max-MP growth | may rise after an inn rest (I PDF 11) | same, phrased as "cast spells while in the wilderness" (III PDF 6 / pr.7) or "after using Magic Points" (III PDF 9 / pr.13) |
| Number of spells learnable | limited by INT, class and level (I PDF 20) | same (III PDF 10 / pr.14) |
| Spell cost source | carried gold (I PDF 10) | party treasury (III PDF 5 / pr.5) |
| Spell types | E, C/A, C/P, N/T, N/W, N/WD (I PDF 18) | same codes (III PDF 10 / pr.14–15) |
| **#13–16** | Strength 1–4 (C/P; +1-2 / 1-4 / 1-7 / 1-11 damage) (I PDF 18–19) | **Arrow Flame 1 (#13), Party Arrow Flame 1 (#14), Arrow Flame 2 (#15), Party Arrow Flame 2 (#16)**, C/P, 1–4 MP. AF1 gives +50% accuracy and +15 bow points; AF2 gives +100% and +25 (III PDF 10–11 / pr.15–16) |
| **#53** | Vision (N/W, 1 MP), "see in all directions for hundreds of miles" (I PDF 18, 20) | **Removed.** "All areas are already mapped so spell 53 (Vision) is unnecessary" (III PDF 15 / pr.24). Absent from the III spell table (III PDF 10 / pr.15) |
| **#55, #56** | — (I's list ends at #54) | **New: Accuracy (#55, 3 MP, C/P), Party Accuracy (#56, 4 MP, C/P)**. They raise one adventurer's (or the whole party's) Fire Bow % "to 250" (III PDF 10 / pr.15) |
| **#57+** | — | "Divine Spells (#57+): these spells may only be acquired during the course of the game for special purposes" (III PDF 11 / pr.16) |
| Passive-spell example | Strength 3 cancels an enemy Weakness 4 (I PDF 18) | an enemy Slow is overridden by Quickness (III PDF 10 / pr.14). "Slow" is not on the III list, so this is presumably a monster-only spell (inferred) |
| Healing #1–4 | HP only: 1-9 / 1-27 / 1-57 / 1-99 (I PDF 19) | Same HP, **plus body areas** (tiers in section 4) (III PDF 11 / pr.16) |
| Fireflash #5–8 | one monster "close to the spellcaster" (I PDF 19) | one monster ("close to" dropped); **attacks a specific Body Area** (III PDF 11 / pr.16; PDF 9 / pr.12–13) |
| Flamebolt #37–40 | every monster, reduced by armour (I PDF 19) | same, **plus hits a Body Area** (III PDF 9 / pr.12–13) |
| Mindblast #33–36 | HP damage (I PDF 19) | "only affect the monster's Hit Points" (III PDF 9 / pr.13) |
| Other damage/effect numbers (Fireflash, Flamebolt, Mindblast, Protection, Quickness) | I PDF 19 | identical values (III PDF 11 / pr.16) |
| Summon Elemental #48 | elementals Fire, Water, Earth (guild #38–40); "will only serve a party of six adventurers" (I PDF 9, 20) | elementals **Mud, Mist, Air**; "One Elemental can be added to the party. It does not count as a member" (III PDF 5 / pr.4; PDF 11 / pr.16) |
| Resurrection #44 | raises the dead; CON permanently −1 or −2 (I PDF 19) | same (III PDF 11 / pr.16) |
| Awaken #51 | — | the manual names it "spell 51, Awaken" for night fights (III PDF 8 / pr.11) |

**Spells available by class and level** (both tables read from page renders: I PDF 20; III PDF 10 / pr.14). Everything not listed below is the same.

| Class | Change I → III |
|---|---|
| Wizard | adds #55 at L2 and #56 at L7 |
| Priest | loses #53 (Vision) at L1. Otherwise identical: L1 1, 51 |
| Monk | adds #55 at L1; #56 replaces #53 at L5 |
| Fighter | adds #55 at L4 and #56 at L10. Keeps #13 at L9 and #15 at L15, which are now Arrow Flame 1 and Arrow Flame 2 instead of Strength 1 and Strength 3 |
| Ranger | unchanged |
| Thief | unchanged (L4 41, L7 42, L10 18, L13 8) |
| All classes | every slot that held #13–16 now gives Arrow Flame spells instead of Strength (Wizard L2 13, L4 14, L11 15, L14 16; Monk L7 13, L12 14; Fighter L9 13, L15 15) |

### 1e. Items, equipment, inventory

| Topic | I | III |
|---|---|---|
| Slots | one weapon, one armour, one shield; never lost or stolen (I PDF 11) | one weapon, one shield, one suit of armour, **and one bow**; "never lost, stolen, or destroyed" (III PDF 6 / pr.6) |
| Carry limits | 9 items per character; party carries up to 90 found items (I PDF 12, 15) | 9 per character; 90 found items (III PDF 6 / pr.7; PDF 7 / pr.8; PDF 8 / pr.11) |
| Shields #1–20, Armour #21–40 | tables (I PDF 21) | **identical** names, points and STR requirements (III PDF 11 / pr.17) |
| Weapons | #41–100 = 60 melee weapons (Stick … God sword), requirement = STR×2+DEX, requirements 2…60 (I PDF 22) | **#41–60 are now 20 Bows** (Self Bow … God Bow), requirement = STR+DEX (III PDF 11 / pr.17). **#61–100 = 40 melee weapons** (Knife … God Sword), requirement = 2×STR+DEX, requirements 3…60 (III PDF 12 / pr.18). The melee list is condensed and renumbered. **Gone in III:** Stick, Small club, Small staff, Small mace (+1/+2), Small flail, Small hammer, Pitch fork, Maul, Morning Star, Spetum, God axe, Dagger +1…+4, Staff +1, Axe +1, Large Axe +1, Sword +3, Sword +7, Halbred +3/+4/+7. **New in III:** Knife +1/+2, Club +1/+2, Flail +2. Point values and requirements for shared names also differ (e.g. Knife: I 1 pt/req 4, III 1 pt/req 3) |
| Bow table oddity | — | #51 and #52 are both printed "Compound Bow +1" (III PDF 11 / pr.17). #52 is probably a misprint for +2 (inferred) |
| Magic equipment | — | "Magic Armor and Weapons have lower requirements" (III PDF 6 / pr.6; PDF 11 / pr.17) |
| Healing potions #101–110 | 1/4/9/16/25/**35**/49/64/81/100 HP (I PDF 23, checked on the render) | 1/4/9/16/25/**36**/49/64/81/100 HP, **plus body-area healing** (tiers in section 4) (III PDF 12 / pr.18, 36 checked on the render; table on PDF 9 / pr.12). I's 35 breaks the n² pattern and is probably a misprint (inferred) |
| Magic potions #111–120 | 3…30 MP (I PDF 23) | identical (III PDF 12 / pr.18) |
| Scrolls | 20 scrolls about Gelnor; read only in town (I PDF 23) | number not stated; some on sale at Armories; read only in a city (III PDF 12 / pr.18–19) |
| Quest artefacts | "Rings and Wand" (I PDF 23) | none named. Only the Wand of Nikademus is mentioned, as the one item that survives a transfer (III PDF 13 / pr.20) |
| Trade Items | — (Distribute only) | **New** town option T: give one item to a party member, or sell it by "trading it to adventurer 7" (III PDF 6 / pr.7; PDF 14 / pr.23) |
| Distribute and Sell | type 7 to sell (I PDF 12) | same function (III PDF 6 / pr.7) |

### 1f. Towns, services, prices

| Topic | I | III |
|---|---|---|
| Start town | Pelnor, town #1 of 11 on Gelnor (I PDF 8) | Pendragon, town #1 on Scandor; number of towns not stated (III PDF 5 / pr.4) |
| Facilities | Guild, Bank, Armory, Mystic, Inn, Leave (I PDF 8) | same set (III PDF 5 / pr.4) |
| Guild size | Apple 37 characters (I PDF 9); C64 21 (I PDF 2) | "Dozens of different adventurers can be recorded" (III PDF 5 / pr.4) |
| Guild commands | L, P, N, R, A + party S, T, D; no rename while in a party (I PDF 9–10) | List, Create, Purge, Add + party Spells, Training, Drop, **Rename a Party Member** (III PDF 5 / pr.4–5). The Apple key list puts R under Guild Members (III PDF 13 / pr.21) |
| Bank | per-character accounts; 65,000 cap per party and per account (I PDF 10) | same caps (III PDF 5 / pr.5). **New**: "A small banking fee will be charged at every deposit" (ibid.) |
| Armory | stock varies between visits (I PDF 10) | same (III PDF 6 / pr.6) |
| Mystic | party score (I PDF 11) | "overall score … how far they have proceeded on the quest and a rating of how experienced" (III PDF 6 / pr.7) |
| Inn | town inns free, about 2 weeks per stay; independent inns 250 GP + tax (I PDF 11, 13) | same prices (III PDF 6 / pr.7; PDF 7 / pr.9), **plus** the inn cures "1 Break or 2 Injuries" (III PDF 9 / pr.12) |
| Inspect | name, level, class, race, status, attributes, LUCK, age, HP, MP, bank gold, XP, score, skills, equipment, spells (I PDF 11–12) | adds **continent of origin, social class, Fire Bow skill, bow slot, body-part display** (III PDF 6–7 / pr.7–8) |

### 1g. Overland, dungeons, travel

| Topic | I | III |
|---|---|---|
| World map | Gelnor in 16 sections (I PDF 13) | Scandor in **25 sections, 5×5** (A–E west→east, I–V north→south). "Other planes, dimensions, have their own labels" (III PDF 7 / pr.9; PDF 15 / pr.24) |
| Map knowledge | not stated in the original I | "The adventurers are assumed to have maps of the wilderness, so all terrain in the area is shown" (III PDF 7 / pr.9) |
| Planes | — | "alternate planes of good and evil" (III PDF 7 / pr.8) |
| Wilderness menu | Cast, Health, Inspect, Use potion, Time lag, moves 3/N, E, S, W (I PDF 13) | Cast, Inspect, Use, Time Lag, moves. **No Health command** (Inspect shows HP and MP) (III PDF 7 / pr.9; PDF 13 / pr.21) |
| Terrain | path, desert, grass/forest, mountains, river/lake (swim or bridge); more insects and undead at night (I PDF 25) | path, desert, grass/forest, mountains, rivers/lakes/**seas**; no bridges mentioned; no encounters on water (III PDF 13 / pr.21) |
| Swimming | poor swimmers may take damage (I PDF 13) | per-adventurer Swim % roll; failure means damage and possible drowning (III PDF 4 / pr.3; PDF 7 / pr.9) |
| Dungeon commands | adds L)ook (I PDF 14) | same, plus a note that Look "will not see through walls" (III PDF 7 / pr.9) |
| Dungeon exit | "through the same door used when the party entered" (I PDF 14) | "may only leave the dungeon at an exit" (III PDF 7 / pr.9) |
| Dungeon re-entry | monsters re-stock (I PDF 14) | same text, plus new traps and new locks (III PDF 7 / pr.9) |
| Dungeon skills | Listen, Pick Lock, Find Item and Spot Trap are automatic; Disarm is chosen (I PDF 14) | same; some locks cannot be picked (III PDF 4 / pr.3; PDF 7 / pr.9) |
| Dungeon terrain | party, item, corridor, column, door (I PDF 14) | party, doors (some locked), corridor, rooms (some lairs), item dots, **secret doors** (found by walking into a wall) (III PDF 14 / pr.22) |
| Dungeon save | choose on exit whether to save status (I PDF 14) | same (III PDF 7 / pr.9). ST/Amiga keep several saved dungeons and offer a "Previously Saved Dungeon" reset; once-only items are not restored (III-AU PDF 17–18) |

### 1h. Monsters

| I | III |
|---|---|
| 80 types on Gelnor; example stats for skeletons, vipers and magic users (I PDF 17) | Count not stated. **New Monster Reputation Table** giving each monster a Fighting Skill (Low/Med/High/V High) and a Reputation (Good/Neutral/Bad) (III PDF 9 / pr.13). The text layer of that table is scrambled; it is **not** transcribed here. Monsters also have 6 body locations (III PDF 9 / pr.12) and status U/S/D/K (III PDF 15 / pr.24) |

### 1i. Death, resurrection, aging

| Topic | I | III |
|---|---|---|
| Death | HP reaches 0 (I PDF 17) | HP reaches 0, **or** Head or Torso removed (III PDF 8–9 / pr.11–12) |
| Resurrection | spell #44, CON −1 or −2 (I PDF 19) | same (III PDF 8 / pr.11; PDF 11 / pr.16) |
| Aging | lifespans: elves ~3000 y, dwarves/gnomes ~200, humans ~80, others 40–50 (I PDF 11) | identical text (III PDF 6 / pr.7) |

### 1j. Quest and endgame

| I | III |
|---|---|
| Black Knights, Nikademus; 20 scrolls, 9 rings and a wand (I PDF 6, 23; II PDF 16) | Nikademus on Scandor. Start at the Pendragon Archives south of town, find the wise man **Filmon**, and return to him after each adventure (III PDF 3 / pr.1; PDF 15 / pr.25). Scrolls are "the key to the whole puzzle" (ibid.). Divine spells #57+ are gained for "special purposes" (III PDF 11 / pr.16). The win condition is not described |

### 1k. Interface and commands

| I | III (Apple keys) |
|---|---|
| Town: A, G, M, N, B, L; C, U, D, I, S (I PDF 8, 11) | same plus **T Trade** (III PDF 13 / pr.21) |
| — | combat keys 1–9 and Other 1–5 (section 1c). Mouse on ST/Amiga/Mac; joystick on C64/Atari 800 (III PDF 14 / pr.22) |
| — | Melee screen shows stick figures with per-limb O/I/B/G (III PDF 7 / pr.8) |

IBM (DOS) III card (III-AU PDF 18): highlight-bar menus, Esc exits most menus, "Makedata" creates a Data Disk, "Makehard" installs to `C:\PH3`, run "Main", load a saved game via "Options menu → Load Game", no pre-generated characters, "All other rules are as stated in the rulebook." (The DUNGEONS / SPEED / COMBAT / SAVING / KEYBOARD paragraphs on the left half of AU PDF 18 belong to the Amiga card, not the IBM one, checked on the page render.)

### 1l. Save and disk handling

| I | III |
|---|---|
| Save only in town with S; continue by giving a guild number (I PDF 12, 23) | Save in town with S, **and** "Every time the party leaves a town the game is saved automatically" (III PDF 7 / pr.8; PDF 13 / pr.21). Apple: continue by giving one party member's guild number (III PDF 14 / pr.22) |
| Backup menu on the dungeon side: 1 Continue, 2 Init, 3 To backup, 4 From backup, 5 New game (I PDF 23) | Apple: you must make a Player disk and a Dungeon disk (about a dozen swaps each). Other Utilities: 1 Continue, 2 Init backup, 3 To backup, 4 From backup, 5 Start new game, **6 Transfer from Phantasie I or II** (III PDF 12–13 / pr.19–20) |
| — | ST/Amiga cards: "To save the game, click on the Save Option in the File Menu in Town. This is the only time a game will be saved." (III-AU PDF 17, 18). This **contradicts** the rulebook's autosave on leaving town. C64: saving writes the characters to disk, and rebooting restores the last save (III-AU PDF 16) |

### 1m. Caps and limits

| Limit | I | III |
|---|---|---|
| Party | 6 (I PDF 9) | 6, plus 1 elemental that does not count (III PDF 5 / pr.4) |
| Guild | 37 Apple / 21 C64 (I PDF 9, 2) | "Dozens" (III PDF 5 / pr.4) |
| Party gold | 65,000 (I PDF 10) | 65,000 (III PDF 5 / pr.5). ST: 1,000,000,000 and "held in common instead of each character having a private account". The Amiga card repeats the same paragraph, still saying "In the ST version" (III-AU PDF 16, 17) |
| Bank account | 65,000 (I PDF 10) | 65,000 (III PDF 5 / pr.5) |
| Items | 9 per character / 90 found (I PDF 12, 15) | same (III PDF 6–8) |
| Level tables | to 15 (I PDF 17, 20) | to 15 (III PDF 8, 10) |
| Attribute | 3–22 (I PDF 8) | 3–22 (III PDF 4 / pr.3) |
| Accuracy cap | — | Fire Bow raised "to 250" (III PDF 10 / pr.15) |

---

## 2. Character transfer (III)

**Where:** only in the **Apple-specific** section, as Other Utilities Menu option **6 "Transfer Characters From PHANTASIE I Or II"** (III PDF 12–13 / pr.19–20). The other cards:
- **C64 card**: only says "There are no Transfer Characters to or from Backup Disk options"; it does not mention I/II transfer (III-AU PDF 16).
- **ST, Amiga and IBM cards**: silent on transfer (III-AU PDF 16–18).
- **WizardWorks 1990 (DOS)**: silent.

Verbatim (III PDF 13 / pr.20):

> "Transfer Characters: this option allows you to transfer characters from PHANTASIE I or II for use in PHANTASIE III.
> If you decide to use old adventurers in PHANTASIE III then the transfer is the first action you should take after making your Player and Dungeon Disks. Warning: this process will have the following adverse effects:
> The transfer process will erase any characters already created in PHANTASIE III. Your characters will lose all of their items except the Wand of Nikademus. They also lose the majority of their money, experience points, and accumulated spells. They will retain any Divine spells (#57 and above) and their normal beginning spell by class.
> The transferred characters will begin at level 0, but should have enough remaining experience and money to train up several levels. At the start of the game these characters should go to the bank, withdraw their money, and then go to the Guild and train."

Also: "The Other Utilities Menu is used to Backup your characters onto another disk, or to transfer characters from PHANTASIE I or II into PHANTASIE III." (III PDF 12–13 / pr.19–20)

| Aspect | What III states |
|---|---|
| Procedure | Make the Player and Dungeon disks, then Utilities → Other Utilities → 6. Do it first |
| Erases | all existing III characters |
| Items | all lost except the **Wand of Nikademus** |
| Gold | "majority" lost (amount not given) |
| XP | "majority" lost; enough remains to "train up several levels" |
| Level | reset to **0** |
| Spells | lost, except Divine spells **#57+** and the "normal beginning spell by class" |
| Everything else | not stated (section 3) |

II's I→II text (II PDF 12 and 16) is almost the same: it keeps "divine spells (#55 and above). Wizards will keep spell #5 and Priests spell #1", the level becomes 0, and characters already on the II disk are erased (`09-trilogy-manual-p2.md` section 2). Both side by side: `09-trilogy-matrix.md` section 3.

---

## 3. Unclear or open in the manuals (no guesses)

The questions as the manuals leave them. Answers: Q1–9 and 15–19 from the DOS III code
(`reverse-engineering/dos/phantasie3/FORMAT.md` section 7); Q10 autosave decided as the manual (D3-028); Q12 one character
is enough for an elemental (`logic-specs/elementals.md`); Q13 bank fee (D3-025), skill tables (D3-032) and XP / costs
(`logic-specs/training.md`); Q20 not a conflict (`09-trilogy-matrix.md` section 4). Still open: Q11 (the Amiga's gold
cap) and Q14.

1. **Transfer platforms.** The Apple section is the only one that documents it. It is not known whether C64, ST, Amiga, Mac or IBM/DOS III have I/II transfer, or which I/II disk formats each accepts.
2. **Transfer details not stated:** attributes, race, class, HP, max MP, age, LUCK, skills, and body-part state. Also unstated:
   - the initial **Fire Bow** skill for imported characters;
   - how **social class** is assigned (I and II characters have none);
   - what happens to II's **Toss Rock** skill and II's amulets and orb;
   - whether I characters can go straight to III.
3. **Divine spell numbering.** III keeps #57+, while II's transfer says #55+, and in III #55/#56 are now Accuracy and Party Accuracy. It is not stated what happens to an I/II character's spell #55 or #56.
4. **"Normal beginning spell by class."** III L1 offers two spells per caster class (Wizard 5/9, Priest 1/51, Ranger 1/5, Monk 5/55), so which one is kept is not stated. II named only Wizard #5 and Priest #1.
5. **Social classes.** III says "four social classes are: Peasant Laborer, Craftsman, and Noble" (PDF 5 / pr.4) and "Peasant Laborer, Craftsman, or Noble" (PDF 14 / pr.23). That is three names for a stated four. WW (PDF 13) prints "Peasant, Laborer, Craftsman, or Noble". The race tendencies also differ: pr.4 names Humans, Elves and Dwarves; pr.23 adds Gnomes and Halflings. Gold amounts per class are not given.
6. **Body-area count.** Inspect and the screen list 4 areas (Head, Torso, Arms, Legs) (PDF 7 / pr.8); the melee section lists 6 (L/R arm, L/R leg) (PDF 9 / pr.12).
7. **Broken-limb rules.** The bow ban is stated for "Broken" arms or legs only, so it is unclear whether Gone also blocks it. "Parry is the only combat option" is stated for a Broken right arm only, while Gone blocks spells and weapons. Left-arm effects are not stated (for example on parry or shield).
8. **"Enough hit points to an arm to kill him"** (PDF 9 / pr.12). Whether HP damage is tracked per location is not explained.
9. **Hit-location odds.** Only described qualitatively: bow is best at vital areas, spell is equal across areas, hand weapon is worst, especially vs tall monsters.
10. **Autosave.** The rulebook autosaves on leaving town; the ST/Amiga cards say File→Save is "the only time" a game is saved. The IBM card says "all other rules are as stated in the rulebook".
11. **Amiga gold cap.** The Amiga card repeats the ST text word for word ("In the ST version … 1,000,000,000"), so whether Amiga has the same cap is unclear.
12. **Elementals.** I: "will only serve a party of six". III: "does not count as a member". Whether III requires a full party of six to summon is not stated.
13. **Bank deposit fee** amount; **XP formula** (race/attributes/class); **skill-training increments**; **spell-learning cost**: none of these is given.
14. **Rename.** It is listed under Party Commands in the general rules and under Guild Members in the Apple keys.
15. **"Slow" spell.** It is used in the passive-spell example but is not on the III spell list.
16. **Bow #52** is printed "Compound Bow +1", a duplicate of #51.
17. **Endgame or win condition** is not described. Neither is the number of towns or scrolls in III.
18. **Inn and Breaks.** III PDF 7 / pr.8: "Injured Body Areas can be cured with rest in the Inn. More drastic problems can be cured with high level Healing Spells or powerful Healing Potions." III PDF 9 / pr.12 and PDF 14 / pr.23: an inn stay "will cure 1 Break or 2 Injuries". The two disagree on whether rest fixes a Break.
19. **Thief reach.** The class text says thieves "sneak into the second rank" (III PDF 4 / pr.2); the melee text says "any rank, not just the first" (III PDF 8 / pr.11). I says "any rank" (I PDF 15).
20. **Map knowledge in I.** The original I manual is silent on fog of war; WW (below) says the map starts mostly unknown.

---

## 4. Rock throwing and body-part injury

**Rock throwing is not a III feature.** It is not in I either: the combat list on I PDF 15 has no rock option. Rock tossing is a **Phantasie II** feature (II PDF 16):

> "Combat/Rock Tossing (p. 10): The combat menu of PHANTASIE II contains a new feature: 7) Toss Rock. Rocks may be thrown at any enemy rank. Each character has an unlimited supply of rocks. Accuracy with this new weapon depends on the Toss Rock skill level of each individual character."

III **removes rocks and replaces them with bows** (III PDF 14 / pr.23):

> "Bows: bows have replaced thrown rocks as every man's ranged weapon. It is easier to hit an opponent with a bow, and Armor is less effective vs. them. Thieves get a free +30 to their Fire Bow Skill. Elves receive a +15 to Fire Bow Skill."

III bow rules:
- Fire at a chosen rank (PDF 8 / pr.11).
- Separate bow slot (PDF 6 / pr.6).
- Bows #41–60 require STR+DEX (PDF 11 / pr.17).
- Party position has no effect on bow hits (PDF 15 / pr.24).
- Any Broken arm or leg prevents firing (PDF 9 / pr.13).
- Bows have the best chance to hit Head or Torso (PDF 9 / pr.13).
- Spells #13–16 (Arrow Flame) and #55/#56 (Accuracy) boost bow fire (PDF 10–11 / pr.15–16).

**Body-part injury is described in III, and it is new in III.** It is not in I or II. Rules as given (III PDF 7 / pr.8; PDF 9 / pr.12–13; App. II PDF 14–15 / pr.23–24):

- **Locations:** 6 (Head, Torso, Left Arm, Right Arm, Left Leg, Right Leg) on every character **and monster**. Each is **Okay (O), Injured (I), Broken (B) or Gone (G)**, shown as a stick figure.
- **Equivalences:** Gone = 1 Broken + 1 Injured, or 3 Injured; Broken = 2 Injured.
- **Healing tiers** (each heal fixes "the most vital area it can"):

| Healing spell | Healing potion | Areas healed |
|---|---|---|
| I | 1–3 | 1 Injury |
| II | 4–6 | 1 Break or 2 Injuries |
| III | 7–9 | 1 Gone, 1 Break & 1 Injury, etc. |
| IV | 10 | more than 1 Gone |

- **Inn:** restores all HP and cures 1 Break or 2 Injuries. Town inns are free; independent inns cost 250.
- **HP are separate from body areas.** A character can be at full HP with a limb Gone, or have no damaged limbs and low HP.
- **Death** comes from Head or Torso removed, or "from enough hit points to an arm to kill him", as well as from HP reaching 0.
- **Spells and locations:** Fireflash (#5–8) and Flamebolt (#37–40) hit a specific Body Area; Mindblast (#33–36) affects HP only.
- **Hit-location odds:** bows have the best chance at Head or Torso, spells are equal across all areas, and hand weapons are worst, especially vs very tall monsters.
- **Aim** (combat option 5): one blow aimed at head or torso, at a reduced chance to hit.
- **Restrictions:**
  - any Arm or Leg Broken → no bow;
  - Right Arm Broken or Gone → no spells and no weapon swings;
  - Broken Right Arm → Parry is the only option.
- **Flavour:** "A limb that's 'Gone' is probably just so broken that normal rest won't fix it" (PDF 15 / pr.25).

---

## 5. WizardWorks 1990 (I + III, DOS/C64): where it differs from the originals

**About I (relevant to the I baseline; the Thrust and wilderness-map points are settled as not conflicts in
`09-trilogy-matrix.md` section 4):**
- **Thrust** is "less likely to hit and does 1 to 2 more points of damage" (WW PDF 9). Original I: "more likely to hit" (I PDF 15). This is a direct contradiction.
- **Wilderness map**: "The area near your starting point is known to you and visible. When you travel to unknown lands, the map is blank. When you pass through, it becomes visible." (WW PDF 8). The original I does not say this.
- **IBM features** (WW PDF 2):
  - highlight-bar menus and Esc;
  - an **"Autosave" town option** that saves on leaving town until toggled off or reboot;
  - Backspace to enter a partial bank withdrawal;
  - Enter/Esc show and hide the outdoor menu bar;
  - combat speed setting persists.
- **Inspect** is in the Guild and can inspect all Guild members (WW PDF 3). I has it under Town Other Options.
- The **Inn** is "the only place where you may distribute and sell items" (WW PDF 3, C64 section).
- **Guild size**: both "maximum of 21" (WW PDF 3) and "As many as 37" (WW PDF 6) appear.
- **Town other-options keys** are C, U, G, P: G = save, P = print attributes (WW PDF 3, C64).
- The **combat list** omits Redo/Cast keys in the C64 section; options persist between rounds (WW PDF 4).

**About III:**
- WW gives only an intro, the Differences appendix and the Tips.
- The rule text for III is replaced by "IBM version: (see Phantasie instruction manual)" (WW PDF 12).
- WW prints **I's spell list and descriptions** (Strength #13–16, Vision #53, Fire/Water/Earth elementals; WW PDF 10–12). It has **no Arrow Flame, Accuracy or Party Accuracy entries and no III spell or equipment tables**.
- The Differences heading names only "PHANTASIE and THE WRATH OF NIKODEMUS", without II (WW PDF 12).
- Its Tips still mention the Monster Reputation Table, which WW does not include (WW PDF 14).
- The C64 III text says "If you have created a character, you will automatically start the game in the town of Pendragon" (WW PDF 12). AU has "If you have **not** created a character" (III-AU PDF 16), so a word was dropped.
- Social class is printed as four names (section 3 item 5).
- There is nothing about character transfer.
