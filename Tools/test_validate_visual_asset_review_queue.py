import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateVisualAssetReviewQueue as validator  # noqa: E402


class ValidateVisualAssetReviewQueueTests(unittest.TestCase):
    def test_current_project_visual_queue_matches_static_contract(self) -> None:
        rows = validator.validate_visual_asset_review_queue()

        self.assertEqual(11, len(rows))
        self.assertEqual(2, sum(1 for row in rows if row.priority == "P0"))

    def test_queue_order_is_enforced(self) -> None:
        rows = validator.load_rows()
        rows[0], rows[1] = rows[1], rows[0]

        with self.assertRaises(SystemExit):
            validator.validate_queue_rows(rows)

    def test_non_pending_status_rejected(self) -> None:
        rows = validator.load_rows()
        row = rows[0]
        rows[0] = validator.VisualQueueRow(
            queue_order=row.queue_order,
            priority=row.priority,
            target=row.target,
            source_or_asset=row.source_or_asset,
            route_context=row.route_context,
            why_first=row.why_first,
            required_visual_proof=row.required_visual_proof,
            reject_condition=row.reject_condition,
            status="READY",
        )

        with self.assertRaises(SystemExit):
            validator.validate_queue_rows(rows)

    def test_missing_visual_proof_language_rejected(self) -> None:
        rows = validator.load_rows()
        row = rows[0]
        rows[0] = validator.VisualQueueRow(
            queue_order=row.queue_order,
            priority=row.priority,
            target=row.target,
            source_or_asset=row.source_or_asset,
            route_context=row.route_context,
            why_first=row.why_first,
            required_visual_proof="static doc only",
            reject_condition=row.reject_condition,
            status=row.status,
        )

        with self.assertRaises(SystemExit):
            validator.validate_queue_rows(rows)

    def test_missing_checked_source_path_rejected(self) -> None:
        row = validator.VisualQueueRow(
            queue_order="1",
            priority="P0",
            target="Waterline foam/contact",
            source_or_asset="Docs/AssetAudit/DOES_NOT_EXIST.png",
            route_context="route",
            why_first="reason",
            required_visual_proof="Unity readback and screenshot proof",
            reject_condition="no import proof",
            status="PENDING_VERIFICATION",
        )

        with self.assertRaises(SystemExit):
            validator.validate_source_paths(row)


if __name__ == "__main__":
    unittest.main()
