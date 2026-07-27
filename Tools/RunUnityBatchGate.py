#!/usr/bin/env python3
r"""Run a Unity batchmode method behind the AGENTS.md process preflight, or refuse.

Evidence class: none. This is a launcher. It produces no proof of its own; it only
decides whether a heavy Unity action is allowed to start, and reports the blocker
honestly when it is not.

WHY THIS EXISTS
    Six lanes share one working tree and one Unity project. Two agents cannot open
    it at once: the second aborts with "another Unity instance is running with this
    project open", having already paid ~40s of editor startup. Five launch attempts
    in one cycle died that way, and the ad-hoc retry loops written to work around it
    poll indefinitely - which `AGENTS.md` Unity And Build Gates explicitly forbids:

        "Heavy proof actions must back off after a blocked preflight. Wait for load
         to clear or stop with the exact blocker; do not retry in a tight loop.
         After two blocked attempts over unchanged state, report the blocker instead
         of polling."

    So the retry budget here is deliberately small and the refusal is a first-class
    outcome, printed as BUILD_GATE_BLOCKED with the reason.

PREFLIGHT, per AGENTS.md
    - no other Unity.exe holding the project
    - CPU load at or below --max-cpu (default 50, the figure the rule names)
    - no active dotnet/csc/msbuild compile owner unless --allow-compile-owner
    - a stale Temp/UnityLockfile left by a killed run is removed only when no Unity
      process is alive, never while one is

WHAT IT DOES NOT DO
    It does not judge the run. Exit code 0 means Unity exited 0, not that the probe
    inside it proved anything - read the log. It also cannot tell a Unity that is
    importing from one that is idle-with-the-project-open; any live Unity blocks.

USAGE
    python Tools/RunUnityBatchGate.py --method Hecton8.EditorTools.Diagnostics.H8_SceneCompositionCensus.Run \
        --log Logs/census.log --quit
    python Tools/RunUnityBatchGate.py --method ...H8_HeadlessPlayModeProbe.Run \
        --log Logs/playprobe.log --timeout 1620 -- -h8Scene Assets/_Project/Scenes/00_BOOTSTRAP.unity \
        -h8WarmupFrames 2000 -h8MenuSeconds 420 -h8SettleSeconds 600 -h8GameplaySeconds 180 \
        -h8StartGame 1 -h8TimeoutSeconds 1500

    Play Mode probes must NOT pass --quit: the editor exits before Play Mode starts.

EXIT CODES
    0   Unity ran and exited 0
    n   Unity's own exit code
    75  BUILD_GATE_BLOCKED - preflight never cleared, nothing was launched
"""

from __future__ import annotations

import argparse
import subprocess
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
LOCKFILE = REPO_ROOT / "Temp" / "UnityLockfile"
GATE_BLOCKED_EXIT = 75


def project_editor_version() -> str:
    version_file = REPO_ROOT / "ProjectSettings" / "ProjectVersion.txt"
    for line in version_file.read_text(encoding="utf-8").splitlines():
        if line.startswith("m_EditorVersion:"):
            return line.split(":", 1)[1].strip()

    raise SystemExit(f"no m_EditorVersion in {version_file}")


def powershell(script: str) -> str:
    done = subprocess.run(
        ["powershell", "-NoProfile", "-Command", script],
        capture_output=True,
        text=True,
    )
    return done.stdout.strip()


def running(names: list[str]) -> list[str]:
    quoted = ",".join(names)
    raw = powershell(
        f"Get-Process {quoted} -ErrorAction SilentlyContinue | "
        "Select-Object -ExpandProperty Id"
    )
    return [line.strip() for line in raw.splitlines() if line.strip()]


def cpu_load() -> int:
    raw = powershell(
        "(Get-CimInstance Win32_Processor | "
        "Measure-Object -Property LoadPercentage -Average).Average"
    )
    try:
        return int(float(raw))
    except ValueError:
        # An unreadable counter must not read as "idle" - refuse instead.
        return 100


def blocker(max_cpu: int, allow_compile_owner: bool) -> str | None:
    unity = running(["Unity"])
    if unity:
        return f"Unity.exe already holds the project (pid {', '.join(unity)})"

    if not allow_compile_owner:
        compilers = running(["dotnet", "csc", "msbuild"])
        if compilers:
            return (
                f"{len(compilers)} compile process(es) active "
                "(dotnet/csc/msbuild) - one compile owner per target"
            )

    load = cpu_load()
    if load > max_cpu:
        return f"CPU at {load}%, above the {max_cpu}% preflight ceiling"

    return None


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--method", required=True, help="fully qualified -executeMethod target")
    parser.add_argument("--log", required=True, help="log file path, relative to the repo root")
    parser.add_argument("--quit", action="store_true", help="pass -quit; NEVER for Play Mode probes")
    parser.add_argument("--graphics", action="store_true", help="drop -nographics (required for compute/MapMagic)")
    parser.add_argument("--timeout", type=int, default=900, help="wall-second cap on the Unity process")
    parser.add_argument("--attempts", type=int, default=2, help="blocked-preflight attempts before refusing")
    parser.add_argument("--wait", type=int, default=60, help="seconds between attempts")
    parser.add_argument("--max-cpu", type=int, default=50)
    parser.add_argument("--allow-compile-owner", action="store_true")
    parser.add_argument("extra", nargs=argparse.REMAINDER, help="-- then any -h8* arguments")
    args = parser.parse_args()

    if args.quit and "PlayMode" in args.method:
        print(
            "REFUSED: --quit with a Play Mode probe. The editor exits before Play Mode "
            "starts and the run reports nothing."
        )
        return 2

    editor = Path(
        f"C:/Program Files/Unity/Hub/Editor/{project_editor_version()}/Editor/Unity.exe"
    )
    if not editor.is_file():
        print(f"BUILD_GATE_BLOCKED: the project's editor is not installed: {editor}")
        return GATE_BLOCKED_EXIT

    reason = None
    for attempt in range(1, max(1, args.attempts) + 1):
        reason = blocker(args.max_cpu, args.allow_compile_owner)
        if reason is None:
            break

        print(f"preflight {attempt}/{args.attempts} blocked: {reason}")
        if attempt < args.attempts:
            time.sleep(args.wait)

    if reason is not None:
        print(
            f"BUILD_GATE_BLOCKED: {reason}. Nothing was launched. Per AGENTS.md this is "
            "reported, not polled - rerun when the other lane is done."
        )
        return GATE_BLOCKED_EXIT

    # Only safe now: the preflight just proved no Unity process is alive, so a lockfile
    # here is the residue of a killed run rather than a live lock.
    if LOCKFILE.exists():
        LOCKFILE.unlink()
        print(f"removed stale {LOCKFILE.relative_to(REPO_ROOT)} (no Unity process alive)")

    command = [str(editor), "-batchmode", "-projectPath", str(REPO_ROOT), "-logFile", args.log]
    if not args.graphics:
        command.append("-nographics")
    if args.quit:
        command.append("-quit")
    command += ["-executeMethod", args.method]
    command += [token for token in args.extra if token != "--"]

    print(f"launching {args.method} -> {args.log} (timeout {args.timeout}s)")
    started = time.time()
    try:
        exit_code = subprocess.call(command, timeout=args.timeout)
    except subprocess.TimeoutExpired:
        print(
            f"TIMEOUT after {args.timeout}s. Unity may still be alive and holding "
            "Temp/UnityLockfile; kill it before the next run."
        )
        return 124

    print(f"Unity exited {exit_code} after {int(time.time() - started)}s. Read {args.log}.")
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
