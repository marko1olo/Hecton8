import json
import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))
SCRATCH_ROOT = TOOLS_ROOT.parent / "Temp" / "ToolTests" / "test_validate_unity_process_gate"

import ValidateUnityProcessGate as gate  # noqa: E402


class ValidateUnityProcessGateTests(unittest.TestCase):
    def test_default_blockers_include_unity_hub(self) -> None:
        self.assertIn("Unity Hub", gate.DEFAULT_BLOCKER_NAMES)

    def test_green_when_cpu_under_threshold_and_no_blockers(self) -> None:
        samples = (
            gate.GateSample(cpu_percent=24.0, processes=()),
            gate.GateSample(cpu_percent=49.9, processes=()),
        )

        result = gate.evaluate_gate(samples, max_cpu=50.0)

        self.assertEqual("UNITY_PROCESS_GATE_GREEN", result.status)
        self.assertEqual(0, result.cpu_over_count)
        self.assertEqual(0, result.blocker_count)

    def test_red_when_cpu_unknown_or_over_threshold(self) -> None:
        samples = (
            gate.GateSample(cpu_percent=None, processes=()),
            gate.GateSample(cpu_percent=50.1, processes=()),
        )

        result = gate.evaluate_gate(samples, max_cpu=50.0)

        self.assertEqual("UNITY_PROCESS_GATE_RED", result.status)
        self.assertEqual(2, result.cpu_over_count)

    def test_red_when_cpu_sample_failed_and_reason_is_preserved(self) -> None:
        samples = (
            gate.GateSample(cpu_percent=None, processes=(), cpu_error="Access denied"),
        )

        result = gate.evaluate_gate(samples, max_cpu=50.0)

        self.assertEqual("UNITY_PROCESS_GATE_RED", result.status)
        self.assertEqual(1, result.cpu_over_count)
        self.assertEqual("Access denied", samples[0].cpu_error)

    def test_red_when_blocker_process_exists_even_with_low_cpu(self) -> None:
        samples = (
            gate.GateSample(
                cpu_percent=12.0,
                processes=(gate.ProcessInfo(name="dotnet", pid=1234, cpu=3.5),),
            ),
        )

        result = gate.evaluate_gate(samples, max_cpu=50.0)

        self.assertEqual("UNITY_PROCESS_GATE_RED", result.status)
        self.assertEqual(1, result.blocker_count)

    def test_sample_file_accepts_nested_samples_shape(self) -> None:
        payload = {
            "samples": [
                {
                    "LoadPercentage": 35,
                    "processes": [{"ProcessName": "Unity", "Id": 42, "CPU": 1.25}],
                    "topProcesses": [{"ProcessName": "Code", "Id": 100, "CPU": 44.5}],
                }
            ]
        }
        SCRATCH_ROOT.mkdir(parents=True, exist_ok=True)
        path = SCRATCH_ROOT / "samples.json"
        path.write_text(json.dumps(payload), encoding="utf-8")

        samples = gate.load_samples(path)

        self.assertEqual(1, len(samples))
        self.assertEqual(35.0, samples[0].cpu_percent)
        self.assertEqual("Unity", samples[0].processes[0].name)
        self.assertEqual(42, samples[0].processes[0].pid)
        self.assertEqual("Code", samples[0].top_processes[0].name)
        self.assertEqual(100, samples[0].top_processes[0].pid)

    def test_sample_file_preserves_zero_cpu_and_cpu_error(self) -> None:
        payload = {
            "cpu_percent": 0,
            "cpuError": "Access denied",
            "processes": [{"ProcessName": "dotnet", "Id": 14464, "CPU": 0}],
        }
        SCRATCH_ROOT.mkdir(parents=True, exist_ok=True)
        path = SCRATCH_ROOT / "zero_cpu_sample.json"
        path.write_text(json.dumps(payload), encoding="utf-8")

        samples = gate.load_samples(path)

        self.assertEqual(0.0, samples[0].cpu_percent)
        self.assertEqual("Access denied", samples[0].cpu_error)
        self.assertEqual(0.0, samples[0].processes[0].cpu)

    def test_top_processes_do_not_count_as_blockers(self) -> None:
        samples = (
            gate.GateSample(
                cpu_percent=12.0,
                processes=(),
                top_processes=(gate.ProcessInfo(name="Code", pid=100, cpu=44.5),),
            ),
        )

        result = gate.evaluate_gate(samples, max_cpu=50.0)

        self.assertEqual("UNITY_PROCESS_GATE_GREEN", result.status)
        self.assertEqual(0, result.blocker_count)


if __name__ == "__main__":
    unittest.main()
