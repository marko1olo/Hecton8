#!/usr/bin/env python3
"""Unit tests for static player-route evidence validation."""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidatePlayerRouteStaticEvidence as validator  # noqa: E402


class PlayerRouteStaticEvidenceTests(unittest.TestCase):
    def test_current_project_static_route_is_rejected_or_absent(self) -> None:
        scene = validator.DEFAULT_SCENE
        bootstrap = validator.DEFAULT_BOOTSTRAP
        scene_gate = validator.DEFAULT_SCENE_GATE
        bootstrap_state = validator.DEFAULT_BOOTSTRAP_STATE
        spawner = validator.DEFAULT_SPAWNER
        movement = validator.DEFAULT_PLAYER_MOVEMENT
        interaction = validator.DEFAULT_PLAYER_INTERACTION
        # DEFAULT_WORLD_SHELL is deliberately NOT in this gate. 621403ad5 deleted
        # HectonWorldShellController1428.cs on 2026-06-15 ("1428 file cleanup"), and because
        # `shell.exists()` used to be a skip condition, THIS TEST HAS BEEN DORMANT EVER SINCE - six
        # weeks of `OK (skipped=1)` reading as a pass. Worse, its assertions below still named the
        # six pre-fix blocker keys, so un-skipping it would have failed on CORRECT behaviour and
        # invited someone to "fix" the validator back. A skip gate must name only the files the test
        # actually needs; the validator itself already treats the shell as optional.
        shell = validator.DEFAULT_WORLD_SHELL
        if not scene.exists() or not bootstrap.exists() or not scene_gate.exists() or not bootstrap_state.exists() or not spawner.exists() or not movement.exists() or not interaction.exists():
            self.skipTest("Current project player-route files are not present")

        evidence = validator.classify(
            scene.read_text(encoding="utf-8-sig", errors="replace"),
            bootstrap.read_text(encoding="utf-8-sig", errors="replace"),
            spawner.read_text(encoding="utf-8-sig", errors="replace"),
            scene_gate_text=scene_gate.read_text(encoding="utf-8-sig", errors="replace"),
            bootstrap_state_text=bootstrap_state.read_text(encoding="utf-8-sig", errors="replace"),
            player_movement_text=movement.read_text(encoding="utf-8-sig", errors="replace"),
            player_interaction_text=interaction.read_text(encoding="utf-8-sig", errors="replace"),
            world_shell_text=shell.read_text(encoding="utf-8-sig", errors="replace") if shell.exists() else "",
            player_prefab_text=validator.DEFAULT_PLAYER_PREFAB.read_text(encoding="utf-8-sig", errors="replace"),
            hud_internal_prefab_text=validator.DEFAULT_HUD_INTERNAL_PREFAB.read_text(encoding="utf-8-sig", errors="replace"),
            suit_hud_canvas_prefab_text=validator.DEFAULT_SUIT_HUD_CANVAS_PREFAB.read_text(encoding="utf-8-sig", errors="replace"),
            # The raw bytes, or the whole point is lost: read_text uses errors="replace", so every
            # non-UTF8 byte of the BINARY world scene becomes U+FFFD and no GUID can be found. Passing
            # only the mangled text is what let this validator report six false blockers for weeks.
            scene_bytes=scene.read_bytes(),
        )
        self.assertEqual("PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED", evidence.status)
        joined = "\n".join(evidence.blockers)
        # These six assertions used to name the pre-fix keys and were WRONG in both directions:
        # four of them were false blockers the validator emitted because it text-searched a binary
        # scene, and two were real findings under a misleading name. Corrected to what the tool now
        # reports, and split by which kind of claim each one is.
        joined_notes = chr(10).join(evidence.notes)

        # Retired blockers: these are now POSITIVE notes, and asserting their absence from the blocker
        # list is the actual regression guard. Player.prefab IS referenced by the scene, and the two
        # script GUIDs ARE bound on the prefab; a component carried by a prefab instance emits no scene
        # entry unless overridden, so scene-absence was Unity behaving correctly.
        self.assertNotIn("scene-missing-production-prefab-guid", joined)
        self.assertNotIn("scene-missing-player-movement-guid", joined)
        self.assertNotIn("scene-missing-player-interaction-guid", joined)
        self.assertIn("scene-production-prefab-guid", joined_notes)
        self.assertIn("player-movement-component", joined_notes)
        self.assertIn("player-interaction-component", joined_notes)

        # Re-aimed blockers: still real, asked of the artifact that can answer. HUD_Internal.prefab and
        # Suit_HUD_Canvas.prefab are referenced by neither the scene nor Player.prefab, and neither
        # carries a GameObject with their root name.
        self.assertIn("hud-internal-prefab-unreferenced-by-player-route", joined)
        self.assertIn("suit-hud-canvas-prefab-unreferenced-by-player-route", joined)
        self.assertIn("player-prefab-pda-null-panel-route", joined)
        self.assertIn("player-prefab-pda-null-tab-route", joined)
        self.assertNotIn("player-prefab-pause-menu-null-route", joined)
        self.assertNotIn("player-prefab-swim-contract-null-route", joined)
        self.assertIn("player-prefab-hud-presentation-null-route", joined)
        self.assertIn("player-prefab-hud-render-camera-disabled-or-unbound", joined)
        self.assertIn("player-prefab-hud-extension-null-route", joined)
        self.assertIn("hud-internal-compositor-disabled", joined)
        self.assertIn("hud-internal-compositor-null-route", joined)
        self.assertIn("hud-internal-force-overlay-route", joined)
        self.assertIn("suit-hud-canvas-overlay-render-mode", joined)
        self.assertIn("suit-hud-canvas-null-runtime-route", joined)
        self.assertNotIn("runtime-missing-production-prefab-guid", joined)
        self.assertNotIn("player-interaction-hatch-fallback-prompt", joined)
        self.assertNotIn("bootstrap-publish-player-without-production-validation", joined)
        self.assertNotIn("bootstrap-tagged-shell-acceptance-route", joined)
        self.assertNotIn("bootstrap-mark-player-instantiated-without-production-validation", joined)
        self.assertNotIn("spawner-bootstrap-transform-rigidbody-fallback-route", joined)
        notes = "\n".join(evidence.notes)
        self.assertIn("runtime-production-player-authority-guard", notes)
        self.assertIn("runtime-production-prefab-guid", notes)
        self.assertIn("player-prefab-dev-pause-smoke-null-note", notes)
        self.assertIn("player-prefab-builder-swim-contract-null-readback-note", notes)
        self.assertIn("player-prefab-suit-advisory-runtime-ref-null-note", notes)

    def test_synthetic_production_route_passes_static_gate(self) -> None:
        scene = "\n".join(
            [
                "m_Name: Player",
                "m_TagString: Player",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.PLAYER_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.HUD_INTERNAL_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.SUIT_HUD_CANVAS_PREFAB_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.HECTON_PLAYER_MOVEMENT_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.PLAYER_INTERACTION_GUID}, type: 3}}",
            ]
        )
        prefab_route = (
            f"private const string ProductionPlayerPrefabGuid = \"{validator.PLAYER_PREFAB_GUID}\"; "
            "private GameObject productionPlayerPrefab; "
            "Instantiate(productionPlayerPrefab); "
            "TryAcceptProductionPlayerTransform(default, out _, out _); "
            "SpawnPlayerAsync();"
        )
        bootstrap = "SpawnPlayerAsync();"
        spawner = prefab_route
        evidence = validator.classify(scene, bootstrap, spawner)
        self.assertTrue(evidence.is_static_route_visible)
        self.assertEqual("PLAYER_ROUTE_STATIC_EVIDENCE_PASS", evidence.status)

    def test_guid_string_without_prefab_source_route_is_rejected(self) -> None:
        scene = "\n".join(
            [
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.PLAYER_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.HUD_INTERNAL_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.SUIT_HUD_CANVAS_PREFAB_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.HECTON_PLAYER_MOVEMENT_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.PLAYER_INTERACTION_GUID}, type: 3}}",
            ]
        )
        bootstrap = f"private const string PlayerPrefabGuid = \"{validator.PLAYER_PREFAB_GUID}\"; SpawnPlayerAsync();"
        spawner = "SpawnPlayerAsync();"

        evidence = validator.classify(scene, bootstrap, spawner)

        self.assertEqual("PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED", evidence.status)
        self.assertIn("runtime-missing-production-prefab-guid", "\n".join(evidence.blockers))

    def test_hatch_specific_default_prompt_is_rejected(self) -> None:
        scene = "\n".join(
            [
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.PLAYER_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.HUD_INTERNAL_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.SUIT_HUD_CANVAS_PREFAB_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.HECTON_PLAYER_MOVEMENT_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.PLAYER_INTERACTION_GUID}, type: 3}}",
            ]
        )
        bootstrap = "SpawnPlayerAsync();"
        spawner = (
            f"private const string ProductionPlayerPrefabGuid = \"{validator.PLAYER_PREFAB_GUID}\"; "
            "private GameObject productionPlayerPrefab; "
            "Instantiate(productionPlayerPrefab); "
            "TryAcceptProductionPlayerTransform(default, out _, out _); "
            "SpawnPlayerAsync();"
        )
        evidence = validator.classify(
            scene,
            bootstrap,
            spawner,
            player_interaction_text='private const string DefaultLookTargetPrompt = "OPEN HATCH";',
        )

        self.assertEqual("PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED", evidence.status)
        self.assertIn("player-interaction-hatch-fallback-prompt", "\n".join(evidence.blockers))

    def test_synthetic_null_ui_and_swim_routes_are_rejected(self) -> None:
        scene = "\n".join(
            [
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.PLAYER_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.HUD_INTERNAL_PREFAB_GUID}, type: 3}}",
                f"m_SourcePrefab: {{fileID: 100100000, guid: {validator.SUIT_HUD_CANVAS_PREFAB_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.HECTON_PLAYER_MOVEMENT_GUID}, type: 3}}",
                f"m_Script: {{fileID: 11500000, guid: {validator.PLAYER_INTERACTION_GUID}, type: 3}}",
            ]
        )
        bootstrap = "SpawnPlayerAsync();"
        spawner = (
            f"private const string ProductionPlayerPrefabGuid = \"{validator.PLAYER_PREFAB_GUID}\"; "
            "private GameObject productionPlayerPrefab; "
            "Instantiate(productionPlayerPrefab); "
            "TryAcceptProductionPlayerTransform(default, out _, out _); "
            "SpawnPlayerAsync();"
        )
        player_prefab = "\n".join(
            [
                "m_EditorClassIdentifier: Assembly-CSharp::Hecton8.UI.PlayerPDA",
                "pdaPanel: {fileID: 0}",
                "pdaCanvasGroup: {fileID: 0}",
                "tabs:",
                "- {fileID: 0}",
                "- {fileID: 0}",
                "controlsRebindUI: {fileID: 0}",
                "pauseMenu: {fileID: 0}",
                "_swimContract: {fileID: 0}",
                "m_EditorClassIdentifier: Assembly-CSharp::NASAPunk.Visor.SuitHUDPresentationController",
                "overlayModernHud: {fileID: 0}",
                "projectedModernHud: {fileID: 0}",
                "canvasOverlay: {fileID: 0}",
                "projectionSourceOverlay: {fileID: 0}",
                "screenCompositor: {fileID: 0}",
            ]
        )
        hud_internal = "\n".join(
            [
                "m_Enabled: 0",
                "m_EditorClassIdentifier: Assembly-CSharp::NASAPunk.Visor.SuitHUDScreenCompositor",
                "targetCanvas: {fileID: 0}",
                "visorController: {fileID: 0}",
                "forceScreenSpaceOverlay: 1",
            ]
        )
        suit_hud = "\n".join(
            [
                "m_RenderMode: 0",
                "m_EditorClassIdentifier: Assembly-CSharp::Hecton8.UI.SuitHUDV4CanvasOverlay",
                "projectionCamera: {fileID: 0}",
                "survival: {fileID: 0}",
                "playerMovement: {fileID: 0}",
                "underwaterVisuals: {fileID: 0}",
            ]
        )

        evidence = validator.classify(
            scene,
            bootstrap,
            spawner,
            player_prefab_text=player_prefab,
            hud_internal_prefab_text=hud_internal,
            suit_hud_canvas_prefab_text=suit_hud,
        )

        self.assertEqual("PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED", evidence.status)
        joined = "\n".join(evidence.blockers)
        self.assertIn("player-prefab-pda-null-panel-route", joined)
        self.assertIn("player-prefab-pda-null-tab-route", joined)
        self.assertIn("player-prefab-pause-menu-null-route", joined)
        self.assertIn("player-prefab-swim-contract-null-route", joined)
        self.assertIn("hud-internal-compositor-disabled", joined)
        self.assertIn("suit-hud-canvas-null-runtime-route", joined)

    def test_marker_interfaces_require_source_implementations(self) -> None:
        bootstrap_state = "\n".join(
            [
                "IsProductionPlayerAuthorityObject",
                "IsLegacyWorldShellOwned",
                "IBootstrapProductionPlayerMovementAuthority",
                "IBootstrapProductionPlayerInteractionAuthority",
                "IBootstrapLegacyWorldShellOwner",
                "Rigidbody",
            ]
        )

        self.assertFalse(validator.has_production_player_authority_guard(bootstrap_state))
        self.assertTrue(
            validator.has_production_player_authority_guard(
                bootstrap_state,
                player_movement_text="public sealed class HectonPlayerMovement : IBootstrapProductionPlayerMovementAuthority",
                player_interaction_text="public sealed class PlayerInteraction : IBootstrapProductionPlayerInteractionAuthority",
                world_shell_text="public sealed class HectonWorldShellController1428 : IBootstrapLegacyWorldShellOwner",
            )
        )

    def test_cli_missing_file_rejects_when_required(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_player_route_") as temp_dir:
            root = Path(temp_dir)
            scene = root / "missing.unity"
            bootstrap = root / "GameBootstrapper.cs"
            spawner = root / "HectonPlayerSpawner.cs"
            bootstrap.write_text("SpawnPlayerAsync();", encoding="utf-8")
            spawner.write_text("SpawnPlayerAsync();", encoding="utf-8")
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidatePlayerRouteStaticEvidence.py"),
                    "--scene",
                    str(scene),
                    "--bootstrap",
                    str(bootstrap),
                    "--spawner",
                    str(spawner),
                    "--require-production-static",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED", result.stdout)
        self.assertIn("missing-file", result.stdout)

    def test_cli_no_fail_returns_success_for_missing_required_file(self) -> None:
        missing_scene = SCRIPT_DIR / "missing_player_route_scene.unity"

        result = subprocess.run(
            [
                sys.executable,
                str(SCRIPT_DIR / "ValidatePlayerRouteStaticEvidence.py"),
                "--scene",
                str(missing_scene),
                "--require-production-static",
                "--no-fail",
            ],
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, result.returncode)
        self.assertIn("PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED", result.stdout)
        self.assertIn("missing-file", result.stdout)


if __name__ == "__main__":
    unittest.main()
