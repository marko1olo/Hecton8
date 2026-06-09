#!/usr/bin/env python3
"""Validate the HECTON-8 Unity/process safety gate."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BLOCKER_NAMES = (
    "Unity",
    "Unity Hub",
    "Unity.ILPP.Runner",
    "Unity.ILPP.Trigger",
    "UnityShaderCompiler",
    "UnityPackageManager",
    "AssetImportWorker",
    "Bee",
    "dotnet",
    "csc",
    "MSBuild",
    "VBCSCompiler",
    "UnityAutoQuitter",
)


@dataclass(frozen=True)
class ProcessInfo:
    name: str
    pid: int | None = None
    cpu: float | None = None


@dataclass(frozen=True)
class GateSample:
    cpu_percent: float | None
    processes: tuple[ProcessInfo, ...]
    top_processes: tuple[ProcessInfo, ...] = ()
    cpu_error: str = ""


@dataclass(frozen=True)
class GateResult:
    status: str
    samples: tuple[GateSample, ...]
    max_cpu: float
    cpu_over_count: int
    blocker_count: int


def _to_float(value: Any) -> float | None:
    if value is None or value == "":
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _to_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _first_present(raw_item: dict[str, Any], keys: tuple[str, ...]) -> Any:
    for key in keys:
        value = raw_item.get(key)
        if value is not None and value != "":
            return value
    return None


def normalize_processes(raw_processes: Any) -> tuple[ProcessInfo, ...]:
    if raw_processes is None:
        return ()
    if isinstance(raw_processes, dict):
        raw_items = [raw_processes]
    elif isinstance(raw_processes, list):
        raw_items = raw_processes
    else:
        return ()

    processes: list[ProcessInfo] = []
    for item in raw_items:
        if not isinstance(item, dict):
            continue
        name = str(item.get("ProcessName") or item.get("name") or item.get("Name") or "").strip()
        if not name:
            continue
        processes.append(
            ProcessInfo(
                name=name,
                pid=_to_int(_first_present(item, ("Id", "pid"))),
                cpu=_to_float(_first_present(item, ("CPU", "cpu"))),
            )
        )
    return tuple(processes)


def normalize_sample(raw_sample: Any) -> GateSample:
    if not isinstance(raw_sample, dict):
        return GateSample(cpu_percent=None, processes=())
    cpu_percent = _to_float(_first_present(raw_sample, ("cpu_percent", "cpu", "LoadPercentage", "loadPercentage")))
    raw_top_processes = _first_present(raw_sample, ("top_processes", "topProcesses", "TopProcesses"))
    cpu_error = _first_present(raw_sample, ("cpu_error", "cpuError", "CpuError"))
    return GateSample(
        cpu_percent=cpu_percent,
        processes=normalize_processes(raw_sample.get("processes")),
        top_processes=normalize_processes(raw_top_processes),
        cpu_error="" if cpu_error is None else " ".join(str(cpu_error).split()),
    )


def load_samples(path: Path) -> tuple[GateSample, ...]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(payload, dict) and "samples" in payload:
        raw_samples = payload["samples"]
    else:
        raw_samples = payload
    if isinstance(raw_samples, dict):
        raw_samples = [raw_samples]
    if not isinstance(raw_samples, list):
        raise SystemExit(f"FAIL: sample file must contain a sample object or samples array: {path}")
    return tuple(normalize_sample(item) for item in raw_samples)


def sample_process_gate(top_process_limit: int = 5) -> GateSample:
    process_names = ",".join(f"'{name}'" for name in DEFAULT_BLOCKER_NAMES)
    top_process_limit = max(0, int(top_process_limit))
    script = f"""
$ErrorActionPreference = 'Stop'
$cpu = $null
$cpuError = ""
try {{
    $cpu = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
}} catch {{
    $cpuError = $_.Exception.Message
}}
$names = @({process_names})
$procs = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {{ $names -contains $_.ProcessName }} | Select-Object ProcessName,Id,CPU)
$logicalCores = [Math]::Max(1, [Environment]::ProcessorCount)
$sampleMs = 250
$before = @{{}}
Get-Process -ErrorAction SilentlyContinue |
    Where-Object {{ $_.CPU -ne $null }} |
    ForEach-Object {{ $before[$_.Id] = [pscustomobject]@{{ ProcessName = $_.ProcessName; Id = $_.Id; CPU = [double]$_.CPU }} }}
