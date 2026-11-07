#!/usr/bin/env bash
# Drive the remaster Client on a virtual X screen (never the user's desktop).
#
#   client_xvfb.sh start  :N [game args…]     Xvfb :N (1920x1080) + metacity + the Client (via play.sh)
#   client_xvfb.sh key    :N KEY…             press each key: focus the window, keydown, hold, keyup
#                                             (xdotool names: space Return Escape 1 a F5 Up …; "sleep:0.5" waits)
#   client_xvfb.sh shot   :N OUT.png          screenshot of the game window
#   client_xvfb.sh frames :N OUTDIR COUNT [INTERVAL_S]
#                                             COUNT screenshots INTERVAL_S apart (default 0.1): for animations
#   client_xvfb.sh audio  :N OUT.wav SECONDS  record the Client's sound for SECONDS (runs in the background: start it,
#                                             then press keys)
#   client_xvfb.sh stop   :N                  stop the Client, metacity and Xvfb on :N (only that display)
#
# Settings and saves go to a throwaway XDG home (CLIENT_HOME, default /tmp/phantasie-client-N) so the real ones stay
# untouched. Log: $CLIENT_HOME/client.log. Sound goes to a PulseAudio null sink of its own (phantasie_xvfb_N, s114),
# never the user's speakers; `audio` records that sink's monitor.
#
# Why each step (s73, s113): MonoGame/SDL polls key state once per frame, so a key is held HOLD seconds (default 0.12)
# instead of xdotool's instant press+release; the window is focused before every key; metacity gives the window a
# normal focus/activate path. Screenshots use xwd -root + ffmpeg crop, because `import -window` gives black frames.
set -euo pipefail

cmd=${1:?usage: client_xvfb.sh start|key|shot|frames|stop :N …}
disp=${2:?display, e.g. :77}
shift 2
[[ $disp =~ ^:[0-9]+$ ]] || { echo "display must look like :77" >&2; exit 1; }
num=${disp#:}
repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
home=${CLIENT_HOME:-/tmp/phantasie-client-$num}
hold=${HOLD:-0.12}
sink=phantasie_xvfb_$num
export DISPLAY=$disp

xvfb_pid() { pgrep -x Xvfb -a | awk -v d="$disp" '$3 == d { print $1 }'; }
client_pid() {
  local p
  for p in $(pgrep -f '[O]penPhantasie\.Client\.dll' || true); do
    tr '\0' '\n' < "/proc/$p/environ" 2>/dev/null | grep -qx "DISPLAY=$disp" && echo "$p"
  done
  [[ -f $home/client.pid ]] && cat "$home/client.pid" || true
}

window() {
  local w i
  for i in $(seq 1 ${WAIT_TICKS:-100}); do
    w=$(xdotool search --onlyvisible --name 'Phantasie' 2>/dev/null | tail -1 || true)
    [[ -n $w ]] && { echo "$w"; return; }
    sleep 0.2
  done
  echo "no Client window on $disp" >&2; exit 1
}

focus() {
  local w=$1
  xdotool windowactivate --sync "$w" >/dev/null 2>&1 || true
  xdotool windowfocus --sync "$w" >/dev/null 2>&1 || true
}

geometry() {  # prints WxH+X+Y of the window's client area
  local w=$1
  eval "$(xdotool getwindowgeometry --shell "$w")"
  echo "${WIDTH}x${HEIGHT}+${X}+${Y}"
}

shoot() {  # window id, out file
  local g w h x y
  g=$(geometry "$1"); IFS='x+' read -r w h x y <<< "$g"
  xwd -root -silent | ffmpeg -loglevel error -y -f xwd_pipe -i - -vf "crop=$w:$h:$x:$y" -frames:v 1 "$2"
}

case $cmd in
  start)
    if [[ -z $(xvfb_pid) ]]; then
      Xvfb "$disp" -screen 0 1920x1080x24 -nolisten tcp >/dev/null 2>&1 &
      for _ in $(seq 1 50); do xdpyinfo >/dev/null 2>&1 && break; sleep 0.1; done
    fi
    # Never fall back to the user's desktop (s118): with no Xvfb, SDL would open the window on the Wayland session.
    xdpyinfo >/dev/null 2>&1 && [[ -n $(xvfb_pid) ]] || { echo "Xvfb $disp is not up; not starting the Client" >&2; exit 1; }
    pgrep -x metacity -a | grep -q -- "--display=$disp" ||
      { CANBERRA_DRIVER=null PULSE_SINK=$sink metacity --replace --display="$disp" >/dev/null 2>&1 & sleep 1; }
    mkdir -p "$home"
    if command -v pactl >/dev/null && ! pactl list short sinks | awk '{print $2}' | grep -qx "$sink"; then
      pactl load-module module-null-sink sink_name="$sink" > "$home/sink.module"
    fi
    # OpenAL Soft would pick PipeWire directly and ignore PULSE_SINK (s114): force its pulse backend.
    env -u WAYLAND_DISPLAY SDL_VIDEODRIVER=x11 ALSOFT_DRIVERS=pulse PULSE_SINK=$sink XDG_DATA_HOME=$home XDG_CONFIG_HOME=$home setsid "$repo/play.sh" "$@" > "$home/client.log" 2>&1 &
    echo $! > "$home/client.pid"
    w=$(WAIT_TICKS=900 window) # up to 3 minutes: play.sh may build first
    sleep 1
    echo "Client window $w on $disp; log $home/client.log"
    ;;
  key)
    w=$(window)
    for k in "$@"; do
      if [[ $k == sleep:* ]]; then sleep "${k#sleep:}"; continue; fi
      focus "$w"
      if [[ $k =~ ^F[0-9]+$ ]]; then
        # s153: xdotool 3.20160805 sends a function key with Alt_L held on this keymap (it picks a level of the key's
        # CTRL+ALT type), and metacity grabs Alt+F2 (its run dialog), so the Client saw F2's release only (s128's "F2
        # did nothing"). Send the bare keycode through XTEST instead (python3-xlib).
        python3 -I - "$disp" "$k" "$hold" <<'PY'
