#!/usr/bin/env python3
"""Unit tests for terrain probe evidence classification."""

from __future__ import annotations

import subprocess
import sys
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from test_local_temp import project_local_tempdir_factory  # noqa: E402

import ValidateTerrainProbeEvidence as validator  # noqa: E402

temporary_directory = project_local_tempdir_factory("terrain_probe_evidence_tests")


PROBE_L_LOG = REPO_ROOT / "Docs" / "Logs" / "UnityCaptureSurfaceCrestActualTerrainProbeL_20260606_022301.log"
PROBE_L_METADATA = REPO_ROOT / "Docs" / "Screenshots" / "MCP" / "h8_1914_surface_crest_recovery_probe.txt"
VISUAL_CAPTURE_SOURCE = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "Editor" / "H8VisualProofCapture1912.cs"


class TerrainProbeEvidenceTests(unittest.TestCase):
    def test_probe_l_is_classified_rejected(self) -> None:
        if not PROBE_L_LOG.exists() or not PROBE_L_METADATA.exists():
            self.skipTest("ProbeL artifacts are not present in this workspace state")

        evidence = validator.classify(
            PROBE_L_LOG.read_text(encoding="utf-8-sig", errors="replace"),
            PROBE_L_METADATA.read_text(encoding="utf-8-sig", errors="replace"),
            (str(PROBE_L_LOG), str(PROBE_L_METADATA)),
        )
        self.assertEqual("TERRAIN_PROBE_EVIDENCE_REJECTED", evidence.status)
        joined = "\n".join(evidence.blockers)
        self.assertIn("unity-memory-leaks", joined)
        self.assertIn("editor-only-unsaved", joined)
        self.assertIn("h8-1914-diagnostic", joined)
        self.assertIn("erosion-disabled", joined)
        self.assertIn("anomaly-disabled", joined)
        self.assertIn("anomaly-height-unlinked", joined)
        self.assertIn("splat-sediment-unlinked", joined)

    def test_clean_production_probe_is_accepted(self) -> None:
        log = "\n".join(
            [
                "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
            ]
        )
        metadata = "\n".join(
            [
                "captureTruth=surface_actual_terrain_crest_recovery_production",
                "  erosion=type=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode enabled=True",
                "  anomaly=type=MapMagic.Nodes.MatrixGenerators.HectonAnomalyMapMagicNode enabled=True",
                "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                "  link splat.sedimentIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                "  link anomaly.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
            ]
        )
        evidence = validator.classify(log, metadata, ("Docs/Screenshots/MCP/h8_surface_probe.png",))
        self.assertTrue(evidence.is_production_ready)
        self.assertEqual("TERRAIN_PROBE_EVIDENCE_ACCEPTED", evidence.status)

    def test_cli_require_production_accepts_clean_packet(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text(
                "\n".join(
                    [
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
                    ]
                ),
                encoding="utf-8",
            )
            metadata.write_text(
                "\n".join(
                    [
                        "captureTruth=surface_actual_terrain_crest_recovery_production",
                        "  erosion=type=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode enabled=True",
                        "  anomaly=type=MapMagic.Nodes.MatrixGenerators.HectonAnomalyMapMagicNode enabled=True",
                        "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.sedimentIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link anomaly.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                    ]
                ),
                encoding="utf-8",
            )
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(0, result.returncode)
        self.assertIn("TERRAIN_PROBE_EVIDENCE_ACCEPTED blockers=0", result.stdout)

    def test_memory_payload_rejects_production_requirement(self) -> None:
        evidence = validator.classify('##utp:{"type":"MemoryLeaks","version":2}', "")
        self.assertFalse(evidence.is_production_ready)
        self.assertIn("unity-memory-leaks", "\n".join(evidence.blockers))

    def test_completed_capture_with_memory_payload_and_disabled_graph_is_rejected(self) -> None:
        log = "\n".join(
            [
                "*** Tundra build success",
                "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png bytes=1407261",
                "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.txt",
                '##utp:{"type":"MemoryLeaks","version":2}',
            ]
        )
        metadata = "\n".join(
            [
                "captureTruth=surface_actual_terrain_crest_recovery_probe_editor_only_unsaved",
                "  erosion=type=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode enabled=False",
                "  anomaly=type=MapMagic.Nodes.MatrixGenerators.HectonAnomalyMapMagicNode enabled=False",
                "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonBiomeMatrixMapMagicPostProcessNode",
                "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonBiomeMatrixMapMagicPostProcessNode",
                "  link splat.sedimentIn=UNLINKED linkedOutletId=0",
                "  link anomaly.heightIn=UNLINKED linkedOutletId=0",
            ]
        )
        evidence = validator.classify(log, metadata)
        joined = "\n".join(evidence.blockers)
        self.assertFalse(evidence.is_production_ready)
        self.assertIn("unity-memory-leaks", joined)
        self.assertNotIn("capture-output-missing", joined)
        self.assertIn("erosion-disabled", joined)
        self.assertIn("anomaly-disabled", joined)
        self.assertIn("height-output-not-eroded", joined)
        self.assertIn("splat-height-not-eroded", joined)

    def test_compile_poison_rejects_production_requirement(self) -> None:
        log = "\n".join(
            [
                "[ ] Modification date of `Assets\\_Project\\Scripts\\SeamGapDitherRenderer.cs` changed while running `Csc Library/Bee/artifacts/Hecton8.Core.dll`.",
                "Assets\\_Project\\Scripts\\SeamGapDitherRenderer.cs(322,21): error CS0103: The name '_registeredToDispatcher' does not exist in the current context",
                "*** Tundra build failed (35.74 seconds), 2 items updated, 3801 evaluated",
                "Editor compiler errors found. Will not reload assemblies.",
            ]
        )
        evidence = validator.classify(log, "")
        joined = "\n".join(evidence.blockers)
        self.assertFalse(evidence.is_production_ready)
        self.assertIn("compile-input-mutated", joined)
        self.assertIn("compile-error", joined)
        self.assertIn("tundra-build-failed", joined)
        self.assertIn("editor-compiler-errors", joined)

    def test_disabled_nodes_reject_production_requirement(self) -> None:
        metadata = "\n".join(
            [
                "captureTruth=surface_actual_terrain_crest_recovery_production",
                "  erosion=type=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode enabled=False",
                "  anomaly=type=MapMagic.Nodes.MatrixGenerators.HectonAnomalyMapMagicNode enabled=False",
                "  link anomaly.heightIn=UNLINKED linkedOutletId=0",
                "  link splat.sedimentIn=UNLINKED linkedOutletId=0",
            ]
        )
        evidence = validator.classify("", metadata)
        joined = "\n".join(evidence.blockers)
        self.assertIn("erosion-disabled", joined)
        self.assertIn("anomaly-disabled", joined)
        self.assertIn("anomaly-height-unlinked", joined)
        self.assertIn("splat-sediment-unlinked", joined)

    def test_non_eroded_graph_links_reject_production_requirement(self) -> None:
        metadata = "\n".join(
            [
                "captureTruth=surface_actual_terrain_crest_recovery_production",
                "  erosion=type=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode enabled=True",
                "  anomaly=type=MapMagic.Nodes.MatrixGenerators.HectonAnomalyMapMagicNode enabled=True",
                "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonBiomeMatrixMapMagicPostProcessNode",
                "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonBiomeMatrixMapMagicPostProcessNode",
                "  link splat.sedimentIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonTerrainSplatmapMapMagicNode",
                "  link anomaly.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonBiomeMatrixMapMagicPostProcessNode",
            ]
        )
        evidence = validator.classify("", metadata)
        joined = "\n".join(evidence.blockers)
        self.assertIn("height-output-not-eroded", joined)
        self.assertIn("splat-height-not-eroded", joined)
        self.assertIn("splat-sediment-not-eroded", joined)
        self.assertIn("anomaly-height-not-eroded", joined)

    def test_capture_invocation_without_outputs_rejects_production_requirement(self) -> None:
        log = "Hecton8.Editor.H8VisualProofCapture1912.CaptureSurfaceCrestRecoveryProbeAndExit\n"
        evidence = validator.classify(log, "")
        self.assertFalse(evidence.is_production_ready)
        self.assertIn("capture-output-missing", "\n".join(evidence.blockers))

    def test_surface_crest_probe_source_forces_crest_cleanup_before_exit(self) -> None:
        source = VISUAL_CAPTURE_SOURCE.read_text(encoding="utf-8-sig", errors="replace")
        if "private static void CaptureSurfaceCrestProbeAndExit(" not in source:
            self.assertIn('WriteDisabledDiagnosticRouteAndExit("disabled_legacy_surface_crest_recovery_probe")', source)
            return

        self.assertIn("CleanupSurfaceCrestRecoveryProbe();\n                EditorApplication.Exit(exitCode);", source)
        self.assertIn('SetSerializedBool(serialized, "_debug._destroyResourcesInOnDisable", true);', source)
        self.assertIn("behaviour.enabled = false;", source)
        self.assertIn("PumpEditorLoop(0.10d);", source)
        self.assertIn("UnityEngine.Object.DestroyImmediate(_surfaceCrestProbeMaterial);", source)

    def test_surface_crest_probe_source_wires_skycard_horizon_parameter(self) -> None:
        source = VISUAL_CAPTURE_SOURCE.read_text(encoding="utf-8-sig", errors="replace")
        self.assertIn("CaptureSurfaceCrestSkyCardHorizonProbeAndExit", source)
        skycard_route_disabled = 'WriteDisabledDiagnosticRouteAndExit("h8_1919_surface_crest_skycard_horizon_probe")' in source
        helper_present = "private static void CaptureSurfaceCrestProbeAndExit(" in source
        if skycard_route_disabled and not helper_present:
            self.assertIn('WriteDisabledDiagnosticRouteAndExit("h8_1919_surface_crest_skycard_horizon_probe")', source)
            return

        self.assertRegex(source, r"bool\s+disableSurfaceSkyCardsForHorizonProbe[\),]")
        self.assertIn("DisableSurfaceSkyCardsForHorizonProbe();", source)
        if skycard_route_disabled:
            self.assertGreaterEqual(source.count("disableSurfaceSkyCardsForHorizonProbe:"), 5)
        else:
            self.assertIn("disableSurfaceSkyCardsForHorizonProbe: true", source)
            self.assertGreaterEqual(source.count("disableSurfaceSkyCardsForHorizonProbe:"), 6)

    def test_cli_require_production_fails_on_rejected_evidence(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text('##utp:{"type":"MemoryLeaks","version":2}', encoding="utf-8")
            metadata.write_text("captureTruth=surface_actual_terrain_crest_recovery_production", encoding="utf-8")
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("TERRAIN_PROBE_EVIDENCE_REJECTED", result.stdout)

    def test_cli_missing_log_is_rejected_evidence(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            missing_log = root / "missing.log"
            metadata = root / "probe.txt"
            metadata.write_text("captureTruth=surface_actual_terrain_crest_recovery_production", encoding="utf-8")
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(missing_log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("TERRAIN_PROBE_EVIDENCE_REJECTED", result.stdout)
        self.assertIn("missing-log", result.stdout)

    def test_cli_require_production_without_metadata_is_rejected(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            log.write_text(
                "\n".join(
                    [
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
                    ]
                ),
                encoding="utf-8",
            )
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("missing-metadata", result.stdout)

    def test_cli_require_production_without_capture_outputs_is_rejected(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text("*** Tundra build success", encoding="utf-8")
            metadata.write_text(
                "\n".join(
                    [
                        "captureTruth=surface_actual_terrain_crest_recovery_production",
                        "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.sedimentIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link anomaly.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                    ]
                ),
                encoding="utf-8",
            )
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("capture-output-missing", result.stdout)

    def test_cli_require_production_without_capture_truth_is_rejected(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text(
                "\n".join(
                    [
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
                    ]
                ),
                encoding="utf-8",
            )
            metadata.write_text(
                "\n".join(
                    [
                        "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.sedimentIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link anomaly.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                    ]
                ),
                encoding="utf-8",
            )
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("metadata-capture-truth-missing", result.stdout)

    def test_cli_require_production_missing_link_rows_are_rejected(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text(
                "\n".join(
                    [
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
                    ]
                ),
                encoding="utf-8",
            )
            metadata.write_text(
                "\n".join(
                    [
                        "captureTruth=surface_actual_terrain_crest_recovery_production",
                        "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                    ]
                ),
                encoding="utf-8",
            )
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("anomaly-height-not-eroded", result.stdout)
        self.assertIn("splat-sediment-not-eroded", result.stdout)

    def test_cli_require_production_missing_generator_enabled_rows_are_rejected(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text(
                "\n".join(
                    [
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
                    ]
                ),
                encoding="utf-8",
            )
            metadata.write_text(
                "\n".join(
                    [
                        "captureTruth=surface_actual_terrain_crest_recovery_production",
                        "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.sedimentIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link anomaly.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                    ]
                ),
                encoding="utf-8",
            )
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("erosion-enabled-row-missing", result.stdout)
        self.assertIn("anomaly-enabled-row-missing", result.stdout)

    def test_cli_require_production_generator_enabled_false_is_rejected(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text(
                "\n".join(
                    [
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
                    ]
                ),
                encoding="utf-8",
            )
            metadata.write_text(
                "\n".join(
                    [
                        "captureTruth=surface_actual_terrain_crest_recovery_production",
                        "  erosion=type=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode enabled=False",
                        "  anomaly=type=MapMagic.Nodes.MatrixGenerators.HectonAnomalyMapMagicNode enabled=False",
                        "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.sedimentIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link anomaly.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                    ]
                ),
                encoding="utf-8",
            )
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("erosion-disabled", result.stdout)
        self.assertIn("anomaly-disabled", result.stdout)

    def test_cli_require_production_capture_truth_without_production_is_rejected(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text(
                "\n".join(
                    [
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
                    ]
                ),
                encoding="utf-8",
            )
            metadata.write_text(
                "\n".join(
                    [
                        "captureTruth=surface_actual_terrain_crest_recovery",
                        "  erosion=type=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode enabled=True",
                        "  anomaly=type=MapMagic.Nodes.MatrixGenerators.HectonAnomalyMapMagicNode enabled=True",
                        "  link heightOutput.in=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link splat.sedimentIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                        "  link anomaly.heightIn=sourceType=MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode",
                    ]
                ),
                encoding="utf-8",
            )
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("capture-truth-not-production", result.stdout)

    def test_cli_require_production_empty_metadata_is_rejected(self) -> None:
        with temporary_directory(prefix="h8_probe_evidence_") as temp_dir:
            root = Path(temp_dir)
            log = root / "probe.log"
            metadata = root / "probe.txt"
            log.write_text(
                "\n".join(
                    [
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.png bytes=1234",
                        "[H8VisualProofCapture1912] Wrote Docs/Screenshots/MCP/h8_surface_probe.txt",
                    ]
                ),
                encoding="utf-8",
            )
            metadata.write_text("", encoding="utf-8")
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateTerrainProbeEvidence.py"),
                    "--log",
                    str(log),
                    "--metadata",
                    str(metadata),
                    "--require-production",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertEqual(2, result.returncode)
        self.assertIn("metadata-capture-truth-missing", result.stdout)
        self.assertIn("height-output-not-eroded", result.stdout)


if __name__ == "__main__":
    unittest.main()
