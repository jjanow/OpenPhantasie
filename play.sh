#!/usr/bin/env bash
# Launch the remaster. Builds the client only when it is missing or a source file is newer than the last build;
# every other argument goes to the game.
#
#   ./play.sh                      normal start
#   ./play.sh --test-party         six-member level-1 test party in the Guild
#   ./play.sh --test-level 10      the same party trained to level 10 (1..15)
#   ./play.sh --test-party --test-game 2   the test party in Phantasie II (a built game; default I)
#   ./play.sh --test-level 10 --p1-victory   I's test party wins the Black Lord fight (the victory screen)
#   ./play.sh --test-level 5 --p1-combat 12  I's test party in a wilderness fight with monster 12 (the combat screen)
#   ./play.sh --test-level 5 --p1-dungeon 2 6 28   I's test party on DNG2 cell (6, 28) (a dungeon step's boxes)
#   ./play.sh --test-party --test-game 2 --p2-victory   II's test party at Filmon's ceremony (the last member DEAD)
#   ./play.sh --rebuild ...        force a build first
#   ./play.sh --help               this text
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$root/src/OpenPhantasie.Client"
config=Debug
out="$project/bin/$config/net10.0"
dll="$out/OpenPhantasie.Client.dll"
stamp="$out/.play-built" # touched after each build: copied data files don't change the dll's time

rebuild=0
game_args=()
for arg in "$@"; do
    case "$arg" in
        --rebuild) rebuild=1 ;;
        -h|--help) sed -n '2,14p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) game_args+=("$arg") ;;
    esac
done

stale() {
    [[ ! -f "$dll" || ! -f "$stamp" ]] && return 0
    # Any source the client build reads: Client, Core, Data (code, project files, game data, content).
    [[ -n "$(find "$root/src" \( -name bin -o -name obj \) -prune -o -type f -newer "$stamp" -print -quit)" ]]
}

if (( rebuild )) || stale; then
    echo "Building the client..."
    dotnet build "$project" -c "$config" -nologo -v quiet
    touch "$stamp"
fi

cd "$root"
exec dotnet "$dll" "${game_args[@]}"
