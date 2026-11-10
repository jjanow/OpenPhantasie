# DIFFERENCES-III — Phantasie III manual vs game code

Every place where the **Phantasie III manual** (`docs/manuals/phantasie3/phantasie3-manual.pdf`; comparison in
`docs/plan/09-trilogy-manual-p3.md`) and the **game code** disagree, what the user decided, and what the remaster does.
Same rules as `DIFFERENCES.md` (Phantasie I): the manual is the source of truth, every conflict goes to the user, who
decides; between versions DOS wins over Amiga on numbers. Keep each entry to **Manual · Code · Decision · Remaster ·
Side effects** (plus the spec). IDs are `D3-nnn`.

**Status:** `PENDING` = raised, no decision yet · `DECIDED: <side>` + `built` / `not built` · `NOT A CONFLICT`.

| ID | Topic | Status |
|---|---|---|
| D3-001 | II → III transfer: the wand of Nikademus is dropped by DOS III | DECIDED: manual · built (s147, `p3.transfer-wand`) |
| D3-002 | I → III transfer: DOS keeps no divine spells | DECIDED: manual (DIVINE I → Divine Aid) · built (s147, `p3.transfer-divine`) |
| D3-003 | I → III transfer: DOS adds to the roster instead of erasing it | DECIDED: DOS (add) · built (s147, `p3.transfer-roster`) |
| D3-004 | Transfer source: DOS (and Amiga) take Phantasie I characters only, not II | DECIDED: manual (I or II) · built (s147, `p3.transfer-source`) |
| D3-005 | Item names: #81/#82 "Dagger" vs "Knife", #52 "+2" vs "+1", Armour/Halberd spellings | DECIDED: DOS names · built (data) |
| D3-006 | Social classes: four in the manual, six names in the code (Darklord, Superhero) | NOT A CONFLICT (endgame titles) |
| D3-007 | I → III transfer: an Undead character starts at level 20, not 0 | DECIDED: manual (level 0) · built (s147, `p3.transfer-level`) |
| D3-010 | Slash with too low an Attack %: DOS falls back to Thrust, manual to Attack | DECIDED: manual (Slash → Attack; DOS Thrust as option) · built |
| D3-011 | Lunge: DOS lets Thieves lunge too; manual "only for fighters" | DECIDED: manual (Fighters only; DOS Thief Lunge as option) · built |
| D3-012 | Thieves cannot Attack the second rank in DOS (Thrust and Slash only) | DECIDED: manual (Thief Attack any rank; DOS as option) · built |
| D3-013 | Aim: no reduced chance to hit and no Attack % minimum in DOS | DECIDED: manual, user values (min 150, Attack %/2; DOS as option) · built |
| D3-014 | Healing IV / Potion 10 cure at most one Gone area; manual "more than 1 Gone" | DECIDED: manual (tier IV = 6 cure points; DOS 4 as option) · built |
| D3-015 | Town inns heal every Injured and Broken area; manual "1 Break or 2 Injuries" | DECIDED: manual (every inn 1 Break or 2 Injuries; DOS as option) · built |
| D3-016 | I → III transfer: DOS starts transferred characters at level 1, not 0 (new evidence for D3-007) | DECIDED: literal level 0 (D3-007 stands; DOS level 1 as option) · built (s147) |
| D3-017 | Arrow Flame replaces the bow damage die (points + 15/25) and can lower damage | DECIDED: manual (bow points +15/+25 into the normal range; DOS as option) · built |
| D3-018 | Monster melee to-hit: worn armour makes a character easier to hit (verified live s81) | DECIDED: manual (sign fixed; DOS as option) · built |
| D3-019 | Down (and Out) targets are always hit; manual "much easier to hit … but can still dodge" | DECIDED: manual, user values (Down/Out chance ×2, cap 95; DOS as option) · built |
| D3-020 | Party found-item limit: 60 in DOS, 90 in the manual | DECIDED: manual 90 (options: unlimited, DOS 60) · built |
| D3-021 | Monster status letters: DOS prints "O" for an unconscious monster, the manual "U" | DECIDED: manual U (DOS O as option) · built |
| D3-022 | Hit points ignore Constitution in DOS (creation and training) | DECIDED: manual, I's CON rule (DOS as option) · built |
| D3-023 | No gold for going up a level (manual: by social class) | DECIDED: manual, social class · 256 per level (DOS none as option) · built |
| D3-024 | Monsters 44–79 can never be greeted, begged or made to surrender, and are hard or impossible to flee | DECIDED: manual, no id exceptions; final bosses block Flee (options: DOS block, bosses fleeable); Mercy items 50 % per new item, gold-only option (s89) · built (s91) |
| D3-025 | Bank deposit: no fee, equal split instead of 1/2/3 shares, withdraw all only | DECIDED: manual shares, fee = Apple II truncation (s89); options no-fee (I's deposit) / DOS even split · built (s91); Amiga-style bank windows (s157) |
| D3-026 | No 65,000 gold cap on the party treasury or bank accounts | DECIDED: manual 65,000 (options: DOS uncapped) · built |
| D3-027 | Trade Items is missing in DOS (Amiga has it) | DECIDED: manual Trade (give + sell at 75 %), options DOS none / Amiga give-only · built (s91; Client Trade screen); any time with `modern-inventory` (s178) |
| D3-028 | No autosave on leaving town in DOS | DECIDED: manual autosave on leaving town (DOS menu-only as option) · built |
| D3-029 | Guild roster: 20 slots including the 3 elementals (17 characters); manual "dozens" | DECIDED: manual, unlimited roster by default (DOS 17 as option) · built |
| D3-030 | How the I → III transfer is started: DOS only from floppy and only with a saved game; manual a utilities option | DECIDED: manual, transfer as a utilities / menu option · built (s147: game menu Transfer from I / II) |
| D3-031 | Healing order: after a cure DOS keeps scanning from the next area, so an Injured Head can be skipped for a later area | DECIDED: manual, most vital first (DOS order as option `p3.heal-order`) · built |
| D3-032 | Skill-training tables: five DOS cells differ from I (Thief Pick Lock L3 35 vs 55, Ranger Attack L15 25 vs 250, Ranger Parry L15 60 vs 85, Parry table one byte short so Monk/Wizard read a level ahead) | DECIDED: I's values (data errors; DOS as read as option) · built |
| D3-033 | Armory stock never changes by itself in DOS; manual: weapons and potions can change each visit | DECIDED: Apple II restock minus Light Key / Burnt Gem; DOS no-restock option (s89) · built (s91) |
| D3-034 | Kobold maximum CHA: 3 in DOS's race table, 13 in the manual | DECIDED: manual 13, DOS 3 = data bug, no option (s89) · built |
| D3-035 | Transportation (#54): DOS offers only the 14 Scandor towns; the remaster reads "interdimensionally" as every town 1–17 | NOT A CONFLICT: DOS 1–14 (s89) · built (s91); QoL option visited plane towns built (`p3.transport-planes`, off) |
| D3-036 | Parry: DOS reads the option saved at fight start, so Parry picked mid-fight does nothing that fight; no damage reduction | DECIDED: Apple II roll + one-hit reduction, plus shield off hit chance; DOS option (s89) · built (s91) |
| D3-037 | After a successful Greet / Accept Surrender / Mercy / Flee, DOS still plays one full round | DECIDED: manual, fight ends at once; DOS extra round = bug, no option (s89) · built |
| D3-038 | Re-entering a dungeon: DOS keeps a saved map per dungeon and asks "[original text]", no time factor; manual: monsters restock unless the party returns shortly | DECIDED (s96): I's D-027 rule per dungeon (YES = back soon, NO = restocked), no time factor |
| D3-039 | Bow spells over each other: the last one sets the Fire Bow % (Arrow Flame after Accuracy lowers 250; Accuracy over a high Arrow Flame II lowers it) | DECIDED (s96): manual, no spell lowers the % (`p3.bow-spells`) |
| D3-040 | Purge: DOS refuses a party member ("[original text]"); the manual's Purge drops the adventurer "from the guild list from the party" | DECIDED: manual, elemental as III's Drop · built (s172: `elemental-purge` = `game` follows the preset, `p3.purge` folded in) |
| D3-041 | Lifespans: the manual's old age (decline, death); DOS writes a maximum age at creation but nothing reads it | DECIDED: manual (I's lifespans) · built (`p3.lifespans`) |
| D3-065 | Title page: the manual says "Hit any key to get to the Initial Menu"; the Amiga shows the title only while the game loads and clears it with no key | DECIDED (user s103): wait for a key (manual) · built; tune plays on after the splash (user s157) · built |
| D3-067 | Wolf / Mud Elemental pictures: the Amiga has none (byte 27 ≥ 48), shows the earlier fight's picture or hangs on the first fight after boot | DECIDED (user s126): bug, fixed with borrowed pictures (Wolf = Wild Dog, Mud Elemental = the party elemental), no option · built. s161: the DOS CGA pack borrows too (DOS's Skeleton / Forest Elf replaced; Troll Baby = the party Troll) · built |
| D3-066 | Sound switch: the manual toggles sound with the Time Lag; DOS has a Sound menu entry; the Amiga has no sound switch (its "Speed" menu sets 3 speeds only) | DECIDED (user s111): manual — sound toggled on the Time Lag prompt · built (s112) |
| D3-068 | Dungeon Yes / No questions: DOS lights No on "[original text]" (and the exit / save-map questions); the Amiga's window 1 has no highlight and Return = OK (continue / take it) | DECIDED: Amiga under AMIGA SCREENS (user, s135) · built (s134) |
| D3-069 | Inn magic gain: the manual's "characters who have cast spells while in the wilderness"; DOS rolls for every living member on the first stay after a fight | DECIDED (user s137): manual (a cast outside town) · built |
| D3-070 | Outside-town keys: the manual's Adventure Menu has E = move east (3 / N W E S, I, U, T, L); the Amiga's bar has E = Examine Member, P = Party Status, and no letter moves | DECIDED · manual: E moves east |
| D3-071 | XP pool / found items: the manual divides the experience "when they reach a town" (the Amiga and DOS on town entry); the remaster paid out only by the town's Distribute | DECIDED (user s142): manual · paid out on town entry (DOS order, s143) |
| D3-072 | I / II → III transfer XP: the manual's characters keep "enough remaining experience … to train up several levels"; DOS gives 100 XP per old level (one or two trainings for most) | DECIDED: DOS (user s148) · built (s147) |
| D3-073 | Armory list: the manual lists an item the buyer cannot use "as Unusable" and shows the point value of equipment; DOS prints "N/A" in the Pt Val column, and also for the three requirement-0 items (Cloth +1, Robes +1, Self Bow) | DECIDED (user s174): manual · built s174 |

---

## D3-001 — II → III transfer: the wand of Nikademus

- **Manual:** "Your characters will lose all of their items except the Wand of Nikademus." (III PDF 13 / pr.20.)
- **Code:** III has a wand item (id 180) `[C]`. **Apple III** turns II's wand flag (a character's Phantasie I score over
  99) into a wand bit (byte 126, bit 3) for every transferred character if anyone in the roster had it, and its dungeon
  scripts test and set that bit ("YOU DO NOT HAVE THE WAND, STUPID", "WAVE THE WAND AND SAY 'STRATICON UBLE'") `[C]` bytes,
  `[~]` which event is which. **DOS III**'s transfer copies no items and no flag, only the score (for "heroes of Gelnor")
  `[C]`, whole call tree checked. Amiga III imports from Phantasie I only, not decoded `[?]`.
- **Decision:** (user s77) manual: the wand is kept (Apple III's rule: if anyone in the roster has it, every transferred
  character gets the wand bit).
- **Remaster (s147):** `p3.transfer-wand` = `manual` (default): when any character of the source roster has a Phantasie I
  score above 99 (II's "WAND OF NIK" flag, D2-001), every transferred character carries item 180 and gets quest bit 2
  (`Phantasie3Campaign.AdmitTransferred`, `rule-tables.json` `transfer`); `dos` drops it.
- **Side effects:** decides whether a party carried I → II → III can use the wand's dungeon events. (s82) In DOS III,
  "having the wand" for those events is quest bit 2 of record `+0x8a` (score weight 8; it also locks the item in the
  inventory), set when the wand is taken in the archives or the Castle of Dark — not item 180 itself. A "wand kept"
  transfer therefore has to set that bit as well as give the item, or the Castle of Dark's centre-room options (break /
  wave the wand → the Netherworld exit) stay shut (`reverse-engineering/dos/phantasie3/logic-specs/dungeon-events.md` §4–5). Found s76 (static,
  6502 and x86); notes in `reverse-engineering/cross-version/wand-and-transfer.md`.

## D3-002 — I → III transfer: divine spells

- **Manual:** transferred characters "will retain any Divine spells (#57 and above) and their normal beginning spell by
  class" (III PDF 13 / pr.20).
