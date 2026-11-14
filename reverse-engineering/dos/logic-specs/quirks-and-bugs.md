# Behavioral Quirks & Bugs — with preserve/fix decisions

Each entry records the behavior, its status, and a **preserve vs. fix** call for the remaster.
Confidence tags as in `README.md`.

## Q1 — Gold silently truncated at the 16-bit cap [X]
**Behavior:** bank balances and the carried party pool are 16-bit; anything over
**65,535** is **silently discarded** (walkthrough + manual "up to 65,000 GP";
Scorpia flagged the money-handling in Phantasie II, and Phantasie I shares it).
The Atari **ST** re-release later raised the cap to 1,000,000,000, i.e. SSI
themselves treated it as a fix-worthy limitation.
**Impact:** late-game gold vanishes with no warning.
**Decision (D-048, user: manual):** gold is capped at the manual's **65,000** (purse and each account) and the excess is
lost. Option `p1.gold-cap`: `dos` = DOS's 65,535 cap, `lifted` = no cap (see the deviations section).
**DOS sites [C]:** clamp helper COM `0x2e5d` (OV1 near `0xd791`; 32-bit → `min(x, 0xffff)`). Bank Withdraw (OV1
`0x29c4..0x29e6`) subtracts the full amount from `@262` and then clamps the purse → the excess is destroyed; the share
split (OV1 `0x1780`) clamps each `@262`. Manual's cap is 65,000, DOS's 65,535 — see `town-menus.md` §Bank.

