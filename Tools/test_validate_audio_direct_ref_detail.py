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


if __name__ == "__main__":
    unittest.main()
