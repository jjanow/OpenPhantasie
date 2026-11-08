# Wand of Nikademus and the I → II (→ III) transfer: cross-version research (s76, static only)

Tags: [C] confirmed from code or data bytes · [~] probable · [?] unknown or not looked at.
Nothing was run in an emulator. Extracted files and listings were kept in the s76 session scratchpad (not in the repo; re-run the scripts).
Scripts are in `tools/scripts/cross-version/` (Applesoft and C64 BASIC detokenisers, DOS 2 ATR and D64 extractors, D88 to raw, EXEPACK
unpacker, capstone 6502/68k/x86 wrappers).

## 0. Headline: the ST does not drop the wand. Every version turns it into a flag made from Phantasie I's SCORE.

No version carries the wand **item** over. Every converter clears the carried-item slots. But no converter
touches the character's Phantasie-I **score** field. Phantasie II then shows "WAND OF NIK" for any character
whose I score is > 99, and the Mystic adds "ALSO HEROES OF GELNOR" for the same characters. This is how the
manual's "pay all items except the wand of Nikademus" is built.

| Version | Score field kept by transfer | II test | What II shows | Evidence |
|---|---|---|---|---|
| Apple II (4am crack, v2/5/86) | record byte 42 (= I's score: Apple I `7452 PRINT "SCORE" FN P(42)`) | `FN P(42) > 99` | items screen `"WAND OF NIK(USELESS NOW)"` (line 5460); inspect list `"-WAND OF NIK-"` (7525); Mystic `Z = Z + (FN P(42) > 99)` → `"ALSO HEROES OF "H$(0)` (=GELNOR) (4632/4662) | [C] |
| C64 (v1.0 side A/B; v1.2 side A has the same TOWN2/OUTDOORS2) | byte 42 | bytecode `89 2a b4 00 80 89 63 2a 4f` = `IF FN P(42) > 99` | `"-WAND OF NIK-"` (TOWN2 `0x3eaf`, OUTDOORS2 `0x52eb`); Mystic count (TOWN2 `0x26f2`). There is no "USELESS NOW" line | [C] |
| Atari 8-bit (atarimania set) | byte 42 | `lda #$2a; jsr $363f; cmp #$64; bcc` | `"-WAND OF NIK-"` (AUTORUN.SYS side A, mem `0x3234`); Mystic (TOWNR.COM mem `0x4dc4`, followed by the byte-8 ≥ 151 test) | [C] |
| **Atari ST** | word @70 (`0x46`) = DOS/Amiga I score (`save-file-format.md` @70 [C]). The UTILIT2 converter copies it: @70/71 are not in FORMAT §6's exception list | `cmpi.w #$63,$46(a0); ble` | PHANT.PRG `0x3a16` → `0x3012(…, wandflag)` → RSC tree 0x17 object 7 **"- WAND OF NIK -"** (objects 4/6/7 = THE ORB / FILMON WAND / WAND OF NIK). The same in `0x17d62` → OUT.RSC tree 0x24 obj 7. Mystic `0xd7e4` → `0xf64` → `"[original text]"` | [C] |
| PC-98 (Star Craft) | byte `0x23` of the 155-byte record. The utility converter `0x32bf` writes 08,09,0a,0b,0c(w),12–20 even,29–2b,2c(dw),30,31,35,37–6c,7e,88–91,93–96,99 and never `0x23` | `mov al,[si+23h]; cmp al,63h; jbe` (Program disk img `0xc58a`) | sheet `"  [original text]"` (DS:1a00, refs `0xc5a4` and 4 more) | [C] code. That I's score sits at `0x23` in the PC-98 I record is [~] (the utility printout has "P1 P2" score columns) |
| PC-88 (Star Craft) | `0x23`. The utility converter at img `0xabb0`–`0xae05` is the PC-98 C code compiled for Z80 (same field writes, no `0x23`) | `ld de,23h; add hl,de; ld a,(hl); cp 64h; jp c` (Program disk ×4, Player disk ×3) | "[original text]" strings present | [C] converter. [~] that those `cp 64h` sites are the wand display |
| MSX2 (Logic Tank 1988) | — | `cp 64h` on `+23h` at 7 sites (same record) | "[original text]" in the overlays | [?] **No I→II transfer routine found.** There is no utility, and no "Phantasie I"/old-man text in PM2.COM or any *.OVR. GSELECT only offers start options. The MSX manual's text layer is OCR garbage |

So **D2-001 needs reframing** (for the parent; tracked files not edited). The ST does what the manual says, the
same way every other version does it. The item (I id 150) is gone everywhere. "The wand" in II is a derived
flag (I score > 99). Only the Apple label calls it useless, and it is.

What makes the I score > 99 [C]:
- **Apple I:** the Zeus win does `POKE FN C(42),100` for every party member (dungeon side B, line 150). The
  Mystic max score includes `22 * I%(10)` for holding item 150, the wand.
- **DOS I:** the score is only the Mystic formula (`endgame.md` §2.3: Σlevels + 18 per distinct ring id
  141..150 + 66 for WAND 150 + 1, all ÷ 3), and the Zeus win writes no score. So > 99 in practice needs the
  wand plus most rings and high levels.
- **Amiga I** (the I that feeds ST II): the game has no `move.w #100,$46(An)` (`7c 00 64 00 46/47`). So it
  probably sets no score on the win, like DOS [~]. Other encodings were not searched.

## 1. D2-001: items on transfer, per version

| Version | Items | Starter kit | Wand |
|---|---|---|---|
| Apple II | `FOR X=152 TO 160: POKE Y+X,0` (all 9 slots) | 152/153/154 := 4/49/23. Worn 48/49/50 := 4/23/49. Points 162/163/164 := 4/4/3 (lines 1030–1060, side B `0xb0a4`–`0xb1ce`) | flag via byte 42 (§0) [C] |
| C64 | same table (side B file `B`, lines 2030–2095; BASIC V2 source) | same | same [C] |
| Atari 8-bit | side C `B.COM`, 6502 at `$4925`–`$4a3e`: 152–160 := 0, then the same kit (`$49a3`–`$49cd`) | same | byte 42 not written [C] |
| ST | FORMAT §6 (slots 4–9 := 0, kit 4/49/23) | same | @70 kept [C] |
| PC-98 / PC-88 | `@88..@91 := 0`, then `@88/89/8a := 4/23/49`, `@93/94/95 := 4/4/3`, `@96 := 1`; worn `@29/2a/2b := 4/23/49` | same ids | `0x23` kept [C] |
| MSX2 | no transfer found | — | [?] |

**II item ids:** in no version is there a "wand of Nikademus" item. II-ST id 149 = `FLMN WAND`, 150 = `---ORB---`
[C, known]. The same table order is in C64 side A (`…AMULET 8 FLMN WAND ---ORB--- QUARTZ…`), Atari side A,
MSX2/PC-88 (reversed listing) and PC-98 [C]. The PC-98/PC-88 *utility* disks also hold Phantasie I's item list
with `--WAND--` as I id 150 (PC-98 utility img near `0x7b00`) [C]. It is only used for the printout [~].

## 2. Is the wand used later?

**In II: no.** It is display only plus the Mystic "heroes of Gelnor" line.
- Apple: the only `P(42)` uses are 4632, 5460 and 7525 (grep over both sides). Nothing writes `C(42)` [C].
- C64: the only `89 2a b4` hits are the three above [C].
- ST: the only `cmpi #$63,$46` hits are `0x3a16`, `0x17d62` and `0xd7e4` [C].
- No other use is [~], because accesses in other encodings (raw PEEK, other registers) would not show up in
  these searches.

**In III: yes, as a quest object, on the Apple. The DOS transfer does not bring it.**
- **Item id** [C]: DOS III item 180 = "Wand of Nikademus". The name table is DS:1308 (DS = unpacked image
  `0x28c00`; EXEPACK removed with `scripts/unexepack.py`); entry 150+id, id 180 → "Wand of Nikademus", 181 =
  "0", then spells.
  - Apple III hides item 180 in the item list when the wand flag is set: `5470 … IF Y = 180 AND FD = 1 THEN Y = 0`.
- **Apple III transfer** (disk 1 side B; from I or II, `FR`) [C]:
  - Line 1001: `W2=0:W1=0:FOR Y=8225 TO 14345 STEP 170: W2=W2+(PEEK(Y+8)>199): W1=W1+(PEEK(Y+42)>99): NEXT`.
  - Line 1080: `IF W1 > 0 THEN POKE Y+126,4`. The bytes run across sectors `0x8af6`→`0x8909`; the Applesoft
    link pointers check out: 1070→`0x5460`, 1080→`0x5473`, 1090→`0x5483`.
  - So if **any** roster character had I score > 99, **every** converted character gets byte 126 := 4. That is
    bit 3, the wand: `5460 MF = FN P(126): IF FN D(3) = 1 THEN … PRINT "NIKS WAND"`, and 7525 `"-WAND OF NIK-"`.
  - The rest of III's transfer: items and equipment cleared (1030, 1065; **no starter kit**). Gold uses the
    same ÷35 formula (1040/1055). XP `EXP(LOG(Q)/3.5)-1`. Monk/Wizard #5, Ranger/Priest #1 (1060/1070).
- **Apple III uses the wand bit** [C for the interpreter; [~] for which event is which]:
  - The dungeon script interpreter has op 24 (`8750 POKE FN C(D2),FN P(D2)+2^(D-1)`: set a bit), op 25 (8760:
    any member has bit D of byte D2 → jump) and op 23 (8700: item D held or in the sack → jump).
  - The dungeon disk (disk 2, side A) has op-25 records `99 03 7e xx` (test byte 126 bit 3) 9×, op-23 `97 b4`
    (item 180) 3× and op-24 `98 03 7e` (set the wand bit) 3×, in clusters at `0xa5c5`, `0xecc5` and
    `0x1445f`–`0x145fb`.
  - The same disk has the texts "BLACK STATUE OF NIKADEMUS HOLDING A WAND… 2)TAKE THE WAND", "THE WAND SEEMS TO
    OVERPOWER YOU… YOU CAN NOT GET RID OF IT", "YOU DO NOT HAVE THE WAND, STUPID", "WAVE THE WAND AND SAY
    'STRATICON UBLE'", "PLACE THE WAND ON THE FLOOR" and "DO NOT USE THE WAND UNTIL YOU ARE READY".
  - So in III the wand is a key item of the Nikademus endgame: you either find it in III (item 180 → bit set) or
    bring it in through the transfer flag.
  - The III Mystic also adds `INT(LOG(1+max P(126))*16.23)` to the score (4635/4640) [C].
- **DOS III transfer** (MAIN.EXE MS-LINK overlay 1, file `0x1bc00`) [C]:
  - Two modes:
    - Mode 1: `guild.dat` opens; the converter reads 282-byte records (I).
    - Mode 2: tried only when the `guild.dat` open fails (`0x119`–`0x13b`). It is set when `final2.pic` exists and
      would read 318-byte records (II-ST).
    - But function 0 then reopens "Guild.dat" in both modes, so mode 2 looks unreachable (dead path) [~].
  - It copies stats, status, score, level, class, race, gold and name only (field-word list DS:005e = 0,1,2,3,4,5,
    12,35,44,110,128,131,132,141,142,200).
  - **No item field, no item 180, no wand flag.**
  - The I score (word 35 = @70) goes to the III record field that the Mystic (overlay 2 `0x2ce1`, `+0x292 ≠ 0`)
    prints as "[original text]".
  - Gold `÷35` is added to III gold. XP := 100 × level.
  - Mode 2 also sets a "≥ 100 → Ferronrah" field from a local slot that seems never to be read (id 200 is at the
    loop limit). This is on the dead path [~].
  - **The call tree was checked for wand writes.** None of these touch item 180, the score/Gelnor field, the
    item words, or 99/100:
    - `0834:247c` (MAIN.img `0xa7bc`): clears the 262-byte new-character buffer at `0x9de8`.
    - `0834:1d5b` (`0xa09b`): inserts the buffer into the roster.
    - The overlay-2 call `int 3Fh 02 7d 22` → ovl2 `0x227d`: the character review screen. Its only item-like writes
      are a class kit `0x9e7e/80/82/84` := 4, 22, 41|44, 64 and class spells `0x9e98`.
    - The lone `mov ax,0B4h` at MAIN.img `0x1f23` is a long argument to a UI call, not item code [~].
  - Item 180 is special elsewhere: the sack pooling skips it (`cmp si,0B4h; jge`, ovl2 `0x39f`) and `0x5c8`
    removes it from the sack (cursed) [C].
  - So **DOS III does not keep the wand** [C static]. That contradicts III's manual ("lose all of their items
    except the Wand of Nikademus") and the Apple III code. Whether a DOS III event treats the Gelnor/score field as
    "has the wand" is not decoded [?].
- **Amiga III:** strings show a transfer from Phantasie I only ("Transfer Phantasie I characters", `final.pic` /
  `final2.pic`, "This [original text] disk!"). Not decoded [?].
- **III's manual:** "Your characters will lose all of their items except the Wand of Nikademus… They will
  retain any Divine spells (#57 and above) and their normal beginning spell by class" (`phantasie3-manual.pdf`
  text, ~line 981).

## 3. D2-003: gold

| Version | Rule | Tag |
|---|---|---|
| ST | ⌊hi/35⌋·256 + 100 | [C] (FORMAT §6) |
| Apple II, C64 | `POKE Y+14,PEEK(Y+14)/35`, `POKE Y+15,100`. Byte 14 = high byte (bank `FN P(14)*256+FN P(15)`) → **identical** | [C] |
| PC-98, PC-88 | `@0c` word: `div 2300h` (= 35·256), `shl 8`, `+64h` → **identical** (PC-98 `0x32d8`–`0x32ec`; PC-88 `0xabda`–`0xabfd`, `ld de,2300h; call div`) | [C] |
| **Atari 8-bit** | `$499d lda #$64; ldy #$0e; sta (80),y` → byte 14 := 100. Later `$4a0a lda (80),14; lsr ×5; sta (80),15` → byte 15 := 100>>5 = **3**. It reads byte 14 **after** overwriting it, so the result is a **flat amount for everyone**. The shipped saves are little-endian (a new character is `0,1` = 256 in CHAR2.RUD; C64 is `1,0`), so the result is **868 GP** (3·256 + 100). Probably a bug: >>5 ≈ ÷32 meant to replace ÷35, applied to the wrong byte | [C] code. [~] 868 vs 25,603 (byte order taken from save data, not from the bank code) |
| MSX2 | no transfer found | [?] |
| Apple III → III | same ÷35 rule | [C] |
| DOS III | III gold += old gold ÷ 35 (no +100) | [C] |

## 4. D2-002: Rangers / Monks

- **Text** [C for the text-layer PDFs]: `phantasie2-manual.pdf` has three transfer sections (raw text lines
  3396, 3884, 4196): Apple/C64, Atari, Mac/ST. `phantasie2-manual-alt.pdf` and `phantasie2-readme.pdf` (line 82)
  say only "Wizards will keep spell #5 and Priests spell #1". **No section mentions Rangers or Monks.** The
  200-dpi combined rulebook in `phantasie1-and-2/` has no text layer and was not read [?]. III's manual says
  "their normal beginning spell by class", which covers Rangers and Monks.
- **Code: identical in every version decoded** [C]. Class ids THIEF 0, MONK 1, RANGER 2, FIGHTER 3, PRIEST 4,
  WIZARD 5 (Apple `260 DATA`). Class 1 or 5 gets spell #5; class 2 or 4 gets spell #1. Spells #1–#54 are cleared
  first and #55+ kept:
  - Apple 1060/1070 (`POKE Y+65,1` / `POKE Y+61,1`, spells 61–114 zeroed)
  - C64 2080/2085
  - Atari `$49db`–`$49fc` (bytes `0x41` / `0x3d`)
  - PC-98 `0x339c`–`0x33e8` and PC-88 `0xad11`–`0xad70` (class `@87`; `@3b` / `@37` := 1; `@37..@6c` zeroed)
  - Apple III 1060/1070
  So the ST rule is the original design, not an ST quirk.

## 5. Side findings (not in DIFFERENCES-II yet)

- **Skills.**
  - Apple, C64 and Atari zero bytes 20–40 even on transfer. Those bytes are ATTACK 20, PARRY 22, FIND 24,
    SPOT 26, DISARM 28, LISTEN 30, PICK LOCK 34, SWIM 38 (Apple sheet 7456–7464).
  - PC-98 and PC-88 zero `@12..@20` even.
  - **The ST copies skills** (FORMAT §6). The transfer screen in every US 8-bit version says "YOU FEEL AS IF YOU
    HAVE LOST MANY SKILLS AND SPELLS" [C]. This may need a new D2 entry.
- **XP.**
  - Apple/C64: Q = XP/256 + 1 → `EXP(LOG(Q)/4.5)-1`, stored in units of 256.
  - Atari: byte52 := (byte52>>5)+3, bytes 51 and 53 := 0.
  - PC-98/PC-88: XP/1000 against the step table DS:196c = 36, 131, 358, 813, 1627, 2966, 5039, 8096 → (n+1)·256
    [~] (helper `0x5cae` not traced).
  - ST: its own step table. So all differ [C].
- **Score byte 8 / II score.** The US 8-bit converters print the "orb destroyed, curse lingers" text when
  byte 8 ≥ 200. C64 `Z=Z+PEEK(Y+8)>=200` has an operator-precedence bug. The Apple III transfer uses
  byte 8 > 199. The II Mystic gives "HEROES OF FERRONRAH" at byte 8 > 150 (Apple 4635; Atari `cmp #$97`;
  ST @282 > 150).

## Not looked at / limits

- MSX2 transfer: none found [?].
- Amiga III: not decoded [?].
- DOS III dungeon events: not decoded [?].
- Apple II `.woz` originals: only the 4am crack was read here (since s169 the `.woz` decodes; its files equal the crack's
  but for `SSI.INIT` / `SSI.INIT2`, `QWERTY` and RDOS: `reverse-engineering/apple2/phantasie2/FORMAT.md` section 1).
- C64 v1.2 side B: not on IA.
- Linking each Apple III script record to its message: not done [~].
