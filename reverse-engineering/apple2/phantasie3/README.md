# Phantasie III (Apple II, SSI 1987 [cr]): program notes

s88. Images: `../original-images/phantasie3/` (4 sides, cracked). The game is **Applesoft BASIC** (plus machine-code
helpers) on SSI's RDOS 3.3 (16-sector; `tools/scripts/a2_rdos.py`). `tools/scripts/detok_apple2_p3.py <side.dsk>
[PROGRAM [first last] | --all] [--offsets]` lists a program by name (offsets file-relative). Sides 1A and 3A (Player)
hold the same town / wilderness programs (`TOWN3`, `OUTDOORS3`, `STARTER`, `CEMETARY3`; 1A adds the `PHANTASIE III`
loader); 1B and 2A (Dungeon) the dungeon ones (`DUNGON3`, `DNGLD3`, `DNGOUT3`, `BACKUP`; 1B boots a `PHANTASIE III`
loader where 2A has `TEST`). The s88 tool re-laid tracks as sectors 15..0, which swaps logical sectors 0 and 15 of
every track: 124 of the 2,150 lines of the distinct programs printed wrong (`STARTER` 12, `TOWN3` 21, `OUTDOORS3` 24,
`DUNGON3` 28, `BACKUP` 23, `DNGLD3` 10, 1B's loader 6; s211). Of the lines cited below, `TOWN3` 2000 / 6190 and `DUNGON3`
1110 / 1120 were among them; all read as described.

| What | Program, lines | Logic `[C]` |
|---|---|---|
| Bank | `TOWN3` 6190–6250 | W)ithdraw per member, typed amount, capped so party gold ≤ 65,000; D)eposit = GOSUB 600 with DD = 0 |
| Divide (shares) | `TOWN3` 600–695 | shares 0–3 per member (0 only if `P(18)` ≠ 1); `TS = .00001 + ΣS`; gold share `GL = gold / TS · (1 − DD)`, XP share `10 · pool / TS`; each account += S·GL via line 2000 (adds, clamps 0…65,535, POKE truncates); party gold := 0 when DD = 0. Town entry calls it with DD = 1 (XP only, line 320); S)ave game calls it if gold > 0 (line 3695) |
| Beg for Mercy | `OUTDOORS3` (wilderness combat) 1110–1130; `DUNGON3` 1110–1130 | success `rnd·100 < 90 + NF` (wilderness) / `< 60 + 3·NF` (dungeon), no monster-id block; `GL = −256·PEEK(SA+1199)` so line 4280 leaves the party gold **mod 256**; `YS = 1` skips the treasure roll (`OUTDOORS3` 4300, `DUNGON3` 4291); XP kept; found items (`SA+1210…1269`) untouched |
| Town entry restock | `TOWN3` 800–890 | `RECALL "TWN3"+TN` loads the town file at 2049 = LA + 1 (LA = 2048: name LA+231…250, price factor LA+227, pool LA+181…226); unless SA+1196 (first entry after start / load): 18 × `stock[rnd·111] = 0`, then 13 × `stock[pool[(1 − rnd·rnd)·L·3]] = 1`, L = SA+1202 capped at 15; **no town test**. The town files (`TWN31`…`TWN317`, 250 B each, written back by S)ave at line 3730; `TF3` = all 17 in order, the template `BACKUP` 500–550 rewrites them from, records 15–17 = TOWN OF LIGHT / DARK / DEATH) all carry the same pool, so towns 15–17 restock too (s98). On 1A / 3A `TWN317` holds an ASMITH record `[?]` |
| Combat round: Parry | `OUTDOORS3` (wilderness combat) 1470–1531 | after "REDO?" (1470) line 1520 `GOSUB 1525` rolls every Parry order (1527: `IN% = 4 − (rnd·100 < Parry OR shield arm hurt)`) before the action loop `FOR SR = 1 TO 5 STEP 2` (1600) (s98) |
| Drop / Purge | `TOWN3` 3500–3520 / 4390–4420 | Drop copies the record back and clears the slot, **no gold**; Purge takes any guild number 1–37 (party members too) and clears party slots 1–6 holding it (slot 0 skipped) (s98, D3-040) |
