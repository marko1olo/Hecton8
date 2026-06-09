import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))
SCRATCH_ROOT = TOOLS_ROOT.parent / "Temp" / "ToolTests" / "test_validate_gemini_unity_apply_runner"

import ValidateGeminiUnityApplyRunner as validator  # noqa: E402


class GeminiUnityApplyRunnerValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.original_runner = validator.RUNNER

    def tearDown(self) -> None:
        validator.RUNNER = self.original_runner

    def validate_runner_text(self, text: str) -> list[str]:
        SCRATCH_ROOT.mkdir(parents=True, exist_ok=True)
        runner = SCRATCH_ROOT / "RunGeminiMaterialUnityApplyAll.ps1"
        runner.write_text(text, encoding="utf-8")
        validator.RUNNER = runner
        errors: list[str] = []
        validator.validate_runner(errors)
        return errors

    def minimal_runner_text(self, extra_gate_text: str = "") -> str:
        validators = "\n".join(validator.REQUIRED_POST_APPLY_VALIDATORS)
        return f"""
$executeMethod = "{validator.EXPECTED_EXECUTE_METHOD}"
$unityProcessGateValidator = Join-Path $projectRoot "Tools\\ValidateUnityProcessGate.py"
$PostPreflightCooldownSeconds = 10
function Invoke-UnityProcessGate {{
    & python -B $unityProcessGateValidator --max-cpu $CpuLimitPercent --samples $CpuSamples --interval-seconds $CpuSampleIntervalSeconds --top-processes 8
}}
function Wait-AfterStaticPreflight {{
    Start-Sleep -Seconds $PostPreflightCooldownSeconds
}}
{extra_gate_text}
function Wait-Or-Assert-Gate {{ Invoke-UnityProcessGate }}
function Get-UnityLogIssueSummary {{ }}
& $staticPreflightRunner
Wait-AfterStaticPreflight
Wait-Or-Assert-Gate
Write-Host "startUtc="
& $resolvedUnity
Write-Host "endUtc= exitCode=$unityExitCode warningCount=$($unityLogSummary.WarningCount) errorCount=$($unityLogSummary.ErrorCount) logExists=$($unityLogSummary.LogExists)"
if ($unityExitCode -ne 0 -or -not $unityLogSummary.LogExists -or $unityLogSummary.ErrorCount -gt 0) {{ throw "failed" }}
{validators}
"""

    def test_runner_rejects_duplicate_local_process_gate(self) -> None:
        errors = self.validate_runner_text(
            self.minimal_runner_text("function Assert-NoBuildProcesses { Get-Process dotnet }")
        )

        self.assertTrue(any("duplicate local process gate" in error for error in errors), errors)

    def test_runner_accepts_canonical_process_gate_tokens(self) -> None:
        errors = self.validate_runner_text(self.minimal_runner_text())
        process_gate_errors = [
            error for error in errors if "canonical Unity process gate token" in error
        ]

        self.assertEqual([], process_gate_errors)


if __name__ == "__main__":
    unittest.main()
