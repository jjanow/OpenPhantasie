# Phantasie IV (X68000) — function map (3d.4)

`P4.X` keeps its full symbol table (705 text symbols), so every function already has its C name; this file records
what the names **do**. All 705 text symbols are listed (section 1 the first readings, section 2 the rest, section 3 the XC
runtime). Gameplay functions were read; UI / graphics plumbing is grouped and partly named from its callees (`[~]`).
Addresses are P4.X image addresses (file offset − 0x40). Full list of names: `extracted/p4x-symbols.json`. Tags as in `FORMAT.md`.

**How to reproduce the decompile:** `tools/scripts/x68k_p4_ghidra_prep.py OUTDIR`, then Ghidra headless with
`tools/scripts/GhidraP4Setup.java` (command in the prep script's docstring; one Ghidra at a time, `MAXMEM=4G` under
`flock /tmp/claude-1000/ghidra.lock`). Integer FPACK calls are rewritten to 68020 multiply/divide so array strides show;
float code stays as `trap(0xe)` and is read in the disassembly (operand order: `FORMAT.md` section 6).

## 1. Game functions read (s79)

### Save / load / disk

| Addr | Name | Role |
|---|---|---|
| `0x126a` | `_sysinit` | screen init, drive check, loads `tabl` → `_dtbl` (`_tbl` = pointer), `icon.pix` |
| `0x12fa` | `_load(slot)` | slot 0: new game (Hall = `fame`, defaults); 1–3: read the six save files; rebuild `_anc` from the records |
| `0x1714` | `_save(slot)` | copy `_anc` into records, write the six files to drive 1 |
| `0x1ab4` | `_diskmenu` | New Game / `*1:Save`–`*3:Save` icons; `*` = `fames.N` exists |
| `0x31748` | `_setdir(drive)` | `A:\` / `B:\`, or the hard-disk path when `_drivetype` = 0 |
| `0x317d2` / `0x318f8` / `0x31924` | `_xreads` / `_xread` / `_xwrite` | whole-file read (length = file length) / write, error → `_xaccer` |
| `0x319ec` | `_xexfile` | file exists on drive 1 |
| `0x31a5c`… | `_xrndint` / `_xrndread` / `_xrndend` | fixed-size record file access (`datm` 70 B, `elms` 190 B, `p4mon.pix`, `mout` 700 B) |
| `0x31466` | `_dready` | "No disk is inserted" retry / end loop (IOCS `DRVCTRL`) |

### Character creation (spec: `logic-specs/ancestry.md`)

| Addr | Name | Role |
|---|---|---|
| `0x1198c` | `_make` | creation loop: free slot (`_mkpos`), "descendant of a mixed race?" Y → `_make1`, N → `_make2` |
| `0x11c28` | `_make1` | ancestor 1: N → `_mkcha(0x23)`, H → `_make3`; then `_push_work(0)`, `_make5` |
| `0x11a6e` | `_make3` | pick a Hall person as ancestor 1 (`_fame2buf(i, 0)`) |
| `0x136c6` | `_make2` | plain character: `_mkcha(0x25)`, name, `_mkdetail` |
| `0x13db0` | `_make5` | ancestor 2 S/D/H → descendant race, class, `_cmbattr`, confirm, `_set_anc_social`, name, `_mkdetail` |
| `0x13a7a` | `_mopta(threshold, wanted)` | S/D race roll, "Sorry, the ancestor you found was …", new ancestor 2 |
| `0x13978` | `_moptb` | H: Hall person as ancestor 2; "Not possible" rules |
| `0x138c0` | `_err_fame` | "Not possible" |
| `0xb79a` | `_combi(a, b)` | mixed-race result (5×5 table + nine packed pairs) |
| `0x12a0c` | `_mkcha(win)` | race menu → (`_rrace`) → `_set2work(…, 0)` → confirm |
| `0x12838` / `0x12704` / `0x125c8` | `_racemenu` / `_rrace` / `_rracemenu` | 5 races + Random Creature + Back; random lists |
| `0x1238a` / `0x1252a` | `_classmenu` / `_rclass` | class menu filtered by the race's class mask |
| `0x1256c` | `_set2work(win, race, inherit)` | clear work buffer, race, class, then `_newattr` or `_cmbattr` |
| `0x122e0` | `_newattr` | five abilities with exponent 0.6 + move base |
| `0x121f2` | `_cmbattr` | inherited abilities (`_sub_bonus`, `_avg_attr`, `_get_modifer`, `_mdice`) |
| `0xb99a` | `_sub_bonus(n)` | remove race + class modifiers from ancestor n's abilities |
| `0xba90` | `_avg_attr(i)` | (anc1 + anc2) / 2.0 |
| `0xbb2c` | `_get_modifer(avg)` | exponent table lookup (18.0 down by 0.5) |
| `0xb5d8` | `_mdice(race, class, ability, k)` | 3 × 6·u^k + 3, class mod, cap 18, race mod, floor 3 |
| `0xb754` | `_moverate(race, class)` | move base = race table + class table |
| `0x6b7c` | `_get_social(race)` | random social class 0–3 |
| `0x13744` | `_set_anc_social` | store "new" ancestors into `_anc` with their own social class |
| `0x12b1e` | `_mkdetail` | build the 190-byte record (social, Hall effects, Divine Aid, origin, spells, Luck, age, HP/MP) |
| `0x13624` | `_mkname` | name entry (8 characters) |
| `0x12a90` | `_chkwon(hall)` | Hall person completed I, II or III (any result ≥ 100) |
| `0xb8ea` / `0xb942` / `0x6862` | `_push_work` / `_pop_work` / `_clrwork` | copy work buffer ↔ `_tmp[n]` (22 B) |
| `0x6896` | `_fame2buf(hall, n)` | Hall person → work buffer, ancestor ref n = (1, hall) |
| `0x638c` | `_set_fame_lst` | Hall list for picking |
| `0x6b2c` | `_mkpos` | first free guild slot or −1 |

### Level-up, items, monsters

| Addr | Name | Role |
|---|---|---|
| `0xc320` | `_calpow(rec, level)` | level-up (returns at once for level 30): gold += social^social × level × 30 (cap 65 535), skill bonuses, skills, HP (TABL 0x000), magic level (TABL 0x14A), learnable spells (TABL 0x6EA), level |
| `0xbbb0` / `0xbdb8` | `_calsk` / `_calbon` | skill value from TABL 0x1C2 / ability bonus (0 below 10, else 2 × value − 20) |
| `0x3c30` | `_trang` | guild training: experience needed = 1350 × (1.8^L − 0.8) / class-ability ratio; cost = L^2.2 × (20 − Cha)^2 × `_ctab`[map] / 100, cap 25 000, from party gold; then `_calpow` (`logic-specs/training.md`) `[C]` |
| `0x34b0` | `_mage` | Magic Training Hall: quota L × Int^1.177 / 90 × (class + 1, Fighter −2); price trunc(9000 × `_ctab` / 100) / Cha / Int × L / (9 − class); age +2/26 yr per try; success rand(100) < 5 × Int (`logic-specs/training.md` section 5) `[C]` |
| `0x97be` | `_cal_user(ch, item)` | item usable by Str/Dex requirement |
| `0x23e54` / `0x23c06` | `_baserate` / `_movlost` | combat movement incl. armour % and Shoes of Speed |
| `0xa236` | `_sell` | sell: price / 4 + 1, shop stock +1 |
| `0xdcfa` / `0xddd4` | `_igen` / `_igen8` | random shop item / restock 8 |
| `0x20d54` | `_enc_mons` | load encounter groups from `datm`, counts, HP, gold, XP, follow-up groups |
| `0x845c` | `_page1(rec)` | character sheet page 1 (field names for section 6.2) |
| `0x8344` | `_dsp_uni` | "<name> a <social> <race> <class>" ancestor line |

### Towns (spec: `logic-specs/towns.md`)

| Addr | Name | Role |
|---|---|---|
| `0x321c` | `_town` | town loop: building doors by town type (Guild, Mystic, Armory, Bank, Inn, Hall / Shipwright), menu F/C/T/D/U/S |
| `0x2cf2` | `_townent` | town entry: `_gatebank`, age += time counter / 0x700, decline at 70 % of lifespan, death of old age |
| `0x4ef0` | `_gatebank` | "Share out experience": party XP pool (`_par`+18, ×10) by shares into record +04 |
| `0x2c8e` / `0x2988` | `_welcome` / `_tload` | forced item distribution if the pack is not empty / load town, set +1B, restock |
| `0x5ae8` | `_exittown` | leave: re-equip, place party at `_townexit` |
| `0x2188` | `_inn` | free rest: MP-growth roll, HP/MP refill, `_heal(·, 2)`, ring recharge |
| `0x1ffa` / `0x1d3c` / `0x316` | `_heal` / `_setpc` / `_healch` | wound healing on the packed body-part levels (+0C) / combat condition / status window |
| `0x57a4` / `0x4992` / `0x5414` | `_bank` / `_deposit` / `_withdraw` | bank: account = record +26; deposit by shares (cap 65 000 each), withdraw (party cap 65 000) |
| `0xe552` / `0xde82` / `0xe2da` | `_armory` / `_itemlist` / `_buyitem` | shop: list (6 per page), buy at trunc(price × `_ctab`/100) |
| `0xa2ba` / `0x1079a` | `_distribute` / `_trade` | item distribution (sell "S") / item exchange (also sells) |
| `0x6bf0` | `_mystic` | party score and verdict; stores the average level in `_par`+0B |
| `0x5e4e` | `_shipyard` | ship 1500 GP, replaces the old one |
| `0x47f8` / `0x46fc` | `_guild` / `_guildmenu` | roster (`_mlist`), training (`_trang`), magic (`_mage`) |
| `0x14f98` / `0x14cac` / `0x14dc2` | `_oinn` / `_oboatman` / `_opool` | outdoor inn 256 GP + `_dayout(1000)` / Charon / teleport pool |
| `0x13efe` | `_dayout(n)` | time: `__daytime` = (n + `__daytime`) mod 24, counter `_par`+16 += quotient (cap 65 535) |
| `0x248ac` | `_esub01` | combat end: XP pool += XP / 10, party gold += loot (both cap 999 999 999) |

### Hall of Glory and III transfer

| Addr | Name | Role |
|---|---|---|
| `0xce70` | `_dsp_hinfo(slot)` | Hall person screen: fields, per-game results, deeds, status (and Lord Wood's status rule) |
| `0xd7f6` / `0xd97a` | `_hprint` / `_hhist` | the scrolling history text (`_hstr`) |
| `0xccfe` | `_htran` | "transfer III's data?" → `_trans` |
| `0xcb1a` | `_trans` | swap-disk prompts, `_p3disk`, `_mtrans` |
| `0xcab4` | `_p3disk` | III player disk check: sector 0x120 has "Pendragon" at byte 230 |
| `0x11680` | `_mtrans` | read 21 III records (15 sectors from 0x20), choose, copy into free Hall slots |
| `0x1141e` | `_mcopy(rec, slot)` | III record → Hall record (field map `FORMAT.md` section 6.3) |
| `0x112c6` | `_cntdown` | "N more can be transferred" / "No more can be transferred." |
| `0x121ba` / `0x12012` / `0x11f68` | `_rdiskv` / `_rwdisk` / `_rfd256` | raw 256-byte-sector floppy read via IOCS `B_READ` |

### Random numbers

| Addr | Name | Role |
|---|---|---|
| `0x35d0c` | `_rand(n)` | 16 steps of a shift register (`rreg = rreg << 1 | bit30 ^ bit26`), returns `rreg % n + 1` with an **unsigned** remainder (`$FEE5` `__CUMOD`; rreg = 0 is treated as 1), so 1..n; n = 0 → 1 `[C]` |
| `0x35c6a` | `_rinit` | seeds by stepping the register (time-of-day & 0xFF) times |
| `0x30996` | `_frand` | rand(100) / 100.0 |
| `0x30948` | `_mexp(x, y)` | x^y = exp(ln x · y) (`_ln`, `_expe`) |

## 2. Functions read in s82 (combat, dungeon events, skills, outdoor, items, endings, pictures, UI)

Specs: `logic-specs/combat.md`, `events.md`, `skills.md`, `outdoor.md`, `items.md`, `ending.md`; pictures `FORMAT.md` section 6.9.
Rows whose role comes only from the name and the callees are marked `[~]` in the last table.

### Combat (`logic-specs/combat.md`)

| Addr | Name | Role |
|---|---|---|
| `0x9024` | `_equip` | best usable shield / armour / weapon / bow / thrown → values +B6 / +B7 / +6B / +6A / +69, ids +64–68 |
| `0x20634` | `_set1` | per-member combat setup: Wm, Hm, Wr, Hr, armour A (+6F), base MV, defence D (+35) |
| `0x2810c` | `_chcond` | attack possible / impossible / off-hand, by `_pc` and arm wounds |
| `0x28266` | `_chdtyp` | melee hit type 1 or 5 (monster +43, flier vs walker) |
| `0x27e18` | `_chatkp` | party hit chance |
| `0x27920` | `_chatkd` | party damage (melee, bare hand, bow / thrown) |
| `0x2788a` | `_chatkf` | melee damage offset by action (Thrust +1, Attack −1, Slash −n) |
| `0x277fa` / `0x27854` | `_chlostp` / `_chlostd` | hit × 3/4 for Down, ÷ 2 off-hand / damage × 2/3 off-hand |
| `0x2771a` | `_critical` | critical chance by item id |
| `0x28322` | `_chrexe` | one member's action in a phase |
| `0x28ff8` / `0x262be` | `_enc_patk` / `_enc_matk` | party / monster phase loops (surprise flags) |
| `0x251d4` | `_monexe` | monster turn: wake, initiative, spell / special, melee, parry |
| `0x21adc` | `_mdamage` | damage to a monster (Weakness %, min 1, wound, death, XP) |
| `0x1e940` | `_pdamage` | damage to a member: in combat with hit location and wounds (min 1, death, game over); outside combat HP only |
| `0x1e8d6` | `_setdead` | HP 0, condition 0 |
| `0x1e728` / `0x218ba` | `_plget` / `_mlget` | hit location for a member / a monster |
| `0x1e4b0` / `0x21694` | `_getpd` / `_getmd` | wound level from damage for a member / a monster |
| `0x21452` | `_setmc` | monster combat condition `_mc` |
| `0x22978` | `_is_alive` | combatant still on the field |
| `0x28088` / `0x28020` | `_target` / `_mdatptr` | combatant on a square / its monster type and slot |
| `0x297d6` | `_f2f` | face-to-face animation (shot, slash, flash); repeats no strike |
| `0x1d5ec` | `_opt_set` | which manual-mode actions are offered |
| `0x1d9a2` | `_opt_get` | store the chosen action: parry roll, Slash n, target |
| `0x1dd72` | `_b_chmove` | manual-mode per-member setup (move, action, spell target) |
| `0x2324a` / `0x230cc` / `0x229ee` | `_b_chauto` / `_dii` / `_b_mmove` | step movement / diagonal-cost toggle / move one square |
| `0x23f86` / `0x23c06` / `0x23e54` | `_calmove` / `_movlost` / `_baserate` | member MV per round / wing-leg loss / base stride with armour and Shoes of Speed |
| `0x2605c` / `0x25d30` | `_mcalmove` / `_mmovlost` | monster MV / monster wing-leg loss |
| `0x1ce02` | `_enc_opt` | round options: Greet / Beg / Demand surrender / Fight / Flee |
| `0x1cc36` / `0x1c8d2` | `_partyact` / `_partymenu` | party-strategy choice / options menu |
| `0x1c5f6` | `_eoptreset` | reset a member's planned action |
| `0x2743e` / `0x270b6` | `_b_chkflee` / `_b_chkedge` | HP-based automatic flight / leaving from an edge square |
| `0x24ae2` / `0x248ac` | `_enc_end` / `_esub01` | end of fight by cause / XP and gold payout |
| `0x2b6f8` / `0x2b86e` | `_getmp` / `_splist` | MP cost table (`0x44B7C`) / castable-spell list (MP ≥ cost) |
| `0x2bcc4` / `0x2a5a0` | `_pmagic1` / `_pmagic2` | party combat spells 1–37 / 38–62 |
| `0x29a08` | `_mmagic` | monster spells and breath 66 / 67 |
| `0x1efe2` / `0x1edf8` | `_try_cast` / `_psetcast` | auto-mode spell choice / cast if MP stays above max / 2 or / 3 |
| `0x1fa34` / `0x1fd42` | `_protect` / `_atklist` | auto-mode guard target `[~]` / most-attacked target |
| `0x823a` | `_page2` | profile page 2: strategy A–E = record +2A–+2E |

### Dungeon and event bytecode (`logic-specs/events.md`)

| Addr | Name | Role |
|---|---|---|
| `0x1a1f2` | `_event(x, y, dir)` | runs the first matching trigger entry of `_evbuf`, then `_rndenc` |
| `0x195b0` | `_exe_ev` | event bytecode interpreter, opcodes 0x00–0x3A (`logic-specs/events.md` section 4) |
| `0x18474` / `0x184c4` | `_evadr` / `_evval` | little-endian u16 operand: base + offset / plain value |
| `0x1850e` | `_evj(op)` | jumps 1–6 (always, true, false, cmp > 0, cmp = 0, cmp < 0) and 7 (call) |
| `0x185d6` | `_eprint(text)` | message box print, `$` = new line, 18 full-width characters per line |
| `0x187bc` | `_ewait` | "Space" prompt; stops Examine |
| `0x188ba` / `0x189c6` / `0x192a4` | `_hasitm` / `_setitm` / `_remitm` | item test / give (full pack → overwrite slot rand(127)) / remove, over party pack + carried slots |
| `0x18b1a` | `_setgp(n)` | party gold += n (bug: above 990 000, += 990 000) |
| `0x18be2` / `0x1a322` | `_evdam(n)` / `_ddamage(m, d)` | rand(n) damage to every member / HP −= d, death, wipe-out check |
| `0x18c28` / `0x193e6` / `0x19524` | `_setmove` / `_setyoko` / `_setfl` | set a side's movement code / north-wall picture / floor type |
| `0x18c9e` | `_openg(x, y)` | animate a gate picture 16 → 19 |
| `0x18d72` | `_tele(f, n, x, y)` | teleport to town / outdoors / dungeon; town number typed in when f = n = 0 |
| `0x18e46` | `_xmenu` | numbered event menu |
| `0x18fe4` | `_listen` | Listen roll; message only |
| `0x1911a` / `0x19240` | `_denc(m)` / `_rndenc` | event fight / random dungeon encounter (rate %, or out of 400 during Examine) |
| `0x19386` | `_raceis(r)` | race present in party |
| `0x19480` | `_refle` | HP/MP to max + status refresh |
| `0x1a93a` / `0x1a7ea` | `_dload` / `_dreset` | load `IND<n>.PDT` + `O_0nn`, `_dpixset`, run the map-load trigger / rotating per-map flag reset |
| `0x1a440` / `0x16abe` | `_mkdin` / `_prex` | build the movement map from `_pinmap` / set its bits 14–15 |
| `0x1aac2` | `_dmove(dir)` | dungeon step, locked doors, edge = 7 |
| `0x17c56` / `0x1ad22` | `_dmain` / `_dungeon` | dungeon loop (event + encounter roll every action) / entry |
| `0x1b33e` / `0x1ad50` / `0x1b1fc` | `_dchest(k)` / `_ctrap` / `_cspi` | chest plain / locked / trapped / hidden item |
| `0x1b604` | `_dtrap` | floor trap: Spot Trap find, Disarm, sprung damage rand(5) × (map / 5 + 1), dodge on record +35 |
| `0x1bb14` / `0x1bac2` | `_try_ul` / `_plock` | pick a locked door (the side becomes code 3 either way) / "won't open" |
| `0x1ac54` | `_dswim` | dungeon water: rand(L + 1) + 1 damage on a failed Swim roll |
| `0x180ca` / `0x18110` | `_seainit` / `_seasel` | Examine = 63-step automatic walk to the least-visited square (clearing loop bugged) |
| `0x14e8c` / `0x1509e` | `_odungeon` / `_otown` | enter a dungeon / town from the outdoor map |
| `0x20cdc` | `_monevf` | story flag 12 / 9 / 59 for beating encounter 72 / 73 / 83 (Castlebaum / Kor / Arathamus) |

### Skills, outdoor travel, ships, treasure (`logic-specs/skills.md`, `outdoor.md`)

| Addr | Name | Role |
|---|---|---|
| `0xc0ee` | `_sklup` | 3 skill picks per level-up, the same skill may be repeated |
| `0x630a` | `_bvs(key)` | key → button index (−1 if none) |
| `0x15a92` | `_omain` | outdoor step: hours and encounter ‰ by terrain, tile handlers, forced drift (`logic-specs/outdoor.md`) |
| `0x14864` | `_omove` | 8-direction move, map-edge transitions (5-wide / 3-wide grid) |
| `0x14096` | `_ochkedge(type)` | rapids / River of Death (needs Charon's Raft and Staff) / open sea / barrier |
| `0x13fde` | `_obarrier` | shining sea: ship destroyed or party pushed back |
| `0x13f7e` / `0x1435c` | `_oblkofs` / `_getxy` | overview block, screen position |
| `0x1514e` | `_owater` | swim damage, or navigation drift and storms |
| `0x152e8` | `_olava` | "It's hot!": rand(18) ≤ Luck → 1–5 damage |
| `0x14478` | `_oship_mark` | board ship, navigator = best Navigation; ship marker |
| `0x14bf4` | `_save_ship` | disembark, or "ran aground" (ship lost) |
| `0x157f6` / `0x15fbe` | `_omap_load` / `_oload` | `MOUT` record (map → record table `0x42DD2`) / outdoor map entry |
| `0x1668c` | `_oencchk` | outdoor encounter: night attack 37 %, sleeping monsters, Listen / surprise |
| `0x165d6` | `_oencgo` | run the fight; afterwards a ship is moored if fought at sea |
| `0x26e0c` / `0x26862` | `_e_terrain` / `_e_on_ship` | battlefield by terrain; at sea the enemy becomes Swashbuckler 64 % / Pirate 36 % |
| `0x24774` / `0x24664` | `_esub02` / `_findit` | post-fight treasure (45 % in dungeons, `_emlevel` + 18 % outdoors) / offer an item for the pack |
| `0x153a2` | `_ocast` | cast outside combat (outdoors 5, dungeon 6, town 7) |

### Pictures (`FORMAT.md` section 6.9)

| Addr | Name | Role |
|---|---|---|
| `0x328e4` | `_xunpack(drive, x, y, file, keep, …)` | read a PKH, unpack it into a picture slot, show it at x, y (`_scst_displays`), release the slot unless kept |
| `0x32a08` | `_xunpack68` | the same without the `_cvsx` / `_cvsy` coordinate conversion |
| `0x322d8` | `_scst_stacf(buf, len)` | new picture slot from a PKH buffer: palette, size, store address, `_scsvb1` |
| `0x31fac` | `_scst_stacs(x, y, w, h)` | new picture slot that saves a screen rectangle (palette from `_GPALET`, `_scsvs1`) |
| `0x325d4` / `0x326b6` / `0x32760` | `_scst_display` / `_scst_displays` / `_scst_displays2` | set the slot palette, copy the slot to the screen (`_scdisp1` / `_scdisp2` / `_scdisd2`) |
| `0x3280a` / `0x32846` / `0x32896` | `_scst_clr` / `_scst_all_clr` / `_scst_ck_f` | free the top slot / free all slots / find a slot by id |
| `0x3a8b0` | `_scsvb1(dst, src, len)` | PKH run-length unpacker (`FORMAT.md` section 6.9) |
| `0x3a720` | `_scsvs1` | GVRAM rectangle → store |
| `0x3a770` / `0x3a7c0` / `0x3a82a` | `_scdisp1` / `_scdisp2` / `_scdisd2` | store → GVRAM rectangle (`_scth` scroll offset) |
| `0x31f6c` / `0x31f2c` | `_cv_pword` / `_cv_pxword` | read an unaligned BE word / longword `[~]` |
| `0x34e3e` | `_pal_set_all(pal)` | 16 × `_GPALET` |
| `0x34c78` | `_bapal(rgb[16][3])` | build GRB palette words from RGB triples and set them |
| `0x34b3e` | `_screen` | CRT mode 16 (768 × 512 × 16), pages, text palette, slots cleared, mouse init |
| `0x1a854` | `_dpixset` | load `fl` / `yoko` / `tate` / `mn.pix`, build masks (`_ConvData`) |
| `0x34ab8` | `_ConvData` | build the AND masks for sprite data from `_pix_and_table` |
| `0xc96a` | `_hpix(mode, slot)` | Hall person sprite: draw (`_chpix` / `_mopix`), or load its `p4mon.pix` record (index + 0x54) |
| `0x3b2a6` / `0x3b198` | `_chpix` / `_mopix(x, y, frame, flip)` | masked 48 × 48 sprite blit; flip = 1 mirrors it |
| `0x3ab3e` | `_fpix` | 32 × 32 4-bit tile blit (`fl.pix`) |
| `0x3ae6a` / `0x3af6c` | `_tpix` / `_mpix` | masked tile blits (`tate` / `mn`) |
| `0x263d6` | `_bo1pix(square)` | draw a combat-field object (`_obpix`) or background (`_bkpix`) at square % 7, square / 7 |
| `0x290c8` | `_itmpix(n)` | map a sprite frame number by facing (`__xdir`) for n > 6 `[~]` |

### Program frame, system menu, sound

| Addr | Name | Role |
|---|---|---|
| `0x0` | `_openning` | title: music 99, "STARCRAFT Presents", `OPEN.PKH` + `OPENANI.PKH` animation, "By Winston Douglas Wood" |
| `0x130` / `0x1ac` | `_init` / `_main` | party window + buttons / program entry: `_sysinit`, `_vinit`, `_rinit`, `_xprtck`, title, `_disk`, then `_game` |
| `0x27e` | `_game` | main loop on `_cnewf`: 0 `_town`, 1 `_outdoor`, 2 `_dungeon`, until `__cflag` |
| `0xb5e` | `_about` | "About" box: version 1.02, credits |
| `0xd52` / `0xe52` | `_acend` / `_acgamend` | "End game" (back to title / exit) / game-select menu |
| `0xecc` / `0xf6e` / `0x10c6` | `_music(n)` / `_acswitch` / `_acsound` | play tune n unless music is off / music and sound-effect switches (`contr` 3–4) |
| `0x1c7e` | `_disk(mode)` | Save / Load front end (`_diskmenu`, `_save`, `_load`) |
| `0x11e6` | `_p4_screen` | screen, palette, mouse, windows init |
| `0x312c4` | `_xexit` | quit dialog → `_exit` |
| `0x3618a` / `0x362e0` | `_xprtck` / `_xchkpgm` | start-up disk check: IOCS `B_DRVCHK` on drive 0, then `B_READ` of 1024 B at cylinder 76, head 1, sector 1; fails (asks "Insert the original program disk in drive 0") when the result's low byte is 0x4D and repeats until it passes. Probably a copy-protection / original-disk check `[~]` |
| `0x31780` | `_xdrvck` | `_drivetype` = no `id2` file on the current drive (hard-disk install) |
| `0x3612a` | `_disk_ej` | eject check (`B_DRVCHK`) `[~]` |
| `0x35a68` / `0x35344` / `0x35306` | `_m_inn` / `_mus(n)` / `_xm_trk` | load `P4.SND` into the OPM driver / MML strings per tune → `_m_assign`, `_m_play` / track error print |
| `0x35a34` / `0x35a4e` | `_mus_stop` / `_mstop` | stop music |
| `0x35f68` / `0x35fbc` / `0x36048` | `_vinit` / `_voice(n)` / `_noise` | load `VDATA` (sound effects) / play effect n (`_a_play`) |
| `0x3605e`–`0x36110` | `_bsetr`, `_sdbeep`, `_sdnoi`, `_sdwlk`, `_sdatk`, `_sddam`, `_sdmag` | `_voice` 1, 2, 3, 4 / 5 by turns (`_wlkflg` `0x4A35C`), 6, 7, 8; `_noise` = `_voice(0)`; `_bsetr` and `_sdatk` have no caller (FORMAT.md 6.13, every call site) |
| `0x3b4fa`–`0x3b62e` | `_m_alloc`, `_m_assign`, `_m_vget`, `_m_vset`, `_m_tempo`, `_m_trk`, `_m_free`, `_m_play`, `_m_stat`, `_m_stop`, `_m_cont` | OPM music driver (`OPMDRV.X`) calls |

### Party, roster and character screens

| Addr | Name | Role |
|---|---|---|
| `0x8e2` / `0x914` / `0x982` / `0xa06` | `_healall` / `_chopen` / `_clschw` / `_opnchw` | redraw all status lines / open one member's status window / close / open the "Party" window |
| `0xaaa` | `_alldead` | 1 if no party member (slot 6 = elemental excluded) has a non-zero condition (+0A) |
| `0x9350` | `_lifeck` | number of members whose condition is not 0 (alive) |
| `0xb0a` | `_haveit(member, item)` | item id among the member's nine carried items (+52…+5A) |
| `0x12d0` | `_clrbuf(p, n)` | zero n bytes |
| `0x6ae2` | `_clrcha(slot)` | clear a 190-byte guild record |
| `0x7086` | `_clrparty` | close the party windows |
| `0x714c` / `0x73f0` / `0x7cb0` | `_set_mem_lst` / `_mlistsubset` / `_mlistmenu` | guild roster list ("#, registered name, race, current town") and selection |
| `0x748c` | `_dislodge` | delete characters from the guild (link byte +01 = 0) after confirmation |
| `0x7520` | `_re_name` | rename a guild member |
| `0x769c` | `_inspect` | "Whose details do you view?" → `_detail` |
| `0x780a` / `0x111d2` | `_marker` / `_mark2` | list highlight boxes |
| `0x8f8c` / `0x8e02` | `_detail` / `_detailmenu` | character sheet pages 1–3 (`_page1`, `_page2`, `_page3`) |
| `0x823a` / `0x7e12` / `0x8094` | `_page2` / `_ds1` / `_p_info3menu` | page 2 "Combat Strategy" A–E (`_ds1` draws one strategy line) |
| `0x11dfa` / `0x11ca2` | `_page3` / `_dsp_spell` | page 3 "Spells Known" |
| `0x655a` / `0x65d2` / `0x67ae` | `_dsp_race` / `_dsp_buf` / `_dsp_skill` | creation work-buffer display: race, name/class/abilities, skills |
| `0x1652e` / `0x16290` / `0x16124` | `_party_info` / `_p_info1menu` / `_p_item(kind)` | "Party Info": gold, experience pool, items found (pack items counted by kind `[~]`), party strategy |
| `0x93ba` / `0x94b0` | `_dvsort` / `_set_dx(0)` | item pool for Distribute: strip every member's carried items (0 also clears equipment slots) and the pack into `_dxi`/`_dxz` (id, charge), sorted by id, highest first |
| `0x9904` / `0x9c9a` / `0x9d5a` / `0x9f4e` | `_dvitmlst` / `_init_dl` / `_disp_eq` / `_init_eq` | Distribute screens: item list (Item / Points / User), equipment table (Wpn, Arm, Shd, Bow, Thr) |
| `0x103b6` / `0x10664` | `_tritmlst` / `_init_tl` | trade / use item list |
| `0x10ef8` | `_show_gm` | III transfer: III guild roster list |
| `0x11842` | `_pos_err_msg` | error message window |
| `0x6a4c` / `0x6a8c` | `_spc_wait` / `_confirm` | "SPACE" wait / "Okay" confirmation |
| `0x30e7c` / `0x309e4` | `_noyes` / `_numin` | YES/NO box / number entry |
| `0x31098` / `0x3110e` / `0x311ea` / `0x31246` | `_setwho` / `_getwho` / `_amark` / `_bmark` | member buttons, pick a member, highlight / flash a member's status box |

### Creation menus (logic in section 1)

| Addr | Name | Role |
|---|---|---|
| `0x118ca` / `0x11abc` / `0x119f8` / `0x13902` / `0x13cd6` | `_makemenu` / `_make1menu` / `_make3menu` / `_moptbmenu` / `_make5menu` | windows for creation, First / Second ancestor, Hall pick lists, "Create Descendant" |

### Items, scrolls, endings, Hall

| Addr | Name | Role |
|---|---|---|
| `0xfeb0` | `_use` | Use Item screen (`logic-specs/items.md` section 1) |
| `0xec4a` | `_spoile(member)` | 10 % "This potion has gone bad!", 1–8 damage |
| `0xec2e` | `_udam` | wrapper of `_ddamage` |
| `0xedc4` / `0xecd2` / `0xeef0` / `0xf8c6` | `_use_hp` / `_use_mp` / `_use_op` / `_use_mi` | healing / magic / special potions, magic items (`items.md` sections 3–5) |
| `0xfad0` / `0xfcb2` | `_use_sc` / `_use_box` | read a scroll (`scr` file, `SCBACK.PKH`) / open The Box (story) |
| `0xfdea` | `_setuse` | Use Item window |
| `0x2ddba` / `0x2da08` / `0x2dcf4` | `_ident` / `_ident_mi` / `_setident` | Item Appraisal: per item kind a description ("Hit points … will be restored somewhat", "It teaches a spell", …) |
| `0xb378` | `_ending` | chooses one of five endings from `_par`+158…15C (`logic-specs/ending.md`) |
| `0xb042` / `0xaece` / `0xade0` / `0xacdc` / `0xa8b2` / `0xa9e6` | `_endset` / `_common_proc` / `_banquet` / `_arathamus` / `_endmsg` / `_endstory` | ending: party reset and Score +09, "Lord" title, banquet and Arathamus scenes, message pages, final screen with score and completion code (`ending.md`) |
| `0xd6d2` / `0xd4b0` | `_hprof` / `_hprofmenu` | Hall of Glory person profile screen (`HALL_A/B.PKH`, `_dsp_hinfo`, `_hpix`) |
| `0xbf48` / `0xbdf2` | `_sklupmenu` / `_sklpr` | level-up skill raise screen "Training Menu — Skill Now → After" (`logic-specs/skills.md`) |

### Town and dungeon frames

| Addr | Name | Role |
|---|---|---|
| `0x25e2` / `0x26c2` / `0x275e` / `0x493a` | `_tcset` / `_tw_open` / `_townmenu` / `_disppcg` | town message window / town window / town command icons (F Profile … S System) / "Party gold N GP" |
| `0x611c` / `0x6154` / `0x61b4` / `0x61f4` / `0x6348` | `_clrret` / `_clrbtd` / `_clrtdraw` / `_mkicon` / `_bvs2` | button-table resets, clear the town picture, command-icon window, member-button lookup |
| `0x17970` / `0x17a10` / `0x17a30` / `0x17a50` | `_dcset` / `_dcast` / `_dprof` / `_dsetup` | dungeon message window / cast (`_ocast`) / profile / command icons |
| `0x16c16` / `0x16d10` / `0x16d44` | `_idcopy` / `_nw` / `_ww` | copy the 9 × 9 cells around the party (`_pibuf`) / cell fields |
| `0x16d7a` / `0x171ce` / `0x175bc` | `_calim` / `_idraw` / `_ianim` | build / draw / animate the location-map view from `_pibuf`: a 9 × 9 overhead view with standing walls (tiles `_fpix`, `_ypix`, `_tpix`, `_mpix`; `FORMAT.md` section 6.9) |

### Windows, text, input, graphics primitives (roles from names and callees, `[~]` unless stated)

| Range | Functions | Role |
|---|---|---|
| `0x32c68`–`0x34742` | `_wloc`, `_wprint`, `_wprcen`, `_whole`, `_wbox`, `_wfbox`, `_wxbox`, `_wind_clr`, `_windinit`, `_wcls`, `_wwind`, `_wttl`, `_wactive`, `_wopen`, `_wopen68`, `_wclose`, `_wputc`, `_wkprint`, `_wlocpr`, `_wkprcen`, `_wdout`, `_wdoutc`, `_wlocdc`, `_wdouti`, `_wlocdi`, `_wdoutl`, `_wdoutf`, `_wgetc`, `_wscan`, `_wiscan` | window system: open/close/clear windows with titles, print text (ANK and kanji), centred text, numbers (char / int / long / float), boxes, text and number input |
| `0x2e144`–`0x2e6c8` | `_setpage`, `_font`, `_hole`, `_box`, `_fbyte`, `_fsize`, `_fprint`, `_kfput`, `_kfprint` | graphics page select, font attributes (`_font(font, style, fg, bg)`, FORMAT.md section 6.12), frames, ANK / kanji glyph output |
| `0x39c86`–`0x3a57c` | `_wfnt`, `_flen`, `_wkfnt`, `_wfopt`, `_fstl` | font renderers (ANK from P4.X's three glyph tables with the `_wfopt` styles bold / italic / outline / shadow, Shift-JIS kanji via the CG ROM `trap`), string length; FORMAT.md section 6.12 |
| `0x2e6c8`–`0x2e98e`, `0x303b4` | `_clanim`, `_setanim`, `_anima`, `_animdoor` | picture animation (random frame changes from the `*_AN` / `*ANI` sheets, counted in `_anima` calls = input-loop passes; the open door + a 3 × 30 000 busy wait); tables and timing `FORMAT.md` section 6.10 `[C]` |
| `0x2e98e`–`0x2f90e` | `_clsc`, `_scinit`, `_scinit68`, `_scclose`, `_scprsub`, `_scupcopy`, `_scupz`, `_scup`, `_scdown`, `_scupb`, `_rprint`, `_dconv`, `_xprintf`, `_scgetc` | scrolling message window with `printf`-style output (`_dconv`: fixed-width decimal) and scroll-back |
| `0x2f9b6`–`0x30020` | `_clrbtn`, `_setbtn`, `_seticon`, `_getbtn`, `_inbtn`, `_xbtn`, `_onbtn`, `_kbn` | clickable buttons / icons (`_button` table, 256 × 32 B) and their keyboard shortcuts |
| `0x30020`–`0x302e0`, `0x349b2` | `_blocmc`, `_pushmc`, `_popmc`, `_inkey3`, `_mouseup`, `_while_mbtn`, `_mgetc`, `_mgetc2`, `_inkey2` | input: mouse cursor save/restore, wait for key or mouse button (runs animations while waiting) |
| `0x34e7e`–`0x352ea`, `0x3b698`–`0x3b6ec` | `_mopen`, `_mclose`, `_dispmc`, `_hidemc`, `_mcget`, `_s_ms_init`, `_s_ms_wait`, `_s_ms_get_cp`, `_s_ms_trg_on`, `_s_ms_trg_wait`, `_s_ms_trg_on2`, `_s_ms_trg_nowait`, `_s_ms_move_on`, `_MS_CURGT`, `_MS_CUROF`, `_MS_CURON`, `_MS_CURST`, `_MS_GETDT`, `_MS_INIT`, `_MS_LIMIT`, `_MS_PATST`, `_MS_SEL` | mouse driver / cursor (IOCS mouse calls) |
| `0x34c48`–`0x34dd4` | `_show_graf`, `_hide_graf`, `_show_text`, `_hide_text`, `_apalwait`, `_apalinit`, `_apalbrk` | video planes on/off, palette fades |
| `0x31bfa`–`0x31f02`, `0x3a57c`–`0x3a6d8`, `0x3a9b2`–`0x3aac4`, `0x32b0c`–`0x32bfe` | `_cvx`, `_cvy`, `_cvsx`, `_cvsy`, `_gcopy`, `_gcopy2`, `_gcopy68k`, `_ggcopy`, `_ggcopy68k`, `_xbox`, `_fbox`, `_xblack`, `_outp`, `_gcopya2`, `_gcopya22`, `_ggcopya2`, `_xbox2`, `_fbox2`, `_cs1`, `_ps1`, `_cs2`, `_ps2`, `_pushscrn`, `_popscrn`, `_cut_buf`, `_paste_buf` | coordinate conversion (screen-layout scaling), GVRAM block copies, filled / XOR boxes, screen save/restore |
| `0x3b3b4`–`0x3b49a` | `_aicon`, `_xicon`, `_ppp` | icon drawing; `_ppp` saves / restores a 32 × 32 text-VRAM block |
| `0x30462` / `0x3070a` | `_abs` / `_expi` | float helpers (`trap #14` FPACK) |
| `0x31b3c` / `0x31b9e` | `_xrndwrite` / `_xsperead` | write / read a record at an offset (`_lseek`) |

