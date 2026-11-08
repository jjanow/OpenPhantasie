# Phantasie IV — level-up training, level-up gold and magic training (spec, 3d.6)

Source: X68000 `P4.X` (`_trang` `0x3c30`, `_calpow` `0xc320`), read in the disassembly with the FPACK operand order of
`../FORMAT.md` §6. Cross-checked against the MSX2 code (segment `100C`, `../../../msx2/phantasie4/FORMAT.md` §6): same
constants, same ability choice per class, same town table, same gates, same `_calpow` gold rule `[C]`. Tags: `[C]`
from the code, `[~]` likely, `[?]` open. Static reading only; nothing here was checked in an emulator.

Record offsets: `../FORMAT.md` §6.2 (+04 experience, +08 level, +09 "Score", +0A condition, +18 class, +1A social class,
+26 personal gold, +2F–34 Str, Int, Dex, Con, Cha, Luck). Party gold = `_par` +1C (`../FORMAT.md` §6.7).

## 1. Who may train `[C]`

At the guild's training hall the player picks a party member. Then:

1. Condition must be Okay (1); otherwise "is in poor condition and cannot train".
2. If record +09 ≥ 125 or level ≥ 31: "There is nothing more I can teach". (+09 is shown as "Score" on the character
   sheet; the manual (p. 29) defines Score as "a number giving an overall rating of what the character has achieved".
   s82: only the ending writes it — ending score 125–225 + the party's average level — so after any ending the hall
   teaches nothing more `[C]`, `ending.md` §2.)

## 2. Experience needed for the next level `[C]`

```
r = (sum of the class's abilities) / D[class]          (r = 0 → r = 0.00001)
need = trunc( 1350 × (1.8^L − 0.8) / r )               L = current level
if L = 0: need −= 3333
```

| Class | Abilities summed | D |
|---|---|---|
| 0 Thief | Str + 2 × Dex | 19.8 |
| 1 Monk | Str + Int + Dex | 49.5 |
| 2 Ranger | Str + Int + Cha | 52.8 |
| 3 Fighter | 2 × Str + Dex | 33.0 |
| 4 Priest | 2 × Int + Str | 36.3 |
| 5 Wizard | 2 × Int + Luck | 42.9 |
| 6 Shadow Warrior | Int + Dex + Luck | 75.0 |
| 7 Gargoyle | Str + Dex + Con | 48.0 |
| 8 Sage, 9 Lord | (no case: r stays 0 → 0.00001) | — |

The ability choices are written as the offsets read; they are not corrected (Ranger uses Cha, Wizard and Shadow
Warrior use Luck). Consequences `[C]`: Thieves need the least experience (an 18/18 Thief has r ≈ 2.7, an 18/18
Fighter r ≈ 1.6). A Sage or Lord would need about 1.35 × 10^8 × (1.8^L − 0.8) experience, so in practice they can
never train; only Hall persons have those classes, and Hall persons are never party members.

If the character's experience is below `need`, the hall says "needs N more experience to reach the next level" and
stops. MSX2 computes the same value as 1350 × 1.8^L − 1080 (`100C:0x0585`).

## 3. Training cost `[C]`

```
cost = trunc( L^2.2 × (20 − Cha)^2 × T[map] / 100 ),   capped at 25 000
```

- `L` = current level, `Cha` = the trainee's Charisma.
- `T[map]` (`_ctab`, indexed by the current map number `_cmapn`): maps 0–9 → 80, 10–12 → 100, 13–21 → 120. Which map
  number is which town is `[~]` (`ancestry.md` §7 item 5).
- The cost is paid from **party gold** (`_par` +1C). Not enough → "not enough gold". The player confirms first.
- Properties of the formula: Charisma 20 trains for free, and the cost rises again above 20 (`(20 − Cha)^2`).
  Examples at T = 100: level 5, Cha 12 → 2 207; level 10, Cha 12 → 10 143; level 10, Cha 3 → capped 25 000.
- **No social class or ancestry term** `[C]`: neither `_trang` (X68000) nor seg `100C` (MSX2) reads record +1A or the
  ancestor fields +22–25.

## 4. Level-up gold: `_calpow(record, L)` `[C]`

After paying, `_calpow` raises the level (skills, magic level, hit points, spells: `../FUNCTIONS.md`). First it adds gold:

```
L = min(L, 30); if L = 30: return (no gold, no level-up)
bonus = social ^ social                     (Peasant 0 → 0)
personal gold (+26) = min(65 535, personal gold + bonus × L × 30)
```