- **Code:** DOS III's converter (`MAIN.EXE` overlay 1) copies a fixed list of I record words (stats, status, score,
  level, class, race, bank gold, age, name: `DS:0x5e`) and no spell field `[C]`; spells come only from the class kit the
  review screen (ovl2 `0x227d`) writes `[~]`. III's only divine spell is #57 Divine Aid; I has DIVINE I at #58. So a
  transferred character keeps no divine spell. Static; not run in DOSBox.
  **Verified live s82** (DOSBox-X): priesto in the Phantasie I `GUILD.DAT` was given DIVINE I (I spell slot 57, `@210`
  = 1); after the transfer the III record had only spell #1 Healing I and `+0xf0` (Divine Aid) = 0. A Dead I wizard
  came over Dead with only #5 Fireflash I (`screens-s82/s82_d3002_transfer_priesto_divine1.png`). In III, Divine Aid is
  otherwise only sold by the wizard in the Gnome catacombs (`logic-specs/dungeon-events.md` §6).
- **Decision:** (user s79) manual: divine spells are kept; I's DIVINE I (#58) maps to III's #57 Divine Aid.
- **Remaster (s147):** `p3.transfer-divine` = `manual`: a known DIVINE I (I or II) or II's DIVINE II gives Divine Aid
  (`+0xf0`); `dos` keeps none.
- **Side effects:** I's DIVINE I has no III counterpart (III's table ends at #57 Divine Aid), so "keeping" an I divine
  spell needs a mapping rule. Spec: `reverse-engineering/dos/phantasie3/FORMAT.md` §6.

## D3-003 — I → III transfer: erase or add

- **Manual:** "The transfer process will erase any characters already created in PHANTASIE III." (III PDF 13 / pr.20.)
- **Code:** DOS asks for "the data disk on to which these characters will be added" and inserts each accepted character
  with the normal roster insert (`0834:1d5b`), stopping when the roster is full (counter = 20 − roster count) `[C]`
  static. Existing III characters stay.
  **Verified live s82:** a III data disk with 10 roster records (7 characters + Air / Mist / Mud) took two transferred
  characters: roster count 10 → 12, all old records kept; the new ones were inserted before the three elementals
  ("Insert the data disk on to which these characters will be added.", `screens-s82/s82_d3003_transfer_added_prompt.png`).
- **Decision:** (user s79) DOS: transferred characters are added; nothing is erased. The user also wants no Guild
  roster limit, as a QoL option (P3).
