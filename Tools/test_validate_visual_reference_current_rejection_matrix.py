import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateVisualReferenceCurrentRejectionMatrix as validator  # noqa: E402


class ValidateVisualReferenceCurrentRejectionMatrixTests(unittest.TestCase):
    def test_current_project_visual_reference_rejection_matrix_is_rejection_only(self) -> None:
        rows = validator.validate_visual_reference_current_rejection_matrix()

        self.assertEqual(10, len(rows))
        self.assertEqual("surface/sky/Aegir/moons/clouds", rows[0].area)
        self.assertEqual("surface water recovery probe 1914", rows[-1].area)

    def test_promotion_status_is_rejected(self) -> None:
        rows = validator.load_rejection_rows()
        edited = list(rows)
        edited[0] = _row_from(edited[0], status="ACCEPTED / READY")

        with self.assertRaises(SystemExit):
            validator.validate_rejection_rows(edited, validator.load_known_h8_artifacts())

    def test_raw_mcp_capture_route_is_rejected(self) -> None:
        rows = validator.load_rejection_rows()
        edited = list(rows)
        edited[0] = _row_from(
            edited[0],
            required_capture="Docs/Screenshots/MCP/raw_surface.png; h8_1475_surface_sky_aegir_ocean_hud_game.png",
        )

        with self.assertRaises(SystemExit):
            validator.validate_rejection_rows(edited, validator.load_known_h8_artifacts())

    def test_missing_proof_field_must_name_evidence_class(self) -> None:
        rows = validator.load_rejection_rows()
        edited = list(rows)
        edited[0] = _row_from(edited[0], current_missing_proof="Looks fine later.")

        with self.assertRaises(SystemExit):
            validator.validate_rejection_rows(edited, validator.load_known_h8_artifacts())


def _row_from(row: validator.RejectionRow, **overrides: str) -> validator.RejectionRow:
    values = {
        "area": row.area,
        "reference_demand": row.reference_demand,
        "current_static_evidence": row.current_static_evidence,
        "current_missing_proof": row.current_missing_proof,
        "rejection_trigger": row.rejection_trigger,
        "required_capture": row.required_capture,
        "owner": row.owner,
        "status": row.status,
    }
    values.update(overrides)
    return validator.RejectionRow(**values)


if __name__ == "__main__":
    unittest.main()
