#!/usr/bin/env python3
from __future__ import annotations

import contextlib
import io
import json
import unittest
from dataclasses import dataclass
from pathlib import Path
from unittest.mock import patch

import AppliedLoreBlobDeltaAudit as blob_delta
import AppliedLorePacketCoverageAudit as packet_coverage
from AppliedLoreImporter import ImportCheckStats
from AppliedLoreSignalRouteAudit import SignalRouteStats
from AppliedLoreIntegrationPreflight import (
    main,
    render_result,
    result_to_json_payload,
    run_preflight,
)
from AppliedLorePageExporter import PublicationCheckStats
from AppliedLoreScenePlacementDeltaAudit import PlacementDelta, PlacementIssue
from ValidateAppliedLoreAuthoringBridge import BridgeStats


@dataclass(frozen=True)
class FakeRow:
    packet_id: str
    locale: str
    release_set_id: str
    line_number: int
    packet_hash: int
    locale_hash: int

    @property
    def key(self) -> tuple[int, int]:
        return self.packet_hash, self.locale_hash


@dataclass(frozen=True)
class FakeRecord:
    packet_hash: int
    locale_hash: int
    index: int = 0

    @property
    def key(self) -> tuple[int, int]:
        return self.packet_hash, self.locale_hash


@dataclass(frozen=True)
class FakeRouteSource:
    route_card_id: str
    route_card_hash: int
    line_number: int


@dataclass(frozen=True)
class FakeRouteRecord:
    route_card_hash: int
    index: int = 0


def row(packet_id: str, locale: str, packet_hash: int, locale_hash: int, line_number: int = 2) -> FakeRow:
    return FakeRow(packet_id, locale, "RS_TEST", line_number, packet_hash, locale_hash)


def record(packet_hash: int, locale_hash: int) -> FakeRecord:
    return FakeRecord(packet_hash, locale_hash)


def route_source(route_card_id: str, route_card_hash: int, line_number: int = 2) -> FakeRouteSource:
    return FakeRouteSource(route_card_id, route_card_hash, line_number)


def route_record(route_card_hash: int) -> FakeRouteRecord:
    return FakeRouteRecord(route_card_hash)


