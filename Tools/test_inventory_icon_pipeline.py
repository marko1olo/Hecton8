#!/usr/bin/env python3
"""Regression tests for offline inventory icon generation/binding helpers."""

from __future__ import annotations

import json
from argparse import Namespace
import tempfile
import unittest
from pathlib import Path

import InventoryGeminiPromptFromGapAudit as prompt_gen
import InventoryIsolatedObjectBaker as isolated_baker
import InventoryIconBindingMapFromGapAudit as binding_map_gen
import InventoryGapBatchPipeline as batch_pipeline
import InventoryIconBindingMapValidator as binding_validator
import InventoryIconReadabilityPreview as readability_preview
import InventoryIconReviewMap as review_map
import InventoryIconGapAudit as gap_audit
import InventoryUnityImportStateAudit as import_audit
from PIL import Image, ImageDraw


TEST_TMP_ROOT = Path("C:/tmp/Hecton8PythonTests")


def temporary_directory() -> tempfile.TemporaryDirectory:
    TEST_TMP_ROOT.mkdir(parents=True, exist_ok=True)
    return tempfile.TemporaryDirectory(dir=TEST_TMP_ROOT)


def make_gap(
    stable_id: str,
    *,
    priority: int,
    planned: bool = False,
    icon_empty: bool = True,
) -> gap_audit.ItemIconGap:
    return gap_audit.ItemIconGap(
        path=Path(f"Assets/_Project/Data/Items/{stable_id}.asset"),
        stable_id=stable_id,
        display_name=stable_id,
        category=2,
        progression_tier=0,
        width=1,
        height=1,
        icon_empty=icon_empty,
        planned=planned,
        priority=priority,
    )


class InventoryIconGapAuditTests(unittest.TestCase):
    def test_forced_missing_targets_keep_order_before_scored_items(self) -> None:
        items = [
            make_gap("Item_Tool_Builder", priority=200),
            make_gap("Item_Tool_SalvageSampler", priority=50),
            make_gap("Data_ElectrolyteAmpoule", priority=40),
        ]

        names = gap_audit.build_names(
            items,
            limit=3,
            include_planned=False,
            force_ids=("Item_Tool_SalvageSampler", "Data_ElectrolyteAmpoule"),
        )

        self.assertEqual("SalvageSampler,ElectrolyteAmpoule,Builder\n", names)

    def test_forced_planned_target_is_rejected(self) -> None:
        items = [make_gap("Data_TitaniumScrap", priority=100, planned=True)]

        with self.assertRaisesRegex(RuntimeError, "already planned"):
            gap_audit.build_names(
                items,
                limit=1,
                include_planned=False,
                force_ids=("Data_TitaniumScrap",),
            )


class InventoryGapBatchPipelineTests(unittest.TestCase):
    def test_overwrite_guard_blocks_existing_generated_content(self) -> None:
        with temporary_directory() as temp_dir:
            output = Path(temp_dir) / "Alpha512"
            output.mkdir()
            (output / "DRAFT_TX_Batch33_InventoryGap_01_Builder_Alpha512.png").write_bytes(b"not-empty")

            with self.assertRaisesRegex(RuntimeError, "Refusing to overwrite existing asset Alpha512 output"):
                batch_pipeline.require_overwrite_allowed(output, "asset Alpha512 output", False)

            batch_pipeline.require_overwrite_allowed(output, "asset Alpha512 output", True)

    def test_overwrite_guard_ignores_meta_only_import_sentinel(self) -> None:
        with temporary_directory() as temp_dir:
            output = Path(temp_dir) / "Atlas"
            output.mkdir()
            (output / "old.meta").write_text("guid: placeholder\n", encoding="utf-8")

            batch_pipeline.require_overwrite_allowed(output, "asset atlas output", False)


