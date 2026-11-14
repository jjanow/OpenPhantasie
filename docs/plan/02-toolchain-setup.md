# Phase 2 — Toolchain Setup (Kubuntu, all native, no Wine/Proton)

> **Closed (historical).** The setup below is the original Phase 2 plan. Current tooling differs: the projects target
> .NET 10, and the emulators in use are DOSBox-X, FS-UAE, Hatari and XEiJ (`RE-REFERENCE.md`). Live state: `PLAN.md`.

**Goal:** every tool needed for Phases 3–7 installed and verified working.

## Reverse engineering
```bash
# Ghidra (Java-based, native on Linux)
sudo snap install ghidra --classic
# or manual install if you want a specific version:
#   download zip from github.com/NationalSecurityAgency/ghidra/releases
#   extract to /opt/ghidra, add to PATH, requires JDK 17+ (sudo apt install openjdk-17-jdk)

# DOSBox — run/verify the DOS version, and useful for behavioral ground-truth
# while building the logic spec
sudo apt install dosbox

# amitools — Amiga disk image handling (xdftool) + related utilities
pip install --user amitools

# netpbm / ImageMagick — image format conversion (IFF ILBM -> PNG etc.)
sudo apt install netpbm imagemagick

# capstone — disassembly library, useful for scripting/automation alongside Ghidra
pip install --user capstone

# strings, xxd, hexdump — already in binutils/bsdmainutils, confirm present
sudo apt install binutils bsdmainutils
```

## Optional but useful
```bash
# VICE — only needed if you ever want to cross-check against the C64 version
sudo apt install vice

# extract-adf — fallback for damaged Amiga images (amitools' xdftool is primary)
git clone https://github.com/mist64/extract-adf && cd extract-adf && make
```

## Development
```bash
# .NET SDK (for the C# engine)
sudo apt install dotnet-sdk-8.0
dotnet --version   # confirm

# MonoGame templates (cross-platform 2D framework — this is your rendering/
# input/audio layer; game logic itself stays framework-agnostic per Phase 5)
dotnet new install MonoGame.Templates.CSharp
```

## Claude Code
Installed per Anthropic's current docs; confirmed in the terminal and the VS Code extension before Phase 3.

## Verification checklist
- [x] Ghidra available via snap (`ghidra` / `ghidra.analyzeHeadless`);
      `ghidra_12.1_PUBLIC` installed — GUI window not re-confirmed in Phase 8,
      but headless entrypoint responds; project lives under
      `reverse-engineering/dos/ghidra/`
- [x] DOSBox boots / mounts Phase 1 DOS files — `dosbox` 0.74-3 installed;
      Phase 8 smoke: mount `original-files`, `dir PHANTASI.COM`, exit OK
      (full interactive playthrough already used for passes 1–4)
- [x] `xdftool <your>.adf list` shows the Amiga disk's file tree — verified
      against `reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf`
- [x] `dotnet new mgdesktopgl -n SanityCheck` scaffolds and `dotnet run`s a
      blank MonoGame window — scaffold, build, and `dotnet run` all succeeded
      (net9.0); window launched in this session for visual confirmation
- [x] Claude Code responds inside this repo from both terminal and VS Code —
      running in this session (v2.1.196)

## Done-criteria for Phase 2
All boxes above checked, with Ghidra and DOSBox confirmed against the Phase 1 files.