## Q2 — Gold lost to round-down on share distribution [X]
**Behavior:** when the XP/gold pool is split into shares on town entry (not at the Inn; shares 1/2/3 per member),
each share is rounded down; the remainder is lost ("you'll lose some gold in
rounding down"). Same for XP shares.
**Impact:** minor, systematic leakage of gold/XP.
**Recommendation:** **FIX (carry remainder)** — distribute the remainder to one
member (or the pool) instead of discarding. Preserve-authentic optional.
**Status:** **APPLIED** — `TownMenus.Distribute` gives XP/gold remainder to
the first living survivor. **User (D-037):** keep it as is.

## Q3 — "Create → pool → purge" starting-gold exploit [X]
**Behavior:** create characters (each gets **256 GP** in the bank), withdraw &
pool their gold, then purge them; repeat to bankroll a real party. Standard
emergent mechanic, not a code bug, but it exists because starting gold is
unconditional and purge doesn't reclaim withdrawn gold.
**Recommendation:** **PRESERVE** — it's a beloved bit of cheese and harmless;
document it. (A QoL alternative: a higher, one-time starting stipend.)

## Q4 — Duplicate strong-race roll suppression [C]
**Behavior (CONFIRMED on DOS):** the DOS creation routine **does** have the
strong-race duplicate guard for the **RANDOM** race choice. Disassembly of the
RANDOM branch (`OV1.OVR` `0x0C28`–`0x0C7C`) reads exactly:

```
if raceChoice == 5 (RANDOM):
    race = rand / 3277 + 5              # 0x0C2D — strong races PIXIE..MINOTAUR (5..14)
    for i in 0..21:                      # scan all 22 roster slots
        rec = *(DS:0x63e0) + i*0x11a
        if rec.@92 == 0: continue        # 0x0C59 — empty slot marker
        if rec.@256 == race: break       # 0x0C5F — race echo matches candidate
    if a duplicate was found:
        race = rand / 6554               # 0x0C78 — reroll to a WEAK race HUMAN..GNOME (0..4)
```

So a RANDOM roll that lands on a strong race already present in the guild is
silently demoted to a common race; the classic workaround is add-then-purge the
existing strong-race member. (Explicitly-chosen races are unaffected — the guard
only gates the RANDOM path.)

**Recommendation:** **PRESERVE.** This is authentic DOS anti-cheese, cheap to
reproduce, and matches `character-creation.md` §"RANDOM race". If a future
authentic-mode toggle is added, an "allow duplicate strong races" option could
live there, but the default stays DOS-faithful (suppress). Marked **[C]**.

## Q5 — Item / treasure re-duplication on dungeon reset [X]
**Behavior:** "special items can be re-duplicated as you pass through again" —
re-entering/resetting a dungeon respawns fixed treasure because `DNG.SAV`
"taken" flags reset (or aren't consulted) on reset.
**Impact:** farmable unique items.
**Recommendation:** **PRESERVE with optional FIX.** Verify how `DNG.SAV`
persistence + dungeon reset interact before deciding (`dungeon-representation.md`).

## Q6 — "Bug that prevents winning" (Apple II rumor) [C static, s31]
**Behavior:** the walkthrough mentions hearsay of an Apple II version bug that
blocks completion; no specifics, and it's platform-specific.
**DOS (`endgame.md` section 6):** no unwinnable defect found. The win test (BLACK LORD killed → state 7) is sound,
and the rune chain is complete. There are three **soft-locks** from one-shot dungeon events: Zeus spent before
qualifying, gems taken instead of destroyed, Black Lord fight escaped. Each lasts until DNG9/DNG10 is reloaded
from the original file. Single-slot `DNG.SAV` / `DNGX.SAV`; the writer is untraced.
**Recommendation:** **PRESERVE** (faithful). A post-1:1 QoL option could re-arm the one-shot quest events.

## Q7 — Age / death from aging [C] (s11)
**Behavior:** decoded — aging runs on **town entry** (OV1 `0x1954`), not at the Inn.
Past 70 % of the lifespan `@252` a character loses 1/5 of every attribute once
("%s has aged"); past 100 % they die and all attributes are zeroed ("%s has died").
Full rule: `town-menus.md` §Aging. Lifespans differ from the manual → `DIFFERENCES.md` D-003.
**Quirk:** the travel-time counter is reset on every town entry after `(time>>8)/7`
is added, so trips under 1792 time units age nobody — town-hopping slows aging.
**Recommendation:** **PRESERVE**; the race lifespan factors are data, so D-003 can be applied either way.

## Q8 — Guild → Add ages every member it adds, same town too [C] (s49)
**Behavior:** OV1 `0x2835..0x2851` asks "Not in the town / [original text] / to get here" (COM menu 9) only for a
member whose `@254` is another town, but adds 4 to `@264` (= 2 months) on **every** confirmed Add — the same-town path
jumps past the prompt straight to the `+4`. Fixtures: re-added members gain +4 standing in Pelnor (`town-menus.md` §Aging).
**Impact:** each party reshuffle ages every re-added member 2 months; negligible against lifespans of decades.
**Decision (D-067, user):** **FIX** — manual silent, user: don't age a same-town Add. An Add from another town keeps
the gate and the +4. Core: `GuildHall.TryAddToParty`. Listed below as an intentional deviation.

## Intentional deviations from DOS (P1.3, s49)

What the remaster does **on purpose** differently from both the manual and DOS. Rules the user decided for the manual
over DOS are not deviations; they are listed in `DIFFERENCES.md` (status table), not here.

| Id | Behaviour | DOS | Remaster | Why / where |
|---|---|---|---|---|
| Q2 | Town-entry payout remainder | share split (1/2/3 per member), remainders dropped (OV1 `0x167d`) | equal split among the living, XP and gold remainders to the first survivor (`TownMenus.Distribute`) | **FIX** — kept by the user as part of Core's payout (D-037) |
| Q8 | Guild Add in the member's own town | `@264 += 4` on every Add (OV1 `0x284c`) | +4 only for an Add from another town (`GuildHall.TryAddToParty`) | **FIX** — manual silent, user (D-067) |

**Q1** is not a deviation: gold follows the manual's 65,000 cap (D-048). Rules options (P3, locked per save): `p1.gold-cap` = `dos` plays Q1 as DOS (65,535, the excess lost) and
`lifted` lifts the cap; `p1.payout-remainder` = `dropped` plays Q2 as DOS (the payout remainders are lost; Original (DOS)
preset), `first-survivor` is the fix above.

## Notes for later
- The Q1/Q2 sites are pinned (above). Not yet checked: sign-handling bugs in the 16-bit gold math of
  `OV1.OVR`/`OV2.OVR` deposit/withdraw/distribute (a classic source of "negative gold" glitches).
