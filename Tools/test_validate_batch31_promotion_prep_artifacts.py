import copy
import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateBatch31PromotionPrepArtifacts as validator  # noqa: E402


class ValidateBatch31PromotionPrepArtifactsTests(unittest.TestCase):
    def test_current_project_batch31_promotion_prep_matches_static_contract(self) -> None:
        checked_files, blocked = validator.validate_batch31_promotion_prep_artifacts()

        self.assertEqual(28, checked_files)
        self.assertEqual(3, blocked)

    def test_promotion_ready_true_rejected(self) -> None:
        data = validator.load_json(validator.INDEX_JSON_PATH)
        edited = copy.deepcopy(data)
        edited["packages"][0]["promotion_ready"] = True

        with self.assertRaises(SystemExit):
            validator.validate_index_json(edited)

    def test_sha_mismatch_rejected(self) -> None:
        data = validator.load_json(validator.INDEX_JSON_PATH)
        edited = copy.deepcopy(data)
        edited["packages"][0]["sha256"]["albedo"] = "0" * 64

        with self.assertRaises(SystemExit):
            validator.validate_index_json(edited)

    def test_static_qa_import_safe_claim_rejected(self) -> None:
        data = validator.load_json(validator.STATIC_QA_PATH)
        edited = copy.deepcopy(data)
        edited["checks"]["unity_import_safe_to_claim"] = True

        with self.assertRaises(SystemExit):
            validator.validate_static_qa(edited)

    def test_decision_queue_mrao_must_remain_blocked(self) -> None:
        rows = validator.load_decision_rows()
        edited = [dict(row) for row in rows]
        for row in edited:
            if row["ArtifactSet"] == "MRAO Candidate":
                row["Status"] = "PENDING_OWNER_DECISION"
                break

        with self.assertRaises(SystemExit):
            validator.validate_decision_queue(edited)


if __name__ == "__main__":
    unittest.main()
