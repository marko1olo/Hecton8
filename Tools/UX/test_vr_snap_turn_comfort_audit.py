#!/usr/bin/env python3
"""Stdlib tests for the Quest VR comfort audit."""

from __future__ import annotations

import json
import ast
import tempfile
import unittest
from pathlib import Path

import vr_snap_turn_comfort_audit as audit


class VRSnapTurnComfortAuditTests(unittest.TestCase):
    def test_audit_script_has_no_duplicate_top_level_functions(self) -> None:
        script_path = Path(str(audit.__file__))
        tree = ast.parse(script_path.read_text(encoding="utf-8"))
        seen: set[str] = set()
        duplicates: set[str] = set()
        for node in tree.body:
            if isinstance(node, ast.FunctionDef):
                if node.name in seen:
                    duplicates.add(node.name)
                seen.add(node.name)

        self.assertEqual(set(), duplicates)

    def test_audit_script_path_contract_matches_module_file(self) -> None:
        self.assertEqual(Path(str(audit.__file__)).resolve(), audit.SCRIPT_PATH)
        self.assertEqual("vr_snap_turn_comfort_audit.py", audit.SCRIPT_PATH.name)

    def test_current_profile_passes(self) -> None:
        payload = audit.build_audit_payload()

        self.assertEqual("PASS", payload["status"])
        self.assertEqual([], payload["errors"])
        self.assertEqual(10, payload["hapticWaveformCount"])
        self.assertEqual(320.0, payload["sourceContract"]["runtimeJerkFullRadS3"])
        self.assertEqual(50.0, payload["sourceContract"]["runtimeAccelerationSoftTunnelStartRadS2"])
        self.assertEqual(180.0, payload["sourceContract"]["runtimeAccelerationEmergencyClampRadS2"])
        self.assertEqual(30.0, payload["sourceContract"]["runtimeAccelerationReleaseBelowRadS2"])
        self.assertEqual(0.22, payload["sourceContract"]["runtimeAccelerationReleaseHysteresisSeconds"])
        self.assertEqual(0.05, payload["sourceContract"]["runtimeComfortVignetteAttackSlewPerFrame"])
        self.assertEqual(0.022, payload["sourceContract"]["runtimeComfortVignetteReleaseSlewPerFrame"])
        self.assertEqual(16.0, payload["sourceContract"]["hapticBufferCapacity"])
        self.assertEqual(2, len(payload["results"]))
        for result in payload["results"]:
            self.assertEqual(0, result["shock_frames"])
            self.assertLessEqual(result["max_opacity_delta"], 0.10)

    def test_required_haptic_events_exist(self) -> None:
        payload = json.loads(audit.WAVEFORM_JSON.read_text(encoding="utf-8"))
        events = {str(waveform.get("event", "")) for waveform in payload["waveforms"]}

        self.assertIn("Collision", events)
        self.assertIn("LowO2Pulse", events)
        self.assertIn("EngineHum", events)

    def test_report_writes_source_hashes(self) -> None:
        payload = audit.build_audit_payload()
        with tempfile.TemporaryDirectory() as temp_dir:
            report_path = Path(temp_dir) / "audit.json"
            audit.write_report(payload, report_path)
            written = json.loads(report_path.read_text(encoding="utf-8"))

        hashes = written["sourceHashes"]
        self.assertEqual(64, len(hashes["comfortProfileSha256"]))
        self.assertEqual(64, len(hashes["comfortMarkdownSha256"]))
        self.assertEqual(64, len(hashes["hapticWaveformsSha256"]))
        self.assertEqual(64, len(hashes["auditScriptSha256"]))
        self.assertEqual(64, len(hashes["vrSomaticProviderSha256"]))
        self.assertEqual(64, len(hashes["toolHapticsRuntimeSha256"]))

    def test_report_writer_sanitizes_non_finite_values(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            report_path = Path(temp_dir) / "audit.json"
            audit.write_report({"value": float("nan"), "nested": [float("inf")]}, report_path)
            written = json.loads(report_path.read_text(encoding="utf-8"))

        self.assertIsNone(written["value"])
        self.assertIsNone(written["nested"][0])

    def test_report_check_accepts_current_report(self) -> None:
        payload = audit.build_audit_payload()
        with tempfile.TemporaryDirectory() as temp_dir:
            report_path = Path(temp_dir) / "audit.json"
            audit.write_report(payload, report_path)
            errors = audit.validate_report(report_path)

        self.assertEqual([], errors)

    def test_report_check_rejects_stale_hashes(self) -> None:
        payload = audit.build_audit_payload()
        with tempfile.TemporaryDirectory() as temp_dir:
            report_path = Path(temp_dir) / "audit.json"
            audit.write_report(payload, report_path)
            written = json.loads(report_path.read_text(encoding="utf-8"))
            written["sourceHashes"]["comfortProfileSha256"] = "0" * 64
            report_path.write_text(json.dumps(written, allow_nan=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            errors = audit.validate_report(report_path)

        self.assertTrue(any("report source hashes stale" in error for error in errors))

    def test_visual_shock_failure_injection(self) -> None:
        profiles, jerk_profile, _, errors = audit.load_comfort_profile()
        self.assertEqual([], errors)
        strict_rules = audit.ShockRules(
            max_opacity_delta_per_frame=0.001,
            max_untunneled_angle_delta_deg=0.1,
            min_opacity_for_large_angle_delta=0.99,
        )

        shock_frames = 0
        for profile in profiles:
            result = audit.simulate_profile(profile, jerk_profile, strict_rules)
            shock_frames += int(result["shock_frames"])

        self.assertGreater(shock_frames, 0)

    def test_source_contract_mismatch_injection(self) -> None:
        errors: list[str] = []

        audit.compare_close("injected", 1.0, 2.0, 0.001, errors)

        self.assertEqual(1, len(errors))
        self.assertIn("source contract mismatch", errors[0])

    def test_runtime_acceleration_fragment_failure_injection(self) -> None:
        errors: list[str] = []

        audit.validate_runtime_source_fragments("class MissingAccelerationPath {}", errors)

        self.assertTrue(any("runtime acceleration integration missing source fragment" in error for error in errors))

    def test_runtime_acceleration_source_requires_hysteresis_and_slew_fragments(self) -> None:
        errors: list[str] = []
        partial_source = """
            private void UpdateAccelerationComfortState() {}
            AccelerationVignette01 = Sanitize01(_accelerationComfortVignette01, 0f);
            vignette01 = math.max(vignette01, math.saturate(input.AccelerationVignette01));
        """

        audit.validate_runtime_source_fragments(partial_source, errors)

        self.assertTrue(any("_accelerationReleaseBelowTimer" in error for error in errors))
        self.assertTrue(any("math.clamp(target - _accelerationComfortVignette01" in error for error in errors))
        self.assertTrue(any("ApproximateMagnitudeNoSqrt(angularAcceleration)" in error for error in errors))
        self.assertTrue(any("PublishComfortVignette(0f)" in error for error in errors))
        self.assertTrue(any("_accelerationComfortVignette01 = 0f" in error for error in errors))

    def test_source_contract_malformed_profile_number_fails_closed(self) -> None:
        payload = json.loads(audit.COMFORT_JSON.read_text(encoding="utf-8"))
        payload["jerk"]["fullEventRadS3"] = "not-a-number"
        original_path = audit.COMFORT_JSON

        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir) / "comfort.json"
            temp_path.write_text(json.dumps(payload, allow_nan=False), encoding="utf-8")
            try:
                audit.COMFORT_JSON = temp_path
                _, errors = audit.validate_source_contract()
            finally:
                audit.COMFORT_JSON = original_path

        self.assertTrue(any("sourceContract.jerk.fullEventRadS3 must be numeric" in error for error in errors))

    def test_source_contract_malformed_shape_fails_closed(self) -> None:
        original_comfort_path = audit.COMFORT_JSON
        original_waveform_path = audit.WAVEFORM_JSON
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_root = Path(temp_dir)
            comfort_path = temp_root / "comfort.json"
            waveform_path = temp_root / "haptic.json"
            comfort_path.write_text(
                json.dumps(
                    {
                        "jerk": [],
                        "stabilization": {"modes": ["bad-mode"]},
                        "devices": ["bad-device"],
                    }
                ),
                encoding="utf-8",
            )
            waveform_path.write_text(json.dumps({"limits": []}), encoding="utf-8")
            try:
                audit.COMFORT_JSON = comfort_path
                audit.WAVEFORM_JSON = waveform_path
                _, errors = audit.validate_source_contract()
            finally:
                audit.COMFORT_JSON = original_comfort_path
                audit.WAVEFORM_JSON = original_waveform_path

        self.assertTrue(any("sourceContract.jerk must be object" in error for error in errors))
        self.assertTrue(any("sourceContract.stabilization.modes[0] must be object" in error for error in errors))
        self.assertTrue(any("sourceContract.devices[0] must be object" in error for error in errors))
        self.assertTrue(any("sourceContract.haptic.limits must be object" in error for error in errors))

    def test_table_mismatch_injection(self) -> None:
        comfort_payload = json.loads(audit.COMFORT_JSON.read_text(encoding="utf-8"))
        profiles, _, _, errors = audit.load_comfort_profile()
        self.assertEqual([], errors)
        comfort_payload["deviceTable"]["refreshHz"][0] = 80.0

        table_errors: list[str] = []
        audit.validate_comfort_device_table(comfort_payload, profiles, table_errors)

        self.assertTrue(any("table parity" in error for error in table_errors))

    def test_markdown_threshold_match_requires_unit(self) -> None:
        self.assertTrue(audit.markdown_contains_number_with_unit("Quest 2 | 42 rad/s2 |", 42.0, "rad/s2"))
        self.assertTrue(audit.markdown_contains_number_with_unit("Quest 3 | 50.0 rad/s2 |", 50.0, "rad/s2"))
        self.assertFalse(audit.markdown_contains_number_with_unit("Quest 2 | 42 ms |", 42.0, "rad/s2"))

    def test_waveform_table_mismatch_injection(self) -> None:
        payload = json.loads(audit.WAVEFORM_JSON.read_text(encoding="utf-8"))
        payload["waveformTable"]["event"][0] = "WrongEvent"

        _, errors = audit.validate_waveform_payload(payload)

        self.assertTrue(any("waveformTable event mismatch" in error for error in errors))

    def test_waveform_identity_failure_injection(self) -> None:
        payload = json.loads(audit.WAVEFORM_JSON.read_text(encoding="utf-8"))
        payload["waveforms"][0]["id"] = "renamed_collision"
        payload["runtimeContract"] = "WrongRuntime"

        _, errors = audit.validate_waveform_payload(payload)

        self.assertTrue(any("waveform id set/order mismatch" in error for error in errors))
        self.assertTrue(any("haptic runtimeContract mismatch" in error for error in errors))

    def test_waveform_malformed_numeric_fails_closed(self) -> None:
        payload = json.loads(audit.WAVEFORM_JSON.read_text(encoding="utf-8"))
        payload["waveforms"][0]["lowFreqIntensity"] = "not-a-number"
        payload["waveforms"][0]["priority"] = "not-an-int"
        payload["waveformTable"]["priority"][0] = "not-an-int"

        _, errors = audit.validate_waveform_payload(payload)

        self.assertTrue(any("lowFreqIntensity must be numeric" in error for error in errors))
        self.assertTrue(any("priority must be integer" in error for error in errors))

    def test_integer_fields_reject_bool_and_string_values(self) -> None:
        payload = json.loads(audit.WAVEFORM_JSON.read_text(encoding="utf-8"))
        payload["waveformCount"] = "10"
        payload["waveforms"][0]["priority"] = True
        payload["waveformTable"]["priority"][0] = "3"

        _, errors = audit.validate_waveform_payload(payload)

        self.assertTrue(any("waveformCount must be integer" in error for error in errors))
        self.assertTrue(any("hull_collision_light.priority must be integer" in error for error in errors))
        self.assertTrue(
            any("hull_collision_light.waveformTable.priority must be integer" in error for error in errors)
        )

    def test_waveform_malformed_shape_fails_closed(self) -> None:
        payload = json.loads(audit.WAVEFORM_JSON.read_text(encoding="utf-8"))
        payload["waveforms"][0] = "not-an-object"
        payload["waveformCount"] = "not-an-int"
        payload["limits"] = []
        payload["waveformTable"]["event"] = "not-an-array"

        _, errors = audit.validate_waveform_payload(payload)

        self.assertTrue(any("waveforms[0] must be object" in error for error in errors))
        self.assertTrue(any("waveformCount must be integer" in error for error in errors))
        self.assertTrue(any("haptic limits must be object" in error for error in errors))
        self.assertTrue(any("waveformTable.event must be array" in error for error in errors))

    def test_comfort_malformed_numeric_fails_closed(self) -> None:
        payload = json.loads(audit.COMFORT_JSON.read_text(encoding="utf-8"))
        payload["devices"][0]["refreshHz"] = "not-a-number"
        payload["jerk"]["softRadS3"] = "not-a-number"
        payload["visualTeleportShock"]["maxOpacityDeltaPerFrame"] = "not-a-number"
        payload["speedVignetteLutQuest3"][0]["speed"] = "not-a-number"
        payload["stabilization"]["modes"][0]["sharpness"] = "not-a-number"
        payload["deviceTable"]["refreshHz"][0] = "not-a-number"

        _, _, _, errors = audit.parse_comfort_payload(payload)

        self.assertTrue(any("Quest2_72Hz.refreshHz must be numeric" in error for error in errors))
        self.assertTrue(any("jerk.softRadS3 must be numeric" in error for error in errors))
        self.assertTrue(
            any("visualTeleportShock.maxOpacityDeltaPerFrame must be numeric" in error for error in errors)
        )
        self.assertTrue(any("speedVignetteLutQuest3[0].speed must be numeric" in error for error in errors))
        self.assertTrue(any("stabilization mode low sharpness must be numeric" in error for error in errors))
        self.assertTrue(any("Quest2_72Hz.deviceTable.refreshHz must be numeric" in error for error in errors))

    def test_float_fields_reject_bool_and_numeric_strings(self) -> None:
        payload = json.loads(audit.COMFORT_JSON.read_text(encoding="utf-8"))
        payload["devices"][0]["refreshHz"] = "72.0"
        payload["jerk"]["softRadS3"] = True
        payload["speedVignetteLutQuest3"][0]["opacity"] = "0.0"
        payload["deviceTable"]["opacityMax"][0] = False

        _, _, _, errors = audit.parse_comfort_payload(payload)

        self.assertTrue(any("Quest2_72Hz.refreshHz must be numeric" in error for error in errors))
        self.assertTrue(any("jerk.softRadS3 must be numeric" in error for error in errors))
        self.assertTrue(any("speedVignetteLutQuest3[0].opacity must be numeric" in error for error in errors))
        self.assertTrue(any("Quest2_72Hz.deviceTable.opacityMax must be numeric" in error for error in errors))

    def test_runtime_integration_failure_injection(self) -> None:
        payload = json.loads(audit.COMFORT_JSON.read_text(encoding="utf-8"))
        payload["runtimeIntegration"]["executionPhase"] = "SIMULATION"
        payload["runtimeIntegration"]["fieldBindings"][0]["profilePath"] = "wrong.path"
        errors: list[str] = []

        audit.validate_runtime_integration(payload, errors)

        self.assertTrue(any("executionPhase must be VISUAL_SYNC" in error for error in errors))
        self.assertTrue(any("fieldBindings mismatch" in error for error in errors))

    def test_runtime_integration_malformed_shape_fails_closed(self) -> None:
        payload = json.loads(audit.COMFORT_JSON.read_text(encoding="utf-8"))
        payload["phaseOwnership"] = []
        payload["runtimeIntegration"] = {
            "hotPathRules": [],
            "fieldBindings": ["not-an-object"],
        }
        errors: list[str] = []

        audit.validate_runtime_integration(payload, errors)

        self.assertTrue(any("phaseOwnership must be object" in error for error in errors))
        self.assertTrue(any("runtimeIntegration.hotPathRules must be object" in error for error in errors))
        self.assertTrue(any("runtimeIntegration.fieldBindings[0] must be object" in error for error in errors))
        self.assertTrue(any("fieldBindings mismatch" in error for error in errors))

    def test_missing_runtime_source_fails_closed(self) -> None:
        original_path = audit.VR_SOMATIC_PROVIDER_CS
        try:
            audit.VR_SOMATIC_PROVIDER_CS = audit.ROOT / "missing_vr_somatic_provider_for_test.cs"
            payload = audit.build_audit_payload()
        finally:
            audit.VR_SOMATIC_PROVIDER_CS = original_path

        self.assertEqual("FAIL", payload["status"])
        self.assertEqual("MISSING", payload["sourceHashes"]["vrSomaticProviderSha256"])
        self.assertTrue(any("missing source contract file" in error for error in payload["errors"]))

    def test_missing_comfort_json_fails_closed(self) -> None:
        original_path = audit.COMFORT_JSON
        try:
            audit.COMFORT_JSON = audit.ROOT / "missing_vr_comfort_profile_for_test.json"
            payload = audit.build_audit_payload()
        finally:
            audit.COMFORT_JSON = original_path

        self.assertEqual("FAIL", payload["status"])
        self.assertEqual("MISSING", payload["sourceHashes"]["comfortProfileSha256"])
        self.assertTrue(any("comfort profile JSON missing" in error for error in payload["errors"]))

    def test_non_object_comfort_json_fails_closed(self) -> None:
        original_path = audit.COMFORT_JSON
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir) / "comfort.json"
            temp_path.write_text("[]", encoding="utf-8")
            try:
                audit.COMFORT_JSON = temp_path
                payload = audit.build_audit_payload()
            finally:
                audit.COMFORT_JSON = original_path

        self.assertEqual("FAIL", payload["status"])
        self.assertTrue(any("comfort profile JSON root must be object" in error for error in payload["errors"]))

    def test_invalid_haptic_json_fails_closed(self) -> None:
        original_path = audit.WAVEFORM_JSON
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir) / "haptic.json"
            temp_path.write_text("{", encoding="utf-8")
            try:
                audit.WAVEFORM_JSON = temp_path
                payload = audit.build_audit_payload()
            finally:
                audit.WAVEFORM_JSON = original_path

        self.assertEqual("FAIL", payload["status"])
        self.assertTrue(any("haptic waveform JSON invalid" in error for error in payload["errors"]))


if __name__ == "__main__":
    unittest.main()
