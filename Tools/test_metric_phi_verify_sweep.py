#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import tempfile
import time
import unittest
from pathlib import Path

from Tools import RunMetricPhiVerifySweep as sweep


class MetricPhiVerifySweepTests(unittest.TestCase):
    def test_selfcheck_sidecar_cleanup_removes_stale_files(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            report = Path(tmp_dir) / "METRIC_PHI_VERIFY_SWEEP.json"
            stale = Path(tmp_dir) / "METRIC_PHI_VERIFY_SWEEP.selfcheck.123.json"
            stale.write_text("{}", encoding="utf-8")
            old_time = time.time() - 7200.0
            os.utime(stale, (old_time, old_time))

            leftovers = sweep.cleanup_selfcheck_sidecars(report)

            self.assertEqual([], leftovers)
            self.assertFalse(stale.exists())

    def test_selfcheck_sidecar_cleanup_preserves_fresh_foreign_files(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            report = Path(tmp_dir) / "METRIC_PHI_VERIFY_SWEEP.json"
            foreign = Path(tmp_dir) / "METRIC_PHI_VERIFY_SWEEP.selfcheck.987654.json"
            foreign.write_text("{}", encoding="utf-8")

            leftovers = sweep.cleanup_selfcheck_sidecars(report)

            self.assertEqual([], leftovers)
            self.assertTrue(foreign.exists())

    def test_selfcheck_sidecar_cleanup_removes_current_pid_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            report = Path(tmp_dir) / "METRIC_PHI_VERIFY_SWEEP.json"
            own = Path(tmp_dir) / "METRIC_PHI_VERIFY_SWEEP.selfcheck.456.json"
            own.write_text("{}", encoding="utf-8")

            leftovers = sweep.cleanup_selfcheck_sidecars(report, current_pid=456, stale_after_seconds=0.0)

            self.assertEqual([], leftovers)
            self.assertFalse(own.exists())

    def test_final_report_is_not_written_as_pending_selfcheck(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            json_path = Path(tmp_dir) / "METRIC_PHI_VERIFY_SWEEP.json"
            md_path = Path(tmp_dir) / "METRIC_PHI_VERIFY_SWEEP.md"
            payload = {
                "status": "VERIFY_SWEEP_PASS",
                "summary": {
                    "selfCheckPending": False,
                    "transientRetryPasses": [],
                    "requiredFailures": 0,
                    "failedRequiredLabels": [],
                },
                "results": [],
            }

            sweep.write_reports(payload, json_path, md_path)
            written = json.loads(json_path.read_text(encoding="utf-8"))

            self.assertFalse(written["summary"]["selfCheckPending"])
            self.assertTrue(md_path.exists())
            self.assertEqual([], list(Path(tmp_dir).glob("*.selfcheck.*.json")))

    def test_make_payload_marks_only_sidecar_as_pending(self) -> None:
        args = argparse.Namespace(python_exe="python")

        sidecar_payload = sweep.make_payload(args, [], [], None, None, "TEST", True)
        final_payload = sweep.make_payload(args, [], [], None, None, "TEST", False)

        self.assertTrue(sidecar_payload["summary"]["selfCheckPending"])
        self.assertFalse(final_payload["summary"]["selfCheckPending"])
        self.assertEqual("VERIFY_SWEEP_PASS", final_payload["status"])


if __name__ == "__main__":
    unittest.main()
