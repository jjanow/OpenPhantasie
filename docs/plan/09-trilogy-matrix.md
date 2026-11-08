# Phase 9 T0 — Trilogy sourcing, platforms, features, transfer

Cold file for `09-trilogy.md` T0 (s69, 2026-10-25). Sources: the manuals in `docs/manuals/` (index: `docs/manuals/README.md`)
and the disk images in `reverse-engineering/<platform>/original-images/<game>/` (each platform's `SOURCES.txt` lists origin,
version, clean/cracked and sha256). Phantasie I's existing images moved into `phantasie1/` subfolders in the same session.

## 1. Platform matrix

Which platforms each game shipped on (Wikipedia platform lists, checked against what dumps exist) and what is in the repo.
**Bold** = preferred image set per cell. "Art" says whether the platform is a candidate graphics pack (T1.5 / T5).

| Platform | Phantasie I | Phantasie II | Phantasie III | Art |
|---|---|---|---|---|
| DOS (IBM PC) | **SSI 1987** (`dos/…/phantasie1`, parity reference), Bonus Ed. v1.1 1991 | — (no release) | **SSI 1988** `[!]`, WizardWorks 1991 | CGA, 4 colours |
| Amiga | **v1.0 1987** (current art source) | — (no release) | **v1.0 1987** | yes (default) |
| Commodore 64 | **English** (`en/`), German cracked (`de/`, the s13 C64 reference) | **v1.0 sides A+B**, v1.2 side A only | **v1.1**, 1987 cracked set | yes |
| Apple II | **4am crack** + original `.woz` | **4am crack** + original `.woz` | 1987 `[cr]` (no clean dump found) | yes |
| Atari ST | **v2.0 English** (Pasti), v1.0 German (Pasti), TOSEC repacked | **v1.0** (Pasti), TOSEC `.st` | **1987** `.st` | yes |
| Atari 8-bit | **4 sides** (atarimania) | **4 sides** (atarimania) | — (no release) | yes |
| MSX2 (JP, Bothtec) | 1988 | 1988 "Story of Ferronrah" | 1989 | JP port |
| PC-88 (JP, Star Craft) | 1986 | 1987 | 1988 | JP port |
| PC-98 (JP, Star Craft) | yes | yes | yes | JP port |
| Sharp X1 (JP, Star Craft) | yes | not found | not found | JP port |
| FM-7 (JP) | not found | not found | not found | — |
| X68000 (JP) | — | — | not found | — |

Notes:
- Japanese ports are separate code bases by Star Craft / Bothtec. They can be a rules or art source where that makes
  sense (the s69 "reference only" rule was dropped in s70); for IV they are the only source.