| Social class | bonus | gold per level-up from level L |
|---|---|---|
| 0 Peasant | 0 | 0 |
| 1 Laborer | 1 | 30 L |
| 2 Craftsman | 4 | 120 L |
| 3 Noble | 27 | 810 L |
| 4 Super Hero | 256 | 7 680 L |
| 5 Dark Lord | 3 125 | 93 750 L (→ cap) |
| 7 Royalty | 823 543 | → cap 65 535 |
| 8 Outcast | 16 777 216 | 32-bit overflow `[~]` |

- `L` is the level **before** the level-up (creation calls `_calpow(rec, 0)`, so a new character gets no bonus).
- **Level 30 `[?]`:** §1 lets a level-30 character train (it blocks only level ≥ 31), the cost is charged before
  `_calpow`, and `_calpow` returns at once for L = 30 — so training at 30 would take the gold and change nothing. But
  the experience check of §2 comes first, and `need` passes the signed 32-bit range at about level 24–26 (1350 × 1.8^25
  ≈ 3.3 × 10^9 before dividing by r ≈ 1.1–2.7). What the float→long conversion returns there is FLOAT2's `__CDTOL`
  behaviour on X68000 (not in P4.X) and seg `100C`'s own routine on MSX2, so the practical level cap and whether the
  two platforms agree at the top levels are open.
- Social classes 6 and 9 (Black Lord, Lord) belong only to Hall persons. For 8 (Outcast) and 9 the product overflows
  32 bits; the comparison with 65 535 is signed, so a wrapped negative sum is written as its low 16 bits `[~]`
  (original quirk, not fixed).
- +26 is the character's **bank account** (s81). It reaches party gold only through the bank: `_withdraw` moves
  account gold into party gold (cap 65 000), `_deposit` shares party gold out into the accounts (cap 65 000 each) `[C]`
  (`towns.md` §5).

**Effect `[~]`:** a Noble, Super Hero, Dark Lord or Royalty character earns at each level-up about as much as, or more
than, the next training costs (Royalty gets the 65 535 cap every time). This is the only code path where social class
makes levelling "cost no money". See `ancestry.md` §9 item 2 for how it relates to magazine C.

### 4.1 The rest of `_calpow` and the training screen (s102) `[C]`

Read in the disassembly (`0xC320`–`0xC966`) and checked against MSX2 `100C:0x0A3C`–`0x0EDD`. Order after the gold:

1. Skill bonuses (`skills.md` §1), then the skills: creation (L = 0) computes all eleven; a level-up runs `_sklup` (three
   picks, `skills.md` §3) — the picks come **before** the HP rolls, so they draw no random numbers.
2. HP: n = (TABL[class][min(L, 14)] × (Con + 5)) >> 4 (`__CUMUL`, `lsr.l #4` at `0xC724`); max HP = Σ n × rand(3),
   at least old max + 1, cap 255. Current HP is not touched here (`_trang` then calls `_healch`).
3. Magic level +36 = TABL 0x14A[class][min(L, 14)] + (L − 14 when L ≥ 15), cap 255. **Max MP (+37) is not changed by a
   level-up**: it grows only at the inn (§4.2).
4. Learnable spells: x = L + 1; if x ≥ 2: when x > 15, x = x mod 15 (`__CLMOD` at `0xC868`; MSX2 the same at
   `0x0E48`–`0x0E5A`); the three spell numbers of TABL 0x6EA row (class × 15 + x − 1) whose state is not 1 become 2. So
   from level 16 on the schedule starts again at row 1 (spells already known or open are unchanged), and at L = 29 the
   row index is −1: the **previous class's** level-15 row (for Thieves the three bytes before the table) `[C]`.
5. Level (+08) = x. **X68000 writes the wrapped x**: a level-15 character who trains becomes **level 1** (16 mod 15),
   and can never pass level 15. **MSX2 adds 1 to the level byte** (`0x0ECB`–`0x0ED7`: `ld a,1; add a,(hl)`), so its
   levels go on to 30. Manual: levels rise with experience; Priests reach higher levels than other classes (D4-015,
   decided cap 35). → `DIFFERENCES-IV.md` D4-060.