### Combat screen, field and auto mode; outdoor frame; system (roles from code read in passing or from names + callees `[~]`)

| Addr | Name | Role |
|---|---|---|
| `0x227b0` | `_encount` | fight driver: `_enc_mons`, field (`_e_terrain`, `_opn_field`), round loop `_enc_opt` / `_enc_patk` / `_enc_matk` / `_reset_act`, `_enc_end` `[C]` (`combat.md` section 1) |
| `0x223b4` / `0x22678` / `0x20b08` | `_enc_init` / `_reset_act` / `_enc_char` | "Encount!" window and monster info / clear the acted flags / `_set1` for every member |
| `0x20b40` / `0x24d4c` | `_minfo` / `_echkdisp` | monster picture and name box / combat status display |
| `0x2032c` / `0x20474` / `0x202de` | `_msg` / `_say` / `_bwait` | combat message window / speech line / message delay (`contr` 2) |
| `0x1e308` | `_deadmsg` | "The party has been wiped out. Sadly, your adventure ends here." → title |
| `0x21408` / `0x21e40` / `0x21f64` / `0x22040` / `0x22270` | `_copystr` / `_dsp_exit` / `_terr` / `_obj` / `_e_drpos` | field drawing: exit marker, terrain, one object / combatant, redraw a square |
| `0x26482` / `0x2679a` / `0x266c8` | `_opn_field` / `_e_finit` / `_e_obstacle` | draw the 7 × 7 field (`bback.pix`, `bobj.pix`) / load field graphics / random obstacles |
| `0x268d8`–`0x26cb0`, `0x26d9c`, `0x26ff4` | `_e_on_desert`, `_e_on_hill`, `_e_on_forrest`, `_e_on_grass`, `_e_on_mist`, `_e_on_nether`, `_init_terr`, `_init_edge` | field layout per terrain (`_e_terrain` picks one) and exit squares |
| `0x22950` / `0x1dcba` | `_b_decmvcnt` / `_e_drmvcnt` | MV counter decrement / display |
| `0x22b48` / `0x23012` / `0x23172` / `0x231ce` | `_bf_checker` / `_bv_checker` / `_b_sqchecker` / `_b_sqchkinit` | square occupancy / line-of-reach / reachable-square marks |
| `0x22d06` / `0x22f88` | `_b_face` / `_b_range` | turn a combatant to face (`p4mon.pix` frame by facing, table `0x44278`; `FORMAT.md` section 6.9) / distance between two squares |
| `0x233b6` / `0x234aa` / `0x2352a` / `0x27368` | `_center` / `_faraway` / `_b_friend` / `_b_getnear` | auto-mode movement helpers: toward the centre, away from enemies, toward a friend, nearest enemy |
| `0x1ed38` / `0x2998c` / `0x2bbee` | `_faropp` / `_chr_here` / `_mon_here` | farthest opponent / member on a square / monster on a square |
| `0x1edc4` / `0x1edde` | `_csf` / `_msf` | member / monster spell-effect flag lookups |
| `0x238ae` / `0x1c956` / `0x1cb30` | `_b_amove` / `_eauto1` / `_eautomov` | auto-mode member turn: move (strategy D), protect, `_try_attack`, `_try_cast` |
| `0x1fdf2` / `0x2505c` | `_try_attack` / `_obj_select` | auto-mode weapon action and target choice / random target in range |
| `0x236a0` / `0x236dc` | `_whobtn` / `_protwho` | "Whom does %s support?" (protect another member) |
| `0x1d282` / `0x1d396` / `0x1d4da` | `_b_chbtn` / `_b_skbtn` / `_chk_range` | enemy-in-range tests and target buttons |
| `0x24212` / `0x24280` / `0x244c2` / `0x24562` | `_b_cmouse` / `_b_smbtn` / `_b_drbtn` / `_b_getsqr` | square picking with the mouse |
| `0x29172` / `0x29296` | `_b_chkdir` / `_shoot` | direction to a target / missile flight animation |
| `0x1bcb4`–`0x1c4f6`, `0x1c806` | `_setbf`, `_clrsetb`, `_sbpr`, `_pset`, `_pnumb`, `_mnumb`, `_popt`, `_membermenu`, `_eoptinit` | combat member-options screen ("#  MV  ACTION", "Auto combat") |
| `0x1cb7c` | `_timelag` | "Display speed" setting (`contr` 1) |
| `0x2b718` / `0x2ba96` | `_spelltop` / `_getmagic(member, place)` | "Spell List" window / pick a castable spell for a place (lists per place, filter state 1 and MP: `spells-noncombat.md`) |
| `0x2d3ec` / `0x2d0c6` / `0x2d2b2` | `_pmagic3` / `_cast_47` / `_cast_50` | spells outside combat / Transportation town menu / Magic Bag (`logic-specs/spells-noncombat.md`) |
| `0x14218` / `0x14708` / `0x15612` / `0x15674` / `0x156ea` / `0x1575e` / `0x1578a` | `_eightkey` / `_omark` / `_ot_open` / `_oscroll` / `_outdoormenu` / `_opts1` / `_optset` | outdoor screen: 8-direction buttons, party marker, windows, command icons |
| `0x2682` / `0x26a2` | `_tcast` / `_tprof` | town: cast / profile |
| `0x1134` | `_syscon` | "System" menu: About, Save / Load (`_disk`), sound, display speed, end game |
| `0x7a14` / `0xa74` | `_party` / `_tranch` | guild: form the party (add / remove members, Elemental slot) `[~]` / copy one 190-byte record (src, dst) `[C]` |
| `0xdacc` | `_hall` | Hall of Glory building: history (`_hhist`), profiles (`_hprof`), III transfer (`_htran`) |
| `0x2e4` | `_cls` | clear the screen |
| `0x3a2ee`–`0x3a2f8` | `_wfopt`, `_xlen`, `_fgcol`, `_ylen` | font-renderer data (not code) |
| `0x3b108` | `_flpix` | combat-field flier sprite blit (`fly.pix`) |
| `0x3d01a`–`0x3d022` | `_tzstn`, `_tzdtn`, `_daylight` | XC libc time-zone data |

