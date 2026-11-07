# Phantasie III (Apple II, SSI 1987 [cr]): program notes

s88. Images: `../original-images/phantasie3/` (4 sides, cracked). The game is **Applesoft BASIC** (plus machine-code
helpers) on SSI's own DOS (no DOS 3.3 catalog). `tools/scripts/detok_apple2_p3.py <side.dsk> [lo hi]` re-lays each track
as sectors 15..0 (files are written in descending sector order) and lists the Applesoft lines; offsets below are in
that re-laid image (track = off // 4096, sector = 15 − (off % 4096) // 256). Lines that cross an out-of-order sector
print as garbage. Sides 1A and 3A (Player) hold the same town / wilderness programs; 1B and 2A (Dungeon) the dungeon ones.

| What | Side, lines (offset) | Logic `[C]` |
|---|---|---|
| Bank | 1A town, 6190–6250 (`0x9120`) | W)ithdraw per member, typed amount, capped so party gold ≤ 65,000; D)eposit = GOSUB 600 with DD = 0 |
| Divide (shares) | 1A town, 600–695 (`0x61d5`) | shares 0–3 per member (0 only if `P(18)` ≠ 1); `TS = .00001 + ΣS`; gold share `GL = gold / TS · (1 − DD)`, XP share `10 · pool / TS`; each account += S·GL via line 2000 (adds, clamps 0…65,535, POKE truncates); party gold := 0 when DD = 0. Town entry calls it with DD = 1 (XP only, line 320); S)ave game calls it if gold > 0 (line 3695) |
| Beg for Mercy | 1A wilderness combat 1110–1130 (`0xb2ce`); 1B dungeon combat (`0x1d0d5`, `0x1ef27`) | success `rnd·100 < 90 + NF` (wilderness) / `< 60 + 3·NF` (dungeon), no monster-id block; `GL = −256·PEEK(SA+1199)` so line 4280 leaves the party gold **mod 256**; `YS = 1` skips the treasure roll (4300); XP kept; found items (`SA+1210…1269`) untouched |
| Town entry restock | 1A town, 800–890 (`0x6500`) | `RECALL "TWN3"+TN` loads the town file at LA = 2048 (name LA+231…250, price factor LA+227, pool LA+181…226); unless SA+1196 (first entry after start / load): 18 × `stock[rnd·111] = 0`, then 13 × `stock[pool[(1 − rnd·rnd)·L·3]] = 1`, L = SA+1202 capped at 15; **no town test**. The town files (one 256-B sector each, 1A / 3A raw `0x15190…`, `0x16754` DEATH, `0x1685a` DARK, `0x16960` LIGHT) all carry the same pool, so towns 15–17 restock too (s98) |
| Combat round: Parry | 1A wilderness combat 1470–1531 (`0xb947`) | after "REDO?" (1470) line 1520 `GOSUB 1525` rolls every Parry order (1527: `IN% = 4 − (rnd·100 < Parry OR shield arm hurt)`) before the action loop `FOR SR = 1 TO 5 STEP 2` (1600) (s98) |
| Drop / Purge | 1A town, 3500–3520 / 4390–4420 (`0x8337` / `0x8695`) | Drop copies the record back and clears the slot, **no gold**; Purge takes any guild number 1–37 (party members too) and clears party slots 1–6 holding it (slot 0 skipped) (s98, D3-040) |
