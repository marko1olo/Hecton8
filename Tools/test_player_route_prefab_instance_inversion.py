#!/usr/bin/env python3
"""Pin the prefab-instance inversion fix in ValidatePlayerRouteStaticEvidence.py.

WHY THIS FILE EXISTS SEPARATELY from test_validate_player_route_static_evidence.py
----------------------------------------------------------------------------------
That file's `test_current_project_static_route_is_rejected_or_absent` asserts the
four inverted blockers this change removes or re-aims:

    scene-missing-hud-internal-prefab-guid
    scene-missing-suit-hud-prefab-guid
    scene-missing-player-movement-guid
    scene-missing-player-interaction-guid

It has been SKIPPED since 2026-06-15 because it gates on
`DEFAULT_WORLD_SHELL.exists()` and HectonWorldShellController1428.cs was deleted
that day - the same deleted file that made the validator itself short-circuit for
six weeks. So those assertions are dormant, not passing. Verified 2026-07-29:
`skipped 'Current project player-route files are not present'`.

This file therefore does two things the older one cannot:
  1. asserts the CORRECTED behaviour, and
  2. gates each test only on the files that test actually reads, so a future file
     deletion cannot silently turn the suite green again.

THE INVERSION, and the control that proves it rather than asserting it
---------------------------------------------------------------------
A component carried by a prefab INSTANCE emits no scene entry unless overridden.
`test_prefab_interior_is_invisible_to_the_scene` demonstrates this against the
real artifacts: `HUD_Render_Camera` is a GameObject inside Player.prefab and
occurs ZERO times in the binary world scene, while `Main Camera` occurs. Any
change that reintroduces a scene-only search for a prefab-borne component will
fail these tests.
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidatePlayerRouteStaticEvidence as validator  # noqa: E402


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


class PrefabInstanceInversionTests(unittest.TestCase):
    """The scene cannot answer questions about a prefab instance's interior."""

    def test_prefab_interior_is_invisible_to_the_scene(self) -> None:
        """The control for the whole fix, measured against the real artifacts."""
        prefab = validator.DEFAULT_PLAYER_PREFAB
        scene = validator.DEFAULT_SCENE
        if not prefab.exists() or not scene.exists():
            self.skipTest("Player.prefab or the world scene is not present")

        prefab_text = read(prefab)
        scene_bytes = scene.read_bytes()

        # HUD_Render_Camera is unambiguously inside Player.prefab.
        self.assertTrue(validator.object_name_present(prefab_text, "HUD_Render_Camera"))

        # A name search against this scene demonstrably works - positive control
        # first, exactly as Tools/SceneGuidReachability.py demands.
        self.assertTrue(
            validator.scene_object_name_present("Main Camera", "", scene_bytes),
            "name probe found no positive control in the scene; a negative from it would prove nothing",
        )

        # And yet the prefab's own child is absent from the scene. That is the
        # inversion: scene absence of a prefab-borne object is expected.
        self.assertFalse(validator.scene_object_name_present("HUD_Render_Camera", "", scene_bytes))

    def test_scene_guid_search_positive_control_holds(self) -> None:
        """Player.prefab's GUID must be findable in the scene bytes.

        This is the control that licenses every scene negative the validator
        reports. If it ever fails, the validator is required to withhold negatives
        rather than blame the assets.
        """
        scene = validator.DEFAULT_SCENE
        if not scene.exists():
            self.skipTest("world scene is not present")
        scene_bytes = scene.read_bytes()
        self.assertTrue(validator.is_binary_scene(scene_bytes), "world scene is expected to be binary")
        self.assertTrue(
            validator.scene_guid_present(validator.PLAYER_PREFAB_GUID, "", scene_bytes),
            "byte-aware GUID search cannot find Player.prefab in the world scene",
        )


