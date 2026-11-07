# Phantasie II vs Phantasie I: rules comparison from the SSI manuals

> Cold file (s69, Phase 09 T0). Full manual-to-manual comparison behind `09-trilogy-matrix.md`; every claim carries a page
> citation. Built from the manuals' text layers and page renders; check the cited page before building a rule on it.

## Sources and citation keys

| Key | File | Notes |
|---|---|---|
| **P1** | `docs/manuals/phantasie1/Phantasie_I_1986_SSI_manual_text.pdf` | 1985 Apple rulebook plus the C64 supplement. Cited as printed page / PDF page. Printed p.1 = PDF 6, p.2 = PDF 7, p.3 = PDF 8 … p.10 = PDF 15. Printed pp.11–12 are missing (PDF 16 is blank). p.13 = PDF 17 … p.19 = PDF 23. The terrain key is PDF 25. The C64 supplement is PDF 2–3. |
| **P1RC** | `docs/manuals/phantasie1/phantasie1-refcard.pdf` | The same C64 supplement as P1 PDF 2–3. |
| **CB** | `docs/manuals/phantasie2/phantasie2-manual.pdf` | **Not a II-only manual.** It is the combined "PHANTASIE I and PHANTASIE II RULEBOOK" for Apple, C64, Atari 800, Atari ST and Mac (CB PDF 1). Cited as printed page / PDF page. p.1–2 = PDF 3, p.3–4 = PDF 4, p.5–6 = PDF 5 … p.27 = PDF 16. PDF 17 is a spell-table card. |
| **ALT** | `docs/manuals/phantasie2/phantasie2-manual-alt.pdf` | A second printing of CB with the same pagination. |
| **RM** | `docs/manuals/phantasie2/phantasie2-readme.pdf` | "READ THIS CARD FIRST": PDF 1 is the story, PDF 2 is "UPDATE". |
| **SCAN** | `docs/manuals/phantasie1-and-2/phantasie1-and-2-rulebook-ssi-200dpi.pdf` | The same combined rulebook, one page per image. PDF 30 = printed p.27 (Updated Rules). |
| **P3** | `docs/manuals/phantasie3/phantasie3-manual.pdf` | Used only for item 4. |

**The key fact.** CB says that "The following pages (3 - 16) describe the general rules, player hints, and characteristics of PHANTASIE and PHANTASIE II" (CB p.3 / PDF 4). Its Updated Rules page says "PHANTASIE II utilizes the same rules as PHANTASIE I with the following changes" (CB p.27 / PDF 16; RM PDF 2 has the same sentence).

I spot-checked the general rules in CB pp.3–16 against P1 pp.2–19, and they are the P1 text almost word for word. Values checked:
- Guild cap of 37, party of 6, 65,000 GP, 256 GP starting gold
- 9 items per character, 90 found items, 250 GP inns, luck 8–13, lifespans
- 80 monster types, 11 towns, 16 map sections
- Attack options, the HP and magic-point tables, spells by class, the spell list, equipment and potion tables

So the manuals document **only a small set of II changes**. Every "no change stated" row below means CB's shared general rules apply unchanged; it does not mean confirmed identical in the game code.

---

## 1. What II adds or changes, by area