- **Remaster (s147):** `p3.transfer-roster` = `add` in every preset; `erase` (the manual's text) erases every III character
  and the party first, in a town only. A character whose name is in the Guild, or who finds no room (D3-029), stays behind.
- **Side effects:** none beyond roster space. Spec: `FORMAT.md` §6.

## D3-004 — Transfer source: Phantasie I or II

- **Manual:** "transfer characters from PHANTASIE I or II for use in PHANTASIE III" (Apple section, III PDF 13 / pr.20;
  the IBM and Amiga cards say nothing).
- **Code:** DOS asks at start-up "Would you like to transfer older characters saved from Phantasie I?" and reads I's
  282-B `guild.dat` `[C]`; a II branch (318-B records) exists but runs only if the first open fails and then reopens the I
  file, so it looks unreachable `[~]` (s76/s78). Amiga III's strings offer Phantasie I only ("Transfer Phantasie I
  characters", "This [original text] disk!") `[C]` strings. Apple III takes I or II (s76, `wand-and-transfer.md`).
  **Live s82:** with no `GUILD.DAT` but a `FINAL2.PIC` on the disk (the II test, ovl1 `0x128`), the game answered "Not a
  Phantasie I data disk!" and transferred nothing. The II branch picks mode 2 only after `guild.dat` failed to open
  and then opens `Guild.dat` again (ovl1 `0x0`), so on DOS it cannot succeed (`screens-s82/s82_d3004_final2pic_no_guild_not_p1_disk.png`).
  Related (s82): III stores the Phantasie II score at `+0x18` (Filmon and the archive altar test it ≥ 200), so a II → III
  transfer would need to write it (`FORMAT.md` §5).
- **Decision:** (user s79) manual: I or II characters can enter III (as Apple III).
- **Remaster (s147):** `p3.transfer-source` = `manual`: III's game menu has Transfer from I and Transfer from II (the
  remaster's own I / II saves); `dos` shows only the I row. A II character brings II's quest score to `+0x18` and the
  origin Ferronrah (Gelnor when II's own record says it came from I).
- **Side effects:** decides whether II characters (ST records) can enter III; the remaster can convert II → III from
  its own model either way.

## D3-005 — Item names

- **Manual:** #81 "Knife +1", #82 "Knife + 2" (III PDF 12 / pr.18); #51 and #52 both "Compound Bow +1" (PDF 11 / pr.17);
  "God Armor", "Halbred".
- **Code:** DOS item names (string table 150 + id) `[C]`: #81/#82 "Dagger +1" / "Dagger +2"; #52 "Compound Bow +2";
  "God Armour", "Halberd". Points and requirements of all 100 equipment ids equal the manual's (#81/#82: points 2/3,
  requirement 3/6; #52: 12/22).
- **Decision:** (user s79) DOS names: "Dagger +1/+2", "Compound Bow +2", "God Armour", "Halberd". #52 and the
  spellings are not a conflict (manual typos); Dagger vs Knife is decided for DOS.
- **Remaster:** III's item data uses the DOS names (`GameData/phantasie3/items.json`, built s82).
- **Side effects:** names only (shop lists, sheet). Spec: `reverse-engineering/dos/phantasie3/FORMAT.md` §4.1.

## D3-006 — Social classes: four or six

- **Manual:** "four social classes are: Peasant Laborer, Craftsman, and Noble" (III PDF 5 / pr.4; App. II pr.23); the
  copy-protection quiz asks "How many different social classes are there? (page 23)".
- **Code:** the social-class name table has six entries: Peasant, Labourer, Craftsman, Noble, **Darklord**, **Superhero**
  `[C]`. The endgame text ("[original text] Pluto./Zeus. … I grant thee the highest status a mortal can ever
  attain. You are now …") suggests 4/5 are titles given at the end, for the evil / good side `[~]`; two characters in the
  Amiga save hold 5. The I → III transfer sets every character to Noble (3) `[C]`.
- **Decision:** (user s79) NOT A CONFLICT: four classes at creation; Darklord / Superhero are endgame titles.
- **Remaster:** as the code; III's endgame spec pins down how 4/5 are reached (built: the ending sets 4 / 5,
  `Phantasie3Campaign.Combat.cs`).
- **Side effects:** none at creation; the endgame spec decides how 4/5 are reached. Spec: `FORMAT.md` §3.

## D3-007 — I → III transfer: Undead characters start at level 20

- **Manual:** "The transferred characters will begin at level 0" (III PDF 13 / pr.20).
- **Code:** DOS III's converter maps I's UNDEAD race (15) to III's Undead (17) and then sets the new record's level word
  (+0x74) to 20 for that race (ovl1 `0x434`) `[C]` static, verified live s81 (D3-016); everyone else starts at 0 (corrected s79 T4: at level 1, D3-016). (DOS I also sets level 20 when a
  character turns undead.)
- **Decision:** (user s79) manual: every transferred character starts at level 0, Undead too.
- **Remaster (s147):** `p3.transfer-level` = `manual`: level 0 for all; `dos`: level 1, Undead 20.
- **Side effects:** only characters that turned undead in I. Spec: `FORMAT.md` §6.

## D3-010 — Slash with too low an Attack %

- **Manual:** "If an adventurer does not have a high enough attack percentage to Slash he will Attack instead."
  (III PDF 8 / pr.11.)
- **Code:** the combat menu (root `0xfc41`) accepts Slash at Attack % ≥ 150 and otherwise stores **Thrust** (action 3),
  the same fallback Attack uses below 76 `[C]` static (`logic-specs/combat-actions-and-ranks.md` §2).
  **Verified live s82** (DOSBox-X): fighto (Fighter, Attack % set to 100) chose Slash; the combat list showed
  "Thrusts (R1)" and the round's action array `FAR:0x228` held 3 (Thrust) (`reverse-engineering/dos/phantasie3/screens-s82/s82_d3010_slash_attack100_becomes_thrust.png`).
- **Decision (user s81):** manual: below Attack % 150 a Slash becomes an Attack (which itself becomes a Thrust below 76). DOS's direct Thrust as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.slash-fallback`, manual default, `dos` in Original (DOS); `P3Combat.Choose`. Wired into `Phantasie3Campaign`.
- **Side effects:** characters with Attack 76–149 get one Thrust instead of two Attack swings.

## D3-011 — Lunge for Thieves

- **Manual:** "Lunge: this option is only for fighters … If a non-fighter attempts a Lunge he will Thrust instead."
  (III PDF 8 / pr.11.)
- **Code:** Lunge (R2) is allowed when a second monster rank exists and the class is **Fighter or Thief** (root
  `0xfc41`); others Thrust `[C]` static.
  **Verified live s82:** with a second monster rank present (Wild Cats behind Giant Bees), thiefo's Lunge became
  "Lunges (R2)" (no rank prompt), while monko (Monk) and rango (Ranger) got "Thrusts (R1)"; action array 14 / 3 / 3
  (`screens-s82/s82_d3010_011_013_menu_results.png`).
- **Decision (user s81):** manual: only Fighters can Lunge; others (Thieves too) Thrust. DOS's Thief Lunge as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.lunge`, manual default, `dos` in Original (DOS); `P3Combat.Choose`. Wired into `Phantasie3Campaign`.
- **Side effects:** Thieves get a rank-2 swing with the +1 damage of Lunge.

## D3-012 — Thieves and the Attack option at rank 2

- **Manual:** "Thieves … can thrust, attack, or slash at monsters in any rank, not just the first." (III PDF 8 / pr.11.)
- **Code:** a Thief chooses the rank for Thrust and Slash ("[original text]", root `0x10cf3`), but Attack is always
  rank 1; the string "Attacks (R2)" exists but no path stores it `[C]` static.
  **Verified live s82:** thiefo's Attack was stored as "Attacks (R1)" without a rank question; Thrust asked "Target
  which rank?" (`screens-s82/s82_d3012_*.png`).
- **Decision (user s81):** manual: a Thief chooses the rank for Attack as for Thrust and Slash ("Attacks (R2)"). DOS's rank-1-only Attack as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.thief-attack`, manual default, `dos` in Original (DOS); `P3Combat.Choose` (a manual-rule Thief keeps the chosen rank through the Attack / Thrust fallbacks; manual-silent). Wired into `Phantasie3Campaign`.
- **Side effects:** a Thief's two-swing Attack never reaches rank 2.

## D3-013 — Aim: chance to hit and minimum Attack %

- **Manual:** "An aimed blow has less chance of hitting than a normal attack. If the adventurer does not have a high
  enough attack percentage to Aim his blow, he will Thrust instead." (III PDF 8 / pr.11.)
- **Code:** Aim is always accepted (no Attack % test in root `0xfc41`). Resolved (root `0xc03b`) as one swing at the full
  Attack %, damage without modifier; only the hit-location row changes (row 3: head 40 %, torso 17 %) `[C]` static.
  Normal Attack uses Attack % / 2 for each of its two swings, so an aimed swing is *more* likely to hit than one Attack
  swing and as likely as a Thrust.
  **Verified live s82:** Aim Blow was accepted at Attack % 100 (crusho) and at 60 (gobbo, below even Attack's 76):
  "Aims Blow (R1)", action code 6 for both (`screens-s82/s82_d3013_aim_attack60_accepted.png`).
- **Decision (user s81):** manual, with values the user chose because the manual gives none: Aim needs Attack % ≥ 150 (the Slash gate), otherwise it becomes a Thrust; an aimed swing hits at Attack % / 2 (like one Attack swing). Hit-location row 3 and damage as DOS. DOS's Aim (always allowed, full Attack %) as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.aim`, manual default, `dos` in Original (DOS); `P3Combat.Choose` / `SwingBase`. Wired into `Phantasie3Campaign`.
- **Side effects:** Aim has Thrust's chance to hit, gives up its +1 damage and gains a 57 % chance at Head or Torso.

## D3-014 — Healing IV and Healing Potion 10 against Gone areas

- **Manual:** the healing table gives Healing IV / Potion 10 "More than 1 Gone" (III PDF 9 / pr.12; spell list PDF 11 /
  pr.16 "Cure more than 1 Gone").
- **Code:** healing spends cure points equal to the tier (I 1, II 2, III 3, IV 4; potions 1–3 → 1, 4–6 → 2, 7–9 → 3,
  10 → 4) on whole areas, Gone costing 3 (root `0xaafc`, `0x194d`, `0x94a5`) `[C]` static. Tier IV can therefore cure one
  Gone area plus one Injury, never two Gone areas. Tiers I–III equal the manual.
  **Verified live s82:** Healing IV (cast in Pendragon by a Ranger) on a character with Left Arm Gone, Torso Injured,
  Right Leg Gone left only the Right Leg Gone: one Gone (3 points) + one Injury (1 point) (`screens-s82/s82_d3014_healing4_cast.png`).
- **Decision (user s81):** manual: Healing IV and Healing Potion 10 spend 6 cure points (enough for two Gone areas). Tiers I–III unchanged. DOS's 4 points as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.healing-iv`, manual default, `dos` in Original (DOS); `P3BodyAreas.HealingCurePoints`. Wired into `Phantasie3Campaign`.
- **Side effects:** a character with two Gone limbs needs two Healing IV casts (or an inn for the Breaks).

## D3-015 — Town inns heal every Injured and Broken area

- **Manual:** an inn stay "will cure 1 Break or 2 Injuries" (III PDF 9 / pr.12; App. II PDF 14 / pr.23); PDF 7 / pr.8:
  "Injured Body Areas can be cured with rest in the Inn" (manual question Q18).
- **Code:** the inn (ovl2 `0x347d`) restores HP and magic in both cases. In a **town** (`FAR:0xe596` set) it sets every
  area that is not Gone to Okay; at a **wilderness** inn it spends 2 cure points (= 1 Break or 2 Injuries) `[C]` static.
  The flag is set on entering a town (ovl3 `0x6c8`, place type 1) and cleared on leaving it (ovl3 `0x88`); the
  independent inns (place type 3, 250 gold) call the same inn routine with it clear `[C]`.
  **Verified live s82:** one night at the Pendragon inn healed every Injured and Broken area (fighto: two Injured + a
  Broken leg; gobbo: Injured head + two Broken arms; monko: two Injured legs); crusho's Gone leg stayed Gone
  (`screens-s82/s82_d3015_town_inn.png`). **Verified live s109:** one night at The Jolly Inn (independent inn) spent
  exactly 2 cure points per character (a Broken arm; two Injuries; an Injury beside a Gone arm), and 250 gold after
  the stay (`reverse-engineering/dos/phantasie3/screens-s109/README.md`).
- **Decision (user s81):** manual: every inn stay (town, wilderness, independent) cures 1 Break or 2 Injuries. DOS's "town inn heals every non-Gone area" as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.inn-healing`, manual default, `dos` in Original (DOS); `P3BodyAreas.InnStay`, run by every inn stay of the III campaign (`Campaign.AfterInnStay`; independent inns `Phantasie3Campaign.StayAtWildernessInn`).
- **Side effects:** in DOS one town night fixes any number of Injured and Broken areas; Gone areas always need healing
  magic or potions.

## D3-016 — I → III transfer: transferred characters start at level 1

- **Manual:** "The transferred characters will begin at level 0" (III PDF 13 / pr.20).
- **Code:** new evidence (s79, T4): the converter (ovl1) calls the shared creation routine (ovl2 `0x227d`) for every
  transferred character, which writes **level 1**, the class kit, the first spell and fresh skills; afterwards ovl1
  sets level 20 for Undead only `[C]` static (`FORMAT.md` §6). D3-007's "everyone else starts at 0" was wrong. New
  characters also start at level 1, and III's level tables start at 1.
  **Verified live s81** (DOSBox-X): the transfer was run on a Phantasie I `GUILD.DAT` (DOS III offers it only when
  the player answers "No" to "Are you playing off the hard drive?" and only when the data disk already has a saved
  game). Results: fighto (I level 3) → **level 1**, XP 300; the five level-1 characters → **level 1**, XP 100; rango
  (I level 2, race patched to UNDEAD) → III Undead **level 20**, XP 200. All of them got social class 3 (Noble) and
  the class kit (4, 22, 41/44, 64). The Guild roster lists 1 / 20 (`screens-s81/s81_roster_after_transfer_levels.png`).
- **Decision (user s81):** literal level 0: D3-007 stands (every transferred character starts at level 0, Undead too). III has no level-0 row; the missing table values are a manual-silent detail filled in when built. DOS's level 1 (Undead 20) as a rules option.
- **Remaster (s147):** level 0 reads level 1's training cells (`P3Training.TableLevel`: XP needed and price as for level
  1; DOS's level-0 cells are neighbouring data, G = 25,362,764); HP dice, skills and the magic ceiling are creation's
  level-1 values; Divine Aid and spells learnable at level 1 wait for level 1.
- **Side effects:** XP after transfer is 100 × old level either way.

## D3-017 — Arrow Flame bow damage

- **Manual:** "Arrow Flame 1 increases accuracy by +50 percent and Bow Point Values by +15. Arrow Flame 2 increases
  accuracy by +100 percent and bow point values by +25." These spells "increase the accuracy and damage of bow fire"
  (III PDF 10 / pr.15).
- **Code:** bow damage per hit is `rnd(2 · points[bow] + 4)`. Arrow Flame I / II set the Fire Bow chance to skill + 50 /
  + 100 (as the manual) and **replace** the damage range with `points[bow] + 15` / `+ 25` (root `0xc3cc`) `[C]` static.
  Bows have at most 20 points, so Arrow Flame II always raises the range; Arrow Flame I lowers it for bows over 11
  points (items 52–60).
  **Verified live s82:** a Ranger cast P.Arrow Flame I in combat; the per-member bow damage words (`FAR:0xb58c`) went
  from 6 (Self Bow, 1 point: 2·1 + 4) to **16** (1 + 15) and from 44 (God Bow, 20 points) to **35** (20 + 15), and the
  bow chance (`FAR:0xdd9c`) from 60 to 110. The values stayed after a second round without a cast, so Arrow Flame lasts
  for the rest of the fight (`screens-s82/s82_d3017_*.png`).
- **Decision (user s81):** manual, read literally: Arrow Flame 1 / 2 add 15 / 25 to the bow's point value, which then feeds the normal range `rnd(2·(points + n) + 4)`; accuracy +50 / +100 as both. DOS's replacement range (`points + 15/25`) as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.arrow-flame`, manual default, `dos` in Original (DOS); `P3Combat.BowStats`. Wired into `Phantasie3Campaign`.
- **Side effects:** with the nine best bows Arrow Flame I trades damage for accuracy.

## D3-018 — Armour and the chance to be hit by monsters

- **Manual:** armour protects ("Armor is less effective vs. [bows]", III PDF 14 / pr.23; armour tables PDF 11 / pr.17).
- **Code:** a monster's melee chance to hit a character subtracts `defence = points[shield] / 2 − 5 · points[armour]
  + Parry / 4 + 2 · DEX` (root `0xd730`, used by `0xb64e`) `[C]` code reading: armour points *raise* the chance to be
  hit. No other armour soak was found in the melee path. Armour does protect against breath (damage = `rnd(36) −
  armour points`, root `0xb582`), so the melee sign looks like a DOS bug. A DOSBox check (3c.8) should confirm before
  the user decides.
  **Verified live s81** (DOSBox-X, memory read from save states; `reverse-engineering/dos/phantasie3/screens-s81/`):
  six transferred characters, all with Small Shield (4 points), were given armour 0 / 22 Robes / 40 God Armour /
  30 / 22 / 35 through the save file (the Inspect screen shows "God Armour 11"). At the start of the fight the game
  wrote the defence words `FAR:0x488e` = **46, 1, −7, −18, 19, 16**, exactly `shield/2 − 5·armour + Parry/4 + 2·DEX`
  for each character. **Cleanest measured pair:** fighto and thiefo have the same DEX 17, Parry 40 and shield; fighto
  wore no armour, thiefo item 35 (6 points). Defence 46 vs 16 (difference 30 = 5 × 6), so a Wild Cat hits fighto at
  **−6 % (never)** and thiefo at **24 %**. The live item-points table equals the static one (armour 21–30 → 1–10, 31–40 → 2–11). The
  monster group record held attack 40 (Wild Cat: `CREATURE.DAT` byte 15 × 10), so each cat makes one attack with a
  chance of `40 − defence` (party all Front, rank term 0): **−6 % (never) for the character with no armour,
  47 % for the one in God Armour**. Computed, not measured: with Robes the God Armour character (gobbo) would have
  defence 38 and a 2 % chance, so God Armour adds 45 points to the chance of being hit. `0xd81d` (write) and `0xb809`
  (read) are the only instructions with displacement `0x488e` in the root and all four overlays. In round 1 of that fight the God Armour character was the only one hit.
- **Decision (user s81):** manual (confirmed live s81, so the condition holds): armour lowers the monster's chance to hit (sign fixed: `+ 5 · points[armour]`). DOS's inverted sign as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.armour-defence`, manual default, `dos` in Original (DOS); `P3Combat.Defence`. Wired into `Phantasie3Campaign`.
- **Side effects:** if true, heavy armour helps against breath but makes monsters hit more often in melee.

## D3-019 — Down targets are always hit

- **Manual:** "Sleeping adventurers and monsters are automatically hit in combat. Down adventurers and monsters are much
  easier to hit than Okay targets, but can still dodge." (III PDF 15 / pr.24.)
- **Code:** both directions set the chance to 100 when the target's status is anything but Okay (Asleep, Out, Down;
  monsters also Unconscious) (root `0xc03b`, `0xb64e`) `[C]` static.
  **Verified live s81 for monsters attacking Down characters** (DOSBox-X): all six party members were set to Down
  (right leg Broken, status 4) with no armour and a 20-point shield. The defence words were 54, 19, 56, 40, 37, 54
  and the monsters were 5 Orcs (attack 40) and 1 Kobold (attack 50). Okay, these characters would have been hit at
  −16 % to 31 % per attack, and fighto, gobbo and thiefo never (≤ −4 against either monster). Over three rounds the monsters made 14 attacks:
  **14 hits, 0 misses**. Those three were hit anyway: fighto twice, gobbo twice and thiefo three times. As a
  control, the earlier fight with the party Okay (the D3-018 setup) showed "Wild Cat misses" lines. The character
  → monster direction (Down monsters) and Out targets were not tested live.
  **Out targets verified live s82:** three members Out (head Broken) and three Okay, all with defence 56 against Wild
  Cats (attack 40, chance −16): over five rounds the Okay members lost no HP, the Out members took 10 hits (fighto 20,
  gobbo 8, rango 22 HP) (`screens-s82/s82_out_targets_*.png`). The character → monster direction (Down / Unconscious
  monsters) is still not run live (it needs a monster with a broken leg, which a save edit cannot set).
  **Character → monster verified live s110** (DOSBox-X, monster status poked into a save state with
  `tools/scripts/p3_dosbox_poke.py`, `reverse-engineering/dos/phantasie3/screens-s110/`): three fighters Thrust
  (Attack % 61, Mace 4) at three Skeletons with defence 30, so 31 % each against an Okay target. Control (Skeletons
  Okay): **4 hits in 18** thrusts over six rounds. Skeletons Down (right leg Broken, status 3, combat list "DDD"):
  **18 hits in 18** over six rounds. Skeletons unconscious (head Broken, status 4, list "OOO"): every thrust hit too
  (12 of 12 in four rounds). Damage was 2…5 = 1 + rnd(4) every time and the lines were the usual "<name> hits!" /
  "Skeleton takes N damage!": no bonus damage and no extra message for a Down or unconscious target. Unconscious
  Skeletons made no attacks; Down ones still attacked ("Skeleton misses"). The remaster's `p3.down-targets` = `dos`
  matches (`P3Combat.TargetChance` → 100; test `D3019_DosLive_s110_…`).
- **Decision (user s81):** manual, with values the user chose: Asleep targets are always hit; Down and Out (monsters also Unconscious) targets are hit at double the normal chance, capped at 95 %, so a dodge stays possible. DOS's always-hit as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.down-targets`, manual default, `dos` in Original (DOS); `P3Combat.TargetChance` (a normal chance ≤ 0 stays 0 when doubled: see Edge below). Wired into `Phantasie3Campaign`.
- **Side effects:** a broken leg (Down) makes every melee attack and bow shot land.
- **Edge (user s82):** a monster whose normal chance is 0 or below still never hits a Down / Out target (doubling
  nothing stays nothing); built that way.

## D3-020 — Party found-item limit

- **Manual:** "The party can carry up to 90 items that they find while exploring." (III PDF 8 / pr.11.)
- **Code:** the found-item list holds **60** ids (`FAR:0x960`, count `0x9d8`); adding the 61st prints "The party can't
  carry any more items." (root `0xf421`) `[C]` static; the save block has room for 60 (`FORMAT.md` §8).
  **Verified live s82:** with 60 found items in the save, keeping the archives' Scroll II gave "The party can't carry
  any more items." and the count stayed 60 (`screens-s82/s82_d3020_found_items_full_at_60.png`).
- **Decision (user s81):** manual: 90 found items. Options: unlimited, and DOS's 60. A DOS save export keeps the first 60 (the format holds 60).
- **Remaster:** built (T4 Core rules): rules option `p3.found-items`, manual default, `dos` in Original (DOS); `P3Town.TryAddFoundItem` (`manual` 90 / `unlimited` / `dos` 60). Wired into `Phantasie3Campaign`.
- **Side effects:** an item found while the list is full is discarded.

## D3-021 — Monster status letters

- **Manual:** "The Status of a monster can be: Unconscious (U), Sleeping (S), Down (D), and Okay (K)", example
  "2 GNOME(S) S U D" (III PDF 15 / pr.24).
- **Code:** the combat list prints S for Asleep, D for Down, **O** for a monster whose head or torso is Broken (the
  unconscious state), K otherwise (root `0x11297`, letters DS `0x6a66…0x6a6c`) `[C]` static
  (`logic-specs/encounters.md` §2).
- **Decision (user s81):** manual: an unconscious monster shows **U**. DOS's **O** as a `p3.*` rules option.
- **Remaster:** built: rules option `p3.status-letter`, manual default, `dos` in Original (DOS); `P3Encounters.StatusLetter`,
  shown after each rank line of the combat list (s90).
- **Side effects:** display only; the state and its rules are the same.

## D3-022 — Hit points and Constitution

- **Manual:** "The number of hit points an adventurer has is based on his Level, Class, and Constitution" and the chart
  is "for Adventurers with 10 Constitution" (III PDF 8 / pr.11); CON is "a modifier on the number of Hit Points …
  The higher the adventurer's Constitution, the more Hit Points he has" (PDF 4 / pr.3).
- **Code:** creation rolls `dice × (rnd(2)+1)` and training `dice × rnd(3)` with `dice` from a class × level table
  (DS `0xa78`) whose doubled values are exactly the manual's CON-10 chart; neither HP-setting path reads CON, and no
  direct or record-field read of CON elsewhere feeds HP (ovl2 `0x227d`, `0x1031`) `[C]` static
  (`logic-specs/training.md` §4).
- **Decision (user s81):** manual: HP depend on Constitution. The chart is the CON-10 value; other CON values use Phantasie I's CON HP modifier (the manual gives no III formula, so I's rule is reused by the user's choice). DOS's CON-free HP as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.hit-points`, manual default, `dos` in Original (DOS); `P3Training.HitDiceCount`: `dice·(CON + 5)/15`, CON 10 = the chart. Wired into `Phantasie3Campaign`.
- **Side effects:** in DOS every character of a class gets the chart's HP on average whatever his CON; CON matters
  only for Swimming, age and Resurrection losses.

## D3-023 — Gold for going up a level

- **Manual:** "The adventurer's class determines how much gold he gets to start and how much he gets for going up a
  level; the higher the social class the more gold the adventurer receives" (III PDF 5 / pr.4; App. II PDF 14 / pr.23).
- **Code:** social class sets only the starting gold (class · 256) (ovl2 `0x227d`); training (ovl2 `0x1031`) writes
  no gold, and no other code adds gold by social class `[C]` static (`logic-specs/training.md` §4).
- **Decision (user s81):** manual: each level gained pays gold by social class, amount chosen by the user as the starting-gold rule, **social class · 256** per level. DOS's no level-up gold as a `p3.*` rules option.
- **Remaster:** built (T4 Core rules): rules option `p3.level-gold`, manual default, `dos` in Original (DOS); `P3Training.LevelUp` (into the bank account). Wired into `Phantasie3Campaign`.
- **Side effects:** no per-level income; social class matters for starting gold, the Mystic text and the endgame
  titles only. The manual gives no amounts, so the manual side would need a rule (e.g. class · 256 per level).

## D3-024 — Monsters 44–79 and the encounter options

- **Manual:** the Monster Reputation Table (III PDF 9 / pr.13) lists Good and Neutral monsters among the strong ones
  (High Ranger and Wizard Good; Forest Elf, King Cobra, Leopard, Great Troll, Gnome Mage, all Dark Things, Dragons,
  Giants and Lords Neutral); "Trying to greet possibly friendly monsters doesn't cost you anything", "don't be afraid
  to Beg for Mercy", flee "if it is out-matched" (PDF 15 / pr.25); Flee, Greet, Mercy and Surrender carry no monster
  exceptions (PDF 7 / pr.9–10).
- **Code:** Greet, Beg for Mercy and Accept Surrender (and monsters fleeing) all require the rank-1 monster id < 44;
  Flee fails for ids ≥ 44 unless every rank-1 monster is already asleep / down / out, and always for 77–79
  (Nikademus, Lord Wood) (root `0x112e9`, `0x10e14`, `0xd171`, `0x10f30`) `[C]` static
  (`logic-specs/encounters.md` §4).
- **Decision (user s81):** manual: Greet, Beg for Mercy, Accept Surrender and monsters fleeing work for every monster by its reputation, with no id ≥ 44 block. Exception, as in I / II: the final bosses (77–79, Nikademus and Lord Wood) block the party's Flee; an option lets the party flee them too. DOS's id ≥ 44 block as a `p3.*` rules option. Beg for Mercy losing "some of its new items" follows the manual; how many is `[?]` (to settle when built). (user s87) The count: check Amiga / Apple III's Mercy first and use their rule if it takes items; otherwise back to the user. (user s89) The count: manual by default — each item found since the party last left town is taken with a 50 % chance (rolled per item; a remaster rule, no version has one); gold only (DOS / Amiga) as an option.
- **Remaster:** built (T4 Core rules, wired in `Phantasie3Campaign`): rules option `p3.encounter-block`, manual default, `dos` in Original (DOS); `P3Encounters` Greet / Mercy / Surrenders / Flee; `p3.boss-flee` (`blocked` in every preset, `allowed`). Mercy items (s91): `P3Encounters.MercyTakeItems` rolls `rnd(100) ≤ 50` per found-item slot that is new since the party last left town (the sack remembers its slots at town exit, `LootSack.TownExitItemIds`, saved; items kept from an earlier trip are safe), in slot order; a taken item leaves a hole as I's Mercy; "They take some of your new items!" when any go; rules option `p3.mercy` (`manual`, `dos` = gold only, Original (DOS) preset).
- **Side effects:** 36 of 80 monster types (Dark Gnome onward) can only be fought; the reputation of those listed as
  Good / Neutral has no effect. Related: Mercy takes all gold but no found items (manual "some of its new items"; the
  fight's gold and treasure are lost, XP for monsters already killed is kept in the wilderness) — noted for the same
  decision.
- **Amiga / Apple II (s88):** neither takes items. Amiga Mercy (`PhantasieIII` flat `0x19a88`): `rnd(100) < reaction + 60`
  and rank-1 id < 44 → "[original text]", party gold (`0x25d42`) = 0, gold flag / outcome cleared, as DOS; no item
  code `[C]` (after-combat path assumed = DOS `[~]`). Apple II (Applesoft, `reverse-engineering/apple2/phantasie3/README.md`):
  lines 1110–1130, success `rnd·100 < 90 + NF` (wilderness) / `< 60 + 3·NF` (dungeon), **no id block**; gold set via
  `GL = −256·PEEK(SA+1199)`, so the party keeps its gold **mod 256**; treasure roll skipped (`YS = 1`); XP kept; found
  items untouched `[C]`. So the item count has no answer in either version → back to the user (per s87).

## D3-025 — Bank deposit: fee and shares

- **Manual:** Deposit "allows the adventurers to divide the party's gold into shares … Each adventurer will get either
  1, 2, or 3 shares of the party's gold and the value of each share will be displayed … A small banking fee will be
  charged at every deposit"; Withdraw "allows each adventurer to withdraw gold from his account" (III PDF 5 / pr.5).
- **Code:** the bank (ovl2 `0x297a`) has "[original text]" (every account emptied into the party treasury) and
  "[original text]" (amount typed in, split evenly, the remainder 1 gold at a time to random members); **no fee**,
  no shares, no share value shown `[C]` static (`logic-specs/town.md` §2).
- **Decision (user s81):** manual: deposit by 1 / 2 / 3 shares per adventurer with the share value shown (reuse I's deposit). The fee is `[?]`: the manual names none, so no fee until a value is chosen. (user s87) The fee: check Amiga / Apple III's bank first and use their fee if they charge one; otherwise back to the user. (user s89) The fee: Apple II's share maths — each account gets ⌊S × gold ÷ (ΣS + 0.00001)⌋, so ~1 gold per member with shares plus any remainder is lost on every deposit; no fee (DOS / Amiga) as an option. DOS's even split / withdraw-all as a `p3.*` rules option. The user flagged banking (fees, shares) as a possible QoL area across the games (PLAN P3).
- **Remaster:** built (T4 Core rules): rules option `p3.bank` = `manual` (shares 1–3 with Apple II's maths, `P3Town.AppleIiAccountShare`: 600 gold on 1 / 2 / 3 shares → 99 / 199 / 299, the rest lost; the share value shown is ⌊gold / (ΣS + 0.00001)⌋), `no-fee` (Phantasie I's deposit, S · ⌊gold / ΣS⌋, which still drops the remainder below one share) or `dos` (even split + withdraw all, Original (DOS) preset); `P3Town.DepositShares` / `DosDeposit` / `DosWithdrawAll` (s91). Apple II's other share rules (0 shares only for a member not status 1, the forced division on S)ave) are not copied: I's checks apply. Client: III Amiga's Bank is window 9 (s117); under `manual` / `no-fee` its Withdraw / Deposit open the remaster's per-member withdraw and share deposit, drawn in window 9's style under AMIGA SCREENS (user s157; the layout inside is the remaster's own, FORMAT.md 8.2; CANCEL in the share window leaves with nothing deposited). AMIGA SCREENS off: the remaster's panel and prompt.
- **Evidence s82:** DOS does have 1–3 shares, but for the **experience** split: Distribute asks "Shares for <name>:"
  (1–3, record `+0x54`) and gives each member shares × (party XP ÷ total shares) (ovl2 `0x0000`); the manual's shares
  are for gold deposits (`FORMAT.md` §5).
- **Side effects:** the manual gives no fee amount or share rule, so its side would need numbers; the I remaster's
  deposit (I's shares) could be reused for the shares part.
- **Amiga / Apple II (s88):** Amiga bank (`PhantasieIII` flat `0x11812`) = DOS: withdraw all, deposit typed amount split
  evenly with the remainder 1 gold at a time to random members (`0x18846`), **no fee**; its "Shares for" (`0xc1a0…0xc3be`)
  splits the XP pool (`0x25d3e` → record +0x102) only `[C]`. Apple II bank (town lines 6190–6250, Divide 600–695):
  **manual shares** — Deposit asks 0–3 shares per member (0 only if not status 1), shows "EACH SHARE IS … GOLD",
  each account += S × gold ÷ (ΣS + 0.00001), stored truncated (line 2000–2020, cap 65,535), party gold → 0; withdraw per
  member with a typed amount, party capped at 65,000; S)ave game with gold carried forces the same division (line 3695)
  `[C]`. **No explicit fee**; the only loss is the truncation (the 0.00001 makes every share fall just short, so ~1 gold
  per member with shares, plus any remainder, e.g. 600 gold / 6 × 1 share → 99 each) `[C]`; that this is the manual's
  "small banking fee" `[?]`. → back to the user (per s87).

## D3-026 — 65,000 gold cap

- **Manual:** "A party can carry up to 65,000 Gold Pieces … An adventurer's bank account can hold a maximum of 65,000
  Gold Pieces" (III PDF 5 / pr.5).
- **Code:** party gold and accounts are 32-bit; the bank, armory, rewards, Drop and transfer paths read add or
  subtract without a ceiling, and the constant 65000 does not occur in the program `[C]` static
  (`logic-specs/town.md` §2).
- **Decision (user s81):** manual: party gold and each account capped at 65,000 (as I / II). Option: uncapped, as DOS.
- **Remaster:** built (T4 Core rules): rules option `p3.gold-cap`, manual default, `dos` in Original (DOS); `GameOptions.GoldCap` (`dos` = lifted; the 65,000 comes from III's rules profile). Wired into `Phantasie3Campaign`.
- **Side effects:** none in normal play until gold passes 65,000; the ST card's shared 1,000,000,000 purse is a third
  variant.

## D3-027 — Trade Items

- **Manual:** "Trade Items: this option allows an adventurer to give one of his items to someone else in the party, or
  to sell one of his items to the Armory … by trading it to adventurer 7" (III PDF 6 / pr.7); tip "while in town
  adventurers may trade items without having to redistribute them" (PDF 14 / pr.23); Apple key T (PDF 13 / pr.21).
- **Code:** DOS has no Trade: the Options menu offers Cast Spell, Use Item, Show Party, Distribute, Load, Save, Quit,
  Sound, About; the trade strings (453–455) are never referenced. Items move only through Distribute (pool and hand
  out / sell). The Amiga program has "G) Trade Item" `[C]` static (`logic-specs/town.md` §3).
- **Decision (user s81):** manual (as the Amiga program): Trade Items gives an item to another member or sells it via "adventurer 7". DOS's no-Trade as a `p3.*` rules option. User: with the inventory rework (T2) enabled, Trade should be available at all times (QoL, PLAN P3). (user s89, after the s88 Amiga finding) Default stays give + sell; the sale pays Distribute's price (⌊⌊value · index / 100⌋ · 75 / 100⌋, minimum 1); `p3.trade` gains a third value, Amiga's give-only Trade.
- **Remaster:** built (T4 Core rules, s91): rules option `p3.trade` = `manual` (give + sell), `give-only` (the Amiga's Trade) or `dos` (none, Original (DOS) preset); `P3Town.Give` / `Sell`; a sale pays `P3Town.SalePrice` and raises the town's stock by 1 (capped at 99, as Distribute). **Side fix (s91):** III's Distribute SELL paid Phantasie I's `V²/8`; it now pays the same 75 % price (`ICampaign.DistributeSalePrice`), as DOS's ovl2 `0xa8f`. Client: Trade in the town Option list (`TradeDialog`). **Trade at any time (QoL, user s178):** with the `modern-inventory` option on (Remaster preset) the inventory screen gives an item to another member anywhere outside combat and sells it in any town, whatever `p3.trade` says; giving no longer re-readies anyone (a new item is worn only into an empty slot); `inventory-actions.md` section 7.
- **Sale price (s88):** DOS's only sale (Distribute) pays `⌊⌊value · index / 100⌋ · 75 / 100⌋`, minimum 1 = 75 % of the town's price `[C]`; Amiga's Distribute uses the same formula `[~]`; in the Amiga Trade routine no sale path or price maths was found, and the running game offers none (s116: "[original text]" lists only the party; `amiga/phantasie3/FORMAT.md` §8).
- **Side effects:** in DOS every single hand-over or sale goes through the full redistribution screen.

## D3-028 — Autosave on leaving town

- **Manual:** "Every time the party leaves a town the game is saved automatically" (III PDF 7 / pr.8); "Every time you
  leave town the game is automatically saved. If you stop the game while outside a town you can continue it again from
  the last town the party was in." (PDF 13 / pr.21). The IBM card: "All other rules are as stated in the rulebook";
  the ST and Amiga cards: saving from the menu "is the only time a game will be saved" (III-AU PDF 17–18).
- **Code:** `save_game` (root `0x8176`) is called only by Options ▸ Save Game and at the end of the I → III transfer;
  leaving town (ovl3 `0x88`) saves nothing `[C]` static (`logic-specs/town.md` §4).
- **Decision (user s81):** manual: the game saves automatically every time the party leaves a town. DOS's menu-only saving as a `p3.*` rules option. User: modern save slots as a further QoL option.
- **Remaster:** built (T4 Core rules): rules option `p3.autosave`, manual default, `dos` in Original (DOS); `P3AutoSaveOnLeavingTown()` (the campaign calls it on leaving town). Wired into `Phantasie3Campaign`. Modern save slots (the user's QoL): built as the SAVE SLOTS switch for all four games, the autosave keeping its own AUTO slot (`docs/plan/12-save-slots.md`).
- **Side effects:** in DOS, quitting outside town loses everything since the last manual save (dungeon map saves
  aside).

## D3-029 — Guild roster size

- **Manual:** "Dozens of different adventurers can be recorded in the guild but only six will be in a party at a time"
  (III PDF 5 / pr.4).
- **Code:** the roster holds 20 records (`FAR:0xe820`, creation needs a count below 20, "The character roster is
  full."); a new data disk starts with the three elementals in slots 0–2, so 17 characters fit (root `0xa856`,
  ovl2 `0x227d`, `0x3f04`) `[C]` static (`logic-specs/elementals.md` §1, `FORMAT.md` §5).
- **Decision (user s81):** manual: "dozens" read as **no roster limit** by default. DOS's 20 slots (17 characters + 3 elementals) as a `p3.*` rules option; a DOS export writes the first 17 characters.
- **Remaster:** built (T4 Core rules): rules option `p3.roster`, manual default, `dos` in Original (DOS); `P3Town.RosterHasRoom` (`dos` 17 characters; `guild-limit` = `none` lifts it). Wired into `Phantasie3Campaign`.
- **Side effects:** limits the I → III transfer too (it stops when the roster is full).

## D3-030 — How the I → III transfer is started

- **Manual:** the transfer is option 6 of the Other Utilities Menu ("Transfer Characters From PHANTASIE I Or II"),
  "the first action you should take after making your Player and Dungeon Disks" (III PDF 13 / pr.19–20). Nothing
  about hard disks or needing a saved game.
- **Code:** DOS III has no utilities menu. At start-up it asks "Are you playing off the hard drive?"; only after
  **No** does it ask "Would you like to transfer older characters saved from Phantasie I?" (ovl1 `0x988…0x9c5`), so a
  hard-disk player is never offered the transfer. The transfer then refuses to run until the data disk holds a saved
  game: "There is not a saved game on this data disk to load. Save a game to this data disk and try again later."
  `[C]` verified live s81 (the player has to start a game, save it, quit and restart).
- **Decision (user s81):** manual: the I / II → III transfer is a menu option (the remaster's transfer screen, T5), available any time. DOS's startup-only, floppy-only, saved-game-first flow is not reproduced.
- **Remaster (s147):** III's game menu (Esc) › Transfer from I / Transfer from II, any time: the source save file, then
  DOS's review per character (name, level, class, race, status, age, attributes, "[original text]"),
  then the kept ones join the Guild (`GameSession.TransferIntoPhantasieIII`, the Client's `DosSaveDialog`).
- **Side effects:** none on the characters; it only changes how the transfer is reached. Spec: `FORMAT.md` §6.

## D3-031 — Healing order: which area is cured first

- **Manual:** healing cures the worst areas it can afford, "the most vital area it can" (III PDF 9 / pr.12, healing
  table) `[~]` wording as summarised in `logic-specs/body-areas.md` §4.
- **Code:** `heal_body_areas` (root `0xaafc`, disassembled s82) compares each area with the current level `[bp−8]` and,
  after a cure, sets that level to the points left **inside the same pass** (`0xab3b…0xab44`), so the scan goes on from
  the next area. With 4 points, Head Injured, Left Arm Gone and Left Leg Injured, DOS cures the arm and the **leg** and
  leaves the Head Injured; an area before the cured one is reached only on a later pass, after the points are spent
  `[C]` static. Among equal states the order is Head, Left Arm, Right Arm, Torso, Left Leg, Right Leg (Torso after the arms).
- **Decision:** manual (user s82): each cure takes the worst area the points left can pay for, the most vital first.
- **Remaster:** `P3BodyAreas.Heal(points, rules)` starts over after every cure; among equal states the order is the
  manual's Inspect order Head, Torso, Arms, Legs, left before right `[~]` (the manual names no order). DOS's pass as
  option `p3.heal-order` = `dos`. Tests: `D3031_*`.
- **Side effects:** only when the points left after a big cure can pay for a small area both before and after it.

## D3-032 — Skill-training tables: five cells that look like data errors

- **Manual:** gives no skill-training increments.
- **Code:** `extract_dos_p3_rules.py` reads III's tables equal to I's except Thief Pick Lock level 3 = 35 (I 55), Ranger
  Attack level 15 = 25 (I 250), Ranger Parry level 15 = 60 (I 85), and the Parry table is one byte short after the Ranger
  row, so Monk and Wizard read one level ahead of I's rows (their level 15 comes out 40 and 0) `[C]` static (s82).
- **Decision (user s82):** treat them as data errors: I's values by default; DOS's tables as read as a `p3.*` option.
- **Remaster:** built s83: `rule-tables.json` `skill_table_fixes` holds I's rows where III's differ (written by
  `build_p3_gamedata.py` from I's tables); `P3WorldData.Training` uses them, `TrainingAsRead` keeps DOS as read for the
  option `p3.skill-tables` = `dos`. Tests: `D3032_*`.
- **Side effects:** none outside skill training.

## D3-033 — Armory stock between visits

- **Manual:** "The armorer buys his inventory from crazy adventurers who come into town to sell the treasures … The
  stocks of scrolls and other treasures in the armory remain for future visits. However, the stocks of weapons and
  potions can change each time the party enters." (III PDF 6 / pr.6.)
- **Code:** the stock is one byte per town and item (`INITARMS.DAT` rows, loaded once at start-up by ovl1 `0x5e0`, the
  only reference to "initarms.dat" is ovl1 `0x96c`, saved in `gamesave.dat`). Only the party changes it: a purchase
  subtracts 1 (ovl2 `0x313e` / `0x31fc`; a count of 0 drops the kind and row byte 0 −1); a sale adds 1 to the town the
  party is in (ovl2 `0xa8f` / `0xab3`, a new kind also raises byte 0). Entering a town (root `0xa911`) only rebuilds
  the price list from the live stock (root `0xf22`). No other code writes the table (every `· 0xb5` row access in the
  root and the four overlays checked) `[C]` static (s88). So nothing restocks and nothing changes on its own.
- **Decision (user s89):** manual, by Apple II's rule: on each town entry wipe 18 random ids 0–110 and set 13 pool picks to 1, but with the Light Key (172) and Burnt Gem (155) removed from the pool (keeps the key a quest find and matches "weapons and potions"). DOS / Amiga no-restock as a `p3.*` option. Starting stock stays DOS's `INITARMS.DAT`.
- **Remaster:** built (s91): rules option `p3.armory-restock` = `manual` (`P3ArmoryRestock`, data `rule-tables.json` `armory_restock`: Apple II's 46-byte pool read from the 1A disk, 18 wipes of ids 0–110, 13 picks at ⌊(1 − r₁r₂) · L · 3⌋, L = the party's average level capped at 15) or `dos` (no restock, Original (DOS) preset). The two removed ids keep their pool positions: a pick landing on them stocks nothing (so the level bands of the other items stay Apple II's). Runs on every ordinary town entry (`ICampaign.OnTownArrival`), not on the new-game start (Apple II skips the first entry after start / load `[~]`); plane towns 15–17 restock too, as Apple II `[C]` s98: the restock (town lines 840–860) reads the pool from the entered
  town's own file with no town test, and the TOWN OF LIGHT / DARK / DEATH files on sides 1A and 3A carry the same 46-byte pool. Buying lowers the count; Distribute and Trade sales add 1, capped at 99 (DOS: no cap).
- **Side effects:** in DOS a weapon or potion bought out stays gone for good in that town; anything sold (weapons,
  armour, treasures, scrolls) becomes buyable in the town where it was sold, at the town's price. **With the Apple II
  rule (s91):** DOS's starting "plenty" stacks (200 Healing Potions etc.) can be wiped to 0 or set to exactly 1 by a
  pick (ids 101–110 are wiped, 105–110 and the Magic Potions 115–119 are in the pool), so within a few visits a 200-stack
  may be gone; the plane towns 15–17, which stock nothing in DOS, pick up weapons and potions too (as Apple II, s98). The manual's "change"
  has no rule for what it changes to (random? from INITARMS?), so the manual side would need a user rule. Amiga: not checked `[?]`.
- **Amiga / Apple II (s89):** Amiga = DOS: `inititem.dat` loaded once (`0x1f9ee`, only caller `0x75da`), saved with the
  game; only buy −1 (`0x1129c`) / sell +1 (`0xcc8a`, byte wraps, no cap) write it; town entry (`0xfb8c`) only rebuilds the
  price list `[C]`. **Apple II re-rolls on each town entry** (side 1A town program, lines 840–890) `[C]`: line 850 sets 18
  random ids in 0–110 (all weapons / armour / shields / bows 1–100 + Healing potions 101–110) to 0 (with replacement,
  ~15 % per stocked id); line 860 makes 13 picks from a fixed 46-entry pool (`LA+181…226`, same in every town file) at
  position `⌊(1 − rnd·rnd) · DM · 3⌋`, DM = party average of record byte 54 (level `[~]`) capped at 15, and sets that
  item's stock to exactly 1 (overwrites). Magic potions, scrolls and treasures are never wiped; the pool also holds
  **Light Key (172) and Burnt Gem (155)** at positions 36–37 (reachable at average level ≥ 12). Skipped when `SA+1196` > 0
  (probably the first entry after start / load `[~]`). Sales capped at 99 (lines 8440–8450). Apple II's starting stocks
  differ from DOS's `[~]`.
- **Spec:** `reverse-engineering/dos/phantasie3/logic-specs/town.md` §7.

## D3-034 — Kobold maximum Charisma

- **Manual:** "Maximum Attributes by Race": Kobold STR 17, INT 15, DEX 19, CON 19, **CHA 13** (III PDF 4 / pr.3).
- **Code:** creation (root `0xa719`) caps each roll by the race-maximum table `DS:0xa28` (race · 5 + attribute). The
  Kobold row (race 8) reads **17, 15, 19, 19, 3** `[C]` static (s88). Every other cell of the 15 race rows equals the
  manual's table. With the floor of 3, every DOS Kobold has CHA exactly 3. The Kobold's CHA modifier (`DS:0xf42`) is
  5, the same as the manual-derived value the remaster uses.
- **Decision (user s89):** manual 13; DOS's 3 is a data bug and is fixed outright, with no option (as D4-056).
- **Remaster:** built: uses the manual's table (Kobold CHA ceiling 13; `GameData/phantasie3/creation-tables.json`, copied from I).
- **Side effects:** Kobold CHA only (3 vs 3–13 on the manual side); what III reads CHA for is not traced `[?]`.
- **Spec:** `reverse-engineering/dos/phantasie3/FORMAT.md` §4.3.

## D3-035 — Transportation: which towns

- **Manual:** "Transportation (#54): this spell will transport the party interdimensionally from one town to
  another." (III PDF 11 / pr.17; type N/T, town only, PDF 10 / pr.14.)
- **Code:** `cast_spell_noncombat` (root `0x8e50`, spell 54 = `0x36`): only while in a town (`FAR:0xe596`); "Transport to
  which town?" (string 435) opens a 14-row menu (`DS:0x560`, count byte 14); the choice + 1 is accepted only when it is
  1–14 and not the current town, then the party is put in that town ("[original text]"). So the
  destinations are the 14 Scandor towns (Pendragon … Sciattica); the plane towns 15–17 (Light, Darkness, Death) can be
  left by the spell but never reached `[C]` static (s88).
- **Decision (user s89):** NOT A CONFLICT — DOS 1–14: "interdimensionally" = from any plane's town back to Scandor. The
  plane dungeons have no exit to Scandor, so the spell is the designed way home; reaching towns 15–17 by spell would skip
  the Chronos lever, both keys and the Castle of Dark. QoL option (after 1:1, off by default): a plane town the party
  has visited once becomes a Transportation destination.
- **Remaster:** built (s91): `Phantasie3Campaign.TransportTowns` = 1–14, cast from any town. QoL option built (s176):
  `p3.transport-planes` (QUALITY OF LIFE section of III's tab), `off` in every preset (DOS's 1–14) or `visited` (a player
  choice in Custom): Transportation also lists each plane town 15 Light, 16 Dark, 17 Death that a member of the party
  has entered, after the Scandor towns; arriving runs the normal town entry. The visited flag is per character
  (`Phantasie3CharacterData.PlaneTownsVisited`, remaster only, saved with the character), set on every III town entry
  whatever the option and on a Guild Add in a plane town; "the party has visited" = any member (the elemental not
  counted) has it. Per character, not per save: with several parties (D-069) a party that never reached the planes gets
  no shortcut past the quest route, and the flag follows characters through Guild Drop / Add.
- **Side effects:** none with the option off. Under `visited` only a plane town some member has already reached by the
  quest route is offered; a newcomer who joins such a party can be taken there without walking the route `[~]`
  (`planes.md`).
- **Spec:** `reverse-engineering/dos/phantasie3/logic-specs/town.md` §8.

## D3-036 — Parry

- **Manual:** "Parry: … A successful parry makes the adventurer harder to hit and reduces the effects of enemy attacks."
  (III combat options, PDF 8 / pr.11.)
- **Code:** the Parry action does nothing on its own turn (`0xc104`). Monster melee (`0xb64e`, test at `0xb848`)
  subtracts the shield's points from its chance when record **`+0x6e`** is 5 (Parry). `+0x6e` is written only at fight
  start (`0xd55b`, the saved option) and at fight end (`0xd4ff`, this fight's last action); the round menu writes
  `FAR:0x228`, never `+0x6e` (every writer checked in root and the four overlays). So **Parry chosen during a fight has no
  defensive effect in that fight**, a member whose saved option was Parry keeps the shield bonus all fight even while
  attacking, and **no damage reduction exists** (`rnd(damage)` passes unchanged, no "parries" message) `[C]` static (s88,
  `logic-specs/combat-round.md` §4); live check wanted.
- **Amiga / Apple II (s89):** Amiga = DOS: only the saved option `+0x6e` counts (written at fight start `0x1504a` / end
  `0x14fe8`), shield points off the monster's chance (`0x17872`), no damage reduction (`0x17e16`); adds a "<name> parries"
  message on the member's turn (`0x16330`) `[C]`. **Apple II** (side 1A / 1B, same lines): this round's Parry counts —
  line 1527 rolls it, success if `rnd·100 < Parry skill %` (or the shield arm is hurt `[~]`, looks inverted); success shows
  shape 21 (raised shield `[~]`); no hit-chance term for the action (Parry skill/4 is in the always-on defence, line 4180);
  line 1925: after armour, a successful parry takes the **shield's points off the first damaging melee hit**, then is used
  up; spells and bows ignore it `[C]`. A failed or successful parry means no attack that turn.
- **Decision (user s89):** manual, as Apple II plus the hit-chance half: this round's Parry rolls the Parry skill %; on success the shield is shown raised, the shield's points come off monster melee chances that round and off the first damaging melee hit (after armour, then used up; spells and bows unaffected). The member does not attack that turn. Apple II's hurt-shield-arm auto-success is not copied. DOS's saved-option Parry as a `p3.*` option.
- **Remaster (s91, built):** rules option `p3.parry`. `manual`: at the start of each normal round every member whose order is
  Parry (or forced to it by a broken right arm) and who is Okay or Down rolls `rnd(100) ≤ Parry skill`; a success logs
  "<name> parries." and, for that round, takes the shield's points off monster melee chances and off the first damaging
  melee hit ("<name> blocks the blow with the shield!" when nothing is left); spells and breath unaffected
  (`P3CombatModule.RollParries` / `TryMonsterMelee`). Rolled at the round start so it holds against monsters acting
  before the member, as Apple II (s98 `[C]`: line 1520 `GOSUB 1525` rolls every Parry order (line 1527) right after the
  orders are confirmed, before the round's action loop at line 1600). `dos` (Original (DOS) preset): the
  shield points come off the chance all fight when the member's last order of the previous fight was Parry
  (`Phantasie3CharacterData.ParrySaved` = `+0x6e`, written at fight end, saved); no damage reduction. The fight-start
  write `0xd55b` (saved option) is not traced, so `+0x6e` is taken to keep the previous fight's value `[~]`.
- **Side effects:** the manual reading needs a damage-reduction amount the manual doesn't give (manual-silent detail).
- **Spec:** `logic-specs/combat-round.md` §4, `combat-actions-and-ranks.md` §4.

## D3-037 — The round after a successful encounter option

- **Manual:** Greet "greets the monsters in the hopes of avoiding combat. If the monsters don't respond, then the battle
  is on!"; Beg for Mercy: "If the monsters accept the party's surrender they take all of the party's gold, some of its new
  items, and then leave."; Flee: "If the adventurers are successful in fleeing, they will avoid combat; if they are
  unsuccessful the monsters get free shots." (III Encounter Menu, PDF 7 / pr.9–10.)
- **Code:** a successful Greet / Accept Surrender / Mercy / Flee sets `fight_on = 0` inside the party menu, but
  `combat_main` (root `0x104c0`) calls `combat_round(mode)` after the menu without testing `fight_on`
  (`0x10756…0x1077e`); the loop checks only after the round. So **one full round** (party actions and monster attacks)
  is still played after the monsters accept `[C]` control flow, `[~]` effect (live check wanted).
- **Decision (user s89):** manual: the fight ends at once; DOS's extra round is a bug and is fixed outright, with no option.
- **Remaster:** built: ends the fight at once (no round), as the manual reads.
- **Spec:** `logic-specs/combat-round.md` §1.

## D3-038 — Re-entering a dungeon: the saved map, time and DOS's load question

- **Manual:** "When the party leaves the dungeon, you will be given the choice of saving the dungeon status or not. When
  adventurers enter a dungeon for the second time, the monsters may be ready for them; new traps may be set, new locks on
  the doors, and new monsters may be in the dungeon. Monsters can only do so much redecorating in a short time, however,
  and if the party returns shortly after it left, then the monsters it has killed might still be dead." (III "Leaving the
  Dungeon", PDF 7.) The same text as Phantasie I's (`DIFFERENCES.md` D-027).
- **Code:** DOS keeps **one saved map per dungeon**, `DNP<letter>` / `DNG<letter>` (ovl4 `0x62d`, letter from
  `DS:0x1c84 + 2·dungeon`) `[C]`. On entry (ovl4 `0x14444`, s95 static) it asks **"[original text]"**
  (DS `0x62ec`); Yes reads `DNP<letter>` (`0x1080` bytes) into the work record, No (or no file) reads the pristine
  `DNGS.DAT` record `letter − 'A'`. The game's only time measure, the days-out counter `FAR:0x9e2`, is read in two
  places in the whole program (root `0x1c62`, the party status screen; ovl3 `0x676`, town-entry aging) and written by the
  step routine root `0x14a9`; no overlay-4 code reads it (s95 scan of the root and all four overlays). So nothing is
  restocked by time: the saved map comes back as saved, a fresh dungeon is the original `[C]`. The entry also stores
  `rand() & 7` at `DS:0x1c9c`, the copy-protection quiz's next question (s97 `[C]`; no effect on the map). Leaving by an exit asks
  "[original text]" (DS `0x6326`).
- **Decision (user s96):** keep I's D-027 reading. The manual gives no time, chance or trap data for restocking, and DOS
  reads no time at all, so a time-based restock would be an invented rule. The save-map question carries the manual's
  idea: YES = the party comes back soon (the dungeon stays as left), NO = the monsters had time (the original dungeon).
- **Remaster:** I's D-027 rule on III's per-dungeon maps (s95): each dungeon keeps its own saved map, an entry loads
  it **without asking**, a NO on leaving forgets only that dungeon's map, no time factor. Option `p3.dungeon-map-no`:
  `manual` (forget, as above; Original (manual) and Remaster presets) · `dos` (a NO keeps the old map; Original (DOS)).
  Under `dos` the entry also asks DOS's "[original text]" after "Will you enter?" (built s97: every
  entry, as DOS; Yes = the saved map if any, No = the original; the saved map stays either way).
- **Side effects:** saves keep every dungeon's map (`SessionSnapshot.OtherDungeonSaves`).
- **Spec:** `logic-specs/dungeon-events.md` §1.

## D3-039 — Bow spells cast over each other: the last one sets the Fire Bow %

- **Manual:** Accuracy / Party Accuracy "raise one adventurer's, or the whole party's, Fire Bow percentage to 250";
  Arrow Flame "increase[s] the accuracy and damage of bow fire … Arrow Flame 1 increases accuracy by +50 percent …
  Arrow Flame 2 … by +100 percent" (III spell list, PDF 10–11). Silent on casting one over the other.
- **Code:** each spell overwrites the fight's bow % word `FAR:0xdd9c + 2·member` `[C]` (s95 static): Arrow Flame (root
  `0xc3cc`) writes Fire Bow + 50 / + 100 and the damage word; Accuracy (`0xca34` / `0xca46`) writes 250 only. So an Arrow
  Flame cast after Accuracy **lowers** the % from 250 to Fire Bow + 50 / + 100 (for Fire Bow under 200 / 150), and
  Accuracy cast over Arrow Flame II with Fire Bow above 150 lowers it to 250. The damage of an earlier Arrow Flame stays
  under Accuracy.
- **Decision (user s96):** obey the manual: each spell "raises" / "increases", so none lowers what an earlier one gave.
- **Remaster:** `P3Fight.SetBowSpell` / `BowStatsOf`. Option `p3.bow-spells`: `manual` (Original (manual) and Remaster
  presets): Accuracy makes the % at least 250 and stays when an Arrow Flame follows; the higher Arrow Flame stays (I after
  II changes nothing) · `dos` (Original (DOS)): the last spell sets the %, as above.
- **Side effects:** none; damage follows D3-017 either way (under `manual` an Arrow Flame's damage also comes in after Accuracy).
- **Spec:** `logic-specs/bows.md` §5.

## D3-040 — Purge of a party member

- **Manual:** "Purge: this option drops an adventurer from the guild list from the party, and out of the game, never to
  be seen again. Use with caution." (III PDF 5, Guild Commands.) Read as: Purge also takes a party member out of the
  party. Silent on a loaded elemental.
- **Code:** DOS Guild ▸ Purge (root `guild_menu` `0xafe3…0xb09c`) `[C]` s98 static: needs more than the 3 elemental
  records in the roster; "Who shall be purged?"; an elemental (race 16) → "[original text]" (string 406);
  a character in the party (roster record `+0x24` ≠ 0) → **"[original text]"** (string 448, pointer
  `DS:0x1a08`); else "Are you sure you want to purge this character permanently?" and root `0x864a` closes the roster gap
  (records after it move down, every party record's roster slot `+0x7e` above it −1, roster count −1). Party slots and
  the elemental are never touched. So in DOS a party member must be dropped first, and Purge never meets a loaded
  elemental. **Apple II** (town lines 4390–4420) purges any guild number 1–37 (no party test) and then clears the party
  slots holding it, `FOR PM = 1 TO 6` over the 0-based slots (slot 6 = the elemental, numbers 38–40, never matches) `[C]`:
  a party member is purged and leaves the party, except one in the first slot, which the loop skips (an off-by-one `[~]`);
  the elemental stays. Amiga not checked.
- **Decision (user s99):** manual: a party member can be purged and leaves the party; a loaded elemental is handled as
  III's Drop (it stays while a character is left). I's rule (the elemental is unloaded) is an option. In no case can an
  elemental be removed from the game. Flagged as a QoL target for later (`PLAN.md` P3).
- **Remaster:** (s172, user: fold `p3.purge` into `elemental-purge`) the every-game option `elemental-purge`
  (`GuildHall.TryPurge`, `ElementalGuildRules.PurgeRule`) stays `game` in every preset, and III's `game` follows the
  preset: the Original (manual), Remaster and Custom presets purge as III's Drop (`phantasie3/rules.json`
  `elementals.purge` = `as-drop`), the Original (DOS) preset refuses ("[original text]",
  `purge_original` = `refused`). `phantasie-i` / `as-drop` / `refused` pick a rule outright. Side effect of reading the
  preset: a DOS-preset save whose rules are changed in any other option becomes Custom, and Custom reads the manual's
  side, so its III Purge turns from refused to as-Drop while the option still says GAME'S OWN RULE (pick `refused` to keep
  DOS's). The elemental records are Guild templates outside the roster; III's Purge list shows them after the characters
  (as DOS's roster holds them in slots 0-2), and picking one is refused with DOS's string 406, "You cannot purge an
  Elemental." (`core.guild.cannot_purge_elemental` in III's table; Amiga `0xb524`, window 7). Purging only unloads the
  party copy, and the elemental can be loaded again. s172 also: III's Drop list holds the loaded elemental after the
  characters and drops it by itself (DOS ovl2 `0x3c92`, `elementals.drops_elemental`).
- **QoL across the games (PLAN P3, built):** every-game options for I, II and III (`09-startup-options.md` section 4):
  `elemental-load`, `elemental-drop` and `elemental-purge`, `game` in every preset (so nothing above changes by
  default). `elemental-purge` = `phantasie-i` / `as-drop` / `refused` puts I's rule, III's manual (what the save's Drop
  does) or DOS III's refusal in any of the three games. Purge now also refuses an elemental
  itself in every game (`core.guild.cannot_purge_elemental`; DOS III string 406 is the same rule), so no game and no value
  removes an elemental from the game.
- **Side effects:** none beyond the Guild; DOS's refusal only adds a step (Drop, then Purge).
- **Spec:** `logic-specs/elementals.md` §1 (Purge).

## D3-041 — Old age

- **Manual:** "Age: the age of the adventurer in years. Age adversely affects an adventurer's attributes after he
  reaches his declining years. Each race has a different life span; elves live about 3000 years, dwarves and gnomes live
  about 200 years, humans live about 80 years, and most others croak off after only 40 to 50 years." (III PDF 6 / pr.7;
  the same text as I's.)
- **Code:** creation writes a maximum age `+0x2a` = `⌊LCK · factor⌋` (Human, Gnome 1; Dwarf, Halfling 2.5; Elf 19; every
  other race 0.4) and the age clock `+0x28` = `⌊(rnd(7) + 24) · LCK · factor + 260⌋` (ovl2 `0x2691…0x2748`) `[C]` s98
  (`FORMAT.md` §5.1). Read as I's lifespan steps of 256 age units (I compares `@264 >> 8` with `@252`, D-003) `[~]`:
  Human / Gnome 79–128 years, Dwarf / Halfling 197–320, Elf 1497–2433, the others 30–49. **Nothing reads `+0x2a`**: the
  only access in a linear sweep of the root and all four overlays is the creation write, and town-entry aging (ovl3
  `0x65a`) only adds to the age clock `[~]` (an access through a computed pointer is not ruled out). So DOS III has no
  decline and no death by old age; the age is shown on the sheet only.
- **Decision (user s99):** manual, with the logic already built for Phantasie I: the manual's text is I's word for word,
  so I's D-003 rule applies (lifespan = the manual figure ±10 % for the race, picked by LCK 8–13 as I's seed; Pixie and
  Halfling are "others", 40–50; decline ×4/5 once past 70 %, death past the lifespan, checked on town entry). DOS's
  maximum age read as 256-unit steps was not used (it makes Gnomes live like Humans, Halflings like Dwarves).
- **Remaster:** III's creation sets `Character.Lifespan` from `phantasie3/creation-tables.json` `lifespan_bands` (I's
  bands) and, with it, I's starting age (`CharacterCreation.StartingAgeClock`: 10 years + 10–12.5 % of the lifespan; one
  draw after DOS's age roll). Manual-silent detail handled automatically: DOS III's start age uses its own race factors
  (Halfling ×2.5), which would start a 40–50-year Halfling at 29–49 years, so it is kept only under the DOS option.
  `Aging.ApplyTownEntry` does the rest (III ages only members not Dead, ovl3 `0x65a`). Rules option `p3.lifespans`:
  `manual` · `dos` (Original (DOS) preset: DOS's start age, no lifespan). Related QoL: D4-033's "aging normalised across
  I–IV", built as `shared-aging` (`docs/plan/13-aging-normalised.md`): while on, III ages by the shared model even under
  `dos`.
- **Side effects:** start ages as I (others 14–16, Human 17–21, Dwarf / Gnome 28–37, Elf 280–422). Characters made
  before s99 have no lifespan (no old-save migration).
- **Spec:** `FORMAT.md` §5 (`+0x2a`) and §5.1; `logic-specs/planes.md` §3a (aging).

## D3-065 — Title page: "Hit any key"

- **Manual:** "Turn your computer on. The system will boot and the title page will be displayed. Hit any key to get to
  the Initial Menu." and "When you get to the title page everything is Okay. Hit any key to proceed to the Initial Menu."
  (III PDF, Loading the Game) `[X]`.
- **Code:** Amiga `0x736c` (s102): `Graphics/Title.cmp` is unpacked to the screen, the tune `D/M0` started (`0x1fa5c`),
  the title palette set (`0x7432`), then the game loads its other files (Anatomy1/2, Dng, Scroll, Dlgbttns,
  Phantasy.set, the seven sprite sheets, monsinfo / inititem, D/W4, D/W3) and clears the screen with a new palette at
  `0x7660`, with no key read in between `[C]`: the title stays only as long as the loading, about 20 s on FS-UAE (s156
  `[C] live`), and its tune `D/M0` (48.28 s at 60 Hz) plays on for about 28 s over the Initial Menu. DOS III (s156
  `[C] live`, DOSBox-X): the title picture, then over it "Are you playing off the hard drive?", "Would you like to
  transfer older characters saved from Phantasie I?" and the data-disk prompt ("… press a key..."), so the title waits
  for the player's input; no start-up tune (recorded silent with the PC speaker on).
- **Decision:** DECIDED (user, s103): **wait for a key**, as the manual says (the TITLE SCREEN option can switch the splash off).
- **Remaster:** the title splash (s102, `GameData/phantasie3/graphics/amiga.json` `title_sequence`) shows
  `Title.cmp` until a key or click, as the manual says. The Amiga's own length is the floppy load time, about 20 s on
  FS-UAE (s156, `reverse-engineering/amiga/working/capture_s156_p3_title.md`). The splash stops `D/M0` with the
  title until s157. (user s157) The tune is not stopped with the splash: it plays on into the game until its end or the first sound effect, as the Amiga's driver does (every effect start `0x1f52` resets the voices, FORMAT 7.2); it also stops on Main menu, another game or TITLE TUNE off, and a sound Core raises cuts it with the Sound switch off too; s158: so do a fight round's, a flash run's or the reward intro's first sound (at that sound's time), the astral defeat's sounds and the stop 33 before "[original text]" / "[original text]". TITLE SCREEN off: no splash, no tune. Pack `title_sequence.tune.plays_on`, Client `Audio.TitleTuneCarry`.
- **Side effects:** none (presentation only; the TITLE SCREEN option can switch the splash off).
- **Spec:** `reverse-engineering/amiga/phantasie3/FORMAT.md` "Title"; `docs/plan/09-startup-options.md` §8.

## D3-066 — Sound switch

- **Manual:** Time Lag "changes the speed of melee action ... The sound may also be turned on and off at this time"; the
  key list: "Set time lag in combat and toggle sound on/off" (III PDF, Wilderness / Combat options, key summary) `[X]`.
- **Code:** Amiga (s110, static): the sound routine `0xb126(n)` starts the effect on the music driver (`0x1f52`) with no
  switch tested; the program holds no "sound" text; its "Speed" menu (`0x8766` / `0x87ba` / `0x880c`) sets `0x240f6` to
  1 / 2 / 3, which only scales the combat line wait (`0x1bacc`: ÷ 4, × 1, × 2 of 30 ticks) `[C]`. DOS III (SSI 1988
  `MAIN.EXE`) does have one: "Sound" is an entry of its menus (the strings sit beside "Save Game" / "Quit Game" / "About",
  "Party Health", "Adv. Status") `[C]` strings, its handler not traced `[~]`. So only the Amiga port lacks the switch.
- **Decision:** DECIDED (user, s111): **the manual** — sound on / off is offered on III's Time Lag prompt. The
  Options tab / Esc-menu Sound row stays as the same switch.
- **Remaster (built, s112):** III's wilderness / dungeon **T** opens the shared T)IME LAG dialog (D-073): a digit for the
  lag, then "Sound Y/N" into the same Sound switch (pinned by `P3TimeLagSoundTests`; with the D-073 QoL switch on, both
  are Options rows instead). III's sound effects (sound 15 on the reward scene's and the fights' flashes,
  `sound_effects` in `GameData/phantasie3/graphics/amiga.json`) play only while the game's Sound switch is on (the
  manual's toggle; on by default, so the default sounds as the Amiga). The reward tune `D/M1` is not gated (as before).
- **Side effects:** none on the rules (presentation only).
- **Spec:** `reverse-engineering/amiga/phantasie3/FORMAT.md` §7.2.

## D3-067 — Wolf / Mud Elemental pictures

- **Manual:** silent (it shows no monster pictures by name) — not a manual conflict; logged here as a fixed bug of the
  original, so the end review sees it.
- **Code:** Amiga `0x1b4c8` (s97 / s125): a monster's picture is record byte 27; the sheets hold 0–47. Mud Elemental (48)
  and Wolf (65) get no picture cut, so the field shows whatever the slots last held (seen: two Sniverlings from the fight
  before), and on the first fight after boot the empty slot makes the blitter copy 65536 words a row and the game hangs
  `[C]` (`reverse-engineering/amiga/phantasie3/FORMAT.md` §6, §8.8). **DOS III** (decoded s160, DOS `FORMAT.md` section
  10): no bug there: root `0x10b8b` takes byte 27 (minus 1 above 27) and `0x7e89` falls back to picture 15, the
  Skeleton's, for any index past the giants, so both show a Skeleton (at the giant places) `[C]` static.
- **Decision (user s126):** a bug, fixed outright, **no option**: neither the stale picture nor the hang is reproduced.
  Both borrow the game's own art: **Wolf = the Wild Dog's picture** (byte 0; III already shares pictures, e.g. Wild Cat /
  Panther, Orc / Kobold / Goblin), **Mud Elemental = the summoned party elemental's** (heros picture 22; the Troll Baby
  already shows a heros picture).
- **Remaster (built s126):** `tools/scripts/extract_amiga_p3_graphics.py` `BORROWED_PICTURE` cuts those cells into
  `monsters/{idle,attacking}/31-wolf.png` and `53-mud-elemental.png`. Only the picture is borrowed: byte 27 is unchanged,
  so both stay giants (one group, at most two, the giant places and the flash's +2 bytes; `UiOverlays.FieldGiant`) and
  draw their normal-size pictures there, as the Amiga does with its stale one.
- **DOS CGA pack (decided user s161: "find an analogous creature and use those graphics instead"):** DOS shows the
  Skeleton's picture for Wolf and Mud Elemental and the Forest Elf's for the Troll Baby (byte 27 = 27). The pack borrows
  the same analogous creatures as the Amiga: Wolf = the Wild Dog's picture, Mud Elemental = the party elemental's
  (SHAPES.CGA party picture 22), Troll Baby = the party Troll's (party picture 20). Built s161 in
  `extract_dos_p3_graphics.py` `BORROWED_PICTURE` and `P3CgaArt.BorrowedPicture`; giant placement unchanged.
- **Side effects:** none on the rules (presentation only).
- **Spec:** `reverse-engineering/amiga/phantasie3/FORMAT.md` §6 (byte 27 table), §8.8; DOS: `reverse-engineering/dos/phantasie3/FORMAT.md` section 10.

## D3-068 — Dungeon Yes / No questions: Return's answer

- **Manual:** silent (it names no default answer) — not a manual conflict; a DOS ↔ Amiga difference with a play effect,
  logged for the user.
- **Code:** DOS III opens the dungeon Yes / No questions with an answer lit (ovl4 `0x1b81` → root `0x1820`, s108 static
  `[C]`): "… [original text]" Yes, "… [original text]" **No**, "[original text]" and "Would
  you like to save this map?" **No**; Return takes the lit one. The Amiga asks all four in window 1 (`0x1d77e`, OK /
  CANCEL, s128 / s134 static `[C]`): no highlight, Return / `o` / `O` = OK (`0x3ba0`). OK = continue: on "hears
  monsters" OK sets the fight (`0x1c56a` → `0x1da5c`), CANCEL skips it (`0x1c572`); on the trap OK opens "who disarms"
  (`0x1c6d6`, window 6 `0x127ce(92)`).
- **Decision:** (user, s135) keep the Amiga way: under AMIGA SCREENS, window 1 with no highlight and Return = OK; with the
  remaster's prompt bar, DOS's lit answer.
- **Remaster (s128, s134):** with AMIGA SCREENS on and the III Amiga pack, the four questions are window 1 and Return =
  OK (the exit / save-map since s128, "hears monsters" / trap since s134); with the remaster's prompt bar, DOS's lit
  answer. So under AMIGA SCREENS a Return on "[original text]" walks into the fight, where DOS's Return goes
  back. s160 (user s159: the town's questions under every III layout): the same split for "Are you sure …?" before Load,
  Save, Distribute and a Purge pick (DOS root `0x1820`(text, 2), "No" lit): window 1 with Return = OK under AMIGA
  SCREENS, DOS's prompt with Return = No otherwise (`P3ConfirmQuestion`).
- **Side effects:** none on the rules (which answer Return gives only).
- **Spec:** `reverse-engineering/amiga/phantasie3/FORMAT.md` section 8.7 ("window 1"), `Phantasie3Campaign.YesNoHighlight`.

## D3-069 — Inn: whose magic maximum can grow

- **Manual:** "Characters who have cast spells while in the wilderness may have their maximum number of Magic Points
  increased" (III manual, THE INN).
- **Code:** DOS III (ovl2 `0x347d`, s136 static `[C]`) tests no casting: the chance (`20 · (ceiling − max) > rnd(100)`)
  is rolled for **every living member** on the **first inn stay after a fight** (`FAR:0xde70`, cleared at each fight's
  start by `combat_main`, root `0x1057c`, set by the inn); a second stay with no fight between raises nobody. Casting
  outside a fight does not count; a member who never casts still grows.
- **Decision:** DECIDED (user, s137): obey the manual.
- **Remaster:** a member gains only after casting a spell outside town (in a fight, the wilderness or a dungeon) since the
  last stay (I's flag `CastSinceRest`; a III town cast no longer sets it, s137); the rest of the routine is DOS's (s136 `P3Inn`: the ceiling refreshed from the Maximum Magic Points
  table for the member's level, the 20 % per point chance, the dead skipped).
- **Side effects:** with DOS's gate a party that fights and rests grows every caster's maximum (and non-casters'
  ceiling-bound maximum too); with the manual's only the casters grow.
- **Spec:** `reverse-engineering/dos/phantasie3/logic-specs/body-areas.md` section 4 ("The inn routine").

## D3-070 — Outside-town keys: E moves east (manual) or examines a member (Amiga)

- **Manual:** the Adventure Menu ("the same in the dungeon as in the Wilderness"): C cast, I inspect, U use an item,
  T time lag / sound, L look (dungeon), 3 / N north, W west, **E east**, S south ("so that the 3-E-S-W diamond can be
  used"; III manual, ADVENTURE MENU). The movement paragraph adds "Atari ST, Amiga and Macintosh users can use their
  mouse … Apple users use the diamond of keys", and the Amiga card (Australian manual) says "Movement is entered through
  either the mouse or use of the cursor (arrow) keys" and that Speed replaces the Time Lag; it names no letter keys.
- **Code:** Amiga (s140, `[C]` static and seen in FS-UAE, `reverse-engineering/amiga/phantasie3/FORMAT.md` section 8.9):
  outside the towns the keys go to `0x83e0` (the dungeon's own loop `0x1c052` passes the same letters there): `?` / `/`
  Info (credits), `C` Cast Spell, **`E` Examine Member** ("[original text]"), **`P` Party Status** (a trip
  summary window), `U` Use Item, `+ = -` speed; moves only by the number pad's 8 / 2 / 4 / 6, the cursor keys and the
  mouse. No letter moves the party (no N / E / S / W), and there is no I, T or L. Seen: E on the overworld and in a
  dungeon opened the member list; the party did not move.
  **DOS III (s143) `[C]` static and live:** E does nothing on the map. The wilderness loop (ovl3 `0x88`) hands the key
  to ovl3 `0x28c`, which moves only on the four cursor keys and tests no letter; the map's bar (Options: Continue on,
  Cast Spell, Use Item, Show Party, Adv. Status, Sound, About) has no Examine. Seen in DOSBox-X: `e` / `E` left the
  party in place, cursor-right moved it (`reverse-engineering/dos/phantasie3/capture_s143_town_entry.md` section 2,
  `screens-s143/s143_map_e_key_nothing.png`). So DOS has neither the manual's E = east nor the Amiga's E = Examine:
  **DOS contradicts the manual here too**; the decision below already follows the manual.
- **Decision:** DECIDED (user, s141): **the manual**, E moves east (the Amiga bar too).
- **Note:** the difference is in which key does what under the Amiga's menu bar (AMIGA SCREENS), not in a game rule.
  Related: D3-068 (Return in window 1).
- **Remaster:** E moves east (the manual's, I's D-076) under the Amiga bar too; the bar's own items (Options ›
  Examine Member, the mouse) still examine. The keys that do not clash with the manual are bound under the bar (s140):
  `?` / `/` Info, `P` Party Status (the manual has no P outside towns). The manual's I / T / L stay.
- **Side effects:** none; the Amiga's E (Examine Member) is reached through the bar's menu or the mouse.
- **Spec:** `reverse-engineering/amiga/phantasie3/FORMAT.md` sections 8.5 and 8.9; `OpenPhantasie.Client.AmigaOutsideBar.ForKey`.

## D3-071 — XP pool and found items: paid out on town entry or by Distribute

- **Manual:** "the experience will be divided among the adventurers when they reach a town" (III manual).
- **Code:** Amiga (s141, `[C]` static): town entry `0x7964` calls `0xbc7c(1)`, which shares out the experience pool and
  the found items. **DOS III (s143) does the same** `[C]` static and live: every town arrival runs root `0xa911`
  (the main loop `0x0000` → `0x8f`), which after aging and prices calls ovl2 `0x0000`(1) (`distribute_experience`, the
  Amiga's `0xbc7c` twin) when the XP pool or the found count is above 0. That path readies each member's best
  equipment, drops the wand (item 180) from the found list, opens the disburse / sell screen (ovl2 `0x061f`) for the
  found items **first**, then asks "Shares for <name> :" (1–3) for each member and splits the pool. A wiped party's
  arrival empties the pool and sack instead; the endgame's arrival (`0xfc9a` = 3) keeps them and pays out. Options ›
  Distribute (ovl2 `0x0000`(2)) re-pools the carried items only and never shares XP `[C]`. Seen in DOSBox-X: XP pool
  500 and two found items poked, "Will you enter?" Yes → item screen → shares screen → pool 0, fighto XP 500
  (`reverse-engineering/dos/phantasie3/capture_s143_town_entry.md`). So DOS agrees with the manual (not a conflict).
  s144 static (same file, section 3) `[C]`: the loops run over the characters only (never the elemental); readying
  never empties a slot and gives ties to the later item; `0x5a9` drops only the last wand, for good (no quest bit); the
  shares are asked of every character, a dead one 0–3 (pre-filled 0), any other 1–3 (pre-filled with `+0x54`); the
  remainder of pool ÷ total shares goes out point by point to random characters, so nothing is lost. Options ›
  Distribute empties the found list first and destroys a carried wand (not built `[~]`). The manual is silent on all of
  these details.
- **Decision:** DECIDED (user s142): obey the manual.
- **Remaster (s142, DOS order s143, DOS detail s144):** every ordinary town entry (`GameSession.EnterTown`,
  `ICampaign.PaysOutOnTownEntry` = true for III) with a pool or found items first readies each character's best
  equipment and drops the last wand from the found list (`ICampaign.BeforeTownEntryPayout` → `P3TownEntryPayout`:
  points / requirement / attribute sum from `items.json`, slots from `rules.json`, the dropped id from `rule-tables.json`
  `town_entry_payout`), then opens the hand-out screen for the found items only (`TakeTownItemHandOut` → the Client's
  `DistributeDialog`, `BeginItemDistribution(foundOnly: true)`; carried items stay put; a lone wand opens nothing), then
  asks "Shares for NAME :" for every character (`GameSession.TownEntryShareTakers`, no elemental; dead 0–3 lit on 0,
  others 1–3 lit on the stored `Phantasie3CharacterData.ExperienceShares`, the `+0x54` word, saved) and splits the pool
  DOS's way (`P3TownEntryPayout.ShareOut`: the remainder drawn point by point, nothing lost). A defeat had already
  emptied the sack (Amiga `0x793c`). I / II keep Distribute only. Left `[~]`: found gold joins the purse on entry (s142's
  rule; DOS's payout covers only the pool and the items); the town's Distribute still shares any pool (DOS's never does;
  the pool is 0 in town after entry) and neither empties the found list nor destroys a carried wand as DOS's does; the
  hand-out screen's "who can use" column still uses I's usability rule, not III's (`ItemDistribution`).
- **Side effects:** Distribute in town has nothing left to share unless something is found in town; window 22 no
  longer shows a stale pool after a town visit. Leaving town before the hand-out or the shares (not possible from the
  menus) drops them: the items and the pool stay in the sack for the next town.
- **Spec:** `reverse-engineering/amiga/phantasie3/FORMAT.md` section 8.9; `capture_s141_p3_window22.md`.

## D3-072 — I / II → III transfer: how much experience is kept

- **Manual:** "The transferred characters will begin at level 0, but should have enough remaining experience and money to
  train up several levels. At the start of the game these characters should go to the bank, withdraw their money, and
  then go to the Guild and train." (III PDF 13 / pr.20.) No number is given; elsewhere the "majority" of the experience is
  lost (`09-trilogy-manual-p3.md`).
- **Code:** DOS III's converter writes XP = **100 × old level** `[C]`, verified live s81 (D3-016). III's training needs
  `(1000·G[L] − 80000) / class score` XP in total: for a typical fighter (score ≈ 130) about 770 for level 1, 1,900 for
  level 2 and 3,900 for level 3. So a level-10 I character (1,000 XP) can train once and a level-20 one (2,000) twice;
  under DOS's own level 1 the level-10 character cannot train at all. Apple III's transfer computes `EXP(LOG(Q)/3.5) − 1`
  (`wand-and-transfer.md` section 2); what Q is was not decoded `[?]`. Bank gold (old ÷ 35 plus the creation roll) is not in
  question here.
- **Decision:** DECIDED (user, s148, 2026-11-02): keep DOS's system, 100 XP per old level.
- **Remaster:** DOS's 100 × old level (`rule-tables.json` `transfer.xp_per_level`), under every preset; no option.
- **Side effects:** only transferred characters; their first trainings and their power when they arrive.

## D3-073 — Armory list: "Unusable" or "N/A", and the requirement-0 items

- **Manual:** "The list will show the Item Number (used to buy the item), the Item Name, the Cost (in Gold Pieces), and
  the Point Value of Weapons and Equipment. If the buyer does not have the necessary minimum attributes to use an item,
  the Item will be listed as Unusable." (III PDF 6, Armory.)
- **Code:** DOS III's list routine (ovl2 `0x32c8`, s174 `[C]` static, not seen live) prints the name, a **Pt Val** text and
  the cost per row. The Pt Val text is **"N/A"** when the item's requirement (`DS:0x11a2`) is above the shopper's
  attribute sum (`0x39b5`: STR; bows STR + DEX; melee 2·STR + DEX), and also **"N/A"** whenever the requirement is 0:
  every potion, scroll and treasure (no points), and the three equipment items with requirement 0, **31 Cloth +1,
  32 Robes +1 and 41 Self Bow**, which anyone can use and which have points 2, 3 and 1. Otherwise it prints the points.
  The word "Unusable" is not in DOS III; the Amiga program has neither word. Apple II III (disk 1 side A, BASIC line 1220)
  prints `"  UNUSABLE"` in the points column and the points of every usable item, so the manual describes the Apple II.
  So DOS differs from the manual twice: the word (N/A for Unusable), and no point value for three usable items.
- **Decision:** (user s174) manual ("not a significant difference"): points for every usable weapon, bow, shield and
  armour, UNUSABLE for the rest; no DOS option.
- **Remaster (s174):** III's Armory rows gain a column between the name and the stock, for the shopper picked:
  the item's points (`ICampaign.ItemPoints`, III's table) when III's gate passes (`CanUseItem`), `UNUSABLE`
  (`core.armory.unusable`) when not, empty for potions, scrolls and treasure (`GameSession.ArmoryPointsText`,
  `ICampaign.ArmoryListsPoints`, `UiOverlays.DrawArmory`). Cloth +1, Robes +1 and Self Bow show 2, 3 and 1. Shot
  `parity/shots/p3-s174-armory-points.png`.
- **Side effects:** I and II's list is unchanged (no column); the column changes with the shopper (1–6). The list has no
  column heading (the manual names none; DOS's "Pt Val" header is not drawn).
- **Spec:** `reverse-engineering/dos/phantasie3/logic-specs/town.md` section 7 ("The list rows").
