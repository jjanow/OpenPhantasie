# C64 (German) Phantasie I — compiled-BASIC bytecode notes (s14)

Extract: `python3 tools/scripts/extract_c64_d64.py` → `extracted/side_{a,b}/` + `manifest.json`.
`TOWNR`/`OUTDOORS`/`DUNGEON1` = bytecode of an unidentified BASIC compiler; `RTL-64` = its runtime (a patched
BASIC ROM, loaded at `$A000`). PRINT/DEF FN/SYS lines stay as plain tokenized BASIC (readable with `strings`).

Opcodes decoded so far (RPN, operands pushed first) `[~]` = inferred from patterns:
- `80..8f` small int 0..15 · `89 nn` byte const · `8d lo hi` word · `88 xx×5` float
- `90 vv` / `94 hh ll` push var · `d0 vv` / `d4 hh ll` store var
- `b4 hh ll` call DEF FN (arg on stack) · `b8 nn` builtin `0xb4+nn` (00 SGN, 01 INT, 07 RND, 0e PEEK)
- `01` + · `02` − · `03` × · `04` / `[~]` · `06` AND · `07` OR · `50` POKE (addr, value)
- compares (true = −1, false = 0; left operand pushed first) **[C], RTL-64 traced + emulated (below)**: `2a >` · `2b =` ·
  `2c >=` · `2d <` · `2e <>` · `2f <=`; `06` AND · `07` OR (bitwise); `4f nn` = IF: true → body (`4f+2`), false → `4f+nn`.
- `48 hh ll` jump · `4a hh ll` gosub · `42` FOR · `47` NEXT. **Target address [C]:** big-endian, bit-inverted:
  memory = `~(hh·256+ll) & 0xffff`; file offset = memory − `0x7ff` (all modules load at `$0801`). Checked on
  TOWNR (resurrect FOR `0x3bab`, its NEXT `0x3c22`, heal `0x339d`), OUTDOORS, DUNGEON1.
- `a1 nn` / `e1 nn` array read/write `[~]` · `8a len text` print string · `4c` statement end `[~]`
- Character record = 170 B at `SA + 170·idx`; `FN P(X)=PEEK(...)`, `FN C(X)` = address. P(1..6) = STR INT DEX
  CON CHA LUCK, P(12) HP max, P(13) HP cur, P(18) status (0 dead, 1 OK), P(54) level.

Findings:
- Town RESURRECTION (TOWNR `0x3ba0`): target needs status 0 and CON ≥ 1; success = `RND·100 < 50 + 5·(lvl_c − lvl_t)`;
  status := 1, HP := max. **No CON write** — same as DOS.
- Inn MP raise (TOWNR `0x35f0`): `RND·100 < 20·(P9 − P10)` — same as DOS.
- **CON loss happens at death** (OUTDOORS `0x2f20`, `0x2fd0`, `0x628a`; DUNGEON1 `0x2eb5`, `0x2fa4`), next to
  "STIRBT". It reads `CON − SGN(CON) − (CON>1)·(P6<11)` (in `0x2fd0`/`0x2fa4` applied twice) = DOS OV2 `0x12cf`
  exactly [C] (−1, −2 if
  CON>1 and the 6th stat < 11; DOS tests seed `@10` < 11).

## Opcode semantics — resolved s58 [C]: reading B

RTL-64 loads at `$A000`. Dispatch `$a090`: `ldy #0; lda ($39),y; tax; lda lo,x; sta $a0a2; lda hi,x; sta $a0a3; jmp ****`.
The file's `$d800/$d801` operands (and `$91` at `$a092`) are placeholders; init `$a692-$a6a5` patches them to the lo
table `$aa0d` / hi table `$ab06` (`$d800` is colour RAM / charset). Handlers: `2a..2f` → `$adfc/$ae52/$ae56/$ae5a/$ae5e/$ae62`
(mask 1..6 in `$12`, shared body `$adfe`, result `rol(cmp+1) & mask` → −1/0); `06` → `$ad95` AND, `07` → `$adb1` OR;
`4f` → `$b588` (nonzero → `+2`, zero → `+nn`). Emulated with py65 on integer operands: truth tables and operand order
match B (the s14 reading A is wrong). Not traced: the `$a6e5` alternative patch (writes `$d8xx` operands); float compares
(`$bdc1`) not run — the `88` 5-byte float format above may be wrong `[~]`.

## Town Option → cast spell (TOWNR `0x31b6`, s16)

Spells are numbered **1-based** (manual numbering; DOS index + 1). Picker lists known spells (`P(60+n) = 1`, no
category filter, like the DOS `[~]`); "DU KENNST DIESES ZAUBERWORT NICHT" if unknown; cost `((n−1) mod 4) + 1`
must be ≤ MP cur `P(11)` ("DEIN MAGIEVERMÖGEN IST NICHT GENÜGEND"); MP is deducted **before** the effect
(`0x335b`). Dispatch: `n = 44` → gosub resurrect `0x3bab`; `n < 5` → heal `0x339d` (tier = n); `n = 54` → transport.
Any other known spell: MP spent, nothing happens. Same flow as DOS OV1 `0x254e`.

## RESURRECTION (C2, s16) — same rule as DOS

