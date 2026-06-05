import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateVisualSourcePromotionQueue as validator  # noqa: E402


class ValidateVisualSourcePromotionQueueTests(unittest.TestCase):
    def test_current_project_queue_matches_static_contract(self) -> None:
        rows = validator.validate_visual_source_promotion_queue()

        self.assertEqual(10, len(rows))
        self.assertEqual(4, sum(1 for row in rows if row.priority == "P0"))
        self.assertEqual(5, sum(1 for row in rows if row.priority == "P1"))
        self.assertEqual(1, sum(1 for row in rows if row.priority == "P2"))

    def test_missing_antifalse_proof_rejects_row(self) -> None:
        row = _promotion_row(required_proof="Slot readback; bright surface screenshot; Frame Debugger; memory")

        with self.assertRaises(SystemExit):
            validator.validate_common_proof_gates([row])

    def test_missing_vref_id_rejects_row(self) -> None:
        row = _promotion_row(vref_scope="VREF-99", source_candidates="visual hero source coverage row VHSC-01")

        with self.assertRaises(SystemExit):
            validator.validate_vref_and_vhsc_links([row], {"VREF-03"}, {"VHSC-01"})

    def test_unknown_owner_rejects_route(self) -> None:
        row = _promotion_row(owner_route="ASSET_OWNER_20; ASSET_OWNER_36; ASSET_OWNER_37; ASSET_OWNER_99")

        with self.assertRaises(SystemExit):
            validator.validate_owner_routes([row], {"ASSET_OWNER_20", "ASSET_OWNER_36", "ASSET_OWNER_37"})

    def test_source_input_queues_match_expected_blockers(self) -> None:
        foam_rows, batch31_rows = validator.validate_source_decision_inputs()

        self.assertEqual(8, foam_rows)
        self.assertEqual(7, batch31_rows)


def _promotion_row(**overrides: str) -> validator.PromotionRow:
    values = {
        "queue_id": "VSPQ-01",
        "priority": "P0",
        "route_moment": "Surface sky Aegir coast first-viewport signal",
        "vref_scope": "VREF-03; VREF-05; VREF-15",
        "source_candidates": "CleanupPass AegirCloud source maps; visual hero source coverage row VHSC-01",
        "source_status": "SOURCE_ONLY_STATIC_IMAGE_QA",
        "blocking_gap": "Aegir/sky/cloud active slots unproven",
        "owner_route": "ASSET_OWNER_14; ASSET_OWNER_16; ASSET_OWNER_20; ASSET_OWNER_36; ASSET_OWNER_37",
        "next_action": "Run no-mutation sky/Aegir/cloud material slot readback",
        "required_proof": (
            "Slot readback; import role; bright surface screenshot; Frame Debugger; memory; "
            "no-mutation proof packet; anti-false-proof gate"
        ),
        "reject_if": "Aegir remains smeared pasted transparent sphere",
        "low_consequence": "Compressed role-correct maps with readable Aegir silhouette",
        "middle_consequence": "Route-owned layered sky after slot proof",
        "high_ultra_consequence": "Longer residency and richer Aegir/cloud layering only after render/memory proof",
        "status": "PENDING_UNITY_SLOT_AND_VISUAL_PROOF",
    }
    values.update(overrides)
    return validator.PromotionRow(**values)


if __name__ == "__main__":
    unittest.main()
