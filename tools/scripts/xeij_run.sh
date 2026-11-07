#!/usr/bin/env bash
# Boot an X68000 disk set in XEiJ on a virtual X screen (never the user's desktop).
#
#   xeij_run.sh WORKDIR DISPLAY_NUM DISK1 [DISK2] [extra XEiJ -key=value ...]
#
# WORKDIR gets the copied disks, XEiJ.ini, the control pipe (tmp/XEiJControl) and GIF captures (capture/).
# The machine is the original X68000 (model Shodai: MC68000 at 10 MHz, IPL ROM 1.0). F12 starts a GIF recording
# (one GIF frame per emulated vsync, so frame counts are emulated time). Settings are not saved on exit.
# XEIJ_HOME overrides the emulator folder (default ~/emulators/XEiJ-0.26.10.08); XEIJ_JAVA the java binary
# (XEiJ 0.26 needs Java 26: default ~/emulators/jdk-26.0.2.1+1/bin/java, an Adoptium JDK 26 unpacked there).
set -euo pipefail
work=$(realpath "$1"); disp=$2; d1=$3; shift 3
d2=""
if [[ $# -gt 0 && $1 != -* ]]; then d2=$1; shift; fi
xeij=${XEIJ_HOME:-$HOME/emulators/XEiJ-0.26.10.08}
java=${XEIJ_JAVA:-$HOME/emulators/jdk-26.0.2.1+1/bin/java}
mkdir -p "$work/tmp" "$work/capture"
cp -f "$d1" "$work/d1.dim"
[[ -n $d2 ]] && cp -f "$d2" "$work/d2.dim"
if ! xdpyinfo -display ":$disp" >/dev/null 2>&1; then
  Xvfb ":$disp" -screen 0 1600x1200x24 >/dev/null 2>&1 &
  sleep 1
fi
xdpyinfo -display ":$disp" >/dev/null 2>&1 || { echo "Xvfb :$disp is not up; not starting XEiJ (never the desktop)" >&2; exit 1; }
cd "$work"
args=(-ini="$work/XEiJ.ini" -saveonexit=off -lang=en -model=Shodai -boot=fd0 -fd0="$work/d1.dim"
      -f12key=gifanimation -gifrecordingtime=10 -gifinterpolation=nearest -gifmagnification=100 -pastepipe=on
      -iplrom16=off -flrenabled=off -flrinformed=on)
[[ -n $d2 ]] && args+=(-fd1="$work/d2.dim")
env -u WAYLAND_DISPLAY DISPLAY=":$disp" nohup "$java" -Xmx6g -Djava.io.tmpdir="$work/tmp" -Duser.home="$work" -jar "$xeij/XEiJ.jar" \
  "${args[@]}" "$@" >"$work/xeij.log" 2>&1 &
echo "XEiJ pid $! on :$disp, work dir $work"
