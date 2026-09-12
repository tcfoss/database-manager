#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"

max=-1
for f in "$SCRIPT_DIR"/../resharper-inspect-results/ResharperInspectResults*.sarif; do
    if [[ "$f" =~ ^.*/ResharperInspectResults([0-9]+)\.sarif$ ]]; then
        n="${BASH_REMATCH[1]}"
        if (( n > max )); then
            max=$n
        fi
    fi
done

if (( max == -1 )); then
    next=0
else
    next=$(( max + 1 ))
fi

echo "ResharperInspectResults${next}.sarif"

jb inspectcode "$SCRIPT_DIR"/../DatabaseManager.slnx -o="$SCRIPT_DIR"/../resharper-inspect-results/ResharperInspectResults${next}.sarif
