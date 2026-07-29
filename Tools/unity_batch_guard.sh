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
#   H8_GATE_WAIT_SECONDS=600 sh Tools/unity_batch_guard.sh <Method> <log> -quit
#
# It refuses to run and exits non-zero when another editor is live, so a caller chaining
# with && cannot proceed by accident.
#
# Pass `-quit` yourself when the target does NOT call EditorApplication.Exit. This script
# deliberately does not add it: a validator that exits on its own and an authoring tool that
# must stay alive to finish an import need different handling, and guessing wrong either
# truncates the work or leaves an editor holding the lock forever.
#
# H8_GATE_WAIT_SECONDS makes it wait for a busy slot instead of failing immediately. See the
# wait-and-acquire block below for why that is not a loosening of the one-owner rule.

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

# --- optional wait-and-acquire ----------------------------------------------
# H8_GATE_WAIT_SECONDS=N makes gates 1 and 2 POLL for up to N seconds instead of failing on
# the first look. Default 0 keeps the original fail-fast behaviour exactly.
#
# WHY. This is a contended host: several orchestrators drive the same editor, and on
# 2026-07-29 three consecutive legitimate windows were lost to a race, not to a conflict -
# checked the door, found it open, and another owner launched in the seconds before the
# guard ran. Hand-polling from the caller is the wrong mechanism: it burns a round trip per
# attempt and still races. This does NOT weaken the guard. It never runs beside a live
# editor; it waits for one that has genuinely exited. The one-owner rule is enforced by the
# same checks, just at the moment of acquisition rather than at the moment of asking.
WAIT_SECONDS=${H8_GATE_WAIT_SECONDS:-0}

count_editors() { tasklist 2>/dev/null | grep -icE '^Unity\.exe' || true; }
count_compile_owners() { tasklist 2>/dev/null | grep -icE '^(dotnet|csc|msbuild)\.exe' || true; }
sample_cpu() {
	powershell.exe -NoProfile -Command \
		"(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average" \
		2>/dev/null | tr -d '\r' | tail -1
}

# The wait must cover CPU too, not just the editor and compile slots. Measured 2026-07-29:
# the wait acquired the editor after 105 s and was then refused by gate 3 at CPU 60% then
# 100% - because the SAME orchestrator's Blender fan-out was saturating the box. An
# orchestrator's own parallel asset jobs and its own Unity gate compete for one CPU ceiling,
# so a wait that ignores CPU just relocates the failure three gates later, after paying the
# wait. Waiting on all three conditions makes the loop acquire in a genuine lull instead.
if [ "$WAIT_SECONDS" -gt 0 ] 2>/dev/null; then
	WAITED=0
	ANNOUNCED=0
	while [ "$WAITED" -lt "$WAIT_SECONDS" ]; do
		W_ED=$(count_editors)
		W_CO=$(count_compile_owners)
		W_CPU=$(sample_cpu)
		W_CPU=${W_CPU:-0}
		# CPU is sampled TWICE here, matching gate 3 below. Measured 2026-07-29: a single
		# sample let the wait loop exit on a dip and gate 3 then failed at 84% then 94%,
		# burning a 130 s wait for nothing. The wait's exit condition must be at least as
		# strict as the gate it is waiting for, or passing the wait means nothing.
		if [ "$W_ED" = "0" ] && [ "$W_CO" = "0" ] && [ "$W_CPU" -le 50 ] 2>/dev/null; then
			sleep 2
			W_CPU2=$(sample_cpu)
			W_CPU2=${W_CPU2:-0}
			if [ "$W_CPU2" -le 50 ] 2>/dev/null; then
				break
			fi
		fi
		if [ "$ANNOUNCED" = "0" ]; then
			echo "waiting for a clear slot (editors=${W_ED} compile=${W_CO} cpu=${W_CPU}%), up to ${WAIT_SECONDS}s"
			ANNOUNCED=1
		fi
		sleep 5
		WAITED=$((WAITED + 5))
	done
	if [ "$WAITED" -gt 0 ]; then
		echo "waited ${WAITED}s for the slot (editors=$(count_editors) compile=$(count_compile_owners))"
	fi
fi

# --- gate 1: no other editor -------------------------------------------------
LIVE=$(count_editors)
if [ "$LIVE" != "0" ]; then
	echo "BUILD_GATE_BLOCKED: ${LIVE} Unity process(es) already running - another owner holds the editor." >&2
	tasklist 2>/dev/null | grep -iE '^Unity\.exe' >&2 || true
	exit 3
fi

# --- gate 2: no active compile owner ----------------------------------------
# Sampled TWICE, for the same reason gate 3 below is sampled twice, and the reason was
# measured here on 2026-07-29: short-lived `dotnet.exe` probes appear and vanish within
# seconds on this host (one was already gone by the time it was inspected, PID 19516).
# A single reading turned those transients into a hard block and cost two legitimate Unity
# windows in a row while the machine was otherwise idle. A SUSTAINED compile owner - a real
# build - is still caught, because it is present in both samples. That is the distinction
# this gate needs to make: it must block a build, not a blink.
BUSY1=$(count_compile_owners)
if [ "$BUSY1" != "0" ]; then
	sleep 2
	BUSY2=$(count_compile_owners)
	if [ "$BUSY2" != "0" ]; then
		echo "BUILD_GATE_BLOCKED: ${BUSY1} then ${BUSY2} dotnet/csc/msbuild process(es) - a compile owner is active in both samples." >&2
		tasklist 2>/dev/null | grep -iE '^(dotnet|csc|msbuild)\.exe' >&2 || true
		exit 4
	fi
	echo "gate 2: ${BUSY1} compile process(es) in the first sample, 0 in the second - transient, proceeding"
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
