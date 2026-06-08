import csv
import io
import json
import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent))

import AppliedLorePacketCoverageAudit as coverage_audit
from AppliedLoreImporter import TARGET_LOCALES, fnv1a32
from AppliedLorePacketCoverageAudit import AppliedLoreCoverageError, audit_selected_packets, inventory_sources, main
from AppliedLorePageExporter import PUBLICATION_INDEX_HEADERS, publication_surface_rows
from AppliedLoreRouteCardExporter import INPUT_HEADERS as ROUTE_CARD_HEADERS
from AppliedLoreRouteCardExporter import export_route_cards


def localized_row():
    return {
        "title": "Title",
        "scanner": "Scanner text",
        "terminal": "Terminal text",
        "audio": "Audio text",
        "in_game_wiki": "Wiki body",
        "external_site": "Site body",
    }


def write_csv(path: Path, headers: tuple[str, ...], rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def make_repo(root: Path) -> None:
    base = root / "Docs" / "Lore" / "AppliedContent"
    packet = {
        "packet_id": "P_TEST_COVERAGE",
        "release_set_id": "RS_TEST",
        "article_id": "test.coverage",
        "unlock_id": "unlock.test",
        "surface_mask": 48,
        "localized": {locale: localized_row() for locale in TARGET_LOCALES},
    }
    packet_dir = base / "packets"
    packet_dir.mkdir(parents=True, exist_ok=True)
    packet_path = packet_dir / "RS_TEST.packets.json"
    packet_path.write_text(
        json.dumps({"release_set_id": "RS_TEST", "packets": [packet]}),
        encoding="utf-8",
    )
    manifest_path = base / "release_sets" / "RS_TEST_manifest.json"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(
        json.dumps(
            {
                "release_set_id": "RS_TEST",
                "packets": ["P_TEST_COVERAGE"],
                "packet_sources": [packet_path.as_posix()],
                "canonical_importer_ready": True,
            }
        ),
        encoding="utf-8",
    )

    packet_rows = []
    for locale in TARGET_LOCALES:
        packet_rows.append(
            {
                "packet_id": "P_TEST_COVERAGE",
                "locale": locale,
                "release_set_id": "RS_TEST",
                "article_id": "test.coverage",
                "unlock_id": "unlock.test",
                "surface_mask": "48",
                "title": "Title",
                "scanner": "Scanner text",
                "terminal": "Terminal text",
                "audio": "Audio text",
                "in_game_wiki": "Wiki body",
                "external_site": "Site body",
                "field_note": "",
                "poi_tags": "",
                "biome_tags": "",
                "flags": "0",
            }
        )
    write_csv(
        root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv",
        (
            "packet_id",
            "locale",
            "release_set_id",
            "article_id",
            "unlock_id",
            "surface_mask",
            "title",
            "scanner",
            "terminal",
            "audio",
            "in_game_wiki",
            "external_site",
            "field_note",
            "poi_tags",
            "biome_tags",
            "flags",
        ),
        packet_rows,
    )

    publication_packet = dict(packet)
    publication_packet["_source_path"] = str(packet_path.resolve())
    publication_rows = publication_surface_rows(base, [publication_packet])
    write_csv(base / "Publication_Surface_Index.csv", PUBLICATION_INDEX_HEADERS, publication_rows)
    for row in publication_rows:
        page_path = base / row["page_path"]
        page_path.parent.mkdir(parents=True, exist_ok=True)
        page_path.write_text("---\npacket_id: P_TEST_COVERAGE\n---\nBody\n", encoding="utf-8")

    packet_hash = fnv1a32("P_TEST_COVERAGE")
    write_csv(
        base / "binding_maps" / "RS_TEST_runtime_binding_map.csv",
        (
            "packet_id",
            "packet_hash_hex",
            "packet_hash_uint",
            "release_set",
            "primary_component",
            "primary_field",
            "secondary_component",
            "secondary_field",
            "suggested_world_target",
            "unlock_moment",
            "notes",
        ),
        [
            {
                "packet_id": "P_TEST_COVERAGE",
                "packet_hash_hex": f"0x{packet_hash:08X}",
                "packet_hash_uint": str(packet_hash),
                "release_set": "RS_TEST",
                "primary_component": "NarrativeDiscovery",
                "primary_field": "appliedLorePacketHash",
                "secondary_component": "",
                "secondary_field": "",
                "suggested_world_target": "poi.test",
                "unlock_moment": "test",
                "notes": "test",
            }
        ],
    )
    write_csv(
        base / "graphs" / "RS_TEST_evidence_graph.csv",
        (
            "packet_id",
            "arc_id",
            "depth_band",
            "route_moment",
            "prereq_packet_ids",
            "next_packet_ids",
            "evidence_type",
            "truth_claim",
            "player_decision",
            "spoiler_tier",
            "primary_surface",
        ),
        [
            {
                "packet_id": "P_TEST_COVERAGE",
                "arc_id": "test_arc",
                "depth_band": "0-10m",
                "route_moment": "test_moment",
                "prereq_packet_ids": "",
                "next_packet_ids": "",
                "evidence_type": "test proof",
                "truth_claim": "Test claim.",
                "player_decision": "Test question?",
                "spoiler_tier": "0",
                "primary_surface": "in_game_wiki",
            }
        ],
    )
    write_csv(
        base / "route_cards" / "RS_TEST_route_cards.csv",
        ROUTE_CARD_HEADERS,
        [
            {
                "route_card_id": "RC_TEST_COVERAGE",
                "phase_id": "test_phase",
                "depth_min_m": "0",
                "depth_max_m": "10",
                "packet_ids": "P_TEST_COVERAGE",
                "required_packet_ids": "",
                "primary_surface": "in_game_wiki",
                "world_object_hint": "poi.test",
                "player_question": "Test question?",
                "truth_payload": "Test claim.",
                "replay_axis": "test_axis",
                "ending_pressure": "none",
            }
        ],
    )
    export_route_cards(root)


class TestAppliedLorePacketCoverageAudit(unittest.TestCase):
    def test_complete_packet_coverage_passes(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)

            stats = audit_selected_packets(root, ("P_TEST_COVERAGE",))

            self.assertEqual(stats.target_packets, 1)
            self.assertEqual(stats.baked_rows, len(TARGET_LOCALES))
            self.assertEqual(stats.publication_rows, len(TARGET_LOCALES) * 2)
            self.assertEqual(stats.binding_rows, 1)
            self.assertEqual(stats.graph_rows, 1)
            self.assertEqual(stats.route_cards, 1)
            self.assertEqual(stats.route_source_rows, 1)

    def test_missing_binding_map_fails(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)

            real_iter_source_csvs = coverage_audit.iter_source_csvs

            def iter_without_binding_maps(base: Path, folder: str, pattern: str):
                if folder == "binding_maps":
                    return []
                return real_iter_source_csvs(base, folder, pattern)

            with patch(
                "AppliedLorePacketCoverageAudit.iter_source_csvs",
                side_effect=iter_without_binding_maps,
            ), self.assertRaises(AppliedLoreCoverageError):
                audit_selected_packets(root, ("P_TEST_COVERAGE",))

    def test_main_reports_missing_selector_without_traceback(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            output = io.StringIO()

            with patch(
                "sys.argv",
                [
                    "AppliedLorePacketCoverageAudit.py",
                    "--root",
                    str(root),
                    "--packet-id",
                    "P_DOES_NOT_EXIST",
                ],
            ), patch("sys.stdout", output):
                code = main()

            self.assertEqual(code, 1)
            self.assertIn("Selected packet ids not found: P_DOES_NOT_EXIST", output.getvalue())
            self.assertNotIn("Traceback", output.getvalue())

    def test_route_card_source_only_required_packet_is_pruned_from_runtime_audit(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            base = root / "Docs" / "Lore" / "AppliedContent"
            packet_path = base / "packets" / "RS_TEST.packets.json"
            source = json.loads(packet_path.read_text(encoding="utf-8"))
            source["packets"].append(
                {
                    "packet_id": "P_SOURCE_ONLY_PREREQ",
                    "release_set_id": "RS_TEST",
                    "article_id": "test.source_only",
                    "unlock_id": "unlock.source_only",
                    "surface_mask": 48,
                    "localized": {locale: localized_row() for locale in TARGET_LOCALES},
                }
            )
            packet_path.write_text(json.dumps(source), encoding="utf-8")
            write_csv(
                base / "route_cards" / "RS_TEST_route_cards.csv",
                ROUTE_CARD_HEADERS,
                [
                    {
                        "route_card_id": "RC_TEST_COVERAGE",
                        "phase_id": "test_phase",
                        "depth_min_m": "0",
                        "depth_max_m": "10",
                        "packet_ids": "P_TEST_COVERAGE",
                        "required_packet_ids": "P_SOURCE_ONLY_PREREQ",
                        "primary_surface": "in_game_wiki",
                        "world_object_hint": "poi.test",
                        "player_question": "Test question?",
                        "truth_payload": "Test claim.",
                        "replay_axis": "test_axis",
                        "ending_pressure": "none",
                    }
                ],
            )

            export_route_cards(root)

            stats = audit_selected_packets(root, ("P_TEST_COVERAGE",))

            self.assertEqual(stats.route_cards, 1)

    def test_unselected_draft_route_card_refs_do_not_fail_all_scope(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            base = root / "Docs" / "Lore" / "AppliedContent"
            write_csv(
                base / "route_cards" / "RS_DRAFT_route_cards.csv",
                ROUTE_CARD_HEADERS,
                [
                    {
                        "route_card_id": "RC_DRAFT_ONLY",
                        "phase_id": "draft_phase",
                        "depth_min_m": "0",
                        "depth_max_m": "10",
                        "packet_ids": "P_DRAFT_NOT_BAKED",
                        "required_packet_ids": "P_DRAFT_PREREQ_NOT_BAKED",
                        "primary_surface": "in_game_wiki",
                        "world_object_hint": "poi.draft",
                        "player_question": "Draft question?",
                        "truth_payload": "Draft claim.",
                        "replay_axis": "draft_axis",
                        "ending_pressure": "none",
                    }
                ],
            )

            stats = audit_selected_packets(root, (), include_all=True)

            self.assertEqual(stats.target_packets, 1)
            self.assertEqual(stats.route_cards, 1)

    def test_inventory_counts_canonical_ready_unbaked_packets(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            base = root / "Docs" / "Lore" / "AppliedContent"
            packet = {
                "packet_id": "P_TEST_UNBAKED",
                "release_set_id": "RS_UNBAKED",
                "article_id": "test.unbaked",
                "localized": {locale: localized_row() for locale in TARGET_LOCALES},
            }
            packet_path = base / "packets" / "RS_UNBAKED.packets.json"
            packet_path.write_text(
                json.dumps({"release_set_id": "RS_UNBAKED", "packets": [packet]}),
                encoding="utf-8",
            )
            manifest_path = base / "release_sets" / "RS_UNBAKED_manifest.json"
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_UNBAKED",
                        "packets": ["P_TEST_UNBAKED"],
                        "packet_sources": [packet_path.as_posix()],
                        "canonical_importer_ready": True,
                    }
                ),
                encoding="utf-8",
            )

            stats = inventory_sources(root)

            self.assertEqual(stats.source_packets, 2)
            self.assertEqual(stats.baked_packets, 1)
            self.assertEqual(stats.canonical_ready_unbaked_packets, 1)
            self.assertEqual(stats.sample_canonical_ready_unbaked, ("P_TEST_UNBAKED",))

    def test_all_audits_only_canonical_ready_packets_by_default(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            base = root / "Docs" / "Lore" / "AppliedContent"
            packet = {
                "packet_id": "P_TEST_NOT_READY",
                "release_set_id": "RS_NOT_READY",
                "article_id": "test.not_ready",
                "localized": {locale: localized_row() for locale in TARGET_LOCALES},
            }
            packet_path = base / "packets" / "RS_NOT_READY.packets.json"
            packet_path.write_text(
                json.dumps({"release_set_id": "RS_NOT_READY", "packets": [packet]}),
                encoding="utf-8",
            )
            manifest_path = base / "release_sets" / "RS_NOT_READY_manifest.json"
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_NOT_READY",
                        "packets": ["P_TEST_NOT_READY"],
                        "packet_sources": [packet_path.as_posix()],
                        "canonical_importer_ready": False,
                    }
                ),
                encoding="utf-8",
            )

            stats = audit_selected_packets(root, (), include_all=True)

            self.assertEqual(stats.source_packets, 2)
            self.assertEqual(stats.target_packets, 1)

    def test_all_rejects_stale_baked_packet_outside_canonical_selection(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            baked_path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
            with baked_path.open("r", encoding="utf-8", newline="") as handle:
                reader = csv.DictReader(handle)
                headers = tuple(reader.fieldnames or ())
                rows = list(reader)
            stale_row = dict(rows[0])
            stale_row["packet_id"] = "P_STALE_EXTRA"
            rows.append(stale_row)
            write_csv(baked_path, headers, rows)

            with self.assertRaisesRegex(AppliedLoreCoverageError, "outside canonical importer selection"):
                audit_selected_packets(root, (), include_all=True)

    def test_inventory_rejects_canonical_ready_manifest_source_mismatch(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            base = root / "Docs" / "Lore" / "AppliedContent"
            packet = {
                "packet_id": "P_TEST_PRESENT",
                "release_set_id": "RS_MISMATCH",
                "article_id": "test.present",
                "localized": {locale: localized_row() for locale in TARGET_LOCALES},
            }
            packet_path = base / "packets" / "RS_MISMATCH.packets.json"
            packet_path.write_text(
                json.dumps({"release_set_id": "RS_MISMATCH", "packets": [packet]}),
                encoding="utf-8",
            )
            manifest_path = base / "release_sets" / "RS_MISMATCH_manifest.json"
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_MISMATCH",
                        "packets": ["P_TEST_PRESENT", "P_TEST_MISSING"],
                        "packet_sources": [packet_path.as_posix()],
                        "canonical_importer_ready": True,
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(
                AppliedLoreCoverageError,
                "Canonical ready manifest/source mismatch: .*P_TEST_MISSING",
            ):
                inventory_sources(root)


if __name__ == "__main__":
    unittest.main()
