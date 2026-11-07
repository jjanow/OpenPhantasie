# Phantasie II (Atari ST): Toss Rock — logic spec

Phase 09 T3, task 3b.7. II's new combat option "7) Toss Rock" (manual p.27) and the TOSS skill behind it, as rules.
Everything not listed here (round order, orders, monster turn, damage application, death, re-rank, rewards) is
Phantasie I's: `reverse-engineering/dos/logic-specs/combat-resolution.md`. Tags as in `docs/plan/RE-REFERENCE.md`:
`[C]` read in `PHANT.PRG`, `[C] live` also seen in Hatari, `[~]` probable, `[?]` open. Addresses are image addresses
(`../FORMAT.md` §1). Live check: `../parity/toss-training-live.md`.

Code: chooser `combat_choose_action 0x0f298` (case 6 at `0x0f74a`), rank prompt `pick_rank 0x0f14c`, resolution
block `0x0772e…0x079aa` inside `combat_round 0x07610`, damage `0x099e0` (I: `h38:0x0cf6` / DOS `0x1597`), skill growth
`level_up 0x0c71a`, creation `0x0c90c` (seed at `0x0cb70`), sheet `0x03a50` (`TOSS %3u`).

## 1. Who may toss, and at what `[C] live`

- **Any party member whose status is OK**, any class, any race, any weapon. No item, no ammunition: unlimited rocks.
  Members asleep or dead get no orders (I's rule).
- **Any rank.** Choosing TOSS always asks "TOSS AT RANK" with one button per rank (unlike melee, where only a THIEF is
  asked and everyone else hits rank 1). Live: a WIZARD got the prompt with 3 ranks, and (s83) with a **single rank** the
  prompt still shows, one button "1" (`../parity/shots/p2-toss-one-rank.png`).
- **Order encoding:** the packed action is **−rank** (1-based; −1 … −n). Any negative order is TOSS.
- **Repeat:** an unchanged order is repeated the next round (I's rule, `combat-resolution.md` §Action chooser) and
  its rank is **clamped to the current rank count**. Live: rank 3 → 2 → 1 as ranks died out ("TOSS AT RANK n" in the
  order list).
- **Menu:** the ST shows the seven actions as GEM buttons THRUST / ATTACK / SLASH / LUNGE / CAST / TOSS / PARRY. TOSS is
  switch case 6, the **7th** action (code order THRUST, ATTACK, SLASH, LUNGE, SPELL, PARRY, TOSS), so the manual's
  "7) Toss Rock" matches. There is **no REDO** action on the ST. The Apple key table on CB p.19 still lists 7 = Redo
  (an Apple question, not the ST's).

## 2. Resolving a toss `[C]` (hit, miss and damage values seen live)

In the member's turn (party order, as I's melee):

```
print "<name> THROWS A ROCK"
r      = rank chosen − 1 (0-based);  g = the monster group in that rank
k      = target slot:                                     # up to 3 tries
         try 3 times: k = trunc( (i + 0.02·rnd) / (N + 2) · (L + 1) )
                      stop at the first k whose monster has HP > 0
         i = the thrower's party index (0-based), N = party size, L = the group's last slot index (count − 1)
no live target after 3 tries  → print "<name> MISSES"; done          # live: a toss at a rank killed earlier this round
hit  if rnd ≤ TOSS (@42)       else print "<name> MISSES"; done
damage = max(0, trunc(12·rnd / 100) − monster armor (+0x14))
       + trunc(rnd · @28 / 100)
apply damage through I's party→monster routine: at least 1 HP; "<monster> TAKES n DAMAGE", death, XP as melee
```

- `rnd` = `rand() / 327` (`0x0fd40`), **0 … 100**; 100 is rare (68 of 32,768 values). The slot formula runs in the
  program's Motorola FFP, in this order: `ftoi(fmul(fdiv(fadd(itof(i), fmul(itof(rnd), 0.02)), itof(N + 2)), itof(L + 1)))`
  (`0x77bc..0x784e`). For N 1..7, rnd 0..100, L + 1 ≤ 15, **58 of 42,420 cases land one slot lower** than exact
  arithmetic (s83, the program's own FFP routines run in a 68000 emulator, `tools/scripts/st_p2_ffp_oracle.py`).
- **RNG order:** one `rnd` per slot try, then the hit roll, then the 12-roll, then the `@28` roll.
- **Hit chance** = P(rnd ≤ TOSS): TOSS 0 → 327/32,768 ≈ 1 %; TOSS s < 100 → 327·(s + 1)/32,768 ≈ (s + 1) %;
  TOSS ≥ 100 → always (when a target is found). No monster defense, no swings, no QUICKNESS, no level term.
  Live: TOSS 255 hit every time a target existed (7 hits seen, 1 more inferred from a kill); TOSS 0 missed 4 of 4.
- **Damage**: 0–12 from the rock, less the monster's armor, plus 0…`@28` from strength, at least 1.
  STRENGTH (which raises `@248`) does not help: the toss reads `@28` itself. Live vs WILD DOG (armor 1): 8, 12, 2, 1, 6, 6
  from throwers with `@28` 2–3 (maximum 13–14).
- **`@28`** is I's "weapon damage A", written only on leaving town (`0x0dd6e`, store `0x0de4c`):
  `@28 = (11·STR + 3·level·|7 − 2·class|) / 66` `[C]` (DOS `save-file-format.md` @28). The class term is 7 THIEF, 5 MONK,
  3 RANGER, 1 FIGHTER, 1 PRIEST, 3 WIZARD, so thieves and monks throw a little harder at equal STR and level. A new or
  transferred character has the `@28` of its last town exit (new: 0) until it leaves town `[~]`.
- **Target spread:** each thrower aims at its own share of the group (front members low slots, rear members high
  ones), with a random shift of up to 2 party places. The formula never picks past the last slot.
- Rock tossing gives **no skill gain on use**; TOSS grows only at level-up (§3).

## 3. The TOSS skill (`@42`) and its bonus (`@62`) `[C] live`

The record slot `@42` (unused in I) holds TOSS; `@62` is its skill bonus. II computes TOSS like I's other nine skills
(`town-menus.md` §Level-up gains), with two differences: its bonus is **recomputed at every level-up** from current
stats, and its class coefficient is (almost) zero.

```
at creation and at every level-up (level_up(L), L = level before the gain; creation runs level_up(0)):
    @62  = 2·L + 4·DEX + STR + 10  (+15 DWARF, +30 GNOME; race = displayed race @256)
    TOSS = max(TOSS, min(255, ((coef[class][L][6] + TOSS) >> 1) + @62 − 49))
```

- `coef[…][6]` is `TWNS.INT` skill-coefficient column 6: **0 everywhere except MONK at L = 9 (14)**.
- **Starting value** (new character, TOSS 0): `max(0, 4·DEX + STR − 39 + race bonus)`; e.g. HUMAN DEX 18 STR 10 → 43.
- Long run at fixed stats: TOSS climbs toward ≈ 2·(@62 − 49).
- A **transferred** character arrives with TOSS 0 and `@62` 0 (I never set them, `transfer.md` §2) and gets the
  formula at its first training. Live: level 0 → 1, TOSS 0 → 77 (GNOME DEX 17 STR 18).
- Checked against the shipped played roster (7 of 7 characters' TOSS and `@62` reproduce) and live (§5).
- Shown on the character sheet as `TOSS %3u` (`0x03a50`) and in the printout (`0x17b44`).

## 4. Versus the manual

Manual p.27: "7) Toss Rock. Rocks may be thrown at any enemy rank. Each character has an unlimited supply of rocks.
Accuracy with this new weapon depends on the Toss Rock skill level of each individual character." All four statements
hold in the code. The manual gives no damage, skill start or growth: those are the code's numbers above
(manual question Q9, `docs/plan/03b-phantasie2-re.md`). No conflict.

## 5. Live check (s78, Hatari + EmuTOS, `../parity/toss-training-live.md`)

Roster `parity/train-data/setR-guild-in.dat` (played roster, TOSS set to 255 / 0), one wilderness fight in region 2
(5 WILD DOGs in 3 ranks): menu buttons, the rank prompt for a wizard, the clamp of repeated ranks, hits and misses by
skill, damage within the formula's range, a toss at an emptied rank missing.

## 6. Open

- Answered s83: the rank prompt with a single rank (live), the slot model (below), the field ids (below).
- What monsters with armor ≥ 12 take (only the `@28` part, then the 1-point floor): not seen live `[~]`.
- Field ids `[C]` (s83): the field map `0x12078` reaches `@42` / `@62` through ids 32 / 33, but no shipped DNG script
  sets or tests them (all eight levels decoded, `../parity/p2-dungeon-events.txt`).
- Slot model `[C]` (s83): every monster death runs `0xb16e` (called from the damage routine at `0x9b20`), which moves the
  living monsters of each rank to the front and recounts, as I's `0x1694`. So the slots are the living monsters: a living
  rank costs one slot draw, an emptied rank three, then the toss misses. Core's model.
- What monsters with armor ≥ 12 take (only the `@28` part, then the 1-point floor): not seen live `[~]`.

## 7. Core (s79)

Built as the feature module `p2.rock-throwing` (`src/OpenPhantasie.Core/RockThrowing.cs`, `P2RockThrowingModule`): on by
default in II, a "from other games" option elsewhere (09 T5). Tests: `Phantasie2RulesTests`.
The numbers are in `GameData/phantasie2/rules.json` `toss_rock` (`RulesProfile.TossRock`). That block is a mirror pinned by
a test to the code's `TossRockRules.PhantasieII`: the module registry builds the module with the code values. Play doesn't
read the JSON yet, so editing it alone changes nothing. Feeding the profile's values into the module set is left for when
II's campaign builds its sessions.

- **Order:** `CombatChoice.Toss`, any OK member, any class, any rank, no roll. Packed as `CombatAction.Toss(rank)` =
  5000 + rank, not the ST's −rank: I's round loop skips every order ≤ 4, and the Client's repeat reads < 10 as PARRY. The
  block is clear of I's melee (10–99), casts (105+) and HEALING (1000–4999).
- **Repeat:** the Client's `CombatText.RepeatFor` keeps TOSS and clamps its rank to the living ranks; the rank prompt
  shows whenever at least one rank lives (single rank: seen live s83).
- **Resolution:** §2 as written. RNG order: slot tries, hit, rock, `@28`; `rnd` = `Next(0, 32768) / 327`. The slot
  formula runs in the ST's FFP (`MotorolaFfp`, ported bit for bit from `PHANT.PRG` and pinned on all 42,420 cases, s83).
  The slots are the rank's **living** monsters in order (`0xb16e` compacts on every death), so a rank emptied earlier in
  the round misses after 3 tries, as seen live.
  Damage goes through the shared monster-damage step as `DamageSource.PartyRock` (1-point floor, death, XP).
- **TOSS skill:** `Character.TossRock` / `TossRockBonus` (`@42` / `@62`), written to the remaster save only when not 0
  and never to I's DOS record. Creation (`level_up(0)`) and every level-up grow it while the module is on
  (`FeatureModuleSet.TossRock` → `LevelUp.Apply`), so I characters get it too when I plays with rocks.
- **Text:** `core.combat.x_throws_a_rock` and `combattext.toss` in `shared/strings/en.json` (the module runs in any game).
- Nothing open in Core (s83: the field ids are unused by the shipped scripts, §6).
