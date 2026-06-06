import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateTextureRoleTechnicalLedger as validator  # noqa: E402


class ValidateTextureRoleTechnicalLedgerTests(unittest.TestCase):
    def test_current_project_texture_role_technical_ledger_rejects_known_drift(self) -> None:
        result = validator.validate_texture_role_technical_ledger()

        self.assertEqual(13, result.role_rows)
        self.assertEqual(2, result.exact_asset_rows)
        self.assertEqual(4, result.directory_rows)
        self.assertEqual(7, result.docs_source_only_rows)
        self.assertGreaterEqual(len(result.blockers), 1)
        self.assertTrue(any("ui_oxygen_mask:mask" in blocker for blocker in result.blockers))

    def test_exact_asset_srgb_mismatch_is_rejected(self) -> None:
        role = _role_row(role="mask", srgb="false")
        ledger = _ledger_row(meta_srgb="1")

        blockers = validator.validate_exact_asset(role, ledger)

        self.assertTrue(any("srgb_mismatch" in blocker for blocker in blockers))

    def test_directory_albedo_accepts_matching_color_row(self) -> None:
        role = _role_row(role="albedo", srgb="true", texture_type="Default")
        ledger = [_ledger_row(path="Assets/_Project/Test/Color.png", meta_texture_type="0", meta_srgb="1")]

        blockers = validator.validate_directory_scope(role, ledger)

        self.assertEqual([], blockers)

    def test_directory_normal_mask_requires_normal_and_linear_mask(self) -> None:
        role = _role_row(role="normal_mrao", srgb="false", texture_type="NormalMap or Default linear mask by role")
        ledger = [_ledger_row(path="Assets/_Project/Test/Normal.png", meta_texture_type="1", meta_srgb="0")]

        blockers = validator.validate_directory_scope(role, ledger)

        self.assertTrue(any("missing_linear_mask" in blocker for blocker in blockers))

    def test_scope_part_parser_keeps_asset_and_generated_docs_parts(self) -> None:
        parts = validator.source_scope_parts("Assets/_Project/A and Docs/GeneratedAssets/B sources")

        self.assertEqual(["Assets/_Project/A", "Docs/GeneratedAssets/B"], parts)


def _role_row(
    role: str = "albedo",
    srgb: str = "true",
    texture_type: str = "Default",
    mipmaps: str = "true",
    streaming_mips: str = "true",
) -> validator.RoleRow:
    return validator.RoleRow(
        priority="P1",
        texture_family="test_family",
        role=role,
        source_scope="Assets/_Project/Test",
        srgb=srgb,
        texture_type=texture_type,
        mipmaps=mipmaps,
        streaming_mips=streaming_mips,
        blockers="unproven",
        disposition="PENDING_VERIFICATION",
    )


def _ledger_row(
    path: str = "Assets/_Project/Test/Mask.png",
    meta_texture_type: str = "0",
    meta_srgb: str = "0",
    meta_mipmaps: str = "1",
    meta_streaming_mips: str = "1",
) -> validator.TextureLedgerRow:
    return validator.TextureLedgerRow(
        path=path,
        meta_texture_type=meta_texture_type,
        meta_srgb=meta_srgb,
        meta_mipmaps=meta_mipmaps,
        meta_streaming_mips=meta_streaming_mips,
        ledger_class="texture_source",
        policy_flags="NONE",
    )


if __name__ == "__main__":
    unittest.main()