class AppliedLoreIntegrationPreflightTests(unittest.TestCase):
    @contextlib.contextmanager
    def temp_root(self):
        yield Path(__file__).resolve().parents[1]

    def artifact_state(self) -> blob_delta.ArtifactState:
        return blob_delta.ArtifactState(
            csv_path="Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv",
            route_csv_path="Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv",
            blob_path="Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin",
            csv_exists=True,
            route_csv_exists=True,
            blob_exists=True,
            csv_mtime_ns=100,
            route_csv_mtime_ns=100,
            blob_mtime_ns=200,
        )

    def stale_artifact_state(self) -> blob_delta.ArtifactState:
        return blob_delta.ArtifactState(
            csv_path="Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv",
            route_csv_path="Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv",
            blob_path="Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin",
            csv_exists=True,
            route_csv_exists=True,
            blob_exists=True,
            csv_mtime_ns=300,
            route_csv_mtime_ns=400,
            blob_mtime_ns=200,
        )

    def bridge_stats(self, *issues: str) -> BridgeStats:
        return BridgeStats(
            packets=2,
            localized_rows=30,
            runtime_fields=7,
            publication_article_fields=12,
            issues=tuple(issues),
        )

    def clean_blob_stats(self) -> blob_delta.BlobDeltaStats:
        return blob_delta.compute_blob_delta(
            [row("P001", "en_US", 1, 11)],
            [record(1, 11)],
        )

    def clean_route_stats(self) -> blob_delta.RouteDeltaStats:
        return blob_delta.compute_route_delta([], [])

    def clean_inventory_stats(self) -> packet_coverage.InventoryStats:
        return packet_coverage.InventoryStats(
            source_packets=2,
            baked_packets=2,
            unbaked_packets=0,
            baked_missing_source_packets=0,
            canonical_ready_unbaked_packets=0,
            canonical_not_ready_unbaked_packets=0,
            sample_unbaked=(),
            sample_canonical_ready_unbaked=(),
        )

    def clean_import_stats(self) -> ImportCheckStats:
        return ImportCheckStats(checked_files=2, stale_files=0, missing_files=0, sample_issues=())

    def clean_publication_stats(self) -> PublicationCheckStats:
        return PublicationCheckStats(
            checked_files=96,
            stale_files=0,
            missing_files=0,
            disabled_generated_pages=0,
            integrity_issues=0,
            sample_issues=(),
        )

    def clean_scene_delta(self) -> PlacementDelta:
        return PlacementDelta(total_rows=1, covered_rows=1, issues=())

    @contextlib.contextmanager
    def patch_clean_generated_artifacts(self):
        with patch(
            "AppliedLoreIntegrationPreflight.importer.check_import_outputs",
            return_value=self.clean_import_stats(),
        ), patch(
            "AppliedLoreIntegrationPreflight.page_exporter.check_publication_freshness",
            return_value=self.clean_publication_stats(),
        ), patch(
            "AppliedLoreIntegrationPreflight.route_exporter.route_card_export_current",
            return_value=(3, True),
        ):
            yield

    def patch_clean_signal_route(self):
        return patch(
            "AppliedLoreIntegrationPreflight.signal_route.validate_signal_route",
            return_value=SignalRouteStats(checked_files=6, checked_methods=18, issues=()),
        )

    def test_preflight_passes_when_all_gates_are_clean(self) -> None:
        with self.temp_root() as tmp:
            with patch(
                "AppliedLoreIntegrationPreflight.authoring_bridge.validate_authoring_bridge",
                return_value=self.bridge_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.packet_coverage.inventory_sources",
                return_value=self.clean_inventory_stats(),
            ), patch("AppliedLoreIntegrationPreflight.blob_delta.run", return_value=self.clean_blob_stats()), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.run_route",
                return_value=self.clean_route_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.collect_artifact_state",
                return_value=self.artifact_state(),
            ), patch(
                "AppliedLoreIntegrationPreflight.scene_delta.run",
                return_value=self.clean_scene_delta(),
            ), self.patch_clean_generated_artifacts(), self.patch_clean_signal_route():
                result = run_preflight(Path(tmp))

        self.assertTrue(result.clean)
        rendered = render_result(result)
        self.assertIn("integration preflight OK", rendered)
        self.assertIn("authoring_bridge OK", rendered)
        self.assertIn("generated_artifacts OK", rendered)
        self.assertIn("packet_inventory OK", rendered)
        self.assertIn("signal_route OK", rendered)

    def test_preflight_fails_when_generated_artifacts_are_stale(self) -> None:
        stale_import = ImportCheckStats(
            checked_files=2,
            stale_files=1,
            missing_files=0,
            sample_issues=("stale: Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv",),
        )

        with self.temp_root() as tmp:
            with patch(
                "AppliedLoreIntegrationPreflight.authoring_bridge.validate_authoring_bridge",
                return_value=self.bridge_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.importer.check_import_outputs",
                return_value=stale_import,
            ), patch(
                "AppliedLoreIntegrationPreflight.page_exporter.check_publication_freshness",
                return_value=self.clean_publication_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.route_exporter.route_card_export_current",
                return_value=(3, True),
            ), patch(
                "AppliedLoreIntegrationPreflight.packet_coverage.inventory_sources",
                return_value=self.clean_inventory_stats(),
            ), patch("AppliedLoreIntegrationPreflight.blob_delta.run", return_value=self.clean_blob_stats()), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.run_route",
                return_value=self.clean_route_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.collect_artifact_state",
                return_value=self.artifact_state(),
            ), patch(
                "AppliedLoreIntegrationPreflight.scene_delta.run",
                return_value=self.clean_scene_delta(),
            ), self.patch_clean_signal_route():
                result = run_preflight(Path(tmp))

        self.assertFalse(result.clean)
        payload = result_to_json_payload(result)
        generated = payload["gates"]["generated_artifacts"]
        self.assertFalse(generated["clean"])
        self.assertEqual(generated["payload"]["import"]["stale_files"], 1)
        self.assertIn("generated_artifacts FAIL", render_result(result))

    def test_preflight_fails_with_packet_route_and_scene_deltas(self) -> None:
        missing_packet_stats = blob_delta.compute_blob_delta(
            [row("P001", "en_US", 1, 11)],
            [],
        )
        missing_route_stats = blob_delta.compute_route_delta(
            [route_source("R001", 1234)],
            [],
        )
        issue = PlacementIssue(
            line_number=2,
            packet_id="P001",
            component="NarrativeDiscovery",
            reason="object_missing_in_scene",
            scene_path="Assets/_Project/Scenes/02_HECTON_WORLD.unity",
            object_name="AL_DISC_P001",
            source_prefab="Assets/_Project/Prefabs/WorldProceduralProxy/PFB_P001.prefab",
            discovery_id="applied_lore_p001",
            depth_band="mid_depth",
            zone_tag="wreck_field",
            local_position="1|2|3",
        )

        with self.temp_root() as tmp:
            with patch(
                "AppliedLoreIntegrationPreflight.authoring_bridge.validate_authoring_bridge",
                return_value=self.bridge_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.packet_coverage.inventory_sources",
                return_value=self.clean_inventory_stats(),
            ), patch("AppliedLoreIntegrationPreflight.blob_delta.run", return_value=missing_packet_stats), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.run_route",
                return_value=missing_route_stats,
            ), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.collect_artifact_state",
                return_value=self.artifact_state(),
            ), patch(
                "AppliedLoreIntegrationPreflight.scene_delta.run",
                return_value=PlacementDelta(total_rows=1, covered_rows=0, issues=(issue,)),
            ), self.patch_clean_generated_artifacts(), self.patch_clean_signal_route():
                result = run_preflight(Path(tmp), max_packets=1, max_scene_rows=1)

        self.assertFalse(result.clean)
        payload = result_to_json_payload(result)
        self.assertEqual(payload["gates"]["blob_delta"]["payload"]["packets"]["missing_rows"], 1)
        self.assertEqual(payload["gates"]["blob_delta"]["payload"]["routes"]["missing_rows"], 1)
        self.assertEqual(payload["gates"]["scene_placement"]["payload"]["missing"], 1)
        self.assertIn("scene_placement FAIL", render_result(result))

    def test_preflight_fails_when_blob_artifact_is_stale_even_if_keys_match(self) -> None:
        with self.temp_root() as tmp:
            with patch(
                "AppliedLoreIntegrationPreflight.authoring_bridge.validate_authoring_bridge",
                return_value=self.bridge_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.packet_coverage.inventory_sources",
                return_value=self.clean_inventory_stats(),
            ), patch("AppliedLoreIntegrationPreflight.blob_delta.run", return_value=self.clean_blob_stats()), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.run_route",
                return_value=self.clean_route_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.collect_artifact_state",
                return_value=self.stale_artifact_state(),
            ), patch(
                "AppliedLoreIntegrationPreflight.scene_delta.run",
                return_value=self.clean_scene_delta(),
            ), self.patch_clean_generated_artifacts(), self.patch_clean_signal_route():
                result = run_preflight(Path(tmp))

        self.assertFalse(result.clean)
        payload = result_to_json_payload(result)
        blob_gate = payload["gates"]["blob_delta"]
        self.assertFalse(blob_gate["clean"])
        self.assertTrue(blob_gate["payload"]["artifact"]["blob_older_than_csv"])
        self.assertTrue(blob_gate["payload"]["artifact"]["blob_older_than_route_csv"])
        self.assertIn("artifact_current=false", render_result(result))

    def test_preflight_fails_when_canonical_ready_packet_is_not_baked(self) -> None:
        inventory = packet_coverage.InventoryStats(
            source_packets=3,
            baked_packets=2,
            unbaked_packets=1,
            baked_missing_source_packets=0,
            canonical_ready_unbaked_packets=1,
            canonical_not_ready_unbaked_packets=0,
            sample_unbaked=("P_READY_NOT_BAKED",),
            sample_canonical_ready_unbaked=("P_READY_NOT_BAKED",),
        )

        with self.temp_root() as tmp:
            with patch(
                "AppliedLoreIntegrationPreflight.authoring_bridge.validate_authoring_bridge",
                return_value=self.bridge_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.packet_coverage.inventory_sources",
                return_value=inventory,
            ), patch("AppliedLoreIntegrationPreflight.blob_delta.run", return_value=self.clean_blob_stats()), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.run_route",
                return_value=self.clean_route_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.collect_artifact_state",
                return_value=self.artifact_state(),
            ), patch(
                "AppliedLoreIntegrationPreflight.scene_delta.run",
                return_value=self.clean_scene_delta(),
            ), self.patch_clean_generated_artifacts(), self.patch_clean_signal_route():
                result = run_preflight(Path(tmp))

        self.assertFalse(result.clean)
        payload = result_to_json_payload(result)
        self.assertFalse(payload["gates"]["packet_inventory"]["clean"])
        self.assertEqual(
            payload["gates"]["packet_inventory"]["payload"]["sample_canonical_ready_unbaked"],
            ["P_READY_NOT_BAKED"],
        )

    def test_strict_authoring_failure_stays_in_authoring_gate(self) -> None:
        with self.temp_root() as tmp:
            with patch(
                "AppliedLoreIntegrationPreflight.authoring_bridge.validate_authoring_bridge",
                return_value=self.bridge_stats("P001/en_US/title contains LOC HOLD"),
            ), patch(
                "AppliedLoreIntegrationPreflight.packet_coverage.inventory_sources",
                return_value=self.clean_inventory_stats(),
            ), patch("AppliedLoreIntegrationPreflight.blob_delta.run", return_value=self.clean_blob_stats()), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.run_route",
                return_value=self.clean_route_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.collect_artifact_state",
                return_value=self.artifact_state(),
            ), patch(
                "AppliedLoreIntegrationPreflight.scene_delta.run",
                return_value=self.clean_scene_delta(),
            ), self.patch_clean_generated_artifacts(), self.patch_clean_signal_route():
                result = run_preflight(Path(tmp), strict_localized_text=True)

        self.assertFalse(result.clean)
        payload = result_to_json_payload(result)
        self.assertFalse(payload["gates"]["authoring_bridge"]["clean"])
        self.assertEqual(
            payload["gates"]["authoring_bridge"]["payload"]["issues"],
            ["P001/en_US/title contains LOC HOLD"],
        )

    def test_cli_json_writes_stdout_and_preserves_failure_exit_code(self) -> None:
        with self.temp_root() as tmp:
            stdout = io.StringIO()
            stderr = io.StringIO()
            with patch(
                "AppliedLoreIntegrationPreflight.authoring_bridge.validate_authoring_bridge",
                return_value=self.bridge_stats("bad localized text"),
            ), patch(
                "AppliedLoreIntegrationPreflight.packet_coverage.inventory_sources",
                return_value=self.clean_inventory_stats(),
            ), patch("AppliedLoreIntegrationPreflight.blob_delta.run", return_value=self.clean_blob_stats()), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.run_route",
                return_value=self.clean_route_stats(),
            ), patch(
                "AppliedLoreIntegrationPreflight.blob_delta.collect_artifact_state",
                return_value=self.artifact_state(),
            ), patch(
                "AppliedLoreIntegrationPreflight.scene_delta.run",
                return_value=self.clean_scene_delta(),
            ), self.patch_clean_generated_artifacts(), self.patch_clean_signal_route(), contextlib.redirect_stdout(
                stdout
            ), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", str(tmp), "--json", "--strict-localized-text"])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stderr.getvalue(), "")
        payload = json.loads(stdout.getvalue())
        self.assertFalse(payload["clean"])
        self.assertIn("bad localized text", payload["gates"]["authoring_bridge"]["payload"]["issues"])


if __name__ == "__main__":
    unittest.main()
