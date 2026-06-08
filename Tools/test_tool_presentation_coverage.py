#!/usr/bin/env python3
"""Regression tests for tool presentation coverage state classification."""

from __future__ import annotations

import unittest

import ValidateToolPresentationCoverage as coverage


class ToolPresentationCoverageTests(unittest.TestCase):
    def test_binding_review_state_requires_real_approval_metadata(self) -> None:
        self.assertEqual("", coverage.binding_review_state({"enabled": False}))
        self.assertEqual(
            coverage.ICON_STATE_PENDING_REVIEW,
            coverage.binding_review_state({"enabled": True, "approved": True}),
        )
        self.assertEqual(
            coverage.ICON_STATE_REJECTED,
            coverage.binding_review_state({"enabled": True, "reviewStatus": "REJECTED"}),
        )
        self.assertEqual(
            coverage.ICON_STATE_REJECTED,
            coverage.binding_review_state({"enabled": False, "reviewStatus": "REJECTED"}),
        )
        self.assertEqual(
            coverage.ICON_STATE_APPROVED,
            coverage.binding_review_state(
                {
                    "enabled": True,
                    "reviewStatus": "APPROVED",
                    "reviewedBy": "unit-test",
                    "reviewedAt": "2026-06-07T00:00:00Z",
                }
            ),
        )

    def test_missing_icon_classification_routes_next_action(self) -> None:
        pending_specs = {"Item_Tool_Builder", "Item_Tool_Scanner", "Item_Tool_Knife"}
        binding_states = {
            "Item_Tool_Builder": {coverage.ICON_STATE_APPROVED},
            "Item_Tool_Scanner": {coverage.ICON_STATE_PENDING_REVIEW},
            "Item_Tool_Knife": {coverage.ICON_STATE_REJECTED},
            "Item_Tool_StunPistol": {coverage.ICON_STATE_REJECTED},
        }

        self.assertEqual(
            coverage.MISSING_ICON_APPROVED_PENDING_UNITY,
            coverage.classify_missing_icon("Item_Tool_Builder", binding_states, pending_specs),
        )
        self.assertEqual(
            coverage.MISSING_ICON_PENDING_REVIEW,
            coverage.classify_missing_icon("Item_Tool_Scanner", binding_states, pending_specs),
        )
        self.assertEqual(
            coverage.MISSING_ICON_REJECTED_NEEDS_REGENERATION,
            coverage.classify_missing_icon("Item_Tool_Knife", binding_states, pending_specs),
        )
        self.assertEqual(
            coverage.MISSING_ICON_REJECTED_WITHOUT_SPEC,
            coverage.classify_missing_icon("Item_Tool_StunPistol", binding_states, pending_specs),
        )
        self.assertEqual(
            coverage.MISSING_ICON_PENDING_GENERATION,
            coverage.classify_missing_icon("Item_Tool_SalvageSampler", binding_states, pending_specs | {"Item_Tool_SalvageSampler"}),
        )
        self.assertEqual(
            coverage.MISSING_ICON_NO_ROUTE,
            coverage.classify_missing_icon("Item_Tool_EnvAnalyzer", binding_states, pending_specs),
        )

    def test_rejected_disabled_binding_identity_uses_spec_slot(self) -> None:
        spec_items = [
            {"persistentId": "Item_Tool_Builder"},
            {"persistentId": "Item_Tool_Scanner"},
        ]
        rejected_binding = {
            "enabled": False,
            "approved": False,
            "reviewStatus": "REJECTED",
            "persistentId": "",
            "itemAsset": "",
        }
        rejected_with_provenance = {
            "enabled": False,
            "approved": False,
            "reviewStatus": "REJECTED",
            "persistentId": "",
            "rejectedPersistentId": "Item_Tool_SeafloorDrill",
        }

        self.assertEqual(
            "Item_Tool_Scanner",
            coverage.persistent_id_for_binding_state(rejected_binding, 1, spec_items),
        )
        self.assertEqual(
            "Item_Tool_SeafloorDrill",
            coverage.persistent_id_for_binding_state(rejected_with_provenance, 1, spec_items),
        )

    def test_localized_inline_item_fallback_does_not_require_sprite_icon(self) -> None:
        source = coverage.ROOT / "Assets/_Project/Scripts/LocalizedInlineIconResolver.cs"
        text = source.read_text(encoding="utf-8-sig")
        string_block = coverage.source_block(
            text,
            "public static bool TryResolveItemChip(ItemData item",
            "public static bool TryResolveItemChipSpan(ItemData item",
        )
        span_block = coverage.source_block(
            text,
            "public static bool TryResolveItemChipSpan(ItemData item",
            "/// <summary>\n        /// Build a combined inline chip",
        )
        errors: list[str] = []

        coverage.validate_localized_inline_item_fallback(text, errors)

        self.assertEqual([], errors)
        self.assertNotIn("item.icon != null", string_block)
        self.assertNotIn("item.icon != null", span_block)
        self.assertIn("markup = GenericItemChip;", string_block)
        self.assertIn("markup = GenericItemChip.AsSpan();", span_block)

    def test_localized_inline_item_fallback_rejects_sprite_dependent_route(self) -> None:
        bad_text = """
        public static bool TryResolveItemChip(ItemData item, out string markup)
        {
            if (item != null && item.icon != null)
                markup = GenericItemChip;
            markup = string.Empty;
            return false;
        }

        public static bool TryResolveItemChipSpan(ItemData item, out ReadOnlySpan<char> markup)
        {
            if (item != null && item.icon != null)
                markup = GenericItemChip.AsSpan();
            markup = ReadOnlySpan<char>.Empty;
            return false;
        }

        /// <summary>
        /// Build a combined inline chip + localized item name string.
        """
        errors: list[str] = []

        coverage.validate_localized_inline_item_fallback(bad_text, errors)

        self.assertTrue(any("depends on item.icon" in error for error in errors), errors)

    def test_powershell_wrapper_guard_rejects_array_splatting(self) -> None:
        errors: list[str] = []
        fake_runner = "$runnerArgs = @{}\n"
        fake_apply = "$materialArgs = @{}\n$iconArgs = @{}\n$mapIconArgs = $iconArgs.Clone()\n"
        fake_bad = "$intakeArgs = @(\n'-Batch'\n)\n"

        coverage.validate_powershell_wrapper_splatting(
            errors,
            {
                coverage.RUN_INVENTORY_ICON_GEMINI_SHEET_INTAKE: fake_runner,
                coverage.RUN_TOOL_INVENTORY_BATCH33_SHEET_INTAKE: fake_bad,
                coverage.RUN_TOOL_PRESENTATION_UNITY_APPLY: fake_apply,
            },
        )

        self.assertTrue(any("hashtable splatting" in error for error in errors), errors)
        self.assertTrue(any("array splatting" in error for error in errors), errors)


if __name__ == "__main__":
    unittest.main()