| Area | Phantasie I (P1) | Phantasie II (CB / RM) | Status |
|---|---|---|---|
| **Setting** | Isle of Gelnor; start in Pelnor, "one of the 11 towns" (P1 p.1 / PDF 6; p.3 / PDF 8) | Island of Ferronrah, "just south of the Isle of Gelnor", under a cloud for 40 years; Nikademus is enslaving people to make weapons (CB p.2 / PDF 3; RM PDF 1). The shared Towns text still says "START IN PELNOR … one of the 11 towns of Gelnor" (CB p.4 / PDF 4). | **Changed** (setting). II's towns are not documented. |
| **Character creation**: races, classes, stat caps, class modifiers, attribute range 3–22, luck 8–13 | P1 pp.2–3 / PDF 7–8; luck on p.6 / PDF 11 | Same tables and text (CB pp.3–4 / PDF 4; luck CB p.7 / PDF 6) | No change stated |
| **Skills** | Attack, Parry, Swim, Listen, Spot Trap, Disarm Trap, Find Item, Pick Lock (P1 p.7 / PDF 12) | Same list in the shared text (CB p.8 / PDF 6). **Adds a "Toss Rock skill level"** (CB p.27 / PDF 16; RM PDF 2). The sample Inspect screen does not show it. | **Added** (one skill) |
| **Levels and training** | Training at the Guild costs gold; charisma affects price; random creatures pay more; HP/MP/spell tables cover levels 1–15 (P1 p.3 / PDF 8; p.5 / PDF 10; p.13 / PDF 17; p.16 / PDF 20) | Same (CB pp.4–5 / PDF 4–5; p.11 / PDF 8; p.14 / PDF 9). The only II-specific note: transferred characters arrive "at level 0" (CB p.27). | No change stated, except transfer |
| **Combat: actions** | 1 Thrust, 2 Attack, 3 Slash, 4 Lunge, 5 Cast, 6 Parry, 7 Redo (P1 p.10 / PDF 15). C64 drops Redo (P1 PDF 3). | **New "7) Toss Rock."** Rocks may be thrown "at any enemy rank"; "Each character has an unlimited supply"; accuracy depends on the Toss Rock skill (CB p.27 / PDF 16; RM PDF 2; SCAN PDF 30). The Apple key table in CB still lists **7 = Redo options** (CB p.19 / PDF 12). | **Added**. Key 7 conflicts with Redo. |
| **Combat: ranks** | Most attacks only reach Rank 1; Lunge reaches Rank 2; thieves can hit any rank (P1 p.9–10 / PDF 14–15) | Same (CB pp.9–10 / PDF 7), plus Toss Rock hits any rank (CB p.27) | **Extended** by Toss Rock |
| **Combat: damage, party options, fleeing** | Thrust does 1–2 more damage, Slash 1–2 less; party options Fight / Accept surrender / Greet / Beg mercy / Flee (P1 p.10 / PDF 15) | Same (CB pp.9–10 / PDF 7; C64 list on CB p.23 / PDF 14) | No change stated |
| **Injuries** | HP only; death at 0 HP (P1 p.13 / PDF 17) | HP only (CB p.10 / PDF 7). No injury system mentioned. | No change |
| **Magic** | Spells 1–54, costs 1–4, same types (P1 pp.14–16 / PDF 18–20). No "divine spells" text in P1. | Same 54-spell list, costs and class/level tables (CB pp.11–14 / PDF 8–9; card PDF 17). CB adds: "DIVINE SPELLS (#58+) may only be acquired in the course of the game for special purposes" (CB p.14 / PDF 9). The II transfer text says "divine spells (#55 and above)" (CB p.27). | **New text** about divine spells. The two numbers conflict (#58 vs #55). No new spells are listed. |
| **Items and equipment** | Shields 1–20, armor 21–40, weapons 41–100, potions 101–120, STR/DEX requirements (P1 pp.17–19 / PDF 21–23) | Same tables (CB pp.15–16 / PDF 10) | No change stated |
| **Quest items** | 20 scrolls; "RINGS AND WAND" (P1 p.19 / PDF 23) | "approximately 15" scrolls in Ferronrah. "In PHANTASIE I, 9 rings and a wand played an important role. In PHANTASIE II, amulets and an enchanted orb are the important artifacts." (CB p.27 / PDF 16; RM PDF 2) | **Changed** |
| **Inventory caps** | 9 items per character, 90 found items per party (P1 p.7 / PDF 12; p.10 / PDF 15) | Same (CB p.8 / PDF 6; p.10 / PDF 7) | No change stated |
| **Towns, services, prices** | Armory, Guild, Mystic, Inn (free in town; 250 GP + tax outside), Bank; random creatures pay more for training (P1 pp.3–6 / PDF 8–11) | Same (CB pp.4–8 / PDF 4–6) | No change stated |
| **Overland travel and terrain** | Path, Desert, Grassland/Forest, Mountains/Hills, River/Lake (swim or bridge, may cause damage); more insects and undead at night; map in 16 sections (P1 p.8 / PDF 13; terrain key PDF 25) | Same base terrain (CB p.9 / PDF 7; p.18 / PDF 11). **New terrain:** **Molten Lava**: "orange wave pattern … almost always causes damage … No encounters." **Mist**: "blue dots … making travelling difficult. Undead and insect encounters abound." **Dark Voids**: "dangerous to travel through … undead and insects flourish" (CB p.27 / PDF 16; RM PDF 2). | **Added** (3 terrain types) |
| **Dungeons** | Disk flip; Look (L); auto Listen, Pick Lock, Find Item and Spot Trap; Disarm; exit through the entry door with a save prompt; restocking (P1 pp.8–9 / PDF 13–14) | Same (CB p.9 / PDF 7; p.19 / PDF 12) | No change stated |
| **Monsters** | 80 types; examples given (P1 p.13 / PDF 17) | The same "80 different types" text, still naming Gelnor (CB p.11 / PDF 8) | No change stated. II's bestiary is not documented. |
| **Death, resurrection, aging** | Death at 0 HP; revived by "a high-level **priest** using a RESURRECT spell" (P1 p.13 / PDF 17); Resurrection #44 lowers CON by 1–2 (P1 p.15 / PDF 19); lifespans (P1 p.6 / PDF 11) | Same, except the Mortality text says "a high-level **wizard** using a RESURRECT spell" (CB p.10 / PDF 7). The spell tables still give #44 to priests and rangers only (CB p.14 / PDF 9). | **Wording changed**: priest → wizard. Probably a misprint (inferred). |
| **Quest and endgame** | Black Knights and Nikademus; scrolls, rings, wand (P1 p.1 / PDF 6; p.19 / PDF 23) | Free Ferronrah from Nikademus (CB p.2 / PDF 3); amulets and an enchanted orb (CB p.27). The wand of Nikademus survives a transfer (CB p.27). No endgame is described. | Changed (story only) |
| **Interface and commands** | Apple keys; C64 menus with joystick (P1 p.3–8; P1 PDF 2–3) | Same keys. Apple initial menu adds "3 Demo (in PHANTASIE II only)" (CB p.17 / PDF 11). Toss Rock is option 7 (CB p.27). Mac section: "all of the spells are referred to by name, not by number" (CB p.26 / PDF 15). | **Added**: Demo and Toss Rock. The Mac note belongs to the combined book, not to II specifically. |
| **Save and disk handling** | Save in town by Guild number; on leaving a dungeon, prompt to save its state; utilities on the back side, keys 1–5 (P1 p.7 / PDF 12; p.9 / PDF 14; p.19 / PDF 23) | Same, plus: "When you leave a town, your characters are automatically saved onto the game disk" (Apple, CB p.19 / PDF 12). Mac has "Auto Save" (CB p.26). "The PHANTASIE II disk is not copy protected" (CB p.19; RM PDF 2), and "Both PHANTASIE I and PHANTASIE II are not copy protected" (C64, CB p.24 / PDF 14). Adds a P1→P2 transfer utility (see §2). | **Added**: transfer utility and the copy-protection note. Autosave is first stated here; P1 is silent. |
| **Caps and limits** | 37 Guild members on Apple, 21 on C64 (P1 p.4 / PDF 9; P1 PDF 2); party of 6; 65,000 GP carried and 65,000 per bank account (P1 p.5 / PDF 10); levels in tables go to 15 | Same values (CB p.5 / PDF 5; C64 21 on CB p.20 / PDF 12; 65,000 on CB p.6 / PDF 5) | No change stated |

### Differences between CB and ALT
The rules text is the same in both printings, including the Updated Rules page and the transfer text. The only real wording differences are in the computer-specific pages (p.24 / PDF 14 in both):

- **Atari 8-bit section**
  - CB: "Atari 400, 800, 1200, and XL"; boot the front side, with 800XL owners holding OPTION; "see the Commodore rules".
  - ALT: "Atari 8-Bit computers"; "insert disk #1 … (XL owners will need to hold down the option key.) You will be prompted to put in disk #3 and to press any key except 'u'"; ESC returns from a town sub-menu; "Those owners who have XL's or XE's may use the Option key to speed up combat"; advice to copy your disks.
- **Atari ST section**
  - CB: "Phantasie Game Disk" / "Phantasie Data Disk", then "Place the arrow in the 'Good Luck' box and click".
  - ALT: "'Game Disk'" / "'Data Disk'", then "Press any key to continue", plus a note to copy the data disks.
- **Menu selection**: ALT says "Press the RETURN key to select"; CB says "RETURN key or SPACE BAR".

Everything else is layout, or OCR noise from screenshots.

---

## 2. Character transfer (Phantasie I → II)

The manuals give two versions of the procedure.

**Version A** appears in the Apple section (CB p.19 / PDF 12), the C64 section (CB p.24 / PDF 14) and the readme (RM PDF 2). Verbatim from RM:

> "Those of you who have played Phantasie I and still have your characters saved may use them in Phantasie II by booting the dungeon side of the Phantasie II disk and selecting Option 6) Transfer characters from Phantasie I to Phantasie II. *Warning: This process will have the following adverse effects:* Your characters must pay all items except the wand of Nikademus. They must also pay the majority of their money and will lose most of the experience they have accumulated and all spells except for any divine spells (#55 and above). Wizards will keep spell #5 and Priests spell #1. If you decide to use your old characters in Phantasie II then "transfer characters" should be the first thing you do. At this point, you may wish to train the characters at the guild as they will be at level 0 but have enough experience to improve. Any characters you have created beforehand on the Phantasie II disk will be erased by the transfer process."

**Version B** is on the Updated Rules page (CB p.27 / PDF 16; SCAN PDF 30). The body text is the same, but the opening says:

> "…may use them in PHANTASIE II by selecting the Utilities function of the game and choosing "Transfer characters from Phantasie I to Phantasie II." Warning: This process will have the following adverse effects: …"

"pay all items" and "pay the majority of their money" are what the page says. I checked the scanned image (RM PDF 2; SCAN PDF 30), so this is not an OCR error.

| Aspect | What the manual says |
|---|---|
| Procedure | Boot the **dungeon side** of the II disk and choose **Option 6** (Apple, C64, readme), or use the "Utilities function" (Updated Rules). Do it "first". |
| Disks and files | Not specified beyond the II dungeon side. Nothing says how the P1 disk or backup disk is read. |
| Items | All lost ("pay") **except the wand of Nikademus**. |
| Gold | "the majority" lost. The amount kept is not given. |
| Experience | "most" lost. The character lands at **level 0** "but [with] enough experience to improve" (train at the Guild). |
| Spells | All lost, except divine spells (#55 and above). **Wizards keep #5** (Fireflash 1) and **Priests keep #1** (Healing 1). |
| Existing II characters | **All erased** by the transfer. |
| Stats, HP, MP, age, race, class, luck, skills, status, bank gold, Guild numbers, elementals | **Not mentioned.** |
| Runes | Not mentioned anywhere. P1 has no runes either. |
| Limits | None stated: no count, no restriction on dead characters, no rule about repeating the transfer. |

---

## 3. Open questions the manuals leave unclear (not guessed)

The list stays as the manuals leave it. The Atari ST code answers Q1–15: `03b-phantasie2-re.md` "Manual questions
answered" (conflicts in `DIFFERENCES-II.md`). Only Q16 (the Apple "Demo" option; the ST has none) is unanswered.

1. Does "pay" mean the items and gold are removed outright, or exchanged for something? Exactly how much gold is kept?
2. How much experience is kept? What does "level 0 but enough experience to improve" mean in numbers, and how many levels can be retrained?
3. Which attributes carry over: STR/INT/DEX/CON/CHA, luck, age, HP, MP, race, class, skills, health status (dead, stoned and so on), bank balances, names and Guild numbers?
4. Divine spells: the general rules say **#58+** (CB p.14); the transfer text says **#55 and above** (CB p.27; RM). Which is right, and what are spells 55–57?
5. Wizards keep #5 and Priests keep #1. Do Rangers (who also learn #1 at level 1) and Monks (who learn #5 at level 1) keep anything?
6. Transfer entry point: "Option 6" on the dungeon side, or a "Utilities function"? The utility menus listed for Apple and C64 only show options 1–5 (CB p.19; p.24), and the Mac/ST utility lists have no transfer entry (CB pp.25–26). Where is the P1 save data read from (P1 game disk or backup disk)? Can you transfer one character at a time?
7. Does the erase wipe every II character and the dungeon state? Can the transfer be run more than once?
8. What does the wand of Nikademus do in II?
9. Toss Rock: what is its key, given that option 7 is still listed as Redo on Apple (CB p.19)? What damage does a rock do, does it need STR/DEX, what is the skill's starting value, how does it grow, which classes or races are good at it, and does it show on the Inspect screen?
10. Lava damage amount and frequency; Mist's movement cost; what makes Dark Voids "dangerous". **Answered**
    (`reverse-engineering/atari-st/phantasie2/logic-specs/terrain.md` section 7): lava 1 + ⌊r·T/10⌋ to every living member on
    every step (up to 11/21/31/51), digit-0 cells harmless; mist and voids cost 60 a step (the cheapest) and share one
    ≈ 9 % once-per-trip undead/insect fight, no damage (D2-008, NOT A CONFLICT).
11. II's towns: how many, their names, the starting town, map size or sections, and whether the C64 21-character Guild cap still applies. The shared text still says Pelnar and 11 towns of Gelnor (CB p.4). ST says "Start this turn in Pelnar" (CB p.24). **Answered (ST):** `03b-phantasie2-re.md` Q11, `reverse-engineering/atari-st/phantasie2/logic-specs/towns.md`.
12. Exact scroll count ("approximately 15"); how many amulets; the orb's role; II's win condition.
13. II's monster roster; the shared text is unchanged from I (80 types, Gelnor).
14. Is "high-level **wizard** using a RESURRECT spell" (CB p.10) meant for II, or is it a misprint? I says priest, and the spell tables do not give #44 to wizards.
15. Does autosave on leaving town (CB p.19) apply to I, II or both? P1 does not state it.
16. What does the Apple "Demo" option do (CB p.17)?

---

## 4. Rock throwing and body-part injuries

- **Throwing rocks is a Phantasie II feature.** It is the "7) Toss Rock" combat option: any rank, unlimited rocks, its own Toss Rock skill (CB p.27 / PDF 16; RM PDF 2; SCAN PDF 30). The Phantasie I manual (P1) has nothing about it.
- **Body-part injuries are not in the Phantasie I or II manuals.** CB and RM mention only hit points. They appear in **Phantasie III**: "Body Parts: the state of the adventurer's Head, Torso, Arms, and Legs is listed. Each area can be Okay, Injured, Broken, or Gone" (P3 `phantasie3-manual.pdf` PDF 7). Further rules there cover injured areas, a broken arm or leg preventing bow use, and losing the Head or Torso (P3 PDF 9). The Australian P3 printing has the same text (`phantasie3-manual-australia.pdf` PDF 7, 9).
