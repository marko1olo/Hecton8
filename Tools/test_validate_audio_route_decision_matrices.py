import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioRouteDecisionMatrices as validator  # noqa: E402


class ValidateAudioRouteDecisionMatricesTests(unittest.TestCase):
    def test_current_project_matrices_match_static_contract(self) -> None:
        route_rows = validator.validate_spec(validator.ROUTE_SPEC)
        mix_rows = validator.validate_spec(validator.MIX_SPEC)
        cue_rows = validator.validate_spec(validator.CUE_SPEC)

        self.assertEqual(13, len(route_rows))
        self.assertEqual(10, len(mix_rows))
        self.assertEqual(12, len(cue_rows))

    def test_proof_looking_status_is_rejected(self) -> None:
        with self.assertRaises(SystemExit):
            validator.validate_status("matrix", "ROW-01", "ACCEPTED")

    def test_p0_row_requires_asset_owner_route(self) -> None:
        row = _valid_route_row()
        row["PrimaryOwnerPackets"] = "SHINOBU_352"

        with self.assertRaises(SystemExit):
            validator.validate_rows(validator.ROUTE_SPEC, _rows_for_spec(validator.ROUTE_SPEC, replacement=row))

    def test_missing_runtime_proof_language_is_rejected(self) -> None:
        row = _valid_mix_row()
        row["RequiredProof"] = "static scan only"

        with self.assertRaises(SystemExit):
            validator.validate_rows(validator.MIX_SPEC, _rows_for_spec(validator.MIX_SPEC, replacement=row))

    def test_expected_id_order_is_enforced(self) -> None:
        rows = _rows_for_spec(validator.CUE_SPEC)
        rows[0], rows[1] = rows[1], rows[0]

        with self.assertRaises(SystemExit):
            validator.validate_rows(validator.CUE_SPEC, rows)


def _base_row(spec: validator.MatrixSpec, row_id: str, priority: str = "P1") -> dict[str, str]:
    row = {column: "static value" for column in spec.required_columns}
    row[spec.id_column] = row_id
    row["Priority"] = priority
    row[spec.owner_column] = "ASSET_OWNER_23; ASSET_OWNER_28"
    row[spec.proof_column] = "runtime proof; import readback; listening notes; 0 B/frame proof"
    row["RejectIf"] = "missing owner route or treated as static proof"
    row["Status"] = "PENDING_VERIFICATION"
    return row


def _rows_for_spec(spec: validator.MatrixSpec, replacement: dict[str, str] | None = None) -> list[dict[str, str]]:
    rows = [
        _base_row(spec, row_id, priority="P0" if index < spec.expected_p0_count else "P1")
        for index, row_id in enumerate(spec.expected_ids)
    ]
    if replacement is not None:
        rows[0] = replacement
    return rows


def _valid_route_row() -> dict[str, str]:
    return _base_row(validator.ROUTE_SPEC, validator.ROUTE_SPEC.expected_ids[0], priority="P0")


def _valid_mix_row() -> dict[str, str]:
    return _base_row(validator.MIX_SPEC, validator.MIX_SPEC.expected_ids[0], priority="P0")


if __name__ == "__main__":
    unittest.main()
