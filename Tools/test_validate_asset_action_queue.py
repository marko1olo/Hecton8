import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAssetActionQueue as validator  # noqa: E402


class ValidateAssetActionQueueTests(unittest.TestCase):
    def test_current_project_action_queue_matches_static_contract(self) -> None:
        rows = validator.validate_asset_action_queue()

        self.assertEqual(11, len(rows))
        self.assertEqual(4, sum(1 for row in rows if row.priority == "P0"))
        self.assertEqual(5, sum(1 for row in rows if row.priority == "P1"))
        self.assertEqual(2, sum(1 for row in rows if row.priority == "P2"))

    def test_domain_order_is_enforced(self) -> None:
        rows = validator.load_rows()
        rows[0], rows[1] = rows[1], rows[0]

        with self.assertRaises(SystemExit):
            validator.validate_rows(rows)

    def test_missing_owner_packet_rejected(self) -> None:
        row = _row(owner_packet="DOES_NOT_EXIST.md")

        with self.assertRaises(SystemExit):
            validator.validate_owner_packets(row)

    def test_missing_affected_path_rejected(self) -> None:
        row = _row(affected_paths="Docs/AssetAudit/DOES_NOT_EXIST.csv")

        with self.assertRaises(SystemExit):
            validator.validate_explicit_paths(row)

    def test_non_pending_status_rejected(self) -> None:
        rows = validator.load_rows()
        row = rows[0]
        rows[0] = validator.ActionQueueRow(
            priority=row.priority,
            domain=row.domain,
            defect=row.defect,
            evidence=row.evidence,
            affected_paths=row.affected_paths,
            owner_packet=row.owner_packet,
            required_action=row.required_action,
            acceptance_proof=row.acceptance_proof,
            status="READY",
        )

        with self.assertRaises(SystemExit):
            validator.validate_rows(rows)


def _row(**overrides: str) -> validator.ActionQueueRow:
    values = {
        "priority": "P0",
        "domain": "water_visual",
        "defect": "defect",
        "evidence": "evidence",
        "affected_paths": "Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv",
        "owner_packet": "ASSET_OWNER_01_UNITY_MATERIAL_READBACK.md",
        "required_action": "Read Unity material state",
        "acceptance_proof": "Unity readback and screenshot proof",
        "status": "PENDING_VERIFICATION",
    }
    values.update(overrides)
    return validator.ActionQueueRow(**values)


if __name__ == "__main__":
    unittest.main()
