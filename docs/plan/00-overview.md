# Phantasie I Remaster — Project Plan (Overview)

> **Historical overview** of the phase plan. Live state and open items: `PLAN.md`; ground rules: `AGENTS.md`.

## Goal
Reverse-engineer the DOS release of Phantasie I into a faithful behavioral spec, reimplement it as a clean C# / .NET
engine with extracted Amiga graphics, then add QoL features, built so II, III and IV can be added without a rewrite.
The project now covers all four games in one interface (Phase 9).

## Why this document set is split up
Each numbered file is a self-contained work package (inputs, outputs, tools, done-criteria), so a session loads only the
one it works on. Which cold file to read for what: `PLAN.md`'s header table.

## Phase list

| # | File | Produces |
|---|------|----------|
| 1 | `01-legal-and-sourcing.md` | Legit DOS + Amiga disk/executable images on disk |
| 2 | `02-toolchain-setup.md` | All RE + dev tools installed on Kubuntu |
| 3 | `03-dos-disassembly-and-logic-extraction.md` | Ghidra project + plain-English/pseudocode behavioral spec of game logic |
| 3b–3d | `03b-phantasie2-re.md`, `03c-phantasie3-re.md`, `03d-phantasie4-re.md` | II (Atari ST), III (DOS + Amiga) and IV (X68000) RE, run under Phase 9 |
| 4 | `04-amiga-graphics-extraction.md` | PNG spritesheets/tilesets + asset manifest, cross-referenced to game data IDs |
| 5 | `05-engine-architecture-csharp.md` | Repo skeleton: Core/Data/Client project split, data schemas |
| 6 | `06-implementation-roadmap.md` | Milestone-by-milestone build order (M0–M8) |
| 7 | `07-qol-backlog.md` | Post-parity QOL feature list, prioritized |
| 8 | `08-parity-closeout.md` | DOS 1:1 parity closeout + inventory QoL (equip/unequip without party re-equip) |
| 9 | `09-trilogy.md` | Phantasie I–IV in one game: cross-game options, party transfer, per-game platform graphics |
| 9 (detail) | `09-trilogy-matrix.md`, `09-trilogy-manual-p2.md` / `-p3.md`, `09-trilogy-iv-parentage.md`, `09-startup-options.md` | Rules / platform matrix, manual citations, IV's ancestors, main menu and Options |
| 10 | `10-importer.md` | First-run importer (active since s158) |
| 11–16 | `11-guild-treasury.md` … `16-reset-game.md` | Later QoL / series designs (list in `PLAN.md`'s header table) |

Repo layout: see `README.md`. Live status: `PLAN.md`. RE conventions and tooling: `RE-REFERENCE.md`.

## Ground rules for the whole project
The live list is in `AGENTS.md` (spec before code, data-driven, Core without rendering dependencies, manual as source of
truth). Each phase also had done-criteria that had to pass before the next numbered file started.

## Status
Phases 1–7 are closed; Phase 8 (1:1 DOS parity) is signed off and its QoL package (P3) is open. Phase 9 (the series)
runs alongside it. Open items live in `PLAN.md` only.

Platform decisions: Amiga graphics for I and III; II's rules and art come from the Atari ST; IV is Japanese-only
(X68000 / MSX2 / PC-98), see `09-trilogy-matrix.md`.