| Site | Caller | Eligible | Roll (success) | On success |
|---|---|---|---|---|
| TOWNR `0x3bab` | town cast | status = 0, CON `P(4)` ≥ 1, slot used `P(59)` ≠ 0 | `RND·100 < 50 + 5·(lvl_c − lvl_t)` | status 1, HP cur := max `P(12)`, stop |
| OUTDOORS `0x2b02` | wilderness cast, `n = 44` gosub at `0x3d88` | dead, CON ≥ 1 | same | same, stop |
| DUNGEON1 `0x2aaf` | dungeon cast, gosub at `0x44a4` | dead, CON ≥ 1 | same | same, stop |

- `FOR` over party slots in order; a failed check or roll goes to `NEXT` (next dead member gets its own roll),
  success ends the loop. **No target choice. No CON / age / gold change.** Levels are `P(54)`.
- vs DOS (town OV1 `0x2693`): identical except granularity. C64 `RND` is continuous → `(50+5Δ)/100`; DOS
  `rand/327` is an integer 0..100 → `(50+5Δ)/101` (always at Δ ≥ +11, C64 at Δ ≥ +10). Cosmetic; DOS wins on numbers.
- Not resurrection: OUTDOORS `0x2ae7` / DUNGEON1 `0x2a94` (`RND·100` vs `50+3·lvl`, then `50+5·(lvl − v)`, `v −= 6`,
  no status/HP write) — some other level contest, unidentified.
- OUTDOORS `0x62ad`: a third copy ("IST WIEDER LEBENDIG"), right after the trap/damage death block. No direct
  jump/gosub reaches it (a computed dispatch might), and its first-check failure jumps **into** the success block
  (`0x62e6`), not to `NEXT`. Context unclear `[~]`; not counted above.

## HEALING (C3, s16) — `c = 6·tier² + 4` (10/28/58/100), HP capped at max everywhere

| Site | Path | Formula | Range |
|---|---|---|---|
| TOWNR `0x33bd` | town cast | `INT(RND·c)` | **0 … c−1** (no floor) |
| OUTDOORS `0x3e37` | wilderness cast | `INT(RND·c)` | **0 … c−1** (no floor) |
| DUNGEON1 `0x4506` | dungeon cast | `max(1, INT(RND·c))` | 1 … c−1 |
| OUTDOORS `0x203a`, DUNGEON1 `0x200d` | combat (action code `tier·1000 + target`) | `max(1, INT(RND·c))` | 1 … c−1 |

- Combat = DOS combat (OV2 `0x47fb`, `(c·rng)/100`, floor 1) = the manual's 1–9/1–27/1–57/1–99, including 1
  carrying double weight.
- Town = **DOS town** in shape (no floor, 0 possible ~1 in `c`). DOS town's rare top value `c` doesn't exist on the
  C64: that part is DOS's RNG scaling. So the town/combat split is in **both** ports → likely inherited from the
  original, not a DOS-port slip (`DIFFERENCES.md` D-010 evidence).
- Wilderness / dungeon cast: on the C64 the wilderness has no floor and the dungeon has one.

## More opcodes (s223, read on `P.END` / `CEMETARY` / `WIN`, RTL-64 handler table)

- `8d hh ll` word constant is **big-endian** (`8d 04 b3` = 1203). `88` + 5 bytes = a CBM float (`88 90 50 18 00 00` = 53272).
- Variables: `90 vv` / `91 vv` / `92 vv` push (numeric / string), `d0`–`d2 vv` store; vv = the entry's offset in the
  program's variable table (7-byte entries: 2 name bytes, 5 value bytes; the table follows `4c 5e 03 00 80 00 00 00`).
  `b4 00 vv` calls `DEF FN` vv; `a2 vv` reads string array vv.
- `42 00 vv` FOR vv (start stored first, limit pushed) · `43 00 vv` FOR with STEP (step pushed last) · `46` NEXT ·
  `4c` RETURN · `19` NOT · `05` power · `b8 18` TI · `f8 1b` TI$ := · `b8 0f` LEN · `b8 16` MID$.
- `4e nn t1 t2 …` **ON … GOSUB** (RTL-64 `$B528`), `4d` ON … GOTO (`$B52C`): nn = 2 × the number of targets; value v
  takes the v-th target (v = 1 the first), out of range falls through. `48` GOTO is `$B4AF`, `4a` GOSUB `$B4A6`.
- Plain-text statements: after a statement break, a BASIC token runs as tokenised text to the next `:` or `00`:
  `87` READ, `99` PRINT, `9f` OPEN, `a0` CLOSE, `a1` GET, `84` INPUT#, `93` LOAD; `51` is SYS with a computed address
  (pushed first) and its plain-text arguments.

## More opcodes (s228, read on `OUTDOORS` / `DUNGEON` / `TOWNR`, II's `…2`)

- Variable names: in the table after the code, both name bytes' bit 7 = an integer variable (`%`), the first's alone =
  `DEF FN`, the second's = a string (CBM's rule; `FN P(X)` is a `DEF FN`, `L%` / `H%` integers).
- `47 hh ll` NEXT with its variable (`46` without). `94` / `d4 hh ll` push / store a variable whose id is ≥ 256.
- `a0` / `a1` / `a2 vv` read float / integer / string array vv; `e0`–`e2` store. Array ids follow the `DIM`s in order
  (`DIM C%(2)` → `$20`, `SU(11)` → `$5A` in `OUTDOORS`).
- `8d ff ff` (word 65535) is −1 (a `STEP −1`, the ring tune's end mark).
- The SID sounds: `GRAPHICS.md` 9.8 (`phantasie1/`).

