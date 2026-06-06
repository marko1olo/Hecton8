import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateTextureImportRoleMatrix as validator  # noqa: E402


class ValidateTextureImportRoleMatrixTests(unittest.TestCase):
    def test_current_project_texture_queues_match_static_contract(self) -> None:
        results = validator.validate_texture_import_role_matrix()

        self.assertEqual(13, len(results[validator.TEXTURE_ROLE_SPEC.name]))
        self.assertEqual(7, len(results[validator.BATCH31_SPEC.name]))

    def test_albedo_must_be_srgb(self) -> None:
        row = _valid_texture_row()
        row["srgb"] = "false"

        with self.assertRaises(SystemExit):
            validator.validate_texture_role_semantics(row)

    def test_normal_must_be_normalmap(self) -> None:
        row = _valid_texture_row(role="normal", srgb="false", texture_type="Default")

        with self.assertRaises(SystemExit):
            validator.validate_texture_role_semantics(row)

    def test_missing_texture_source_path_rejects_row(self) -> None:
        row = _valid_texture_row()
        row["source_scope"] = "Docs/GeneratedAssets/DOES_NOT_EXIST.png"

        with self.assertRaises(SystemExit):
            validator.validate_texture_source_paths(row)

    def test_batch31_order_is_enforced(self) -> None:
        rows = _rows_for_spec(validator.BATCH31_SPEC)
        rows[0], rows[1] = rows[1], rows[0]

        with self.assertRaises(SystemExit):
            validator.validate_rows(validator.BATCH31_SPEC, rows)

    def test_blocked_batch31_mask_requires_channel_choice(self) -> None:
        rows = _rows_for_spec(validator.BATCH31_SPEC)
        rows[1]["RequiredBeforeUnityPromotion"] = "static review only"

        with self.assertRaises(SystemExit):
            validator.validate_rows(validator.BATCH31_SPEC, rows)


def _valid_texture_row(
    role: str = "albedo",
    srgb: str = "true",
    texture_type: str = "Default",
) -> dict[str, str]:
    row = {column: "static value" for column in validator.TEXTURE_ROLE_SPEC.required_columns}
    row["priority"] = "P1"
    row["texture_family"] = "test_family"
    row["role"] = role
    row["source_scope"] = "Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv"
    row["srgb"] = srgb
    row["texture_type"] = texture_type
    row["mipmaps"] = "true"
    row["streaming_mips"] = "true"
    row["proof_needed"] = "Unity import readback and material proof"
    row["blockers"] = "source-only and unproven"
    row["disposition"] = "SOURCE_ONLY_NOT_IMPORT_READY"
    return row


def _rows_for_spec(spec: validator.MatrixSpec) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for index, expected_id in enumerate(spec.expected_ids):
        row = {column: "static value" for column in spec.required_columns}
        if spec is validator.BATCH31_SPEC:
            row["DecisionId"] = expected_id
            row["Priority"] = "P0" if index < spec.expected_p0_count else "P1"
            row["OwnerRoute"] = "ASSET_OWNER_16; ASSET_OWNER_24"
            row["RequiredBeforeUnityPromotion"] = "Choose ARM_REPACK or MRAO_TARGET; Unity material readback proof"
            row["RejectIf"] = "imported without proof"
            row["Status"] = "BLOCKED_CHANNEL_SEMANTICS" if index == 1 else "PENDING_OWNER_DECISION"
        else:
            family, role = expected_id.split(":", 1)
            row.update(_valid_texture_row(role=role))
            row["texture_family"] = family
            row["priority"] = "P0" if index < spec.expected_p0_count else "P1"
        rows.append(row)
    return rows


if __name__ == "__main__":
    unittest.main()