import sys, time
from Xlib import X, XK, display
from Xlib.ext import xtest
d = display.Display(sys.argv[1])
code = d.keysym_to_keycode(XK.string_to_keysym(sys.argv[2]))
xtest.fake_input(d, X.KeyPress, code); d.sync(); time.sleep(float(sys.argv[3]))
xtest.fake_input(d, X.KeyRelease, code); d.sync()
PY
        sleep 0.15
        continue
      fi
      xdotool keydown "$k"; sleep "$hold"; xdotool keyup "$k"; sleep 0.15
    done
    ;;
  shot)
    shoot "$(window)" "${1:?out file}"
    ;;
  frames)
    out=${1:?out dir}; count=${2:?count}; step=${3:-0.1}
    mkdir -p "$out"; w=$(window)
    for i in $(seq -w 1 "$count"); do shoot "$w" "$out/f$i.png"; sleep "$step"; done
    ;;
  audio)
    out=${1:?out.wav}; secs=${2:?seconds}
    timeout "$secs" parec -d "$sink.monitor" --file-format=wav "$out" || true
    ;;
  stop)
    for p in $(client_pid); do pkill -9 -P "$p" 2>/dev/null || true; kill -9 "$p" 2>/dev/null || true; done
    rm -f "$home/client.pid"
    for p in $(pgrep -x metacity -a | awk -v d="--display=$disp" '$0 ~ d { print $1 }'); do kill "$p"; done
    p=$(xvfb_pid)
    if [[ -n $p ]]; then  # wait until it is gone, so a following start never meets a dying server (s118)
      kill "$p"; for _ in $(seq 1 50); do kill -0 "$p" 2>/dev/null || break; sleep 0.1; done
    fi
    if [[ -f $home/sink.module ]]; then pactl unload-module "$(cat "$home/sink.module")" || true; rm -f "$home/sink.module"; fi
    ;;
  *) echo "unknown command $cmd" >&2; exit 1 ;;
esac
