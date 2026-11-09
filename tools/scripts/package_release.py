#!/usr/bin/env python3
"""Build a player download of OpenPhantasie for one platform (s174; used by the CI workflow, runs locally too).

    python3 tools/scripts/package_release.py --rid linux-x64 --version 1.0.0 [--out dist] [--builtin-gamedata]

Publishes the Client self-contained (no .NET install needed) for the runtime id (`win-x64`, `linux-x64`, `osx-x64`,
`osx-arm64`) and packs it as the archive the self-updater looks for (`AppVersion.PlatformId`, `UpdateService.IsAssetFor`):

- Windows: `OpenPhantasie-<version>-win-x64.zip`, a folder `OpenPhantasie/` with `OpenPhantasie.exe`;
- Linux:   `OpenPhantasie-<version>-linux-x64.tar.gz`, a folder `OpenPhantasie/` with `OpenPhantasie` (executable);
- macOS:   `OpenPhantasie-<version>-osx-<arch>.tar.gz`, an `OpenPhantasie.app` bundle (program in `Contents/MacOS/`),
           signed ad hoc with `codesign` when run on a Mac (Apple silicon refuses unsigned programs).

The build takes no game data of its own (`OpenPhantasieBuiltInGameData=false`): players import their copies. Pass
`--builtin-gamedata` only for a private test build (it holds the private repo's GameData; never publish it).
Prints the archive's path. The unpacked build stays in `<out>/work-<rid>/` (the script deletes nothing; `--out` must not
hold a `work-<rid>` folder yet).
"""
from __future__ import annotations

import argparse
import os
import platform
import shutil
import subprocess
import sys
import tarfile
import zipfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
CLIENT = REPO / "src" / "OpenPhantasie.Client" / "OpenPhantasie.Client.csproj"
RIDS = {"win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"}
DOCS = ["LICENSE", "THIRD-PARTY-NOTICES.md", "README.md"]

INFO_PLIST = """<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>OpenPhantasie</string>
  <key>CFBundleDisplayName</key><string>OpenPhantasie</string>
  <key>CFBundleIdentifier</key><string>io.github.jjanow.openphantasie</string>
  <key>CFBundleExecutable</key><string>OpenPhantasie</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>{version}</string>
  <key>CFBundleVersion</key><string>{version}</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
"""


def run(cmd: list[str]) -> None:
    print("+", " ".join(cmd), flush=True)
    subprocess.run(cmd, check=True)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--rid", required=True, choices=sorted(RIDS))
    ap.add_argument("--version", required=True, help="1.2.3 for a release; anything with a '-' never self-updates")
    ap.add_argument("--out", default="dist")
    ap.add_argument("--repository", default="jjanow/OpenPhantasie", help="owner/name the updater reads releases from")
    ap.add_argument("--builtin-gamedata", action="store_true", help="private test build only")
    args = ap.parse_args()

    out = Path(args.out).resolve()
    work = out / f"work-{args.rid}"
    if work.exists():
        raise SystemExit(f"{work} exists: pick an empty --out (this script never deletes)")
    mac = args.rid.startswith("osx")
    win = args.rid.startswith("win")
    root_name = "OpenPhantasie.app" if mac else "OpenPhantasie"
    publish = work / root_name / "Contents" / "MacOS" if mac else work / root_name

    run(["dotnet", "publish", str(CLIENT), "-c", "Release", "-r", args.rid, "--self-contained", "true",
         f"-p:Version={args.version}", f"-p:UpdateRepository={args.repository}",
         f"-p:OpenPhantasieBuiltInGameData={'true' if args.builtin_gamedata else 'false'}",
         "-p:DebugType=none", "-o", str(publish)])

    docs_root = publish if not mac else work / root_name / "Contents" / "Resources"
    docs_root.mkdir(parents=True, exist_ok=True)
    for name in DOCS:
        src = REPO / name
        if not src.exists():
            src = REPO / "tools" / "public" / name
        if src.exists():
            shutil.copy2(src, docs_root / name)

    if mac:
        (work / root_name / "Contents" / "Info.plist").write_text(INFO_PLIST.format(version=args.version.split("-")[0]),
                                                                 encoding="utf-8")
        if platform.system() == "Darwin":
            run(["codesign", "--force", "--deep", "--sign", "-", str(work / root_name)])
        else:
            print("note: not on a Mac, the bundle is not signed (Apple silicon will refuse it)", file=sys.stderr)
    if not win:
        program = publish / "OpenPhantasie"
        program.chmod(program.stat().st_mode | 0o111)

    out.mkdir(parents=True, exist_ok=True)
    stem = f"OpenPhantasie-{args.version}-{args.rid}"
    if win:
        archive = out / f"{stem}.zip"
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
            for f in sorted((work / root_name).rglob("*")):
                z.write(f, f.relative_to(work).as_posix())
    else:
        archive = out / f"{stem}.tar.gz"

        def clean(info: tarfile.TarInfo) -> tarfile.TarInfo:
            info.uid = info.gid = 0
            info.uname = info.gname = ""
            return info

        with tarfile.open(archive, "w:gz") as t:
            t.add(work / root_name, arcname=root_name, filter=clean)
    print(archive)
    return 0


if __name__ == "__main__":
    sys.exit(main())
