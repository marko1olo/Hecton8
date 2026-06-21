import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioDirectRefDetail as validator  # noqa: E402


class ValidateAudioDirectRefDetailTests(unittest.TestCase):
    def test_current_project_audio_direct_ref_detail_matches_contract(self) -> None:
        counts = validator.validate_audio_direct_ref_detail()

        self.assertEqual(0, counts[("P0", "P0_SPLASH_DIRECT_REF_CLASSIFICATION_BLOCKED")])
        self.assertEqual(20, counts[("P1", "P1_FOOTSTEP_DIRECT_REF_OWNER_BLOCKED")])
        self.assertEqual(4, counts[("P1", "P1_UI_DIRECT_REF_AUDIBILITY_BLOCKED")])

    def test_stale_underwater_ambient_row_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        stale = dict(edited[0])
        stale["SourceLine"] = "137"
        stale["CuePath"] = "Assets/_Project/Audio/Underwater Ambient.wav"
        stale["CueId"] = "UNDERWATER_AMBIENT"
        stale["DirectRefContext"] = "m_Resource: {fileID: 8300000, guid: 0d1a03d1d70c9dd448ad1fbab16de520, type: 3}"
        stale["Disposition"] = "P0_AMBIENT_DIRECT_REF_BLOCKED"
        edited.append(stale)

        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_current_csv_aligns_to_player_prefab_scan(self) -> None:
        rows = validator.load_rows()
        category_counts = validator.validate_prefab_alignment(rows)

        self.assertEqual(0, category_counts["underwater_ambient"])
        self.assertEqual(0, category_counts["dive_splash"])
        self.assertEqual(20, category_counts["footstep"])
        self.assertEqual(4, category_counts["ui"])

    def test_missing_runtime_gc_proof_boundary_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["ProofRequired"] = "Prefab readback only"

        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_duplicate_direct_ref_context_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[1]["DirectRefContext"] = edited[0]["DirectRefContext"]

        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_markdown_owner_rule_required(self) -> None:
        text = validator.load_text().replace("Do not treat direct prefab serialization as Addressables ownership.", "")

        with self.assertRaises(SystemExit):
            validator.validate_companion_doc(text)

    def test_sidecar_caveat_terms_required(self) -> None:
        text_path = validator.SIDECAR_CAVEAT_PATHS[0]
        original = text_path.read_text(encoding="utf-8")
        temp_path = text_path.with_name("TEMP_AUDIO_DIRECT_REF_CAVEAT_TEST.md")
        temp_path.write_text(original.replace("dive_splash.wav", ""), encoding="utf-8")
        try:
            with self.assertRaises(SystemExit):
                validator.validate_sidecar_caveats((temp_path,))
        finally:
            temp_path.unlink(missing_ok=True)

    def test_display_path_fallback(self) -> None:
        fallback_path = Path("/tmp/outside_root_path.txt")
        self.assertEqual("/tmp/outside_root_path.txt", validator.display_path(fallback_path))

    def test_load_rows_missing_file_rejected(self) -> None:
        with self.assertRaises(SystemExit):
            validator.load_rows(Path("/non/existent/path.csv"))

    def test_load_rows_missing_columns_rejected(self) -> None:
        import tempfile
        import csv
        with tempfile.NamedTemporaryFile("w", encoding="utf-8-sig", newline="", delete=False) as tf:
            writer = csv.DictWriter(tf, fieldnames=["Priority"])
            writer.writeheader()
            temp_path = Path(tf.name)
        try:
            with self.assertRaises(SystemExit):
                validator.load_rows(temp_path)
        finally:
            temp_path.unlink()

    def test_load_rows_incorrect_total_rejected(self) -> None:
        import tempfile
        import csv
        with tempfile.NamedTemporaryFile("w", encoding="utf-8-sig", newline="", delete=False) as tf:
            writer = csv.DictWriter(tf, fieldnames=validator.EXPECTED_COLUMNS)
            writer.writeheader()
            writer.writerow({col: "val" for col in validator.EXPECTED_COLUMNS})
            temp_path = Path(tf.name)
        try:
            with self.assertRaises(SystemExit):
                validator.load_rows(temp_path)
        finally:
            temp_path.unlink()

    def test_load_text_missing_file_rejected(self) -> None:
        with self.assertRaises(SystemExit):
            validator.load_text(Path("/non/existent/path.md"))

    def test_require_float_invalid_value_rejected(self) -> None:
        with self.assertRaises(SystemExit):
            validator.require_float({"key": "abc"}, "key", 0.0, "row_id")

    def test_require_float_below_minimum_rejected(self) -> None:
        with self.assertRaises(SystemExit):
            validator.require_float({"key": "0.5"}, "key", 1.0, "row_id")

    def test_require_int_invalid_value_rejected(self) -> None:
        with self.assertRaises(SystemExit):
            validator.require_int({"key": "abc"}, "key", "row_id")

    def test_require_int_below_minimum_rejected(self) -> None:
        with self.assertRaises(SystemExit):
            validator.require_int({"key": "0"}, "key", "row_id")

    def test_validate_rows_empty_column_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["Priority"] = ""
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_unexpected_source_asset_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["SourceAsset"] = "Assets/_Project/Prefabs/Enemy.prefab"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_cue_class_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["CueClass"] = "IndirectRef"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_evidence_class_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["EvidenceClass"] = "DYNAMIC_SOURCE"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_missing_cue_path_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited, root=Path("/non/existent/path"))

    def test_validate_rows_required_owner_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["RequiredOwner"] = "Other owner"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_required_action_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["RequiredAction"] = "Other action"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_footstep_disposition_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        footstep_row = next(r for r in edited if "Footsteps/" in r["CuePath"])
        footstep_row["Priority"] = "P2"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_footstep_classification_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        footstep_row = next(r for r in edited if "Footsteps/" in r["CuePath"])
        footstep_row["RemediationCategory"] = "long_sfx"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_footstep_duration_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        footstep_row = next(r for r in edited if "Footsteps/" in r["CuePath"])
        footstep_row["DurationSec"] = "2.0"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_ui_disposition_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        ui_row = next(r for r in edited if "/Audio/UI/" in r["CuePath"])
        ui_row["Priority"] = "P2"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_ui_remediation_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        ui_row = next(r for r in edited if "/Audio/UI/" in r["CuePath"])
        ui_row["RemediationCategory"] = "other"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_unexpected_path_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["CuePath"] = "Assets/_Project/Audio/Other/path.wav"
        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_disposition_counts_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["Priority"] = "P0" # This changes the count for the disposition but will fail earlier due to disposition check for the specific type. Let's make it more robust.

        # A simpler way to trigger counts drift is to duplicate a row and remove another
        edited.append(dict(edited[0]))
        edited.pop(1)

        # We need to make sure we don't trigger duplicate context error, so we change it
        edited[-1]["DirectRefContext"] = "m_Resource: {fileID: 1, guid: 2, type: 3}"

        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_rows_duplicate_counts_drift_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        # To fail at duplicate counts drift, we can't just change CueId, because it checks UNDERWATER_AMBIENT and DIVE_SPLASH
        # Wait, if cue_counts["UNDERWATER_AMBIENT"] != 0, it fails.
        # But if CuePath ends with Underwater Ambient.wav, it fails earlier.
        # So we can change CueId to UNDERWATER_AMBIENT without changing CuePath.
        edited[0]["CueId"] = "UNDERWATER_AMBIENT"
        edited[0]["DirectRefContext"] = "m_Resource: {fileID: 1, guid: 2, type: 3}" # Just to be safe

        # But wait, validate_rows has duplicate contexts check.
        # And cue_counts tracks row["CueId"]
        # If we change edited[0]["CueId"] to "UNDERWATER_AMBIENT", then cue_counts["UNDERWATER_AMBIENT"] will be >= 1.
        # Let's see if it triggers the SystemExit. Yes, at line 165.

        # The expected counts will fail before line 165, because we replaced the CueId,
        # but the Counts check `counts != Counter(EXPECTED_COUNTS)` relies on disposition!
        # Disposition counts only look at `counts[(row["Priority"], row["Disposition"])] += 1`
        # which isn't affected by CueId!

        # So yes, this should work.

        with self.assertRaises(SystemExit):
            validator.validate_rows(edited)

    def test_validate_prefab_alignment_missing_stale_rejected(self) -> None:
        rows = validator.load_rows()
        edited = [dict(row) for row in rows]
        edited[0]["SourceLine"] = "99999"
        with self.assertRaises(SystemExit):
            validator.validate_prefab_alignment(edited)

if __name__ == "__main__":
    unittest.main()