Start-Sleep -Milliseconds $sampleMs
$elapsedSeconds = [double]$sampleMs / 1000.0
$top = @(Get-Process -ErrorAction SilentlyContinue |
    Where-Object {{ $_.CPU -ne $null -and $before.ContainsKey($_.Id) }} |
    ForEach-Object {{
        $previous = $before[$_.Id]
        $deltaSeconds = [Math]::Max(0.0, [double]$_.CPU - [double]$previous.CPU)
        $cpuPercent = ($deltaSeconds / $elapsedSeconds / $logicalCores) * 100.0
        [pscustomobject]@{{ ProcessName = $_.ProcessName; Id = $_.Id; CPU = [Math]::Round($cpuPercent, 2) }}
    }} |
    Sort-Object CPU -Descending |
    Select-Object -First {top_process_limit} ProcessName,Id,CPU)
[pscustomobject]@{{ cpu_percent = $cpu; cpuError = $cpuError; processes = $procs; topProcesses = $top }} | ConvertTo-Json -Depth 6 -Compress
"""
    completed = subprocess.run(
        ("powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script),
        cwd=ROOT,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        details = (completed.stderr or completed.stdout or "").strip()
        raise SystemExit(f"FAIL: process gate sample failed: {details}")
    output = completed.stdout.strip()
    if not output:
        raise SystemExit("FAIL: process gate sample returned empty output")
    return normalize_sample(json.loads(output))


def collect_samples(sample_count: int, interval_seconds: float, top_process_limit: int) -> tuple[GateSample, ...]:
    samples: list[GateSample] = []
    for index in range(sample_count):
        if index > 0 and interval_seconds > 0:
            time.sleep(interval_seconds)
        samples.append(sample_process_gate(top_process_limit=top_process_limit))
    return tuple(samples)


def evaluate_gate(samples: tuple[GateSample, ...], max_cpu: float) -> GateResult:
    cpu_over_count = 0
    blocker_count = 0
    for sample in samples:
        if sample.cpu_percent is None or sample.cpu_percent > max_cpu:
            cpu_over_count += 1
        blocker_count += len(sample.processes)
    status = "UNITY_PROCESS_GATE_GREEN" if cpu_over_count == 0 and blocker_count == 0 else "UNITY_PROCESS_GATE_RED"
    return GateResult(
        status=status,
        samples=samples,
        max_cpu=max_cpu,
        cpu_over_count=cpu_over_count,
        blocker_count=blocker_count,
    )


def print_result(result: GateResult) -> None:
    cpu_values = [sample.cpu_percent for sample in result.samples if sample.cpu_percent is not None]
    max_sample_cpu = max(cpu_values) if cpu_values else None
    max_cpu_text = "unknown" if max_sample_cpu is None else f"{max_sample_cpu:.1f}"
    print(
        f"{result.status} samples={len(result.samples)} max_sample_cpu={max_cpu_text} "
        f"cpu_over={result.cpu_over_count} blocker_processes={result.blocker_count} threshold={result.max_cpu:.1f}"
    )
    for index, sample in enumerate(result.samples, start=1):
        cpu_text = "unknown" if sample.cpu_percent is None else f"{sample.cpu_percent:.1f}"
        cpu_error_text = f" cpuError={sample.cpu_error}" if sample.cpu_error else ""
        print(f"sample[{index}] cpu={cpu_text} blockers={len(sample.processes)}{cpu_error_text}")
        for process in sample.processes:
            pid_text = "unknown" if process.pid is None else str(process.pid)
            cpu_process_text = "unknown" if process.cpu is None else f"{process.cpu:.2f}"
            print(f"- {process.name} pid={pid_text} cpuSeconds={cpu_process_text}")
        if sample.cpu_percent is not None and sample.cpu_percent > result.max_cpu and not sample.processes:
            for process in sample.top_processes:
                pid_text = "unknown" if process.pid is None else str(process.pid)
                cpu_process_text = "unknown" if process.cpu is None else f"{process.cpu:.2f}"
                print(f"- top {process.name} pid={pid_text} cpuPercent={cpu_process_text}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sample-file", help="JSON sample file for deterministic tests/offline evaluation.")
    parser.add_argument("--samples", type=int, default=1, help="Number of live samples.")
    parser.add_argument("--interval-seconds", type=float, default=0.0, help="Delay between live samples.")
    parser.add_argument("--max-cpu", type=float, default=50.0, help="Maximum allowed CPU percentage.")
    parser.add_argument("--top-processes", type=int, default=5, help="Diagnostic top process count when CPU gate is red.")
    parser.add_argument("--no-fail", action="store_true", help="Return 0 even when the gate is red.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(sys.argv[1:] if argv is None else argv)
    if args.samples < 1:
        raise SystemExit("FAIL: --samples must be >= 1")
    if args.sample_file:
        samples = load_samples(Path(args.sample_file))
    else:
        samples = collect_samples(args.samples, args.interval_seconds, args.top_processes)
    result = evaluate_gate(samples, max_cpu=args.max_cpu)
    print_result(result)
    if args.no_fail or result.status == "UNITY_PROCESS_GATE_GREEN":
        return 0
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
