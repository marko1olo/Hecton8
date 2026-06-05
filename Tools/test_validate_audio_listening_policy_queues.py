import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioListeningPolicyQueues as validator  # noqa: E402


class ValidateAudioListeningPolicyQueuesTests(unittest.TestCase):
    def test_current_project_queues_match_static_contract(self) -> None:
        results = validator.validate_audio_listening_policy_queues()

        self.assertEqual(13, len(results[validator.LISTENING_SPEC.name]))
        self.assertEqual(8, len(results[validator.IMPORT_POLICY_SPEC.name]))

    def test_proof_looking_status_is_rejected(self) -> None:
        with self.assertRaises(SystemExit):
            validator.validate_status("queue", "1", "ACCEPTED")

    def test_expected_listening_order_is_enforced(self) -> None:
        rows = _rows_for_spec(validator.LISTENING_SPEC)
        rows[0], rows[1] = rows[1], rows[0]

        with self.assertRaises(SystemExit):
            validator.validate_rows(validator.LISTENING_SPEC, rows)

    def test_missing_runtime_proof_language_is_rejected(self) -> None:
        rows = _rows_for_spec(validator.IMPORT_POLICY_SPEC)
        rows[0]["proof_needed"] = "static scan only"

        with self.assertRaises(SystemExit):
            validator.validate_rows(validator.IMPORT_POLICY_SPEC, rows)

    def test_missing_asset_reference_is_rejected(self) -> None:
        rows = _rows_for_spec(validator.LISTENING_SPEC)
        rows[0]["asset_or_config"] = "Assets/_Project/Audio/DOES_NOT_EXIST.wav"

        with self.assertRaises(SystemExit):
            validator.validate_rows(validator.LISTENING_SPEC, rows)


def _rows_for_spec(spec: validator.QueueSpec) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for index, row_id in enumerate(spec.expected_ids):
        row = {column: "static value" for column in spec.required_columns}
        row[spec.id_column] = row_id
        row[spec.priority_column] = "P0" if index < spec.expected_p0_count else "P1"
        if "required_runtime_proof" in row:
            row["required_runtime_proof"] = "runtime proof; import readback; listening proof; 0 B/frame proof"
        if "reject_condition" in row:
            row["reject_condition"] = "missing runtime proof or allocates"
        if "proof_needed" in row:
            row["proof_needed"] = "runtime proof; import readback; listening proof; memory proof"
        if "blockers" in row:
            row["blockers"] = "runtime proof absent and route blocked"
        if "status" in row:
            row["status"] = "PENDING_VERIFICATION"
        if "disposition" in row:
            row["disposition"] = "PENDING_VERIFICATION"
        if "asset_or_config" in row:
            row["asset_or_config"] = "Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv"
        rows.append(row)
    return rows


if __name__ == "__main__":
    unittest.main()
