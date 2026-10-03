#!/usr/bin/env bash
# Headless C# compile check for Assembly-CSharp. It does not open the Unity Editor.
#
# How it works: it takes the compiler response file that Unity last generated
# (Library/Bee/artifacts/*/Assembly-CSharp.rsp), keeps its defines, references and
# options, swaps in the CURRENT list of Assets/**/*.cs, and runs Unity's bundled
# Roslyn compiler. The output DLL goes to a temp dir, so Library/ is never touched.
#
# Requirements: Unity has opened this project at least once on this machine (so
# Library/Bee exists), and the matching editor is installed (see UNITY_DIR below).
# Limits: it compiles only Assembly-CSharp (there are no asmdefs), skips source
# generators/analyzers, and does not import assets or generate .meta files.
#
# Usage:  bash tools/compile_check.sh            # prints errors + warning count
#         VERBOSE=1 bash tools/compile_check.sh  # also prints every warning
set -euo pipefail
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"

UNITY_VERSION=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')
UNITY_DIR="${UNITY_DIR:-F:/Unity/$UNITY_VERSION/Editor}"
DOTNET="$UNITY_DIR/Data/NetCoreRuntime/dotnet.exe"
CSC="$UNITY_DIR/Data/DotNetSdkRoslyn/csc.dll"
SRC_RSP=$(ls -t Library/Bee/artifacts/*/Assembly-CSharp.rsp 2>/dev/null | head -1 || true)

[ -f "$DOTNET" ] || { echo "dotnet not found at $DOTNET (set UNITY_DIR)"; exit 2; }
[ -f "$CSC" ]    || { echo "csc.dll not found at $CSC (set UNITY_DIR)"; exit 2; }
[ -n "$SRC_RSP" ] || { echo "No Library/Bee/artifacts/*/Assembly-CSharp.rsp. Open the project in Unity once."; exit 2; }

OUT_DIR="${TMPDIR:-${TEMP:-/tmp}}/dnd_compile_check"
command -v cygpath >/dev/null && OUT_DIR=$(cygpath -m "$OUT_DIR")  # Git Bash: give csc a Windows path
mkdir -p "$OUT_DIR"
RSP="$OUT_DIR/check.rsp"

# Keep every option line except the output, analyzer and additionalfile lines and
# the stale source list.
grep -v -E '^-out:|^-refout:|^-analyzer:|^/additionalfile:|\.cs"?$' "$SRC_RSP" > "$RSP"
echo "-out:\"$OUT_DIR/Assembly-CSharp-check.dll\"" >> "$RSP"
find Assets -name '*.cs' -not -path '*/Editor/*' | sed 's/.*/"&"/' >> "$RSP"

LOG="$OUT_DIR/compile.log"
set +e
"$DOTNET" exec "$CSC" -noconfig "@$RSP" > "$LOG" 2>&1
STATUS=$?
set -e

ERRORS=$(grep -c 'error CS' "$LOG" || true)
WARNINGS=$(grep -c ': warning ' "$LOG" || true)
grep 'error CS' "$LOG" | sort -u || true
[ "${VERBOSE:-0}" = "1" ] && grep ': warning ' "$LOG" | sort -u || true
echo "compile_check: exit=$STATUS errors=$ERRORS warnings=$WARNINGS (full log: $LOG)"
exit $STATUS