## 3. XC runtime (Sharp/Hudson XC C library), identified

All named by the symbol table; roles are the standard XC/Human68k libc ones. The DOS call words found inside each confirm
the role `[C]`:

| Range | Functions | DOS calls inside |
|---|---|---|
| `0x3b744`–`0x3b7e6` | `_access`, `_close`, `_exit` | `$FF43` CHMOD, `$FF3E` CLOSE, `$FF44` IOCTRL |
| `0x3b7e6`–`0x3bc8c` | `_fclose`, `_fflush`, `_filelength`, `_flushall`, `_fdopen`, `_fopen`, `_fread`, `_free` | `$FF0D` FFLUSH, `$FF44` |
| `0x3bcbe`–`0x3c0ce` | `_ltoa`, `_itoa`, `_wtoa`, `_lseek`, `_getml`, `_getmem`, `_malloc`, `_open` | `$FF42` SEEK, `$FF3C` CREATE, `$FF3D` OPEN, `$FF40` WRITE |
| `0x3c0ce`–`0x3c2dc` | `b_eof_seek_s`, `t_eof_seek_s`, `__fnocnv`, `_printf`, `_read`, `_sprintf`, `_strcat`, `_strcmp`, `_strcmpi`, `_strcpy`, `_tell` | `$FF3F` READ, `$FF42` |
| `0x3c2dc`–`0x3cc28` | `_write`, `__errcnv`, `__exit`, `__filbuf`, `__fmtout`, `__getiob` | `$FF40`, `$FF4C` EXIT2 |
| `0x3cc28` | `__main` (start-up, the program entry) | `$FF25` INTVCS, `$FF4A` SETBLOCK, `$FF51` GETPDB, `$FF44`, `$FF4C` |
| `0x3ce88` | `__stack_over` | `$FF09` PRINT |
| `0x3d012`–`0x3d628` | `_tzname`… `_timezone` (data), `_calloc`, `_ecvt`, `_fcvt`, `_fputc`, `_tzset`, `__flsbuf`, `_getenv` | |
| `0x3b62e`–`0x3b744` | IOCS wrappers `_APAGE`, `_B_CLR_AL`, `_B_DRVCHK`, `_B_KEYINP`, `_B_KEYSNS`, `_B_READ`, `_B_SUPER`, `_CRTMOD`, `_B_CUROFF`, `_GPALET`, `_G_CLR_ON`, `_MS_*` (mouse), `_SKEY_MOD`, `_TIMEBIN`, `_TIMEGET`, `_TPALET2`, `_VPAGE`, `_DRVCTRL` (`$FF0F`) | IOCS `trap #15` |
| `0x3b4fa`–`0x3b62e` | `_a_play`, `_beep`, `_m_init` … `_m_atoi` | OPM music driver (`OPMDRV.X`) calls |
| `0x35dc4`–`0x35f68` | `_vsprintf`, `_jstrncpy`, `_jstrlen` | game-side helpers (Shift-JIS aware) |