class PlayerComponentChecksAskThePrefabTests(unittest.TestCase):
    """HectonPlayerMovement / PlayerInteraction resolve only to Player.prefab."""

    def test_real_player_prefab_carries_both_components(self) -> None:
        prefab = validator.DEFAULT_PLAYER_PREFAB
        if not prefab.exists():
            self.skipTest("Player.prefab is not present")
        prefab_text = read(prefab)
        self.assertTrue(validator.prefab_carries_script(prefab_text, validator.HECTON_PLAYER_MOVEMENT_GUID))
        self.assertTrue(validator.prefab_carries_script(prefab_text, validator.PLAYER_INTERACTION_GUID))

    def test_bare_guid_mention_is_not_a_component(self) -> None:
        """"The GUID occurs" is a weaker claim than "the component is on it"."""
        self.assertFalse(
            validator.prefab_carries_script(
                f"someSerializedField: {validator.HECTON_PLAYER_MOVEMENT_GUID}",
                validator.HECTON_PLAYER_MOVEMENT_GUID,
            )
        )
        self.assertTrue(
            validator.prefab_carries_script(
                f"m_Script: {{fileID: 11500000, guid: {validator.HECTON_PLAYER_MOVEMENT_GUID}, type: 3}}",
                validator.HECTON_PLAYER_MOVEMENT_GUID,
            )
        )

    def test_prefab_borne_components_are_not_blockers(self) -> None:
        """Scene silence + prefab binding = note, never a blocker."""
        scene = f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.PLAYER_PREFAB_GUID}, type: 3}}"
        player_prefab = "\n".join(
            [
                f"m_Script: {{fileID: 11500000, guid: {validator.HECTON_PLAYER_MOVEMENT_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.PLAYER_INTERACTION_GUID}, type: 3}}",
            ]
        )
        evidence = validator.classify(scene, "", "", player_prefab_text=player_prefab)
        joined = "\n".join(evidence.blockers)
        notes = "\n".join(evidence.notes)

        self.assertNotIn("player-movement-component-missing", joined)
        self.assertNotIn("player-interaction-component-missing", joined)
        self.assertNotIn("scene-missing-player-movement-guid", joined)
        self.assertNotIn("scene-missing-player-interaction-guid", joined)
        self.assertIn("player-movement-component: present", notes)
        self.assertIn("player-interaction-component: present", notes)

    def test_component_absent_from_prefab_and_scene_is_a_real_blocker(self) -> None:
        """The fix must not have neutered the check - a real gap still blocks."""
        scene = f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.PLAYER_PREFAB_GUID}, type: 3}}"
        evidence = validator.classify(
            scene, "", "", player_prefab_text="m_Name: Player\nm_Script: {fileID: 11500000, guid: ffff, type: 3}"
        )
        joined = "\n".join(evidence.blockers)
        self.assertIn("player-movement-component-missing", joined)
        self.assertIn("player-interaction-component-missing", joined)

    def test_unread_prefab_is_undecidable_not_missing(self) -> None:
        """No Player.prefab text means the answering artifact was never read."""
        scene = f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.PLAYER_PREFAB_GUID}, type: 3}}"
        evidence = validator.classify(scene, "", "")
        joined = "\n".join(evidence.blockers)
        notes = "\n".join(evidence.notes)
        self.assertNotIn("player-movement-component-missing", joined)
        self.assertIn("player-movement-component: UNDECIDABLE", notes)
        self.assertIn("player-interaction-component: UNDECIDABLE", notes)