- **Phantasie IV** (sourced s71, T6.0): Japan-only, designed by Wood and coded by Star Craft. All four platforms are in the
  repo (`<platform>/original-images/phantasie4/`):
  - PC-88: Sep 1990, 3×2D
  - PC-98: 14 Sep 1990, 2×2HD, runs on MS-DOS 3.05
  - MSX2: 30 Jan 1991, 2×2DD. TOSEC has the disk labels swapped. The 2008 DJANGO English patch is stored as pre-patched
    images in `patch-2008/`
  - **X68000: 28 Mar 1991, the last release, so it is IV's rules source (user, s71).** The date rests on MobyGames and
    en.wikipedia only. 2 disk dumps exist; one database says 3 floppies `[?]`
  
  **The PC-98 manual was found s81** (MOCAGH: manual, reference card / supplement, spell list, owner's and registration
  cards; Suruga-ya lists the full box as 2 disks, manual, 補足マニュアル supplement, 呪文一覧表 spell list, owner's card).
  It is IV's written source of truth; the MSX Magazine articles, the postcard ad and the item catalogue are secondary
  (all in `docs/manuals/phantasie4/`; guides, walkthroughs and interviews: `phantasie4-secondary-sources.md`). Design:
  Wood designed IV on paper and never played it; Star Craft did everything else (Wood / Sato interviews, RPG Codex). Parentage and transfer: `09-trilogy-iv-parentage.md`. en.wikipedia also lists an X68000
  Phantasie III (1989-08), but no dump was found. file-hunter has English MSX2 builds of I and II, which are not in the repo.
- Phantasie II has no DOS or Amiga release. **Rules source: Atari ST** (user, s71: the last-released of the Apple II / C64 /
  ST options; ST also supplies II's art). Release dates:
  - Apple II: Mar 1986 (the disk says "VERSION 1.0 2/5/86")
  - C64: Aug 1986
  - **Atari ST: Feb 1987** (program files dated Dec 1986)
  - Later releases that were not among the options: Atari 8-bit (Dec 1987) and the JP ports (PC-88 and PC-98 Mar 1987,
    FM-7/X1 Apr 1987, MSX2 1988)
  
  Both ST sets are played copies: TOSEC `.st` (save data from 1987-04-22) and the v1.0 Pasti set (saves dated 1995-10-06,
  7 Guild characters; s75). Program and data files are byte-identical; the Pasti set is the one extracted (T3). Neither has
  fresh default saves, so starting state comes from the `.INT` files or a new game. The C64 "v1.0/v1.2"
  labels rest only on archive item names `[?]`.
- Not found anywhere: the English Atari ST Phantasie I v1.0 two-disk original; a clean (uncracked) Apple II Phantasie III.
- Phantasie I DOS Bonus Edition v1.1 (WizardWorks 1991, also the Memorial Set's I) is 1987's program repacked
  (`PHANTASI.EXE`, LZEXE over EXEPACK): diffed s168 (`dos/bonus-edition.md`), it fixes none of the DOS faults in
  `DIFFERENCES.md` (saves are no longer hidden files, the manual page lines changed). The importer reads it as 1987.

## 2. Feature matrix

Summary from the manuals. Full detail with page citations: `09-trilogy-manual-p2.md` and `09-trilogy-manual-p3.md`.
**Home** = the game where the feature first appears (on by default there, an option elsewhere per `09-trilogy.md` T1.6).

**What the manuals are:** II has no standalone rulebook. `phantasie2-manual.pdf` is the combined "Phantasie I and Phantasie II
Rulebook", whose shared rules (pp.3–16) are almost word for word I's manual. II's own changes are on one page (p.27, "Phantasie
II Updated Rules", repeated in the readme). III has a full rulebook of its own.

| Feature | I | II | III | Home |
|---|---|---|---|---|
| Setting / start | Gelnor, Pelnor, 11 towns, 16 map sections | Ferronrah (south of Gelnor) | Scandor, Pendragon, 25 sections (5×5) + other planes | each |
| Quest items | 20 scrolls, 9 rings, wand | ~15 scrolls, amulets + enchanted orb | scrolls; Wand of Nikademus survives transfer; wise man Filmon | each |
| Races, classes, attribute range, race maxima, class modifiers | base | same | same | I |
| **Rock throwing** (combat "7) Toss Rock": any rank, unlimited rocks, own skill) | — | **yes** | removed ("bows have replaced thrown rocks") | **II** |
| **Body-part injury** (Head, Torso, L/R Arm, L/R Leg; Okay/Injured/Broken/Gone; death by head/torso; limb limits; healing tiers; inn cures 1 Break or 2 Injuries) | — | — | **yes**, on adventurers and monsters | **III** |
| Bows (separate bow slot, Fire Bow skill, weapons #41–60 become bows, armour less effective vs arrows, combat "8) Fire") | — | — | yes | III |
| Aim (one blow at head or torso, lower hit chance) | — | — | yes | III |
| Party ranks Front / Middle / Back (relative hit/defence bonus, hand weapons only) | — | — | yes | III |
| Combat "9) Other" submenu (ranks, Time Lag, Redo) | Redo = 7 | Redo = 7 (clashes with Toss Rock = 7 on Apple keys) | yes | III |
| Monster status U/S/D/K; monsters may flee each round; failed Flee gives free shots | — | — | stated | III |
| Monster Reputation table (fighting skill, Good/Neutral/Bad) | — | — | yes | III |
| Social class (Peasant, Laborer, Craftsman, Noble): starting gold + gold per level | — (256 GP each) | — | yes | III |
| Skill training (3 skills per level, diminishing returns) | — | — | yes | III |
| Spells #13–16 | Strength 1–4 | same | Arrow Flame 1/2 + party versions | III |
| Spell #53 Vision | yes | yes | removed (map always shown) | — |
| Spells #55/#56 | — | — | Accuracy, Party Accuracy | III |
| Divine spells (found in the game only) | — | #58+ (p.14) or #55+ (transfer text) | #57+ | II |
| Elementals | Fire/Water/Earth, only for a party of six | same | Mud/Mist/Air, one extra member that doesn't count | III |
| Healing spells and potions also heal body areas | HP only | HP only | yes | III |
| New terrain: lava, mist, dark voids | — | yes | — | II |
| Seas; no bridges; no encounters on water | rivers, bridges | — | yes | III |
| Wilderness map always fully shown | DOS reveals as you travel (built) | — | yes | III |
| Health command in the wilderness | yes | yes | removed (Inspect shows HP/MP) | — |
| Secret doors; dungeon exit only at exits | — | — | yes | III |
| Town T)rade items (give to a member or sell) | — | — | yes | III |
| Bank deposit fee | — | — | yes (amount not given) | III |
| Training / spell costs from the party treasury | carried gold | carried gold | party treasury | III |
| Rename a party member in the Guild | no (manual; D-065: DOS allows it) | no | yes | III |
| Autosave when leaving town | no | yes (Apple rulebook p.19) | yes (rulebook; the ST/Amiga cards say only File→Save saves) | II |
| Pre-generated starting party | — | — | yes (not on IBM) | III |
| Guild size | 37 Apple / 21 C64 / 22 DOS / 22 slots Amiga (`guild_by_platform`, T1.3) | same | "dozens" | each |
| Gold caps | 65,000 party and account | same | 65,000; ST (and maybe Amiga): 1,000,000,000, shared purse | each |
| Party size 6, 9 items per character, 90 party items, level tables to 15 | yes | same | same (+1 elemental) | I |
| Equipment tables | shields, armour, 60 melee weapons | same | same shields/armour; 20 bows + 40 renumbered melee weapons | each |

**For the user's two example features:** rock throwing is a **Phantasie II** feature that III replaced with bows; body-part
injury is a **Phantasie III** feature. As cross-game options (T1.6, T5), I monsters would need body-area data and II/III
characters a Toss Rock skill. That monster-data question was resolved in s75 (`09-trilogy.md` T5 "Cross-game modules").

## 3. Transfer rules

**II** describes I → II transfer in its Apple and C64 sections and in the readme (combined rulebook pp.19, 24, 27). **III**
describes it **only in its Apple section**; the C64, Atari ST, Amiga and IBM cards and the WizardWorks manual say nothing (the
C64 III card's "There are no Transfer Characters to or from Backup Disk options" is about backups). The code settled it:
DOS and Amiga III import Phantasie I characters only (`reverse-engineering/dos/phantasie3/FORMAT.md` section 7; D3-004).

| | I → II (combined rulebook pp.19, 24, 27; readme) | I or II → III (III rulebook pr.19–20) |
|---|---|---|
| Where | Boot the II dungeon side, "Option 6", or the Utilities function (the two passages disagree; printed menus show only 1–5) | Utilities → Other Utilities → 6 "Transfer Characters From PHANTASIE I Or II", first thing after making the Player and Dungeon disks |
| Existing characters on the new disk | erased | erased |
| Items | all lost except the Wand of Nikademus | all lost except the Wand of Nikademus |
| Gold | "the majority" lost | "the majority" lost |
| Experience / level | most lost; level 0 with enough XP to train up | most lost; level 0, "enough remaining experience and money to train up several levels" |
| Spells | lost except divine (#55+); Wizards keep #5, Priests #1 | lost except divine (#57+) and "their normal beginning spell by class" |
| Not stated | attributes, HP, MP, age, skills, status, bank gold, Guild number, runes | same, plus Fire Bow skill, social class, Toss Rock skill, II's amulets and orb, body areas |

**III → IV** (s71 from IV's interface text and magazines; IV's manual p. 8, found s81, agrees): only III characters can be
transferred, and they go into IV's **Hall of Glory** as possible ancestors for new characters, not into the party. At most
8 can be transferred, and biography lines are generated from deeds in III. Detail and remaster options:
`09-trilogy-iv-parentage.md`.

Exact amounts (how much gold and XP survive) are in the code, not the manuals. They are decoded and every transfer is built
(`09-trilogy.md` T5): `reverse-engineering/atari-st/phantasie2/logic-specs/transfer.md` (I ↔ II),
`reverse-engineering/dos/phantasie3/FORMAT.md` section 6 (I / II → III), `ancestry.md` sections 8.2–8.4 (into IV).

## 4. Manual findings that touch Phantasie I

- WizardWorks 1990 manual: Thrust "less likely to hit". The original manual says "more likely", and DOS agrees with the
  original (Thrust's action penalty ≈ 11 vs ATTACK's ≈ 20, `combat-resolution.md`). A reprint error, not a conflict.
- WizardWorks 1990: the wilderness map starts blank and is revealed as you travel. The original manual is silent; DOS does
  this and the remaster already builds it. Not a conflict.
- The combined I+II rulebook says a high-level **wizard** casts Resurrect (p.10); I's manual says priest and the spell tables
  agree. Probably a misprint; I's manual stays the source of truth. (For II the user took the wizard text: D2-015.)

## 5. Open questions (no guesses)

The full lists are in the two comparison files (section 3 of each). The ones that mattered for the plan, as the manuals
leave them; the code has since answered most (`03b-phantasie2-re.md` and `reverse-engineering/dos/phantasie3/FORMAT.md`
section 7, "Manual questions answered"; endgames in each game's `endgame.md`, Toss Rock in `toss-rock.md`, autosave
D2-016 / D3-028). Still open: the Amiga III gold cap and Apple-only details (II's Demo option).
- Transfer: amounts of gold/XP kept; which "beginning spell" (III L1 casters have two); divine-spell numbering (#55, #57,
  #58); what happens to stats, skills, age, social class, Fire Bow, Toss Rock; which platforms support it at all.
- Body areas: 4 (Inspect) vs 6 (melee section); per-location HP?; hit-location odds; Gone vs Broken limits; whether the inn
  cures Breaks.
- Rock tossing numbers and key (clashes with Redo = 7 on the Apple key table).
- Autosave: rulebook (on leaving town) vs ST/Amiga cards (File→Save only); Amiga gold cap.
- Social class: three names for a stated four; gold per class.
- II's and III's endgames are not described in the manuals.