class InventoryGeminiPromptTests(unittest.TestCase):
    def test_render_prompt_uses_position_words_not_visible_cell_numbers(self) -> None:
        spec = {
            "items": [
                {
                    "index": 1,
                    "promptPhrase": "hydroacoustic scanner wand with sensor face, sealed glass with no text",
                },
                {
                    "index": 2,
                    "promptPhrase": "single rugged electrolyte ampoule held alone",
                },
            ]
        }

        prompt = prompt_gen.render_prompt(spec, "Docs/GeneratedAssets/Gemini/reference.png")
        prompt_gen.lint_prompt(prompt)

        self.assertIn("reading-order position one: hydroacoustic scanner wand", prompt)
        self.assertIn("treat those as defects to avoid", prompt)
        self.assertIn("unmarked physical surfaces", prompt)
        self.assertIn("clear safety moat", prompt)
        self.assertIn("never a close-up crop", prompt)
        self.assertIn("no label plates", prompt)
        self.assertIn("no text-like decal noise", prompt)
        self.assertIn("no badge rim, halo, glow card, or app-store icon pose", prompt)
        self.assertNotIn("Cell 1", prompt)
        self.assertNotIn("with no text", prompt)

    def test_lint_rejects_internal_ids_in_visible_prompt(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "internal persistent id"):
            prompt_gen.lint_prompt("top-left position: Item_Tool_Builder")