class HudPrefabReferenceChecksTests(unittest.TestCase):
    """HUD prefab asset GUIDs: ask the scene AND Player.prefab, not the scene alone."""

    SCENE_WITH_PLAYER = f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.PLAYER_PREFAB_GUID}, type: 3}}"

    def test_nested_inside_player_prefab_counts_as_present(self) -> None:
        """A nested prefab serialises into the PARENT PREFAB and never the scene.

        This is the case the old scene-only check would have called a defect.
        """
        player_prefab = "\n".join(
            [
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.HUD_INTERNAL_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.SUIT_HUD_CANVAS_PREFAB_GUID}, type: 3}}",
            ]
        )
        evidence = validator.classify(
            self.SCENE_WITH_PLAYER, "", "", player_prefab_text=player_prefab
        )
        joined = "\n".join(evidence.blockers)
        notes = "\n".join(evidence.notes)
        self.assertNotIn("hud-internal-prefab-unreferenced-by-player-route", joined)
        self.assertNotIn("suit-hud-canvas-prefab-unreferenced-by-player-route", joined)
        self.assertIn("nested inside Player.prefab", notes)

    def test_unpacked_scene_object_is_undecidable_not_missing(self) -> None:
        """Root name in the scene with no prefab link = unpacked authoring."""
        scene = "\n".join(
            [
                self.SCENE_WITH_PLAYER,
                f"  m_Name: {validator.HUD_INTERNAL_ROOT_NAME}",
                f"  m_Name: {validator.SUIT_HUD_CANVAS_ROOT_NAME}",
            ]
        )
        evidence = validator.classify(scene, "", "", player_prefab_text="m_Name: Player")
        joined = "\n".join(evidence.blockers)
        notes = "\n".join(evidence.notes)
        self.assertNotIn("hud-internal-prefab-unreferenced-by-player-route", joined)
        self.assertNotIn("suit-hud-canvas-prefab-unreferenced-by-player-route", joined)
        self.assertIn("hud-internal-prefab-reference: UNDECIDABLE", notes)

    def test_negative_is_withheld_when_the_search_has_no_positive_control(self) -> None:
        """No Player.prefab GUID anywhere = the search proved nothing.

        Reporting a blocker here would repeat the exact seven-week failure: a
        confident negative from a search never shown able to find a positive.
        """
        evidence = validator.classify("nothing relevant here", "", "", player_prefab_text="m_Name: Player")
        joined = "\n".join(evidence.blockers)
        notes = "\n".join(evidence.notes)
        self.assertNotIn("hud-internal-prefab-unreferenced-by-player-route", joined)
        self.assertIn("hud-internal-prefab-reference: UNDECIDABLE", notes)
        self.assertIn("search is unvalidated", notes)

    def test_genuinely_unreferenced_hud_prefab_still_blocks(self) -> None:
        """The fix must not have lowered the number by deleting the question."""
        evidence = validator.classify(
            self.SCENE_WITH_PLAYER, "", "", player_prefab_text="m_Name: Player"
        )
        joined = "\n".join(evidence.blockers)
        self.assertIn("hud-internal-prefab-unreferenced-by-player-route", joined)
        self.assertIn("suit-hud-canvas-prefab-unreferenced-by-player-route", joined)

    def test_real_project_hud_prefabs_are_unreferenced_and_that_is_earned(self) -> None:
        """The two HUD blockers survive the correction on the real artifacts.

        The premise that all four blockers were false is only half right. These
        two prefabs are absent from every live scene and prefab - Player.prefab
        included, verified by Tools/SceneGuidReachability.py on 2026-07-29 - so
        their blocker is TRUE and was merely worded as a scene question.
        """
        prefab = validator.DEFAULT_PLAYER_PREFAB
        if not prefab.exists():
            self.skipTest("Player.prefab is not present")
        prefab_text = read(prefab)
        for guid, root_name in (
            (validator.HUD_INTERNAL_PREFAB_GUID, validator.HUD_INTERNAL_ROOT_NAME),
            (validator.SUIT_HUD_CANVAS_PREFAB_GUID, validator.SUIT_HUD_CANVAS_ROOT_NAME),
        ):
            self.assertFalse(
                validator.text_references_guid(prefab_text, guid),
                f"{guid} is nested in Player.prefab after all; the blocker wording must change",
            )
            self.assertFalse(validator.object_name_present(prefab_text, root_name))


if __name__ == "__main__":
    unittest.main()