**FPACK (FLOAT2) calls used** (count over the text): `$FEE0` `__CLMUL` 1531, `$FEEA` `__CFTOD` 133, `$FEE8` `__CLTOF` 76,
`$FEE1` `__CLDIV` 62, `$FEEB` `__CDTOF` 53, `$FEF0` `__CDDIV` 50, `$FEE2` `__CLMOD` 49, `$FEEF` `__CDMUL` 47, `$FEED`
`__CDADD` 45, `$FEE3` `__CUMUL` 32, `$FEE6` `__CLTOD` 30, `$FEE7` `__CDTOL` 26, `$FEE4` `__CUDIV` 20, `$FEEC` `__CDCMP`
15, `$FEE9` `__CFTOL` 13, `$FEEE` `__CDSUB` 7, `$FE01` `__LDIV` 7, `$FE02` `__LMOD` 4, `$FE10` `__STOL` 4, `$FEF8`
`__CDTST` 3, `$FE00` `__LMUL` 3, `$FE0C` 2, `$FE42` 2, `$FEE5` `__CUMOD` 1, `$FE24` `__ECVT` 1, `$FE25` `__FCVT` 1
(names `[~]` from the FLOAT2 numbering; `__CL*`/`__CU*`/`__CD*` take their operands on the stack). With
instruction-start decoding `$FEE0` is the most common (not `$FE00`). The 36 `$FFFF` and other `$FFxx` words in
`_wfinit` and `_timezone` are data, not calls.
