# OpenPhantasie

OpenPhantasie is a new engine for SSI's *Phantasie* series (Phantasie I, II and III, and the Japanese-only
Phantasie IV), written in C# with MonoGame. The four games run in one interface with one set of options. The rules
were reconstructed from the original programs and manuals, and the faithful behaviour stays the default; quality-of-life
changes are options you can switch on or off. Phantasie IV is played in English: a translation, keyed by the game's
own text ids, is laid over the Japanese text from your copy.

**This repository contains no game files.** No program, text, map, picture or music of the original games is in it.
To play, you need your own copy of each game you want to play. On first start, OpenPhantasie asks for your copy (disk
images, a zip, or the installed game's folder) and builds the game's tables, text, art and sound from it on your
machine.

## Supported copies

| Game | Version | What it gives |
|---|---|---|
| Phantasie I | DOS, SSI 1987 (360K disk image or the game's folder) | the game (tables, text, maps) and the DOS CGA pictures |
| Phantasie I | Amiga v1.0, 1987 (`.adf`) | the Amiga pictures, title music and sounds (needs the DOS copy for the game itself) |
| | Amiga Kickstart 1.3 ROM | the Amiga screen font (optional, for the Amiga look) |
| Phantasie II | Atari ST v1.0 (two `.stx` or `.st` disks) | the game and the Atari ST pictures and music |
| Phantasie II | Apple II v1.0 (two-sided `.dsk` pair, including the Steam / GOG "Phantasie Memorial Set" disks) | the game and the Apple II pictures (a few values still differ from the Atari ST version and are under review) |
| Phantasie III | DOS, SSI 1988, WizardWorks 1991 or the "Phantasie Memorial Set" (disk image or folder) | the game and the DOS CGA pictures |
| Phantasie III | Amiga v1.0, 1987 (`.adf`) | the Amiga pictures, music and sounds (needs the DOS copy for the game itself) |
| Phantasie IV | X68000 Ver.1.02 (two `.dim` disks) | the game, its pictures, music and voices, with the English translation |

Copies are recognised by their content (checksums of the program and data files), not by file or folder names, so
an installed store version can be pointed at directly. A copy that has been played is imported as it is; the import
report says what that changes (for DOS Phantasie I: map regions already explored and fixed fights already won).
Other versions (DOS Phantasie I Bonus Edition, which is also the Memorial Set's I; C64, Apple II Phantasie I / III,
PC-88, PC-98, MSX2, …) are recognised but cannot be imported yet.

## Building and running

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build OpenPhantasie.sln
dotnet run --project src/OpenPhantasie.Client      # or ./play.sh
```

The first start shows the import screen: type or drop the paths of your copies (or pick a Steam / GOG library
shortcut) and import. Imports can be repeated or extended later from Options → General → Import games. A game with
nothing imported stays greyed out in the main menu.

The importer also runs on the command line:

```sh
dotnet run --project src/OpenPhantasie.Importer.Cli -- <path>... [--out DIR] [--list] [--steam]
```

`--list` only shows what was recognised; `--steam` adds the Steam / GOG library folders found on this machine.

Settings, saves and the import live in your user data folder: `~/.local/share/OpenPhantasie` on Linux,
`%LOCALAPPDATA%\OpenPhantasie` on Windows, `~/Library/Application Support/OpenPhantasie` on macOS (the import is in its
`import/` subfolder; delete that folder to start over).

## Tests

```sh
dotnet test OpenPhantasie.sln
```

Tests that need the original games' data are skipped when it is not present, so the suite passes on a fresh clone.

## Layout

```
src/OpenPhantasie.Core/          rules: combat, magic, movement, towns, dungeons (no rendering)
src/OpenPhantasie.Data/          JSON schemas and loaders; GameData/ holds our own data (rules, numbers, layouts, text)
src/OpenPhantasie.Client/        the MonoGame front end
src/OpenPhantasie.Importer/      reads the player's copies (FAT12, AmigaDOS, Human68k, Apple RDOS, Pasti, zip) and builds the import
src/OpenPhantasie.Importer.Cli/  openphantasie-import, the command-line importer
tests/                           xUnit tests
tools/scripts/                   the Python reverse-engineering and extraction tools the importer was ported from
docs/plan/, reverse-engineering/ design notes and behavioural specs of the original programs
DIFFERENCES*.md                  every place where a game's manual and its program disagree, and what OpenPhantasie does
```

The files in `GameData/` that would hold a game's own text carry `...` where that text goes; the importer fills it in
from your copy (`strings/en.sources.json` says where each line is found).

## Licence

OpenPhantasie is released under the MIT licence (`LICENSE`). Third-party packages and their licences are listed in
`THIRD-PARTY-NOTICES.md` (MonoGame is under the MS-PL).

OpenPhantasie is an independent project. *Phantasie* and the original games belong to their respective owners.