class InventoryBindingMapValidatorTests(unittest.TestCase):
    def test_spec_order_rejects_sprite_token_mismatch(self) -> None:
        with temporary_directory() as temp_dir:
            spec_path = Path(temp_dir) / "spec.json"
            spec_path.write_text(
                json.dumps(
                    {
                        "items": [
                            {
                                "index": 1,
                                "persistentId": "Item_Tool_SalvageSampler",
                                "asset": "Assets/_Project/Data/Items/Tools/Item_Tool_SalvageSampler.asset",
                                "safeName": "SalvageSampler",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            bindings = [
                {
                    "enabled": True,
                    "persistentId": "Item_Tool_SalvageSampler",
                    "itemAsset": "Assets/_Project/Data/Items/Tools/Item_Tool_SalvageSampler.asset",
                    "spriteAsset": "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch32/Alpha512/DRAFT_TX_Batch32_InventoryGap_01_Builder_Alpha512.png",
                }
            ]
            errors: list[str] = []

            binding_validator.validate_spec_order(spec_path, bindings, errors)

        self.assertTrue(any("sprite name mismatch" in error for error in errors), errors)

    def test_spec_order_allows_rejected_disabled_gaps_when_requested(self) -> None:
        with temporary_directory() as temp_dir:
            spec_path = Path(temp_dir) / "spec.json"
            spec_path.write_text(
                json.dumps(
                    {
                        "items": [
                            {
                                "index": 1,
                                "persistentId": "Item_Tool_Builder",
                                "asset": "Assets/_Project/Data/Items/Tools/Item_Tool_Builder.asset",
                                "safeName": "Builder",
                            },
                            {
                                "index": 2,
                                "persistentId": "Item_Tool_Scanner",
                                "asset": "Assets/_Project/Data/Items/Tools/Item_Tool_Scanner.asset",
                                "safeName": "Scanner",
                            },
                        ]
                    }
                ),
                encoding="utf-8",
            )
            bindings = [
                {
                    "enabled": False,
                    "approved": False,
                    "reviewStatus": "REJECTED",
                    "reviewedBy": "unit-test",
                    "reviewedAt": "2026-06-07T00:00:00Z",
                    "reviewNote": "cropped tool cell",
                    "persistentId": "",
                    "itemAsset": "",
                    "spriteAsset": "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch33/Alpha512/DRAFT_TX_Batch33_InventoryGap_01_Builder_Alpha512.png",
                },
                {
                    "enabled": True,
                    "persistentId": "Item_Tool_Scanner",
                    "itemAsset": "Assets/_Project/Data/Items/Tools/Item_Tool_Scanner.asset",
                    "spriteAsset": "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch33/Alpha512/DRAFT_TX_Batch33_InventoryGap_02_Scanner_Alpha512.png",
                },
            ]
            strict_errors: list[str] = []
            gap_errors: list[str] = []

            binding_validator.validate_spec_order(spec_path, bindings, strict_errors)
            binding_validator.validate_spec_order(spec_path, bindings, gap_errors, allow_disabled_spec_gaps=True)

        self.assertTrue(any("spec/enabled binding count mismatch" in error for error in strict_errors), strict_errors)
        self.assertEqual([], gap_errors)

    def test_disabled_rejected_spec_gap_requires_review_metadata_and_sprite(self) -> None:
        with temporary_directory() as temp_dir:
            spec_path = Path(temp_dir) / "spec.json"
            spec_path.write_text(
                json.dumps(
                    {
                        "items": [
                            {
                                "index": 1,
                                "persistentId": "Item_Tool_Builder",
                                "asset": "Assets/_Project/Data/Items/Tools/Item_Tool_Builder.asset",
                                "safeName": "Builder",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            bindings = [
                {
                    "enabled": False,
                    "approved": False,
                    "reviewStatus": "REJECTED",
                    "persistentId": "",
                    "itemAsset": "",
                    "spriteAsset": "",
                }
            ]
            errors: list[str] = []

            binding_validator.validate_spec_order(spec_path, bindings, errors, allow_disabled_spec_gaps=True)

        self.assertTrue(any("no review metadata" in error for error in errors), errors)
        self.assertTrue(any("no sprite proof" in error for error in errors), errors)

    def test_bake_manifest_review_items_are_errors_by_default(self) -> None:
        with temporary_directory() as temp_dir:
            preview = Path(temp_dir) / "InventorySourceGridMarginPreview.png"
            preview.write_bytes(b"preview")
            manifest = Path(temp_dir) / "InventoryIsolatedObjectBakeManifest.json"
            manifest.write_text(
                json.dumps(
                    {
                        "sourceGridMarginPreview": str(preview),
                        "reviewCount": 1,
                        "items": [
                            {
                                "index": 1,
                                "name": "ClippedTool",
                                "status": "REVIEW_SOURCE_CELL_EDGE_MARGIN",
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            errors: list[str] = []
            warnings: list[str] = []

            binding_validator.validate_bake_manifest(manifest, errors, warnings, allow_bake_review=False)

        self.assertTrue(any("source bake manifest has review items" in error for error in errors), errors)
        self.assertEqual([], warnings)

    def test_bake_manifest_review_items_can_be_downgraded_to_warning(self) -> None:
        with temporary_directory() as temp_dir:
            preview = Path(temp_dir) / "InventorySourceGridMarginPreview.png"
            preview.write_bytes(b"preview")
            manifest = Path(temp_dir) / "InventoryIsolatedObjectBakeManifest.json"
            manifest.write_text(
                json.dumps(
                    {
                        "sourceGridMarginPreview": str(preview),
                        "reviewCount": 1,
                        "items": [{"index": 1, "name": "ClippedTool", "status": "REVIEW"}],
                    }
                ),
                encoding="utf-8",
            )
            errors: list[str] = []
            warnings: list[str] = []

            binding_validator.validate_bake_manifest(manifest, errors, warnings, allow_bake_review=True)

        self.assertEqual([], errors)
        self.assertTrue(any("source bake manifest has review items" in warning for warning in warnings), warnings)

    def test_bake_manifest_without_source_preview_is_error(self) -> None:
        with temporary_directory() as temp_dir:
            manifest = Path(temp_dir) / "InventoryIsolatedObjectBakeManifest.json"
            manifest.write_text(
                json.dumps({"reviewCount": 0, "items": [{"index": 1, "name": "CleanTool", "status": "OK"}]}),
                encoding="utf-8",
            )
            errors: list[str] = []
            warnings: list[str] = []

            binding_validator.validate_bake_manifest(manifest, errors, warnings, allow_bake_review=False)

        self.assertTrue(any("missing sourceGridMarginPreview" in error for error in errors), errors)
        self.assertEqual([], warnings)

    def test_binding_approval_helper_accepts_only_explicit_approval(self) -> None:
        self.assertFalse(binding_validator.binding_is_approved({"enabled": True}))
        self.assertFalse(binding_validator.binding_is_approved({"enabled": True, "reviewStatus": "PENDING_VISUAL_REVIEW"}))
        self.assertFalse(binding_validator.binding_is_approved({"enabled": True, "approved": True}))
        self.assertTrue(
            binding_validator.binding_is_approved(
                {
                    "enabled": True,
                    "approved": True,
                    "reviewedBy": "unit-test",
                    "reviewedAt": "2026-06-07T08:00:00Z",
                    "reviewNote": "preview accepted",
                }
            )
        )
        self.assertTrue(
            binding_validator.binding_is_approved(
                {
                    "enabled": True,
                    "reviewStatus": "APPROVED",
                    "reviewedBy": "unit-test",
                    "reviewedAt": "2026-06-07T08:00:00Z",
                    "reviewNote": "preview accepted",
                }
            )
        )


class InventoryIconBindingMapFromGapAuditTests(unittest.TestCase):
    def test_generated_bindings_start_pending_with_empty_review_metadata(self) -> None:
        with temporary_directory() as temp_dir:
            root = Path(temp_dir)
            spec = root / "spec.json"
            spec.write_text(
                json.dumps(
                    {
                        "items": [
                            {
                                "index": 1,
                                "persistentId": "Item_Tool_Scanner",
                                "asset": "Assets/_Project/Data/Items/Tools/Item_Tool_Scanner.asset",
                                "safeName": "Scanner",
                                "promptPhrase": "scanner prop",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            output = root / "InventoryIconCandidateBindingMap.json"
            args = Namespace(
                previous_binding_map=None,
                spec_json=spec,
                alpha_root=root / "Alpha512",
                output=output,
                stem_prefix="DRAFT_TX_Batch32_InventoryGap",
                limit=1,
                require_sprites=False,
            )

            self.assertEqual(0, binding_map_gen.generate(args))
            payload = json.loads(output.read_text(encoding="utf-8"))
            binding = payload["bindings"][0]

        self.assertTrue(binding["enabled"])
        self.assertFalse(binding["approved"])
        self.assertEqual("PENDING_VISUAL_REVIEW", binding["reviewStatus"])
        self.assertEqual("", binding["reviewedBy"])
        self.assertEqual("", binding["reviewedAt"])
        self.assertEqual("", binding["reviewNote"])


class InventoryIconReviewMapTests(unittest.TestCase):
    def test_reject_persistent_id_disables_and_clears_target(self) -> None:
        with temporary_directory() as temp_dir:
            binding_map = Path(temp_dir) / "InventoryIconCandidateBindingMap.json"
            binding_map.write_text(
                json.dumps(
                    {
                        "bindings": [
                            {
                                "enabled": True,
                                "approved": False,
                                "reviewStatus": "PENDING_VISUAL_REVIEW",
                                "persistentId": "Data_TitaniumScrap",
                                "itemAsset": "Assets/_Project/Data/Items/Resources/Data_TitaniumScrap.asset",
                                "spriteAsset": "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch32/Alpha512/DRAFT_TX_Batch32_InventoryGap_03_TitaniumScrap_Alpha512.png",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            args = Namespace(
                map=binding_map,
                approve_all_enabled=False,
                approve_persistent_id=[],
                reject_persistent_id=["Data_TitaniumScrap"],
                reviewer="unit-test",
                reason="cropped source cell",
            )

            self.assertEqual(0, review_map.apply_reviews(args))
            payload = json.loads(binding_map.read_text(encoding="utf-8"))
            binding = payload["bindings"][0]

        self.assertFalse(binding["enabled"])
        self.assertFalse(binding["approved"])
        self.assertEqual("REJECTED", binding["reviewStatus"])
        self.assertEqual("", binding["persistentId"])
        self.assertEqual("", binding["itemAsset"])
        self.assertEqual("Data_TitaniumScrap", binding["rejectedPersistentId"])
        self.assertEqual("Assets/_Project/Data/Items/Resources/Data_TitaniumScrap.asset", binding["rejectedItemAsset"])
        self.assertEqual("cropped source cell", binding["reviewNote"])

    def test_approve_all_enabled_does_not_approve_disabled_bindings(self) -> None:
        with temporary_directory() as temp_dir:
            binding_map = Path(temp_dir) / "InventoryIconCandidateBindingMap.json"
            binding_map.write_text(
                json.dumps(
                    {
                        "bindings": [
                            {
                                "enabled": True,
                                "approved": False,
                                "reviewStatus": "PENDING_VISUAL_REVIEW",
                                "persistentId": "Item_Tool_Scanner",
                                "itemAsset": "Assets/_Project/Data/Items/Tools/Item_Tool_Scanner.asset",
                                "spriteAsset": "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch32/Alpha512/DRAFT_TX_Batch32_InventoryGap_08_Scanner_Alpha512.png",
                            },
                            {
                                "enabled": False,
                                "approved": False,
                                "reviewStatus": "REJECTED",
                                "persistentId": "",
                                "itemAsset": "",
                                "spriteAsset": "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch32/Alpha512/DRAFT_TX_Batch32_InventoryGap_03_TitaniumScrap_Alpha512.png",
                            },
                        ]
                    }
                ),
                encoding="utf-8",
            )
            args = Namespace(
                map=binding_map,
                approve_all_enabled=True,
                approve_persistent_id=[],
                reject_persistent_id=[],
                reviewer="unit-test",
                reason="preview accepted",
            )

            self.assertEqual(0, review_map.apply_reviews(args))
            payload = json.loads(binding_map.read_text(encoding="utf-8"))

        self.assertTrue(payload["bindings"][0]["approved"])
        self.assertEqual("APPROVED", payload["bindings"][0]["reviewStatus"])
        self.assertFalse(payload["bindings"][1]["approved"])
        self.assertEqual("REJECTED", payload["bindings"][1]["reviewStatus"])


class InventoryUnityImportStateAuditTests(unittest.TestCase):
    def test_infers_atlas_manifest_from_binding_map_sibling_folder(self) -> None:
        with temporary_directory() as temp_dir:
            batch_root = Path(temp_dir) / "Batch32"
            atlas_root = batch_root / "Atlas"
            atlas_root.mkdir(parents=True)
            binding_map = batch_root / "InventoryIconCandidateBindingMap.json"
            binding_map.write_text("{}", encoding="utf-8")
            manifest = atlas_root / "TX_Batch32_InventoryGenerated_CandidateAtlas_Manifest.json"
            manifest.write_text("{}", encoding="utf-8")

            self.assertEqual(manifest, import_audit.infer_atlas_manifest_path(binding_map))

    def test_expected_atlas_max_size_is_batch_agnostic(self) -> None:
        self.assertEqual(4096, import_audit.expected_atlas_max_size(Path("TX_Batch32_512xCells.png"), 512))
        self.assertEqual(2048, import_audit.expected_atlas_max_size(Path("TX_Batch32_512xCells_256xCells.png"), 512))


class InventoryIsolatedObjectBakerTests(unittest.TestCase):
    def run_baker_for_rect(self, rect: tuple[int, int, int, int]) -> int:
        with temporary_directory() as temp_dir:
            root = Path(temp_dir)
            source = root / "sheet.png"
            image = Image.new("RGBA", (512, 512), (22, 24, 25, 255))
            draw = ImageDraw.Draw(image)
            draw.rounded_rectangle(rect, radius=28, fill=(186, 206, 204, 255), outline=(41, 153, 161, 255), width=8)
            source.parent.mkdir(parents=True, exist_ok=True)
            image.save(source, "PNG")

            args = Namespace(
                source=str(source),
                output=str(root / "out"),
                preset="",
                spec_json="",
                grid_rows=1,
                grid_columns=1,
                names="ClippedTool",
                cell_inset_ratio=0.0,
                stem_prefix="DRAFT_TX_Test",
                size=512,
                padding_ratio=0.13,
                grabcut_iterations=2,
                segmentation_max_side=512,
                source_edge_margin_px=32,
                source_preview_max_side=512,
                min_coverage=0.035,
                max_removed_foreground_coverage=0.018,
                max_removed_bottom_band_coverage=0.002,
                allow_review=False,
                contact_thumb_size=128,
                contact_columns=1,
            )
            return isolated_baker.bake(args)

    def test_centered_object_passes_source_margin_gate(self) -> None:
        self.assertEqual(0, self.run_baker_for_rect((128, 142, 384, 360)))

    def test_object_near_source_cell_edge_fails_before_atlas(self) -> None:
        self.assertEqual(2, self.run_baker_for_rect((0, 142, 318, 360)))

    def test_bake_manifest_records_source_margin_preview(self) -> None:
        with temporary_directory() as temp_dir:
            root = Path(temp_dir)
            source = root / "sheet.png"
            image = Image.new("RGBA", (512, 512), (22, 24, 25, 255))
            draw = ImageDraw.Draw(image)
            draw.rounded_rectangle((128, 142, 384, 360), radius=28, fill=(186, 206, 204, 255))
            image.save(source, "PNG")

            output = root / "out"
            args = Namespace(
                source=str(source),
                output=str(output),
                preset="",
                spec_json="",
                grid_rows=1,
                grid_columns=1,
                names="CleanTool",
                cell_inset_ratio=0.0,
                stem_prefix="DRAFT_TX_Test",
                size=512,
                padding_ratio=0.13,
                grabcut_iterations=2,
                segmentation_max_side=512,
                source_edge_margin_px=32,
                source_preview_max_side=256,
                min_coverage=0.035,
                max_removed_foreground_coverage=0.018,
                max_removed_bottom_band_coverage=0.002,
                allow_review=False,
                contact_thumb_size=128,
                contact_columns=1,
            )

            self.assertEqual(0, isolated_baker.bake(args))
            manifest = json.loads((output / "InventoryIsolatedObjectBakeManifest.json").read_text(encoding="utf-8"))
            preview = isolated_baker.ROOT / manifest["sourceGridMarginPreview"]

            self.assertTrue(preview.exists(), manifest)
            self.assertEqual("STATIC_SOURCE_DRAFT_NO_UNITY_IMPORT", manifest["evidenceClass"])


class InventoryIconReadabilityPreviewTests(unittest.TestCase):
    def test_renders_multiscale_preview_from_binding_map(self) -> None:
        with temporary_directory() as temp_dir:
            root = Path(temp_dir)
            sprite = root / "DRAFT_TX_Test_01_CleanTool_Alpha512.png"
            icon = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
            draw = ImageDraw.Draw(icon)
            draw.rounded_rectangle((28, 20, 100, 108), radius=16, fill=(184, 205, 203, 255))
            icon.save(sprite, "PNG")

            binding_map = root / "InventoryIconCandidateBindingMap.json"
            binding_map.write_text(
                json.dumps(
                    {
                        "bindings": [
                            {
                                "enabled": True,
                                "approved": True,
                                "reviewStatus": "APPROVED",
                                "reviewedBy": "unit-test",
                                "reviewedAt": "2026-06-07T08:00:00Z",
                                "reviewNote": "preview accepted",
                                "persistentId": "Item_Test",
                                "itemAsset": "Assets/_Project/Data/Items/Item_Test.asset",
                                "spriteAsset": str(sprite),
                                "spriteName": "",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            output = root / "readability.png"
            args = Namespace(
                map=str(binding_map),
                output=str(output),
                include_disabled=False,
                sizes="64,32,24",
                columns=1,
                slot_gap=8,
                group_gap=12,
                padding_ratio=0.12,
                page_background=(0, 0, 0, 0),
                slot_background=(6, 20, 23, 255),
                approved_outline=(28, 128, 147, 255),
                pending_outline=(194, 138, 44, 255),
                disabled_outline=(128, 99, 51, 255),
            )

            self.assertEqual(0, readability_preview.render(args))
            self.assertTrue(output.exists())
            with Image.open(output) as preview:
                self.assertEqual((136, 64), preview.size)

    def test_pending_binding_uses_pending_outline(self) -> None:
        with temporary_directory() as temp_dir:
            root = Path(temp_dir)
            sprite = root / "DRAFT_TX_Test_01_PendingTool_Alpha512.png"
            icon = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
            ImageDraw.Draw(icon).ellipse((18, 18, 46, 46), fill=(184, 205, 203, 255))
            icon.save(sprite, "PNG")

            binding_map = root / "InventoryIconCandidateBindingMap.json"
            binding_map.write_text(
                json.dumps(
                    {
                        "bindings": [
                            {
                                "enabled": True,
                                "approved": False,
                                "reviewStatus": "PENDING_VISUAL_REVIEW",
                                "persistentId": "Item_Test",
                                "itemAsset": "Assets/_Project/Data/Items/Item_Test.asset",
                                "spriteAsset": str(sprite),
                                "spriteName": "",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            output = root / "pending_readability.png"
            pending_outline = (194, 138, 44, 255)
            args = Namespace(
                map=str(binding_map),
                output=str(output),
                include_disabled=False,
                sizes="64",
                columns=1,
                slot_gap=8,
                group_gap=12,
                padding_ratio=0.12,
                page_background=(0, 0, 0, 0),
                slot_background=(6, 20, 23, 255),
                approved_outline=(28, 128, 147, 255),
                pending_outline=pending_outline,
                disabled_outline=(128, 99, 51, 255),
            )

            self.assertEqual(0, readability_preview.render(args))
            with Image.open(output) as preview:
                self.assertEqual(pending_outline, preview.convert("RGBA").getpixel((32, 0)))


if __name__ == "__main__":
    unittest.main()
