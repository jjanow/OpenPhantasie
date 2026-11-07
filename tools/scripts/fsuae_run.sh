#!/usr/bin/env bash
# Boot an Amiga disk in FS-UAE on a virtual X screen (never the user's desktop).
#
#   fsuae_run.sh WORKDIR DISPLAY_NUM ADF [extra "key = value" config lines ...]
#
# Machine: A500, Kickstart 1.3 (rev 34.5), 512 K chip + 512 K slow, PAL. The ADF is copied into WORKDIR (writable
# copy, so saves made in the run stay there and the original image is never touched). The config is written to
# WORKDIR/run.fs-uae; the log goes to WORKDIR/fsuae.log. KICKSTART overrides the ROM path
# (default: the repo's reverse-engineering/amiga/original-images/kickstart/kick34005.A500, SHA-1 891e9a54…cb785).
# FS-UAE's own screenshots (F12+S; fsuae_drive.py ashot) go to WORKDIR/screenshots, never the user's Desktop.
# Sound: only to a PulseAudio null sink. Set PULSE_SINK to one that exists (e.g. agents_mute) to hear / record it
# (parec --monitor-stream=<FS-UAE's sink input>); without one the emulator's sound is off ("uae_sound_output = none", "audio_driver = null").
# DEBUG_FIFO=SECONDS (s140): stdin from the FIFO WORKDIR/dbg.in for the console debugger (add "console_debugger = 1";
# F12+D or a breakpoint enters it; fsuae_bp_watch.py answers the stops), held open that long.
set -euo pipefail
work=$(realpath "$1"); disp=$2; adf=$3; shift 3
repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
rom=${KICKSTART:-$repo/reverse-engineering/amiga/original-images/kickstart/kick34005.A500}
[[ -f $rom ]] || { echo "Kickstart ROM not found: $rom" >&2; exit 1; }
mkdir -p "$work" "$work/screenshots"
cp -f "$adf" "$work/df0.adf"
chmod u+w "$work/df0.adf"
{
  echo "[fs-uae]"
  echo "amiga_model = A500"
  echo "kickstart_file = $rom"
  echo "floppy_drive_0 = $work/df0.adf"
  echo "writable_floppy_images = 1"
  echo "chip_memory = 512"
  echo "slow_memory = 512"
  echo "fullscreen = 0"
  echo "window_width = 960"
  echo "window_height = 600"
  echo "base_dir = $work/fs-uae"
  echo "screenshots_output_dir = $work/screenshots"
  if [[ -z ${PULSE_SINK:-} ]] || ! pactl list short sinks 2>/dev/null | awk '{print $2}' | grep -qx "$PULSE_SINK"; then
    echo "uae_sound_output = none"; echo "audio_driver = null"
  fi
  for line in "$@"; do echo "$line"; done
} >"$work/run.fs-uae"
if ! xdpyinfo -display ":$disp" >/dev/null 2>&1; then
  Xvfb ":$disp" -screen 0 1920x1080x24 >/dev/null 2>&1 &
  sleep 1
fi
xdpyinfo -display ":$disp" >/dev/null 2>&1 || { echo "Xvfb :$disp is not up; not starting FS-UAE (never the desktop)" >&2; exit 1; }
cd "$work"
if [[ -n ${DEBUG_FIFO:-} ]]; then
  # s140: the console debugger reads stdin; feed it from the FIFO WORKDIR/dbg.in (pass "console_debugger = 1"). A
  # writer holds the FIFO open for DEBUG_FIFO seconds; the debugger's output goes to fsuae.log unbuffered.
  rm -f "$work/dbg.in"; mkfifo "$work/dbg.in"
  nohup sleep "$DEBUG_FIFO" >"$work/dbg.in" 2>/dev/null &
  echo $! >"$work/dbg.holder.pid"
  env -u WAYLAND_DISPLAY SDL_VIDEODRIVER=x11 ALSOFT_DRIVERS=pulse DISPLAY=":$disp" nohup stdbuf -o0 fs-uae "$work/run.fs-uae" <"$work/dbg.in" >"$work/fsuae.log" 2>&1 &
else
  env -u WAYLAND_DISPLAY SDL_VIDEODRIVER=x11 ALSOFT_DRIVERS=pulse DISPLAY=":$disp" nohup fs-uae "$work/run.fs-uae" >"$work/fsuae.log" 2>&1 &
fi
echo "fs-uae pid $! on :$disp (config $work/run.fs-uae)"
