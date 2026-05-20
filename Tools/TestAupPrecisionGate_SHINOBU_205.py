#!/usr/bin/env python3
from __future__ import annotations

import json
import tempfile
from datetime import datetime, timezone
from pathlib import Path

import AupPrecisionGate_SHINOBU_205 as gate


REPO_ROOT = Path(__file__).resolve().parent.parent


def assert_equal(actual: int, expected: int, label: str) -> None:
    if actual != expected:
        raise AssertionError(f"{label}: expected {expected}, got {actual}")


def write_text(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8")


def test_precision_gate_fixture() -> None:
    with tempfile.TemporaryDirectory(prefix="shinobu205_aup_gate_") as temp_dir:
        root = Path(temp_dir)
        write_text(
            root / "Runtime" / "BadAup.cs",
            """
public sealed class BadAup
{
    public void Tick()
    {
        float3 direct = (float3)entity.AUP;
        float3 component = new float3((float)targetAUP.x, (float)targetAUP.y, (float)targetAUP.z);
        float distance = Vector3.Distance(transform.position, player.position);
        float3 approved = AupPrecisionMath.LocalDeltaFloat3(targetAup, observerAup, float3.zero);
    }
}
""",
        )
        write_text(
            root / "Editor" / "ReviewAup.cs",
            """
public sealed class ReviewAup
{
    public void Draw()
    {
        Vector3 presentation = new Vector3((float)sampleAUP.x, (float)sampleAUP.y, (float)sampleAUP.z);
    }
}
""",
        )
        write_text(
            root / "Editor" / "AUP_Premature_Cast_Scanner.cs",
            """
public sealed class AUP_Premature_Cast_Scanner
{
    public void Draw()
    {
        Vector3 intentionalLie = new Vector3((float)sampleAUP.x, (float)sampleAUP.y, (float)sampleAUP.z);
    }
}
""",
        )

        counts = gate.scan_sources(root, sample_limit=32)
        assert_equal(counts["directAupFloat3CastCount"], 1, "directAupFloat3CastCount")
        assert_equal(counts["runtimeComponentFloatAupCastCount"], 1, "runtimeComponentFloatAupCastCount")
        assert_equal(counts["editorComponentFloatAupCastReviewCount"], 1, "editorComponentFloatAupCastReviewCount")
        assert_equal(counts["strictTransformAuthorityReadCount"], 1, "strictTransformAuthorityReadCount")
        assert_equal(counts["approvedHelperCalls"], 1, "approvedHelperCalls")


def write_report(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(
            {
                "agent": "SHINOBU_205",
                "evidence": "PY_TOOL_STATIC_GATE_SELF_TEST",
                "generatedUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "status": "PASS",
                "tests": rows,
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )


def main() -> int:
    tests = [test_precision_gate_fixture]
    rows: list[dict[str, str]] = []
    for test in tests:
        test()
        rows.append({"name": test.__name__, "status": "PASS"})
        print(f"PASS {test.__name__}")
    write_report(REPO_ROOT / "Docs" / "Reports" / "AUP_PRECISION_GATE_SELF_TEST_SHINOBU_205.json", rows)
    print("SHINOBU_205_AUP_PRECISION_GATE_SELF_TESTS=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
