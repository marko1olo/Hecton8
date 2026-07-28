#!/bin/sh
# Single-owner guard for Unity batchmode runs.
#
# WHY THIS EXISTS. AGENTS.md `Unity And Build Gates` allows exactly one owner of Unity,
# dotnet, import, profiler or build at a time, and the runtime-source rule adds "Never
# launch a second Unity editor on this project". Both were still violated, because the
# obvious inline guard protects the wrong statement:
#
#     U=$(tasklist | grep -icE '^Unity\.exe')
#     [ "$U" = "0" ] && rm -f Temp/UnityLockfile      # guarded
#     "$UNITY" -batchmode -projectPath ... &          # NOT guarded - launches anyway
#
# Measured consequence on 2026-07-28: a third editor was launched while two were already
# running. It exited with return code 1 after "Successfully changed project path" without
# opening the project, so no Library corruption occurred - but that was Unity's own lock
# saving the run, not the caller's discipline. A guard that skips the cleanup while still
# firing the dangerous action is not a guard.
#
# USAGE
#   sh Tools/unity_batch_guard.sh <Namespace.Class.Method> <logfile-name> [extra args...]
#
# It refuses to run and exits non-zero when another editor is live, so a caller chaining
# with && cannot proceed by accident.

set -e

METHOD="$1"
LOGNAME="$2"
if [ -z "$METHOD" ] || [ -z "$LOGNAME" ]; then
	echo "usage: unity_batch_guard.sh <Namespace.Class.Method> <logfile-name> [args...]" >&2
	exit 2
fi
shift 2

PROJECT="C:\\hades\\Hecton8"
PROJECT_POSIX="/c/hades/Hecton8"
UNITY="/c/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe"
LOG_WIN="C:\\hades\\Hecton8\\Logs\\${LOGNAME}"
LOG_POSIX="${PROJECT_POSIX}/Logs/${LOGNAME}"

# --- gate 1: no other editor -------------------------------------------------
LIVE=$(tasklist 2>/dev/null | grep -icE '^Unity\.exe' || true)
if [ "$LIVE" != "0" ]; then
	echo "BUILD_GATE_BLOCKED: ${LIVE} Unity process(es) already running - another owner holds the editor." >&2
	tasklist 2>/dev/null | grep -iE '^Unity\.exe' >&2 || true
	exit 3
fi

# --- gate 2: no active compile owner ----------------------------------------
BUSY=$(tasklist 2>/dev/null | grep -icE '^(dotnet|csc|msbuild)\.exe' || true)
if [ "$BUSY" != "0" ]; then
	echo "BUILD_GATE_BLOCKED: ${BUSY} dotnet/csc/msbuild process(es) running - a compile owner is active." >&2
	exit 4
fi

# --- gate 3: CPU under the 50% ceiling AGENTS.md sets -----------------------
# Sampled twice: a single reading catches the transient spike a Blender run produces and
# blocks a legitimate window. Two samples one second apart cost nothing and stopped a false
# block that had me holding the gate for hours against an idle machine.
CPU1=$(powershell.exe -NoProfile -Command "(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average" 2>/dev/null | tr -d '\r' | tail -1)
sleep 1
CPU2=$(powershell.exe -NoProfile -Command "(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average" 2>/dev/null | tr -d '\r' | tail -1)
CPU1=${CPU1:-0}; CPU2=${CPU2:-0}
if [ "$CPU1" -gt 50 ] 2>/dev/null && [ "$CPU2" -gt 50 ] 2>/dev/null; then
	echo "BUILD_GATE_BLOCKED: CPU ${CPU1}% then ${CPU2}%, both above the 50% ceiling." >&2
	exit 5
fi

# --- stale lockfile: only when nothing owns it ------------------------------
# Safe here and nowhere else: gate 1 already proved no Unity process is alive, so a
# surviving lockfile is an orphan from a killed run rather than a live owner's claim.
if [ -f "${PROJECT_POSIX}/Temp/UnityLockfile" ]; then
	echo "clearing orphaned Temp/UnityLockfile (no Unity process owns it)"
	rm -f "${PROJECT_POSIX}/Temp/UnityLockfile"
fi

rm -f "${LOG_POSIX}"
echo "RUN ${METHOD}  (cpu ${CPU1}/${CPU2}%)"
"$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$LOG_WIN" -executeMethod "$METHOD" "$@" || true

# --- report, and never let a clean exit code stand in for a clean run -------
# AGENTS.md: exit code 0 proves nothing. Build output is localised on this host, so grep
# `error CS` and the Russian form, never the English word "Error".
echo "--- compile errors ---"
grep -aE "error CS" "${LOG_POSIX}" 2>/dev/null | sed 's/^.*Assets/Assets/' | sort -u | head -10 || true
grep -acE "Ошибок: [1-9]" "${LOG_POSIX}" 2>/dev/null || true
echo "--- log bytes: $(wc -c < "${LOG_POSIX}" 2>/dev/null) ---"