**Training screen (`_trang`) `[C]`:** the trainee is the member under the marker (click a member to switch; starts at
member 0). Not Okay: "<name> is in poor health, so cannot be trained." Score ≥ 125 or level ≥ 31: "<name>, there is
nothing more I can teach you." Experience short: "<name> needs N more experience points for the next level." Else
"<name>: the training will cost N gold. Is that all right?" — yes with party gold < N: "You do not have enough gold.";
yes otherwise: party gold −= N, `_calpow(record, L)` (with the skill picks), `_healch` (redraws the member). The hall
stays open for the next member until Leave.

### 4.2 MP growth at the inn (resolves `towns.md` §11) `[C]`

Record +72 is set to 1 by `_psetcast` (`0x1EDF8`), i.e. **every spell a member casts in combat**. The next inn stay
(`_inn`, `towns.md` §4) clears it and, if rand(100) ≤ (magic level +36 − max MP +37) × 20, raises max MP by 1. So max MP
creeps up towards the magic level, one point per inn stay after a fight with casting.

## 5. Magic Training Hall (`_mage` `0x34b0`) `[C]`

Same on MSX2 (seg `100D`: constants 100, 90, 1.177, 9000 and the Fighter −2 at `0x02C5`). `L` = level, `c` = class
number, `Int`, `Cha` = the trainee's.

1. **Gate:** condition Okay. Then, with `known` = number of spells in state 1 and `open` = number in state ≥ 2
   (learnable, set by `_calpow`):
   ```
   quota = L × Int^1.177 / 90 × (c + a)        a = −2 for Fighter (c = 3), else +1
   ```
   If `known > quota` or `open = 0`: refused (s165, read: `0x3584`; +09 ≥ 125 at `0x35A8`, level +08 > 30 at
   `0x35D4`) — "nothing more I can teach" when record +09 ≥ 125 or level ≥ 31, else "not
   yet worthy to learn". (`c + a`: Thief 1, Monk 2, Ranger 3, Fighter 1, Priest 5, Wizard 6, Shadow Warrior 7,
   Gargoyle 8.) Whether `known = quota` still allows one more is read from a float compare (`bgt`) `[~]`.
2. **Price per spell (integer arithmetic, each division truncates):**
   ```
   price = trunc(9000 × T[map] / 100) / Cha / Int × L / (9 − c)
   ```
   with the town factor `T` of §3. No social-class term. Paid from party gold.
   **s102:** the gate is a double compare of known against the quota (`__CDCMP` at `0x3574`, `bgt` → refused), so
   known = quota still passes `[C]`. The quota is checked **once per visit**: inside, the loop only asks while
   open > 0, so a member who passes may buy every open spell in one visit.
3. **Screen (s102) `[C]`:** "Any spell can be learned for N gold each, so, which spell will you learn?" and the spell
   list `_getmagic(member, 8)`: five pages of 13 spells in number order (1–13, 14–26, 27–39, 40–52, 53–65), only spells
   in state 2 shown; page buttons 1–5, 6 Quit. Quit (100) leaves the member. Party gold < price: "You do not have
   enough gold." and the member is done. The price is computed once per visit.
4. **Each attempt:** the price is paid, the character ages 2/26 year (record +1E += 2), and the spell is learnt if
   rand(100) < 5 × Int (rand returns 1..n, so the chance is (5 × Int − 1) %); otherwise "failed" (gold and time are lost).

## 6. Open `[?]`

- (s102) The level wrap of §4.1 (D4-060) has not been seen in an emulator.

- (Resolved s82: record +09 is written only by the ending, = ending score 125–225 + average level, so the 125 gate
  ends training after any ending — `ending.md` §2.)
- Map-number → town mapping for `T[map]`.
- An emulator check of one training (cost and experience) on either platform.
- **Test vector, PC-98 screenshot (Technopolis 1990-11 p. 12, Pendragon guild, s82):** the fee's first digit is blurred.
  Read as **627** (as `phantasie4-secondary-sources.md` §2 has it), no level 1–30 / Charisma 0–25 / town factor 80, 100,
  120 gives 627 with §3. Read as **827**, §3 gives it at factor 80 (Pendragon's shop prices in the same article are
  0.8 × base) for level 4, Charisma 13 or level 2, Charisma 5. The trainee's level and Charisma are not on screen, so
  this neither confirms nor refutes §3 `[~]`.
- PC-98 `TABL` (disk 1) is byte-identical to the X68000 file (s82), so HP growth, magic level, skill growth and the
  learnable-spell schedule are the same on the manual's platform.- The experience overflow from about level 24–26 and the level-30 case (§4).
