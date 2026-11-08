# Phantasie I — WizardWorks Bonus Edition (DOS) vs SSI 1987

Covers the **Bonus Edition Ver 1.0 9/21/90** (the GOG / Steam Memorial Set's I,
`original-images/phantasie1/memorial-set-gog/GAME/PH1/`) and **Ver 1.1 11/12/90**
(`original-images/phantasie1/bonus-v1.1-1991-wizardworks/*.ima`). Read s168.

## Ver 1.0 vs Ver 1.1

Every game file is byte-identical in the two. They differ only in the WizardWorks shell: `VERSION` (date), `WIZ-P.EXE`
(menu, 24283 vs 24411 B), `WIZ-MAIN.BSV`, `GENERAL.DOC`, `LISTING.DOC` and `ORDER.DOC`. The `.ima` has no `PHANTA.DOC`.

## Files vs 1987 (`dos/original-files/`)

- **Same bytes:** every data file: `DNG1`–`10`, `MESS1`–`10`, `OUT*.DAT`, `TWNS.INT` / `.DAT`, `GUILD.DAT`, `SACK.DAT`,
  `DNG.SAV`, `DNGX.SAV`, `SCROLLS.DTX`, `PAT`, `PHM`, `FONT`, `*.IBM` except the cover, `*.PAT`, `M1` / `M2` / `R32768.COM`.
- **Differ:** `PHANTASI.EXE` (instead of `PHANTASI.COM`), `OV1.OVR` (2 bytes), `OV2.OVR` (14 bytes), `IBMCOVER` (5663 → 5273 B).
- **New:** `WIZ-P.EXE`, `WIZ.BAT`, `PH.BAT`, `VERSION`, `GENERAL.DOC`, `LISTING.DOC`, `ORDER.DOC`, `PHANTA.DOC`,
  `W-CROWN.DOC`, `WRATH.DOC`, `VIEWIT.COM` and `WIZ-MAIN.BSV` (the WizardWorks menu and its order / info texts).

## PHANTASI.EXE

- Packing: **LZEXE 0.90** (`LZ09` at 0x1c) outside, Microsoft **EXEPACK** inside. In the LZEXE-unpacked image (34049 B),
  the inner entry is CS:IP 0x83b:0x10, with the EXEPACK header (`RB`) at image 0x83b0. After EXEPACK the image is
  64576 B, the same layout as the 1987 COM's (DS = 0xc9f paragraphs).
- The unpacked image equals 1987's byte for byte except **DS:0x3094**: 1 in 1987, 0 in the Bonus Edition.
- Unpacker: `tools/scripts/re_common.py` `unpack_phantasi_exe` / `unpack_phantasi` (Python) and the importer's
  `ComImage` (C#).

## What changed

- **`[0x3094]`: hidden save files.** The root's file create at 0x34c8 (INT 21h AH=3Ch) passes attribute 0x22
  (hidden + archive) when `[0x3094]` ≠ 0, and 0x20 otherwise. OV1's save routine (0x501d) clears the flag on entry.
  In 1987 it sets the flag back to 1 at both exits (file 0x503b and 0x50e6); the Bonus Edition writes 0 there. So the
  Bonus Edition always creates its files as plain archive files, while 1987 hides everything it creates outside the
  save routine (initial value 1). Nothing the remaster models depends on this.
- **OV2 0x7ea8–0x7ede: manual page references** in the questions that ask for a word or spell from the manual (the
  copy-protection check `[~]`: the code that prints them was not traced). The word question's pages went from 40–41 to
  15–16 and the spell question's from page 32 to the back cover, for WizardWorks' combined Phantasie & Phantasie III manual. The remaster has no
  such check and uses none of these strings.
- **IBMCOVER**: the cover picture shown at start, replaced by WizardWorks' own. Not decoded.

## Consequence for the importer

None of the program tables (monsters DS:0x1a6c, sprite sizes 0x2c3e, item values 0xd54, name pointers 0xec0 /
0x167e / 0x16b2 / 0x174d), OV2's fog row (0x7e2e) and glyph table (0x7d5d), or OV1's ghost (0x5a23) is touched. Every
data file the 1987 pipeline reads is identical. So the 1987 pipeline applies once the EXE is unpacked.
